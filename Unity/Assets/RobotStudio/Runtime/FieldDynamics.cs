using System.Linq;
using UnityEngine;

namespace AshinaStudio {
 public sealed class HangingCage : MonoBehaviour {
  public Rigidbody Body;public ConfigurableJoint Suspension;
 }
 public static class FieldDynamics {
  public static void CreateCages(Transform model,Transform field){
   foreach(var source in model.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("CAGE (GE-25231)")).ToArray()){
    var renderers=source.GetComponentsInChildren<MeshRenderer>(true);if(renderers.Length==0)continue;
    var bounds=WorldBounds(renderers[0]);foreach(var r in renderers)bounds.Encapsulate(WorldBounds(r));
    var root=new GameObject("Suspended "+source.name).transform;root.SetParent(field,false);root.position=bounds.center;
    source.SetParent(root,true);
    // Separate narrow members keep the cage hollow. The chain is visual; a joint
    // represents its suspension rather than an expensive chain-link solver.
    foreach(var r in renderers.Where(r=>r.name.Contains("GE-25201-"))){
     var member=new GameObject("Cage member");member.transform.SetParent(root,false);
     var size=WorldBounds(r);member.transform.position=size.center;var box=member.AddComponent<BoxCollider>();box.size=size.size;
    }
    var cage=root.gameObject.AddComponent<HangingCage>();var body=root.gameObject.AddComponent<Rigidbody>();cage.Body=body;
    body.mass=12;body.linearDamping=.15f;body.angularDamping=.35f;body.interpolation=RigidbodyInterpolation.Interpolate;
    body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.solverIterations=16;body.solverVelocityIterations=8;
    var joint=root.gameObject.AddComponent<ConfigurableJoint>();cage.Suspension=joint;
    joint.autoConfigureConnectedAnchor=false;joint.anchor=new Vector3(0,bounds.extents.y,0);joint.connectedAnchor=new Vector3(bounds.center.x,bounds.max.y,bounds.center.z);
    joint.xMotion=joint.yMotion=joint.zMotion=ConfigurableJointMotion.Locked;
    joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=ConfigurableJointMotion.Limited;
    joint.lowAngularXLimit=new SoftJointLimit{limit=-55};joint.highAngularXLimit=new SoftJointLimit{limit=55};
    joint.angularYLimit=new SoftJointLimit{limit=55};joint.angularZLimit=new SoftJointLimit{limit=55};
    joint.projectionMode=JointProjectionMode.PositionAndRotation;joint.projectionDistance=.03f;joint.projectionAngle=5;
   }
  }
  static Bounds WorldBounds(MeshRenderer renderer){
   var mesh=renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
   var bounds=new Bounds(renderer.transform.TransformPoint(mesh.center),Vector3.zero);
   for(int i=0;i<8;i++)bounds.Encapsulate(renderer.transform.TransformPoint(mesh.center+Vector3.Scale(mesh.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
   return bounds;
  }
 }
}
