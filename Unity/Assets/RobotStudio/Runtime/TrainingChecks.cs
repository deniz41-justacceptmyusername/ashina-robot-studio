using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace AshinaStudio {
 public partial class PlayerChecks {
  async Task TrainingRegression() {
   Check(App.Field.StagedFuel.Count==456&&App.Field.Root.GetComponentsInChildren<Transform>(true).All(t=>t.name.IndexOf("GE-26900",StringComparison.OrdinalIgnoreCase)<0),"2026 sahasında 456 resmi FUEL konumu var; sabit kopya top geometrisi bulunmuyor");
   App.Pieces.Clear();App.Project.inventory.Keys.ToList().ForEach(k=>App.Project.inventory[k]=0);int capacity=App.Project.training.capacity;App.Project.training.capacity=2;
   var a=new RobotAction{type="capture",partId="chassis",piece="fuel",captureShape="box",captureSize=new[]{1.0,1,1},spawn=new[]{0.0,0,1},captureEfficiency=0};
   Vector3 center=App.Nodes["chassis"].content.TransformPoint(Frc.Position(a.spawn));var fuel=App.Pieces.Spawn("fuel",center,Vector3.zero,Quaternion.identity);fuel.Body.isKinematic=true;
   int trials=App.PickupTrials;App.CaptureZone(a,true);App.CaptureZone(a,true);
   Check(App.Stored("fuel")==0&&App.PickupTrials==trials+1,"%0 toplama başarısız; aynı temasa tekrar olasılık uygulanmıyor");
   fuel.transform.position=center+Vector3.one*2;App.CaptureZone(a,true);fuel.transform.position=center;a.captureEfficiency=1;App.CaptureZone(a,true);
   Check(App.Stored("fuel")==1&&fuel.Removed,"Bölgeden çıkıp yeniden temas eden top tekrar deneniyor ve %100'de alınıyor");
   var f1=App.Pieces.Spawn("fuel",center,Vector3.zero,Quaternion.identity);var f2=App.Pieces.Spawn("fuel",center+Vector3.right*.1f,Vector3.zero,Quaternion.identity);f1.Body.isKinematic=f2.Body.isKinematic=true;App.CaptureZone(a,true);
   Check(App.Stored("fuel")==2&&new[]{f1,f2}.Count(x=>x.Removed)==1,"Robot kapasitesi dolunca ek toplar sahada kalıyor");
   App.Pieces.Clear();App.Project.inventory["fuel"]=0;
   Check(App.Hubs.Count==2&&App.Hubs.All(h=>h.Contains(h.Center,.075f)),"2026 HUB hedefleri resmi AprilTag konumlarından oluşturuluyor");
   var hub=App.Hubs.First(h=>h.Alliance=="blue");int before=App.BlueFuel;
   App.Pieces.Spawn("fuel",hub.Center+Vector3.up*(hub.Height+.5f),Vector3.down,Quaternion.identity);
   await Wait(()=>App.BlueFuel==before+1,"FUEL üst açıklıktan aşağı girince HUB sayacı artıyor",3000);
   var outside=App.Pieces.Spawn("fuel",hub.Center+Vector3.right*.9f+Vector3.up*(hub.Height+.3f),Vector3.down,Quaternion.identity);await Task.Delay(700);
   Check(App.BlueFuel==before+1,"HUB açıklığını kaçıran top gol sayılmıyor");
   Check(Math.Abs(outside.Radius-.075f)<.0001&&Math.Abs(outside.Body.mass-.22f)<.001,"FUEL 15 cm çap ve uygun kütle ile fizik simülasyonuna giriyor");
   App.Pieces.Clear();App.Project.training.capacity=capacity;App.Project.inventory["coral"]=3;
   var pad=InputSystem.AddDevice<Gamepad>();
   try {
    InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=new Vector2(.25f,.5f),rightStick=new Vector2(-.3f,-.6f),leftTrigger=.2f,rightTrigger=.8f}.WithButton(GamepadButton.South).WithButton(GamepadButton.RightShoulder).WithButton(GamepadButton.DpadLeft));InputSystem.Update();DriverInput.Invalidate();
    int index=Gamepad.all.ToList().IndexOf(pad);var input=DriverInput.Read(index);
    Check(Math.Abs(input.Axes[1]+pad.leftStick.ReadValue().y)<.001&&input.Axes[1]<0&&Math.Abs(input.Axes[3]-.8)<.001,"Standart gamepad eksenleri Xbox WPILib sırasına çevriliyor");
    Check(input.Button(0)&&input.Button(5)&&input.Pov==270,"Gamepad A / RB ve yön tuşu Xbox düzeninde aktarılıyor");
   }finally{InputSystem.RemoveDevice(pad);DriverInput.Invalidate();}
  }
 }
}
