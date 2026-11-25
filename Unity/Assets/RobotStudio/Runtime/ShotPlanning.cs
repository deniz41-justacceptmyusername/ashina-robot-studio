using System;
using System.Linq;
using UnityEngine;

namespace AshinaStudio {
 public static class ShotPlanning {
  internal static Quaternion LevelHeading(Quaternion orientation){
   var forward=Vector3.ProjectOnPlane(orientation*Vector3.forward,Vector3.up);
   if(forward.sqrMagnitude<.0001f)forward=Vector3.Cross(orientation*Vector3.right,Vector3.up);
   return Quaternion.LookRotation(forward,Vector3.up);
  }

  internal static bool Resolve(RobotAction action,MatchActionInput input,Transform robot,Transform mount,Vector3 origin,Vector3 inherited,out ShotPlan plan,out string reason){
   var orientation=action.directionFrame=="field"?Quaternion.identity:action.directionFrame=="robot"?robot.rotation:mount.rotation;
   float maximum=(float)action.maxShotSpeed;
   plan=new ShotPlan{Origin=origin,Inherited=inherited,Direction=LevelHeading(orientation)*Frc.Direction(input.pitch,input.yaw),Speed=Mathf.Clamp(input.speed,0,maximum)};
   reason="";
   if(action.aimMode=="fixed")return true;
   double[] target=action.target;
   if(action.aimMode=="zones"){
    var position=Frc.Position(robot.position);
    var zone=ZoneAt(action,position);
    if(zone!=null)target=zone.target;
    else if(!action.zoneFallback){reason="Bu bölgede atış hedefi tanımlı değil.";return false;}
   }
   if(input.liveSpeed)maximum=plan.Speed;
   if(!FixedElevation(origin,Frc.Position(target),inherited,(float)action.pitch,maximum,out var launch,out var flight)){
    reason=input.liveSpeed?"Dikey açı ve robot kodunun verdiği hız bu hedefe yetmiyor.":"Dikey açı ve maksimum hız bu hedefe yetmiyor.";
    return false;
   }
   plan.Direction=launch.normalized;plan.Speed=launch.magnitude;plan.Flight=flight;return true;
  }

  internal static AimZone ZoneAt(RobotAction action,double[] position){
   AimZone match=null;double area=double.PositiveInfinity;
   foreach(var zone in action.zones){
    if(Math.Abs(position[0]-zone.center[0])>zone.size[0]/2||Math.Abs(position[1]-zone.center[1])>zone.size[1]/2)continue;
    double size=zone.size[0]*zone.size[1];
    if(size<=area){match=zone;area=size;}
   }
   return match;
  }

  // Solve flight time while preserving the configured elevation. No trajectory
  // assistance runs after the piece is released.
  public static bool FixedElevation(Vector3 origin,Vector3 target,Vector3 inherited,float degrees,float maximum,out Vector3 relative,out float flight){
   relative=Vector3.zero;flight=0;if(maximum<=0||Mathf.Abs(degrees)>=89.9f)return false;
   float tangent=Mathf.Tan(degrees*Mathf.Deg2Rad);
   Func<float,Vector3> required=t=>{Coefficients(t,out var a,out var b);return (target-origin-Physics.gravity*b)/a-inherited;};
   Func<float,float> error=t=>{var v=required(t);return v.y-new Vector2(v.x,v.z).magnitude*tangent;};
   float previous=.015f,last=error(previous);
   for(float t=.035f;t<=8;t+=.02f){float next=error(t);if(last*next<=0){
     float low=previous,high=t;for(int i=0;i<24;i++){float mid=(low+high)*.5f;if(error(low)*error(mid)<=0)high=mid;else low=mid;}
     float time=(low+high)*.5f;var v=required(time);
     if(v.magnitude<=maximum+.0001f&&v.magnitude>.01f){relative=v;flight=time;return true;}
   }previous=t;last=next;}return false;
  }
  public static void Coefficients(float t,out float a,out float b){const float drag=.06f;a=(1-Mathf.Exp(-drag*t))/drag;b=(t-a)/drag;}
  public static Vector3 Position(Vector3 origin,Vector3 velocity,float time){Coefficients(time,out var a,out var b);return origin+velocity*a+Physics.gravity*b;}
  public static double Angle(double value,string unit)=>unit=="radians"?value*180/Math.PI:unit=="rotations"?value*360:value;
  public static Vector3 Scatter(Vector3 direction,float degrees,System.Random random){
   if(degrees<=0)return direction;var up=Mathf.Abs(Vector3.Dot(direction,Vector3.up))>.95f?Vector3.right:Vector3.up;
   var x=Vector3.Cross(direction,up).normalized;var y=Vector3.Cross(direction,x);
   float cosine=Mathf.Lerp(1,Mathf.Cos(degrees*Mathf.Deg2Rad),(float)random.NextDouble());
   float sine=Mathf.Sqrt(Mathf.Max(0,1-cosine*cosine)),azimuth=(float)random.NextDouble()*Mathf.PI*2;
   return (direction*cosine+(x*Mathf.Cos(azimuth)+y*Mathf.Sin(azimuth))*sine).normalized;
  }
 }
 public static partial class AimValidation {
  public static void Validate(RobotProject p){
   foreach(var part in p.parts){if(part.links==null||part.links.Count>6)throw new Exception("Canlı hareket bağlantısı geçersiz.");foreach(var link in part.links){
    if(link==null||!new[]{"rotation.x","rotation.y","rotation.z","position.x","position.y","position.z"}.Contains(link.property)||!new[]{"degrees","radians","rotations","metres","centimetres","millimetres"}.Contains(link.unit)||!new[]{"part","field"}.Contains(link.frame)||link.topic==null||link.topic.Length>1000||link.field==null||link.field.Length>256||!StudioApp.Finite(link.scale)||Math.Abs(link.scale)>100000||!StudioApp.Finite(link.offset)||Math.Abs(link.offset)>100000)throw new Exception("Canlı hareket ayarı geçersiz.");
   }}
   foreach(var a in p.actions){if(!new[]{"fixed","point","zones"}.Contains(a.aimMode)||!new[]{"part","robot","field"}.Contains(a.directionFrame)||!new[]{"degrees","radians","rotations"}.Contains(a.pitchUnit)||!new[]{"degrees","radians","rotations"}.Contains(a.yawUnit)||!StudioApp.Finite(a.maxShotSpeed)||a.maxShotSpeed<.1||a.maxShotSpeed>40||!StudioApp.Finite(a.spreadDegrees)||a.spreadDegrees<0||a.spreadDegrees>30)throw new Exception("Atış ayarı geçersiz.");
    Vector(a.target,3);if(a.zones==null||a.zones.Count>20)throw new Exception("En fazla 20 atış bölgesi olabilir.");
    foreach(var z in a.zones){if(z==null||z.name==null||z.name.Length>100)throw new Exception("Bölge kaydı geçersiz.");Vector(z.center,2);Vector(z.size,2);Vector(z.target,3);if(z.size.Any(v=>v<.01||v>40))throw new Exception("Bölge boyutu geçersiz.");}
    foreach(var text in new[]{a.pitchTopic,a.yawTopic,a.pitchField,a.yawField})if(text==null||text.Length>1000)throw new Exception("Atış veri bağlantısı geçersiz.");
   }
  }
  static void Vector(double[] a,int length){if(a==null||a.Length!=length||a.Any(n=>!StudioApp.Finite(n)||Math.Abs(n)>1000))throw new Exception("Hedef koordinatı geçersiz.");}
 }
}
