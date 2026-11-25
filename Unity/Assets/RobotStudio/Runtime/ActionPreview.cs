using System;
using System.Linq;
using UnityEngine;

namespace AshinaStudio {
 public partial class StudioApp {
  Transform actionAnchor;string anchorAction;LineRenderer actionLine;Material actionLineMaterial;
  bool resizingIntake;
  void EditActionAnchor(RobotAction action,bool resize=false) {
   if(actionAnchor==null||anchorAction!=action.id||actionAnchor.parent!=Nodes[action.partId].content){
    ClearActionAnchor(false);
    anchorAction=action.id;
    actionAnchor=new GameObject(action.type=="capture"?"Intake toplama alanı":"Atış çıkış noktası").transform;
    actionAnchor.SetParent(Nodes[action.partId].content,false);
   }
   resizingIntake=resize&&action.type=="capture";
   SyncActionAnchor(action);
   if(resizingIntake)gizmo.SelectResize(actionAnchor,action.captureShape=="sphere");
   else gizmo.Select(actionAnchor,false);
   Notice=resizingIntake?"Renkli küpleri sürükleyerek toplama alanını boyutlandır.":action.type=="capture"?"Renkli okları sürükleyerek toplama alanını taşı.":"Renkli okları sürükleyerek çıkış noktasını taşı.";
  }
  void SyncActionAnchor(RobotAction action){
   if(actionAnchor==null||anchorAction!=action.id||gizmo.Dragging)return;
   actionAnchor.localPosition=Frc.Position(action.spawn);
   actionAnchor.localScale=action.type!="capture"?Vector3.one:action.captureShape=="sphere"?Vector3.one*(float)(2*action.captureRadius):new Vector3((float)action.captureSize[1],(float)action.captureSize[2],(float)action.captureSize[0]);
  }
  bool RecordActionAnchor() {
   if(actionAnchor==null||Running)return false;var action=Project.actions.Find(x=>x.id==anchorAction);if(action==null)return false;
   if(resizingIntake){
    var size=actionAnchor.localScale;
    if(action.captureShape=="sphere")action.captureRadius=Math.Round(Mathf.Clamp(size.y*.5f,.05f,3),4);
    else action.captureSize=new[]{Math.Round(size.z,4),Math.Round(size.x,4),Math.Round(size.y,4)};
   }else{
    action.spawn=Frc.Position(actionAnchor.localPosition).Select(value=>Math.Round(Math.Max(-10,Math.Min(10,value)),4)).ToArray();
   }
   fields.Clear();return true;
  }
  void ClearActionAnchor(bool record) {
   if(actionAnchor==null)return;
   if(record&&gizmo.Dragging)RecordActionAnchor();
   gizmo.Select(null,false);Lifetime.Destroy(actionAnchor.gameObject);actionAnchor=null;anchorAction=null;resizingIntake=false;
  }
  void UpdateActionPreview() {
   if(actionAnchor!=null&&!gizmo.Dragging){var edited=Project.actions.Find(x=>x.id==anchorAction);if(edited==null)ClearActionAnchor(false);else SyncActionAnchor(edited);}
   var action=uiTab==1?Project.actions.Find(x=>x.id==chosenAction):null;PreviewTargets(action);
   if(action==null||action.type=="move"||Running||networkPanel){if(actionLine!=null)actionLine.enabled=false;return;}
   if(actionLine==null){var go=new GameObject("Eylem bölgesi önizlemesi");go.layer=31;actionLine=go.AddComponent<LineRenderer>();actionLine.useWorldSpace=true;actionLineMaterial=new Material(Shader.Find("Sprites/Default"));actionLine.sharedMaterial=actionLineMaterial;}
   actionLine.enabled=true;actionLine.startColor=actionLine.endColor=action.type=="capture"?new Color(.2f,1,.55f):new Color(1,.8f,.2f);
   actionLine.startWidth=actionLine.endWidth=action.type=="capture"?.016f:.009f;
   var mount=Nodes[action.partId].content;Vector3 center=mount.TransformPoint(Frc.Position(action.spawn));Vector3[] points;
   if(action.type=="emit") {
    if(actionAnchor!=null&&anchorAction==action.id)center=actionAnchor.position;
    points=new Vector3[35];if(!PlanShot(action,out var plan,out _,center)){actionLine.enabled=false;return;}center=plan.Origin;Vector3 speed=plan.Direction*plan.Speed+plan.Inherited;
    for(int i=0;i<points.Length;i++){float time=plan.Flight>0?i*plan.Flight/(points.Length-1):i*.04f;points[i]=ShotPlanning.Position(center,speed,time);}
   } else if(action.captureShape=="sphere") {
    bool editing=actionAnchor!=null&&anchorAction==action.id;if(editing)center=actionAnchor.position;
    float radius=editing?actionAnchor.localScale.y*.5f:(float)action.captureRadius;
    points=new Vector3[195];for(int i=0;i<65;i++){float angle=i*Mathf.PI/32,c=Mathf.Cos(angle),s=Mathf.Sin(angle);points[i]=center+mount.rotation*new Vector3(c,0,s)*radius;points[65+i]=center+mount.rotation*new Vector3(c,s,0)*radius;points[130+i]=center+mount.rotation*new Vector3(0,c,s)*radius;}
   } else {
    bool editing=actionAnchor!=null&&anchorAction==action.id;if(editing)center=actionAnchor.position;
    Vector3 size=editing?actionAnchor.localScale:new Vector3((float)action.captureSize[1],(float)action.captureSize[2],(float)action.captureSize[0]);
    int[] order={0,1,3,2,0,4,5,1,5,7,3,7,6,2,6,4};points=new Vector3[order.Length];
    for(int i=0;i<order.Length;i++){int n=order[i];points[i]=center+mount.rotation*Vector3.Scale(size,new Vector3((n&1)==0?-.5f:.5f,(n&2)==0?-.5f:.5f,(n&4)==0?-.5f:.5f));}
   }
   actionLine.positionCount=points.Length;actionLine.SetPositions(points);
  }
 }
}

