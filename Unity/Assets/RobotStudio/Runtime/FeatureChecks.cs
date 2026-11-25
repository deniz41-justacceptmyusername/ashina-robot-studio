using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace AshinaStudio {
 public sealed class FeatureChecks : MonoBehaviour {
  public StudioApp App;public string Output;readonly List<string> passed=new List<string>();
  void Check(bool ok,string name){if(!ok)throw new Exception(name);passed.Add(name);Debug.Log("PASS FEATURE: "+name);}
  async Task Wait(Func<bool> test,string name){float start=Time.realtimeSinceStartup;while(!test()){if(Time.realtimeSinceStartup-start>8)throw new Exception("Timeout "+name);await Task.Delay(20);}Check(true,name);}
  async void Start(){Directory.CreateDirectory(Output);try{
   Check(App.HomeVisible&&!App.Running&&!App.ViewCamera.enabled,"Normal startup stays on the home screen");
   App.SetRunning(true);Check(!App.Running,"Home screen cannot silently enable the robot");
   App.EnterWorkspace();Check(!App.HomeVisible&&App.Robot.gameObject.activeSelf&&App.ViewCamera.enabled,"Continue opens the workspace explicitly");
   App.SetRunning(true);App.ShowHome();Check(App.HomeVisible&&!App.Running&&!App.Robot.gameObject.activeSelf,"Returning home stops and hides the simulation");App.EnterWorkspace();
   var p=RobotProject.Empty();p.season="practice";p.training.spawnFuel=false;p.initialPose=new[]{2.0,4,0};
   p.parts.Add(new RobotPart{id="axis",name="Moving mechanism",position=new[]{0.0,0,.5}});await App.LoadProject(p);App.CheckIntakeEditor(Check);App.CheckShotGuide(Check);
   var drive=new DriveData{maxSpeed=2,fieldRelative=true};
   Check(StudioApp.RoomManualDrive(Vector2.right,drive,Quaternion.identity,"blue")==Vector3.forward*2&&StudioApp.RoomManualDrive(Vector2.right,drive,Quaternion.identity,"red")==Vector3.back*2&&StudioApp.RoomManualDrive(Vector2.up,drive,Quaternion.identity,"red")==Vector3.right*2,"Red alliance reverses both field-relative translation axes from the driver's station");
   drive.fieldRelative=false;var redFacing=Quaternion.Euler(0,180,0);
   Check(Vector3.Distance(StudioApp.RoomManualDrive(Vector2.right,drive,redFacing,"red"),redFacing*Vector3.forward*2)<.001f&&StudioApp.RoomManualDrive(Vector2.right,drive,Quaternion.identity,"red")==Vector3.forward*2,"Robot-relative manual controls use robot heading only");
   Check(App.FitMainRobot(),"Quick setup fits the main model to the carpet");
   var a=new RobotAction{id="test-shot",type="emit",partId="chassis",piece="fuel",key="",button="",pitch=35,spawn=new[]{.3,0,.4},aimMode="point",target=new[]{6.0,4,1.0},maxShotSpeed=12,inheritVelocity=true};App.Project.actions.Add(a);
   Check(App.PlanShot(a,out var stationary,out _),"Point target has a reachable fixed-elevation solution");
   Check(Math.Abs(Math.Asin(stationary.Direction.y)*180/Math.PI-35)<.01,"Point aiming preserves the configured elevation");
   Check(Vector3.Distance(ShotPlanning.Position(stationary.Origin,stationary.Direction*stationary.Speed+stationary.Inherited,stationary.Flight),Frc.Position(a.target))<.02,"Point solution includes gravity and game-piece drag");
   await CheckMuzzleRelease();
   a.maxShotSpeed=1;int shots=App.ShotsFired;App.Project.inventory["fuel"]=2;
   Check(!App.PlanShot(a,out _,out _)&&App.Emit(a)==null&&App.ShotsFired==shots&&App.Stored("fuel")==2,"Unreachable maximum power does not create a shot or consume stock");a.maxShotSpeed=12;
   a.zones.Add(new AimZone{name="Near",center=new[]{2.0,4},size=new[]{2.0,2},target=new[]{6.0,4,1.0}});a.zones.Add(new AimZone{name="Feed",center=new[]{8.0,4},size=new[]{2.0,2},target=new[]{3.0,4,.1}});a.aimMode="zones";
   Check(App.PlanShot(a,out var zoneOne,out _)&&Vector3.Distance(ShotPlanning.Position(zoneOne.Origin,zoneOne.Direction*zoneOne.Speed,zoneOne.Flight),Frc.Position(a.zones[0].target))<.02,"First field region selects its own target");
   p.initialPose=new[]{8.0,4,0};App.RequestRobotReset();
   Check(App.PlanShot(a,out var feed,out _)&&feed.Direction.z<0&&Vector3.Distance(ShotPlanning.Position(feed.Origin,feed.Direction*feed.Speed,feed.Flight),Frc.Position(a.zones[1].target))<.02,"Second region feeds toward the team's side using the same action");
   p.initialPose=new[]{13.0,4,0};App.RequestRobotReset();Check(!App.PlanShot(a,out _,out _),"No matching region blocks unconfigured shots");a.zoneFallback=true;Check(App.PlanShot(a,out _,out _),"Region fallback uses the explicitly configured default target");
   var random=new System.Random(8);var scattered=Enumerable.Range(0,100).Select(_=>ShotPlanning.Scatter(Vector3.forward,3,random)).ToArray();
   Check(scattered.All(v=>Vector3.Angle(Vector3.forward,v)<=3.01f)&&scattered.Any(v=>Vector3.Angle(Vector3.forward,v)>1),"Scatter varies direction inside the configured cone");
   p.training.unlimitedShots=true;var drop=App.Emit(new RobotAction{type="emit",partId="chassis",piece="coral",speed=0,inheritVelocity=false});Check(drop!=null&&drop.Body.linearVelocity.sqrMagnitude<.0001f,"Zero-speed fixed emission still supports dropping a game piece");App.Pieces.Clear();p.training.unlimitedShots=false;
   var restored=ProjectIO.Parse(ProjectIO.Write(p));Check(restored.actions[0].zones.Count==2&&restored.actions[0].maxShotSpeed==12,"Regions and power limits survive save and reload");
   bool rejected=false;restored.actions[0].spreadDegrees=double.NaN;try{ProjectIO.Validate(restored);}catch{rejected=true;}Check(rejected,"Invalid shot configuration is rejected");
   p.initialPose=new[]{2.0,4,Math.PI/4};App.RequestRobotReset();p.network.driveFromNt=false;p.parts[1].links.Add(new AxisLink{property="rotation.z",topic="/Feature/Axis",unit="rotations",frame="field"});
   App.Nt=new Nt4Client();await App.Nt.Connect("127.0.0.1",5812);await Wait(()=>App.Nt.Get("/Feature/Speed")!=null,"Real WPILib server publishes live shot parameters");
   App.SetRunning(true);await Task.Delay(150);var forward=App.Nodes["axis"].transform.forward;
   Check(Vector3.Angle(forward,Vector3.left)<.1,"Generic field-relative mechanism rotation converts turns to degrees");
   a.aimMode="fixed";a.partId="axis";a.pitch=0;a.yaw=0;a.inheritVelocity=false;Check(App.PlanShot(a,out var rotating,out _)&&Vector3.Angle(rotating.Direction,Vector3.left)<.1,"Shot direction follows the current moving mechanism pose");
   a.partId="chassis";a.aimMode="point";a.pitch=35;a.speedTopic="/Feature/Speed";a.speedScale=1;
   Check(App.PlanShot(a,out var liveTarget,out _)&&liveTarget.Flight>0&&Vector3.Distance(ShotPlanning.Position(liveTarget.Origin,liveTarget.Direction*liveTarget.Speed,liveTarget.Flight),Frc.Position(a.target))<.02,"A live speed alone preserves point aiming");
   a.speedScale=0;Check(!App.PlanShot(a,out _,out _),"A stopped WPILib shooter cannot drop a point-targeted piece");
   App.BeginBinding("pitch",a.id);Check(App.PickBinding("/Feature/Pitch","")&&a.pitchTopic=="/Feature/Pitch","NT picker binds directly to the selected angle parameter");
   a.partId="chassis";a.yawTopic="/Feature/Yaw";a.yawUnit="radians";a.directionFrame="field";a.speedTopic="/Feature/Speed";a.speedScale=.1;a.maxShotSpeed=5;a.aimMode="point";a.target=new[]{100.0,100,4};
   Check(App.PlanShot(a,out var live,out _)&&Math.Abs(live.Speed-5)<.001,"Live speed is scaled and limited by configured maximum power");
   Check(Math.Abs(live.Direction.y-Math.Sin(35*Math.PI/180))<.001&&live.Direction.x<-.8&&Math.Abs(live.Direction.z)<.001,"Live code angles override target assistance with correct units and reference frame");
   a.inheritVelocity=true;p.network.driveFromNt=true;p.network.mode="observe";p.network.autoPose=false;p.network.poseTopic="/Feature/Pose";await Task.Delay(350);
   Check(App.PlanShot(a,out var observed,out _)&&observed.Inherited.magnitude>.15,"Observed WPILib movement contributes muzzle velocity");Check(Vector3.Angle(App.Nodes["axis"].transform.forward,Vector3.left)<.15,"World-referenced mechanism keeps its heading while the robot turns");
   App.SetRunning(false);App.DisconnectNetwork();a.pitchTopic=a.yawTopic=a.speedTopic="";a.partId="chassis";a.directionFrame="field";a.pitch=35;a.aimMode="point";a.maxShotSpeed=12;a.spreadDegrees=0;p.initialPose=new[]{2.0,4,0};App.RequestRobotReset();a.target=new[]{5.0,4,1.0};
   Check(App.PlanShot(a,out var offline,out _)&&Math.Abs(Math.Asin(offline.Direction.y)*180/Math.PI-35)<.01,"Offline target mode resumes without changing the regular angle");
   p.training.unlimitedShots=true;var piece=App.Emit(a);float nearest=float.PositiveInfinity,start=Time.time;while(Time.time-start<offline.Flight+.4f){if(piece!=null)nearest=Mathf.Min(nearest,Vector3.Distance(piece.transform.position,Frc.Position(a.target)));await Task.Delay(8);}
   Check(nearest<.25f,"Actual Rigidbody projectile passes within 25 cm of its target");
   p.parts[1].links.Clear();App.SetRunning(true);var body=App.Robot.GetComponent<RobotChassis>().Body;body.linearVelocity=Vector3.right;body.angularVelocity=Vector3.up;a.aimMode="fixed";a.pitch=0;a.spawn=new[]{.6,0,.4};App.PlanShot(a,out var moving,out _);
   Check(moving.Inherited.x>1.4,"Inherited launch velocity includes rotation at an offset muzzle");
   body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;App.CheckVelocity=Vector3.zero;App.CheckOmega=1;double oldYaw=App.Pose[2];await Task.Delay(450);
   Check(Mathf.DeltaAngle((float)(oldYaw*180/Math.PI),(float)(App.Pose[2]*180/Math.PI))>8,"Positive WPILib yaw request turns in the FRC coordinate direction");App.CheckVelocity=null;App.CheckOmega=0;
   App.SetRunning(false);await App.LoadField("2026");App.Pieces.Clear();p.initialPose=new[]{2.0,4,0};App.RequestRobotReset();p.training.unlimitedShots=true;
   var hub=App.Hubs.First(h=>h.Alliance=="blue");a.aimMode="point";a.pitch=65;a.target=Frc.Position(hub.Center+Vector3.up*hub.Height);a.spawn=new[]{.65,0,.4};a.spreadDegrees=0;
   int previousScore=App.BlueFuel;Check(App.Emit(a)!=null,"Fixed-angle target solver launches toward the official HUB");await Wait(()=>App.BlueFuel>previousScore,"Target-assisted shot enters the real HUB through collision geometry");
   App.ShowHome();File.WriteAllText(Path.Combine(Output,"result.json"),JsonConvert.SerializeObject(new{success=true,count=passed.Count,passed,nearestTargetMetres=nearest},Formatting.Indented));Application.Quit(0);
  }catch(Exception e){Debug.LogException(e);File.WriteAllText(Path.Combine(Output,"result.json"),JsonConvert.SerializeObject(new{success=false,count=passed.Count,passed,error=e.ToString()},Formatting.Indented));Application.Quit(2);}}

  async Task CheckMuzzleRelease(){
   var axle=App.Nodes["axis"].transform;
   var original=axle.localRotation;
   axle.localRotation=Frc.Rotation(new[]{90.0,0,0});
   var action=new RobotAction{type="emit",partId="axis",piece="fuel",pitch=0,yaw=90,inheritVelocity=false};
   Check(App.PlanShot(action,out var flat,out _)&&Vector3.Angle(flat.Direction,Vector3.left)<.05f,"Horizontal angle keeps flight level on a mechanism rolled 90 degrees");
   action.pitch=90;
   Check(App.PlanShot(action,out var vertical,out _)&&Vector3.Angle(vertical.Direction,Vector3.up)<.05f,"Vertical angle points up on a rolled mechanism even with horizontal angle set");
   axle.localRotation=original;

   var project=App.Project;
   project.training.unlimitedShots=true;
   App.SetRunning(true);
   action.partId="chassis";action.aimMode="point";action.pitch=30;
   action.target=new[]{.85,4.5,.27};
   var mount=App.Nodes["chassis"].content;
   var origin=App.Robot.TransformPoint(Frc.Position(new[]{.19,-.19,.16}));
   action.spawn=Frc.Position(mount.InverseTransformPoint(origin));
   Check(App.PlanShot(action,out var plan,out _)&&plan.Direction.z<0,"Point aiming can shoot back across the frame from a low corner");
   var piece=App.Emit(action);
   var bumper=App.Robot.GetComponent<RobotChassis>().Bumper;
   var shape=piece.GetComponentInChildren<Collider>();
   Check(Vector3.Distance(piece.Body.position,origin)<.001f&&Physics.GetIgnoreCollision(shape,bumper),"Aimed shot starts at the exact muzzle and temporarily clears its own bumper");
   float nearest=float.PositiveInfinity, until=Time.time+plan.Flight+.25f;
   var previous=origin;
   var target=Frc.Position(action.target);
   while(Time.time<until){
    var current=piece.Body.position;
    var step=current-previous;
    float progress=Mathf.Clamp01(Vector3.Dot(target-previous,step)/Mathf.Max(.000001f,step.sqrMagnitude));
    nearest=Mathf.Min(nearest,Vector3.Distance(previous+step*progress,target));
    previous=current;
    await Task.Yield();
   }
   if(nearest>=.16f)throw new Exception($"Low shot missed by {nearest:F3} m; flight {plan.Flight:F2} s.");
   Check(!Physics.GetIgnoreCollision(shape,bumper),"A low rear-directed shot flies to its target and restores chassis collisions when clear");
   App.Pieces.Clear();
   App.SetRunning(false);
   project.training.unlimitedShots=false;
  }
 }
}
