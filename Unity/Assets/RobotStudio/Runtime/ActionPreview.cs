using System;
using System.Linq;
using UnityEngine;

namespace AshinaStudio {
 public partial class StudioApp {
  Transform actionAnchor;string anchorAction;LineRenderer actionLine;Material actionLineMaterial;
  void EditActionAnchor(RobotAction action) {
   ClearActionAnchor(true);anchorAction=action.id;actionAnchor=new GameObject("Eylem noktası").transform;actionAnchor.SetParent(Nodes[action.partId].content,false);actionAnchor.localPosition=Frc.Position(action.spawn);gizmo.Select(actionAnchor,false);Notice="Renkli ekseni sürükleyerek toplama / çıkış noktasını taşı.";
  }
  bool RecordActionAnchor() {
   if(actionAnchor==null||Running)return false;var action=Project.actions.Find(x=>x.id==anchorAction);if(action==null)return false;
   action.spawn=Frc.Position(actionAnchor.localPosition);fields.Clear();return true;
  }
  void ClearActionAnchor(bool record) {if(record)RecordActionAnchor();if(actionAnchor!=null)Lifetime.Destroy(actionAnchor.gameObject);actionAnchor=null;anchorAction=null;}
  void UpdateActionPreview() {
   if(actionAnchor!=null&&!gizmo.Dragging){var edited=Project.actions.Find(x=>x.id==anchorAction);if(edited==null){ClearActionAnchor(false);gizmo.Select(Nodes[Selected].content,false);}else actionAnchor.localPosition=Frc.Position(edited.spawn);}
   var action=uiTab==1?Project.actions.Find(x=>x.id==chosenAction):null;PreviewTargets(action);
   if(action==null||action.type=="move"||Running||networkPanel){if(actionLine!=null)actionLine.enabled=false;return;}
   if(actionLine==null){var go=new GameObject("Eylem bölgesi önizlemesi");go.layer=31;actionLine=go.AddComponent<LineRenderer>();actionLine.useWorldSpace=true;actionLine.startWidth=actionLine.endWidth=.009f;actionLineMaterial=new Material(Shader.Find("Sprites/Default"));actionLine.sharedMaterial=actionLineMaterial;}
   actionLine.enabled=true;actionLine.startColor=actionLine.endColor=action.type=="capture"?new Color(.2f,1,.55f):new Color(1,.8f,.2f);
   var mount=Nodes[action.partId].content;Vector3 center=mount.TransformPoint(Frc.Position(action.spawn));Vector3[] points;
   if(action.type=="emit") {
    points=new Vector3[35];if(!PlanShot(action,out var plan,out _)){actionLine.enabled=false;return;}center=plan.Origin;Vector3 speed=plan.Direction*plan.Speed+plan.Inherited;
    for(int i=0;i<points.Length;i++){float time=plan.Flight>0?i*plan.Flight/(points.Length-1):i*.04f;points[i]=ShotPlanning.Position(center,speed,time);}
   } else if(action.captureShape=="sphere") {
    points=new Vector3[65];for(int i=0;i<points.Length;i++){float angle=i*Mathf.PI/32;points[i]=center+mount.rotation*new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(float)action.captureRadius;}
   } else {
    int[] order={0,1,3,2,0,4,5,1,5,7,3,7,6,2,6,4};points=new Vector3[order.Length];
    for(int i=0;i<order.Length;i++){int n=order[i];points[i]=mount.TransformPoint(Frc.Position(new[]{action.spawn[0]+((n&1)==0?-.5:.5)*action.captureSize[0],action.spawn[1]+((n&2)==0?-.5:.5)*action.captureSize[1],action.spawn[2]+((n&4)==0?-.5:.5)*action.captureSize[2]}));}
   }
   actionLine.positionCount=points.Length;actionLine.SetPositions(points);
  }
 }
}

