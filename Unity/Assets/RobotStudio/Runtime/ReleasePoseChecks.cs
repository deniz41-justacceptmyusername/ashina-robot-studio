using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace AshinaStudio {
 public partial class PlayerChecks {
  async Task ReleasePoseRegression(){
   EmptyStock();bool unlimited=App.Project.training.unlimitedShots;App.Project.training.unlimitedShots=true;
   var mount=App.Nodes["chassis"].content;var original=mount.localRotation;
   var a=new RobotAction{type="emit",partId="chassis",piece="coral",orientationMode="custom",piecePitch=55,pieceYaw=35,pieceRoll=20,pitch=12,yaw=-10,speed=4.5,coralSpin=0,inheritVelocity=false,spawn=new[]{0.0,0,1.2}};
   try {
    mount.localRotation=Frc.Rotation(new[]{90.0,0,40});var coral=App.Emit(a);
    var heading=Vector3.ProjectOnPlane(mount.forward,Vector3.up).normalized;
    var flight=Quaternion.AngleAxis(10,Vector3.up)*heading;
    flight=(flight*Mathf.Cos(12*Mathf.Deg2Rad)+Vector3.up*Mathf.Sin(12*Mathf.Deg2Rad))*4.5f;
    var axis=Quaternion.AngleAxis(-35,Vector3.up)*heading;
    axis=axis*Mathf.Cos(55*Mathf.Deg2Rad)+Vector3.up*Mathf.Sin(55*Mathf.Deg2Rad);
    Check(Vector3.Distance(coral.Body.linearVelocity,flight)<.001&&Vector3.Dot(coral.transform.up,axis)>.999f,"Yana 90° yatmış montajda uçuş ve coral duruşu bağımsız yatay ve dikey açıları korur");
    var pose=coral.transform.rotation;a.pitch=-15;a.yaw=60;coral=App.Emit(a);
    Check(Quaternion.Angle(pose,coral.transform.rotation)<.01&&Vector3.Distance(coral.Body.linearVelocity,flight)>1,"Uçuş açısını değiştirmek bağımsız coral duruşunu değiştirmez");
    flight=coral.Body.linearVelocity;a.piecePitch=0;a.pieceYaw=-30;a.pieceRoll=0;coral=App.Emit(a);
    Check(Vector3.Distance(coral.Body.linearVelocity,flight)<.001&&Quaternion.Angle(pose,coral.transform.rotation)>20,"Eşyanın duruş açısını değiştirmek çıkış hızını veya uçuş yönünü değiştirmez");
    var beforeRoll=coral.transform.rotation;var beforeAxis=coral.transform.up;a.pieceRoll=70;coral=App.Emit(a);
    Check(Quaternion.Angle(beforeRoll,coral.transform.rotation)>69&&Vector3.Dot(beforeAxis,coral.transform.up)>.999,"Kendi eksenindeki dönüş uzun eksenin yönünden bağımsız ayarlanabiliyor");
    mount.localRotation=original;mount.localRotation=Quaternion.identity;a.pitch=35;a.yaw=0;a.piecePitch=0;a.pieceYaw=0;a.pieceRoll=0;coral=App.Emit(a);
    Check(Mathf.Abs(coral.transform.up.y)<.001&&coral.Body.linearVelocity.y>2&&coral.Body.angularVelocity.sqrMagnitude<.001,"Coral yere paralel dururken yukarı doğru fırlatılabiliyor ve başlangıçta dönmüyor");
    a.pitch=0;a.piecePitch=90;coral=App.Emit(a);
    Check(Vector3.Dot(coral.transform.up,Vector3.up)>.999&&Mathf.Abs(coral.Body.linearVelocity.y)<.001,"Coral dik dururken yatay yönde fırlatılabiliyor");
    a.piece="note";a.piecePitch=0;var note=App.Emit(a);a.piecePitch=60;var tilted=App.Emit(a);
    Check(Mathf.Abs(note.transform.up.y-1)<.001&&Mathf.Abs(tilted.transform.up.y-.5f)<.001&&Vector3.Distance(note.Body.linearVelocity,tilted.Body.linearVelocity)<.001,"Bağımsız duruş ayarı coral dışındaki oyun eşyalarında da çalışıyor");
    var saved=ProjectIO.Parse(ProjectIO.Write(App.Project));a.piece="coral";a.piecePitch=55;a.pieceYaw=-40;a.pieceRoll=30;saved.actions.Add(a);var restored=ProjectIO.Parse(ProjectIO.Write(saved)).actions.First(x=>x.id==a.id);
    Check(restored.orientationMode=="custom"&&restored.piecePitch==55&&restored.pieceYaw==-40&&restored.pieceRoll==30&&restored.coralSpin==0,"Bağımsız eşya duruşu ve sıfır dönüş robot dosyasında korunuyor");
    var legacy=JObject.Parse(ProjectIO.Write(saved));foreach(JObject action in legacy["actions"]){action.Remove("orientationMode");action.Remove("piecePitch");action.Remove("pieceYaw");action.Remove("pieceRoll");}
    Check(ProjectIO.Parse(legacy.ToString()).actions.All(x=>x.orientationMode=="auto"),"Eski projeler yeni duruş alanları olmadan otomatik modda açılıyor");
   }finally{mount.localRotation=original;App.Project.training.unlimitedShots=unlimited;EmptyStock();}
   await Task.Yield();
  }
 }
}
