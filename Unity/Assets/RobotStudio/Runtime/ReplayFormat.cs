using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace AshinaStudio {
 [Serializable] internal sealed class ReplayHeader {
  public string format="ashina-replay",name,createdUtc,season,layout,mode,projectName,localRobot;
  public int version=1;
  public ReplayRobotInfo[] robots=Array.Empty<ReplayRobotInfo>();
  internal void Validate(){
   if(format!="ashina-replay"||version!=1||string.IsNullOrWhiteSpace(name)||name.Length>100
     ||!DateTime.TryParse(createdUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out _)
     ||!new[]{"practice","2024","2025","2026"}.Contains(season)||!new[]{"welded","andymark"}.Contains(layout)
     ||(mode!="practice"&&mode!="online")||projectName==null||projectName.Length>160||localRobot==null||localRobot.Length>128
     ||robots==null||robots.Length>6)throw new InvalidDataException("Kayıt üst bilgisi geçersiz.");
   foreach(var robot in robots)if(robot==null||string.IsNullOrEmpty(robot.id)||robot.id.Length>128||!MatchProtocol.Valid(robot.size,3,8)||robot.size.Any(x=>x<.05f))throw new InvalidDataException("Kayıttaki robot boyutu geçersiz.");
  }
 }
 [Serializable] internal sealed class ReplayRobotInfo {public string id;public float[] size;}
 [Serializable] internal sealed class ReplayInput {public double[] axes;public long buttons;public int pov;}
 [Serializable] internal sealed class ReplayFrame {
  public string phase;
  public int blue,red;
  public MatchRobotState[] robots=Array.Empty<MatchRobotState>();
  public MatchPieceState[] pieces=Array.Empty<MatchPieceState>();
  public ReplayInput input;
  internal void Validate(){
   if(phase==null||phase.Length>32||blue<0||red<0||robots==null||robots.Length>6||pieces==null||pieces.Length>600)throw new InvalidDataException("Kayıt karesi geçersiz.");
   var ids=new HashSet<string>();var partIds=new HashSet<string>();
   foreach(var robot in robots){
    if(robot==null||string.IsNullOrEmpty(robot.id)||robot.id.Length>128||!ids.Add(robot.id)||robot.name==null||robot.name.Length>160
      ||!MatchProtocol.Valid(robot.p,3,60)||!MatchProtocol.ValidRotation(robot.q)||!MatchProtocol.Valid(robot.v,3,100)
      ||robot.parts==null||robot.parts.Length>128||robot.stock==null||robot.stock.Count>5||robot.stock.Any(s=>!PieceCatalog.Names.ContainsKey(s.Key)||s.Value<0||s.Value>1000))
     throw new InvalidDataException("Kayıt robot durumu geçersiz.");
    partIds.Clear();
    foreach(var part in robot.parts)if(part==null||string.IsNullOrEmpty(part.id)||part.id.Length>128||!partIds.Add(part.id)||!MatchProtocol.Valid(part.p,3,40)||!MatchProtocol.ValidRotation(part.q))throw new InvalidDataException("Kayıt mekanizma durumu geçersiz.");
   }
   var pieceIds=new HashSet<int>();
   foreach(var piece in pieces)if(piece==null||piece.id<0||!pieceIds.Add(piece.id)||!PieceCatalog.Names.ContainsKey(piece.kind)||!MatchProtocol.Valid(piece.p,3,100)||!MatchProtocol.Valid(piece.v,3,250)||!MatchProtocol.ValidRotation(piece.q))throw new InvalidDataException("Kayıt oyun eşyası geçersiz.");
   if(input!=null&&(input.axes==null||input.axes.Length>8||input.axes.Any(x=>!StudioApp.Finite(x)||Math.Abs(x)>1.01)||input.pov< -1||input.pov>360))throw new InvalidDataException("Kayıt kumanda verisi geçersiz.");
  }
 }
 [Serializable] internal sealed class ReplayEvent {
  public string kind,actor,label;
  public int[] added,removed;
 }
 [Serializable] internal sealed class ReplayFooter {public int frames;public double duration;}
 internal readonly struct ReplayOffset {
  internal readonly double Time;internal readonly long Position;
  internal ReplayOffset(double time,long position){Time=time;Position=position;}
 }

 internal static class ReplayFormat {
  internal const int MaxRowBytes=1024*1024,MaxFrames=18001,MaxEvents=6000;
  internal const long MaxBytes=350L*1024*1024;
  internal const double MaxSeconds=1800;
  internal const string Extension=".ashina-replay";
  internal static readonly Encoding Utf8=new UTF8Encoding(false,true);
  internal static string Record<T>(char type,T data,double? at=null){
   string prefix=type+"\t";
   if(at.HasValue)prefix+=at.Value.ToString("0.000000",CultureInfo.InvariantCulture)+"\t";
   return prefix+MatchProtocol.Encode(data);
  }
  internal static T Parse<T>(string json) where T:class{
   var value=JsonConvert.DeserializeObject<T>(json,ProjectIO.Settings);
   if(value==null)throw new InvalidDataException("Kayıt satırı boş.");
   return value;
  }
 }

 internal sealed class ReplayLines : IDisposable {
  readonly Stream file;
  readonly byte[] buffer=ArrayPool<byte>.Shared.Rent(64*1024);
  readonly byte[] line=ArrayPool<byte>.Shared.Rent(ReplayFormat.MaxRowBytes);
  int from,available,length,jsonStart;
  long cursor;
  internal long Offset {get;private set;}
  internal char Type {get;private set;}
  internal double Time {get;private set;}
  internal bool Truncated {get;private set;}
  internal string Json=>ReplayFormat.Utf8.GetString(line,jsonStart,length-jsonStart);
  internal ReplayLines(Stream source){file=source;cursor=source.Position;}

  internal bool Next(){
   Offset=cursor;length=0;Truncated=false;
   while(true){
    if(from==available){
     from=0;available=file.Read(buffer,0,Math.Min(buffer.Length,64*1024));
     if(available==0){Truncated=length>0;return false;}
    }
    int ending=Array.IndexOf(buffer,(byte)'\n',from,available-from);
    int count=(ending<0?available:ending)-from;
    if(length+count>ReplayFormat.MaxRowBytes)throw new InvalidDataException("Kayıt satırı boyut sınırı aşıldı.");
    Buffer.BlockCopy(buffer,from,line,length,count);length+=count;from+=count;cursor+=count;
    if(ending<0)continue;
    from++;cursor++;
    if(length>0&&line[length-1]=='\r')length--;
    break;
   }
   if(length<3||line[1]!='\t')throw new InvalidDataException("Kayıt satırı biçimi geçersiz.");
   Type=(char)line[0];jsonStart=2;Time=0;
   if(Type=='F'||Type=='E'){
    int tab=Array.IndexOf(line,(byte)'\t',2,Math.Min(length-2,29));
    if(tab<0||!double.TryParse(Encoding.ASCII.GetString(line,2,tab-2),NumberStyles.Float,CultureInfo.InvariantCulture,out var time)||!StudioApp.Finite(time)||time<0||time>ReplayFormat.MaxSeconds+.1)
     throw new InvalidDataException("Kayıt zaman damgası geçersiz.");
    Time=time;jsonStart=tab+1;
   }
   return true;
  }

  public void Dispose(){ArrayPool<byte>.Shared.Return(buffer);ArrayPool<byte>.Shared.Return(line);}
 }

 internal sealed class ReplayIndex {
  internal readonly string Path;
  internal readonly ReplayHeader Header;
  internal readonly List<ReplayOffset> Frames=new List<ReplayOffset>();
  internal readonly List<(double time,ReplayEvent item)> Events=new List<(double,ReplayEvent)>();
  internal bool Complete {get;private set;}
  internal double Duration=>Frames.Count==0?0:Frames[Frames.Count-1].Time;
  ReplayIndex(string path,ReplayHeader header){Path=path;Header=header;}

  internal static ReplayIndex Open(string path,CancellationToken stop=default){
   using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
   using(var reader=new ReplayLines(stream)){
    if(stream.Length>ReplayFormat.MaxBytes+ReplayFormat.MaxRowBytes*2||!reader.Next()||reader.Type!='H')throw new InvalidDataException("Ashina kayıt dosyası açılamadı.");
    var header=ReplayFormat.Parse<ReplayHeader>(reader.Json);header.Validate();
    var index=new ReplayIndex(path,header);
    while(reader.Next()){
     stop.ThrowIfCancellationRequested();
     switch(reader.Type){
      case 'F':
       if(index.Frames.Count>=ReplayFormat.MaxFrames||index.Frames.Count>0&&reader.Time<=index.Duration)throw new InvalidDataException("Kayıt kare sırası geçersiz.");
       if(index.Frames.Count==0)ReplayFormat.Parse<ReplayFrame>(reader.Json).Validate();
       index.Frames.Add(new ReplayOffset(reader.Time,reader.Offset));
       break;
      case 'E':
       if(index.Events.Count>=ReplayFormat.MaxEvents)throw new InvalidDataException("Kayıt olay sınırı aşıldı.");
       var item=ReplayFormat.Parse<ReplayEvent>(reader.Json);
       if(item.kind==null||item.kind.Length>30||item.label==null||item.label.Length>220||item.actor?.Length>128||item.added?.Length>600||item.removed?.Length>600)throw new InvalidDataException("Kayıt olayı geçersiz.");
       index.Events.Add((reader.Time,item));
       break;
      case 'Z':
       var footer=ReplayFormat.Parse<ReplayFooter>(reader.Json);
       if(footer.frames!=index.Frames.Count||!StudioApp.Finite(footer.duration)||Math.Abs(footer.duration-index.Duration)>.001||reader.Next()||reader.Truncated)throw new InvalidDataException("Kayıt sonu geçersiz.");
       index.Complete=true;return index;
      default:throw new InvalidDataException("Bilinmeyen kayıt satırı.");
     }
    }
    return index;
   }
  }

  internal int FrameAt(double time){
   int left=0,right=Frames.Count-1;
   while(left<right){int middle=(left+right+1)/2;if(Frames[middle].Time>time)right=middle-1;else left=middle;}
   return left;
  }

  internal (ReplayFrame before,ReplayFrame after) ReadPair(int index){
   if(index<0||index>=Frames.Count)throw new InvalidDataException("Kayıt karesi bulunamadı.");
   using(var stream=new FileStream(Path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){
    var first=ReadFrame(stream,index);
    return (first,index+1<Frames.Count?ReadFrame(stream,index+1):first);
   }
  }
  ReplayFrame ReadFrame(Stream stream,int index){
   stream.Position=Frames[index].Position;
   using(var reader=new ReplayLines(stream)){
    if(!reader.Next()||reader.Type!='F')throw new InvalidDataException("Kayıt karesi eksik.");
    var frame=ReplayFormat.Parse<ReplayFrame>(reader.Json);frame.Validate();return frame;
   }
  }
 }
}
