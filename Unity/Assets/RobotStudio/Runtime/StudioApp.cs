using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AshinaStudio {
 public partial class StudioApp : MonoBehaviour {
  public RobotProject Project;public FieldWorld Field;public readonly Dictionary<string,PartNode> Nodes=new Dictionary<string,PartNode>();public GamePieces Pieces=new GamePieces();public Transform Robot;public Camera ViewCamera;public bool Running,Busy;public string Notice="",Selected="chassis";public StudioBridge Bridge;public Nt4Client Nt;
  ModelLoader loader;RuntimeGizmo gizmo;RobotChassis hull;readonly Dictionary<string,float> progress=new Dictionary<string,float>(),lastEmit=new Dictionary<string,float>();readonly Dictionary<string,bool> previous=new Dictionary<string,bool>(),toggled=new Dictionary<string,bool>();readonly Dictionary<string,Queue<VisionFrame>> visionQueues=new Dictionary<string,Queue<VisionFrame>>();readonly Dictionary<string,VisionFrame> visionResults=new Dictionary<string,VisionFrame>();JObject blockOutput;Vector3 velocity;float omega,heading,tick,netTick;bool rotateGizmo,follow=true;float orbitYaw=-145,orbitPitch=48,orbitDistance=6;Vector3 cameraTarget;string savedPath;bool bridgeWasConnected;public string RobotMode="teleop";int uiTab;float leftWidth=235,rightWidth=300;
  public int GizmoDragStarts=>gizmo?.DragStarts??0;
  public double LastShotSpeed {get;private set;}
  public int BlocklyProjectUpdates {get;private set;}
  public double[] Pose=>Robot!=null?new[]{(double)Robot.position.z,(double)-Robot.position.x,(double)heading}:new[]{0.0,0,0};
  async void Start(){Application.targetFrameRate=90;UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;Time.fixedDeltaTime=.01f;Physics.IgnoreLayerCollision(9,0,true);Physics.IgnoreLayerCollision(9,10,true);Physics.IgnoreLayerCollision(31,0,true);Physics.IgnoreLayerCollision(31,10,true);Physics.IgnoreLayerCollision(31,8,true);Physics.IgnoreLayerCollision(9,8,true);Physics.IgnoreLayerCollision(8,10,false);Physics.queriesHitBackfaces=false;
   ViewCamera=new GameObject("Görünüm kamerası").AddComponent<Camera>();ViewCamera.tag="MainCamera";ViewCamera.nearClipPlane=.03f;ViewCamera.farClipPlane=150;ViewCamera.backgroundColor=new Color(.055f,.075f,.1f);ViewCamera.clearFlags=CameraClearFlags.SolidColor;ViewCamera.fieldOfView=52;ViewCamera.allowHDR=false;QualitySettings.antiAliasing=4;QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowDistance=35;RenderSettings.ambientLight=new Color(.66f,.7f,.78f);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;var light=new GameObject("Saha ışığı").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(55,-35,0);light.shadows=LightShadows.Soft;var fill=new GameObject("Dolgu ışığı").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.45f;fill.transform.rotation=Quaternion.Euler(30,130,0);
   gizmo=new RuntimeGizmo(ViewCamera,RecordNode);try{Bridge=new StudioBridge(Environment.GetCommandLineArgs().Contains("--self-test")?0:5818);}catch(Exception e){Notice="Blockly bağlantısı açılamadı: "+e.Message;}var args=Environment.GetCommandLineArgs();string bootError=null;int projectArg=Array.IndexOf(args,"--project");RobotProject initial=RobotProject.Empty();if(args.Contains("--self-test")){initial.season="2025";initial.training.spawnFuel=false;}if(projectArg>=0&&projectArg+1<args.Length){try{initial=ProjectIO.Parse(File.ReadAllText(args[projectArg+1]));}catch(Exception e){bootError=e.Message;}}if(!await LoadProject(initial)){bootError=Notice;await LoadProject(RobotProject.Empty());}if(bootError!=null)Notice=bootError;int uiTestArg=Array.IndexOf(args,"--ui-test");if(uiTestArg>=0&&uiTestArg+1<args.Length){var probe=gameObject.AddComponent<UiTestProbe>();probe.App=this;probe.Output=args[uiTestArg+1];}if(args.Contains("--self-test"))gameObject.AddComponent<PlayerChecks>().App=this;int physicsCheck=Array.IndexOf(args,"--physics-check");if(physicsCheck>=0&&physicsCheck+1<args.Length){var check=gameObject.AddComponent<PhysicsPolishChecks>();check.App=this;check.Output=args[physicsCheck+1];}int robotCheck=Array.IndexOf(args,"--robot-check");if(robotCheck>=0&&robotCheck+1<args.Length){var check=gameObject.AddComponent<RobotCodeChecks>();check.App=this;check.Output=args[robotCheck+1];}int captureArg=Array.IndexOf(args,"--capture");if(captureArg>=0&&captureArg+1<args.Length){await Task.Delay(3500);orbitDistance=8;await Task.Delay(500);ScreenCapture.CaptureScreenshot(args[captureArg+1]);await Task.Delay(1200);Application.Quit();}
  }
  public async Task<bool> LoadProject(RobotProject project)
  {
   if(Busy)return false;ProjectIO.Validate(project);if(Running)SetRunning(false);
   Busy=true;Notice="Robot projesi hazırlanıyor…";var nextLoader=new ModelLoader();
   var nextRobot=new GameObject("Takım robotu").transform;nextRobot.gameObject.SetActive(false);nextRobot.gameObject.layer=8;
   var nextNodes=new Dictionary<string,PartNode>();FieldWorld nextField=null;bool committed=false;
   try
   {
    foreach(var part in project.parts)nextNodes.Add(part.id,await BuildPart(part,nextRobot,nextLoader,project.assets));
    foreach(var part in project.parts){var node=nextNodes[part.id];node.transform.SetParent(part.parent!=null?nextNodes[part.parent].content:nextRobot,false);node.gameObject.SetActive(true);}
    if(Field==null||Field.Season!=project.season||Field.Layout!=project.tagLayout){Notice="Sezon sahası hazırlanıyor…";nextField=new FieldWorld();await nextField.Load(project.season,false,project.tagLayout);}
    var oldRobot=Robot;var oldLoader=loader;gizmo?.Select(null,false);Pieces.Clear();
    Project=project;Robot=nextRobot;loader=nextLoader;Nodes.Clear();foreach(var item in nextNodes)Nodes.Add(item.Key,item.Value);
    hull=Robot.gameObject.AddComponent<RobotChassis>();ConfigureHull();hull.enabled=false;
    if(nextField!=null){Field?.Dispose();Field=nextField;Field.Root.gameObject.SetActive(true);visionQueues.Clear();visionResults.Clear();}
    Robot.gameObject.SetActive(true);if(oldRobot!=null)Lifetime.Destroy(oldRobot.gameObject);oldLoader?.Dispose();
    committed=true;PrepareInventory();ResetPose();ResetTraining();if(Project.network.autoConnect&&Nt==null)ConnectNetwork();Selected="chassis";chosenAction=null;chosenCamera=null;Select(Selected);if(Bridge?.Connected==true&&Project.blocklyProject!=null)Bridge.Send(new{type="project",project=Project.blocklyProject});Notice="Robot hazır · kendi parçalarını ve hareketlerini ekleyebilirsin.";return true;
   }
   catch(Exception error){Notice="Proje açılamadı: "+error.Message;Debug.LogWarning(Notice);return false;}
   finally{if(!committed){Lifetime.Destroy(nextRobot.gameObject);nextLoader.Dispose();nextField?.Dispose();}Busy=false;}
  }
  async Task<PartNode> BuildPart(RobotPart part,Transform parent,ModelLoader modelLoader,Dictionary<string,ModelAsset> assets)
  {
   var root=new GameObject(part.name);root.SetActive(false);root.transform.SetParent(parent,false);
   try{var node=root.AddComponent<PartNode>();node.partId=part.id;node.transform.localPosition=Frc.Position(part.position);node.transform.localRotation=Frc.Rotation(part.rotation);node.content=new GameObject("Pivot içeriği").transform;node.content.SetParent(node.transform,false);node.content.localPosition=-Frc.Position(part.pivot);var visual=await modelLoader.Visual(part,assets);visual.transform.SetParent(node.content,false);visual.transform.localPosition=part.kind=="asset"?-Frc.Position(part.assetCenter)*(float)part.scale:Vector3.zero;return node;}
   catch{Lifetime.Destroy(root);throw;}
  }
  void ConfigureHull(){hull.Configure(Project.drive);}
  public async Task LoadField(string season,string layout=null)
  {
   if(Busy)return;Busy=true;Notice="Sezon sahası hazırlanıyor…";var next=new FieldWorld();bool committed=false;
   try{await next.Load(season,false,layout??Project.tagLayout);Field?.Dispose();Field=next;Field.Root.gameObject.SetActive(true);bool changedSeason=Project.season!=season;Project.season=season;PrepareInventory(changedSeason);fields.Clear();Project.tagLayout=layout??Project.tagLayout;Pieces.Clear();ResetTraining();visionQueues.Clear();visionResults.Clear();committed=true;Notice=season+" sahası hazır.";}
   catch(Exception error){Notice="Saha açılamadı: "+error.Message;Debug.LogWarning(Notice);}
   finally{if(!committed)next.Dispose();Busy=false;}
  }
  void ResetPose(){hull.enabled=false;heading=(float)Project.initialPose[2];hull.Teleport(Frc.Position(new[]{Project.initialPose[0],Project.initialPose[1],0}),Frc.Rotation(new[]{0.0,0,heading*180/Math.PI}));velocity=Vector3.zero;omega=0;hull.enabled=Running;cameraTarget=Robot.position+Vector3.up*.6f;}
  void Update(){if(Project==null||Busy)return;leftWidth=Screen.width<1150?210:235;rightWidth=Screen.width<1150?270:300;ViewCamera.rect=new Rect(leftWidth/Screen.width,35f/Screen.height,Math.Max(.1f,(Screen.width-leftWidth-rightWidth)/Screen.width),(Screen.height-100f)/Screen.height);bool viewport=!networkPanel&&Input.mousePosition.x>leftWidth&&Input.mousePosition.x<Screen.width-rightWidth&&Input.mousePosition.y>35&&Input.mousePosition.y<Screen.height-65;
   if(viewport&&Input.GetMouseButtonDown(0))GUI.FocusControl(null);if(Input.GetKeyDown(KeyCode.Escape))SetRunning(false);if(Input.GetKeyDown(KeyCode.F)&&!Typing){follow=true;orbitDistance=5;}if(Input.GetKeyDown(KeyCode.G)&&!Typing&&!Running){rotateGizmo=false;gizmo.Select(Nodes[Selected].transform,false);}if(Input.GetKeyDown(KeyCode.T)&&!Typing&&!Running){rotateGizmo=true;gizmo.Select(Nodes[Selected].transform,true);}bool used=!Running&&gizmo.Update(viewport&&!Typing);
   if(viewport&&!used){if(Input.GetMouseButton(1)){orbitYaw+=Input.GetAxisRaw("Mouse X")*3;orbitPitch=Mathf.Clamp(orbitPitch-Input.GetAxisRaw("Mouse Y")*3,8,85);}orbitDistance=Mathf.Clamp(orbitDistance-Input.mouseScrollDelta.y*.45f,.6f,32);if(Input.GetMouseButton(2)){follow=false;cameraTarget+=(-ViewCamera.transform.right*Input.GetAxisRaw("Mouse X")-ViewCamera.transform.up*Input.GetAxisRaw("Mouse Y"))*.025f*orbitDistance;}if(!Running&&Input.GetMouseButtonDown(0)&&!used){if(Physics.Raycast(ViewCamera.ScreenPointToRay(Input.mousePosition),out var hit,1000,1<<9)){var node=hit.collider.GetComponentInParent<PartNode>();if(node)Select(node.partId);}}}
   if(follow)cameraTarget=Vector3.Lerp(cameraTarget,Robot.position+Vector3.up*.5f,1-Mathf.Exp(-Time.unscaledDeltaTime*8));var rotation=Quaternion.Euler(orbitPitch,orbitYaw,0);ViewCamera.transform.position=cameraTarget-rotation*Vector3.forward*orbitDistance;ViewCamera.transform.LookAt(cameraTarget);if(Bridge!=null){if(Bridge.Connected&&!bridgeWasConnected&&Project.blocklyProject!=null)Bridge.Send(new{type="project",project=Project.blocklyProject});bridgeWasConnected=Bridge.Connected;var msg=Bridge.Take();if(msg!=null)HandleBlock(msg);}UpdateActionPreview(); }
  void FixedUpdate(){if(Project==null||Busy||Robot==null)return;if(hull.enabled){heading=Mathf.Atan2(-Robot.forward.x,Robot.forward.z);}tick+=Time.fixedDeltaTime;if(!Running&&NetworkPoseOwned){Vector3 held=Vector3.zero;float turn=0;ReadNetworkDrive(ref held,ref turn);}if(Running){Vector3 desired=Vector3.zero;float desiredOmega=0;bool external=false;external=ReadNetworkDrive(ref desired,ref desiredOmega);
    if(!external&&Bridge?.Fresh==true&&blockOutput!=null){var v=blockOutput["velocity"];if(v!=null){desired=Frc.Position(new[]{(double?)v["x"]??0,(double?)v["y"]??0,0});desiredOmega=(float?)v["omega"]??0;external=true;}}
    if(!external&&!Typing&&Application.isFocused){float x=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0)-(float)DriverInput.Read(0).Axes[1],y=(Input.GetKey(KeyCode.A)?1:0)-(Input.GetKey(KeyCode.D)?1:0)-(float)DriverInput.Read(0).Axes[0],r=(Input.GetKey(KeyCode.Q)?1:0)-(Input.GetKey(KeyCode.E)?1:0)-(float)DriverInput.Read(0).Axes[4];if(Project.drive.type=="differential")y=0;var flat=Vector2.ClampMagnitude(new Vector2(x,y),1);desired=Frc.Position(new[]{flat.x*Project.drive.maxSpeed,flat.y*Project.drive.maxSpeed,0});if(!Project.drive.fieldRelative)desired=Robot.rotation*desired;desiredOmega=Mathf.Clamp(r,-1,1)*(float)Project.drive.turnRate;}
    if(CheckVelocity.HasValue){desired=CheckVelocity.Value;desiredOmega=CheckOmega;}velocity=Vector3.MoveTowards(velocity,desired,(float)Project.drive.acceleration*Time.fixedDeltaTime);omega=Mathf.MoveTowards(omega,desiredOmega,10*Time.fixedDeltaTime);if(!NetworkPoseOwned){MoveChassis(velocity*Time.fixedDeltaTime,omega*Time.fixedDeltaTime);}ApplyActions();}
   TrainingTick();if(tick>=.02f){tick=0;Bridge?.Send(BlockInput());PublishDriverStation();PublishGameState();}netTick+=Time.fixedDeltaTime;if(netTick>=.05f){netTick=0;UpdateVision();if(Nt?.Connected==true){_ = Nt.Publish("/AshinaSim/Robot/pose",Pose);_ = Nt.Publish("/AshinaSim/Robot/velocity",new[]{(double)velocity.z,(double)-velocity.x,(double)omega});}}}
  internal void MoveChassis(Vector3 displacement,float turn){hull.enabled=true;hull.Move(displacement,turn);}

  public static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);static double Clamp(double n,double max)=>Finite(n)?Math.Max(-max,Math.Min(max,n)):0;public static double[] Numbers(object raw)=>NtData.Numbers(raw);
  bool Down(string key){if(Typing||!Application.isFocused)return false;if(string.IsNullOrEmpty(key))return false;if(key=="Space")return Input.GetKey(KeyCode.Space);if(key.StartsWith("Key")&&Enum.TryParse<KeyCode>(key.Substring(3),true,out var k))return Input.GetKey(k);if(Enum.TryParse<KeyCode>(key,true,out var code))return Input.GetKey(code);return false;}
  static readonly string[] Buttons={"a","b","x","y","leftBumper","rightBumper","back","start","leftStick","rightStick"};
  internal bool? CheckInput;internal Vector3? CheckVelocity;internal float CheckOmega;
  bool ActionDown(RobotAction a){if(CheckInput.HasValue)return CheckInput.Value;bool local=!(Nt!=null&&!string.IsNullOrEmpty(a.topic)&&(a.ntSource||a.topic.StartsWith("/")));bool down=local&&Down(a.key);int index=Array.IndexOf(Buttons,a.button);if(local&&index>=0&&!Typing&&Application.isFocused)down|=DriverInput.Read(a.controller).Button(index);if(!string.IsNullOrEmpty(a.topic)){double source=0;if(a.ntSource||a.topic.StartsWith("/")){var n=Nt?.Get(a.topic);if(Nt?.Connected==true)NtData.Scalar(Nt,n,a.ntField,out source);}else if(Bridge?.Fresh==true)source=(double?)blockOutput?["motors"]?[a.topic]??0;down|=a.invert?source<-.02:source>.02;}return down;}
  void ApplyActions(){var poses=Project.parts.ToDictionary(p=>p.id,p=>p.Copy());foreach(var a in Project.actions){bool down=ActionDown(a),was=previous.TryGetValue(a.id,out var old)&&old;previous[a.id]=down;if(down&&!was&&a.mode=="toggle")toggled[a.id]=!(toggled.TryGetValue(a.id,out var t)&&t);bool active=a.mode=="toggle"?(toggled.TryGetValue(a.id,out var toggleState)&&toggleState):down;float current=progress.TryGetValue(a.id,out var value)?value:0;
    if(a.type=="move"){float target=active?1:0;bool ranged=a.ntMode=="position"&&!string.IsNullOrEmpty(a.topic)&&(a.ntSource||a.topic.StartsWith("/"))&&Nt!=null;if(ranged){if(ActionNumber(a,out var number)&&Math.Abs(a.ntMax-a.ntMin)>1e-9){target=Mathf.Clamp01((float)((number-a.ntMin)/(a.ntMax-a.ntMin)));if(a.invert)target=1-target;}else target=current;}if(!ranged&&a.mode=="press"){if(down&&!was)toggled[a.id]=!(toggled.TryGetValue(a.id,out var pressState)&&pressState);target=toggled.TryGetValue(a.id,out var on)&&on?1:0;}if(!ranged&&Down(a.reverseKey))target=0;current=ranged?target:Mathf.MoveTowards(current,target,Time.fixedDeltaTime/(float)a.duration);if(!ranged&&a.loop&&active&&current>=1)current=0;progress[a.id]=current;var pose=poses[a.partId];for(int i=0;i<3;i++){pose.position[i]+=(a.to.position[i]-a.from.position[i])*current;pose.rotation[i]+=(a.to.rotation[i]-a.from.rotation[i])*current;}}
    else if(a.type=="capture"){CaptureZone(a,active);}
    else if(active&&(a.mode!="press"||!was)&&(!lastEmit.TryGetValue(a.id,out var last)||Time.time-last>=a.interval)){lastEmit[a.id]=Time.time;if(a.type=="emit")Emit(a);else{int count=Pieces.Capture(a.piece,Nodes[a.partId].content.TransformPoint(Frc.Position(a.spawn)),(float)a.captureRadius);Project.inventory[a.piece]=(Project.inventory.TryGetValue(a.piece,out int stock)?stock:0)+count;}}}
   foreach(var p in poses){Nodes[p.Key].transform.localPosition=Frc.Position(p.Value.position);Nodes[p.Key].transform.localRotation=Frc.Rotation(p.Value.rotation);}}
  public GamePiece Emit(RobotAction a){
   if(!Project.training.unlimitedShots){if(Stored(a.piece)<=0)return null;Project.inventory[a.piece]--;}
   a.consume=true;ShotsFired++;var node=Nodes[a.partId].content;double speed=a.speed;
   if(Nt?.Connected==true&&!string.IsNullOrEmpty(a.speedTopic)&&NtData.Scalar(Nt,Nt.Get(a.speedTopic),a.speedField,out var requested))speed=Math.Max(0,Math.Min(40,requested*a.speedScale+a.speedOffset));
   LastShotSpeed=speed;if(a.piece=="fuel")FuelShots++;
   Vector3 direction=node.TransformDirection(Frc.Direction(a.pitch,a.yaw)).normalized;
   Quaternion orientation=ReleaseOrientation(a,node,direction);
   var piece=Pieces.Spawn(a.piece,node.TransformPoint(Frc.Position(a.spawn)),direction*(float)speed+(a.inheritVelocity?(hull.enabled?hull.Body.linearVelocity:velocity):Vector3.zero),orientation);
   if(a.piece=="coral"){
    Vector3 axis=Vector3.Cross(piece.transform.up,node.up);if(axis.sqrMagnitude<.0001f)axis=Vector3.ProjectOnPlane(node.right,piece.transform.up);
    float spin=(float)a.coralSpin*Mathf.Deg2Rad;piece.Body.maxAngularVelocity=Mathf.Max(7,Mathf.Abs(spin));piece.Body.angularVelocity=axis.normalized*spin;
   }
   return piece;
  }
  public void SetRunning(bool running){if(Busy||Project==null||FileDialogs.IsOpen)return;if(!Running)RecordNode();ClearActionAnchor(false);Running=running;velocity=Vector3.zero;omega=0;progress.Clear();lastEmit.Clear();previous.Clear();toggled.Clear();if(!running){foreach(var p in Project.parts){Nodes[p.id].transform.localPosition=Frc.Position(p.position);Nodes[p.id].transform.localRotation=Frc.Rotation(p.rotation);}}else ConfigureHull();SetPicking();if(running)hull.Body.ResetInertiaTensor();hull.enabled=running&&!NetworkPoseOwned;gizmo.Select(running?null:Nodes[Selected].transform,rotateGizmo);GUI.FocusControl(null);}
  void SetPicking(){foreach(var c in Robot.GetComponentsInChildren<Collider>(true))if(c.gameObject.layer==9)c.enabled=!Running;}
  void Select(string id){ClearActionAnchor(true);if(!Nodes.ContainsKey(id))return;if(!Running)RecordNode();Selected=id;fields.Clear();gizmo.Select(Running?null:Nodes[id].transform,rotateGizmo);SetPicking();}
  void RecordNode(){if(RecordActionAnchor())return;if(Running||Project==null||!Nodes.TryGetValue(Selected,out var node))return;var part=Project.parts.Find(p=>p.id==Selected);part.position=Frc.Position(node.transform.localPosition);part.rotation=Frc.Euler(node.transform.localRotation);fields.Clear();}
  public async Task<bool> ImportRobotModel(string path){
   if(string.IsNullOrEmpty(path)||Busy||Running)return false;
   Busy=true;Notice="Ana robot modeli okunuyor…";
   try{
    var candidate=await Task.Run(()=>{
     string type=Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
     if(!new[]{"stl","glb","obj"}.Contains(type))throw new Exception("GLB, STL veya OBJ seçin.");
     var info=new FileInfo(path);if(!info.Exists||info.Length==0||info.Length>125000000)throw new Exception("Model dosyası boş, bulunamadı veya 125 MB sınırını aşıyor.");
     var copy=ProjectIO.Parse(ProjectIO.Write(Project));var main=copy.parts.Find(p=>p.id=="chassis");
     string id=Guid.NewGuid().ToString("N");copy.assets[id]=new ModelAsset{name=Path.GetFileName(path),type=type,data=Convert.ToBase64String(File.ReadAllBytes(path))};
     main.name="Ana robot";main.kind="asset";main.asset=id;main.meshIndex=null;main.assetCenter=new[]{0.0,0,0};main.scale=1;
     foreach(var unused in copy.assets.Keys.Where(key=>!copy.parts.Any(p=>p.asset==key)).ToArray())copy.assets.Remove(unused);
     ProjectIO.Validate(copy);return copy;
    });
    Busy=false;bool loaded=await LoadProject(candidate);if(loaded)Notice="Ana robot modeli yüklendi. Hareketli mekanizmaları istersen parça olarak ekle.";return loaded;
   }catch(Exception error){Notice="Ana robot yüklenemedi: "+error.Message;Debug.LogWarning(Notice);return false;}
   finally{Busy=false;}
  }
  public async Task<bool> ImportModel(string path)
  {
   if(string.IsNullOrEmpty(path)||Busy||Running)return false;Busy=true;Notice="Model dosyası okunuyor…";PartNode pending=null;string assetId=Guid.NewGuid().ToString("N");RobotPart part=null;
   try
   {
    if(Project.parts.Count>=128)throw new Exception("En fazla 128 parça eklenebilir.");
    string type=Path.GetExtension(path).TrimStart('.').ToLowerInvariant();if(!new[]{"stl","glb","obj"}.Contains(type))throw new Exception("Bu dosya türü desteklenmiyor. GLB, STL veya OBJ seçin.");
    var asset=await Task.Run(()=>{var info=new FileInfo(path);if(!info.Exists)throw new Exception("Model dosyası bulunamadı.");if(info.Length==0)throw new Exception("Model dosyası boş.");if(info.Length>125000000)throw new Exception("Model 125 MB sınırını aşıyor. CAD modelini sadeleştirerek dışa aktarın.");if(Project.assets.Values.Sum(a=>(long)a.data.Length)+((info.Length+2)/3)*4>180000000)throw new Exception("Projede model dosyaları için ayrılan alan doldu.");return new ModelAsset{name=Path.GetFileName(path),type=type,data=Convert.ToBase64String(File.ReadAllBytes(path))};});
    string name=Path.GetFileNameWithoutExtension(path);if(name.Length>100)name=name.Substring(0,100);
    part=new RobotPart{name=name,parent=Selected,kind="asset",asset=assetId,position=new[]{0.0,0,.15}};
    var assets=new Dictionary<string,ModelAsset>(Project.assets){{assetId,asset}};Notice="Model geometrisi hazırlanıyor…";
    pending=await BuildPart(part,Nodes[part.parent].content,loader,assets);
    Project.assets.Add(assetId,asset);Project.parts.Add(part);Nodes.Add(part.id,pending);pending.gameObject.SetActive(true);Select(part.id);
    Notice=name+" eklendi. Konumunu, pivotunu ve ölçeğini sağ panelden ayarlayabilirsin.";return true;
   }
   catch(Exception error){if(pending!=null){Lifetime.Destroy(pending.gameObject);if(part!=null){Nodes.Remove(part.id);Project.parts.Remove(part);}}Project.assets.Remove(assetId);Notice="Model yüklenemedi: "+error.Message;Debug.LogWarning(Notice);return false;}
   finally{Busy=false;}
  }
  async void AddPart(string kind)
  {
   if(Busy||Running||Project.parts.Count>=128)return;Busy=true;Notice="Parça ekleniyor…";
   try{var part=new RobotPart{kind=kind,parent=Selected,position=new[]{0.0,0,.2}};var node=await BuildPart(part,Nodes[part.parent].content,loader,Project.assets);Project.parts.Add(part);Nodes.Add(part.id,node);node.gameObject.SetActive(true);Select(part.id);Notice="Parça eklendi.";}
   catch(Exception error){Notice="Parça eklenemedi: "+error.Message;Debug.LogWarning(Notice);}finally{Busy=false;}
  }
  async void Split()
  {
   if(Busy||Running)return;var part=Project.parts.Find(p=>p.id==Selected);if(part.kind!="asset"||part.meshIndex.HasValue){Notice="Bölmek için bir GLB montajı seç.";return;}Busy=true;Notice="Montaj parçaları hazırlanıyor…";var pending=new List<KeyValuePair<RobotPart,PartNode>>();bool committed=false;
   try{var source=await loader.Source(part.asset,Project.assets[part.asset]);var filters=source.GetComponentsInChildren<MeshFilter>(true);if(Project.parts.Count+filters.Length>128)throw new Exception("Montaj 128 parça sınırını aşıyor. CAD'de daha az alt grup olarak dışa aktarın.");for(int i=0;i<filters.Length;i++){var matrix=source.transform.worldToLocalMatrix*filters[i].transform.localToWorldMatrix;var center=matrix.MultiplyPoint3x4(filters[i].sharedMesh.bounds.center);var p=new RobotPart{name=filters[i].name.Length>100?filters[i].name.Substring(0,100):filters[i].name,parent=part.id,kind="asset",asset=part.asset,meshIndex=i,position=Frc.Position(center*(float)part.scale),assetCenter=Frc.Position(center),scale=part.scale};pending.Add(new KeyValuePair<RobotPart,PartNode>(p,await BuildPart(p,Nodes[part.id].content,loader,Project.assets)));}foreach(var item in pending){Project.parts.Add(item.Key);Nodes.Add(item.Key.id,item.Value);item.Value.gameObject.SetActive(true);}part.kind="group";Lifetime.Destroy(Nodes[part.id].content.GetChild(0).gameObject);committed=true;Select(part.id);Notice="Montaj ayrıldı; her parçaya ayrı hareket bağlayabilirsin.";}
   catch(Exception error){Notice="Montaj ayrılamadı: "+error.Message;Debug.LogWarning(Notice);}finally{if(!committed)foreach(var item in pending)Lifetime.Destroy(item.Value.gameObject);Busy=false;}
  }
  void RemovePart(){if(Busy||Running||Selected=="chassis")return;string original=Selected;var remove=new HashSet<string>{Selected};bool more=true;while(more){more=false;foreach(var p in Project.parts)if(p.parent!=null&&remove.Contains(p.parent)&&remove.Add(p.id))more=true;}Select("chassis");Lifetime.Destroy(Nodes[original].gameObject);Project.parts.RemoveAll(p=>remove.Contains(p.id));Project.actions.RemoveAll(a=>remove.Contains(a.partId));Project.cameras.RemoveAll(c=>remove.Contains(c.parent));foreach(var id in remove)Nodes.Remove(id);chosenAction=null;chosenCamera=null;Notice="Parça kaldırıldı.";}
  public void Save(string path){RecordNode();ProjectIO.Validate(Project);var temp=path+".tmp";File.WriteAllText(temp,ProjectIO.Write(Project));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);savedPath=path;Notice="Kaydedildi: "+Path.GetFileName(path);}
  async void Open()
  {
   if(Busy||FileDialogs.IsOpen)return;
   try{Notice="Açılacak projeyi seç…";var path=await FileDialogs.OpenAsync();if(path==null){Notice="Proje seçimi iptal edildi.";return;}Busy=true;Notice="Proje dosyası okunuyor…";var json=await Task.Run(()=>{var info=new FileInfo(path);if(info.Length>200000000)throw new Exception("Proje dosyası çok büyük.");return JObject.Parse(File.ReadAllText(path));});if((string)json["format"]=="ashina-robot-studio"){var candidate=await Task.Run(()=>ProjectIO.Parse(json.ToString()));Busy=false;if(await LoadProject(candidate))savedPath=path;}else{if(!(json["subsystems"] is JArray)||json["schemaVersion"]==null)throw new Exception("Bu dosya bir Robot Studio veya Blockly projesi değil.");Project.blocklyProject=json;Notice="Blockly projesi eklendi. Canlı çalıştırmak için Blockly'yi aç.";}}
   catch(Exception error){Notice="Proje açılamadı: "+error.Message;Debug.LogWarning(Notice);}finally{Busy=false;}
  }
  async void SaveDialog()
  {
   if(Busy||FileDialogs.IsOpen)return;
   try{var path=savedPath;if(path==null){Notice="Projenin kaydedileceği dosyayı seç…";path=await FileDialogs.SaveAsync();}if(path==null){Notice="Kaydetme iptal edildi.";return;}RecordNode();Busy=true;Notice="Proje kaydediliyor…";ProjectIO.Validate(Project);await Task.Run(()=>{var temp=path+".tmp";File.WriteAllText(temp,ProjectIO.Write(Project));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);});savedPath=path;Notice="Kaydedildi: "+Path.GetFileName(path);}
   catch(Exception error){Notice="Proje kaydedilemedi: "+error.Message;Debug.LogWarning(Notice);}finally{Busy=false;}
  }
  void HandleBlock(JObject msg){try{if((string)msg["type"]=="state"){blockOutput=msg;if(msg["errors"] is JArray errors&&errors.Count>0)Notice="Bu blok için WPILib simülasyonu gerekli: "+(string)errors[0];if(msg["project"] is JObject p&&p.ToString().Length<24000000){Project.blocklyProject=p;BlocklyProjectUpdates++;Notice="Blockly projesi canlı bağlandı · bloklar 20 ms adımda çalışıyor.";}}}catch(Exception e){Notice=e.Message;}}
  object BlockInput(){var buttons=new List<string>();for(int i=0;i<Buttons.Length;i++)if(DriverInput.Read(0).Button(i))buttons.Add("driver:"+Buttons[i]);return new{type="tick",mode=Running?RobotMode:"disabled",pose=new{x=Pose[0],y=Pose[1],angle=Pose[2]},axes=new{LeftY=Input.GetAxisRaw("PadLeftY")+(Input.GetKey(KeyCode.S)?1:0)-(Input.GetKey(KeyCode.W)?1:0),LeftX=Input.GetAxisRaw("PadLeftX")+(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),RightX=Input.GetAxisRaw("PadRightX")+(Input.GetKey(KeyCode.E)?1:0)-(Input.GetKey(KeyCode.Q)?1:0)},buttons,cameras=visionResults.ToDictionary(x=>x.Key,x=>{var b=x.Value.Seen.FirstOrDefault();return new{target=b!=null,yaw=b?.Tx??0,pitch=b?.Ty??0,area=b?.Area??0,id=b?.Id??-1,distance=b?.Distance??0,poseX=x.Value.Pose[0],poseY=x.Value.Pose[1],poseYaw=x.Value.Pose[2]*180/Math.PI,latency=x.Value.Latency};})};}
  void OpenBlockly(){if(Bridge==null){Notice="Blockly köprüsü kullanılamıyor.";return;}Application.OpenURL(new Uri(Path.Combine(Application.streamingAssetsPath,"blockly/index.html")).AbsoluteUri+"#unity="+Bridge.Token+"&port="+Bridge.Port);}
  void UpdateVision(){if(Field==null)return;foreach(var c in Project.cameras.Where(c=>c.enabled)){if(!visionQueues.TryGetValue(c.id,out var queue))visionQueues[c.id]=queue=new Queue<VisionFrame>();queue.Enqueue(Vision.Detect(c,Nodes[c.parent].content,Field,Pose));while(queue.Count>0&&(Time.realtimeSinceStartup-queue.Peek().Captured)*1000>=c.latency){var frame=queue.Dequeue();frame.Latency=(Time.realtimeSinceStartup-frame.Captured)*1000;visionResults[c.name]=frame;if(Nt?.Connected==true&&Project.network.vision)PublishVision(c,frame);}}}
  void PublishVision(CameraData c,VisionFrame f){_ = Nt.Publish("/AshinaSim/Vision/"+c.name+"/frame",Newtonsoft.Json.JsonConvert.SerializeObject(new{sequence=f.Sequence,latency=f.Latency,cameraPose=f.CameraPose,targets=f.Seen.Select(t=>new{id=t.Id,yaw=t.Tx,pitch=t.Ty,area=t.Area,cameraToTarget=t.CameraToTarget}).ToArray()}),"string");var b=f.Seen.FirstOrDefault();string path="/"+c.name;_ = Nt.Publish(path+"/tv",b==null?0d:1d,"double");_ = Nt.Publish(path+"/tx",b?.Tx??0,"double");_ = Nt.Publish(path+"/ty",b?.Ty??0,"double");_ = Nt.Publish(path+"/ta",b?.Area??0,"double");_ = Nt.Publish(path+"/tid",(double)(b?.Id??-1),"double");_ = Nt.Publish(path+"/tl",(double)f.Latency,"double");_ = Nt.Publish(path+"/cl",0d,"double");_ = Nt.Publish("/AshinaSim/Vision/"+c.name+"/visibleIds",f.Seen.Select(t=>(double)t.Id).ToArray());if(b!=null){var p=new[]{f.Pose[0],f.Pose[1],0,0,0,f.Pose[2]*180/Math.PI,f.Latency,f.Seen.Count,0,b.Distance,b.Area};_ = Nt.Publish(path+"/botpose_wpiblue",p);_ = Nt.Publish(path+"/botpose_orb_wpiblue",p);}}
  void OnApplicationFocus(bool focused){if(!focused){velocity=Vector3.zero;omega=0;}}
  void OnDestroy(){if(actionLine!=null)Lifetime.Destroy(actionLine.gameObject);if(actionLineMaterial!=null)Lifetime.Destroy(actionLineMaterial);robotRunner?.Dispose();Bridge?.Dispose();Nt?.Dispose();loader?.Dispose();Field?.Dispose();Pieces.Dispose();}
 }
}









