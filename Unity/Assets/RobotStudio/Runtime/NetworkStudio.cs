using System;
using System.Linq;
using System.Globalization;
using UnityEngine;

namespace AshinaStudio {
 public partial class StudioApp {
  long resetSequence;
  internal void RequestRobotReset(){if(RoomActive){Notice="Ortak saha oda sahibinin lobiye dönüşüyle sıfırlanır.";return;}if(Nt?.Connected==true&&Project.network.driveFromNt){_ = Nt.Publish("/AshinaSim/Robot/resetPose",Project.initialPose);_ = Nt.Publish("/AshinaSim/Robot/resetSequence",++resetSequence,"int");Notice="WPILib robotuna başlangıç konumu isteği gönderildi.";}else ResetPose();}
  bool networkPanel;string topicFilter="",selectedTopic="",selectedField="",resolvedPoseTopic;
  bool showSystemTopics;Vector2 topicScroll,topicDetailScroll;string[] visibleTopics=new string[0];
  float nextTopicRefresh,nextPoseSearch;string poseSearchRequest;
  public string EffectivePoseTopic=>resolvedPoseTopic??"";
  public bool NetworkPoseOwned=>!RoomActive&&Nt!=null&&Project.network.driveFromNt&&Project.network.mode=="observe";
  void CycleControlSource(){if(Nt!=null){SetRunning(false);DisconnectNetwork();Notice="Bağımsız sürüş · klavye / kumanda hazır.";}else{Project.network.driveFromNt=true;Project.network.forwardGamepad=true;ConnectNetwork();networkPanel=true;Notice="WPILib kaynağı seçildi. Robot kodunu başlat veya açık sunucuya bağlan.";}}
  public void ConnectNetwork(){Nt?.Dispose();Nt=new Nt4Client();resolvedPoseTopic=null;nextPoseSearch=0;_ = Nt.Connect(Project.network.host,Project.network.port);}
  public void DisconnectNetwork(){ClearLiveMotion();Nt?.Dispose();Nt=null;resolvedPoseTopic=null;visibleTopics=new string[0];nextTopicRefresh=0;}
  string PoseSource() {
   var settings=Project.network;if(!settings.autoPose)return resolvedPoseTopic=settings.poseTopic;
   if(Time.unscaledTime>=nextPoseSearch||poseSearchRequest!=settings.poseTopic){nextPoseSearch=Time.unscaledTime+.5f;poseSearchRequest=settings.poseTopic;resolvedPoseTopic=NtData.FindPose(Nt,settings.poseTopic);}
   return resolvedPoseTopic;
  }
  bool ReadNetworkDrive(ref Vector3 desired,ref float desiredOmega) {
   if(Nt==null||!Project.network.driveFromNt)return false;
   // A disconnected or not-yet-mapped WPILib source holds the robot instead of taking keyboard input.
   if(Project.network.mode=="observe") {
    velocity=Vector3.zero;omega=0;
    string topic=PoseSource();
    var poseValue=Nt.Get(topic);
    if(Nt.Connected&&NtData.Pose(poseValue,topic??"",Project.network.poseFormat,out var pose)) {
     var position=Frc.Position(new[]{pose[0],pose[1],0});position.y=Field.GroundHeight(position);heading=(float)pose[2];ObserveMotion(position,heading,poseValue.Received);hull.ObservePose(position,heading);
    }
   } else if(Nt.Connected) {
    var input=Nt.Get(Project.network.requestTopic);
    if(input!=null&&DateTime.UtcNow.Ticks-input.Received<TimeSpan.TicksPerMillisecond*300&&NtData.Speeds(input,out var speeds)) {
     desired=Robot.rotation*Frc.Position(new[]{Clamp(speeds[0],15),Clamp(speeds[1],15),0});desiredOmega=(float)Clamp(speeds[2],15);
    }
   }
   return true;
  }
  bool ActionNumber(RobotAction action,out double value) {
   value=0;return Nt?.Connected==true&&NtData.Scalar(Nt,Nt.Get(action.topic),action.ntField,out value);
  }
  void NetworkInspector() {
   GUILayout.Label("WPILib / NETWORKTABLES",title);
   Text("Sunucu",true);Project.network.host=Edit("host",Project.network.host);
   Project.network.port=(int)Num("port","NT4 portu · WPILib: 5810",Project.network.port,1,65535);
   Project.network.mode=Cycle("Sürüş verisi",Project.network.mode,new[]{"observe","physics"},new[]{"WPILib konumunu izle","Hız isteğini Unity'de sür"});
   Project.network.autoPose=GUILayout.Toggle(Project.network.autoPose," Field2d / Pose2d otomatik bul");
   Text("Konum konusu",true);Project.network.poseTopic=Edit("poseTopic",Project.network.poseTopic);
   Project.network.poseFormat=Cycle("Açı biçimi",Project.network.poseFormat,new[]{"auto","radians","degrees","field2d","botpose"},new[]{"Otomatik","Radyan","Derece","Field2d · derece","Limelight botpose"});
   Text("Hız isteği konusu",true);Project.network.requestTopic=Edit("requestTopic",Project.network.requestTopic);
   Project.network.vision=GUILayout.Toggle(Project.network.vision," Kamera sonuçlarını NT'ye yayınla");
   bool editable=GUI.enabled;GUI.enabled=!Busy&&!FileDialogs.IsOpen;
   if(Button(Nt==null?"WPILib simülasyonuna bağlan":"Bağlantıyı kes")){if(Nt==null)ConnectNetwork();else DisconnectNetwork();}
   if(Button("Tüm NetworkTables verilerini aç")){networkPanel=true;nextTopicRefresh=0;}
   Text("NT: "+(Nt?.Status??"Bağımsız sürüş"),true);
   if(!string.IsNullOrEmpty(EffectivePoseTopic))Text("Konum: "+EffectivePoseTopic,true);
   Text("Blockly: "+(Bridge?.Fresh==true?"Canlı bağlantı":"Blockly'yi açarak bağlan"),true);
   GUI.enabled=editable;
  }
  void DrawNetworkBrowser() {
   if(!networkPanel)return;
   GUI.enabled=!Busy&&!FileDialogs.IsOpen;
   GUI.Box(new Rect(10,70,Screen.width-20,Screen.height-110),"",panel);
   GUILayout.BeginArea(new Rect(25,83,Screen.width-50,Screen.height-139));
   GUILayout.BeginHorizontal();GUILayout.Label("WPILib  /  NETWORKTABLES",title);GUILayout.FlexibleSpace();if(GUILayout.Button("Kapat",button,GUILayout.Width(85))){networkPanel=false;bindingKind=null;GUI.FocusControl(null);}GUILayout.EndHorizontal();
   GUILayout.BeginHorizontal();Text("Sunucu");Project.network.host=GUILayout.TextField(Project.network.host,input,GUILayout.Width(180));Text("Port");
   string port=GUILayout.TextField(Project.network.port.ToString(),input,GUILayout.Width(65));if(int.TryParse(port,out int number)&&number>0&&number<=65535)Project.network.port=number;
   if(GUILayout.Button(Nt==null?"Bağlan":"Bağlantıyı kes",button,GUILayout.Width(140))){if(Nt==null)ConnectNetwork();else DisconnectNetwork();}
   GUILayout.FlexibleSpace();GUILayout.EndHorizontal();
   Text((Nt?.Status??"Bağımsız kullanım · WPILib simülasyonunu başlatıp Bağlan'a bas.")+(Nt?.Connected==true?$" · {Nt.Topics.Count} konu · {Nt.RoundTripMs:0.0} ms":""));
   if(Nt!=null){Project.network.driveFromNt=GUILayout.Toggle(Project.network.driveFromNt," Robotu WPILib verisi sürsün · kapatılırsa klavye / Blockly ile sürülür");Text(Nt.Connected?"Yayınlanan değerler canlı okunuyor. Sabit kalan değerler de geçerlidir.":"Sunucu bekleniyor; bağlantı otomatik yeniden denenecek.",true);}
   Project.network.forwardGamepad=GUILayout.Toggle(Project.network.forwardGamepad," Kumandaları Xbox düzeniyle WPILib simülasyonuna aktar");Project.network.keyboardGamepad=GUILayout.Toggle(Project.network.keyboardGamepad," WASD / Q E klavyesini de robot koduna aktar");Text("Kumanda: "+DriverInput.Read(0).Name,true);Text(Nt?.Get("/AshinaRobot/Bridge/ready")?.Value is bool ready&&ready?"Robot kumanda köprüsü: hazır":"Kumanda aktarımı için robot kodunda AshinaSimLink çalışmalı.",true);
   RobotCodeControls();GUILayout.Space(6);GUILayout.BeginHorizontal();
   GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(330,(Screen.width-80)*.53f)));
   string filter=Edit("ntSearch",topicFilter);if(filter!=topicFilter){topicFilter=filter;nextTopicRefresh=0;}
   bool system=GUILayout.Toggle(showSystemTopics," Şema ve sistem konularını göster");if(system!=showSystemTopics){showSystemTopics=system;nextTopicRefresh=0;}
   if(Time.unscaledTime>=nextTopicRefresh){nextTopicRefresh=Time.unscaledTime+.25f;visibleTopics=Nt==null?new string[0]:Nt.Topics.Keys.Where(x=>(showSystemTopics||!x.StartsWith("$")&&!x.StartsWith("/.schema/"))&&(string.IsNullOrEmpty(topicFilter)||x.IndexOf(topicFilter,StringComparison.OrdinalIgnoreCase)>=0)).OrderBy(x=>x,StringComparer.Ordinal).ToArray();}
   Text(visibleTopics.Length+" konu · ada tıklayıp veriyi bağla",true);
   topicScroll=GUILayout.BeginScrollView(topicScroll);
   // Only draw visible rows; thousands of YAGSL / vendor topics do not create thousands of GUI controls.
   const float rowHeight=53;float viewport=Math.Max(100,Screen.height-365);int first=Math.Max(0,(int)(topicScroll.y/rowHeight)-1),last=Math.Min(visibleTopics.Length,first+(int)(viewport/rowHeight)+4);
   GUILayout.Space(first*rowHeight);
   for(int i=first;i<last;i++){string name=visibleTopics[i];GUILayout.BeginVertical(GUILayout.Height(rowHeight));if(GUILayout.Button(name,selectedTopic==name?active:button,GUILayout.Height(27))){selectedTopic=name;selectedField="";}Text((Nt.Topics.TryGetValue(name,out var topic)?topic.Type:"")+"  "+NtData.Display(null,Nt.Get(name),72),true);GUILayout.EndVertical();}
   GUILayout.Space((visibleTopics.Length-last)*rowHeight);GUILayout.EndScrollView();GUILayout.EndVertical();
   GUILayout.Space(15);GUILayout.BeginVertical();topicDetailScroll=GUILayout.BeginScrollView(topicDetailScroll);
   Text(string.IsNullOrEmpty(selectedTopic)?"Soldan bir veri seç.":selectedTopic);
   if(Nt!=null&&!string.IsNullOrEmpty(selectedTopic)) {
    var value=Nt.Get(selectedTopic);Text(Nt.Topics.TryGetValue(selectedTopic,out var topic)?"Tür: "+topic.Type:"Konu artık yayınlanmıyor.",true);
    Text(NtData.Display(Nt,value));if(value!=null)Text("Son değişim: "+TimeSpan.FromTicks(Math.Max(0,DateTime.UtcNow.Ticks-value.Received)).TotalSeconds.ToString("0.0",CultureInfo.InvariantCulture)+" s önce",true);
    GUILayout.Space(10);
    if(NtData.Pose(value,selectedTopic,"auto",out _))if(Button("Bu veriyi robot konumu yap")){Project.network.poseTopic=selectedTopic;Project.network.poseFormat="auto";Project.network.autoPose=false;Project.network.mode="observe";Project.network.driveFromNt=true;resolvedPoseTopic=selectedTopic;Notice="Konum bağlandı. Sürüşe başla ile sahada izle.";}
    if(NtData.Speeds(value,out _))if(Button("Bu veriyi hız isteği yap")){Project.network.requestTopic=selectedTopic;Project.network.mode="physics";Project.network.driveFromNt=true;Notice="Robot yönüne göre [vx, vy, omega] hız isteği bağlandı.";}
    Text("Dizi / struct alanı · örnek 0 veya translation.x",true);selectedField=Edit("ntFieldPicker",selectedField);
    if(NtData.Scalar(Nt,value,selectedField,out var scalar)) {
     Text("Seçili sayısal değer: "+scalar.ToString("0.#####",CultureInfo.InvariantCulture));if(bindingKind!=null&&Button("Bu veriyi seçtiğim parametreye bağla"))PickBinding(selectedTopic,selectedField);
     var action=Project.actions.Find(x=>x.id==chosenAction);
     if(action!=null){if(Button("Seçili eyleme bağla: "+action.name)){action.topic=selectedTopic;action.ntSource=true;action.ntField=selectedField;Notice="NetworkTables verisi eyleme bağlandı.";}}
     else Text("Eylemlerden bir hareket seç; sonra buradan veri bağlayabilirsin.",true);
    }
    Text("Yalnız yayınlanan veriler alınabilir. Motorun iç değeri için robot kodunun onu NT'ye yayınlaması gerekir.",true);
   }
   GUILayout.EndScrollView();GUILayout.EndVertical();GUILayout.EndHorizontal();GUILayout.EndArea();GUI.enabled=true;
  }
 }
}
