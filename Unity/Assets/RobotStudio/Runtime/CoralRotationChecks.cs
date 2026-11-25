using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
namespace AshinaStudio {
 public partial class PlayerChecks {
  async Task CoralRotationRegression(){
   EmptyStock();bool unlimited=App.Project.training.unlimitedShots;App.Project.training.unlimitedShots=true;
   var mount=App.Nodes["chassis"].content;var original=mount.localRotation;
   var action=new RobotAction{type="emit",partId="chassis",piece="coral",speed=1.2,pitch=25,yaw=20,coralSpin=180,inheritVelocity=false};
   var floor=new GameObject("Coral dönüş fizik testi zemini");floor.transform.position=Frc.Position(new[]{1.0,-2.0,-.06});var box=floor.AddComponent<BoxCollider>();box.size=new Vector3(4,.12f,4);
   try {
    mount.localRotation=Frc.Rotation(new[]{15.0,0,30});action.spawn=Frc.Position(mount.InverseTransformPoint(Frc.Position(new[]{1.0,-2.0,1.2})));
    var coral=App.Emit(action);
    Vector3 direction=coral.Body.linearVelocity.normalized;
    Check(Vector3.Dot(coral.transform.up,direction)>.999f&&Mathf.Abs(coral.transform.up.y)<.95f,"Coral uzun ekseni dönmüş montajın atış yönüne hizalanıyor; dik oluşturulmuyor");
    Check(Math.Abs(coral.Body.angularVelocity.magnitude-Math.PI)<.001&&coral.Body.constraints==RigidbodyConstraints.None,"Coral ayarlanan 180 derece/sn hızıyla serbestçe dönebiliyor");
    var initial=coral.Body.rotation;await Task.Delay(230);
    Check(Quaternion.Angle(initial,coral.Body.rotation)>20,"Coral uçuş sırasında yön değiştiriyor; ilk açıya sabitlenmiyor");
    await Wait(()=>Mathf.Abs((coral.Body.rotation*Vector3.up).y)<.3f&&coral.Body.position.y<.12f&&coral.Body.linearVelocity.sqrMagnitude<.03f,"Coral zemine çarpınca dönüp yan yatıyor",6000);
    App.Pieces.Clear();mount.localRotation=original;action.spawn=new[]{0.0,0,1.0};action.pitch=0;action.yaw=0;action.coralSpin=0;
    coral=App.Emit(action);Check(Mathf.Abs(coral.transform.up.y)<.001&&coral.Body.angularVelocity.sqrMagnitude<.0001,"Sıfır eğim ve sıfır dönüş seçilince coral yatay ve dönmeden çıkıyor");
    App.Pieces.Clear();action.pitch=90;action.coralSpin=-720;coral=App.Emit(action);
    Check(coral.Body.angularVelocity.magnitude>12&&coral.Body.maxAngularVelocity>12&&Mathf.Abs(Vector3.Dot(coral.Body.angularVelocity.normalized,coral.transform.up))<.001,"Dikey atışta ve ters yönde hızlı dönüşte geçerli fiziksel dönme ekseni korunuyor");
    var saved=ProjectIO.Parse(ProjectIO.Write(App.Project));var emit=saved.actions.FirstOrDefault(a=>a.type=="emit");
    if(emit==null){emit=new RobotAction{type="emit",partId="chassis"};saved.actions.Add(emit);}emit.coralSpin=-270;
    Check(ProjectIO.Parse(ProjectIO.Write(saved)).actions.First(a=>a.id==emit.id).coralSpin==-270,"Coral dönüş hızı robot dosyasına kaydedilip geri açılıyor");
   }finally{mount.localRotation=original;App.Project.training.unlimitedShots=unlimited;EmptyStock();Lifetime.Destroy(floor);}
  }
 }
}
