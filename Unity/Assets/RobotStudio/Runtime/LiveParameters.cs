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
  MatchActionInput ReadShotInput(RobotAction action){
   var input=new MatchActionInput{id=action.id,pitch=(float)action.pitch,yaw=(float)action.yaw};
   if(LiveNumber(action.pitchTopic,action.pitchField,out var pitch))input.pitch=(float)ShotPlanning.Angle(pitch,action.pitchUnit);
   if(LiveNumber(action.yawTopic,action.yawField,out var yaw))input.yaw=(float)ShotPlanning.Angle(yaw,action.yawUnit);
   input.liveSpeed=LiveNumber(action.speedTopic,action.speedField,out var speed);
   input.speed=Mathf.Clamp((float)(input.liveSpeed?speed*action.speedScale+action.speedOffset:action.speed),0,(float)action.maxShotSpeed);
   return input;
  }
  public bool PlanShot(RobotAction a,out ShotPlan plan,out string reason,Vector3? previewOrigin=null){
   var mount=Nodes[a.partId].content;
   var origin=previewOrigin??mount.TransformPoint(Frc.Position(a.spawn));
   var inherited=a.inheritVelocity?PointVelocity(origin,a.id):Vector3.zero;
   return ShotPlanning.Resolve(a,ReadShotInput(a),Robot,mount,origin,inherited,out plan,out reason);
  }
 }
}
