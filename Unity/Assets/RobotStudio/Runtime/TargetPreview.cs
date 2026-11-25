using System.Collections.Generic;
using UnityEngine;

namespace AshinaStudio {
 public partial class StudioApp {
  readonly List<LineRenderer> targetLines=new List<LineRenderer>();Material targetMaterial;
  void PreviewTargets(RobotAction action){
   int used=0;bool visible=action!=null&&action.type=="emit"&&!Running&&!networkPanel&&!homeScreen;
   if(visible&&action.aimMode!="fixed"){
    if(action.aimMode=="zones"){
     foreach(var zone in action.zones){TargetZone(used++,zone,new Color(.2f,.9f,.7f));TargetCross(used++,Frc.Position(zone.target));}
     if(pickingAction==action.id&&pendingZone!=null&&pickingStage>=4)TargetZone(used++,pendingZone,new Color(.4f,.78f,1));
    }
    if(action.aimMode=="point"||action.zoneFallback)TargetCross(used++,Frc.Position(action.target));
   }
   for(int i=used;i<targetLines.Count;i++)if(targetLines[i]!=null)targetLines[i].enabled=false;
  }
  void TargetZone(int index,AimZone zone,Color color){
   double x=zone.center[0],y=zone.center[1],w=zone.size[0]/2,h=zone.size[1]/2;
   TargetLine(index,new[]{Frc.Position(new[]{x-w,y-h,.025}),Frc.Position(new[]{x+w,y-h,.025}),Frc.Position(new[]{x+w,y+h,.025}),Frc.Position(new[]{x-w,y+h,.025}),Frc.Position(new[]{x-w,y-h,.025})},color);
  }
  void TargetCross(int index,Vector3 point){TargetLine(index,new[]{point-Vector3.right*.15f,point+Vector3.right*.15f,point,point-Vector3.forward*.15f,point+Vector3.forward*.15f,point,point-Vector3.up*.15f,point+Vector3.up*.15f},new Color(1,.65f,.25f));}
  void TargetLine(int index,Vector3[] points,Color color){
   if(targetMaterial==null)targetMaterial=new Material(Shader.Find("Sprites/Default"));
   while(targetLines.Count<=index)targetLines.Add(null);
   if(targetLines[index]==null){var go=new GameObject("Atış hedefi önizlemesi");go.layer=31;go.transform.SetParent(Field.Root,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=targetMaterial;line.startWidth=line.endWidth=.015f;targetLines[index]=line;}
   var renderer=targetLines[index];renderer.enabled=true;renderer.startColor=renderer.endColor=color;renderer.positionCount=points.Length;renderer.SetPositions(points);
  }
 }
}
