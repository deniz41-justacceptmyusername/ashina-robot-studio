using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace AshinaStudio {
 public partial class StudioApp {
  LocalAutoPlan runningLocalAuto,displayedLocalAuto;
  readonly Dictionary<string,List<LocalAutoCue>> localAutoActions=new Dictionary<string,List<LocalAutoCue>>();
  Transform localAutoGuide;
  Transform guideField;
  Material localAutoMaterial;
  float localAutoTime,localAutoEnd;
  bool showLocalGuide=true,guideRed;
  int localAutoLeg;

  internal bool StandaloneAutoRunning=>runningLocalAuto!=null;
  internal float StandaloneAutoTime=>localAutoTime;
  internal bool StandaloneAutoFinished=>StandaloneAutoRunning&&localAutoTime>=localAutoEnd;

  async void ImportPathPlannerDialog(){
   if(Running||Busy||FileDialogs.IsOpen)return;
   try{
    var file=await FileDialogs.OpenPathPlannerAsync();
    if(file!=null)await ImportPathPlanner(file);
   }catch(Exception error){Notice="PathPlanner dosyası açılamadı · "+error.Message;}
  }

  internal async Task<bool> ImportPathPlanner(string file){
   if(Busy||Running||RoomActive||Project==null)return false;
   var project=Project;Busy=true;
   try{
    Notice="PathPlanner rotası işleniyor…";
    var plan=await Task.Run(()=>PathPlannerImport.Read(file));
    if(Project!=project)return false;
    if(project.localAuto!=null){
     plan.red=project.localAuto.red;
     foreach(var cue in plan.cues)if(project.localAuto.bindings.TryGetValue(cue.name,out var actionId))plan.bindings[cue.name]=actionId;
    }
    project.localAuto=plan;showLocalGuide=true;ShowFieldForShotPick();
    Notice=plan.name+" · PathPlanner otonomu hazır.";
    return true;
   }catch(Exception error){Notice="PathPlanner dosyası açılamadı · "+error.Message;return false;}
   finally{Busy=false;}
  }

  internal bool BeginStandaloneAuto(){
   if(Busy||Running||HomeVisible||RoomActive||FileDialogs.IsOpen||Project.localAuto==null)return false;
   var plan=Project.localAuto;
   if(plan.legs.Count>0){
    foreach(var leg in plan.legs)foreach(var sample in leg.points){
     var point=AutoFieldPose(sample,plan.red);
     if(point.x<-.05f||point.x>Field.Length+.05||point.y<-.05f||point.y>Field.Width+.05){Notice="PathPlanner rotası bu sahanın dışına çıkıyor.";return false;}
    }
   }
   runningLocalAuto=plan;localAutoTime=0;localAutoLeg=0;localAutoEnd=plan.duration;
   localAutoActions.Clear();
   foreach(var cue in plan.cues){
    RobotAction action;
    if(plan.bindings.TryGetValue(cue.name,out var id))action=Project.actions.Find(a=>a.id==id);
    else action=Project.actions.Find(a=>a.name==cue.name);
    if(action==null)continue;
    if(!localAutoActions.TryGetValue(action.id,out var cues))localAutoActions[action.id]=cues=new List<LocalAutoCue>();
    float hold=action.type=="move"&&action.mode=="hold"?Math.Max(cue.length,(float)action.duration):cue.length;
    var active=new LocalAutoCue{name=cue.name,start=cue.start,length=Math.Min(hold,plan.duration+.6f-cue.start)};
    cues.Add(active);localAutoEnd=Math.Max(localAutoEnd,active.start+active.length);
   }
   RobotMode="autonomous";SetRunning(true);
   if(!Running){EndStandaloneAuto();return false;}
   if(plan.resetPose&&plan.legs.Count>0){
    var point=AutoFieldPose(plan.legs[0].points[0],plan.red);
    var position=Frc.Position(new[]{(double)point.x,point.y,0});
    position.y=Field.GroundHeight(position);
    var yaw=Project.drive.type=="differential"?point.tangent:point.yaw;
    heading=yaw*Mathf.Deg2Rad;
    hull.Teleport(position,Frc.Rotation(new[]{0.0,0,yaw}));
    cameraTarget=position+Vector3.up*.6f;
   }
   Notice="PathPlanner otonomu başlatıldı.";
   return true;
  }

  void EndStandaloneAuto(){runningLocalAuto=null;localAutoActions.Clear();localAutoTime=localAutoEnd=0;localAutoLeg=0;}

  bool StandaloneActionDown(RobotAction action){
   if(!localAutoActions.TryGetValue(action.id,out var cues))return false;
   foreach(var cue in cues)if(localAutoTime>=cue.start&&localAutoTime<cue.start+cue.length)return true;
   return false;
  }

  void ReadStandaloneAuto(ref Vector3 desired,ref float desiredOmega){
   var plan=runningLocalAuto;
   float time=localAutoTime;
   if(time<=localAutoEnd+.45f)localAutoTime+=Time.fixedDeltaTime;
   while(localAutoLeg<plan.legs.Count&&time>plan.legs[localAutoLeg].start+plan.legs[localAutoLeg].duration+.001f){
    var past=plan.legs[localAutoLeg];
    bool final=localAutoLeg==plan.legs.Count-1;
    if(final&&time<=past.start+past.duration+.35f)break;
    localAutoLeg++;
   }
   if(localAutoLeg>=plan.legs.Count)return;
   var leg=plan.legs[localAutoLeg];
   if(time<leg.start)return;
   float elapsed=Mathf.Min(leg.duration,time-leg.start);
   var here=AutoFieldPose(Sample(leg,elapsed),plan.red);
   var ahead=AutoFieldPose(Sample(leg,Mathf.Min(leg.duration,elapsed+.06f)),plan.red);
   var pose=Robot.position;
   var error=new Vector3(-here.y-pose.x,0,here.x-pose.z);
   var feedforward=new Vector3(here.y-ahead.y,0,ahead.x-here.x)/.06f;
   desired=Vector3.ClampMagnitude(feedforward+error*3,(float)Project.drive.maxSpeed);
   bool tank=Project.drive.type=="differential";
   if(tank){
    var forward=Vector3.ProjectOnPlane(Robot.forward,Vector3.up).normalized;
    desired=Vector3.Project(desired,forward);
   }
   float yaw=tank?ahead.tangent:ahead.yaw;
   desiredOmega=Mathf.Clamp(Mathf.DeltaAngle(heading*Mathf.Rad2Deg,yaw)*Mathf.Deg2Rad*5,-(float)Project.drive.turnRate,(float)Project.drive.turnRate);
  }

  internal LocalAutoPoint AutoFieldPose(LocalAutoPoint sample,bool red){
   if(!red)return sample;
   sample.x=(float)Field.Length-sample.x;sample.y=(float)Field.Width-sample.y;sample.yaw+=180;sample.tangent+=180;
   return sample;
  }

  internal static LocalAutoPoint Sample(LocalAutoLeg leg,float time){
   var points=leg.points;int left=0,right=points.Length-1;
   while(right-left>1){int middle=(left+right)/2;if(time<points[middle].at)right=middle;else left=middle;}
   var a=points[left];var b=points[right];float ratio=Mathf.InverseLerp(a.at,b.at,time);
   return new LocalAutoPoint{at=time,x=Mathf.Lerp(a.x,b.x,ratio),y=Mathf.Lerp(a.y,b.y,ratio),yaw=Mathf.LerpAngle(a.yaw,b.yaw,ratio),tangent=Mathf.LerpAngle(a.tangent,b.tangent,ratio)};
  }

  void DrawStandaloneAuto(){
   Section("PATHPLANNER OTONOMU");
   if(Button("PathPlanner .auto / .path yükle"))ImportPathPlannerDialog();
   var plan=Project.localAuto;
   if(plan==null){Text("Otonomu NetworkTables olmadan saha üzerinde dene.",true);return;}
   Text(plan.name);
   Text($"{plan.legs.Count} rota · {plan.duration:0.0} saniye",true);
   showLocalGuide=GUILayout.Toggle(showLocalGuide," Saha üzerinde rotayı göster");
   if(Button(plan.red?"İttifak · Kırmızı":"İttifak · Mavi"))plan.red=!plan.red;
   plan.resetPose=GUILayout.Toggle(plan.resetPose," Rota başlangıcına yerleştir");
   if(plan.cues.Count>0){
    Section("PATHPLANNER KOMUTLARI");
    var ids=new[]{""}.Concat(Project.actions.Select(a=>a.id)).ToArray();
    var labels=new[]{"Eylem seç"}.Concat(Project.actions.Select(a=>a.name)).ToArray();
    foreach(string name in plan.cues.Select(c=>c.name).Distinct()){
     if(!plan.bindings.TryGetValue(name,out var id))id=Project.actions.Find(a=>a.name==name)?.id??"";
     if(!ids.Contains(id))id="";
     var next=Cycle(name,id,ids,labels);
     if(next!=id)plan.bindings[name]=next;
    }
   }
   if(Button("▶  Bağımsız otonomu başlat",true))BeginStandaloneAuto();
  }

  void UpdateStandaloneGuide(){
   var plan=Project?.localAuto;
   bool visible=plan!=null&&showLocalGuide&&!HomeVisible&&!RoomActive&&(uiTab==2||StandaloneAutoRunning);
   if(localAutoGuide!=null)localAutoGuide.gameObject.SetActive(visible);
   if(!visible)return;
   if(displayedLocalAuto==plan&&guideRed==plan.red&&guideField==Field.Root)return;
   if(localAutoGuide!=null){localAutoGuide.gameObject.SetActive(false);Lifetime.Destroy(localAutoGuide.gameObject);}
   displayedLocalAuto=plan;guideRed=plan.red;guideField=Field.Root;
   localAutoGuide=new GameObject("PathPlanner rotası").transform;
   if(localAutoMaterial==null)localAutoMaterial=new Material(Shader.Find("Sprites/Default"));
   foreach(var leg in plan.legs){
    var line=new GameObject("Rota adımı").AddComponent<LineRenderer>();
    line.transform.SetParent(localAutoGuide,false);line.sharedMaterial=localAutoMaterial;line.useWorldSpace=true;
    line.startColor=line.endColor=new Color(.25f,.85f,1);
    line.startWidth=line.endWidth=.025f;
    var points=new List<Vector3>();
    foreach(var sample in leg.points){
     if(sample.at>leg.duration)break;
     points.Add(GuidePoint(sample,plan.red));
    }
    points.Add(GuidePoint(Sample(leg,leg.duration),plan.red));
    line.positionCount=points.Count;line.SetPositions(points.ToArray());
   }
  }

  Vector3 GuidePoint(LocalAutoPoint source,bool red){
   var sample=AutoFieldPose(source,red);
   return new Vector3(-sample.y,.175f,sample.x);
  }
 }
}
