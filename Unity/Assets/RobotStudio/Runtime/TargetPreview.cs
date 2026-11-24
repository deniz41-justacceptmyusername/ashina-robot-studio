using System.Collections.Generic;
using UnityEngine;

namespace AshinaStudio {
 public partial class StudioApp {
  readonly List<LineRenderer> targetLines=new List<LineRenderer>();Material targetMaterial;
  void PreviewTargets(RobotAction action){
   int used=0;bool visible=action!=null&&action.type=="emit"&&!Running&&!networkPanel&&!homeScreen;
   if(visible&&action.aimMode!="fixed"){
    if(action.aimMode=="zones")foreach(var zone in action.zones){
     float x=(float)zone.center[0],y=(float)zone.center[1],w=(float)zone.size[0]/2,h=(float)zone.size[1]/2;
     TargetLine(used++,new[]{Frc.Position(new[]{(double)x-w,y-h,.025}),Frc.Position(new[]{(double)x+w,y-h,.025}),Frc.Position(new[]{(double)x+w,y+h,.025}),Frc.Position(new[]{(double)x-w,y+h,.025}),Frc.Position(new[]{(double)x-w,y-h,.025})},new Color(.2f,.9f,.7f));TargetCross(used++,Frc.Position(zone.target));
    }
    if(action.aimMode=="point"||action.zoneFallback)TargetCross(used++,Frc.Position(action.target));
   }
   for(int i=used;i<targetLines.Count;i++)if(targetLines[i]!=null)targetLines[i].enabled=false;
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
