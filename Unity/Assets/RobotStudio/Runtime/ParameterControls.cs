using System;
using System.Linq;
using UnityEngine;

namespace AshinaStudio {
 public partial class StudioApp {
  string bindingKind,bindingId;int bindingIndex;
  string pickingAction;int pickingStage;Vector3 zoneCorner;AimZone pendingZone;
  bool aimAdvanced;
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
  void DrawShotSetup(RobotAction action){
   DrawAimMode(action,"fixed","Ayarladığım yönde at");
   DrawAimMode(action,"point","Tek hedefe at");
   DrawAimMode(action,"zones","Bölgeye göre farklı hedeflere at");
   GUILayout.Space(8);
   action.pitch=Num("pitch"+action.id,"Dikey uçuş açısı · derece",action.pitch,-90,90);
   if(action.aimMode=="fixed"){
    action.yaw=Num("yaw"+action.id,"Yatay uçuş açısı · derece",action.yaw,-180,180);
    action.speed=Num("speed"+action.id,"Çıkış hızı · m/s",action.speed,0,40);
   }else{
    Text("Hedefe göre yatay yön ve hız ayarlanır. Dikey açı korunur.",true);
    if(action.aimMode=="zones")DrawShotZones(action);
    else DrawSingleShotTarget(action,false);
   }
   if(pickingAction==action.id){Text(Notice);if(Button("Saha seçimini iptal et"))CancelTargetPick();}
   aimAdvanced=Fold("Güç sınırı ve saçılma",aimAdvanced);
   if(aimAdvanced){
    action.maxShotSpeed=Num("maxShot"+action.id,"Maksimum çıkış hızı · m/s",action.maxShotSpeed,.1,40);
    action.spreadDegrees=Num("spread"+action.id,"Saçılma · en fazla ±derece",action.spreadDegrees,0,30);
   }
  }

  void DrawAimMode(RobotAction action,string mode,string name){
   if(Button(name,action.aimMode==mode)&&action.aimMode!=mode){action.aimMode=mode;CancelTargetPick();}
  }

  void DrawSingleShotTarget(RobotAction action,bool fallback){
   if(Button(fallback?"Bölge dışı hedefi sahada seç":"Hedefi sahada seç"))BeginShotPick(action,1);
   Text($"Hedef  X {action.target[0]:0.00} · Y {action.target[1]:0.00} m",true);
   action.target[2]=Num("targetHeight"+action.id,"Hedef yüksekliği · metre",action.target[2],0,5);
  }

  AimZone NewHalfZone(RobotAction action,bool first)=>new AimZone{
   name=first?"Mavi yarı saha":"Kırmızı yarı saha",center=new[]{Field.Length*(first?.25:.75),Field.Width/2},size=new[]{Field.Length/2,Field.Width},target=(double[])action.target.Clone()
  };

  void DrawShotZones(RobotAction action){
   Text("Her bölgeye ayrı hedef ver. Bölgeler üst üste gelirse daha küçük bölgenin hedefi kullanılır.",true);
   for(int i=0;i<action.zones.Count;i++){
    var zone=action.zones[i];Section(zone.name.ToUpperInvariant());
    Text($"Hedef  X {zone.target[0]:0.0} · Y {zone.target[1]:0.0} m",true);
    zone.target[2]=Num("zoneHeight"+action.id+i,"Hedef yüksekliği · metre",zone.target[2],0,5);
    GUILayout.BeginHorizontal();
    if(Button("Hedefi değiştir"))BeginShotPick(action,5,zone);
    if(Button("Sil")){CancelTargetPick();action.zones.RemoveAt(i);fields.Clear();i--;}
    GUILayout.EndHorizontal();
   }
   if(action.zones.Count<20){
    if(!action.zones.Any(zone=>zone.name=="Mavi yarı saha")&&Button("+ Mavi yarı saha"))BeginShotPick(action,4,NewHalfZone(action,true));
    if(!action.zones.Any(zone=>zone.name=="Kırmızı yarı saha")&&Button("+ Kırmızı yarı saha"))BeginShotPick(action,4,NewHalfZone(action,false));
    if(Button("+ Kendi bölgemi çiz"))BeginShotPick(action,2,new AimZone{name="Bölge "+(action.zones.Count+1),target=(double[])action.target.Clone()});
   }
   action.zoneFallback=GUILayout.Toggle(action.zoneFallback," Bölge dışında da hedefe at");
   if(action.zoneFallback)DrawSingleShotTarget(action,true);
  }

