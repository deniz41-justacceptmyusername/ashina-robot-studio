using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AshinaStudio {
 public sealed class RobotRunner : IDisposable {
  Process process;IntPtr job;readonly ConcurrentQueue<string> lines=new ConcurrentQueue<string>();
  public string Status="Robot kodu başlatılmadı";
  public bool Running {get{try{return process!=null&&!process.HasExited;}catch{return false;}}}
  public string Log=>string.Join("\n",lines.ToArray());
  void Line(string value){if(value==null)return;lines.Enqueue(value);while(lines.Count>150)lines.TryDequeue(out _);}
  public static string FindJava() {
   string home=Environment.GetEnvironmentVariable("JAVA_HOME");if(!string.IsNullOrEmpty(home)&&File.Exists(Path.Combine(home,"bin/java.exe")))return Path.Combine(home,"bin/java.exe");
   string root=Path.Combine(Environment.GetEnvironmentVariable("PUBLIC")??@"C:\Users\Public","wpilib");
   if(Directory.Exists(root))foreach(var dir in Directory.GetDirectories(root).OrderByDescending(x=>x))if(File.Exists(Path.Combine(dir,"jdk/bin/java.exe")))return Path.Combine(dir,"jdk/bin/java.exe");
   throw new IOException("WPILib JDK bulunamadı. WPILib kurulumu veya JAVA_HOME gerekli.");
  }
  public void Start(string directory,int port) {
   if(Running)throw new InvalidOperationException("Robot kodu zaten çalışıyor.");
   directory=Path.GetFullPath(directory);string wrapper=Path.Combine(directory,"gradle/wrapper/gradle-wrapper.jar");
   if(!File.Exists(Path.Combine(directory,"build.gradle"))||!File.Exists(wrapper))throw new IOException("build.gradle ve Gradle wrapper içeren WPILib projesi seçin.");
   string java=FindJava();job=CreateJobObject(IntPtr.Zero,null);if(job==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
   var info=new JobLimits();info.Basic.Flags=0x2000;
   if(!SetInformationJobObject(job,9,ref info,(uint)Marshal.SizeOf<JobLimits>())){CloseHandle(job);job=IntPtr.Zero;throw new System.ComponentModel.Win32Exception();}
   var start=new ProcessStartInfo{FileName=java,WorkingDirectory=directory,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,
    Arguments="-Dorg.gradle.appname=gradlew -classpath \""+wrapper+"\" org.gradle.wrapper.GradleWrapperMain --no-daemon --console=plain -PashinaStudio=true -PashinaNtPort="+port+" simulateJava"};
   start.EnvironmentVariables["JAVA_TOOL_OPTIONS"]="-Dashina.nt.port="+port;
   start.EnvironmentVariables["JAVA_HOME"]=Path.GetDirectoryName(Path.GetDirectoryName(java));
   process=new Process{StartInfo=start,EnableRaisingEvents=true};process.OutputDataReceived+=(s,e)=>Line(e.Data);process.ErrorDataReceived+=(s,e)=>Line(e.Data);
   process.Exited+=(s,e)=>{try{Status="Robot kodu kapandı · "+process.ExitCode;}catch{}};
   if(!process.Start()){Dispose();throw new IOException("WPILib başlatılamadı.");}
   if(!AssignProcessToJobObject(job,process.Handle)){try{process.Kill();}catch{}Dispose();throw new IOException("Robot alt işlemi izlenemedi.");}
   process.BeginOutputReadLine();process.BeginErrorReadLine();Status="WPILib hazırlanıyor / çalışıyor";
  }
  public void Dispose() {
   // The job contains only this launch (--no-daemon), including its robot JVM.
   if(job!=IntPtr.Zero){CloseHandle(job);job=IntPtr.Zero;}
   process?.Dispose();process=null;Status="Robot kodu durduruldu";
  }
  [StructLayout(LayoutKind.Sequential)]struct BasicLimits{public long ProcessTime,JobTime;public uint Flags;public UIntPtr Minimum,Maximum;public uint Active;public UIntPtr Affinity;public uint Priority,Scheduling;}
  [StructLayout(LayoutKind.Sequential)]struct IoCounters{public ulong ReadOps,WriteOps,OtherOps,ReadBytes,WriteBytes,OtherBytes;}
  [StructLayout(LayoutKind.Sequential)]struct JobLimits{public BasicLimits Basic;public IoCounters Io;public UIntPtr ProcessMemory,JobMemory,PeakProcessMemory,PeakJobMemory;}
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateJobObject(IntPtr attributes,string name);
  [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool SetInformationJobObject(IntPtr job,int type,ref JobLimits info,uint size);
  [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
  [DllImport("kernel32.dll")][return:MarshalAs(UnmanagedType.Bool)]static extern bool CloseHandle(IntPtr handle);
 }
 public partial class StudioApp {
  public string RobotCodeLog=>robotRunner?.Log??"";
  RobotRunner robotRunner;bool showRobotLog;Vector2 robotLogScroll;
  static int FreeRobotPort(int preferred){for(int candidate=preferred;candidate<Math.Min(65535,preferred+25);candidate++){if(candidate==5818)continue;try{var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,candidate);listener.Start();listener.Stop();return candidate;}catch(System.Net.Sockets.SocketException){}}throw new IOException("Boş yerel NT portu bulunamadı.");}
  public void StartRobotCode() {
   try{if(robotRunner?.Running==true)return;robotRunner?.Dispose();robotRunner=new RobotRunner();string path=Project.network.robotProject;if(!Path.IsPathRooted(path))path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../",path));Project.network.port=FreeRobotPort(Project.network.port);Project.network.host="127.0.0.1";robotRunner.Start(path,Project.network.port);ConnectNetwork();Notice="WPILib robot kodu başlatılıyor. Bağlantı otomatik kurulacak.";}
   catch(Exception error){Notice="Robot kodu başlatılamadı: "+error.Message;}
  }
  async void PickRobotCode() {
   try{string file=await FileDialogs.OpenRobotCodeAsync();if(file!=null){if(Path.GetFileName(file)!="build.gradle")throw new IOException("WPILib projesinin build.gradle dosyasını seçin.");Project.network.robotProject=Path.GetDirectoryName(file);Notice="Robot kodu seçildi. Başlat ile simülasyonu çalıştırabilirsin.";}}
   catch(Exception error){Notice=error.Message;}
  }
  void RobotCodeControls() {
   Text("ROBOT KODU",true);Project.network.robotProject=Edit("robotProject",Project.network.robotProject);
   GUILayout.BeginHorizontal();if(Button("Proje seç"))PickRobotCode();if(Button(robotRunner?.Running==true?"Kodu durdur":"Kodu başlat")){if(robotRunner?.Running==true){SetRunning(false);robotRunner.Dispose();}else StartRobotCode();}GUILayout.EndHorizontal();
   Text(robotRunner?.Status??"WPILib'den başlatılan koda doğrudan bağlanabilir veya burada Java projesi seçebilirsin.",true);
   if(robotRunner!=null){showRobotLog=GUILayout.Toggle(showRobotLog," Robot günlüğünü göster");if(showRobotLog){robotLogScroll=GUILayout.BeginScrollView(robotLogScroll,GUILayout.Height(120));Text(robotRunner.Log,true);GUILayout.EndScrollView();}}
  }
 }
}
