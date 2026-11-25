using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AshinaStudio {
 internal sealed class ReplayOutcome {
  internal string Path,Error;
  internal int Frames;
  internal bool Complete;
 }

 internal sealed class SessionRecording {
  readonly BlockingCollection<(double time,ReplayFrame frame)> pending=new BlockingCollection<(double,ReplayFrame)>(48);
  readonly ReplayHeader header;
  readonly string destination;
  readonly Task<ReplayOutcome> result;
  volatile string error;
  double lastTime=-1;
  internal bool Recording=>!pending.IsAddingCompleted;
  internal Task<ReplayOutcome> Result=>result;

  internal SessionRecording(string directory,ReplayHeader info){
   info.Validate();header=info;
   string stamp=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture);
   string safe=new string(info.name.Where(c=>char.IsLetterOrDigit(c)||c==' '||c=='-'||c=='_').Take(44).ToArray()).Trim();
   if(safe.Length==0)safe="Ashina";
   destination=Path.Combine(directory,stamp+"-"+safe+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+ReplayFormat.Extension);
   result=Task.Run(Write);
  }

  internal bool Capture(double time,ReplayFrame frame){
   if(!Recording||time<=lastTime)return false;
   if(time>ReplayFormat.MaxSeconds){Stop();return false;}
   try{
    if(pending.TryAdd((time,frame))){lastTime=time;return true;}
    error="Disk kaydı kareleri zamanında yazamadı.";
    Stop();
   }catch(InvalidOperationException){}
   return false;
  }
  internal Task<ReplayOutcome> Stop(){if(!pending.IsAddingCompleted)pending.CompleteAdding();return result;}

  ReplayOutcome Write(){
   string partial=destination+".partial";
   var outcome=new ReplayOutcome{Path=partial};
   double time=0;int events=0;long bytes=0;
   ReplayFrame previous=null;
   try{
    Directory.CreateDirectory(Path.GetDirectoryName(destination));
    using(var file=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.Read,64*1024,FileOptions.SequentialScan))
    using(var writer=new StreamWriter(file,ReplayFormat.Utf8,64*1024)){
     WriteLine(writer,ReplayFormat.Record('H',header),ref bytes);
     writer.Flush();
     foreach(var capture in pending.GetConsumingEnumerable()){
      if(outcome.Frames>=ReplayFormat.MaxFrames){error="Kare sınırında kayıt durduruldu.";break;}
      capture.frame.Validate();
      WriteLine(writer,ReplayFormat.Record('F',capture.frame,capture.time),ref bytes);
      outcome.Frames++;time=capture.time;
      if(events<ReplayFormat.MaxEvents)foreach(var item in EventsBetween(previous,capture.frame)){
       if(events>=ReplayFormat.MaxEvents)break;
       WriteLine(writer,ReplayFormat.Record('E',item,capture.time),ref bytes);events++;
      }
      previous=capture.frame;
      if(outcome.Frames%10==0)writer.Flush();
     }
     if(error==null)WriteLine(writer,ReplayFormat.Record('Z',new ReplayFooter{frames=outcome.Frames,duration=time}),ref bytes);
     writer.Flush();
    }
    if(error==null){File.Move(partial,destination);outcome.Path=destination;outcome.Complete=true;}
   }catch(Exception failure){error=failure.Message;}
   finally{if(!pending.IsAddingCompleted)pending.CompleteAdding();}
   outcome.Error=error;
   return outcome;
  }

  static void WriteLine(StreamWriter writer,string line,ref long bytes){
   int length=ReplayFormat.Utf8.GetByteCount(line);
   if(length>ReplayFormat.MaxRowBytes||bytes+length+2>ReplayFormat.MaxBytes)throw new IOException("Kayıt dosyası boyut sınırına ulaştı.");
   writer.WriteLine(line);bytes+=length+2;
  }

  static IEnumerable<ReplayEvent> EventsBetween(ReplayFrame before,ReplayFrame after){
   if(before?.phase!=after.phase)yield return new ReplayEvent{kind="phase",label=Phase(after.phase)};
   if(before==null)yield break;
   if(before.blue!=after.blue||before.red!=after.red)
    yield return new ReplayEvent{kind="score",label="Skor · Mavi "+after.blue+"  Kırmızı "+after.red};
   foreach(var robot in after.robots){
    var last=Array.Find(before.robots,r=>r.id==robot.id);
    if(last==null){yield return new ReplayEvent{kind="joined",actor=robot.id,label=robot.name+" sahaya katıldı"};continue;}
    if(robot.shots!=last.shots)yield return new ReplayEvent{kind="shot",actor=robot.id,label=robot.name+" · atış "+robot.shots};
    foreach(var entry in robot.stock){
     last.stock.TryGetValue(entry.Key,out int previous);
     if(entry.Value!=previous)yield return new ReplayEvent{kind="stock",actor=robot.id,label=robot.name+" · "+PieceCatalog.Names[entry.Key]+" "+entry.Value};
    }
   }
   foreach(var robot in before.robots)
    if(!Array.Exists(after.robots,r=>r.id==robot.id))yield return new ReplayEvent{kind="left",actor=robot.id,label=robot.name+" sahadan ayrıldı"};
   var earlier=new HashSet<int>(before.pieces.Select(p=>p.id));var current=new HashSet<int>(after.pieces.Select(p=>p.id));
   var added=current.Where(id=>!earlier.Contains(id)).OrderBy(id=>id).ToArray();
   var removed=earlier.Where(id=>!current.Contains(id)).OrderBy(id=>id).ToArray();
   if(added.Length>0||removed.Length>0)yield return new ReplayEvent{kind="pieces",label="Oyun eşyaları · +"+added.Length+" / −"+removed.Length,added=added,removed=removed};
  }

  static string Phase(string phase)=>phase=="lobby"?"Lobi":phase=="countdown"?"Geri sayım":phase=="autonomous"?"Otonom":phase=="teleop"?"Sürücü kontrolü":phase=="transition"?"Ara":phase=="finished"?"Maç bitti":"Düzenleme";
 }
}
