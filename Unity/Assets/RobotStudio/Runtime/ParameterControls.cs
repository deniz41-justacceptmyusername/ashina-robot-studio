using System;
using System.Linq;
using UnityEngine;

namespace AshinaStudio {
 public partial class StudioApp {
  string bindingKind,bindingId;int bindingIndex;
  string pickingAction;int pickingStage;Vector3 zoneCorner;AimZone pendingZone;
  internal void BeginBinding(string kind,string id,int index=0){bindingKind=kind;bindingId=id;bindingIndex=index;networkPanel=true;nextTopicRefresh=0;selectedField="";}
  internal bool PickBinding(string topic,string field){
   var a=Project.actions.Find(x=>x.id==bindingId);var p=Project.parts.Find(x=>x.id==bindingId);
   if(bindingKind=="axis"&&p!=null&&bindingIndex<p.links.Count){p.links[bindingIndex].topic=topic;p.links[bindingIndex].field=field;}
   else if(a!=null){if(bindingKind=="pitch"){a.pitchTopic=topic;a.pitchField=field;}else if(bindingKind=="yaw"){a.yawTopic=topic;a.yawField=field;}else if(bindingKind=="speed"){a.speedTopic=topic;a.speedField=field;}else if(bindingKind=="trigger"){a.topic=topic;a.ntField=field;a.ntSource=true;}else return false;}
   else return false;
   networkPanel=false;bindingKind=null;fields.Clear();Notice="Canlı veri bağlandı; bağlantı açıkken değer otomatik alınır.";return true;
  }
  void BindingButton(string labelText,string topic,string kind,string id,int index=0){Text(string.IsNullOrEmpty(topic)?"Bağlı veri yok":topic,true);if(Button(labelText+" · NT'den seç"))BeginBinding(kind,id,index);}
  void DrawPartLinks(RobotPart p){
   Section("CANLI HAREKET · İSTEĞE BAĞLI");
   string[] properties={"rotation.z","rotation.y","rotation.x","position.z","position.x","position.y"};string[] names={"Yatay dönüş · Z","Dikey dönüş · Y","Eksen dönüşü · X","Yükseklik · Z","İleri / geri · X","Sağ / sol · Y"};
   for(int i=0;i<p.links.Count;i++){
    var link=p.links[i];string previousProperty=link.property;var available=properties.Where(prop=>prop==link.property||p.links.All(other=>other==link||other.property!=prop)).ToArray();link.property=Cycle("Kontrol",link.property,available,available.Select(prop=>names[Array.IndexOf(properties,prop)]).ToArray());
    if(link.property!=previousProperty){link.unit=link.property.StartsWith("rotation")?"degrees":"metres";fields.Clear();}
    link.unit=link.property.StartsWith("rotation")?Cycle("Birim",link.unit,new[]{"degrees","radians","rotations"},new[]{"Derece","Radyan","Tur"}):Cycle("Birim",link.unit,new[]{"metres","centimetres","millimetres"},new[]{"Metre","Santimetre","Milimetre"});
    if(link.property=="rotation.z")link.frame=Cycle("Yön referansı",link.frame,new[]{"part","field"},new[]{"Üst parçaya göre","Sahaya göre"});else link.frame="part";
    BindingButton("Hareket değeri",link.topic,"axis",p.id,i);link.scale=Num("axisScale"+p.id+i,"Çarpan · ters yön için -1",link.scale,-1000,1000);link.offset=Num("axisOffset"+p.id+i,"Veriye eklenecek sıfır ayarı",link.offset,-1000,1000);
    if(Button("Bu bağlantıyı kaldır")){p.links.RemoveAt(i);break;}GUILayout.Space(8);
   }
   if(p.links.Count<6&&Button("+ Canlı konum / dönüş bağla"))p.links.Add(new AxisLink{property=properties.FirstOrDefault(prop=>p.links.All(l=>l.property!=prop))??"rotation.z"});
  }
  void DrawShotSetup(RobotAction a){
   a.aimMode=Cycle("Nişan",a.aimMode,new[]{"fixed","point","zones"},new[]{"Ayarladığım açı","Bu noktaya at","Bölgeye göre hedef"});
   if(a.aimMode!="fixed"){
    Text("Normal yukarı atış açısı korunur. Gereken yön ve hız hesaplanır; güç yetmiyorsa atış yapılmaz.",true);
    if(a.aimMode=="point"){
     if(Button(pickingAction==a.id?"Sahada hedefe tıkla…":"Hedefi sahada seç")){quickSetup=false;uiTab=1;chosenAction=a.id;Select(a.partId);pickingAction=a.id;pickingStage=1;Notice="Sahada hedefe tıkla; yüksekliğini aşağıdan ayarla.";}
     Text($"Hedef: X {a.target[0]:0.00} · Y {a.target[1]:0.00} m",true);
    }else{
     foreach(var z in a.zones.ToArray()){Text($"{z.name} → X {z.target[0]:0.0}, Y {z.target[1]:0.0}",true);z.target[2]=Num("zoneHeight"+a.id+a.zones.IndexOf(z),"Hedef yüksekliği · m",z.target[2],0,5);if(actionDetails&&Button("Sil · "+z.name))a.zones.Remove(z);}
     if(a.zones.Count<20&&Button(pickingAction==a.id?"Sahadaki seçim adımını tamamla…":"+ Sahada bölge ve hedef çiz")){quickSetup=false;uiTab=1;chosenAction=a.id;Select(a.partId);pickingAction=a.id;pickingStage=2;pendingZone=new AimZone{name="Bölge "+(a.zones.Count+1),target=(double[])a.target.Clone()};Notice="Bölgenin ilk köşesine tıkla; sonra karşı köşe ve atış hedefini seç.";}
     a.zoneFallback=GUILayout.Toggle(a.zoneFallback," Bölge dışında varsayılan hedefe at");
    }
    a.target[2]=Num("targetHeight"+a.id,"Hedef yüksekliği · m · feed için yaklaşık 0.1",a.target[2],0,5);
   }
   a.maxShotSpeed=Num("maxShot"+a.id,"Maksimum çıkış hızı · m/s",a.maxShotSpeed,.1,40);a.spreadDegrees=Num("spread"+a.id,"Saçılma · en fazla ±derece",a.spreadDegrees,0,30);
  }
  void DrawShotLinks(RobotAction a){
   Section("CANLI ATIŞ DEĞERLERİ");Text("Bağladığın açı ve hızlar WPILib bağlıyken otomatik kullanılır; hedef yardımı bu değerleri değiştirmez.",true);
   a.directionFrame=Cycle("Açı referansı",a.directionFrame,new[]{"part","robot","field"},new[]{"Seçili mekanizma","Robot","Saha"});
   BindingButton("Yukarı açı",a.pitchTopic,"pitch",a.id);a.pitchUnit=Cycle("Yukarı açı birimi",a.pitchUnit,new[]{"degrees","radians","rotations"},new[]{"Derece","Radyan","Tur"});
   BindingButton("Yatay açı",a.yawTopic,"yaw",a.id);a.yawUnit=Cycle("Yatay açı birimi",a.yawUnit,new[]{"degrees","radians","rotations"},new[]{"Derece","Radyan","Tur"});
   BindingButton("Çıkış hızı",a.speedTopic,"speed",a.id);BindingButton("Atış izni / komut",a.topic,"trigger",a.id);
   if(Button("Sadece açı bağlantılarını temizle")){a.pitchTopic=a.yawTopic="";}
  }
  bool ProcessSceneTarget(bool viewport){
   if(pickingAction==null||!viewport||!Input.GetMouseButtonDown(0))return false;
   var a=Project.actions.Find(x=>x.id==pickingAction);if(a==null){pickingAction=null;return false;}
   if(!Physics.Raycast(ViewCamera.ScreenPointToRay(Input.mousePosition),out var hit,1000,1,QueryTriggerInteraction.Ignore))return true;
   var position=Frc.Position(hit.point);
   if(pickingStage==1){a.target[0]=position[0];a.target[1]=position[1];pickingAction=null;Notice="Atış hedefi seçildi. Yukarı açı korunacak.";}
   else if(pickingStage==2){zoneCorner=hit.point;pickingStage=3;Notice="Şimdi bölgenin karşı köşesine tıkla.";}
   else if(pickingStage==3){var first=Frc.Position(zoneCorner);pendingZone.center=new[]{(first[0]+position[0])/2,(first[1]+position[1])/2};pendingZone.size=new[]{Math.Max(.1,Math.Abs(first[0]-position[0])),Math.Max(.1,Math.Abs(first[1]-position[1]))};pickingStage=4;Notice="Bu bölgedeyken atılacak hedefe tıkla.";}
   else{pendingZone.target=new[]{position[0],position[1],a.target[2]};a.zones.Add(pendingZone);pendingZone=null;pickingAction=null;Notice="Bölge → hedef eşleşmesi kaydedildi.";}
   return true;
  }
 }
}
