using System.Collections.Generic;
using UnityEngine;
namespace AshinaStudio {
 public class GamePiece : MonoBehaviour {
  public string Kind;public Rigidbody Body;public bool Removed;public float Radius;public Vector3 Previous;public int NetId;
  Collider releasingBumper;
  Collider[] releaseColliders;
  float releaseAt;
  int softBallColliderId;

  internal void RegisterSoftBall(Collider collider)=>softBallColliderId=SoftBallContacts.AddBall(collider);
  void OnDestroy(){if(softBallColliderId!=0)SoftBallContacts.RemoveBall(softBallColliderId);}

  internal void ClearLauncher(Collider bumper){
   if(bumper==null)return;
   releasingBumper=bumper;
   releaseColliders=GetComponentsInChildren<Collider>();
   releaseAt=Time.fixedTime;
   foreach(var shape in releaseColliders)Physics.IgnoreCollision(shape,bumper,true);
  }

  void FixedUpdate(){
   if(releasingBumper==null||Time.fixedTime-releaseAt<.12f)return;
   var bounds=releasingBumper.bounds;
   bounds.Expand(2*(Radius+.04f));
   var center=Body.position;
   if(bounds.Contains(center))return;
   // A muzzle facing back across its own frame can pass through the bumper during release.
   var velocity=Body.linearVelocity;
   if(velocity.sqrMagnitude>.0001f&&bounds.IntersectRay(new Ray(center,velocity.normalized),out var distance)&&distance>=0&&distance<velocity.magnitude*.15f)return;
   foreach(var shape in releaseColliders)Physics.IgnoreCollision(shape,releasingBumper,false);
   releasingBumper=null;releaseColliders=null;
  }
 }
 public sealed class GamePieces {
  public readonly List<GamePiece> Active=new List<GamePiece>();
  int nextNetId;
  readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();readonly Dictionary<string,PhysicsMaterial> physics=new Dictionary<string,PhysicsMaterial>();readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
  internal GamePiece Release(RobotAction action,Transform mount,Collider bumper,ShotPlan plan,System.Random random){
   var direction=ShotPlanning.Scatter(plan.Direction,(float)action.spreadDegrees,random);
   var orientation=StudioApp.ReleaseOrientation(action,mount,direction);
   var piece=Spawn(action.piece,plan.Origin,direction*plan.Speed+plan.Inherited,orientation);
   piece.ClearLauncher(bumper);
   if(action.piece=="coral"){
    var axis=Vector3.Cross(piece.transform.up,mount.up);
    if(axis.sqrMagnitude<.0001f)axis=Vector3.ProjectOnPlane(mount.right,piece.transform.up);
    float spin=(float)action.coralSpin*Mathf.Deg2Rad;
    piece.Body.maxAngularVelocity=Mathf.Max(7,Mathf.Abs(spin));piece.Body.angularVelocity=axis.normalized*spin;
   }
   return piece;
  }
  internal Transform CreateReplayPiece(string kind,Transform parent,int layer){
   var root=new GameObject(PieceCatalog.Names[kind]).transform;
   root.SetParent(parent,false);root.gameObject.layer=layer;
   if(kind=="note"||kind=="coral"){
    if(!meshes.TryGetValue(kind,out var mesh)){
     mesh=kind=="note"?Ring(.1511f,.0267f,48,10,false):Tube(.0508f,.040f,.3016f,32);
     meshes[kind]=mesh;
    }
    root.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
    root.gameObject.AddComponent<MeshRenderer>().sharedMaterial=Appearance(PieceCatalog.Names[kind],kind=="note"?new Color(1,.43f,.08f):new Color(.91f,.91f,.84f));
   }else{
    var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);
    var collider=sphere.GetComponent<Collider>();collider.enabled=false;Lifetime.Destroy(collider);
    sphere.layer=layer;sphere.transform.SetParent(root,false);
    sphere.transform.localScale=Vector3.one*PieceCatalog.Radius(kind)*2;
    sphere.GetComponent<Renderer>().sharedMaterial=Appearance(kind,kind=="algae"?new Color(.25f,.78f,.66f):kind=="fuel"?new Color(.97f,.78f,.16f):new Color(1,.4f,.12f));
   }
   return root;
  }
  Material Appearance(string kind,Color color){if(!materials.TryGetValue(kind,out var m)){m=ModelLoader.Material(color);m.enableInstancing=true;materials[kind]=m;}return m;}
  PhysicsMaterial Contact(string kind){if(!physics.TryGetValue(kind,out var m))physics[kind]=m=new PhysicsMaterial("Oyun eşyası "+kind){dynamicFriction=.5f,staticFriction=.6f,bounciness=kind=="coral"?.12f:.35f};return m;}
  public GamePiece Spawn(string kind,Vector3 position,Vector3 velocity,Quaternion rotation){if(Active.Count>=600){Lifetime.Destroy(Active[0].gameObject);Active.RemoveAt(0);}var go=new GameObject(PieceCatalog.Names[kind]);go.transform.SetPositionAndRotation(position,rotation);go.layer=10;var body=go.AddComponent<Rigidbody>();body.mass=kind=="coral"?.25f:kind=="note"?.235f:kind=="algae"?.45f:kind=="fuel"?.22f:.2f;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.interpolation=RigidbodyInterpolation.Interpolate;body.linearDamping=.06f;body.angularDamping=.35f;body.solverIterations=8;body.solverVelocityIterations=4;body.maxDepenetrationVelocity=3;var mat=Contact(kind);var piece=go.AddComponent<GamePiece>();piece.Kind=kind;piece.NetId=++nextNetId;piece.Body=body;piece.Radius=PieceCatalog.Radius(kind);piece.Previous=position;
   if(kind=="note"){if(!meshes.TryGetValue("note",out var mesh))meshes["note"]=mesh=Ring(.1511f,.0267f,48,10,false);Visual(go,mesh,new Color(1,.43f,.08f));for(int i=0;i<16;i++){float angle=i*Mathf.PI*2/16;var point=new GameObject("Halka çarpışması");point.layer=10;point.transform.SetParent(go.transform,false);point.transform.localPosition=new Vector3(Mathf.Cos(angle)*.1511f,0,Mathf.Sin(angle)*.1511f);var c=point.AddComponent<SphereCollider>();c.radius=.027f;c.material=mat;}}
   else if(kind=="coral"){if(!meshes.TryGetValue("coral",out var tube))meshes["coral"]=tube=Tube(.0508f,.040f,.3016f,32);Visual(go,tube,new Color(.91f,.91f,.84f));var c=go.AddComponent<CapsuleCollider>();c.direction=1;c.radius=.0508f;c.height=.3016f;c.material=mat;}
   else{float radius=piece.Radius;var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere.layer=10;sphere.transform.SetParent(go.transform,false);sphere.transform.localScale=Vector3.one*radius*2;var c=sphere.GetComponent<SphereCollider>();c.material=mat;if(kind=="fuel"||kind=="ball")piece.RegisterSoftBall(c);sphere.GetComponent<Renderer>().sharedMaterial=Appearance(kind,kind=="algae"?new Color(.25f,.78f,.66f):kind=="fuel"?new Color(.97f,.78f,.16f):new Color(1,.4f,.12f));}
   body.linearVelocity=velocity;body.angularVelocity=new Vector3(.2f,0,.4f);Active.Add(piece);return piece;}
  void Visual(GameObject parent,Mesh mesh,Color color){var go=new GameObject("Görünüm");go.layer=10;go.transform.SetParent(parent.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Appearance(parent.name,color);}
  public int Capture(string kind,Vector3 center,float radius){int count=0;for(int i=Active.Count-1;i>=0;i--){var p=Active[i];if(p==null){Active.RemoveAt(i);continue;}if(p.Kind==kind&&Vector3.Distance(center,p.transform.position)<=radius){Lifetime.Destroy(p.gameObject);Active.RemoveAt(i);count++;}}return count;}
  public void Remove(GamePiece piece){if(piece==null||piece.Removed)return;piece.Removed=true;Active.Remove(piece);piece.gameObject.SetActive(false);Lifetime.Destroy(piece.gameObject);}
  public void Clear(){foreach(var p in Active)if(p!=null){p.gameObject.SetActive(false);Lifetime.Destroy(p.gameObject);}Active.Clear();}
  public void Dispose(){Clear();foreach(var m in materials.Values)Lifetime.Destroy(m);foreach(var m in physics.Values)Lifetime.Destroy(m);foreach(var m in meshes.Values)Lifetime.Destroy(m);materials.Clear();physics.Clear();meshes.Clear();}
  static Mesh Ring(float radius,float tube,int segments,int sides,bool unused){var v=new Vector3[(segments+1)*(sides+1)];var ix=new List<int>();for(int i=0;i<=segments;i++)for(int j=0;j<=sides;j++){float a=i*Mathf.PI*2/segments,b=j*Mathf.PI*2/sides;v[i*(sides+1)+j]=new Vector3((radius+tube*Mathf.Cos(b))*Mathf.Cos(a),tube*Mathf.Sin(b),(radius+tube*Mathf.Cos(b))*Mathf.Sin(a));if(i<segments&&j<sides){int n=i*(sides+1)+j;ix.AddRange(new[]{n,n+sides+1,n+1,n+1,n+sides+1,n+sides+2});}}return ModelLoader.Mesh(v,ix.ToArray());}
  static Mesh Tube(float outer,float inner,float height,int segments){var v=new List<Vector3>();var ix=new List<int>();for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;foreach(var p in new[]{new Vector2(outer,-height/2),new Vector2(outer,height/2),new Vector2(inner,-height/2),new Vector2(inner,height/2)})v.Add(new Vector3(Mathf.Cos(a)*p.x,p.y,Mathf.Sin(a)*p.x));if(i<segments){int n=i*4;ix.AddRange(new[]{n,n+1,n+4,n+1,n+5,n+4,n+2,n+6,n+3,n+3,n+6,n+7,n+1,n+3,n+5,n+3,n+7,n+5,n,n+4,n+2,n+2,n+4,n+6});}}return ModelLoader.Mesh(v.ToArray(),ix.ToArray());}
 }
}
