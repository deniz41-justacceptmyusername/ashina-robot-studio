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
  public async Task<bool> HostRoom(int port=MatchProtocol.DefaultPort,string name=null,string alliance="blue",bool localOnly=false,bool spectator=false){
   if(RoomActive||Busy||Project==null)return false;
   try{PrepareRoom();Room=await MatchRoom.Host(this,port,PlayerName(name),alliance,localOnly,spectator);if(this==null){Room.Dispose();return false;}if(spectator)foreach(var r in Robot.GetComponentsInChildren<Renderer>())r.enabled=false;roomConnecting=false;roomPanel=false;Notice="Oda açıldı · davet kodunu arkadaşlarınla paylaş.";return true;}
   catch(Exception e){roomError="Oda açılamadı: "+e.Message;roomConnecting=false;await LeaveRoom();return false;}
  }
  public async Task<bool> JoinRoom(string invitation,string name=null,string alliance="blue"){
   if(RoomActive||Busy||Project==null)return false;
   try{string code=invitation.Trim();if(code.StartsWith("ashina://",StringComparison.OrdinalIgnoreCase))code=code.Substring(9);int slash=code.LastIndexOf('/');if(slash<1)throw new Exception("Adres ve oda anahtarını birlikte yapıştır: adres:port/anahtar");string endpoint=code.Substring(0,slash),key=code.Substring(slash+1);var uri=new Uri("tcp://"+endpoint);if(uri.Port<1||key.Length!=32||key.Any(c=>!Uri.IsHexDigit(c)))throw new Exception("Davet kodu geçersiz.");
    PrepareRoom();Room=await MatchRoom.Join(this,uri.Host,uri.Port,key,PlayerName(name),alliance);roomConnecting=false;roomPanel=false;return true;
   }catch(Exception e){roomError="Odaya girilemedi: "+e.Message;roomConnecting=false;await LeaveRoom();return false;}
  }
  string PlayerName(string name){var result=(name??roomName).Trim();if(string.IsNullOrEmpty(result))result="Takım "+Project.team;if(result.Length>40)result=result.Substring(0,40);return result;}
  void PrepareRoom(){
   RecordNode();ProjectIO.Validate(Project);offlineProject=ProjectIO.Parse(ProjectIO.Write(Project));SetRunning(false);PublishDriverStation();EnterWorkspace();networkPanel=quickSetup=help=false;roomEmergency=false;roomNeedReset=true;roomError="";roomConnecting=true;Project.network.mode="physics";ClearLiveMotion();gizmo.Select(null,false);SetRoomCollider(false);
  }
  void SetRoomCollider(bool enabled){if(hull!=null){hull.enabled=false;foreach(var c in Robot.GetComponents<Collider>())c.enabled=enabled;}if(Robot!=null)foreach(var c in Robot.GetComponentsInChildren<Collider>())if(c.gameObject.layer==9)c.enabled=false;}
  public async Task LeaveRoom(){
   if(Nt?.Connected==true)_ = Nt.Publish("/AshinaSim/Online/active",false,"boolean");var old=Room;Room=null;roomConnecting=false;roomControlled=true;try{SetRunning(false);}finally{roomControlled=false;}PublishDriverStation();old?.Dispose();Pieces.Clear();roomPanel=false;roomEmergency=false;var restore=offlineProject;offlineProject=null;
   if(restore!=null&&this!=null){while(Busy)await Task.Delay(20);await LoadProject(restore);EnterWorkspace();}if(!string.IsNullOrEmpty(roomError))Notice=roomError;else Notice="Odadan ayrıldın · robot projen korundu.";
  }
  internal void ResetRoomWorld(){Pieces.Clear();ResetTraining();}
  internal void RoomTrainingTick()=>TrainingTick();
  internal void ApplyRoomScore(int blue,int red){BlueFuel=blue;RedFuel=red;}
  internal void RoomEpochChanged(){roomNeedReset=true;visionQueues.Clear();visionResults.Clear();lastMuzzleLocal.Clear();muzzleRelativeVelocity.Clear();}
  internal void ApplyRoomRobot(MatchRobotState state){
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
    if(!external&&RobotMode!="autonomous"&&!Typing&&Application.isFocused&&!networkPanel&&!roomPanel){var pad=DriverInput.Read(0);float x=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0)-(float)pad.Axes[1],y=(Input.GetKey(KeyCode.A)?1:0)-(Input.GetKey(KeyCode.D)?1:0)-(float)pad.Axes[0],r=(Input.GetKey(KeyCode.Q)?1:0)-(Input.GetKey(KeyCode.E)?1:0)-(float)pad.Axes[4];if(Project.drive.type=="differential")y=0;var flat=Vector2.ClampMagnitude(new Vector2(x,y),1);desired=Frc.Position(new[]{flat.x*Project.drive.maxSpeed,flat.y*Project.drive.maxSpeed,0});if(!Project.drive.fieldRelative)desired=Robot.rotation*desired;turn=Mathf.Clamp(r,-1,1)*(float)Project.drive.turnRate;}
    if(CheckVelocity.HasValue){desired=CheckVelocity.Value;turn=CheckOmega;}
   }
   return new MatchInput{sequence=sequence,echo=MatchLink.Clock,drive=new[]{desired.z,-desired.x,turn},parts=Project.parts.Select(p=>new MatchPartState{id=p.id,p=MatchProtocol.V(Nodes[p.id].transform.localPosition),q=MatchProtocol.Q(Nodes[p.id].transform.localRotation)}).ToArray(),actions=Project.actions.Select(a=>{
    double pitch=a.pitch,yaw=a.yaw,speed=a.speed;bool code=false;if(LiveNumber(a.pitchTopic,a.pitchField,out var pv)){pitch=ShotPlanning.Angle(pv,a.pitchUnit);code=true;}if(LiveNumber(a.yawTopic,a.yawField,out var yv)){yaw=ShotPlanning.Angle(yv,a.yawUnit);code=true;}if(LiveNumber(a.speedTopic,a.speedField,out var sv)){speed=sv*a.speedScale+a.speedOffset;code=true;}
    return new MatchActionInput{id=a.id,down=allowed&&(RobotMode!="autonomous"||Nt!=null||Bridge?.Fresh==true||CheckInput.HasValue)&&ActionDown(a),pitch=(float)pitch,yaw=(float)yaw,speed=Mathf.Clamp((float)speed,0,(float)a.maxShotSpeed),code=code};}).ToArray()};
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
   if(follow)cameraTarget=Vector3.Lerp(cameraTarget,Robot.position+Vector3.up*.5f,1-Mathf.Exp(-Time.unscaledDeltaTime*8));var rotation=Quaternion.Euler(orbitPitch,orbitYaw,0);ViewCamera.transform.position=cameraTarget-rotation*Vector3.forward*orbitDistance;ViewCamera.transform.LookAt(cameraTarget);
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
    Section("ODA OLUŞTUR");roomPort=(int)Num("roomPort","Bağlantı portu",roomPort,1024,65535);if(Button("Bu bilgisayarda oda oluştur"))_ = HostRoom(roomPort,null,roomAlliance);
    Section("ODAYA KATIL");roomAddress=Edit("roomAddress",roomAddress);if(Button("Davet koduyla katıl"))_ = JoinRoom(roomAddress,null,roomAlliance);GUI.enabled=true;
    Text("Aynı ağda davet kodu yeterlidir. İnternet için oda sahibinin adresi erişilebilir olmalı; özel ağ / VPN adresi veya yönlendirilmiş sunucu adresi kullanılabilir.",true);if(!string.IsNullOrEmpty(roomError))Text(roomError);
   }else{DrawRoomLobby();}
   GUILayout.EndScrollView();GUILayout.EndArea();
  }
  void DrawRoomLobby(){
   if(Room==null)return;Text(Room.Status);if(!string.IsNullOrEmpty(Room.Failure))Text(Room.Failure);if(Room.Hosting){Text("DAVET KODU",true);GUILayout.TextField(RoomInvitation,input);if(Button("Davet kodunu kopyala"))GUIUtility.systemCopyBuffer=RoomInvitation;Text("İnternetten bağlanacak oyuncu, davetteki yerel IP yerine senin erişilebilir IP / VPN adresini kullanır.",true);}
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
