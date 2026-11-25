using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AshinaStudio {
 [Serializable] public sealed class LocalAutoPlan {
  public string name;
  public bool resetPose=true,red;
  public float duration;
  public List<LocalAutoLeg> legs=new List<LocalAutoLeg>();
  public List<LocalAutoCue> cues=new List<LocalAutoCue>();
  public Dictionary<string,string> bindings=new Dictionary<string,string>(StringComparer.Ordinal);

  internal void Validate(){
   if(string.IsNullOrWhiteSpace(name)||name.Length>150||!Number(duration,0,120)||legs==null||legs.Count>64||cues==null||cues.Count>500||bindings==null||bindings.Count>500)throw new InvalidDataException("Kayıtlı otonom planı geçersiz.");
   int samples=0;float lastEnd=0;
   foreach(var leg in legs){
    if(leg==null||!Number(leg.start,lastEnd,duration)||!Number(leg.duration,.001f,duration-leg.start+.001f)||leg.points==null||leg.points.Length<2)throw new InvalidDataException("Otonom rota adımı geçersiz.");
    samples+=leg.points.Length;if(samples>12000)throw new InvalidDataException("Otonom rota sınırı aşıldı.");
    float previous=-1;
    foreach(var point in leg.points){
     if(!Number(point.at,0,120)||point.at<=previous||!Number(point.x,-40,40)||!Number(point.y,-40,40)||!Number(point.yaw,-3600,3600)||!Number(point.tangent,-3600,3600))throw new InvalidDataException("Otonom rota noktası geçersiz.");
     previous=point.at;
    }
    if(leg.points[0].at!=0||previous<leg.duration-.001f)throw new InvalidDataException("Otonom rota zamanı geçersiz.");
    lastEnd=leg.start+leg.duration-.001f;
   }
   foreach(var cue in cues)if(cue==null||string.IsNullOrWhiteSpace(cue.name)||cue.name.Length>120||!Number(cue.start,0,duration)||!Number(cue.length,.02f,120))throw new InvalidDataException("Otonom komut işareti geçersiz.");
   foreach(var entry in bindings)if(string.IsNullOrWhiteSpace(entry.Key)||entry.Key.Length>120||entry.Value==null||entry.Value.Length>160)throw new InvalidDataException("Otonom komut eşlemesi geçersiz.");
  }
  internal static bool Number(float value,float min,float max)=>StudioApp.Finite(value)&&value>=min&&value<=max;
 }
 [Serializable] public sealed class LocalAutoLeg {
  public float start,duration;
  public LocalAutoPoint[] points;
 }
 [Serializable] public struct LocalAutoPoint {
  public float at,x,y,yaw,tangent;
 }
 [Serializable] public sealed class LocalAutoCue {
  public string name;
  public float start,length;
 }

 internal sealed class PathPlannerImport {
  readonly string directory;
  int commands,sampleCount;
  const float Pulse=.10f;
  readonly Dictionary<string,JObject> paths=new Dictionary<string,JObject>(StringComparer.OrdinalIgnoreCase);
  PathPlannerImport(string where){directory=where;}

  internal static LocalAutoPlan Read(string file){
   var source=ReadJson(file);
   var importer=new PathPlannerImport(Path.GetDirectoryName(Path.GetFullPath(file)));
   bool single=string.Equals(Path.GetExtension(file),".path",StringComparison.OrdinalIgnoreCase);
   if(!single&&!string.Equals(Path.GetExtension(file),".auto",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("PathPlanner .auto veya .path dosyası seç.");
   if((bool?)source["choreoAuto"]==true)throw new InvalidDataException("Bu .auto bir Choreo rotasına bağlı. PathPlanner .path kullanan bir otonom seç.");
   var plan=single?importer.ReadPath(source):importer.Command(source["command"],true);
   plan.name=Path.GetFileNameWithoutExtension(file);
   plan.resetPose=single||(bool?)source["resetOdom"]==true;
   if(plan.legs.Count==0&&plan.cues.Count==0)throw new InvalidDataException("Dosyada sürülecek rota veya çalıştırılacak komut bulunamadı.");
   plan.Validate();
   return plan;
  }

  static JObject ReadJson(string file){
   if(!File.Exists(file))throw new FileNotFoundException("PathPlanner dosyası bulunamadı: "+Path.GetFileName(file));
   if(new FileInfo(file).Length>2_000_000)throw new InvalidDataException("PathPlanner dosyası 2 MB sınırını aşıyor.");
   using(var text=File.OpenText(file))
   using(var reader=new JsonTextReader(text){MaxDepth=32}){
    var json=JObject.Load(reader);
    string version=(string)json["version"];
    if(string.IsNullOrEmpty(version)||!(version.StartsWith("2025.",StringComparison.Ordinal)||version.StartsWith("2026.",StringComparison.Ordinal)))throw new InvalidDataException("PathPlanner 2025 veya 2026 dosyası gerekir: "+Path.GetFileName(file));
    return json;
   }
  }

  JObject PathNamed(string name){
   if(string.IsNullOrWhiteSpace(name)||name.Length>160||name=="."||name==".."||name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars())>=0)throw new InvalidDataException("PathPlanner rota adı geçersiz.");
   if(paths.TryGetValue(name,out var found))return found;
   if(paths.Count>=64)throw new InvalidDataException("Bir otonomda en fazla 64 farklı rota kullanılabilir.");
   var parent=Directory.GetParent(directory)?.FullName??directory;
   string file=System.IO.Path.Combine(parent,"paths",name+".path");
   if(!File.Exists(file))file=System.IO.Path.Combine(directory,name+".path");
   return paths[name]=ReadJson(file);
  }

  LocalAutoPlan Command(JToken command,bool allowPath,int depth=0){
   if(depth>20||++commands>512||!(command is JObject node))throw new InvalidDataException("Otonom komut yapısı geçersiz.");
   string type=(string)node["type"];
   var data=node["data"] as JObject;
   var result=new LocalAutoPlan();
   switch(type){
    case "wait":
     var seconds=data?["waitTime"];result.duration=Value(seconds is JObject ? seconds["val"] : seconds,0,120);return result;
    case "named":
     string name=(string)data?["name"];
     if(string.IsNullOrWhiteSpace(name)||name.Length>120)throw new InvalidDataException("İsimli PathPlanner komutunun adı eksik.");
     result.cues.Add(new LocalAutoCue{name=name,length=Pulse});result.duration=Pulse;return result;
    case "path":
     if(!allowPath)throw new InvalidDataException("Rota üzerindeki komut işareti başka rota başlatamaz.");
     return ReadPath(PathNamed((string)data?["pathName"]));
    case "sequential":
    case "parallel":
    case "race":
    case "deadline":
     var children=(data?["commands"] as JArray??new JArray()).Select(c=>Command(c,allowPath,depth+1)).ToArray();
     if(type=="sequential"){
      foreach(var child in children){Append(result,child,result.duration,child.duration);result.duration+=child.duration;}
     }else if(children.Length>0){
      if(children.Count(child=>child.legs.Count>0)>1)throw new InvalidDataException("Bir paralel grupta aynı anda bir rota sürülebilir.");
      result.duration=type=="race"?children.Min(child=>child.duration):type=="deadline"?children[0].duration:children.Max(child=>child.duration);
      foreach(var child in children)Append(result,child,0,result.duration);
     }
     return result;
    default:throw new InvalidDataException("Desteklenmeyen PathPlanner komutu: "+(type??"boş"));
   }
  }

  static void Append(LocalAutoPlan into,LocalAutoPlan child,float offset,float limit){
   foreach(var leg in child.legs)if(leg.start<limit)into.legs.Add(new LocalAutoLeg{start=leg.start+offset,duration=Math.Min(leg.duration,limit-leg.start),points=leg.points});
   foreach(var cue in child.cues)if(cue.start<=limit&&(cue.start<limit||child.duration<=limit))into.cues.Add(new LocalAutoCue{name=cue.name,start=cue.start+offset,length=Math.Max(.02f,Math.Min(cue.length,limit-cue.start+Pulse))});
  }

  LocalAutoPlan ReadPath(JObject data){
   var waypoints=data["waypoints"] as JArray;
   if(waypoints==null||waypoints.Count<2||waypoints.Count>100)throw new InvalidDataException("PathPlanner rotası 2–100 geçerli yol noktası içermeli.");
   var settings=data["globalConstraints"];
   float maxV=Value(settings?["maxVelocity"],.05f,30,3);
   float maxA=Value(settings?["maxAcceleration"],.05f,40,3);
   bool reversed=(bool?)data["reversed"]==true;
   var samples=new List<(Vector2 position,float path,float tangent)>();
   for(int i=0;i<waypoints.Count-1;i++){
    var a=Point(waypoints[i]["anchor"]);var d=Point(waypoints[i+1]["anchor"]);
    var b=OptionalPoint(waypoints[i]["nextControl"],Vector2.Lerp(a,d,1f/3));
    var c=OptionalPoint(waypoints[i+1]["prevControl"],Vector2.Lerp(a,d,2f/3));
    int count=Mathf.Clamp(Mathf.CeilToInt((Vector2.Distance(a,b)+Vector2.Distance(b,c)+Vector2.Distance(c,d))/.055f),8,160);
    for(int j=i==0?0:1;j<=count;j++){
     float t=j/(float)count,u=1-t;
     var position=a*(u*u*u)+b*(3*u*u*t)+c*(3*u*t*t)+d*(t*t*t);
     if(samples.Count>0&&Vector2.Distance(samples[samples.Count-1].position,position)<.0001f)continue;
     var direction=(b-a)*(u*u)+(c-b)*(2*u*t)+(d-c)*(t*t);
     if(direction.sqrMagnitude<.0000001f)direction=d-a;
     samples.Add((position,i+t,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg+(reversed?180:0)));
    }
   }
   if(samples.Count<2|| (sampleCount+=samples.Count)>12000)throw new InvalidDataException("PathPlanner rotasının uzunluğu geçersiz.");
   var start=data["idealStartingState"];var end=data["goalEndState"];float lastPath=waypoints.Count-1;
   var rotations=new List<(float path,float yaw)>{(0,Value(start?["rotation"],-3600,3600,samples[0].tangent))};
   foreach(var angle in data["rotationTargets"] as JArray??new JArray())rotations.Add((Value(angle["waypointRelativePos"],0,lastPath),Value(angle["rotationDegrees"],-3600,3600)));
   rotations.Add((lastPath,Value(end?["rotation"],-3600,3600,samples[samples.Count-1].tangent)));
   rotations=rotations.OrderBy(r=>r.path).ToList();
   var limits=new List<(float from,float to,float speed,float acceleration)>();
   foreach(var zone in data["constraintZones"] as JArray??new JArray()){
    float from=Value(zone["minWaypointRelativePos"],0,lastPath),to=Value(zone["maxWaypointRelativePos"],from,lastPath);
    limits.Add((from,to,Value(zone["constraints"]?["maxVelocity"],.05f,30,maxV),Value(zone["constraints"]?["maxAcceleration"],.05f,40,maxA)));
   }
   var dist=new float[samples.Count];var speed=new float[samples.Count];var accel=new float[samples.Count];
   for(int i=0;i<samples.Count;i++){
    speed[i]=maxV;accel[i]=maxA;
    foreach(var limit in limits)if(samples[i].path>=limit.from&&samples[i].path<=limit.to){speed[i]=Math.Min(speed[i],limit.speed);accel[i]=Math.Min(accel[i],limit.acceleration);}
    if(i>0)dist[i]=Vector2.Distance(samples[i-1].position,samples[i].position);
   }
   speed[0]=Math.Min(speed[0],Value(start?["velocity"],0,30,0));
   for(int i=1;i<speed.Length;i++)speed[i]=Math.Min(speed[i],Mathf.Sqrt(speed[i-1]*speed[i-1]+2*dist[i]*Math.Min(accel[i-1],accel[i])));
   int last=speed.Length-1;speed[last]=Math.Min(speed[last],Value(end?["velocity"],0,30,0));
   for(int i=last-1;i>=0;i--)speed[i]=Math.Min(speed[i],Mathf.Sqrt(speed[i+1]*speed[i+1]+2*dist[i+1]*Math.Min(accel[i],accel[i+1])));
   var points=new LocalAutoPoint[samples.Count];float time=0;int rotation=0;
   for(int i=0;i<points.Length;i++){
    if(i>0)time+=2*dist[i]/Math.Max(.01f,speed[i-1]+speed[i]);
    var sample=samples[i];
    while(rotation<rotations.Count-2&&sample.path>rotations[rotation+1].path)rotation++;
    var left=rotations[rotation];var right=rotations[rotation+1];
    float yaw=Mathf.LerpAngle(left.yaw,right.yaw,Mathf.InverseLerp(left.path,right.path,sample.path));
    points[i]=new LocalAutoPoint{at=time,x=sample.position.x,y=sample.position.y,yaw=yaw,tangent=sample.tangent};
   }
   var plan=new LocalAutoPlan{duration=time};
   plan.legs.Add(new LocalAutoLeg{duration=time,points=points});
   foreach(var marker in data["eventMarkers"] as JArray??new JArray()){
    float from=Value(marker["waypointRelativePos"],0,lastPath);
    float begin=MarkerTime(from,samples,points);
    if(marker["command"] is JObject tree)Append(plan,Command(tree,false),begin,time-begin+Pulse);
    else {
     string name=(string)marker["name"];
     if(string.IsNullOrWhiteSpace(name))continue;
     float? finish=(float?)marker["endWaypointRelativePos"];
     float length=finish.HasValue&&finish.Value>=from?Math.Max(Pulse,MarkerTime(Math.Min(finish.Value,lastPath),samples,points)-begin):Pulse;
     plan.cues.Add(new LocalAutoCue{name=name,start=begin,length=length});
    }
   }
   return plan;
  }

  static float MarkerTime(float at,List<(Vector2 position,float path,float tangent)> path,LocalAutoPoint[] points){
   for(int i=1;i<path.Count;i++)if(path[i].path>=at)return Mathf.Lerp(points[i-1].at,points[i].at,Mathf.InverseLerp(path[i-1].path,path[i].path,at));
   return points[points.Length-1].at;
  }
  static Vector2 Point(JToken point)=>new Vector2(Value(point?["x"],-40,40),Value(point?["y"],-40,40));
  static Vector2 OptionalPoint(JToken point,Vector2 fallback)=>point==null||point.Type==JTokenType.Null?fallback:Point(point);
  static float Value(JToken token,float min,float max,float? fallback=null){
   if(token==null||token.Type==JTokenType.Null){if(fallback.HasValue)return fallback.Value;throw new InvalidDataException("PathPlanner dosyasında bir sayı eksik.");}
   if((token.Type!=JTokenType.Float&&token.Type!=JTokenType.Integer)||!LocalAutoPlan.Number((float)token,min,max))throw new InvalidDataException("PathPlanner dosyasındaki sayı geçersiz.");
   return (float)token;
  }
 }
}
