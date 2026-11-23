using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AshinaStudio {
 public partial class PlayerChecks {
  async Task SingleRobotRegression(){
   App.SetRunning(false);App.DisconnectNetwork();
   var empty=RobotProject.Empty();empty.season="practice";empty.training.spawnFuel=false;
   Check(empty.parts.Count==1&&empty.parts[0].id=="chassis"&&empty.parts[0].name=="Ana robot","Yeni proje yalnız Ana robot içeriyor");
   Check(await App.LoadProject(empty),"Tek robotla temiz proje açılıyor");
   string obj=Path.Combine(root,"MainRobot.obj");File.WriteAllText(obj,"v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3");
   Check(await App.ImportRobotModel(obj)&&App.Project.parts.Count==1&&App.Project.parts[0].kind=="asset"&&App.Nodes.Count==1,"Robot modeli ana gövdeyi değiştiriyor; ek parça oluşturmuyor");
   var withIntake=ProjectIO.Parse(ProjectIO.Write(App.Project));withIntake.parts.Add(new RobotPart{id="optional-intake",parent="chassis",name="Takımın intake'i"});
   withIntake.actions.Add(new RobotAction{type="capture",partId="optional-intake",piece="fuel",name="Intake çalıştır"});
   await App.LoadProject(withIntake);string previousAsset=App.Project.parts[0].asset;
   Check(await App.ImportRobotModel(obj)&&App.Project.parts.Count==2&&App.Project.actions[0].partId=="optional-intake"&&App.Nodes["optional-intake"].transform.parent==App.Nodes["chassis"].content&&App.Project.parts[0].asset!=previousAsset&&App.Project.assets.Count==1,"Ana robotu değiştirmek ek mekanizmayı ve eylemini koruyor; kullanılmayan modeli temizliyor");
   var original=App.Project;string bad=Path.Combine(root,"BrokenMain.glb");File.WriteAllText(bad,"invalid");
   Check(!await App.ImportRobotModel(bad)&&ReferenceEquals(original,App.Project)&&App.Nodes.Count==2,"Hatalı ana model mevcut robotu ve mekanizmaları bozmuyor");
   var restored=ProjectIO.Parse(ProjectIO.Write(App.Project));
   Check(restored.parts.Count==2&&restored.parts.Count(p=>p.id=="chassis")==1&&restored.actions[0].partId=="optional-intake"&&restored.assets.Count==1,"Tek ana robot ve isteğe bağlı mekanizma proje biçiminde korunuyor");
  }
 }
}
