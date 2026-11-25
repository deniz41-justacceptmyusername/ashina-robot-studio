using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace AshinaStudio {
 public partial class StudioApp {
  SessionRecording recorder;
  SessionRecording finalizingRecorder;
  double recordStart,nextRecordSample;
  bool recordOnline;
  bool recordInput,recordingsVisible;
  string recordName="",recordStatus="";
  Vector2 recordingScroll;
  string[] savedRecordings=Array.Empty<string>();
  ReplayIndex replayIndex;
  ReplayPresentation replayView;
  FieldWorld replayFieldSource;
  ReplayFrame replayBefore,replayAfter;
  Task<(ReplayFrame before,ReplayFrame after)> pendingReplayFrames;
  int replayFrame,pendingReplayFrame,eventPage;
  double replayTime;
  bool replayPlaying;
  float replaySpeed=1;
  string replayStatus="";
  string RecordingsFolder=>Path.Combine(Application.persistentDataPath,"recordings");
  internal bool ReplayOpen=>replayView!=null;
  internal bool ReplayVisualsOnly=>replayView?.HasOnlyVisuals==true;
  internal bool ReplayPlaying=>replayPlaying;
  internal bool ReplayOverhead=>replayView?.Overhead==true;
  internal double ReplayPosition=>replayTime;
  internal int CurrentReplayFrame=>replayFrame;

  async void OpenRecordings(){
   recordingsVisible=true;
   await RefreshRecordings();
  }

  async Task RefreshRecordings(){
   string directory=RecordingsFolder;
   try{
    savedRecordings=await Task.Run(()=>Directory.Exists(directory)?Directory.EnumerateFiles(directory,"*.ashina-replay*")
     .Where(p=>p.EndsWith(ReplayFormat.Extension,StringComparison.OrdinalIgnoreCase)||p.EndsWith(ReplayFormat.Extension+".partial",StringComparison.OrdinalIgnoreCase))
     .OrderByDescending(p=>p,StringComparer.OrdinalIgnoreCase).Take(100).ToArray():Array.Empty<string>());
   }catch(Exception error){recordStatus="Kayıt klasörü okunamadı · "+error.Message;}
  }

  internal bool StartSessionRecording(string folder,string name,bool recordControls){
   if(recorder!=null||finalizingRecorder!=null||ReplayOpen||Project==null||Busy||HomeVisible)return false;
   bool online=RoomActive&&Room?.Connected==true;
   var header=new ReplayHeader{name=name.Trim(),season=Field.Season,layout=Field.Layout,mode=online?"online":"practice",
    createdUtc=DateTime.UtcNow.ToString("O"),projectName=Project.name,localRobot=online?(Room.LocalId??""):"local"};
   header.robots=online?Room.Robots.Values.Select(r=>new ReplayRobotInfo{id=r.Id,size=r.Project.drive.dimensions.Select(x=>(float)x).ToArray()}).ToArray()
    :new[]{new ReplayRobotInfo{id="local",size=Project.drive.dimensions.Select(x=>(float)x).ToArray()}};
   try{recorder=new SessionRecording(folder,header);}
   catch(Exception error){recordStatus="Kayıt başlatılamadı · "+error.Message;return false;}
   recordInput=recordControls;recordStart=nextRecordSample=Time.realtimeSinceStartupAsDouble;
   recordOnline=online;
   recordStatus="Kayıt sürüyor.";
   CaptureSessionFrame();
   return true;
  }

  internal async Task<ReplayOutcome> StopSessionRecording(){
   if(recorder==null)return finalizingRecorder!=null?await finalizingRecorder.Result:null;
   var writing=recorder;recorder=null;finalizingRecorder=writing;
   var outcome=await writing.Stop();
   if(finalizingRecorder==writing)finalizingRecorder=null;
   recordStatus=outcome.Complete?"Kayıt saklandı · "+Path.GetFileName(outcome.Path):"Kısmi kayıt · "+outcome.Error;
   await RefreshRecordings();
   return outcome;
  }

  void CaptureSessionFrame(){
   if(recorder==null)return;
   if(!recorder.Recording){_=StopSessionRecording();return;}
   double now=Time.realtimeSinceStartupAsDouble;
   if(now<nextRecordSample||Busy||Project==null||Robot==null)return;
   nextRecordSample=now+.10;
   bool online=recordOnline;
   if((online&&(!RoomActive||Room?.Connected!=true))||(!online&&RoomActive)){_=StopSessionRecording();return;}
   var world=online?Room.State:null;
   if(online&&world==null)return;
   var frame=new ReplayFrame{phase=online?world.phase:Running?RobotMode:"disabled",blue=online?world.blue:BlueFuel,red=online?world.red:RedFuel};
   frame.robots=online?world.robots:new[]{new MatchRobotState{id="local",name=Project.name,alliance="blue",
    p=MatchProtocol.V(Robot.position),q=MatchProtocol.Q(Robot.rotation),v=MatchProtocol.V(hull.Body.linearVelocity),w=MatchProtocol.V(hull.Body.angularVelocity),
    stock=new Dictionary<string,int>(Project.inventory),shots=ShotsFired,pickups=FuelCollected,
    parts=Project.parts.Select(part=>new MatchPartState{id=part.id,p=MatchProtocol.V(Nodes[part.id].transform.localPosition),q=MatchProtocol.Q(Nodes[part.id].transform.localRotation)}).ToArray()}};
   frame.pieces=Pieces.Active.Where(item=>item!=null&&!item.Removed).Select(item=>new MatchPieceState{
    id=item.NetId,kind=item.Kind,p=MatchProtocol.V(item.transform.position),v=MatchProtocol.V(item.Body.linearVelocity),q=MatchProtocol.Q(item.transform.rotation)}).ToArray();
   if(recordInput){
    var pad=DriverInput.Read(0);
    frame.input=new ReplayInput{axes=(double[])pad.Axes.Clone(),buttons=pad.Buttons,pov=pad.Pov};
   }
   if(!recorder.Capture(now-recordStart,frame)&&!recorder.Recording)_=StopSessionRecording();
  }

  async void OpenReplayDialog(){
   try{
    string file=await FileDialogs.OpenReplayAsync();
    if(file!=null)await OpenReplayFile(file);
   }catch(Exception error){recordStatus="Kayıt açılamadı · "+error.Message;}
  }

  internal async Task<bool> OpenReplayFile(string file){
   if(Running||RoomActive||Busy||recorder!=null||finalizingRecorder!=null||ReplayOpen)return false;
   Busy=true;FieldWorld loadedField=null;ReplayPresentation preview=null;
   try{
    recordStatus="Kayıt dizini okunuyor…";
    var index=await Task.Run(()=>ReplayIndex.Open(file));
    if(index.Frames.Count==0)throw new InvalidDataException("Bu kayıtta oynatılacak kare yok.");
    var pair=await Task.Run(()=>index.ReadPair(0));
    var previewField=Field;
    if(Field.Season!=index.Header.season||Field.Layout!=index.Header.layout){
     loadedField=new FieldWorld();
     await loadedField.Load(index.Header.season,false,index.Header.layout);
     previewField=loadedField;
    }
    preview=await ReplayPresentation.Build(Pieces,index.Header,Project,Robot,previewField);
    replayIndex=index;replayView=preview;replayFieldSource=loadedField;
    replayTime=0;replayFrame=0;eventPage=0;replaySpeed=1;
    replayBefore=pair.before;replayAfter=pair.after;replayPlaying=false;
    replayView.Apply(replayBefore,replayAfter,0);
    replayStatus=index.Complete?"":"Kısmi kayıt · son sağlam kareye kadar oynatılabilir.";
    recordingsVisible=false;
    if(ViewCamera!=null)ViewCamera.enabled=false;
    return true;
   }catch(Exception error){
    if(replayView==preview){replayView=null;replayIndex=null;replayFieldSource=null;}
    preview?.Dispose();loadedField?.Dispose();
    recordStatus="Kayıt açılamadı · "+error.Message;
    return false;
   }finally{Busy=false;}
  }

  internal void SeekReplay(double seconds){
   if(replayIndex==null)return;
   replayTime=Math.Max(0,Math.Min(seconds,replayIndex.Duration));
   if(replayTime>=replayIndex.Duration)replayPlaying=false;
  }
  internal void PlayReplay(){if(ReplayOpen){if(replayTime>=replayIndex.Duration)SeekReplay(0);replayPlaying=true;}}
  internal void PauseReplay(){replayPlaying=false;}
  internal void SpeedReplay(float speed){replaySpeed=Mathf.Clamp(speed,.25f,4);}
  internal void SetReplayOverhead(bool overhead){if(replayView!=null)replayView.Overhead=overhead;}

  void TickReplay(){
   if(replayPlaying)SeekReplay(replayTime+Time.unscaledDeltaTime*replaySpeed);
   if(pendingReplayFrames!=null&&pendingReplayFrames.IsCompleted){
    if(pendingReplayFrames.Status==TaskStatus.RanToCompletion){
     var pair=pendingReplayFrames.Result;replayBefore=pair.before;replayAfter=pair.after;replayFrame=pendingReplayFrame;
    }else{replayStatus="Kare okunamadı · "+pendingReplayFrames.Exception?.GetBaseException().Message;replayPlaying=false;pendingReplayFrames=null;return;}
    pendingReplayFrames=null;
   }
   int wanted=replayIndex.FrameAt(replayTime);
   if(wanted!=replayFrame&&pendingReplayFrames==null){
    var source=replayIndex;pendingReplayFrame=wanted;
    pendingReplayFrames=Task.Run(()=>source.ReadPair(wanted));
   }
   double first=replayIndex.Frames[replayFrame].Time;
   double second=replayIndex.Frames[Math.Min(replayFrame+1,replayIndex.Frames.Count-1)].Time;
   float fraction=second>first?Mathf.Clamp01((float)((replayTime-first)/(second-first))):0;
   replayView.Apply(replayBefore,replayAfter,fraction);
  }

  internal void CloseReplay(){
   replayPlaying=false;replayView?.Dispose();replayFieldSource?.Dispose();
   replayView=null;replayFieldSource=null;replayIndex=null;replayBefore=replayAfter=null;
   if(pendingReplayFrames!=null){_=pendingReplayFrames.ContinueWith(task=>{_=task.Exception;},TaskContinuationOptions.OnlyOnFaulted);pendingReplayFrames=null;}
   if(ViewCamera!=null&&!HomeVisible)ViewCamera.enabled=true;
  }

  void DrawRecordingWorkspace(){
   var width=Math.Min(690,Screen.width-50);
   var rect=new Rect((Screen.width-width)/2,80,width,Math.Min(665,Screen.height-130));
   GUI.Box(rect,"",panel);
   GUILayout.BeginArea(new Rect(rect.x+20,rect.y+14,rect.width-40,rect.height-27));
   GUILayout.BeginHorizontal();GUILayout.Label("OTURUM KAYITLARI",title);GUILayout.FlexibleSpace();
   if(Button("Kapat"))recordingsVisible=false;GUILayout.EndHorizontal();
   if(recorder!=null){
    Text("Kayıt sürüyor · "+(Time.realtimeSinceStartupAsDouble-recordStart).ToString("0.0")+" sn");
    if(Button("Kaydı bitir",true))_=StopSessionRecording();
   }else{
    GUI.enabled=finalizingRecorder==null&&!Busy;
    Text("Kayıt adı",true);GUI.SetNextControlName("session-record-name");recordName=GUILayout.TextField(recordName,100,input);
    recordInput=GUILayout.Toggle(recordInput," Kendi kumandamın girdilerini de kaydet");
    if(Button("Kaydı başlat",true)){
     string name=string.IsNullOrWhiteSpace(recordName)?"Antrenman "+DateTime.Now.ToString("dd.MM HH.mm"):recordName;
     StartSessionRecording(RecordingsFolder,name,recordInput);
    }
    GUI.enabled=true;
   }
   if(recordStatus!="")Text(recordStatus,true);
   GUILayout.Space(12);
   Section("KAYITLARI OYNAT");
   GUI.enabled=!Running&&!RoomActive&&!Busy&&recorder==null&&finalizingRecorder==null;
   if(Button("Bir kayıt dosyası aç"))OpenReplayDialog();
   if(savedRecordings.Length==0)Text("Henüz kayıt bulunamadı.",true);
   recordingScroll=GUILayout.BeginScrollView(recordingScroll);
   foreach(string file in savedRecordings)if(Button(Path.GetFileName(file)))_ = OpenReplayFile(file);
   GUILayout.EndScrollView();GUI.enabled=true;
   if(Running||RoomActive)Text("Kayıt izlemek için sürüşü bitir veya odadan ayrıl.",true);
   GUILayout.EndArea();
  }

  void DrawReplayWorkspace(){
   GUI.Box(new Rect(0,0,Screen.width,69),"",panel);
   GUILayout.BeginArea(new Rect(16,11,Screen.width-32,51));
   GUILayout.BeginHorizontal();GUILayout.Label(replayIndex.Header.name,title);
   GUILayout.FlexibleSpace();Text(replayIndex.Header.season+" · "+replayIndex.Header.projectName,true);
   if(Button("Kayıttan çık")){CloseReplay();GUILayout.EndHorizontal();GUILayout.EndArea();return;}
   GUILayout.EndHorizontal();
   if(replayStatus!="")Text(replayStatus,true);
   GUILayout.EndArea();
   const float bar=172;
   GUI.Box(new Rect(0,Screen.height-bar,Screen.width,bar),"",panel);
   GUILayout.BeginArea(new Rect(16,Screen.height-bar+10,Screen.width-32,bar-20));
   GUILayout.BeginHorizontal();
   if(Button(replayPlaying?"Duraklat":"Oynat",true)){if(replayPlaying)PauseReplay();else PlayReplay();}
   if(Button("Baştan başlat")){SeekReplay(0);PlayReplay();}
   if(Button("Durdur")){PauseReplay();SeekReplay(0);}
   if(Button(replayView.Overhead?"Üst görünüm":"Robotu izle"))SetReplayOverhead(!replayView.Overhead);
   if(replayBefore!=null&&replayBefore.robots.Length>1){
    var ids=replayBefore.robots.Select(adh=>adh.id).ToArray();
    var labels=replayBefore.robots.Select(adh=>adh.name).ToArray();
    replayView.Focus=Cycle("Kamera",ids.Contains(replayView.Focus)?replayView.Focus:ids[0],ids,labels);
   }
   replaySpeed=float.Parse(Cycle("Hız",replaySpeed.ToString(System.Globalization.CultureInfo.InvariantCulture),
    new[]{"0.25","0.5","1","2","4"},new[]{"¼×","½×","1×","2×","4×"}),System.Globalization.CultureInfo.InvariantCulture);
   GUILayout.FlexibleSpace();
   Text(replayTime.ToString("0.0")+" / "+replayIndex.Duration.ToString("0.0")+" sn");
   GUILayout.EndHorizontal();
   var seek=GUILayout.HorizontalSlider((float)replayTime,0,(float)Math.Max(.001,replayIndex.Duration));
   if(Math.Abs(seek-replayTime)>.005){PauseReplay();SeekReplay(seek);}
   DrawReplayMarkers();
   if(replayBefore!=null){
    var robot=Array.Find(replayBefore.robots,r=>r.id==replayView.Focus)??replayBefore.robots.FirstOrDefault();
    string state=robot==null?"":"    "+robot.name+"  ·  "+MatchProtocol.Vector(robot.v).magnitude.ToString("0.0")+" m/s  ·  Stok "+robot.stock.Values.Sum();
    Text("Mavi "+replayBefore.blue+"  ·  Kırmızı "+replayBefore.red+"    "+PhaseLabel(replayBefore.phase)+state,true);
   }
   if(replayIndex.Events.Count>0){
    GUILayout.BeginHorizontal();
    if(Button("‹ Olaylar")&&eventPage>0)eventPage--;
    for(int i=eventPage*4;i<Math.Min(eventPage*4+4,replayIndex.Events.Count);i++){
     var point=replayIndex.Events[i];
     if(Button(point.time.ToString("0.0")+" · "+point.item.label)){PauseReplay();SeekReplay(point.time);}
    }
    if(Button("Olaylar ›")&&(eventPage+1)*4<replayIndex.Events.Count)eventPage++;
    GUILayout.EndHorizontal();
   }
   GUILayout.EndArea();
  }

  void DrawReplayMarkers(){
   var row=GUILayoutUtility.GetRect(1,10,GUILayout.ExpandWidth(true));
   if(replayIndex.Events.Count==0||replayIndex.Duration<=0||row.width<10)return;
   if(Event.current.type==EventType.Repaint){
    var old=GUI.color;
    GUI.color=new Color(.27f,.70f,.83f);
    int previous=-5;
    foreach(var marker in replayIndex.Events){
     int x=(int)(row.x+row.width*marker.time/replayIndex.Duration);
     if(x-previous<4)continue;
     GUI.DrawTexture(new Rect(x,row.y+2,2,6),Texture2D.whiteTexture);
     previous=x;
    }
    GUI.color=old;
   }else if(Event.current.type==EventType.MouseDown&&row.Contains(Event.current.mousePosition)){
    double time=(Event.current.mousePosition.x-row.x)/row.width*replayIndex.Duration;
    var marker=replayIndex.Events.OrderBy(m=>Math.Abs(m.time-time)).First();
    PauseReplay();SeekReplay(marker.time);Event.current.Use();
   }
  }
 }
}
