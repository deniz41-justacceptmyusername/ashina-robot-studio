using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AshinaStudio {
 public sealed class DriverPad {
  public string Name="Kumanda bağlı değil"; public bool Connected;public double[] Axes=new double[6];public long Buttons;public int Pov=-1;
  public bool Button(int zeroBased)=>(Buttons&(1L<<zeroBased))!=0;
 }
 public static class DriverInput {
  internal static Gamepad TestPad;
  static int frame=-1;static readonly DriverPad[] cache=new DriverPad[6];
  public static void Invalidate(){frame=-1;}
  public static DriverPad Read(int index){if(index<0||index>=cache.Length)return new DriverPad();if(frame!=Time.frameCount){frame=Time.frameCount;System.Array.Clear(cache,0,cache.Length);}return cache[index]??(cache[index]=ReadFresh(index));}
  static DriverPad ReadFresh(int index) {
   var result=new DriverPad();
   if(index<Gamepad.all.Count||index==0&&TestPad!=null) {
    var pad=index==0&&TestPad!=null?TestPad:Gamepad.all[index];result.Name=pad.displayName;result.Connected=true;
    var left=pad.leftStick.ReadValue();var right=pad.rightStick.ReadValue();
    result.Axes=new[]{(double)left.x,-left.y,pad.leftTrigger.ReadValue(),pad.rightTrigger.ReadValue(),right.x,-right.y};
    ButtonControl[] buttons={pad.buttonSouth,pad.buttonEast,pad.buttonWest,pad.buttonNorth,pad.leftShoulder,pad.rightShoulder,pad.selectButton,pad.startButton,pad.leftStickButton,pad.rightStickButton};
    for(int i=0;i<buttons.Length;i++)if(buttons[i].isPressed)result.Buttons|=1L<<i;
    var dpad=pad.dpad.ReadValue();if(dpad.sqrMagnitude>.1f)result.Pov=((int)Math.Round(Math.Atan2(dpad.x,dpad.y)*180/Math.PI)+360)%360;
   } else if(index==0&&Gamepad.all.Count==0) {
    var joystick=InputSystem.devices.OfType<Joystick>().FirstOrDefault();
    if(joystick!=null) {
     result.Name=joystick.displayName+" · genel joystick";result.Connected=true;var stick=joystick.stick.ReadValue();result.Axes[0]=stick.x;result.Axes[1]=-stick.y;
     var buttons=joystick.allControls.OfType<ButtonControl>().Where(x=>x.parent==joystick).Take(10).ToArray();
     for(int i=0;i<buttons.Length;i++)if(buttons[i].isPressed)result.Buttons|=1L<<i;
    }
   }
   return result;
  }
 }
 public partial class StudioApp {
  long driverSequence;DriverPad driverPad=new DriverPad();
  void PublishDriverStation() {
   if(Nt?.Connected!=true||!Project.network.forwardGamepad)return;
   for(int index=0;index<2;index++) {
    var pad=DriverInput.Read(index);
    if(index==0) {
     driverPad=pad;
     if(Project.network.keyboardGamepad&&Application.isFocused&&!Typing&&!networkPanel&&!telemetryVisible) {
      pad=new DriverPad{Name="Klavye + "+pad.Name,Connected=true,Axes=(double[])pad.Axes.Clone(),Buttons=pad.Buttons,Pov=pad.Pov};
      pad.Axes[0]=Mathf.Clamp((float)pad.Axes[0]+(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),-1,1);
      pad.Axes[1]=Mathf.Clamp((float)pad.Axes[1]+(Input.GetKey(KeyCode.S)?1:0)-(Input.GetKey(KeyCode.W)?1:0),-1,1);
      pad.Axes[4]=Mathf.Clamp((float)pad.Axes[4]+(Input.GetKey(KeyCode.E)?1:0)-(Input.GetKey(KeyCode.Q)?1:0),-1,1);
     }
    }
    string prefix="/AshinaSim/DriverStation/joystick/"+index;
    _ = Nt.Publish(prefix+"/axes",pad.Axes);
    _ = Nt.Publish(prefix+"/buttons",pad.Buttons,"int");
    _ = Nt.Publish(prefix+"/pov",(long)pad.Pov,"int");
    _ = Nt.Publish(prefix+"/connected",pad.Connected,"boolean");
   }
   _ = Nt.Publish("/AshinaSim/DriverStation/enabled",Running&&!StandaloneAutoRunning,"boolean");
   _ = Nt.Publish("/AshinaSim/DriverStation/autonomous",RobotMode=="autonomous","boolean");
   _ = Nt.Publish("/AshinaSim/DriverStation/heartbeat",++driverSequence,"int");
  }
 }
}
