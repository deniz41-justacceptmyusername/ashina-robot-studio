using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace AshinaStudio {
 public sealed class RobotCodeChecks : MonoBehaviour {
  public StudioApp App;public string Output;readonly List<string> passed=new List<string>();Gamepad pad;
  void Check(bool condition,string name){if(!condition)throw new Exception(name);passed.Add(name);Debug.Log("PASS ROBOT: "+name);}
  async Task Wait(Func<bool> condition,string name,int timeout=15000){var start=DateTime.UtcNow;while(!condition()){if((DateTime.UtcNow-start).TotalMilliseconds>timeout)throw new Exception("Timeout "+name+"\n"+App.Notice+"\n"+App.RobotCodeLog);await Task.Delay(40);}Check(true,name);}
  void Input(GamepadState state){InputSystem.QueueStateEvent(pad,state);InputSystem.Update();DriverInput.Invalidate();}
  double Scalar(string topic)=>NtData.Scalar(App.Nt,App.Nt.Get(topic),"",out var value)?value:0;
  async void Start() {
   Directory.CreateDirectory(Output);
   try {
    Check(App.Project.assets.Count==1&&App.Nodes["chassis"].GetComponentsInChildren<Renderer>().Length>=50,"Harun gerçek CAD modeli Windows player'a yükleniyor");
    Check(App.Field.Season=="2026"&&App.Field.Layout=="andymark"&&App.Field.Tags.Count==32,"Robot koduyla aynı 2026 AndyMark AprilTag yerleşimi");
    App.StartRobotCode();
    await Wait(()=>App.Nt?.Connected==true&&App.Nt.Get("/SmartDashboard/Ashina/Drive/truthPose")!=null,"Uygulama gerçek YAGSL projesini derleyip başlatıyor ve NT'ye bağlanıyor",150000);
    pad=InputSystem.AddDevice<Gamepad>();DriverInput.TestPad=pad;DriverInput.Invalidate();App.SetRunning(true);
    await Wait(()=>App.Nt.Get("/AshinaRobot/Bridge/ready")?.Value is bool ready&&ready,"Unity gamepad verisi gerçek robot köprüsüne ulaşıyor");
    var before=App.Pose;Input(new GamepadState{leftStick=new Vector2(0,.4f)});await Task.Delay(1700);var after=App.Pose;
    Check(Math.Sqrt(Math.Pow(after[0]-before[0],2)+Math.Pow(after[1]-before[1],2))>.1,"Aynı Xbox sürüş kodu YAGSL fiziğini sürüyor ve Unity robotu takip ediyor");
    Input(new GamepadState().WithButton(GamepadButton.DpadDown));await Task.Delay(450);Input(new GamepadState().WithButton(GamepadButton.LeftShoulder));
    await Wait(()=>Scalar("/SmartDashboard/Ashina/Intake/isUp")==0&&Scalar("/SmartDashboard/Ashina/Intake/rollerOutput")<-.1,"Gerçek intake komutları NT telemetrisine ulaşıyor");
    var intake=App.Project.actions.First(a=>a.type=="capture");var mount=App.Nodes[intake.partId].content;Vector3 center=mount.TransformPoint(Frc.Position(intake.spawn));int collected=App.FuelCollected;
    for(int i=0;i<10;i++)App.Pieces.Spawn("fuel",center+Vector3.right*(i%3)*.03f,Vector3.zero,Quaternion.identity);
    await Wait(()=>App.FuelCollected>collected,"Robot kodu intake'i çalıştırınca fiziksel temas eden FUEL stoklanıyor");
    Input(new GamepadState());App.SetRunning(false);await Task.Delay(350);App.Project.initialPose=new[]{2.0,4.0213534,0};App.RequestRobotReset();
    await Wait(()=>Math.Abs(App.Pose[0]-2)<.15&&Math.Abs(App.Pose[1]-4.0213534)<.15,"Uygulamadan reset isteği YAGSL odometri / fizik konumunu sıfırlıyor");
    App.SetRunning(true);Input(new GamepadState().WithButton(GamepadButton.RightShoulder));int goals=App.BlueFuel;
    await Wait(()=>Scalar("/SmartDashboard/Ashina/Shooter/stopperOutput")>.1,"Shooter hız geri bildirimi gerçek atış kodundaki hazır koşulunu sağlıyor");
    await Wait(()=>App.FuelShots>0&&App.LastShotSpeed>5,"Robot kodundaki atış hızı fiziksel FUEL çıkış hızını belirliyor");
    await Wait(()=>App.BlueFuel>goals,"Gerçek robot atış koduyla çıkan FUEL fizik kurallarıyla HUB'a giriyor",9000);
    await Wait(()=>App.Nt.Get("/SmartDashboard/Ashina/Vision/estimatedPose")!=null,"Unity AprilTag sonucu mevcut PhotonVision poz kestirim kodunda işleniyor");
    Check(App.Nt.Get("/SmartDashboard/Ashina/Drive/odometryPose")!=null,"Gerçek odometri sonucu ayrıca izlenebiliyor");
    App.SetRunning(false);Input(new GamepadState());
    File.WriteAllText(Path.Combine(Output,"result.json"),JsonConvert.SerializeObject(new{success=true,count=passed.Count,passed,pose=App.Pose,shots=App.FuelShots,goals=App.BlueFuel,collected=App.FuelCollected,topics=App.Nt.Topics.Count},Formatting.Indented));
    Application.Quit(0);
   }catch(Exception error){Debug.LogException(error);File.WriteAllText(Path.Combine(Output,"result.json"),JsonConvert.SerializeObject(new{success=false,count=passed.Count,passed,error=error.ToString(),robotLog=App.RobotCodeLog},Formatting.Indented));Application.Quit(2);}
   finally{DriverInput.TestPad=null;if(pad!=null)InputSystem.RemoveDevice(pad);DriverInput.Invalidate();}
  }
 }
}