  void BeginShotPick(RobotAction action,int stage,AimZone zone=null){
   ClearActionAnchor(true);gizmo.Select(null,false);quickSetup=false;uiTab=1;chosenAction=action.id;
   pickingAction=action.id;pickingStage=stage;pendingZone=zone;GUI.FocusControl(null);
   ShowFieldForShotPick();
   Notice=stage==2?"Sahada bölgenin ilk köşesine tıkla.":"Sahada atışın gideceği hedefe tıkla.";
  }

  void ShowFieldForShotPick(){
   follow=false;cameraTarget=Frc.Position(new[]{Field.Length/2,Field.Width/2,.1});
   orbitYaw=0;orbitPitch=78;
   float fieldHeight=(float)Field.Length;
   float fieldWidth=(float)Field.Width/Mathf.Max(.4f,ViewCamera.aspect);
   orbitDistance=Mathf.Clamp(Mathf.Max(fieldHeight,fieldWidth)*1.22f,7,35);
  }
  void DrawShotLinks(RobotAction a){
   Section("CANLI ATIŞ DEĞERLERİ");Text("Canlı açılar ayarladığın yönde atışta kullanılır. Saha hedefi seçiliyse yönü hedef belirler, canlı hız üst sınır olur.",true);
   a.directionFrame=Cycle("Yatay yön referansı",a.directionFrame,new[]{"part","robot","field"},new[]{"Seçili mekanizma","Robot","Saha"});
   BindingButton("Dikey açı",a.pitchTopic,"pitch",a.id);a.pitchUnit=Cycle("Dikey açı birimi",a.pitchUnit,new[]{"degrees","radians","rotations"},new[]{"Derece","Radyan","Tur"});
   BindingButton("Yatay açı",a.yawTopic,"yaw",a.id);a.yawUnit=Cycle("Yatay açı birimi",a.yawUnit,new[]{"degrees","radians","rotations"},new[]{"Derece","Radyan","Tur"});
   BindingButton("Çıkış hızı",a.speedTopic,"speed",a.id);BindingButton("Atış izni / komut",a.topic,"trigger",a.id);
   if(Button("Sadece açı bağlantılarını temizle")){a.pitchTopic=a.yawTopic="";}
  }
  bool ProcessSceneTarget(bool viewport){
   if(pickingAction==null||!viewport||!Input.GetMouseButtonDown(0))return false;
   var a=Project.actions.Find(x=>x.id==pickingAction);if(a==null){CancelTargetPick();return false;}
   if(!Physics.Raycast(ViewCamera.ScreenPointToRay(Input.mousePosition),out var hit,1000,1,QueryTriggerInteraction.Ignore))return true;
   ApplyShotScenePick(a,hit.point);
   return true;
  }

  void ApplyShotScenePick(RobotAction a,Vector3 point){
   var position=Frc.Position(point);
   if(pickingStage==1){SetSceneShotTarget(a.target,position,a.piece);pickingAction=null;Notice="Atış hedefi seçildi. Dikey açı korunacak.";}
   else if(pickingStage==2){zoneCorner=point;pickingStage=3;Notice="Şimdi bölgenin karşı köşesine tıkla.";}
   else if(pickingStage==3){var first=Frc.Position(zoneCorner);pendingZone.center=new[]{(first[0]+position[0])/2,(first[1]+position[1])/2};pendingZone.size=new[]{Math.Max(.1,Math.Abs(first[0]-position[0])),Math.Max(.1,Math.Abs(first[1]-position[1]))};pickingStage=4;Notice="Bu bölgedeyken atılacak hedefe tıkla.";}
   else if(pendingZone!=null){
    SetSceneShotTarget(pendingZone.target,position,a.piece);
    if(pickingStage==4)a.zones.Add(pendingZone);
    CancelTargetPick();Notice="Bölgeye ayrı atış hedefi kaydedildi.";
   }
  }
  void SetSceneShotTarget(double[] target,double[] position,string piece){
   target[0]=position[0];target[1]=position[1];
   target[2]=Math.Min(5,Math.Max(position[2]+PieceCatalog.Radius(piece),PieceCatalog.Radius(piece)));
   fields.Clear();
  }
 }
}
