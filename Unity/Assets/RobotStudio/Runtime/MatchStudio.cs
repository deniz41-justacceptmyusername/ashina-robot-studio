using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

namespace AshinaStudio {
 public partial class StudioApp {
  public MatchRoom Room {get;private set;}
  public bool RoomActive=>Room!=null||roomConnecting;
  bool roomConnecting,roomPanel,roomControlled,roomEmergency,roomNeedReset;RobotProject offlineProject;string roomName="",roomAlliance="blue",roomAddress="",roomError="";int roomPort=MatchProtocol.DefaultPort;Vector2 roomScroll;float roomPublishTick,roomVisionTick;
  bool internetRoom,internetPreparing,showLanRoom;
  string ownMatchAlliance;
  float offlineOrbitYaw;
  bool offlineFollow;
  internal string CloudRoomExecutable=>Path.GetFullPath(Path.Combine(Application.dataPath,"../tools/cloudflared.exe"));
  async Task HostInternetRoom(){
   if(await HostRoom(0,null,roomAlliance,true)){internetRoom=true;roomPanel=true;await PrepareInternetInvitation();}
  }
  async Task PrepareInternetInvitation(){
   var room=Room;if(room?.Hosting!=true||internetPreparing)return;
   internetPreparing=true;roomError="";
   try{await room.EnableInternet(CloudRoomExecutable);if(Room==room)Notice="İnternet daveti hazır.";}
   catch(Exception e){if(Room==room)roomError=e.Message;}
   finally{internetPreparing=false;}
  }
  public async Task<bool> HostRoom(int port=MatchProtocol.DefaultPort,string name=null,string alliance="blue",bool localOnly=false,bool spectator=false){
   if(RoomActive||Busy||Project==null)return false;
   try{PrepareRoom();Room=await MatchRoom.Host(this,port,PlayerName(name),alliance,localOnly,spectator);if(this==null){Room.Dispose();return false;}if(spectator)foreach(var r in Robot.GetComponentsInChildren<Renderer>())r.enabled=false;roomConnecting=false;roomPanel=false;Notice="Oda açıldı · davet kodunu arkadaşlarınla paylaş.";return true;}
   catch(Exception e){roomError="Oda açılamadı: "+e.Message;roomConnecting=false;await LeaveRoom();return false;}
  }
  public async Task<bool> JoinRoom(string invitation,string name=null,string alliance="blue"){
   if(RoomActive||Busy||Project==null)return false;
   try{
    string code=invitation.Trim();Uri internet=null;Uri endpoint=null;string key;
    if(code.StartsWith("https://",StringComparison.OrdinalIgnoreCase)){
     var invitationUri=new Uri(code);var path=invitationUri.AbsolutePath.Split('/');
     if(!invitationUri.Host.EndsWith(".trycloudflare.com",StringComparison.OrdinalIgnoreCase)||!invitationUri.IsDefaultPort||invitationUri.UserInfo.Length!=0||invitationUri.Query.Length!=0||invitationUri.Fragment.Length!=0||path.Length!=3||path[1]!="room")throw new IOException("İnternet daveti geçersiz. Arkadaşının uygulamadan kopyaladığı linkin tamamını yapıştır.");
     key=path[2];internet=new UriBuilder(invitationUri){Scheme="wss",Port=-1}.Uri;
    }else{
     if(code.StartsWith("ashina://",StringComparison.OrdinalIgnoreCase))code=code.Substring(9);
     int slash=code.LastIndexOf('/');if(slash<1)throw new IOException("Arkadaşının uygulamadan kopyaladığı daveti yapıştır.");
     endpoint=new Uri("tcp://"+code.Substring(0,slash));key=code.Substring(slash+1);if(endpoint.Port<1)throw new IOException("Yerel davet adresinde port eksik.");
    }
    if(key.Length!=32||key.Any(c=>!Uri.IsHexDigit(c)))throw new IOException("Davet anahtarı geçersiz.");
    PrepareRoom();Room=await MatchRoom.Join(this,endpoint?.Host,endpoint?.Port??0,key,PlayerName(name),alliance,internet);roomConnecting=false;roomPanel=false;return true;
   }catch(Exception e){roomError="Odaya girilemedi: "+e.Message;roomConnecting=false;await LeaveRoom();return false;}
  }
  string PlayerName(string name){var result=(name??roomName).Trim();if(string.IsNullOrEmpty(result))result="Takım "+Project.team;if(result.Length>40)result=result.Substring(0,40);return result;}
  void PrepareRoom(){
   ownMatchAlliance=null;offlineOrbitYaw=orbitYaw;offlineFollow=follow;
   RecordNode();ProjectIO.Validate(Project);offlineProject=ProjectIO.Parse(ProjectIO.Write(Project));SetRunning(false);PublishDriverStation();EnterWorkspace();networkPanel=quickSetup=help=false;roomEmergency=false;roomNeedReset=true;roomError="";internetRoom=false;roomConnecting=true;Project.network.mode="physics";ClearLiveMotion();gizmo.Select(null,false);SetRoomCollider(false);
  }
  void SetRoomCollider(bool enabled){if(hull!=null){hull.enabled=false;foreach(var c in Robot.GetComponents<Collider>())c.enabled=enabled;}if(Robot!=null)foreach(var c in Robot.GetComponentsInChildren<Collider>())if(c.gameObject.layer==9)c.enabled=false;}
  public async Task LeaveRoom(){
   if(Nt?.Connected==true)_ = Nt.Publish("/AshinaSim/Online/active",false,"boolean");var old=Room;Room=null;roomConnecting=false;roomControlled=true;try{SetRunning(false);}finally{roomControlled=false;}PublishDriverStation();old?.Dispose();Pieces.Clear();roomPanel=false;roomEmergency=false;var restore=offlineProject;offlineProject=null;
   if(restore!=null&&this!=null){while(Busy)await Task.Delay(20);await LoadProject(restore);orbitYaw=offlineOrbitYaw;follow=offlineFollow;EnterWorkspace();}if(!string.IsNullOrEmpty(roomError))Notice=roomError;else Notice="Odadan ayrıldın · robot projen korundu.";
  }
  internal void ResetRoomWorld(){Pieces.Clear();ResetTraining();}
  internal void RoomTrainingTick()=>TrainingTick();
  internal void ApplyRoomScore(int blue,int red){BlueFuel=blue;RedFuel=red;}
  internal void RoomEpochChanged(){roomNeedReset=true;visionQueues.Clear();visionResults.Clear();lastMuzzleLocal.Clear();muzzleRelativeVelocity.Clear();}
  internal void ApplyRoomRobot(MatchRobotState state){
   if(ownMatchAlliance==null){ownMatchAlliance=state.alliance;orbitYaw=ownMatchAlliance=="red"?180:0;follow=true;}
   hull.Teleport(MatchProtocol.Vector(state.p),MatchProtocol.Rotation(state.q));heading=Mathf.Atan2(-Robot.forward.x,Robot.forward.z);velocity=MatchProtocol.Vector(state.v);omega=-state.w[1];observedVelocity=velocity;observedOmega=omega;observedAt=DateTime.UtcNow.Ticks;
   Project.inventory=new System.Collections.Generic.Dictionary<string,int>(state.stock);ShotsFired=state.shots;FuelCollected=state.pickups;
   if(roomNeedReset&&Nt?.Connected==true){_ = Nt.Publish("/AshinaSim/Robot/resetPose",Pose);_ = Nt.Publish("/AshinaSim/Robot/resetSequence",++resetSequence,"int");roomNeedReset=false;}
  }
  internal void ApplyRoomEnabled(bool enabled,string phase){
   enabled=enabled&&!roomEmergency;RobotMode=phase=="autonomous"||phase=="countdown"?"autonomous":"teleop";
   if(Running!=enabled){roomControlled=true;try{SetRunning(enabled);}finally{roomControlled=false;}SetRoomCollider(false);PublishDriverStation();}
  }
  internal MatchInput CaptureRoomInput(long sequence){
   ApplyActions(false);Vector3 desired=Vector3.zero;float turn=0;bool allowed=Running&&!roomEmergency;
   if(allowed){bool external=false;if(Nt!=null&&Project.network.driveFromNt){external=true;if(Project.network.mode=="physics")ReadNetworkDrive(ref desired,ref turn);}
    if(!external&&Bridge?.Fresh==true&&blockOutput!=null){var v=blockOutput["velocity"];if(v!=null){desired=Frc.Position(new[]{(double?)v["x"]??0,(double?)v["y"]??0,0});turn=(float?)v["omega"]??0;external=true;}}
    if(!external&&RobotMode!="autonomous"&&!Typing&&Application.isFocused&&!networkPanel&&!roomPanel){
     var pad=DriverInput.Read(0);
     float x=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0)-(float)pad.Axes[1];
     float y=(Input.GetKey(KeyCode.A)?1:0)-(Input.GetKey(KeyCode.D)?1:0)-(float)pad.Axes[0];
     float rotation=(Input.GetKey(KeyCode.Q)?1:0)-(Input.GetKey(KeyCode.E)?1:0)-(float)pad.Axes[4];
     desired=RoomManualDrive(new Vector2(x,y),Project.drive,Robot.rotation,ownMatchAlliance);
     turn=Mathf.Clamp(rotation,-1,1)*(float)Project.drive.turnRate;
    }
    if(CheckVelocity.HasValue){desired=CheckVelocity.Value;turn=CheckOmega;}
   }
   return new MatchInput{sequence=sequence,echo=MatchLink.Clock,drive=new[]{desired.z,-desired.x,turn},parts=Project.parts.Select(p=>new MatchPartState{id=p.id,p=MatchProtocol.V(Nodes[p.id].transform.localPosition),q=MatchProtocol.Q(Nodes[p.id].transform.localRotation)}).ToArray(),actions=Project.actions.Select(a=>{
    double pitch=a.pitch,yaw=a.yaw,speed=a.speed;bool code=false;if(LiveNumber(a.pitchTopic,a.pitchField,out var pv)){pitch=ShotPlanning.Angle(pv,a.pitchUnit);code=true;}if(LiveNumber(a.yawTopic,a.yawField,out var yv)){yaw=ShotPlanning.Angle(yv,a.yawUnit);code=true;}if(LiveNumber(a.speedTopic,a.speedField,out var sv)){speed=sv*a.speedScale+a.speedOffset;code=true;}
    return new MatchActionInput{id=a.id,down=allowed&&(RobotMode!="autonomous"||Nt!=null||Bridge?.Fresh==true||CheckInput.HasValue)&&ActionDown(a),pitch=(float)pitch,yaw=(float)yaw,speed=Mathf.Clamp((float)speed,0,(float)a.maxShotSpeed),code=code};}).ToArray()};
  }
  internal static Vector3 RoomManualDrive(Vector2 axes,DriveData drive,Quaternion robotRotation,string alliance){
   if(drive.type=="differential")axes.y=0;
   var limited=Vector2.ClampMagnitude(axes,1)*(float)drive.maxSpeed;
   var desired=new Vector3(-limited.y,0,limited.x);
   if(!drive.fieldRelative)return robotRotation*desired;
   return alliance=="red"?-desired:desired;
  }
  void RoomFixedUpdate(){
   if(Room==null)return;Room.FixedTick();roomPublishTick+=Time.fixedDeltaTime;roomVisionTick+=Time.fixedDeltaTime;
   if(roomPublishTick>=.02f){roomPublishTick=0;PublishDriverStation();PublishGameState();Bridge?.Send(BlockInput());if(Nt?.Connected==true){_ = Nt.Publish("/AshinaSim/Online/active",Room.Connected,"boolean");_ = Nt.Publish("/AshinaSim/Online/phase",Room.Phase,"string");_ = Nt.Publish("/AshinaSim/Online/matchTime",Room.Remaining,"double");_ = Nt.Publish("/AshinaSim/Online/epoch",Room.Epoch,"int");_ = Nt.Publish("/AshinaSim/Robot/pose",Pose);_ = Nt.Publish("/AshinaSim/Robot/velocity",new[]{(double)velocity.z,(double)-velocity.x,(double)omega});if(Room.LocalId!=null&&Room.Robots.TryGetValue(Room.LocalId,out var me)){_ = Nt.Publish("/AshinaSim/DriverStation/alliance",me.Alliance,"string");_ = Nt.Publish("/AshinaSim/DriverStation/station",(long)(me.Slot%3+1),"int");}}}
   if(roomVisionTick>=.05f){roomVisionTick=0;UpdateVision();}
  }
  void RoomUpdate(){
   leftWidth=250;rightWidth=0;ViewCamera.enabled=true;ViewCamera.rect=new Rect(leftWidth/Screen.width,35f/Screen.height,1-leftWidth/Screen.width,(Screen.height-100f)/Screen.height);Room?.Update();DedicatedRoomTick();
   bool viewport=!roomPanel&&!networkPanel&&Input.mousePosition.x>leftWidth&&Input.mousePosition.y>35&&Input.mousePosition.y<Screen.height-65;
   if(viewport&&Input.GetMouseButtonDown(0))GUI.FocusControl(null);if(Input.GetKeyDown(KeyCode.F)&&!Typing){follow=true;orbitDistance=6;}
   if(Input.GetKeyDown(KeyCode.Escape)&&!Typing){roomEmergency=true;ApplyRoomEnabled(false,"disabled");Notice="Senin robotun durduruldu. Devam etmek için sol paneli kullan.";}
   if(viewport){if(Input.GetMouseButton(1)){orbitYaw+=Input.GetAxisRaw("Mouse X")*3;orbitPitch=Mathf.Clamp(orbitPitch-Input.GetAxisRaw("Mouse Y")*3,8,85);}orbitDistance=Mathf.Clamp(orbitDistance-Input.mouseScrollDelta.y*.45f,.6f,35);if(Input.GetMouseButton(2)){follow=false;cameraTarget+=(-ViewCamera.transform.right*Input.GetAxisRaw("Mouse X")-ViewCamera.transform.up*Input.GetAxisRaw("Mouse Y"))*.025f*orbitDistance;}}
   if(follow)cameraTarget=Vector3.Lerp(cameraTarget,Robot.position+Vector3.up*.5f,1-Mathf.Exp(-Time.unscaledDeltaTime*8));var rotation=Quaternion.Euler(orbitPitch,orbitYaw,0);ViewCamera.transform.position=cameraTarget-rotation*Vector3.forward*orbitDistance;ViewCamera.transform.LookAt(cameraTarget);ViewCamera.cullingMask=~((1<<HomeLayer)|(ViewCamera.transform.position.y>=10.2f?1<<ArenaVenue.RoofLayer:0));
   if(Bridge!=null){var message=Bridge.Take();if(message!=null)HandleBlock(message);}if(actionLine!=null)actionLine.enabled=false;
  }
  static string LocalRoomAddress(){try{
   return System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==System.Net.NetworkInformation.OperationalStatus.Up&&n.NetworkInterfaceType!=System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
    .SelectMany(n=>{var properties=n.GetIPProperties();bool gateway=properties.GatewayAddresses.Any(g=>g.Address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork&&!g.Address.Equals(IPAddress.Any));return properties.UnicastAddresses.Where(a=>a.Address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork&&!IPAddress.IsLoopback(a.Address)&&!a.Address.ToString().StartsWith("169.254.")).Select(a=>new{Address=a.Address.ToString(),Gateway=gateway});})
    .OrderByDescending(a=>a.Gateway).ThenBy(a=>a.Address.StartsWith("192.168.")?0:a.Address.StartsWith("10.")?1:2).Select(a=>a.Address).FirstOrDefault()??"127.0.0.1";
   }catch{return "127.0.0.1";}}

