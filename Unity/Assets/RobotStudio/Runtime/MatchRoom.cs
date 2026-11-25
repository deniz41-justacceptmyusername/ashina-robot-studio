using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AshinaStudio {
 public sealed class MatchRoom : IDisposable {
  sealed class Peer {public MatchLink Link;public string Id;public bool Loading;public double Since=MatchLink.Clock,CloseAt,LastPong;}
  sealed class PieceView {public GamePiece Piece;public Vector3 From,To;public Quaternion RotationFrom,RotationTo;public double At;}
  readonly StudioApp app;readonly List<Peer> peers=new List<Peer>();readonly Dictionary<int,PieceView> pieceViews=new Dictionary<int,PieceView>();readonly HashSet<string> loading=new HashSet<string>();
  MatchFieldSync fieldSync;MatchListener listener;MatchLink server;bool disposed,welcomed,fieldReady;long inputSequence,snapshotSequence,lastSnapshot=-1;double lastSend,lastSnapshotAt,phaseEnd,lastClientClock;int pieceTick;
  MatchInternetListener internetListener;
  CloudRoomTunnel tunnel;
  readonly CancellationTokenSource roomClosed=new CancellationTokenSource();
  public string InternetInvitation=>tunnel?.Running==true&&tunnel.Address!=null?tunnel.Address+"/room/"+Token:"";
  public readonly Dictionary<string,MatchRobot> Robots=new Dictionary<string,MatchRobot>();
  public bool Hosting {get;private set;}public bool LocalOnly {get;private set;}public string LocalId {get;private set;}public string Token {get;private set;}public int Port=>listener?.Port??0;
  public string FieldHash {get;private set;}
  public string Status="Oda hazırlanıyor…",Phase="lobby",Failure="";public long Epoch=1;public double Remaining,RttMs,AutoSeconds,TeleopSeconds,CountdownSeconds=3,GapSeconds=3;
  public MatchSnapshot State {get;private set;}public bool Connected=>!disposed&&(Hosting||server?.Connected==true);public bool Enabled=>Phase=="autonomous"||Phase=="teleop";
  public bool CanReady=>Connected&&fieldReady&&LocalId!=null&&Robots.ContainsKey(LocalId)&&loading.Count==0;
  public bool LocalReady=>LocalId!=null&&Robots.TryGetValue(LocalId,out var robot)&&robot.Ready;
  public bool CanStart=>Hosting&&Phase=="lobby"&&Robots.Count>0&&Robots.Values.All(r=>r.Ready)&&peers.Where(p=>p.CloseAt==0).All(p=>!p.Loading&&p.Id!=null);
  MatchRoom(StudioApp app){this.app=app;}
  public static async Task<MatchRoom> Host(StudioApp app,int port,string name,string alliance,bool localOnly=false,bool spectator=false){
   var room=new MatchRoom(app){Hosting=true,LocalOnly=localOnly,LocalId=spectator?null:Guid.NewGuid().ToString("N"),AutoSeconds=app.Project.season=="2026"?20:15,TeleopSeconds=app.Project.season=="2026"?140:135};
   try{var key=new byte[16];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(key);room.Token=BitConverter.ToString(key).Replace("-","").ToLowerInvariant();string streaming=Application.streamingAssetsPath;room.FieldHash=await Task.Run(()=>MatchProtocol.FieldSignature(streaming,app.Project.season,app.Project.tagLayout));room.listener=new MatchListener(port,localOnly);
    if(!spectator){var actor=await MatchRobot.Create(MatchProtocol.Share(app.Project),room.LocalId,name,alliance,alliance=="red"?3:0,true);actor.Spawn(app.Field.Length);actor.HideVisuals();room.Robots.Add(actor.Id,actor);}room.fieldSync=new MatchFieldSync(app.Field,true);room.fieldSync.Reset();room.fieldReady=true;room.welcomed=true;room.Status="Oda açık · oyuncular bekleniyor";app.ResetRoomWorld();return room;
   }catch{room.Dispose();throw;}
  }
  public static async Task<MatchRoom> Join(StudioApp app,string host,int port,string key,string name,string alliance,Uri internetAddress=null){
   var room=new MatchRoom(app);try{var project=MatchProtocol.Share(app.Project);room.server=internetAddress==null?await MatchLink.Connect(host,port):await MatchLink.Connect(internetAddress);room.server.Send(new{type="join",protocol=MatchProtocol.Version,key,name,alliance,project});room.Status="Robot odaya gönderiliyor…";return room;}catch{room.Dispose();throw;}
  }
  public async Task EnableInternet(string executable){
   if(!Hosting||disposed)throw new InvalidOperationException("İnternet davetini oda sahibi oluşturabilir.");
   if(tunnel?.Running==true)return;
   tunnel?.Dispose();internetListener?.Dispose();
   internetListener=new MatchInternetListener(Token);
   try{
    for(int attempt=0;;attempt++){
     roomClosed.Token.ThrowIfCancellationRequested();
     tunnel=new CloudRoomTunnel(executable,internetListener.Port);
     try{await tunnel.Verify(Token,roomClosed.Token);break;}
     catch(TunnelAddressUnavailable) when(attempt<2){tunnel.Dispose();tunnel=null;}
    }
   }
   catch{tunnel?.Dispose();tunnel=null;internetListener?.Dispose();internetListener=null;throw;}
  }
  void AddIncoming(MatchLink link){if(peers.Count>=8)link.Dispose();else peers.Add(new Peer{Link=link});}
  public void Update(){
   if(disposed)return;
   if(Hosting){while(listener.Take(out var link))AddIncoming(link);if(internetListener!=null)while(internetListener.Take(out var webLink))AddIncoming(webLink);
    foreach(var peer in peers.ToArray()){
     if(!peer.Link.Connected||peer.CloseAt>0&&MatchLink.Clock>=peer.CloseAt||MatchLink.Clock-peer.Since>180&&peer.Id==null||MatchLink.Clock-peer.Link.LastReceived>10){Remove(peer);continue;}
     int budget=16;while(budget-->0&&peer.Link.Take(out var packet))try{HostPacket(peer,packet);}catch(Exception e){Reject(peer,"Oda isteği geçersiz: "+e.Message);break;}
    }
    AdvancePhase();
   }else{
    if(server?.Connected!=true){if(string.IsNullOrEmpty(Failure))Failure=string.IsNullOrEmpty(server?.Error)?"Oda sahibiyle bağlantı kesildi.":server.Error;Status=Failure;app.ApplyRoomEnabled(false,"disabled");return;}
    int budget=24;while(budget-->0&&server.Take(out var packet))try{ClientPacket(packet);}catch(Exception e){Failure="Oda verisi okunamadı: "+e.Message;server.Dispose();app.ApplyRoomEnabled(false,"disabled");return;}
    if(MatchLink.Clock-server.LastReceived>10){Failure="Oda sahibi yanıt vermiyor.";server.Dispose();app.ApplyRoomEnabled(false,"disabled");}
    foreach(var robot in Robots.Values)robot.Render();RenderPieces();
    if(welcomed&&MatchLink.Clock-lastSnapshotAt>.5)app.ApplyRoomEnabled(false,"disabled");
   }
  }
  void HostPacket(Peer peer,JObject packet){
   string type=(string)packet["type"];if(peer.Id==null){if(type!="join"||peer.Loading){Reject(peer,"Önce odaya katılman gerekiyor.");return;}if((int?)packet["protocol"]!=MatchProtocol.Version){Reject(peer,"Uygulama sürümleri uyumlu değil.");return;}
    if((string)packet["key"]!=Token){Reject(peer,"Oda kodu yanlış.");return;}if(Phase!="lobby"){Reject(peer,"Maç sürüyor; oda sahibi lobiye dönünce katılabilirsin.");return;}
    string name=(string)packet["name"],alliance=(string)packet["alliance"];if(string.IsNullOrWhiteSpace(name)||name.Length>40||!new[]{"blue","red"}.Contains(alliance)){Reject(peer,"Oyuncu adı veya ittifak geçersiz.");return;}
    if(Robots.Count+peers.Count(p=>p.Loading)>=6){Reject(peer,"Odada altı robot var.");return;}
    var used=new HashSet<int>(Robots.Values.Select(r=>r.Slot).Concat(peers.Where(p=>p.Loading&&p.Id!=null).Select(p=>int.Parse(p.Id.Split(':').Last()))));int start=alliance=="blue"?0:3;int slot=Enumerable.Range(start,3).Where(i=>!used.Contains(i)).DefaultIfEmpty(-1).First();if(slot<0){Reject(peer,"Bu ittifakta üç robot var; diğer ittifakı seç.");return;}
    var project=ProjectIO.Parse(packet["project"]?.ToString());project=MatchProtocol.Share(project);peer.Loading=true;peer.Id="loading:"+slot;_ = AddPeer(peer,project,name,alliance,slot);return;
   }
   if(peer.Loading||peer.CloseAt>0)return;
   if(type=="input"){var input=packet.ToObject<MatchInput>();if(!input.Validate(Robots[peer.Id].Project)){Reject(peer,"Robot kontrol verisi geçersiz.");return;}Robots[peer.Id].Submit(input);if(MatchLink.Clock-peer.LastPong>.5){peer.LastPong=MatchLink.Clock;peer.Link.Send(new{type="pong",echo=input.echo});}}
   else if(type=="ready"&&Phase=="lobby"){Robots[peer.Id].Ready=(bool?)packet["value"]==true;}
   else if(type=="leave")Remove(peer);
   // Host-only commands are deliberately absent from the peer protocol.
  }
  async Task AddPeer(Peer peer,RobotProject project,string name,string alliance,int slot){
   MatchRobot actor=null;try{
    string id=Guid.NewGuid().ToString("N");actor=await MatchRobot.Create(project,id,name,alliance,slot,true);
    if(disposed||!peers.Contains(peer)||!peer.Link.Connected){actor.Dispose();return;}
    actor.Spawn(app.Field.Length);peer.Id=id;peer.Loading=false;Robots.Add(id,actor);
    peer.Link.Send(new{type="welcome",protocol=MatchProtocol.Version,id,season=app.Project.season,layout=app.Project.tagLayout,fieldHash=FieldHash,epoch=Epoch});
    foreach(var other in Robots.Values)peer.Link.Send(RobotMessage(other));
    foreach(var other in peers.Where(p=>p!=peer&&p.Id!=null&&!p.Loading))other.Link.Send(RobotMessage(actor));Status=Robots.Count+" / 6 robot · hazır olunca maçı başlat";
   }catch(Exception e){actor?.Dispose();if(!disposed)Reject(peer,"Robot hazırlanamadı: "+e.Message);}
  }
  object RobotMessage(MatchRobot actor)=>new{type="robot",id=actor.Id,name=actor.Name,alliance=actor.Alliance,slot=actor.Slot,project=actor.Project};
  void Reject(Peer peer,string reason){if(peer.CloseAt>0)return;peer.Link.Send(new{type="error",message=reason});peer.CloseAt=MatchLink.Clock+.4;}
  void Remove(Peer peer){if(!peers.Remove(peer))return;peer.Link.Dispose();if(peer.Id!=null&&Robots.TryGetValue(peer.Id,out var actor)){Robots.Remove(peer.Id);actor.Dispose();foreach(var other in peers.Where(p=>p.Id!=null&&!p.Loading))other.Link.Send(new{type="left",id=peer.Id});}}
  void ClientPacket(JObject packet){
   switch((string)packet["type"]){
    case "welcome": if(welcomed)throw new Exception("Tekrarlanan oda girişi.");if((int?)packet["protocol"]!=MatchProtocol.Version)throw new Exception("Oda protokolü farklı.");welcomed=true;LocalId=(string)packet["id"];Epoch=(long)packet["epoch"];_ = PrepareField((string)packet["season"],(string)packet["layout"],(string)packet["fieldHash"]);break;
    case "robot": string id=(string)packet["id"];if(Robots.ContainsKey(id)||!loading.Add(id))break;_ = AddReplica(packet,id);break;
    case "left": string left=(string)packet["id"];if(Robots.TryGetValue(left,out var gone)){Robots.Remove(left);gone.Dispose();}loading.Remove(left);break;
    case "state": if(!welcomed||!fieldReady)break;var state=packet.ToObject<MatchSnapshot>();if(state.sequence<=lastSnapshot)break;lastSnapshot=state.sequence;lastSnapshotAt=MatchLink.Clock;lastClientClock=state.clock;State=state;Phase=state.phase;Remaining=state.remaining;
     if(Epoch!=state.epoch){Epoch=state.epoch;ClearPieceViews();app.RoomEpochChanged();}
     foreach(var r in state.robots){if(Robots.TryGetValue(r.id,out var actor))actor.Apply(r);if(r.id==LocalId)app.ApplyRoomRobot(r);}
     fieldSync?.Apply(state.dynamics);if(state.pieces!=null)ApplyPieces(state.pieces);app.ApplyRoomScore(state.blue,state.red);app.ApplyRoomEnabled(Enabled,Phase);Status="Bağlı · "+state.robots.Length+" / 6 robot";break;
    case "pong": double echo=(double?)packet["echo"]??0;RttMs=Math.Max(0,(MatchLink.Clock-echo)*1000);break;
    case "error": Failure=(string)packet["message"]??"Oda bağlantısı reddedildi.";Status=Failure;server.Dispose();app.ApplyRoomEnabled(false,"disabled");break;
   }
  }
  async Task PrepareField(string season,string layout,string expected){try{string streaming=Application.streamingAssetsPath;var signature=await Task.Run(()=>MatchProtocol.FieldSignature(streaming,season,layout));if(signature!=expected)throw new Exception("Saha dosyaları oda sahibiyle aynı değil. Aynı uygulama sürümünü kullanın.");FieldHash=signature;if(app.Field.Season!=season||app.Field.Layout!=layout)await app.LoadField(season,layout);if(disposed)return;if(app.Field.Season!=season||app.Field.Layout!=layout)throw new Exception("Ortak saha yüklenemedi.");app.Pieces.Clear();pieceViews.Clear();fieldSync=new MatchFieldSync(app.Field,false);fieldReady=true;Status="Odaya bağlandı · robotlar hazırlanıyor";app.RoomEpochChanged();}catch(Exception e){Failure=e.Message;server?.Dispose();}}
  async Task AddReplica(JObject packet,string id){MatchRobot actor=null;try{var p=MatchProtocol.Share(ProjectIO.Parse(packet["project"].ToString()));actor=await MatchRobot.Create(p,id,(string)packet["name"],(string)packet["alliance"],(int)packet["slot"],false);if(disposed||!loading.Contains(id)){actor.Dispose();return;}if(id==LocalId)actor.HideVisuals();Robots.Add(id,actor);if(State!=null){var state=State.robots.FirstOrDefault(r=>r.id==id);if(state!=null)actor.Apply(state);}}catch(Exception e){actor?.Dispose();Failure="Robot modeli açılamadı: "+e.Message;server?.Dispose();}finally{loading.Remove(id);}}
  public void FixedTick(){
   if(disposed||!Connected||!fieldReady)return;
   if(LocalId!=null&&MatchLink.Clock-lastSend>=.018){lastSend=MatchLink.Clock;var input=app.CaptureRoomInput(++inputSequence);if(Hosting){if(Robots.TryGetValue(LocalId,out var local))local.Submit(input);}else server.Send(input,true);}
   if(!Hosting)return;
   foreach(var robot in Robots.Values.OrderBy(r=>(r.Slot+(int)(snapshotSequence%6))%6))robot.Tick(app.Pieces,Enabled);
   if(Enabled||Phase=="transition")app.RoomTrainingTick();if(LocalId!=null&&Robots.TryGetValue(LocalId,out var own))app.ApplyRoomRobot(own.Snapshot());
   app.ApplyRoomEnabled(Enabled,Phase);
   if(MatchLink.Clock-lastSnapshotAt>=.048){lastSnapshotAt=MatchLink.Clock;State=BuildSnapshot(++pieceTick%2==0);foreach(var peer in peers.Where(p=>p.Id!=null&&!p.Loading&&p.CloseAt==0))peer.Link.Send(State,true);}
  }
  MatchSnapshot BuildSnapshot(bool pieces){
   return new MatchSnapshot{sequence=++snapshotSequence,epoch=Epoch,clock=MatchLink.Clock,phase=Phase,remaining=Remaining,robots=Robots.Values.Select(r=>r.Snapshot()).ToArray(),blue=app.BlueFuel,red=app.RedFuel,dynamics=fieldSync?.Snapshot(),pieces=pieces?app.Pieces.Active.Where(p=>p!=null&&!p.Removed).Select(p=>new MatchPieceState{id=p.NetId,kind=p.Kind,p=MatchProtocol.V(p.transform.position),v=MatchProtocol.V(p.Body.linearVelocity),q=p.Kind=="coral"||p.Kind=="note"?MatchProtocol.Q(p.transform.rotation):null}).ToArray():null};
  }
  void AdvancePhase(){
   if(Phase=="lobby"||Phase=="finished"){Remaining=0;return;}double now=MatchLink.Clock;Remaining=Math.Max(0,phaseEnd-now);if(now<phaseEnd)return;
   if(Phase=="countdown")SetPhase("autonomous",AutoSeconds);else if(Phase=="autonomous")SetPhase("transition",GapSeconds);else if(Phase=="transition")SetPhase("teleop",TeleopSeconds);else if(Phase=="teleop")SetPhase("finished",0);
  }
  void SetPhase(string phase,double duration){Phase=phase;Remaining=duration;phaseEnd=MatchLink.Clock+duration;foreach(var robot in Robots.Values)robot.ResetControls();app.ApplyRoomEnabled(Enabled,Phase);}
  public bool StartMatch(){if(!CanStart)return false;Epoch++;foreach(var robot in Robots.Values)robot.Spawn(app.Field.Length);fieldSync?.Reset();app.ResetRoomWorld();app.RoomEpochChanged();SetPhase("countdown",CountdownSeconds);return true;}
  public void ReturnLobby(){if(!Hosting)return;Epoch++;SetPhase("lobby",0);foreach(var robot in Robots.Values)robot.Spawn(app.Field.Length);fieldSync?.Reset();app.ResetRoomWorld();app.RoomEpochChanged();}
  public void ConfigureKey(string key){if(!Hosting||Robots.Count>1||peers.Count>0||key==null||key.Length!=32||key.Any(c=>!Uri.IsHexDigit(c)))throw new Exception("Room key must contain 32 hexadecimal characters before players join.");Token=key.ToLowerInvariant();}
  public void SetReady(bool value){if(!CanReady||Phase!="lobby")return;if(Hosting)Robots[LocalId].Ready=value;else{Robots[LocalId].Ready=value;server.Send(new{type="ready",value});}}
  public double EstimatedClock=>Hosting?MatchLink.Clock:lastClientClock+MatchLink.Clock-lastSnapshotAt;
  void ApplyPieces(MatchPieceState[] pieces){
   var ids=new HashSet<int>();foreach(var state in pieces){ids.Add(state.id);if(!pieceViews.TryGetValue(state.id,out var view)){
     var piece=app.Pieces.Spawn(state.kind,MatchProtocol.Vector(state.p),Vector3.zero,state.q==null?Quaternion.identity:MatchProtocol.Rotation(state.q));piece.NetId=state.id;piece.Body.isKinematic=true;piece.Body.interpolation=RigidbodyInterpolation.None;foreach(var c in piece.GetComponentsInChildren<Collider>())c.enabled=false;pieceViews[state.id]=view=new PieceView{Piece=piece};
    }view.From=view.Piece.transform.position;view.To=MatchProtocol.Vector(state.p);view.RotationFrom=view.Piece.transform.rotation;view.RotationTo=state.q==null?Quaternion.identity:MatchProtocol.Rotation(state.q);view.At=MatchLink.Clock;
   }
   foreach(int id in pieceViews.Keys.Where(id=>!ids.Contains(id)).ToArray()){app.Pieces.Remove(pieceViews[id].Piece);pieceViews.Remove(id);}
  }
  void RenderPieces(){foreach(var view in pieceViews.Values){float t=Mathf.Clamp01((float)((MatchLink.Clock-view.At)/.1));view.Piece.transform.SetPositionAndRotation(Vector3.Lerp(view.From,view.To,t),Quaternion.Slerp(view.RotationFrom,view.RotationTo,t));}}
  void ClearPieceViews(){foreach(var view in pieceViews.Values)if(view.Piece!=null)app.Pieces.Remove(view.Piece);pieceViews.Clear();}
  public void Dispose(){if(disposed)return;disposed=true;roomClosed.Cancel();tunnel?.Dispose();internetListener?.Dispose();fieldSync?.Dispose();server?.Dispose();listener?.Dispose();foreach(var peer in peers)peer.Link.Dispose();peers.Clear();foreach(var robot in Robots.Values)robot.Dispose();Robots.Clear();ClearPieceViews();}
 }
}
