using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AshinaStudio {
 // Only simulation configuration crosses the room connection. Local code, paths and NT endpoints do not.
 public static class MatchProtocol {
  public const int Version=1,DefaultPort=5820,MaxPlayers=6,MaxFrame=48*1024*1024;
  public static readonly JsonSerializerSettings Json=new JsonSerializerSettings{TypeNameHandling=TypeNameHandling.None,MaxDepth=100,FloatParseHandling=FloatParseHandling.Double};
  public static RobotProject Share(RobotProject source){
   var p=ProjectIO.Parse(ProjectIO.Write(source));p.blocklyProject=null;p.network=new NetworkData();
   if(p.actions.Any(a=>string.IsNullOrWhiteSpace(a.id)||a.id.Length>128)||p.parts.Any(a=>a.id.Length>128)||p.inventory==null||p.inventory.Any(i=>!PieceCatalog.Names.ContainsKey(i.Key)||i.Value<0)||p.inventory.Values.Sum()>p.training.capacity)throw new Exception("Online robot kimliği veya eşya stoğu geçersiz.");
   foreach(var a in p.actions){a.topic="";a.speedTopic="";a.pitchTopic="";a.yawTopic="";}
   foreach(var part in p.parts)part.links.Clear();
   if(JsonConvert.SerializeObject(p,Json).Length>32*1024*1024)throw new Exception("Online robot modeli 32 MB sınırını aşıyor. CAD modelini sadeleştir.");
   return p;
  }
  public static string Encode(object message)=>JsonConvert.SerializeObject(message,Formatting.None,Json);
  public static JObject Decode(string text){using(var sr=new System.IO.StringReader(text))using(var reader=new JsonTextReader(sr){MaxDepth=100})return JObject.Load(reader);}
  public static float Round(float f)=> (float)Math.Round(f,4);
  public static float[] V(Vector3 v)=>new[]{Round(v.x),Round(v.y),Round(v.z)};
  public static float[] Q(Quaternion q)=>new[]{Round(q.x),Round(q.y),Round(q.z),Round(q.w)};
  public static bool Valid(float[] v,int count,float bound){return v!=null&&v.Length==count&&v.All(n=>!float.IsNaN(n)&&!float.IsInfinity(n)&&Math.Abs(n)<=bound);}
  public static Vector3 Vector(float[] v)=>new Vector3(v[0],v[1],v[2]);
  public static Quaternion Rotation(float[] q){var r=new Quaternion(q[0],q[1],q[2],q[3]);return Quaternion.Normalize(r);}
  public static bool ValidRotation(float[] q)=>Valid(q,4,1.01f)&&q.Sum(n=>n*n)>.95f&&q.Sum(n=>n*n)<1.05f;
  public static string FieldSignature(string streaming,string season,string layout){
   if(!new[]{"practice","2024","2025","2026"}.Contains(season)||!new[]{"welded","andymark"}.Contains(layout))throw new Exception("Ortak saha kimliği geçersiz.");
   using(var hash=System.Security.Cryptography.SHA256.Create()){
    var seed=System.Text.Encoding.UTF8.GetBytes("AshinaWorld1/"+season+"/"+layout);hash.TransformBlock(seed,0,seed.Length,seed,0);
    if(season!="practice"){
     string tags=season=="2024"?"2024-crescendo.json":season=="2025"?"2025-reefscape-"+layout+".json":"2026-rebuilt-"+layout+".json";
     foreach(var file in new[]{System.IO.Path.Combine(streaming,"assets/fields",season,"model.glb"),System.IO.Path.Combine(streaming,"assets/fields",season,"collision.bin"),System.IO.Path.Combine(streaming,"assets/tags",tags)}){
      using(var input=System.IO.File.OpenRead(file)){var buffer=new byte[65536];int n;while((n=input.Read(buffer,0,buffer.Length))>0)hash.TransformBlock(buffer,0,n,buffer,0);}
     }
    }
    hash.TransformFinalBlock(new byte[0],0,0);return BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant();
   }
  }
 }
 [Serializable] public sealed class MatchPartState {public string id;public float[] p,q;}
 [Serializable] public sealed class MatchActionInput {public string id;public bool down,code,liveSpeed;public float pitch,yaw,speed;}
 [Serializable] public sealed class MatchInput {
  public string type="input";public long sequence;public double echo;public float[] drive={0,0,0};public MatchPartState[] parts=new MatchPartState[0];public MatchActionInput[] actions=new MatchActionInput[0];
  public bool Validate(RobotProject p){
   if(sequence<0||!MatchProtocol.Valid(drive,3,20)||parts==null||parts.Length!=p.parts.Count||actions==null||actions.Length!=p.actions.Count)return false;
   var ids=new HashSet<string>();foreach(var part in parts)if(part==null||!ids.Add(part.id)||!p.parts.Any(x=>x.id==part.id)||!MatchProtocol.Valid(part.p,3,40)||!MatchProtocol.ValidRotation(part.q))return false;
   ids.Clear();foreach(var a in actions)if(a==null||!ids.Add(a.id)||!p.actions.Any(x=>x.id==a.id)||!StudioApp.Finite(a.pitch)||!StudioApp.Finite(a.yaw)||!StudioApp.Finite(a.speed)||Math.Abs(a.pitch)>36000||Math.Abs(a.yaw)>36000||a.speed<0||a.speed>1000)return false;
   return StudioApp.Finite(echo);
  }
 }
 [Serializable] public sealed class MatchRobotState {
  public string id,name,alliance;public int slot;public bool ready,live;public float[] p,q,v,w;public MatchPartState[] parts;public Dictionary<string,int> stock;public int shots,pickups;
 }
 [Serializable] public sealed class MatchPieceState {public int id;public string kind;public float[] p,v,q;}
 [Serializable] public sealed class MatchSnapshot {
  public string type="state",phase;public long sequence,epoch;public double clock,remaining;public MatchRobotState[] robots;public MatchPieceState[] pieces;public MatchFieldState[] dynamics;public int blue,red;
 }
}
