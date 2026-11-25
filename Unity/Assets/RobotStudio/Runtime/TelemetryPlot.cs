using System;
using UnityEngine;

namespace AshinaStudio {
 internal sealed class TelemetryPlot : IDisposable {
  const int Width=520,Height=152;
  readonly Color32[] data=new Color32[Width*Height];
  internal static readonly Color32[] Colors={
   new Color32(74,201,239,255),new Color32(250,166,85,255),new Color32(170,231,121,255),new Color32(235,117,184,255),
   new Color32(206,181,254,255),new Color32(255,228,115,255),new Color32(139,184,240,255),new Color32(139,236,206,255)};
  internal Texture2D Texture {get;private set;}
  internal string Range {get;private set;}="Veri bekleniyor";

  internal void Update(TelemetryWorkspace workspace,TelemetryPanel panel,double left,double right){
   if(Texture==null)Texture=new Texture2D(Width,Height,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear};
   Array.Fill(data,new Color32(12,22,34,255));
   var grid=new Color32(36,54,70,255);
   for(int i=1;i<6;i++)Line((Width-1)*i/6,0,(Width-1)*i/6,Height-1,grid);
   for(int i=1;i<4;i++)Line(0,(Height-1)*i/4,Width-1,(Height-1)*i/4,grid);
   double min=double.PositiveInfinity,max=double.NegativeInfinity;
   foreach(var series in panel.series){
    var history=workspace.State(series);
    for(int i=history.IndexAt(left);i<history.Count;i++){
     var point=history.At(i);
     if(point.Time>right)break;
     min=Math.Min(min,point.Value);max=Math.Max(max,point.Value);
    }
   }
   if(min<=max){
    Range=min==max?min.ToString("G6"):min.ToString("G6")+" … "+max.ToString("G6");
    double scale=Math.Max(1,Math.Max(Math.Abs(min),Math.Abs(max)));
    double bottom=min/scale,extent=max/scale-bottom;
    for(int index=0;index<panel.series.Count;index++){
     var history=workspace.State(panel.series[index]);
     bool previous=false;int x0=0,y0=0,segment=-1;
     var color=Colors[index%Colors.Length];
     for(int i=history.IndexAt(left);i<history.Count;i++){
      var point=history.At(i);if(point.Time>right)break;
      int x=Mathf.Clamp((int)((point.Time-left)/Math.Max(.05,right-left)*(Width-1)),0,Width-1);
      int y=extent<=0?Height/2:Mathf.Clamp((int)((point.Value/scale-bottom)/extent*(Height-13))+6,0,Height-1);
      if(previous&&segment==point.Segment)Line(x0,y0,x,y,color);else data[y*Width+x]=color;
      previous=true;x0=x;y0=y;segment=point.Segment;
     }
    }
   }else Range="Bu aralıkta sayısal veri yok";
   Texture.SetPixels32(data);Texture.Apply(false);
  }

  void Line(int x0,int y0,int x1,int y1,Color32 color){
   int dx=Math.Abs(x1-x0),sx=x0<x1?1:-1,dy=-Math.Abs(y1-y0),sy=y0<y1?1:-1,error=dx+dy;
   while(true){
    data[y0*Width+x0]=color;
    if(x0==x1&&y0==y1)return;
    int twice=2*error;
    if(twice>=dy){error+=dy;x0+=sx;}
    if(twice<=dx){error+=dx;y0+=sy;}
   }
  }
  public void Dispose(){if(Texture!=null)Lifetime.Destroy(Texture);Texture=null;}
 }
}
