using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace AshinaStudio {
 public sealed class PhysicsPolishChecks : MonoBehaviour {
  public StudioApp App;public string Output;readonly List<string> passed=new List<string>();
  void Check(bool ok,string name){if(!ok)throw new Exception(name);passed.Add(name);Debug.Log("PASS: "+name);}
  async Task Drive(float seconds,Vector3 velocity,float turn=0){App.CheckVelocity=velocity;App.CheckOmega=turn;await Task.Delay((int)(seconds*1000));}
  async void Start(){
   Directory.CreateDirectory(Output);
   try{
    var p=RobotProject.Empty();p.season="practice";p.training.spawnFuel=false;p.initialPose=new[]{2.0,4,0};
    await App.LoadProject(p);App.SetRunning(true);await Drive(.8f,Vector3.zero);
    var chassis=App.Robot.GetComponent<RobotChassis>();
    Check(!chassis.Body.isKinematic&&chassis.Body.mass>40,"Robot has dynamic mass and collider response");
    Check(Math.Abs(App.Robot.position.y)<.07&&Vector3.Angle(App.Robot.up,Vector3.up)<2,"Four contacts settle level on carpet");
    var balls=Enumerable.Range(0,7).Select(i=>App.Pieces.Spawn("fuel",Frc.Position(new[]{2.8+i*.17,4.0,.08}),Vector3.zero,Quaternion.identity)).ToArray();
    var initial=balls.Select(b=>b.transform.position).ToArray();float maximumHeight=0;
    App.CheckVelocity=Vector3.forward*1.6f;
    for(int i=0;i<100;i++){await Task.Delay(15);maximumHeight=Mathf.Max(maximumHeight,App.Robot.position.y);}
    Check(balls.Where((b,i)=>Vector3.Distance(b.transform.position,initial[i])>.3f).Count()>=4,"Bumper physically displaces loose FUEL");
    Check(maximumHeight<.15f,"Loose FUEL does not act as a flat support platform");
    App.SetRunning(false);App.Pieces.Clear();App.RequestRobotReset();App.SetRunning(true);
    App.CheckVelocity=Vector3.zero;chassis.Teleport(App.Robot.position+Vector3.up,App.Robot.rotation);chassis.Body.linearVelocity=Vector3.zero;Physics.SyncTransforms();float height=App.Robot.position.y;
    await Drive(.18f,Vector3.forward*3,2);
    Check(App.Robot.position.y<height-.08f&&chassis.GroundedWheels==0,"Airborne chassis falls under gravity");
    Check(new Vector2(chassis.Body.linearVelocity.x,chassis.Body.linearVelocity.z).magnitude<.05&&Math.Abs(App.Pose[2])<.05,"No ground traction or yaw steering in midair");
    App.SetRunning(false);await App.LoadField("2026");App.Pieces.Clear();App.Project.initialPose=new[]{3.15,2.5,0};App.RequestRobotReset();App.SetRunning(true);
    App.CheckVelocity=Vector3.forward*1.7f;float tilt=0,highest=0;
    for(int i=0;i<170;i++){await Task.Delay(15);tilt=Mathf.Max(tilt,Vector3.Angle(App.Robot.up,Vector3.up));highest=Mathf.Max(highest,App.Robot.position.y);}
    Check(tilt>5&&highest>.075,$"Official REBUILT ramp tilts and lifts the chassis (tilt={tilt:0.0}, high={highest:0.000}, x={App.Pose[0]:0.00})");
    Check(App.Pose[0]>5.85,"Robot traverses the real ramp collision geometry");
    await Drive(.9f,Vector3.zero);
    Check(App.Robot.position.y>-.06f&&App.Robot.position.y<.08f&&Vector3.Angle(App.Robot.up,Vector3.up)<3,"Suspension settles after leaving the ramp");
    App.SetRunning(false);await App.LoadField("2025");
    var cages=App.Field.Root.GetComponentsInChildren<HangingCage>();
    Check(cages.Length==6&&cages.All(c=>!c.Body.isKinematic),"All six REEFSCAPE cages are dynamic");
    Check(cages.All(c=>c.GetComponentsInChildren<BoxCollider>().Length==6),"Cage colliders preserve four uprights and two plates");
    var cage=cages.First(c=>c.GetComponentsInChildren<Renderer>().Min(r=>r.bounds.min.y)<.2f);var before=cage.Body.position;
    cage.Body.AddForceAtPosition(Vector3.forward*12,before-Vector3.up*.2f,ForceMode.Impulse);await Task.Delay(300);
    Check(Vector3.Distance(before,cage.Body.position)>.04,"Deep cage swings after a physical impulse");
    Check(Vector3.Distance(cage.transform.TransformPoint(cage.Suspension.anchor),cage.Suspension.connectedAnchor)<.06,"Cage remains attached at its suspension point");
    File.WriteAllText(Path.Combine(Output,"result.json"),JsonConvert.SerializeObject(new{success=true,count=passed.Count,passed,rampMaximumTilt=tilt,rampMaximumHeight=highest,ballMaximumRideHeight=maximumHeight},Formatting.Indented));
    Application.Quit(0);
   }catch(Exception e){Debug.LogException(e);File.WriteAllText(Path.Combine(Output,"result.json"),JsonConvert.SerializeObject(new{success=false,count=passed.Count,passed,error=e.ToString(),position=Frc.Position(App.Robot.position),up=Frc.Position(App.Robot.up),velocity=Frc.Position(App.Robot.GetComponent<RobotChassis>().Body.linearVelocity),wheels=App.Robot.GetComponent<RobotChassis>().GroundedWheels},Formatting.Indented));Application.Quit(2);}
  }
 }
}
