using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace AshinaStudio
{
    public static class FileDialogs
    {
        // Every field is blittable. Mono's nested StringBuilder marshalling crashed the old dialog.
        [StructLayout(LayoutKind.Sequential)]
        struct OpenFileName
        {
            public uint size;
            public IntPtr owner, instance, filter, customFilter;
            public uint maxCustomFilter, filterIndex;
            public IntPtr file;
            public uint maxFile;
            public IntPtr fileTitle;
            public uint maxFileTitle;
            public IntPtr initialDir, title;
            public uint flags;
            public ushort fileOffset, fileExtension;
            public IntPtr defExt, custData, hook, template, reserved;
            public uint reserved2, flagsEx;
        }

        [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool NativeOpen(ref OpenFileName file);

        [DllImport("comdlg32.dll", EntryPoint = "GetSaveFileNameW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool NativeSave(ref OpenFileName file);

        [DllImport("comdlg32.dll", ExactSpelling = true)]
        static extern uint CommDlgExtendedError();

        [DllImport("ole32.dll", ExactSpelling = true)]
        static extern int CoInitializeEx(IntPtr reserved, uint options);

        [DllImport("ole32.dll", ExactSpelling = true)]
        static extern void CoUninitialize();

        static int active;
        static string lastDirectory;
        public static bool IsOpen => Volatile.Read(ref active) != 0;
        public static int NativeStructureSize => Marshal.SizeOf<OpenFileName>();

        public static Task<string> OpenAsync(bool model = false) => ShowAsync(false, model);
        public static Task<string> OpenRobotCodeAsync()=>ShowAsync(false,false,"WPILib projesi (build.gradle)\0build.gradle\0Tüm dosyalar\0*.*\0\0");
        public static Task<string> OpenPathPlannerAsync()=>ShowAsync(false,false,"PathPlanner (*.auto, *.path)\0*.auto;*.path\0\0");
        public static Task<string> OpenReplayAsync()=>ShowAsync(false,false,"Ashina oturum kaydı\0*.ashina-replay;*.ashina-replay.partial\0\0");
        public static Task<string> SaveAsync() => ShowAsync(true, false);

        static Task<string> ShowAsync(bool save, bool model,string customFilter=null)
        {
            if (Interlocked.CompareExchange(ref active, 1, 0) != 0)
                return Task.FromException<string>(new InvalidOperationException("Önce açık olan dosya penceresini kapatın."));

            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            IntPtr owner = IntPtr.Zero;
            try
            {
                using (var process = System.Diagnostics.Process.GetCurrentProcess())
                    owner = process.MainWindowHandle;

                var thread = new Thread(() =>
                {
                    try { completion.TrySetResult(ShowNative(owner, save, model,customFilter)); }
                    catch (Exception error) { completion.TrySetException(error); }
                    finally { Interlocked.Exchange(ref active, 0); }
                });
                thread.Name = "Ashina dosya seçimi";
                thread.IsBackground = true;
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
            catch (Exception error)
            {
                Interlocked.Exchange(ref active, 0);
                completion.TrySetException(error);
            }
            return completion.Task;
        }

        static string ShowNative(IntPtr owner, bool save, bool model,string customFilter)
        {
            int com = CoInitializeEx(IntPtr.Zero, 2);
            if (com < 0) Marshal.ThrowExceptionForHR(com);
            IntPtr buffer = IntPtr.Zero, filter = IntPtr.Zero, title = IntPtr.Zero;
            IntPtr directory = IntPtr.Zero, extension = IntPtr.Zero;
            try
            {
                const int capacity = 32768;
                buffer = Marshal.AllocHGlobal(capacity * sizeof(char));
                Marshal.Copy(new byte[capacity * sizeof(char)], 0, buffer, capacity * sizeof(char));
                if (save)
                {
                    char[] filename = "Robot.ashina.json\0".ToCharArray();
                    Marshal.Copy(filename, 0, buffer, filename.Length);
                }
                filter = Marshal.StringToHGlobalUni(customFilter??(model
                    ? "3B robot modeli (GLB, STL, OBJ)\0*.glb;*.stl;*.obj\0Tüm dosyalar\0*.*\0\0"
                    : "Robot / Blockly projesi (JSON)\0*.json\0\0"));
                title = Marshal.StringToHGlobalUni(customFilter!=null ? "WPILib build.gradle dosyasını seç" : save ? "Robot projesini kaydet" : model ? "Robot modeli seç" : "Robot projesi aç");
                string initial = lastDirectory;
                if (string.IsNullOrEmpty(initial) || !Directory.Exists(initial))
                    initial = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                directory = Marshal.StringToHGlobalUni(initial);
                extension = Marshal.StringToHGlobalUni(save ? "json" : "");
                var info = new OpenFileName
                {
                    size = (uint)NativeStructureSize, owner = owner, file = buffer, maxFile = capacity,
                    filter = filter, filterIndex = 1, title = title, initialDir = directory, defExt = extension,
                    flags = 0x80000 | 0x800 | 0x8 | (save ? 0x2u : 0x1000u)
                };
                bool selected = save ? NativeSave(ref info) : NativeOpen(ref info);
                if (!selected)
                {
                    uint error = CommDlgExtendedError();
                    if (error != 0) throw new IOException("Dosya seçme penceresi açılamadı (" + error.ToString("X") + ").");
                    return null;
                }
                string path = Marshal.PtrToStringUni(buffer);
                if (!string.IsNullOrEmpty(path)) lastDirectory = Path.GetDirectoryName(path);
                return path;
            }
            finally
            {
                foreach (var pointer in new[] { buffer, filter, title, directory, extension })
                    if (pointer != IntPtr.Zero) Marshal.FreeHGlobal(pointer);
                CoUninitialize();
            }
        }
    }
}
