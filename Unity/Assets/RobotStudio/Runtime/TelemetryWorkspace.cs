using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace AshinaStudio {
 [Serializable] internal sealed class TelemetryLayout {
  public const int MaxPanels=8,MaxSeries=32;
  public int version=1;
  public float seconds=30;
  public List<TelemetryPanel> panels=new List<TelemetryPanel>();

  internal void Validate(){
   if(version!=1||!StudioApp.Finite(seconds)||seconds<5||seconds>180||panels==null||panels.Count>MaxPanels)throw new InvalidDataException("Telemetri düzeni geçersiz.");
   int count=0;
   var ids=new HashSet<string>();
   foreach(var panel in panels){
    if(panel==null||string.IsNullOrWhiteSpace(panel.id)||panel.id.Length>40||!ids.Add(panel.id)||panel.name==null||panel.name.Length>70||!(panel.kind=="plot"||panel.kind=="value"||panel.kind=="boolean")||panel.series==null||panel.series.Count>8)throw new InvalidDataException("Telemetri paneli geçersiz.");
    count+=panel.series.Count;
    if(count>MaxSeries)throw new InvalidDataException("Telemetri konu sınırı aşıldı.");
    foreach(var series in panel.series)if(series==null||!ValidTopic(series.topic)||series.field==null||series.field.Length>160||series.unit==null||series.unit.Length>24)throw new InvalidDataException("Telemetri konusu geçersiz.");
   }
  }
  internal static bool ValidTopic(string topic)=>!string.IsNullOrWhiteSpace(topic)&&topic.Length<=512&&!topic.Contains("\u001f");
  internal static TelemetryLayout Load(string path){
   if(!File.Exists(path))return new TelemetryLayout();
   if(new FileInfo(path).Length>160_000)throw new InvalidDataException("Telemetri düzeni çok büyük.");
   using(var input=File.OpenText(path))
   using(var reader=new JsonTextReader(input){MaxDepth=14}){
    var layout=JsonSerializer.CreateDefault(ProjectIO.Settings).Deserialize<TelemetryLayout>(reader);
    if(layout==null)throw new InvalidDataException("Telemetri düzeni boş.");
    layout.Validate();
    return layout;
   }
  }
  internal void Save(string path){
   Validate();
   Directory.CreateDirectory(Path.GetDirectoryName(path));
   string pending=path+".tmp";
   File.WriteAllText(pending,JsonConvert.SerializeObject(this,Formatting.Indented));
   if(File.Exists(path))File.Replace(pending,path,null);
   else File.Move(pending,path);
  }
 }

 [Serializable] internal sealed class TelemetryPanel {
  public string id=Guid.NewGuid().ToString("N"), name="Yeni panel",kind="plot";
  public List<TelemetrySeries> series=new List<TelemetrySeries>();
 }
 [Serializable] internal sealed class TelemetrySeries {public string topic="",field="",unit="";}

 internal readonly struct TelemetrySample {
  public readonly double Time,Value;
  public readonly int Segment;
  public TelemetrySample(double at,double value,int segment){Time=at;Value=value;Segment=segment;}
 }

 internal sealed class TelemetryHistory {
  internal const int Capacity=4608;
  internal const double StaleSeconds=2;
  readonly TelemetrySample[] samples=new TelemetrySample[Capacity];
  int head,segment;
  NtValue previous;
  internal int Count {get;private set;}
  internal NtValue Latest {get;private set;}
  internal double? Numeric {get;private set;}
  internal bool Announced {get;private set;}
  internal bool Current {get;private set;}
  internal double LastSampleAge {get;private set;}

  internal TelemetrySample At(int index)=>samples[(head-Count+index+Capacity)%Capacity];

  internal int IndexAt(double time){
   int left=0,right=Count;
   while(left<right){int mid=(left+right)/2;if(At(mid).Time<time)left=mid+1;else right=mid;}
   return left;
  }
  internal void Break(){segment++;previous=null;Current=false;Announced=false;}
  internal void Clear(){head=Count=0;segment++;}

  internal void Observe(Nt4Client client,TelemetrySeries series,double time,long nowTicks){
   bool connected=client?.Connected==true;
   Announced=connected&&client.Topics.ContainsKey(series.topic);
   var next=connected?client.Get(series.topic):null;
   if(next==null){if(Current)Break();Announced=connected&&client.Topics.ContainsKey(series.topic);return;}
   Current=true;
   if(!ReferenceEquals(previous,next)){
    previous=Latest=next;
    Numeric=next.Value is bool&&string.IsNullOrEmpty(series.field)?null:
     NtData.Scalar(client,next,series.field,out var number)&&StudioApp.Finite(number)?number:(double?)null;
   }
   LastSampleAge=Math.Max(0,(nowTicks-next.Received)/(double)TimeSpan.TicksPerSecond);
   if(Numeric.HasValue&&LastSampleAge<=StaleSeconds){
    if(Count>0&&time-At(Count-1).Time>StaleSeconds)segment++;
    samples[head]=new TelemetrySample(time,Numeric.Value,segment);
    head=(head+1)%Capacity;if(Count<Capacity)Count++;
   }
  }
 }

 internal sealed class TelemetryWorkspace {
  Dictionary<(string,string),TelemetryHistory> histories=new Dictionary<(string,string),TelemetryHistory>();
  readonly Dictionary<TelemetrySeries,TelemetryHistory> cards=new Dictionary<TelemetrySeries,TelemetryHistory>();
  readonly List<(TelemetrySeries,TelemetryHistory)> watched=new List<(TelemetrySeries,TelemetryHistory)>();
  Nt4Client client;
  int generation=-1;
  bool connected;
  double next;
  internal int WatchCount=>watched.Count;
  internal double Now {get;private set;}
  internal TelemetryHistory State(TelemetrySeries series)=>cards[series];

  internal void Configure(TelemetryLayout layout){
   var nextHistory=new Dictionary<(string,string),TelemetryHistory>();
   cards.Clear();watched.Clear();
   foreach(var panel in layout.panels)foreach(var series in panel.series){
    var key=(series.topic,series.field);
    if(!nextHistory.TryGetValue(key,out var history)){
     if(!histories.TryGetValue(key,out history))history=new TelemetryHistory();
     nextHistory[key]=history;watched.Add((series,history));
    }
    cards[series]=history;
   }
   histories=nextHistory;
  }

  internal void Tick(Nt4Client source,double time,long ticks){
   if(time<next)return;
   next=time+.04;Now=time;
   int latest=source?.Generation??-1;
   bool isConnected=source?.Connected==true;
   if(source!=client||latest!=generation||isConnected!=connected){
    foreach(var entry in watched)entry.Item2.Break();
    client=source;generation=latest;connected=isConnected;
   }
   foreach(var entry in watched)entry.Item2.Observe(source,entry.Item1,time,ticks);
  }
  internal void Clear(TelemetryPanel panel){foreach(var item in panel.series)State(item).Clear();}
 }
}
