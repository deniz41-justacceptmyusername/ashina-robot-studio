using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace AshinaStudio {
 public partial class StudioApp {
  TelemetryLayout telemetryLayout;
  readonly TelemetryWorkspace telemetry=new TelemetryWorkspace();
  readonly Dictionary<string,TelemetryPlot> telemetryPlots=new Dictionary<string,TelemetryPlot>();
  bool telemetryVisible,telemetryPaused,telemetryKeyHandled;
  int telemetryCameraMask;
  double telemetryPlayhead,nextTelemetryPlots,nextTelemetryTopics,telemetrySaveAt;
  Vector2 telemetryScroll;
  string telemetryPicker,telemetryFilter="",telemetryError="";
  NtTopic[] discoveredTelemetry=Array.Empty<NtTopic>();
  string TelemetryFile=>Path.Combine(Application.persistentDataPath,"telemetry-v1.json");

  internal void OpenTelemetry(){
   if(telemetryLayout==null){
    try{telemetryLayout=TelemetryLayout.Load(TelemetryFile);}
    catch(Exception error){telemetryLayout=new TelemetryLayout();telemetryError="Düzen yüklenemedi · "+error.Message;}
    telemetry.Configure(telemetryLayout);
   }
   if(!telemetryVisible&&ViewCamera!=null){
    telemetryCameraMask=ViewCamera.cullingMask;
    ViewCamera.cullingMask=0;ViewCamera.enabled=true;
   }
   telemetryVisible=true;telemetryPicker=null;nextTelemetryTopics=0;nextTelemetryPlots=0;telemetryScroll=Vector2.zero;
   GUI.FocusControl(null);
  }

  internal void CloseTelemetry(){
   if(telemetryVisible&&ViewCamera!=null)ViewCamera.cullingMask=telemetryCameraMask;
   telemetryVisible=false;telemetryPicker=null;
   foreach(var graph in telemetryPlots.Values)graph.Dispose();
   telemetryPlots.Clear();
   SaveTelemetry();
   GUI.FocusControl(null);
  }

  void SaveTelemetry(){
   if(telemetrySaveAt<=0)return;
   telemetrySaveAt=0;
   try{telemetryLayout.Save(TelemetryFile);telemetryError="";}
   catch(Exception error){telemetryError="Düzen kaydedilemedi · "+error.Message;}
  }

  void TelemetryLayoutChanged(bool refreshWatches=false){
   if(refreshWatches)telemetry.Configure(telemetryLayout);
   telemetrySaveAt=Time.realtimeSinceStartupAsDouble+.6;
   nextTelemetryPlots=0;
  }

  void UpdateTelemetry(){
   telemetryKeyHandled=false;
   if(telemetryLayout==null)return;
   double clock=Time.realtimeSinceStartupAsDouble;
   telemetry.Tick(Nt,clock,DateTime.UtcNow.Ticks);
   if(telemetrySaveAt>0&&clock>=telemetrySaveAt)SaveTelemetry();
   if(!telemetryVisible)return;
   if(!RoomActive&&Input.GetKeyDown(KeyCode.Escape)&&!Typing){telemetryKeyHandled=true;CloseTelemetry();return;}
   if(telemetryPicker!=null&&clock>=nextTelemetryTopics){
    nextTelemetryTopics=clock+.6;
    RefreshTelemetryTopics();
   }
   if(clock<nextTelemetryPlots)return;
   nextTelemetryPlots=clock+.14;
   double end=telemetryPaused?telemetryPlayhead:telemetry.Now;
   foreach(var card in telemetryLayout.panels)if(card.kind=="plot"&&card.series.Count>0){
    if(!telemetryPlots.TryGetValue(card.id,out var plot))telemetryPlots[card.id]=plot=new TelemetryPlot();
    plot.Update(telemetry,card,end-telemetryLayout.seconds,end);
   }
  }

  void RefreshTelemetryTopics(){
   discoveredTelemetry=Nt?.Connected==true?Nt.Topics.Values
    .Where(t=>!t.Name.StartsWith("$",StringComparison.Ordinal)&&t.Name.IndexOf(telemetryFilter,StringComparison.OrdinalIgnoreCase)>=0)
    .OrderBy(t=>t.Name,StringComparer.OrdinalIgnoreCase).Take(25).ToArray():Array.Empty<NtTopic>();
  }

  void DrawTelemetry(){
   var bounds=new Rect(0,0,Screen.width,Screen.height);
   GUI.Box(bounds,"",panel);
   GUILayout.BeginArea(new Rect(24,16,Screen.width-48,Screen.height-28));
   GUILayout.BeginHorizontal();
   GUILayout.Label("TELEMETRİ",title);
   GUILayout.FlexibleSpace();
   if(Button("Çalışma alanına dön"))CloseTelemetry();
   GUILayout.EndHorizontal();
   string endpoint=Nt?.Endpoint??(Project!=null?Project.network.host+":"+Project.network.port:"—");
   var received=Nt?.LastTopicTicks??0;
   bool recent=received>0&&DateTime.UtcNow.Ticks-received<TimeSpan.TicksPerSecond*2;
   string health=Nt?.Connected==true?(recent?"Bağlı · güncel veri geliyor":"Bağlı · son 2 saniyede veri yok"):Nt?.Reconnecting==true?"Yeniden bağlanıyor":"Bağlı değil";
   Text(health+"     ·     "+endpoint);
   Text("Son alınan veri  "+TelemetryTimestamp(received)+"      İzlenen konu  "+telemetry.WatchCount,true);
   if(Nt!=null&&!Nt.Connected)Text(Nt.Status,true);
   if(telemetryError!="")Text(telemetryError,true);

   GUILayout.Space(8);
   GUILayout.BeginHorizontal();
   GUI.enabled=telemetryLayout.panels.Count<TelemetryLayout.MaxPanels;
   if(Button("+ Panel ekle",true)){
    telemetryLayout.panels.Add(new TelemetryPanel{name="Panel "+(telemetryLayout.panels.Count+1)});
    TelemetryLayoutChanged(true);
   }
   GUI.enabled=true;
   GUILayout.FlexibleSpace();
   if(Button(telemetryPaused?"Canlıya dön":"Grafiği duraklat")){
    telemetryPaused=!telemetryPaused;telemetryPlayhead=telemetry.Now;nextTelemetryPlots=0;
   }
   GUILayout.EndHorizontal();
   GUILayout.BeginHorizontal();
   GUILayout.Label("Görünen süre  "+telemetryLayout.seconds.ToString("0")+" sn",muted,GUILayout.Width(155));
   float window=GUILayout.HorizontalSlider(telemetryLayout.seconds,5,180,GUILayout.Width(170));
   if(Math.Abs(window-telemetryLayout.seconds)>.05f){telemetryLayout.seconds=window;TelemetryLayoutChanged();}
   GUILayout.Space(15);
   if(telemetryPaused){
    GUILayout.Label("Zamanda kaydır",muted,GUILayout.Width(115));
    double start=Math.Max(0,telemetry.Now-180);
    float scrub=GUILayout.HorizontalSlider((float)telemetryPlayhead,(float)start,(float)telemetry.Now,GUILayout.Width(180));
    if(Math.Abs(scrub-telemetryPlayhead)>.025){telemetryPlayhead=scrub;nextTelemetryPlots=0;}
   }
   GUILayout.EndHorizontal();
   GUILayout.Space(10);

   telemetryScroll=GUILayout.BeginScrollView(telemetryScroll);
   if(telemetryLayout.panels.Count==0)Text("Bir panel ekle ve izlemek istediğin NetworkTables konularını seç.",true);
   int columns=Screen.width>=1190?2:1;
   float width=(Screen.width-104f-10*(columns-1))/columns;
   TelemetryPanel remove=null;int from=-1,to=-1;
   for(int i=0;i<telemetryLayout.panels.Count;i+=columns){
    GUILayout.BeginHorizontal();
    for(int j=i;j<i+columns&&j<telemetryLayout.panels.Count;j++){
     var card=telemetryLayout.panels[j];
     GUILayout.BeginVertical(panel,GUILayout.Width(width));
     GUILayout.BeginHorizontal();
     GUI.SetNextControlName("telemetry-panel-"+card.id);
     var newName=GUILayout.TextField(card.name,70,input,GUILayout.ExpandWidth(true));
     if(newName!=card.name){card.name=newName;TelemetryLayoutChanged();}
     GUI.enabled=j>0;if(GUILayout.Button("↑",button,GUILayout.Width(31))){from=j;to=j-1;}
     GUI.enabled=j<telemetryLayout.panels.Count-1;if(GUILayout.Button("↓",button,GUILayout.Width(31))){from=j;to=j+1;}
     GUI.enabled=true;if(GUILayout.Button("Sil",button,GUILayout.Width(46)))remove=card;
     GUILayout.EndHorizontal();
     var kind=Cycle("Panel",card.kind,new[]{"plot","value","boolean"},new[]{"Grafik","Değerler","Durumlar"});
     if(kind!=card.kind){card.kind=kind;TelemetryLayoutChanged();}
     DrawTelemetryPanel(card);
     GUILayout.EndVertical();
     GUILayout.Space(8);
    }
    GUILayout.FlexibleSpace();
    GUILayout.EndHorizontal();
    GUILayout.Space(8);
   }
   GUILayout.EndScrollView();
   GUILayout.EndArea();
   if(remove!=null){
    telemetryLayout.panels.Remove(remove);if(telemetryPicker==remove.id)telemetryPicker=null;
    if(telemetryPlots.TryGetValue(remove.id,out var plot)){plot.Dispose();telemetryPlots.Remove(remove.id);}
    TelemetryLayoutChanged(true);
   }else if(from>=0){
    var order=telemetryLayout.panels;
    var item=order[from];order.RemoveAt(from);order.Insert(to,item);TelemetryLayoutChanged();
   }
  }

  void DrawTelemetryPanel(TelemetryPanel card){
   if(card.kind=="plot"){
    if(telemetryPlots.TryGetValue(card.id,out var plot)&&card.series.Count>0){
     var graphRect=GUILayoutUtility.GetRect(1,150,GUILayout.ExpandWidth(true));
     GUI.DrawTexture(graphRect,plot.Texture,ScaleMode.StretchToFill);
     Text(plot.Range+"    ·    "+telemetryLayout.seconds.ToString("0")+" sn",true);
    }
   }
   TelemetrySeries remove=null;
   for(int i=0;i<card.series.Count;i++){
    var series=card.series[i];var state=telemetry.State(series);
    GUILayout.BeginHorizontal();
    var color=GUI.color;
    if(card.kind=="plot")GUI.color=TelemetryPlot.Colors[i];
    GUILayout.Label(series.topic,muted,GUILayout.ExpandWidth(true));
    GUI.color=color;
    if(GUILayout.Button("×",button,GUILayout.Width(32)))remove=series;
    GUILayout.EndHorizontal();
    object raw=state.Current?state.Latest?.Value:null;
    string shown=raw is bool boolean?(boolean?"AÇIK":"KAPALI"):state.Current&&state.Numeric.HasValue?state.Numeric.Value.ToString("G9"):raw is string str?str.Length>70?str.Substring(0,70)+"…":str:"—";
    string status=state.Current?(state.LastSampleAge>TelemetryHistory.StaleSeconds?"Eski veri":"Güncel"):Nt?.Connected==true?(state.Announced?"Değer bekleniyor":"Konu bulunamadı"):"Bağlantı yok";
    if(card.kind=="boolean"&&state.Current&&!(raw is bool))status="Boolean değeri gerekli";
    Text(shown+(string.IsNullOrEmpty(series.unit)?"":" "+series.unit));
    Text((state.Latest?.Type??"—")+"   ·   "+TelemetryTimestamp(state.Latest?.Received??0)+"   ·   "+status,true);
    GUILayout.BeginHorizontal();
    GUILayout.Label("Birim",muted,GUILayout.Width(39));
    GUI.SetNextControlName("telemetry-unit-"+card.id+i);
    string unit=GUILayout.TextField(series.unit,24,input,GUILayout.Width(83));
    GUILayout.Label("Alan",muted,GUILayout.Width(35));
    GUI.SetNextControlName("telemetry-field-"+card.id+i);
    string property=GUILayout.TextField(series.field,160,input,GUILayout.ExpandWidth(true));
    GUILayout.EndHorizontal();
    if(unit!=series.unit||property!=series.field){
     bool changed=property!=series.field;
     series.unit=unit;series.field=property;TelemetryLayoutChanged(changed);
    }
    GUILayout.Space(5);
   }
   if(remove!=null){card.series.Remove(remove);TelemetryLayoutChanged(true);}
   if(card.series.Count==0)Text("Bu panelde konu yok.",true);
   GUILayout.BeginHorizontal();
   GUI.enabled=card.series.Count<8&&telemetryLayout.panels.Sum(p=>p.series.Count)<TelemetryLayout.MaxSeries;
   if(Button(telemetryPicker==card.id?"Konu seçimini kapat":"+ Konu ekle")){
    telemetryPicker=telemetryPicker==card.id?null:card.id;
    telemetryFilter="";nextTelemetryTopics=0;
   }
   GUI.enabled=card.kind=="plot"&&card.series.Count>0;
   if(Button("Geçmişi temizle")){telemetry.Clear(card);nextTelemetryPlots=0;}
   GUI.enabled=true;
   GUILayout.EndHorizontal();
   if(telemetryPicker!=card.id)return;
   GUI.SetNextControlName("telemetry-topic-search");
   var filter=GUILayout.TextField(telemetryFilter,512,input);
   if(filter!=telemetryFilter){telemetryFilter=filter;nextTelemetryTopics=0;}
   if(TelemetryLayout.ValidTopic(telemetryFilter)&&Button("Yazılan konuyu ekle"))AddTelemetrySeries(card,telemetryFilter.Trim());
   foreach(var topic in discoveredTelemetry)if(Button(topic.Name+" · "+topic.Type)){AddTelemetrySeries(card,topic.Name);break;}
  }

  void AddTelemetrySeries(TelemetryPanel panel,string topic){
   if(!TelemetryLayout.ValidTopic(topic)||panel.series.Count>=8||telemetryLayout.panels.Sum(p=>p.series.Count)>=TelemetryLayout.MaxSeries)return;
   if(!panel.series.Any(s=>s.topic==topic&&s.field==""))panel.series.Add(new TelemetrySeries{topic=topic});
   telemetryPicker=null;telemetryFilter="";TelemetryLayoutChanged(true);
  }

  static string TelemetryTimestamp(long ticks)=>ticks<=0?"—":new DateTime(ticks,DateTimeKind.Utc).ToLocalTime().ToString("HH:mm:ss.fff");

  internal bool CheckTelemetryView(TelemetryLayout sample){
   var previous=telemetryLayout;
   telemetryLayout=sample;telemetry.Configure(sample);
   int watched=telemetry.WatchCount;
   bool correct=true;
   for(int i=0;i<8;i++){
    int mask=ViewCamera.cullingMask;
    OpenTelemetry();correct&=telemetryVisible&&ViewCamera.cullingMask==0&&telemetry.WatchCount==watched;
    CloseTelemetry();correct&=!telemetryVisible&&ViewCamera.enabled&&ViewCamera.cullingMask==mask&&telemetry.WatchCount==watched;
   }
   telemetryLayout=previous;telemetry.Configure(previous??new TelemetryLayout());
   return correct;
  }

  async Task PrepareTelemetryCapture(){
   EnterWorkspace();
   Nt=new Nt4Client();
   await Nt.Connect("127.0.0.1",5812);
   telemetryLayout=new TelemetryLayout{seconds=5};
   var graph=new TelemetryPanel{name="Saha konumu"};
   graph.series.Add(new TelemetrySeries{topic="/Feature/Pose",field="[0]",unit="m"});
   graph.series.Add(new TelemetrySeries{topic="/Feature/Pose",field="[1]",unit="m"});
   var readout=new TelemetryPanel{name="Çıkış hızı",kind="value"};
   readout.series.Add(new TelemetrySeries{topic="/Feature/Speed",unit="m/s"});
   var indicator=new TelemetryPanel{name="Atıcı durumu",kind="boolean"};
   indicator.series.Add(new TelemetrySeries{topic="/TelemetryFixture/Ready"});
   telemetryLayout.panels.AddRange(new[]{graph,indicator,readout});
   telemetry.Configure(telemetryLayout);
   OpenTelemetry();
  }
 }
}
