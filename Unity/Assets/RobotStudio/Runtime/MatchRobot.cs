using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace AshinaStudio {
 // Per-player state. The host alone owns the body, stock, random pickup trials and emission.
 public sealed class MatchRobot : IDisposable {
  public string Id,Name,Alliance;public int Slot;public bool Ready,Live=true,Authority;public readonly RobotProject Project;
  public Transform Root {get;private set;}public RobotChassis Chassis {get;private set;}public readonly Dictionary<string,PartNode> Nodes=new Dictionary<string,PartNode>();
  public MatchInput Input;public double InputAt;public long LastSequence=-1;public int Shots,Pickups;
  readonly ModelLoader loader=new ModelLoader();readonly Dictionary<string,bool> previous=new Dictionary<string,bool>(),toggles=new Dictionary<string,bool>();
  readonly Dictionary<string,double> emitted=new Dictionary<string,double>();readonly Dictionary<string,HashSet<int>> attempted=new Dictionary<string,HashSet<int>>();
  readonly Dictionary<string,Vector3> muzzleLast=new Dictionary<string,Vector3>(),muzzleVelocity=new Dictionary<string,Vector3>();
  readonly Dictionary<string,int> initialStock;readonly System.Random random;long sampled=-1;double sampledAt;Material bumperMaterial;Vector3 renderFrom,renderTo;Quaternion rotationFrom,rotationTo;double renderAt;
  bool disposed;
  MatchRobot(RobotProject p){Project=p;initialStock=new Dictionary<string,int>(p.inventory);random=new System.Random(p.training.seed);}
  public static async Task<MatchRobot> Create(RobotProject project,string id,string name,string alliance,int slot,bool authority){
   var actor=new MatchRobot(project){Id=id,Name=name,Alliance=alliance,Slot=slot,Authority=authority};
   try{
    actor.Root=new GameObject("Online · "+name).transform;actor.Root.gameObject.layer=8;actor.Root.gameObject.SetActive(false);
    foreach(var part in project.parts){
     var root=new GameObject(part.name);root.transform.SetParent(actor.Root,false);var n=root.AddComponent<PartNode>();n.partId=part.id;n.transform.localPosition=Frc.Position(part.position);n.transform.localRotation=Frc.Rotation(part.rotation);n.content=new GameObject("Pivot içeriği").transform;n.content.SetParent(root.transform,false);n.content.localPosition=-Frc.Position(part.pivot);
     var visual=await actor.loader.Visual(part,project.assets);visual.transform.SetParent(n.content,false);visual.transform.localPosition=part.kind=="asset"?-Frc.Position(part.assetCenter)*(float)part.scale:Vector3.zero;actor.Nodes.Add(part.id,n);
    }
    foreach(var part in project.parts)actor.Nodes[part.id].transform.SetParent(part.parent==null?actor.Root:actor.Nodes[part.parent].content,false);
    foreach(var c in actor.Root.GetComponentsInChildren<Collider>(true))c.enabled=false;
    actor.bumperMaterial=ModelLoader.Material(alliance=="blue"?new Color(.12f,.35f,.96f):new Color(.94f,.16f,.2f));
    var band=GameObject.CreatePrimitive(PrimitiveType.Cube);band.name="İttifak bandı";band.transform.SetParent(actor.Root,false);band.transform.localPosition=new Vector3(0,.22f,(float)project.drive.dimensions[0]*.5f+.008f);band.transform.localScale=new Vector3((float)project.drive.dimensions[1],.09f,.015f);band.GetComponent<Collider>().enabled=false;band.GetComponent<Renderer>().sharedMaterial=actor.bumperMaterial;
    if(authority){actor.Chassis=actor.Root.gameObject.AddComponent<RobotChassis>();actor.Chassis.Configure(project.drive);actor.Chassis.enabled=true;}
    actor.Root.gameObject.SetActive(true);return actor;
   }catch{actor.Dispose();throw;}
  }
  public void Spawn(double fieldLength){
   var p=Frc.Position(new[]{Alliance=="blue"?2.0:fieldLength-2,1.25+(Slot%3)*2.6,0});var q=Frc.Rotation(new[]{0.0,0,Alliance=="blue"?0:180});
   if(Chassis!=null)Chassis.Teleport(p,q);else Root.SetPositionAndRotation(p,q);ResetControls();Shots=Pickups=0;Project.inventory=new Dictionary<string,int>(initialStock);Ready=false;
  }
  public void ResetControls(){previous.Clear();toggles.Clear();emitted.Clear();attempted.Clear();muzzleLast.Clear();muzzleVelocity.Clear();sampled=-1;Input=null;LastSequence=-1;}
  public bool Submit(MatchInput input){if(input==null||input.sequence<=LastSequence||!input.Validate(Project))return false;LastSequence=input.sequence;Input=input;InputAt=MatchLink.Clock;return true;}
  public void Tick(GamePieces pieces,bool enabled){
   bool fresh=Live&&Input!=null&&MatchLink.Clock-InputAt<.35;
   if(fresh&&Input.sequence!=sampled){
    foreach(var part in Input.parts){var n=Nodes[part.id].transform;n.localPosition=MatchProtocol.Vector(part.p);n.localRotation=MatchProtocol.Rotation(part.q);}
    double elapsed=sampled<0?.02:Math.Max(.005,Math.Min(.35,MatchLink.Clock-sampledAt));
    foreach(var a in Project.actions.Where(x=>x.type=="emit")){var local=Root.InverseTransformPoint(Nodes[a.partId].content.TransformPoint(Frc.Position(a.spawn)));muzzleVelocity[a.id]=muzzleLast.TryGetValue(a.id,out var before)?Vector3.ClampMagnitude((local-before)/(float)elapsed,20):Vector3.zero;muzzleLast[a.id]=local;}
    sampled=Input.sequence;sampledAt=MatchLink.Clock;
   }
   Vector3 drive=Vector3.zero;float turn=0;if(enabled&&fresh){drive=Frc.Position(new[]{(double)Input.drive[0],Input.drive[1],0});drive=Vector3.ClampMagnitude(drive,(float)Project.drive.maxSpeed);turn=Mathf.Clamp(Input.drive[2],-(float)Project.drive.turnRate,(float)Project.drive.turnRate);}
   Chassis.Move(drive*Time.fixedDeltaTime,turn*Time.fixedDeltaTime);
   if(!enabled||!fresh){previous.Clear();toggles.Clear();return;}
   foreach(var action in Project.actions){var sample=Input.actions.First(x=>x.id==action.id);bool down=sample.down,was=previous.TryGetValue(action.id,out var last)&&last;previous[action.id]=down;
    if(down&&!was&&action.mode=="toggle")toggles[action.id]=!(toggles.TryGetValue(action.id,out var on)&&on);
    bool active=action.mode=="toggle"?toggles.TryGetValue(action.id,out var toggle)&&toggle:down;
    if(action.type=="capture")Capture(pieces,action,active);
    if(action.type=="emit"&&active&&(action.mode!="press"||!was)&&(!emitted.TryGetValue(action.id,out var at)||MatchLink.Clock-at>=action.interval)){emitted[action.id]=MatchLink.Clock;Emit(pieces,action,sample);}
   }
  }
  public GamePiece Emit(GamePieces pieces,RobotAction a,MatchActionInput sample){
   if(!Project.training.unlimitedShots&&(!Project.inventory.TryGetValue(a.piece,out int stock)||stock<=0))return null;
   var mount=Nodes[a.partId].content;Vector3 origin=mount.TransformPoint(Frc.Position(a.spawn));
   Vector3 inherited=a.inheritVelocity?Chassis.Body.GetPointVelocity(origin):Vector3.zero;if(a.inheritVelocity&&muzzleVelocity.TryGetValue(a.id,out var relative))inherited+=Root.TransformDirection(relative);
   if(!ShotPlanning.Resolve(a,sample,Root,mount,origin,inherited,out var plan,out _))return null;
   var piece=pieces.Release(a,mount,Chassis.Bumper,plan,random);
   if(!Project.training.unlimitedShots)Project.inventory[a.piece]--;Shots++;return piece;
  }
  void Capture(GamePieces pieces,RobotAction a,bool active){
   if(!attempted.TryGetValue(a.id,out var tried))attempted[a.id]=tried=new HashSet<int>();var inside=new HashSet<int>();var mount=Nodes[a.partId].content;var center=mount.TransformPoint(Frc.Position(a.spawn));var half=new Vector3((float)a.captureSize[1],(float)a.captureSize[2],(float)a.captureSize[0])*.5f;
   for(int i=pieces.Active.Count-1;i>=0;i--){var p=pieces.Active[i];if(p==null||p.Removed||p.Kind!=a.piece)continue;var local=Quaternion.Inverse(mount.rotation)*(p.transform.position-center);float r=p.Radius;
    bool touch=a.captureShape=="box"?Math.Abs(local.x)<=half.x+r&&Math.Abs(local.y)<=half.y+r&&Math.Abs(local.z)<=half.z+r:local.magnitude<=a.captureRadius+r;if(!touch)continue;inside.Add(p.NetId);
    if(!active||Project.inventory.Values.Sum()>=Project.training.capacity||!tried.Add(p.NetId))continue;if(random.NextDouble()>=a.captureEfficiency)continue;pieces.Remove(p);Project.inventory[a.piece]=(Project.inventory.TryGetValue(a.piece,out int count)?count:0)+1;Pickups++;
   }tried.IntersectWith(inside);
  }
  public MatchRobotState Snapshot()=>new MatchRobotState{id=Id,name=Name,alliance=Alliance,slot=Slot,ready=Ready,live=Live,p=MatchProtocol.V(Root.position),q=MatchProtocol.Q(Root.rotation),v=MatchProtocol.V(Chassis.Body.linearVelocity),w=MatchProtocol.V(Chassis.Body.angularVelocity),parts=Nodes.Select(x=>new MatchPartState{id=x.Key,p=MatchProtocol.V(x.Value.transform.localPosition),q=MatchProtocol.Q(x.Value.transform.localRotation)}).ToArray(),stock=new Dictionary<string,int>(Project.inventory),shots=Shots,pickups=Pickups};
  public void Apply(MatchRobotState state){Ready=state.ready;Live=state.live;Project.inventory=state.stock;Shots=state.shots;Pickups=state.pickups;renderFrom=Root.position;rotationFrom=Root.rotation;renderTo=MatchProtocol.Vector(state.p);rotationTo=MatchProtocol.Rotation(state.q);renderAt=MatchLink.Clock;foreach(var part in state.parts??new MatchPartState[0])if(Nodes.TryGetValue(part.id,out var n)){n.transform.localPosition=MatchProtocol.Vector(part.p);n.transform.localRotation=MatchProtocol.Rotation(part.q);}}
  public void Render(){if(Authority)return;float t=Mathf.Clamp01((float)((MatchLink.Clock-renderAt)/.05));Root.SetPositionAndRotation(Vector3.Lerp(renderFrom,renderTo,t),Quaternion.Slerp(rotationFrom,rotationTo,t));}
  public void HideVisuals(){foreach(var r in Root.GetComponentsInChildren<Renderer>())r.enabled=false;}
  public void Dispose(){if(disposed)return;disposed=true;if(Root!=null){Root.gameObject.SetActive(false);Lifetime.Destroy(Root.gameObject);}loader.Dispose();if(bumperMaterial!=null)Lifetime.Destroy(bumperMaterial);}
 }
}
