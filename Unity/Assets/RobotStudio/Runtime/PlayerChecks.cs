using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
namespace AshinaStudio {
 public partial class PlayerChecks : MonoBehaviour {
  public StudioApp App;readonly List<string> passed=new List<string>();string root;
  void Check(bool condition,string name){if(!condition)throw new Exception(name);passed.Add(name);Debug.Log("PASS: "+name);}
  async Task Wait(Func<bool> test,string name,int ms=12000){var start=DateTime.UtcNow;while(!test()){if((DateTime.UtcNow-start).TotalMilliseconds>ms)throw new Exception("Timeout: "+name);await Task.Delay(30);}Check(true,name);}
  async void Start(){var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--test-dir");root=at>=0?args[at+1]:Path.Combine(Application.persistentDataPath,"checks");Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,".ashina-test-data"),"test");try{await Task.Delay(500);await Run();File.WriteAllText(Path.Combine(root,"result.json"),JsonConvert.SerializeObject(new{success=true,passed,count=passed.Count},Formatting.Indented));Application.Quit(0);}catch(Exception e){Debug.LogException(e);File.WriteAllText(Path.Combine(root,"result.json"),JsonConvert.SerializeObject(new{success=false,passed,count=passed.Count,error=e.ToString()},Formatting.Indented));Application.Quit(2);}}
  async Task Run(){
   Check(App.Field.Tags.Count==22,"2025 resmi AprilTag yerleşimi");Check(!App.Field.Root.GetComponentsInChildren<Transform>().Any(t=>t.name=="ALGAE (GE-25501)"),"Saha modelindeki sabit oyun eşyaları kaldırılıyor");Check(App.Field.Root.GetComponentsInChildren<MeshCollider>().Length>0,"2025 gerçek saha çarpışma yüzeyi");
   var point=new[]{2.3,-4.6,.8};Check(Frc.Position(Frc.Position(point)).Zip(point,(a,b)=>Math.Abs(a-b)).Max()<.00001,"FRC metre koordinatı gidiş dönüş");var angles=new[]{23.0,-17,125};var rotation=Frc.Rotation(angles);Check(Quaternion.Angle(rotation,Frc.Rotation(Frc.Euler(rotation)))<.05,"FRC dönüş koordinatı gidiş dönüş");Check(Vector3.Distance(Frc.Rotation(new[]{0.0,0,90})*Vector3.forward,Vector3.left)<.001,"Pozitif FRC yaw yönü");
   var project=RobotProject.Empty();project.season="practice";project.parts.Add(new RobotPart{id="test-arm",name="Takımın özel parçası",position=new[]{.15,0,.4},parent="chassis"});project.parts.Add(new RobotPart{id="test-child",name="Bağlı uç",position=new[]{.3,0,0},parent="test-arm"});var action=new RobotAction{id="test-move",name="Özel eksen hareketi",partId="test-arm",duration=.15,from=project.parts[1].Copy(),to=project.parts[1].Copy(),key="",button=""};action.to.position[2]+=.5;action.to.rotation[2]=90;project.actions.Add(action);await App.LoadProject(project);Check(App.Nodes["test-child"].transform.parent==App.Nodes["test-arm"].content,"Serbest parça hiyerarşisi");var childBefore=App.Nodes["test-child"].transform.position;App.SetRunning(true);App.CheckInput=true;await Task.Delay(350);Check(Math.Abs(App.Nodes["test-arm"].transform.localPosition.y-.9)<.02,"Tetiklenen genel parça hareketi");Check(Quaternion.Angle(App.Nodes["test-arm"].transform.localRotation,Frc.Rotation(new[]{0.0,0,90}))<.1,"Tetiklenen eksen dönüşü");Check(Vector3.Distance(childBefore,App.Nodes["test-child"].transform.position)>.45,"Alt parça mekanizmayı izliyor");App.CheckInput=false;await Task.Delay(350);Check(Math.Abs(App.Nodes["test-arm"].transform.localPosition.y-.4)<.02,"Tuş bırakıldığında geri dönüş");App.SetRunning(false);App.CheckInput=null;
   string save=Path.Combine(root,"roundtrip.ashina.json");App.Save(save);var reloaded=ProjectIO.Parse(File.ReadAllText(save));Check(reloaded.actions[0].to.rotation[2]==90&&reloaded.parts.Count==3,"Proje kaydet / aç hareketleri koruyor");var invalid=ProjectIO.Parse(File.ReadAllText(save));invalid.parts[1].parent="test-child";bool rejected=false;try{ProjectIO.Validate(invalid);}catch{rejected=true;}Check(rejected,"Döngü oluşturan montaj reddediliyor");invalid=ProjectIO.Parse(File.ReadAllText(save));invalid.parts[0].position[0]=double.NaN;rejected=false;try{ProjectIO.Validate(invalid);}catch{rejected=true;}Check(rejected,"Geçersiz sayısal proje reddediliyor");
   var stl=ModelLoader.ReadStl(Encoding.ASCII.GetBytes("solid a\nfacet normal 0 0 1\nouter loop\nvertex 0 0 0\nvertex 1 0 0\nvertex 0 1 0\nendloop\nendfacet\nendsolid"));Check(stl.vertexCount==3&&stl.triangles.Length==3,"ASCII STL yükleniyor");var obj=ModelLoader.ReadObj("v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nf -4 -3 -2 -1");Check(obj.triangles.Length==6,"OBJ çokgen ve negatif indis desteği");string model=Path.Combine(Application.streamingAssetsPath,"assets/fields/2025/model_0.glb");await App.ImportModel(model);Check(App.Project.assets.Count==1&&App.Nodes.Count==4,"Çalışan player içine GLB model yükleniyor");
   await ImportRegression();
   await BridgeRegression();
   App.Project.inventory["coral"]=1;var emitter=new RobotAction{type="emit",partId="test-arm",spawn=new[]{.4,0,.5},speed=5,pitch=30,piece="coral"};var emitted=App.Emit(emitter);Check(emitted!=null&&Math.Abs(emitted.Body.linearVelocity.magnitude-5)<.01,"Seçilen açı ve hızla coral çıkışı");Check(emitted.Body.linearVelocity.y>2.4,"Fırlatma açısı fiziğe aktarılıyor");App.Pieces.Clear();var piece=App.Pieces.Spawn("ball",Frc.Position(new[]{1.0,1,2}),Vector3.zero,Quaternion.identity);float startY=piece.transform.position.y;await Task.Delay(450);Check(piece.transform.position.y<startY-.5f,"Unity yerçekimi çalışıyor");await Task.Delay(1800);Check(piece.transform.position.y>.09f&&piece.transform.position.y<.25f,"Oyun eşyası zemine çarpıp duruyor");foreach(var kind in PieceCatalog.Names.Keys)App.Pieces.Spawn(kind,Frc.Position(new[]{1.0,1,.4}),Vector3.zero,Quaternion.identity);Check(App.Pieces.Capture("note",Frc.Position(new[]{1.0,1,.4}),1)==1,"Türüne göre oyun eşyası toplama");App.Pieces.Clear();
   await App.LoadField("2024");Check(App.Field.Tags.Count==16,"2024 saha ve 16 AprilTag");await App.LoadField("2026");Check(App.Field.Tags.Count==32,"2026 saha ve 32 AprilTag");await TrainingRegression();await TerrainRegression();await App.LoadField("2025");await ReefInventoryRegression();
   var mount=new GameObject("Test kamerası").transform;mount.position=Frc.Position(new[]{2.0,4,.8});var camera=new CameraData{position=new[]{0.0,0,0},rotation=new[]{0.0,0,0},hfov=90,vfov=80,range=10,latency=30};var seen=Vision.Detect(camera,mount,App.Field,new[]{2.0,4,0});Check(seen.Seen.Any(t=>t.Id==18),"Kamera reef AprilTag 18'i görüyor");camera.rotation=new[]{0.0,0,180};var behind=Vision.Detect(camera,mount,App.Field,new[]{2.0,4,0});Check(!behind.Seen.Any(t=>t.Id==18),"Kameranın arkasındaki etiket görünmüyor");Lifetime.Destroy(mount.gameObject);
   byte[] packet=MsgPack.Encode(new object[]{100L,12345678L,17L,new[]{2.0,4,1.5}});int cursor=0;var decoded=(object[])MsgPack.Read(packet,ref cursor);Check(decoded[0] is long&&((object[])decoded[3])[0] is double,"NT4 zarf tamsayı, ölçümler double");
   App.Project.cameras.Clear();App.Project.cameras.Add(new CameraData{name="limelight",parent="chassis",position=new[]{0.0,0,.6},hfov=90,vfov=80,range=10,latency=30});App.Project.network.port=5811;App.Project.network.mode="observe";App.Project.network.vision=true;App.Nt=new Nt4Client();await App.Nt.Connect("127.0.0.1",5811);await Wait(()=>App.Nt.Get("/FRCStudio/Drive/Pose")!=null,"Gerçek WPILib NT4 Pose2d alımı");App.SetRunning(true);await Task.Delay(700);Check(Math.Abs(App.Pose[0]-2)<.03&&Math.Abs(App.Pose[1]-4)<.03,"WPILib Pose2d robotu sahaya yerleştiriyor");App.Project.network.mode="physics";double beforeX=App.Pose[0];await Task.Delay(700);Check(App.Pose[0]>beforeX+.2,"WPILib hız isteği robot fiziğini sürüyor");App.SetRunning(false);App.Project.network.mode="observe";App.SetRunning(true);await Task.Delay(500);App.SetRunning(false);
   await Wait(()=>App.Nt.Get("/Fixture/poseReceived")?.Value is bool p&&p,"WPILib sunucusu Unity robot konumunu gerçekten alıyor");await Wait(()=>App.Nt.Get("/Fixture/visionReceived")?.Value is bool v&&v,"WPILib sunucusu Limelight konum ve görünürlük verisini gerçekten alıyor");await NetworkRegression();App.Nt.Dispose();App.Nt=null;App.Project.actions[0].topic="IntakeSubsystem.intakeMotor";App.SetRunning(true);File.WriteAllText(Path.Combine(root,"bridge.json"),JsonConvert.SerializeObject(new{token=App.Bridge.Token,port=App.Bridge.Port,blockly=Path.Combine(Application.streamingAssetsPath,"blockly/index.html")}));await Wait(()=>File.Exists(Path.Combine(root,"blockly-done")),"Gerçek Blockly arayüzü Unity'ye bağlanıyor",60000);Check(App.Project.blocklyProject!=null,"Canlı Blockly proje JSON'u alınıyor");Check(App.Bridge.Connected,"Blockly eşlenmiş bağlantı açık");await Task.Delay(500);Check(App.Bridge.Fresh,"Blockly 20 ms döngüsü güncel");Check(App.Nodes["test-arm"].transform.localPosition.y>.85,"Gerçek Blockly motor bloğu özel parça hareketini tetikliyor");
   var reload=ProjectIO.Parse(ProjectIO.Write(App.Project));reload.blocklyProject["name"]="UnityReloadCheck";int updates=App.BlocklyProjectUpdates;
   Check(await App.LoadProject(reload),"Bağlı Blockly projesi içeren robot tekrar açılıyor");
   await Wait(()=>App.BlocklyProjectUpdates>updates&&(string)App.Project.blocklyProject?["name"]=="UnityReloadCheck","Robot dosyası değişince bağlı Blockly yeni programı alıyor");await SingleRobotRegression();
  }
  async Task ImportRegression()
  {
   Check(FileDialogs.NativeStructureSize==(IntPtr.Size==8?152:88),"Windows dosya seçici bellek düzeni doğru");
   int field=App.Field.Root.GetInstanceID(),count=App.Nodes.Count,assets=App.Project.assets.Count;var originalRoot=App.Robot;
   string broken=Path.Combine(root,"Bozuk model.glb");File.WriteAllText(broken,"broken");
   Check(!await App.ImportModel(broken),"Bozuk GLB anlaşılır hata ile reddediliyor");
   Check(App.Nodes.Count==count&&App.Project.assets.Count==assets&&!App.Busy&&App.Robot==originalRoot,"Başarısız model yükleme mevcut robotu koruyor");
   string unsupported=Path.Combine(root,"model.step");File.WriteAllText(unsupported,"ISO-10303-21;");
   Check(!await App.ImportModel(unsupported)&&App.Nodes.Count==count,"Desteklenmeyen model robotu bozmuyor");
   string ascii=Path.Combine(root,"Türkçe parça.stl");File.WriteAllText(ascii,"solid a\nfacet normal 0 0 1\nouter loop\nvertex 0 0 0\nvertex .3 0 0\nvertex 0 .2 .1\nendloop\nendfacet\nendsolid");
   Check(await App.ImportModel(ascii),"Hatalı modelden sonra Türkçe yollu STL yüklenebiliyor");
   string obj=Path.Combine(root,"Yeni parça.obj");File.WriteAllText(obj,"v 0 0 0\nv .3 0 0\nv .3 .2 0\nv 0 .2 0\nf -4 -3 -2 -1 # comment");
   Check(await App.ImportModel(obj),"OBJ dosyası mevcut robota ekleniyor");
   Check(App.Robot==originalRoot&&App.Field.Root.GetInstanceID()==field,"Model eklerken robot ve saha yeniden yüklenmiyor");
   var originalProject=App.Project;var bad=ProjectIO.Parse(ProjectIO.Write(originalProject));bad.assets.Values.First().data="not-base64";
   Check(!await App.LoadProject(bad),"Bozuk model içeren proje güvenli şekilde reddediliyor");
   Check(ReferenceEquals(App.Project,originalProject)&&App.Robot==originalRoot&&App.Field.Root.GetInstanceID()==field,"Bozuk projeden sonra çalışma ve saha korunuyor");
   App.Save(Path.Combine(root,"models-roundtrip.json"));var restored=ProjectIO.Parse(File.ReadAllText(Path.Combine(root,"models-roundtrip.json")));
   Check(restored.assets.Count==assets+2&&restored.parts.Count==count+2,"GLB STL OBJ içeren robot projesi kayıt açma doğrulaması");
   Check(await App.ImportModel(Path.Combine(Application.streamingAssetsPath,"examples/No-Default-Scene.glb")),"Ana sahne işareti bulunmayan geçerli GLB model de yükleniyor");
   bool rejected=false;try{ModelLoader.ReadObj("v NaN 0 0\nv 0 0 0\nv 1 1 1\nf 1 2 3");}catch{rejected=true;}Check(rejected,"Sonlu olmayan OBJ koordinatı reddediliyor");
  }
  async Task BridgeRegression()
  {
   using(var bridge=new StudioBridge(0))
   using(var client=new System.Net.WebSockets.ClientWebSocket())
   {
    var cancel=System.Threading.CancellationToken.None;
    await client.ConnectAsync(new Uri("ws://127.0.0.1:"+bridge.Port),cancel);
    async Task Send(object value){var bytes=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value));await client.SendAsync(new ArraySegment<byte>(bytes),System.Net.WebSockets.WebSocketMessageType.Text,true,cancel);}
    await Send(new{type="pair",token=bridge.Token});var response=new byte[256];await client.ReceiveAsync(new ArraySegment<byte>(response),cancel);
    await Send(new{type="state",project=new{name="RetainedProject"},marker="first"});
    await Send(new{type="state",marker="latest"});await Task.Delay(150);
    var packet=bridge.Take();Check((string)packet?["project"]?["name"]=="RetainedProject"&&(string)packet?["marker"]=="latest","Blockly proje paketi hızlı durum mesajları arasında kaybolmuyor");
   }
  }
 }
}





