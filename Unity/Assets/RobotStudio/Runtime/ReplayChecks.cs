using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace AshinaStudio {
 public sealed partial class FeatureChecks {
  async Task CheckReplaySessions(){
   string directory=Path.Combine(Output,"session-recordings");
   App.RobotMode="teleop";
   var project=App.Project;
   project.training.unlimitedShots=false;project.inventory["fuel"]=1;
   App.Pieces.Clear();
   var staged=App.Pieces.Spawn("fuel",Frc.Position(new[]{7.0,5,.08}),Vector3.zero,Quaternion.identity);
   Check(App.StartSessionRecording(directory,"Physical practice",true),"A real practice recording starts with local input recording configured");
   App.SetRunning(true);App.CheckVelocity=Vector3.forward*1.1f;
   await Task.Delay(480);
   App.Pieces.Remove(staged);
   Check(App.Emit(project.actions.Find(a=>a.type=="emit"))!=null,"A physical shot takes place during recording");
   await Task.Delay(700);
   App.CheckVelocity=Vector3.zero;App.SetRunning(false);
   var outcome=await App.StopSessionRecording();
   Check(outcome.Complete&&outcome.Frames>=8,"A short live session writes and finalizes an actual recording");
   var index=await Task.Run(()=>ReplayIndex.Open(outcome.Path));
   Check(index.Complete&&index.Header.season==project.season&&index.Header.projectName==project.name&&index.Header.mode=="practice","The saved session retains field and robot metadata");
   Check(index.Events.Any(x=>x.item.kind=="shot")&&index.Events.Any(x=>x.item.kind=="stock")&&index.Events.Any(x=>x.item.kind=="pieces"),"The event index contains shot, inventory and piece lifecycle changes");
   var track=await Task.Run(()=>index.ReadPair(Math.Min(4,index.Frames.Count-1)));
   Check(track.before.robots[0].parts.Length>0&&track.before.input!=null&&track.before.robots[0].v.Length==3,"A recorded frame includes robot velocity, mechanism transform and permitted controller input");
   Vector3 live=App.Robot.position;int stock=App.Stored("fuel");int livePieces=App.Pieces.Active.Count;
   Check(await App.OpenReplayFile(outcome.Path)&&App.ReplayVisualsOnly,"Replay opens as a visual-only world without game physics");
   double seek=index.Duration*.65;App.SeekReplay(seek);
   await Wait(()=>App.CurrentReplayFrame==index.FrameAt(seek),"The replay timeline seeks to an indexed frame");
   App.SetReplayOverhead(true);
   Check(App.ReplayOverhead,"The replay switches to its overhead camera");
   App.SetReplayOverhead(false);App.SeekReplay(0);App.SpeedReplay(.5f);App.PlayReplay();
   await Task.Delay(160);App.PauseReplay();double slow=App.ReplayPosition;
   await Task.Delay(110);
   Check(slow>.02&&slow<.20&&Math.Abs(App.ReplayPosition-slow)<.001,"Replay pause keeps its position and half-speed playback advances");
   App.SeekReplay(0);App.SpeedReplay(2);App.PlayReplay();await Task.Delay(160);App.PauseReplay();
   Check(App.ReplayPosition>slow*2&&!App.ReplayPlaying,"Restart and faster playback change the timeline rate");
   App.CloseReplay();
   Check(!App.ReplayOpen&&Vector3.Distance(App.Robot.position,live)<.08&&App.Stored("fuel")==stock&&App.Pieces.Active.Count==livePieces,"Closing playback leaves the live robot and inventory intact");

   var lines=File.ReadAllLines(outcome.Path);
   string incomplete=Path.Combine(directory,"Interrupted.ashina-replay.partial");
   File.WriteAllText(incomplete,string.Join("\n",lines.Take(Math.Max(3,lines.Length-3)))+"\nF\t0.3\t{",ReplayFormat.Utf8);
   var prefix=await Task.Run(()=>ReplayIndex.Open(incomplete));
   Check(!prefix.Complete&&prefix.Frames.Count>0&&await App.OpenReplayFile(incomplete),"A truncated last row keeps all earlier complete frames playable");
   App.CloseReplay();

   var fixture=new ReplayFrame{phase="disabled",robots=track.before.robots};
   var missingAsset=new ReplayHeader{name="Long practice without CAD",season=project.season,layout=project.tagLayout,mode="practice",
    projectName="Unavailable robot CAD",localRobot="local",createdUtc=DateTime.UtcNow.ToString("O"),robots=index.Header.robots};
   var longWriter=new SessionRecording(directory,missingAsset);
   for(int i=0;i<420;i++){
    if(i%8==0)await Task.Delay(3);
    if(!longWriter.Capture(i*.1,fixture))throw new InvalidOperationException("Long recording producer stalled.");
   }
   var longFinish=await longWriter.Stop();
   var longer=await Task.Run(()=>ReplayIndex.Open(longFinish.Path));
   Check(longer.Frames.Count==420&&longer.Duration>40&&longer.FrameAt(27.52)==275,"A longer session keeps a bounded index and seeks without loading every frame");
   Check(await App.OpenReplayFile(longFinish.Path)&&App.ReplayVisualsOnly,"A recording with no robot asset can still open as a visual session");
   App.CloseReplay();

   var emptyWriter=new SessionRecording(directory,index.Header);
   var empty=await emptyWriter.Stop();
   Check(ReplayIndex.Open(empty.Path).Complete&&!await App.OpenReplayFile(empty.Path),"Empty recordings are recognized and cannot start playback");
   var bad=Path.Combine(directory,"Invalid.ashina-replay");
   File.WriteAllText(bad,"H\t{}\n");
   Check(!await App.OpenReplayFile(bad)&&Directory.GetFiles(directory,"*.ashina-replay").Length>=3,"Malformed clips are rejected and multiple sessions are kept separately");
  }
 }
}
