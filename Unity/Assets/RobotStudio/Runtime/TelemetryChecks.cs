using System;
using System.IO;
using System.Threading.Tasks;

namespace AshinaStudio {
 public sealed partial class FeatureChecks {
  async Task CheckTelemetry(){
   var speed=new TelemetrySeries{topic="/Feature/Speed",unit="m/s"};
   var yaw=new TelemetrySeries{topic="/Feature/Yaw",unit="rad"};
   var ready=new TelemetrySeries{topic="/TelemetryFixture/Ready"};
   var missing=new TelemetrySeries{topic="/TelemetryFixture/Missing"};
   var secondary=new TelemetrySeries{topic=speed.topic};
   var fast=new TelemetrySeries{topic="/TelemetryFixture/Fast"};
   var chart=new TelemetryPanel{name="Hız ve açı"};chart.series.AddRange(new[]{speed,yaw,missing});
   var status=new TelemetryPanel{name="Robot hazır",kind="boolean"};status.series.AddRange(new[]{ready,secondary,fast});
   var layout=new TelemetryLayout{seconds=45};layout.panels.AddRange(new[]{chart,status});
   var workspace=new TelemetryWorkspace();workspace.Configure(layout);
   Check(workspace.WatchCount==5&&ReferenceEquals(workspace.State(speed),workspace.State(secondary)),"Telemetry panels share one logical watch for a topic and field");
   double time=0;
   long ticks=DateTime.UtcNow.Ticks;
   using(var client=new Nt4Client())
   using(var fixturePublisher=new Nt4Client()){
    await client.Connect("127.0.0.1",5812);await fixturePublisher.Connect("127.0.0.1",5812);
    await Wait(()=>client.Connected&&client.Get(speed.topic)!=null&&client.LastTopicTicks>0,"Telemetry reads the connected real WPILib server and its receipt time");
    await fixturePublisher.Publish(ready.topic,true,"boolean");
    await Wait(()=>client.Get(ready.topic)?.Value is bool value&&value,"Boolean panel receives a real NetworkTables boolean topic");
    ticks=DateTime.UtcNow.Ticks;
    workspace.Tick(client,time,ticks);
    Check(workspace.State(speed).Numeric==60&&workspace.State(speed).Latest.Type=="double"&&workspace.State(yaw).Count==1,"Numeric telemetry preserves type, value and timestamp across two graph series");
    Check(workspace.State(ready).Current&&workspace.State(ready).Latest.Value is bool boolean&&boolean,"Boolean cards keep a typed true state");
    Check(!workspace.State(missing).Current&&!workspace.State(missing).Announced,"A missing topic is visible as missing without creating a value");
    using(var plot=new TelemetryPlot()){
     plot.Update(workspace,chart,-45,0);
     Check(plot.Texture!=null&&plot.Range.Contains("60"),"An actual graph texture renders both current numeric series");
     plot.Update(workspace,chart,-55,-10);
     Check(plot.Range.Contains("veri yok"),"A historical viewport before the first sample shows a data gap");
    }
    workspace.Tick(client,2.1,ticks+TimeSpan.TicksPerSecond*3);
    Check(workspace.State(speed).Current&&workspace.State(speed).LastSampleAge>2&&workspace.State(speed).Count==1,"A stale topic stops adding samples while its last value remains identifiable");
    for(int i=0;i<6500;i++){
     time=2.2+i*.041;
     client.Values[fast.topic]=new NtValue{Type="double",Value=(double)i,Received=ticks};
     workspace.Tick(client,time,ticks);
    }
    var history=workspace.State(fast);
    Check(history.Count==TelemetryHistory.Capacity&&history.At(history.Count-1).Value==6499&&history.IndexAt(time-.1)>history.Count-6,"High-rate sampling rolls through a bounded history and supports time seeking");
    workspace.Clear(status);
    Check(history.Count==0&&workspace.State(secondary).Count==0,"Clearing a panel clears its shared histories without keeping hidden samples");
    client.Dispose();workspace.Tick(client,time+.10,ticks);
    Check(!workspace.State(speed).Current,"Disconnection marks existing cards unavailable");
   }
   using(var reconnected=new Nt4Client()){
    await reconnected.Connect("127.0.0.1",5812);
    await Wait(()=>reconnected.Connected&&reconnected.Get(speed.topic)!=null,"Telemetry receives values after reconnecting");
    workspace.Tick(reconnected,time+.20,DateTime.UtcNow.Ticks);
    Check(workspace.State(speed).Current&&workspace.State(speed).Count>0&&workspace.WatchCount==5,"Reconnect restores current samples without duplicating history watches");
   }
   layout.panels.Reverse();
   var file=Path.Combine(Output,"dashboard-test","telemetry-v1.json");
   layout.Save(file);var loaded=TelemetryLayout.Load(file);
   Check(loaded.panels[0].name=="Robot hazır"&&loaded.panels[1].series[0].unit=="m/s"&&loaded.seconds==45,"Dashboard configuration persists renamed panel order, units and history window");
   Check(App.CheckTelemetryView(layout),"Repeatedly opening and closing the telemetry workspace preserves subscriptions and restores the 3D view");
   layout.panels.Remove(chart);workspace.Configure(layout);
   Check(workspace.WatchCount==3,"Removing a panel releases series that no other panel watches");
  }
 }
}
