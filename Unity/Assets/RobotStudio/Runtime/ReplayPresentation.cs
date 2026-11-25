using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace AshinaStudio {
 internal sealed class ReplayPresentation : IDisposable {
  const int Layer=29;
  sealed class Avatar {
   internal Transform root;
   internal readonly Dictionary<string,Transform> nodes=new Dictionary<string,Transform>();
  }
  readonly GamePieces visuals;
  readonly ReplayHeader header;
  readonly RobotProject project;
  readonly Transform model;
  readonly Dictionary<string,Avatar> avatars=new Dictionary<string,Avatar>();
  readonly Dictionary<int,Transform> objects=new Dictionary<int,Transform>();
  readonly Dictionary<int,MatchPieceState> nextPieces=new Dictionary<int,MatchPieceState>();
  readonly Dictionary<string,Stack<Transform>> spare=new Dictionary<string,Stack<Transform>>();
  readonly Dictionary<Transform,string> pieceKind=new Dictionary<Transform,string>();
  readonly HashSet<int> activePieces=new HashSet<int>();
  readonly HashSet<string> activeRobots=new HashSet<string>();
  readonly List<int> oldPieces=new List<int>();
  readonly List<string> oldRobots=new List<string>();
  readonly Material blue,red;
  readonly Transform root;
  readonly Camera camera;
  readonly float fieldLength,fieldWidth;
  internal bool Overhead;
  internal string Focus;
  internal bool HasOnlyVisuals=>root.GetComponentsInChildren<Rigidbody>(true).Length==0&&root.GetComponentsInChildren<Collider>(true).All(c=>!c.enabled);

  ReplayPresentation(GamePieces pieces,ReplayHeader info,RobotProject robot,Transform geometry,FieldWorld field){
   visuals=pieces;header=info;project=robot;model=geometry;
   fieldLength=(float)field.Length;fieldWidth=(float)field.Width;
   root=new GameObject("Kayıt görselleri").transform;root.gameObject.layer=Layer;root.gameObject.SetActive(false);
   camera=new GameObject("Kayıt kamerası").AddComponent<Camera>();
   camera.transform.SetParent(root,false);camera.cullingMask=1<<Layer;camera.clearFlags=CameraClearFlags.SolidColor;
   camera.backgroundColor=new Color(.05f,.08f,.12f);camera.nearClipPlane=.04f;camera.farClipPlane=100;camera.fieldOfView=51;
   blue=ModelLoader.Material(new Color(.16f,.48f,.85f));red=ModelLoader.Material(new Color(.80f,.20f,.24f));
   Focus=info.localRobot;
  }

  internal static async Task<ReplayPresentation> Build(GamePieces pieces,ReplayHeader info,RobotProject robot,Transform geometry,FieldWorld field){
   var view=new ReplayPresentation(pieces,info,robot,geometry,field);
   try{
    int copied=0;
    foreach(var mesh in field.Root.GetComponentsInChildren<MeshFilter>(true)){
     if(!VisibleUnder(mesh.transform,field.Root))continue;
     view.Copy(mesh,view.root,null);
     if(++copied%100==0)await Task.Yield();
    }
    view.root.gameObject.SetActive(true);
    return view;
   }catch{view.Dispose();throw;}
  }

  static bool VisibleUnder(Transform node,Transform stop){
   while(node!=null&&node!=stop){if(!node.gameObject.activeSelf)return false;node=node.parent;}
   return node==stop;
  }

  void Copy(MeshFilter source,Transform parent,Transform relative){
   var material=source.GetComponent<MeshRenderer>();
   if(source.sharedMesh==null||material==null||!material.enabled)return;
   var original=source.transform;
   var copy=new GameObject(source.name).transform;
   copy.gameObject.layer=Layer;copy.SetParent(parent,false);
   if(relative!=null){
    copy.localPosition=relative.InverseTransformPoint(original.position);
    copy.localRotation=Quaternion.Inverse(relative.rotation)*original.rotation;
   }else copy.SetPositionAndRotation(original.position,original.rotation);
   copy.localScale=original.lossyScale;
   copy.gameObject.AddComponent<MeshFilter>().sharedMesh=source.sharedMesh;
   copy.gameObject.AddComponent<MeshRenderer>().sharedMaterials=material.sharedMaterials;
  }

  Avatar MakeAvatar(MatchRobotState state){
   var body=new GameObject(state.name).transform;body.gameObject.layer=Layer;body.SetParent(root,false);
   var avatar=new Avatar{root=body};
   if(state.id==header.localRobot&&project?.name==header.projectName&&model!=null){
    var sources=model.GetComponentsInChildren<PartNode>(true).ToDictionary(n=>n.partId);
    if(state.parts.All(part=>sources.ContainsKey(part.id))){
     foreach(var part in project.parts){
      var node=new GameObject(part.name).transform;node.gameObject.layer=Layer;avatar.nodes[part.id]=node;
     }
     foreach(var part in project.parts){
      var node=avatar.nodes[part.id];
      node.SetParent(part.parent!=null?avatar.nodes[part.parent]:body,false);
      node.localPosition=Frc.Position(part.position);node.localRotation=Frc.Rotation(part.rotation);
     }
     foreach(var mesh in model.GetComponentsInChildren<MeshFilter>(true)){
      if(!VisibleUnder(mesh.transform,model))continue;
      var source=mesh.GetComponentInParent<PartNode>();
      if(source!=null&&avatar.nodes.TryGetValue(source.partId,out var node))Copy(mesh,node,source.transform);
     }
     return avatar;
    }
   }
   var proxy=GameObject.CreatePrimitive(PrimitiveType.Cube);
   proxy.name="Robot gövde temsili";proxy.layer=Layer;proxy.transform.SetParent(body,false);
   var collider=proxy.GetComponent<Collider>();collider.enabled=false;Lifetime.Destroy(collider);
   var size=Array.Find(header.robots,r=>r.id==state.id)?.size??new[]{.88f,.78f,.34f};
   proxy.transform.localScale=new Vector3(size[1],size[2],size[0]);
   proxy.transform.localPosition=Vector3.up*(size[2]*.5f);
   proxy.GetComponent<Renderer>().sharedMaterial=state.alliance=="red"?red:blue;
   return avatar;
  }

  internal void Apply(ReplayFrame before,ReplayFrame after,float fraction){
   activeRobots.Clear();
   foreach(var state in before.robots){
    activeRobots.Add(state.id);
    if(!avatars.TryGetValue(state.id,out var avatar))avatars[state.id]=avatar=MakeAvatar(state);
    var newer=Array.Find(after.robots,r=>r.id==state.id)??state;
    var pos=Vector3.Lerp(MatchProtocol.Vector(state.p),MatchProtocol.Vector(newer.p),fraction);
    var rot=Quaternion.Slerp(MatchProtocol.Rotation(state.q),MatchProtocol.Rotation(newer.q),fraction);
    avatar.root.SetPositionAndRotation(pos,rot);
    foreach(var item in state.parts){
     if(!avatar.nodes.TryGetValue(item.id,out var node))continue;
     var next=Array.Find(newer.parts,p=>p.id==item.id)??item;
     node.localPosition=Vector3.Lerp(MatchProtocol.Vector(item.p),MatchProtocol.Vector(next.p),fraction);
     node.localRotation=Quaternion.Slerp(MatchProtocol.Rotation(item.q),MatchProtocol.Rotation(next.q),fraction);
    }
   }
   oldRobots.Clear();foreach(string id in avatars.Keys)if(!activeRobots.Contains(id))oldRobots.Add(id);
   foreach(string id in oldRobots){avatars[id].root.gameObject.SetActive(false);Lifetime.Destroy(avatars[id].root.gameObject);avatars.Remove(id);}
   nextPieces.Clear();foreach(var item in after.pieces)nextPieces[item.id]=item;
   activePieces.Clear();
   foreach(var item in before.pieces){
    activePieces.Add(item.id);
    if(!objects.TryGetValue(item.id,out var visual)){
     if(spare.TryGetValue(item.kind,out var pool)&&pool.Count>0){visual=pool.Pop();visual.gameObject.SetActive(true);}
     else {visual=visuals.CreateReplayPiece(item.kind,root,Layer);pieceKind[visual]=item.kind;}
     objects[item.id]=visual;
    }
    if(!nextPieces.TryGetValue(item.id,out var next))next=item;
    visual.SetPositionAndRotation(Vector3.Lerp(MatchProtocol.Vector(item.p),MatchProtocol.Vector(next.p),fraction),
     Quaternion.Slerp(MatchProtocol.Rotation(item.q),MatchProtocol.Rotation(next.q),fraction));
   }
   oldPieces.Clear();foreach(int id in objects.Keys)if(!activePieces.Contains(id))oldPieces.Add(id);
   foreach(int id in oldPieces){
    var visual=objects[id];objects.Remove(id);visual.gameObject.SetActive(false);
    string kind=pieceKind[visual];
    if(!spare.TryGetValue(kind,out var pool))spare[kind]=pool=new Stack<Transform>();
    if(pool.Count<50)pool.Push(visual);else{pieceKind.Remove(visual);Lifetime.Destroy(visual.gameObject);}
   }
   var center=new Vector3(-fieldWidth*.5f,.2f,fieldLength*.5f);
   if(Overhead){
    camera.orthographic=true;camera.orthographicSize=Math.Max(fieldLength,fieldWidth/Math.Max(.5f,camera.aspect))* .58f;
    camera.transform.SetPositionAndRotation(center+Vector3.up*20,Quaternion.LookRotation(Vector3.down,Vector3.forward));
   }else{
    camera.orthographic=false;
    if(Focus!=null&&avatars.TryGetValue(Focus,out var chosen))center=chosen.root.position+Vector3.up*.4f;
    camera.transform.position=center+new Vector3(4,3.8f,-4.5f);
    camera.transform.LookAt(center);
   }
  }

  public void Dispose(){
   if(root!=null){root.gameObject.SetActive(false);Lifetime.Destroy(root.gameObject);}
   if(red!=null)Lifetime.Destroy(red);
   if(blue!=null)Lifetime.Destroy(blue);
  }
 }
}
