using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
namespace AshinaStudio {
 public partial class PlayerChecks {
  async Task DemoShotRegression(){
   EmptyStock();Check(!RobotProject.Empty().training.unlimitedShots,"Sınırsız atış yeni projede varsayılan olarak kapalı");
   var emit=new RobotAction{type="emit",partId="chassis",piece="coral",speed=4.5,pitch=35,inheritVelocity=false,spawn=new[]{0.0,0,1.0}};
   App.Project.training.unlimitedShots=true;int shots=App.ShotsFired;GamePiece last=null;
   for(int i=0;i<4;i++)last=App.Emit(emit);
   Check(last!=null&&App.Pieces.Active.Count==4&&App.StoredTotal==0&&App.Project.training.capacity==1,"REEFSCAPE gösteriminde stok sıfırken dört fiziksel coral çıkar; kapasite ve stok değişmez");
   Check(Math.Abs(last.Body.linearVelocity.magnitude-4.5)<.001&&last.Body.linearVelocity.y>2,"Sınırsız coral seçilen hız ve açıyla gerçek fiziğe giriyor");
   Check(App.ShotsFired==shots+4,"Coral dahil bütün atışlar HUD sayacında sayılıyor");
   Check(ProjectIO.Parse(ProjectIO.Write(App.Project)).training.unlimitedShots,"Gösterim tercihi robot dosyasında korunuyor");
   App.Project.inventory["coral"]=1;App.Emit(emit);App.Emit(emit);Check(App.Stored("coral")==1,"Gösterim modunda mevcut stok tüketilmiyor");
   App.Project.training.unlimitedShots=false;Check(App.Emit(emit)!=null&&App.Emit(emit)==null&&App.StoredTotal==0,"Gösterim kapatılınca tek coral bir atışta tükeniyor; sonraki atış duruyor");
   EmptyStock();await CoralRotationRegression();await ReleasePoseRegression();await Task.Yield();
  }
 }
}
