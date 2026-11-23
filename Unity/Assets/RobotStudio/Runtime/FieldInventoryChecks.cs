using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
namespace AshinaStudio {
 public partial class PlayerChecks {
  void EmptyStock(){foreach(var key in App.Project.inventory.Keys.ToArray())App.Project.inventory[key]=0;App.Pieces.Clear();}
  async Task StockRegression(){
   EmptyStock();Check(RobotProject.Empty().inventory.Values.All(n=>n==0),"Yeni robot boş stokla başlıyor");
   Check(App.Project.training.capacity==50,"REBUILT varsayılan robot kapasitesi 50");
   var emit=new RobotAction{type="emit",partId="chassis",piece="fuel",consume=false,spawn=new[]{0.0,0,1.0}};
   App.Project.inventory["fuel"]=2;int shots=App.FuelShots;
   Check(App.Emit(emit)!=null&&App.Emit(emit)!=null&&App.Emit(emit)==null&&App.Stored("fuel")==0&&App.FuelShots==shots+2,"Eski sınırsız atış ayarında bile 2 stok yalnız 2 atış sağlıyor");
   App.SetRunning(true);App.SetRunning(false);Check(App.StoredTotal==0&&App.Emit(emit)==null,"Dur / başlat boş stoğu doldurmuyor; stoksuz atış oluşmuyor");
   App.Pieces.Clear();var capture=new RobotAction{type="capture",partId="chassis",piece="fuel",spawn=new[]{0.0,0,1.0},captureShape="box",captureSize=new[]{1.0,1,1},captureEfficiency=1};
   var center=App.Nodes["chassis"].content.TransformPoint(Frc.Position(capture.spawn));
   for(int i=0;i<55;i++){var p=App.Pieces.Spawn("fuel",center,Vector3.zero,Quaternion.identity);p.Body.isKinematic=true;}
   App.CaptureZone(capture,true);
   Check(App.StoredTotal==50&&App.Pieces.Active.Count==5,"55 FUEL temasında en fazla 50 stoklanıyor; 5 top sahada kalıyor");
   App.Emit(emit);App.CaptureZone(capture,true);Check(App.StoredTotal==50,"Bir atış sonrası boşalan yere yeniden bir top alınabiliyor");EmptyStock();await Task.Yield();
  }
  async Task TerrainRegression(){
   await StockRegression();
   var floor=App.Field.Root.Find("Zemin");Check(!floor.GetComponent<Renderer>().enabled&&floor.position.y+floor.localScale.y/2<-.02f,"Resmi halı tek görünür zemin; yedek zemin altta ve görünmez");
   Physics.SyncTransforms();Check(App.Field.GroundHeight(Frc.Position(new[]{4.58,2.5,0}))>.12f,"Özgün REBUILT rampasının yüksekliği gerçek çarpışma yüzeyinden okunuyor");
   var original=App.Project.initialPose;App.Project.initialPose=new[]{3.15,2.5,0};App.RequestRobotReset();App.SetRunning(true);App.CheckVelocity=Vector3.forward*2;float highest=0;
   for(int i=0;i<150;i++){highest=Mathf.Max(highest,App.Robot.position.y);await Task.Delay(12);}
   Check(App.Pose[0]>5.85&&highest>.07f,$"Robot REBUILT rampasına yükselip diğer tarafa takılmadan geçiyor (x={App.Pose[0]:0.00}, y={App.Robot.position.y:0.00}, high={highest:0.00})");
   App.CheckVelocity=Vector3.zero;await Task.Delay(800);
   Check(App.Robot.position.y>-.05f&&App.Robot.position.y<.06f,"Rampadan inince robot halıya oturuyor ve zeminden düşmüyor");
   App.CheckVelocity=null;App.SetRunning(false);App.Project.initialPose=original;App.RequestRobotReset();
  }
  async Task ReefInventoryRegression(){
   Check(App.Project.training.capacity==1&&App.StoredTotal==0,"REEFSCAPE sezonuna geçince kapasite 1 ve stok boş");
   var capture=new RobotAction{type="capture",partId="chassis",piece="coral",spawn=new[]{0.0,0,1.0},captureShape="box",captureSize=new[]{1.0,1,1},captureEfficiency=1};
   var center=App.Nodes["chassis"].content.TransformPoint(Frc.Position(capture.spawn));
   for(int i=0;i<2;i++){var p=App.Pieces.Spawn("coral",center,Vector3.zero,Quaternion.identity);p.Body.isKinematic=true;}
   App.CaptureZone(capture,true);Check(App.Stored("coral")==1&&App.Pieces.Active.Count==1,"REEFSCAPE'te ikinci coral kapasite dolu olduğundan alınmıyor");
   var emit=new RobotAction{type="emit",partId="chassis",piece="coral",consume=false};
   Check(App.Emit(emit)!=null&&App.Emit(emit)==null&&App.StoredTotal==0,"REEFSCAPE'te tek coral yalnız bir kez çıkarılabiliyor");
   EmptyStock();await DemoShotRegression();await Task.Yield();
  }
 }
}
