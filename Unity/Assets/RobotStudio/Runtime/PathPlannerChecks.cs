using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace AshinaStudio {
 public sealed partial class FeatureChecks {
  async Task CheckPathPlanner(){
   var folder=Path.Combine(Output,"pathplanner");
   var autos=Path.Combine(folder,"autos");var paths=Path.Combine(folder,"paths");
   Directory.CreateDirectory(autos);Directory.CreateDirectory(paths);
   string path=Path.Combine(paths,"Training Curve.path");
   string auto=Path.Combine(autos,"Training Auto.auto");
   File.WriteAllText(path,@"{
    'version':'2025.0',
    'waypoints':[
     {'anchor':{'x':2,'y':4},'prevControl':null,'nextControl':{'x':2.6,'y':4}},
     {'anchor':{'x':4.4,'y':4.9},'prevControl':{'x':3.9,'y':4.6},'nextControl':null}
    ],
    'rotationTargets':[{'waypointRelativePos':0.5,'rotationDegrees':25}],
    'globalConstraints':{'maxVelocity':1.7,'maxAcceleration':2},
    'constraintZones':[{'minWaypointRelativePos':0.35,'maxWaypointRelativePos':0.65,'constraints':{'maxVelocity':1.15,'maxAcceleration':1.4}}],
    'idealStartingState':{'velocity':0,'rotation':0},'goalEndState':{'velocity':0,'rotation':45},
    'eventMarkers':[{'name':'Collect','waypointRelativePos':0.35,'endWaypointRelativePos':0.7,'command':null}],
    'reversed':false
   }");
   File.WriteAllText(auto,@"{
    'version':'2025.0','resetOdom':true,'choreoAuto':false,
    'command':{'type':'sequential','data':{'commands':[
     {'type':'wait','data':{'waitTime':0.28}},
     {'type':'path','data':{'pathName':'Training Curve'}},
     {'type':'named','data':{'name':'Release'}}
    ]}}
   }");
   var robot=RobotProject.Empty();robot.season="practice";robot.training.spawnFuel=false;
   robot.training.unlimitedShots=false;robot.inventory["fuel"]=1;
   robot.actions.Add(new RobotAction{id="shoot",name="Shoot sample",type="emit",partId="chassis",piece="fuel",mode="press",key="",button="",pitch=30,spawn=new[]{.5,0,.4},inheritVelocity=false});
   await App.LoadProject(robot);
   var single=PathPlannerImport.Read(path);
   Check(single.legs.Count==1&&single.legs[0].points.Length>20&&single.cues[0].length>.10f,"Standalone .path imports the spline, constraints and its path marker");
   Check(await App.ImportPathPlanner(auto),"PathPlanner .auto resolves sibling paths without NetworkTables");
   var plan=App.Project.localAuto;
   Check(plan.legs.Count==1&&Mathf.Abs(plan.legs[0].start-.28f)<.001f&&plan.cues.Count==2,"Imported .auto keeps wait, movement and named command order");
   plan.bindings["Release"]="shoot";
   var restored=ProjectIO.Parse(ProjectIO.Write(robot));
   Check(restored.version==2&&restored.localAuto.bindings["Release"]=="shoot"&&restored.localAuto.legs[0].points.Length==single.legs[0].points.Length,"Version 2 project preserves imported route and command mapping");
   string missing=Path.Combine(autos,"Broken.auto");
   File.WriteAllText(missing,"{'version':'2025.0','command':{'type':'path','data':{'pathName':'missing studio sample'}}}");
   Check(!await App.ImportPathPlanner(missing)&&App.Project.localAuto==plan,"An unresolved .path keeps the prior routine loaded");
   var red=App.AutoFieldPose(plan.legs[0].points[0],true);
   Check(Mathf.Abs(red.x-((float)App.Field.Length-2))<.01f&&Mathf.Abs(red.y-((float)App.Field.Width-4))<.01f&&Mathf.Abs(red.yaw-180)<.01f,"Red alliance rotates the imported field route and heading");
   Check(App.Nt==null&&App.BeginStandaloneAuto()&&App.RobotMode=="autonomous","An imported routine starts with no NT4 connection");
   await Task.Delay(135);
   Check(Math.Abs(App.Pose[0]-2)<.05&&Math.Abs(App.Pose[1]-4)<.05,"The file's opening wait holds the initial pose");
   await Wait(()=>App.StandaloneAutoFinished,"Offline auto reaches its final time");
   await Task.Delay(420);
   Check(Math.Abs(App.Pose[0]-4.4)<.25&&Math.Abs(App.Pose[1]-4.9)<.25&&Mathf.Abs(Mathf.DeltaAngle((float)App.Pose[2]*Mathf.Rad2Deg,45))<8,"The physical drivetrain follows the curved path and its holonomic finish rotation");
   Check(App.ShotsFired==1&&App.Stored("fuel")==0,"The named PathPlanner command triggers the mapped shot and consumes one game piece");
   App.SetRunning(false);Check(!App.StandaloneAutoRunning&&!App.Running,"Returning to editing cancels the standalone autonomous owner");
   App.RobotMode="teleop";
  }
 }
}