  public string RoomInvitation=>Room?.Hosting==true?(Room.LocalOnly?"127.0.0.1":LocalRoomAddress())+":"+Room.Port+"/"+Room.Token:"";
  void DrawRoomEntry(){
   if(!roomPanel)return;float w=Mathf.Min(680,Screen.width-40),h=Mathf.Min(570,Screen.height-60);GUI.Box(new Rect((Screen.width-w)/2,(Screen.height-h)/2,w,h),"",panel);GUILayout.BeginArea(new Rect((Screen.width-w)/2+24,(Screen.height-h)/2+20,w-48,h-40));
   GUILayout.BeginHorizontal();GUILayout.Label("ÇEVRİMİÇİ MAÇ",title);GUILayout.FlexibleSpace();if(GUILayout.Button("Kapat",button,GUILayout.Width(80))){roomPanel=false;GUI.FocusControl(null);}GUILayout.EndHorizontal();roomScroll=GUILayout.BeginScrollView(roomScroll);
   if(Room==null){Text("Seçili robot: "+Project?.name);Text("Model ve simülasyon ayarları odadaki oyuncularla paylaşılır. Robot kodu ve bilgisayarındaki dosya yolları paylaşılmaz.",true);GUILayout.Space(8);Text("Oyuncu adı",true);roomName=Edit("roomName",roomName);roomAlliance=Cycle("İttifak",roomAlliance,new[]{"blue","red"},new[]{"Mavi","Kırmızı"});GUI.enabled=!roomConnecting&&!Busy;
    Section("ODA OLUŞTUR");if(Button("İnternet odası aç",true))_ = HostInternetRoom();
    showLanRoom=Fold("Aynı Wi-Fi / yerel ağ",showLanRoom);if(showLanRoom){roomPort=(int)Num("roomPort","Bağlantı portu",roomPort,1024,65535);if(Button("Yerel oda aç"))_ = HostRoom(roomPort,null,roomAlliance);}
    Section("ODAYA KATIL");roomAddress=Edit("roomAddress",roomAddress);if(Button("Davet koduyla katıl"))_ = JoinRoom(roomAddress,null,roomAlliance);GUI.enabled=true;
    if(!string.IsNullOrEmpty(roomError))Text(roomError);else Text("Farklı evlerden oynamak için internet odası aç ve arkadaşına daveti gönder.",true);
   }else{DrawRoomLobby();}
   GUILayout.EndScrollView();GUILayout.EndArea();
  }
  void DrawRoomLobby(){
   if(Room==null)return;Text(Room.Status);if(!string.IsNullOrEmpty(Room.Failure))Text(Room.Failure);
   if(Room.Hosting){
    Text(internetRoom?"İNTERNET DAVETİ":"YEREL AĞ DAVETİ",true);
    string invite=internetRoom?Room.InternetInvitation:RoomInvitation;
    if(invite.Length>0){GUILayout.TextField(invite,input);if(Button("Daveti kopyala",true))GUIUtility.systemCopyBuffer=invite;}
    else if(internetPreparing)Text("İnternet daveti hazırlanıyor…");
    else{Text(string.IsNullOrEmpty(roomError)?"İnternet bağlantısı kapandı.":roomError);if(Button("İnternet davetini yeniden oluştur"))_ = PrepareInternetInvitation();}
   }
   Section("OYUNCULAR · "+Room.Robots.Count+" / 6");foreach(var r in Room.Robots.Values.OrderBy(r=>r.Slot))Text((r.Alliance=="blue"?"Mavi ":"Kırmızı ")+(r.Slot%3+1)+" · "+r.Name+(r.Id==Room.LocalId?" · sen":"")+(r.Ready?"  ✓ hazır":"  · bekliyor"));
   Section("SENİN ROBOTUN");if(Button(Nt==null?"Kontrol: klavye / kumanda":Nt.Connected?"Kontrol: WPILib bağlı":"Kontrol: WPILib bekliyor"))CycleControlSource();if(Button("WPILib bağlantısı / otonom seçimi")){roomPanel=false;networkPanel=true;}
   DrawAutoChooser();Text("WPILib robotları hız isteğiyle sürülür. Standart konum izleme, ortak çarpışma fiziği için yeterli değildir.",true);
   GUI.enabled=Room.CanReady&&Room.Phase=="lobby";if(Button(Room.LocalReady?"Hazır durumunu kaldır":"Hazırım",Room.LocalReady))Room.SetReady(!Room.LocalReady);GUI.enabled=true;
   if(Room.Hosting){if(Room.Phase=="lobby"){AutoDurationControls();GUI.enabled=Room.CanStart;if(Button("Herkesi birlikte başlat",true))Room.StartMatch();GUI.enabled=true;}else if(Button("Maçı durdur · lobiye dön"))Room.ReturnLobby();}
  }
  void AutoDurationControls(){Room.AutoSeconds=Num("roomAuto","Otonom · saniye",Room.AutoSeconds,1,300);Room.TeleopSeconds=Num("roomTeleop","Teleop · saniye",Room.TeleopSeconds,1,600);Text("Geri sayım → otonom → 3 saniye ara → teleop. HUB göstergesi antrenman toplamıdır.",true);}
  string[] RoomAutoOptions(string prefix)=>Nt?.Get(prefix+"/options")?.Value is object[] values?values.OfType<string>().ToArray():new string[0];
  public bool SelectRoomAuto(string prefix,string option){if(Nt?.Connected!=true||!RoomAutoOptions(prefix).Contains(option))return false;_ = Nt.Publish(prefix+"/selected",option,"string");return true;}
  void DrawAutoChooser(){
   if(Nt?.Connected!=true)return;var choices=Nt.Topics.Keys.Where(k=>k.EndsWith("/options",StringComparison.Ordinal)&&Nt.Get(k)?.Value is object[]).OrderBy(k=>k).Take(4).ToArray();foreach(var path in choices){var options=RoomAutoOptions(path.Substring(0,path.Length-8));if(options==null||options.Length==0)continue;string prefix=path.Substring(0,path.Length-8);string selected=Nt.Get(prefix+"/selected")?.Value as string??Nt.Get(prefix+"/default")?.Value as string??options[0];int index=Array.IndexOf(options,selected);if(Button("Otonom: "+selected)){string next=options[(index+1+options.Length)%options.Length];SelectRoomAuto(prefix,next);}}
  }
  void DrawMatchWorkspace(){
   GUI.Box(new Rect(0,0,Screen.width,65),"",panel);GUILayout.BeginArea(new Rect(15,12,Screen.width-30,44));GUILayout.BeginHorizontal();GUILayout.Label("ASHINA / ONLINE",title);GUILayout.FlexibleSpace();Text(Room==null?"Oda hazırlanıyor…":PhaseLabel(Room.Phase)+"  "+Room.Remaining.ToString("0.0")+" sn");if(GUILayout.Button("Oda",button,GUILayout.Width(70)))roomPanel=!roomPanel;if(GUILayout.Button("WPILib / NT",button,GUILayout.Width(110)))networkPanel=!networkPanel;GUI.enabled=!roomConnecting;if(GUILayout.Button("Odadan ayrıl",button,GUILayout.Width(120)))_ = LeaveRoom();GUI.enabled=true;GUILayout.EndHorizontal();GUILayout.EndArea();
   GUI.Box(new Rect(0,65,250,Screen.height-100),"",panel);GUILayout.BeginArea(new Rect(14,80,222,Screen.height-130));Text(Project.name);Section("ORTAK SAHA");Text(Project.season=="2026"?$"Mavi HUB {BlueFuel}  ·  Kırmızı HUB {RedFuel}":Project.season+" · ortak antrenman");Text("Stok "+StoredTotal+" / "+Project.training.capacity+" · Atış "+ShotsFired);Text(Room?.Status??"Bağlanıyor…",true);if(Room!=null){Text(Room.Hosting?"Oda sahibi":$"Gecikme {Room.RttMs:0} ms",true);if(!string.IsNullOrEmpty(Room.Failure))Text(Room.Failure);Section("MAÇ");if(Room.Phase=="lobby"){GUI.enabled=Room.CanReady;if(Button(Room.LocalReady?"Hazır ✓":"Hazırım",Room.LocalReady))Room.SetReady(!Room.LocalReady);GUI.enabled=true;if(Room.Hosting){GUI.enabled=Room.CanStart;if(Button("Maçı başlat",true))Room.StartMatch();GUI.enabled=true;}}else if(roomEmergency){if(Button("Kendi robotuma devam et")){roomEmergency=false;ApplyRoomEnabled(Room.Enabled,Room.Phase);}}else if(Button("Kendi robotumu durdur")){roomEmergency=true;ApplyRoomEnabled(false,"disabled");}Section("ROBOTLAR");foreach(var r in Room.Robots.Values.OrderBy(r=>r.Slot))Text((r.Alliance=="blue"?"M":"K")+(r.Slot%3+1)+" · "+r.Name+(r.Ready?" ✓":""),true);DrawAutoChooser();}
   Section("KONTROLLER");Text("WASD · sürüş\nQ / E · dönüş\nF · robotu bul\nEsc · kendi robotunu durdur",true);GUILayout.EndArea();GUI.Box(new Rect(0,Screen.height-35,Screen.width,35),"",panel);GUI.Label(new Rect(12,Screen.height-29,Screen.width-24,24),Notice,muted);DrawRoomEntry();DrawNetworkBrowser();
  }
  static string PhaseLabel(string phase)=>phase=="lobby"?"LOBI":phase=="countdown"?"HAZIRLAN":phase=="autonomous"?"OTONOM":phase=="transition"?"ARA":phase=="teleop"?"TELEOP":phase=="finished"?"MAÇ BİTTİ":"DURDU";
 }
}
