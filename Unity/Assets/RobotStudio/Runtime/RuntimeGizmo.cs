using System;
using UnityEngine;
namespace AshinaStudio {
 public class GizmoAxis : MonoBehaviour {public int Axis;}
 public sealed class RuntimeGizmo {
  public Transform Root;public bool Rotate;Transform target;Camera camera;int axis=-1;Plane plane;Vector3 startPoint,startPosition,startDirection;Quaternion startRotation;Action changed;float size=1;
  public RuntimeGizmo(Camera camera,Action changed){this.camera=camera;this.changed=changed;Root=new GameObject("Düzenleme eksenleri").transform;Root.gameObject.SetActive(false);}
  public void Select(Transform transform,bool rotate){target=transform;Rotate=rotate;Rebuild();}
  void Rebuild(){foreach(Transform child in Root)Lifetime.Destroy(child.gameObject);Root.gameObject.SetActive(target!=null);if(target==null)return;for(int i=0;i<3;i++){Color color=i==0?new Color(1,.29f,.32f):i==1?new Color(.4f,.9f,.53f):new Color(.34f,.62f,1);Vector3 dir=Frc.Position(i==0?new[]{1.0,0,0}:i==1?new[]{0.0,1,0}:new[]{0.0,0,1});var go=new GameObject("XYZ"[i].ToString());go.transform.SetParent(Root,false);go.layer=31;go.AddComponent<GizmoAxis>().Axis=i;if(Rotate){var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=81;line.startWidth=line.endWidth=.015f;line.material=new Material(Shader.Find("Sprites/Default")){color=color};line.startColor=line.endColor=color;var a=Vector3.Cross(dir,Mathf.Abs(dir.y)>.9f?Vector3.forward:Vector3.up).normalized;var b=Vector3.Cross(dir,a);for(int n=0;n<=80;n++)line.SetPosition(n,(a*Mathf.Cos(n*Mathf.PI/40)+b*Mathf.Sin(n*Mathf.PI/40))*.65f);for(int n=0;n<24;n++){var point=new GameObject("Halka tutamağı");point.layer=31;point.transform.SetParent(go.transform,false);point.transform.localPosition=(a*Mathf.Cos(n*Mathf.PI/12)+b*Mathf.Sin(n*Mathf.PI/12))*.65f;point.AddComponent<SphereCollider>().radius=.045f;point.AddComponent<GizmoAxis>().Axis=i;}}
   else{var shaft=GameObject.CreatePrimitive(PrimitiveType.Cylinder);shaft.layer=31;shaft.transform.SetParent(go.transform,false);shaft.transform.localPosition=dir*.42f;shaft.transform.localRotation=Quaternion.FromToRotation(Vector3.up,dir);shaft.transform.localScale=new Vector3(.025f,.42f,.025f);shaft.GetComponent<Renderer>().sharedMaterial=ModelLoader.Material(color);shaft.AddComponent<GizmoAxis>().Axis=i;var tip=GameObject.CreatePrimitive(PrimitiveType.Sphere);tip.layer=31;tip.transform.SetParent(go.transform,false);tip.transform.localPosition=dir*.9f;tip.transform.localScale=Vector3.one*.1f;tip.GetComponent<Renderer>().sharedMaterial=shaft.GetComponent<Renderer>().sharedMaterial;tip.AddComponent<GizmoAxis>().Axis=i;}}}
  public int DragStarts {get;private set;}public bool Dragging=>axis>=0;
  public bool Update(bool allowed)
  {
   if(target==null)return false;
   Root.position=target.position;Root.rotation=target.parent?target.parent.rotation:Quaternion.identity;
   size=Mathf.Clamp(Vector3.Distance(camera.transform.position,Root.position)*.13f,.25f,2);Root.localScale=Vector3.one*size;
   return axis>=0;
  }
  // Use queued window events so a quick click/drag between render frames is not lost.
  public void HandleEvent(Event current,bool allowed)
  {
   int control=GUIUtility.GetControlID(FocusType.Passive);
   if(target==null||(!allowed&&axis<0)||current.button!=0)return;
   var mouse=new Vector3(current.mousePosition.x,Screen.height-current.mousePosition.y,0);
   var ray=camera.ScreenPointToRay(mouse);
   if(current.type==EventType.MouseDown&&axis<0)
   {
    Physics.SyncTransforms();
    if(!Physics.Raycast(ray,out var hit,1000,1<<31)||!hit.collider.TryGetComponent<GizmoAxis>(out var handle))return;
    axis=handle.Axis;Vector3 dir=Direction();var normal=Rotate?dir:Vector3.ProjectOnPlane(camera.transform.position-Root.position,dir).normalized;
    plane=new Plane(normal,Root.position);
    if(!plane.Raycast(ray,out float enter)){axis=-1;return;}
    startPoint=ray.GetPoint(enter);startDirection=(startPoint-Root.position).normalized;startPosition=target.position;startRotation=target.rotation;
    DragStarts++;GUI.FocusControl(null);GUIUtility.hotControl=control;current.Use();return;
   }
   if(axis>=0&&(current.type==EventType.MouseDrag||current.type==EventType.MouseUp))
   {
    if(plane.Raycast(ray,out float enter))
    {
     var now=ray.GetPoint(enter);var dir=Direction();
     if(Rotate){float angle=Vector3.SignedAngle(startDirection,(now-Root.position).normalized,dir);if(current.control)angle=Mathf.Round(angle/15)*15;target.rotation=Quaternion.AngleAxis(angle,dir)*startRotation;}
     else{float distance=Vector3.Dot(now-startPoint,dir);if(current.control)distance=Mathf.Round(distance/.05f)*.05f;target.position=startPosition+dir*distance;}
    }
    bool finished=current.type==EventType.MouseUp;current.Use();
    if(finished){axis=-1;GUIUtility.hotControl=0;changed?.Invoke();}
   }
  }
  Vector3 Direction()=>Root.rotation*Frc.Position(axis==0?new[]{1.0,0,0}:axis==1?new[]{0.0,1,0}:new[]{0.0,0,1});
 }
}
