using System;
using System.Linq;
using UnityEngine;

namespace AshinaStudio {
 public partial class StudioApp {
  bool quickSetup;int setupStep;Vector2 setupScroll;string setupSpeedUnit="mps";double setupDiameter=100,setupEfficiency=80,setupRatio=1;
  void StartQuickSetup(){quickSetup=true;setupStep=0;setupScroll=Vector2.zero;}
  double[] DefaultMuzzle(string partId){var point=Robot.TransformPoint(new Vector3(0,(float)Project.drive.dimensions[2]+.12f,(float)Project.drive.dimensions[0]/2+.12f));return Frc.Position(Nodes[partId].content.InverseTransformPoint(point));}
  RobotAction SetupEmitter(){var a=Project.actions.FirstOrDefault(x=>x.type=="emit");if(a==null){a=new RobotAction{type="emit",name="Atış",piece=Project.season=="2026"?"fuel":Project.season=="2024"?"note":"coral",partId=Selected,key="Space",button="rightBumper",maxShotSpeed=12,spreadDegrees=1,spawn=DefaultMuzzle(Selected)};Project.actions.Add(a);}return a;}
  internal bool FitMainRobot(){
   var p=Project.parts.Find(x=>x.id=="chassis");var node=Nodes[p.id];var renderers=node.content.GetComponentsInChildren<Renderer>(true);
   if(renderers.Length==0)return false;var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
   if(bounds.size.magnitude>20){Notice="Model çok büyük; önce milimetre ölçeğini seç.";return false;}
   p.position[2]-=bounds.min.y-Robot.position.y;node.transform.localPosition=Frc.Position(p.position);
   var local=new Bounds(Robot.InverseTransformPoint(bounds.center),Vector3.zero);
   for(int i=0;i<8;i++)local.Encapsulate(Robot.InverseTransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
   Project.drive.dimensions=new[]{(double)Mathf.Clamp(local.size.z+.04f,.1f,5),(double)Mathf.Clamp(local.size.x+.04f,.1f,5),(double)Mathf.Clamp(local.size.y,.1f,3)};fields.Clear();Notice="Model zemine oturdu; yaklaşık çarpışma boyutu ayarlandı.";return true;
  }
  void SetModelScale(double scale){var p=Project.parts.Find(x=>x.id=="chassis");p.scale=scale;var content=Nodes[p.id].content;if(content.childCount>0){content.GetChild(0).localScale=Vector3.one*(float)scale;content.GetChild(0).localPosition=-Frc.Position(p.assetCenter)*(float)scale;}fields.Clear();}
  void DrawQuickSetup(){
   if(!quickSetup||networkPanel)return;float width=Mathf.Min(650,Screen.width-60),height=Mathf.Min(720,Screen.height-110),x=(Screen.width-width)/2,y=(Screen.height-height)/2;
   GUI.Box(new Rect(x,y,width,height),"",panel);GUILayout.BeginArea(new Rect(x+22,y+18,width-44,height-36));GUILayout.BeginHorizontal();GUILayout.Label("HIZLI KURULUM  ·  "+(setupStep+1)+" / 4",title);GUILayout.FlexibleSpace();if(Button("Kapat"))quickSetup=false;GUILayout.EndHorizontal();
   setupScroll=GUILayout.BeginScrollView(setupScroll);GUI.enabled=!Busy&&!Running&&!FileDialogs.IsOpen;
   if(setupStep==0){Section("1  /  ANA ROBOT");Text("Robotunu tek model olarak yükle. Sabit teker ve tamponları ayırman gerekmiyor.");if(Button("Robot modelini yükle"))ImportDialog(true);GUILayout.BeginHorizontal();if(Button("Dosya metre ölçeğinde"))SetModelScale(1);if(Button("Dosya milimetre ölçeğinde"))SetModelScale(.001);GUILayout.EndHorizontal();if(Button("Modeli zemine oturt ve boyutu ayarla"))FitMainRobot();Text("CAD henüz hazır değilse mevcut basit gövdeyle devam edebilirsin.",true);}
   else if(setupStep==1){Section("2  /  HAREKETLİ MEKANİZMALAR · İSTEĞE BAĞLI");Text("Yalnız bağımsız dönen veya yükselen parçayı ekle. Tek modelle de devam edebilirsin.");if(Button("Seçili parçaya mekanizma modeli ekle"))ImportDialog();string next=Cycle("Düzenlenen parça",Selected,Project.parts.Select(p=>p.id).ToArray(),Project.parts.Select(p=>p.name).ToArray());if(next!=Selected)Select(next);if(Selected!="chassis")DrawPartLinks(Project.parts.Find(p=>p.id==Selected));Text("Açıyı robot kodu zaten hesaplıyorsa ilgili NT değerini seç. Birim ve ekseni bir kez ayarla.",true);}
   else if(setupStep==2){Section("3  /  ATIŞ VE CANLI DEĞERLER");var a=SetupEmitter();a.partId=Cycle("Eşyanın çıktığı parça",a.partId,Project.parts.Select(p=>p.id).ToArray(),Project.parts.Select(p=>p.name).ToArray());a.piece=Cycle("Eşya",a.piece,PieceCatalog.Names.Keys.ToArray(),PieceCatalog.Names.Values.ToArray());a.pitch=Num("setupPitch","Normal yukarı atış açısı · derece",a.pitch,-85,85);a.speed=Num("setupSpeed","Bağımsız kullanımda hız · m/s",a.speed,.1,40);DrawShotLinks(a);
    setupSpeedUnit=Cycle("Yayınlanan hızın birimi",setupSpeedUnit,new[]{"mps","rps","rpm"},new[]{"Top hızı · m/s","Teker devri · RPS","Teker devri · RPM"});if(setupSpeedUnit!="mps"){setupDiameter=Num("wheelDiameter","Atıcı teker çapı · mm",setupDiameter,10,500);setupEfficiency=Num("wheelEfficiency","Topa aktarım · % · kalibrasyon",setupEfficiency,10,100);setupRatio=Num("wheelRatio","Teker devri / yayınlanan motor devri",setupRatio,.01,20);}if(Button("Hız birimi dönüşümünü uygula")){a.speedScale=setupSpeedUnit=="mps"?1:Math.PI*setupDiameter/1000*(setupSpeedUnit=="rpm"?1.0/60:1)*setupEfficiency/100*setupRatio;a.speedOffset=0;fields.Clear();}Text("Teker çapı hesabı bir başlangıçtır; gerçek atışla kalibre et. Kodun yayınladığı açı ve hız canlı kullanılır.",true);
   }else{Section("4  /  DENE VE KAYDET");var a=SetupEmitter();Text("WPILib bağlı değilken normal açıyla veya sahada seçtiğin hedefe atabilirsin. Hedefli atış açıyı korur; maksimum gücü aşmaz.");DrawShotSetup(a);if(Button("Hedefi sahada düzenlemek için çalışma alanını aç")){quickSetup=false;uiTab=1;chosenAction=a.id;Select(a.partId);}Project.training.unlimitedShots=GUILayout.Toggle(Project.training.unlimitedShots," Kurulum sırasında sınırsız eşya");if(Button("Robot projesini kaydet"))SaveDialog();}
   GUI.enabled=true;GUILayout.EndScrollView();GUILayout.Space(8);Text(Notice,true);GUILayout.BeginHorizontal();GUI.enabled=setupStep>0;if(Button("Geri")){setupStep--;setupScroll=Vector2.zero;}GUI.enabled=!Busy;GUILayout.FlexibleSpace();if(Button(setupStep<3?"Devam":"Kurulumu bitir",true)){if(setupStep<3){setupStep++;setupScroll=Vector2.zero;}else quickSetup=false;}GUI.enabled=true;GUILayout.EndHorizontal();GUILayout.EndArea();
  }
 }
}
