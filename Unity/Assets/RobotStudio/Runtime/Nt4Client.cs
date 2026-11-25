using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AshinaStudio {
 public sealed class NtValue { public object Value; public long Received,Timestamp; public string Type; }
 public sealed class NtTopic { public string Name,Type; public JObject Properties; }

 // No Unity calls: socket, clock synchronization and publishing run on worker threads.
 public sealed class Nt4Client : IDisposable {
  sealed class Publication { public int Id; public string Type; public object Value; public long Revision,Sent; public bool Announced; }
  readonly CancellationTokenSource lifetime=new CancellationTokenSource();
  readonly ConcurrentDictionary<int,string> ids=new ConcurrentDictionary<int,string>();
  readonly Dictionary<string,Publication> publications=new Dictionary<string,Publication>();
  readonly object publicationGate=new object();
  public readonly ConcurrentDictionary<string,NtValue> Values=new ConcurrentDictionary<string,NtValue>();
  public readonly ConcurrentDictionary<string,NtTopic> Topics=new ConcurrentDictionary<string,NtTopic>();
  static readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
  static long Now=>clock.ElapsedTicks*1000000/System.Diagnostics.Stopwatch.Frequency;
  ClientWebSocket socket; Task runner; volatile bool connected; bool disposed; int uid=100,generation;
  long offset,lastReceive,lastClock,lastTopicTicks; double bestRtt=double.MaxValue;
  public string Status="Kapalı"; public bool Connected=>connected&&!disposed;
  public bool Reconnecting {get;private set;} public int Generation=>generation;
  public long LastTopicTicks=>Interlocked.Read(ref lastTopicTicks);
  public double RoundTripMs {get;private set;} public string Endpoint {get;private set;}

  public async Task Connect(string host,int port) {
   if(runner!=null)throw new InvalidOperationException("Bu bağlantı zaten başlatıldı.");
   if(string.IsNullOrWhiteSpace(host)||port<1||port>65535)throw new ArgumentException("NT sunucu adresi geçersiz.");
   Endpoint=host+":"+port;
   var first=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
   runner=Task.Run(()=>Run(host,port,first));
   await first.Task.ConfigureAwait(false);
  }
  async Task Run(string host,int port,TaskCompletionSource<bool> first) {
   while(!lifetime.IsCancellationRequested) {
    using(var session=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
    using(var ws=new ClientWebSocket()) {
     socket=ws; connected=false; Status=Reconnecting?"Yeniden bağlanıyor…":"Bağlanıyor…";
     try {
      ws.Options.AddSubProtocol("networktables.first.wpi.edu"); ws.Options.KeepAliveInterval=TimeSpan.FromSeconds(1);
      using(var connect=CancellationTokenSource.CreateLinkedTokenSource(session.Token)) {
       connect.CancelAfter(4000);
       var builder=new UriBuilder("ws",host,port,"nt/AshinaUnityStudio");
       await ws.ConnectAsync(builder.Uri,connect.Token).ConfigureAwait(false);
      }
      ids.Clear(); Values.Clear(); Topics.Clear(); Interlocked.Exchange(ref lastTopicTicks,0);offset=0;bestRtt=double.MaxValue; lastReceive=lastClock=Now;
      lock(publicationGate)foreach(var p in publications.Values){p.Announced=false;p.Sent=-1;}
      var receive=Receive(ws,session.Token);
      // NT4.0 interoperates with the WPILib 2025 and 2026 desktop servers.
      await Send(ws,MsgPack.Encode(new object[]{-1L,0L,2L,Now}),true,session.Token).ConfigureAwait(false);
      await Send(ws,Control("subscribe",new{subuid=1,topics=new[]{"","$"},options=new{prefix=true,periodic=.02,all=false}}),false,session.Token).ConfigureAwait(false);
      connected=true;Reconnecting=false;Interlocked.Increment(ref generation);Status="Bağlı";first.TrySetResult(true);
      var pump=Pump(ws,session.Token);
      await Task.WhenAny(receive,pump).ConfigureAwait(false); session.Cancel(); ws.Abort();
      try{await Task.WhenAll(receive,pump).ConfigureAwait(false);}catch(OperationCanceledException){}
     } catch(Exception e) { if(!lifetime.IsCancellationRequested)Status="Bağlantı bekleniyor: "+e.Message; }
     finally {connected=false;session.Cancel();ws.Abort();first.TrySetResult(false);}
    }
    if(lifetime.IsCancellationRequested)break;
    Reconnecting=true;
    try{await Task.Delay(1000,lifetime.Token).ConfigureAwait(false);}catch(OperationCanceledException){break;}
   }
   Status="Kapalı";
  }
  static byte[] Control(string method,object param)=>Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new[]{new{method,@params=param}}));
  static async Task Send(ClientWebSocket ws,byte[] bytes,bool binary,CancellationToken token) {
   using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)) {
    timeout.CancelAfter(2500);
    await ws.SendAsync(new ArraySegment<byte>(bytes),binary?WebSocketMessageType.Binary:WebSocketMessageType.Text,true,timeout.Token).ConfigureAwait(false);
   }
  }
  // Coalesce latest values per topic. A slow socket cannot create an unbounded send queue.
  public Task Publish(string name,object value,string type="double[]") {
   if(disposed||string.IsNullOrEmpty(name))return Task.CompletedTask;
   lock(publicationGate) {
    if(!publications.TryGetValue(name,out var p)) {
     if(publications.Count>=512){Status="NT yayın konusu sınırı aşıldı.";return Task.CompletedTask;}
     publications[name]=p=new Publication{Id=++uid,Type=type};
    }
    if(p.Type!=type){Status="NT konu türü değiştirilemez: "+name;return Task.CompletedTask;}
    p.Value=value;p.Revision++;
   }
   return Task.CompletedTask;
  }
  async Task Pump(ClientWebSocket ws,CancellationToken token) {
   while(!token.IsCancellationRequested) {
    if(Now-Interlocked.Read(ref lastReceive)>6000000)throw new IOException("NT sunucusu yanıt vermiyor.");
    if(Now-lastClock>=1000000){lastClock=Now;await Send(ws,MsgPack.Encode(new object[]{-1L,0L,2L,Now}),true,token).ConfigureAwait(false);}
    List<KeyValuePair<string,Publication>> batch;
    lock(publicationGate)batch=publications.Where(x=>!x.Value.Announced||x.Value.Sent!=x.Value.Revision).ToList();
    var announces=batch.Where(x=>!x.Value.Announced).Select(x=>new{method="publish",@params=new{name=x.Key,pubuid=x.Value.Id,type=x.Value.Type,properties=new{}}}).ToArray();
    if(announces.Length>0){await Send(ws,Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(announces)),false,token).ConfigureAwait(false);foreach(var entry in batch)entry.Value.Announced=true;}
    var sent=new List<KeyValuePair<Publication,long>>();
    using(var frame=new MemoryStream())foreach(var entry in batch) {
     var p=entry.Value;object value;long revision;
     lock(publicationGate){value=p.Value;revision=p.Revision;}
     byte[] packet=MsgPack.Encode(new object[]{(long)p.Id,Math.Max(1,Now+Interlocked.Read(ref offset)),(long)TypeId(p.Type),value});
     if(frame.Length>0&&frame.Length+packet.Length>16000){await Send(ws,frame.ToArray(),true,token).ConfigureAwait(false);frame.SetLength(0);frame.Position=0;}
     frame.Write(packet,0,packet.Length);sent.Add(new KeyValuePair<Publication,long>(p,revision));
     if(entry.Equals(batch[batch.Count-1]))await Send(ws,frame.ToArray(),true,token).ConfigureAwait(false);
    }
    lock(publicationGate)foreach(var item in sent)item.Key.Sent=item.Value;
    await Task.Delay(15,token).ConfigureAwait(false);
   }
  }
  public static int TypeId(string type) {
   switch(type){case "boolean":return 0;case "double":return 1;case "int":return 2;case "float":return 3;case "string":case "json":return 4;case "boolean[]":return 16;case "double[]":return 17;case "int[]":return 18;case "float[]":return 19;case "string[]":return 20;default:return 5;}
  }
  async Task Receive(ClientWebSocket ws,CancellationToken token) {
   var buffer=new byte[65536];
   while(!token.IsCancellationRequested) {
    using(var ms=new MemoryStream()) {
     WebSocketReceiveResult result;
     do {
      result=await ws.ReceiveAsync(new ArraySegment<byte>(buffer),token).ConfigureAwait(false);
      if(result.MessageType==WebSocketMessageType.Close)throw new IOException("NT sunucusu bağlantıyı kapattı.");
      ms.Write(buffer,0,result.Count);if(ms.Length>16000000)throw new IOException("NT mesaj boyutu sınırı.");
     }while(!result.EndOfMessage);
     Interlocked.Exchange(ref lastReceive,Now);var bytes=ms.ToArray();
     if(result.MessageType==WebSocketMessageType.Text) {
      foreach(var message in JArray.Parse(Encoding.UTF8.GetString(bytes))) {
       var p=message["params"];string method=(string)message["method"];
       if(method=="announce") {
        string name=(string)p["name"],type=(string)p["type"];int id=(int)p["id"];
        if(Topics.Count>=20000&&!Topics.ContainsKey(name))throw new IOException("NT konu sayısı sınırı.");
        ids[id]=name;Topics[name]=new NtTopic{Name=name,Type=type,Properties=p["properties"] as JObject??new JObject()};
       } else if(method=="unannounce"&&ids.TryRemove((int)p["id"],out var name)) {Topics.TryRemove(name,out _);Values.TryRemove(name,out _);}
       else if(method=="properties"&&Topics.TryGetValue((string)p["name"],out var topic)) {
        var properties=(JObject)topic.Properties.DeepClone();if(p["update"] is JObject updates)foreach(var update in updates.Properties()){if(update.Value.Type==JTokenType.Null)properties.Remove(update.Name);else properties[update.Name]=update.Value.DeepClone();}
        Topics[topic.Name]=new NtTopic{Name=topic.Name,Type=topic.Type,Properties=properties};
       }
      }
     } else {
      int at=0;
      while(at<bytes.Length) {
       var packet=MsgPack.Read(bytes,ref at) as object[];
       if(packet==null||packet.Length!=4)throw new IOException("NT değer zarfı geçersiz.");
       int id=Convert.ToInt32(packet[0]);
       if(id==-1){double rtt=Now-Convert.ToInt64(packet[3]);if(rtt>=0&&rtt<bestRtt){bestRtt=rtt;RoundTripMs=rtt/1000;Interlocked.Exchange(ref offset,Convert.ToInt64(packet[1])+(long)(rtt/2)-Now);}}
       else if(ids.TryGetValue(id,out var name)&&Topics.TryGetValue(name,out var topic)){
        long receipt=DateTime.UtcNow.Ticks;
        Values[name]=new NtValue{Value=packet[3],Received=receipt,Timestamp=Convert.ToInt64(packet[1]),Type=topic.Type};
        if(!name.StartsWith("$",StringComparison.Ordinal))Interlocked.Exchange(ref lastTopicTicks,receipt);
       }
      }
     }
    }
   }
  }
  public NtValue Get(string name)=>!string.IsNullOrEmpty(name)&&Values.TryGetValue(name,out var value)?value:null;
  public void Dispose(){if(disposed)return;disposed=true;connected=false;lifetime.Cancel();socket?.Abort();Status="Kapalı";}
 }
}
