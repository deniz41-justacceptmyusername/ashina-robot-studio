using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshinaStudio {
 public struct ShotPlan {public Vector3 Origin,Direction,Inherited;public float Speed,Flight;}
 public partial class StudioApp {
  readonly Dictionary<string,double> lastAxisValues=new Dictionary<string,double>();
  readonly Dictionary<string,Vector3> lastMuzzleLocal=new Dictionary<string,Vector3>(),muzzleRelativeVelocity=new Dictionary<string,Vector3>();
  readonly System.Random shotRandom=new System.Random(10576);
  Vector3 observedVelocity;float observedOmega;long observedAt;Vector3 previousObservedPosition;float previousObservedYaw;
  bool LiveNumber(string topic,string field,out double number){number=0;return Nt?.Connected==true&&!string.IsNullOrWhiteSpace(topic)&&NtData.Scalar(Nt,Nt.Get(topic),field,out number);}
  void ApplyLiveParameters(){
   foreach(var part in Project.parts)foreach(var link in part.links){
    string key=part.id+":"+link.property;double raw;
    if(LiveNumber(link.topic,link.field,out raw))lastAxisValues[key]=raw;else if(!lastAxisValues.TryGetValue(key,out raw))continue;
    double value=raw*link.scale+link.offset;bool rotation=link.property.StartsWith("rotation");
    value=rotation?ShotPlanning.Angle(value,link.unit):value*(link.unit=="millimetres"?.001:link.unit=="centimetres"?.01:1);
    int axis=link.property.EndsWith("x")?0:link.property.EndsWith("y")?1:2;var node=Nodes[part.id].transform;
    if(rotation){var angles=Frc.Euler(node.localRotation);bool fieldYaw=link.frame=="field"&&axis==2;if(fieldYaw){float parentYaw=Mathf.Atan2(-node.parent.forward.x,node.parent.forward.z)*Mathf.Rad2Deg;value=Mathf.DeltaAngle(parentYaw,(float)value);}angles[axis]=(fieldYaw?0:part.rotation[axis])+value;node.localRotation=Frc.Rotation(angles);}
    else{var position=Frc.Position(node.localPosition);position[axis]=part.position[axis]+value;node.localPosition=Frc.Position(position);}
   }
  }
  void SampleMuzzles(){
   foreach(var a in Project.actions){if(a.type!="emit")continue;var position=Robot.InverseTransformPoint(Nodes[a.partId].content.TransformPoint(Frc.Position(a.spawn)));
    muzzleRelativeVelocity[a.id]=lastMuzzleLocal.TryGetValue(a.id,out var last)?Vector3.ClampMagnitude((position-last)/Time.fixedDeltaTime,20):Vector3.zero;lastMuzzleLocal[a.id]=position;
   }
  }
  void ClearLiveMotion(){lastAxisValues.Clear();lastMuzzleLocal.Clear();muzzleRelativeVelocity.Clear();observedVelocity=Vector3.zero;observedOmega=0;observedAt=0;}
  void ObserveMotion(Vector3 position,float yaw,long received){
   if(received==observedAt)return;
   if(observedAt!=0){double dt=(received-observedAt)/(double)TimeSpan.TicksPerSecond;if(dt>.002&&dt<.5){observedVelocity=Vector3.ClampMagnitude((position-previousObservedPosition)/(float)dt,15);observedOmega=Mathf.Clamp(Mathf.DeltaAngle(previousObservedYaw*Mathf.Rad2Deg,yaw*Mathf.Rad2Deg)*Mathf.Deg2Rad/(float)dt,-15,15);}else{observedVelocity=Vector3.zero;observedOmega=0;}}
   previousObservedPosition=position;previousObservedYaw=yaw;observedAt=received;
  }
  Vector3 PointVelocity(Vector3 point,string actionId){
   Vector3 result=hull!=null&&!hull.Body.isKinematic?hull.Body.GetPointVelocity(point):observedAt!=0&&DateTime.UtcNow.Ticks-observedAt<TimeSpan.TicksPerMillisecond*300?observedVelocity+Vector3.Cross(Vector3.down*observedOmega,point-Robot.position):Vector3.zero;
   if(muzzleRelativeVelocity.TryGetValue(actionId,out var relative))result+=Robot.TransformDirection(relative);return result;
  }
  public bool PlanShot(RobotAction a,out ShotPlan plan,out string reason){
   var node=Nodes[a.partId].content;plan=new ShotPlan{Origin=node.TransformPoint(Frc.Position(a.spawn))};reason="";
   double pitch=a.pitch,yaw=a.yaw,speed=a.speed;bool code=false;
   if(LiveNumber(a.pitchTopic,a.pitchField,out var pv)){pitch=ShotPlanning.Angle(pv,a.pitchUnit);code=true;}
   if(LiveNumber(a.yawTopic,a.yawField,out var yv)){yaw=ShotPlanning.Angle(yv,a.yawUnit);code=true;}
   if(LiveNumber(a.speedTopic,a.speedField,out var sv)){speed=sv*a.speedScale+a.speedOffset;code=true;}
   Quaternion frame=a.directionFrame=="field"?Quaternion.identity:a.directionFrame=="robot"?Robot.rotation:node.rotation;
   plan.Direction=(frame*Frc.Direction(pitch,yaw)).normalized;plan.Inherited=a.inheritVelocity?PointVelocity(plan.Origin,a.id):Vector3.zero;
   if(a.aimMode!="fixed"&&!code){
    double[] target=a.target;
    if(a.aimMode=="zones"){
     var position=Frc.Position(Robot.position);var zone=a.zones.Find(z=>Math.Abs(position[0]-z.center[0])<=z.size[0]/2&&Math.Abs(position[1]-z.center[1])<=z.size[1]/2);
     if(zone!=null)target=zone.target;else if(!a.zoneFallback){reason="Bu bölgede atış hedefi tanımlı değil.";return false;}
    }
    float elevation=Mathf.Atan2(plan.Direction.y,new Vector2(plan.Direction.x,plan.Direction.z).magnitude)*Mathf.Rad2Deg;
    if(!ShotPlanning.FixedElevation(plan.Origin,Frc.Position(target),plan.Inherited,elevation,(float)a.maxShotSpeed,out var launch,out var flight)){reason="Normal atış açısı ve maksimum güç bu hedefe yetmiyor.";return false;}
    plan.Direction=launch.normalized;speed=launch.magnitude;plan.Flight=flight;
   }
   plan.Speed=Mathf.Clamp((float)speed,0,(float)a.maxShotSpeed);return true;
  }
 }
}
