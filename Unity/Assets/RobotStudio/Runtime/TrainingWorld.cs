using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace AshinaStudio {
 [Serializable] public sealed class TrainingData {
  public bool spawnFuel=true,recycleFuel=true,unlimitedShots;public int fuelCount=456,capacity=50,seed=10576;public string alliance="blue",capacitySeason="";
 }
 public sealed class HubGoal {
  public string Alliance;public Vector3 Center;public float Height=1.8288f,Radius=.5296f;
  public bool Contains(Vector3 point,float pieceRadius) {
   // A regular hexagon inside the nominal 41.7-inch HUB opening.
   var delta=point-Center;float a=Mathf.Abs(delta.x),b=Mathf.Abs(delta.z),r=Radius-pieceRadius;
   return r>0&&a<=r&&.5f*a+.8660254f*b<=r;
  }
 }
 public partial class StudioApp {
  readonly Dictionary<string,HashSet<int>> captureAttempts=new Dictionary<string,HashSet<int>>();
  readonly Dictionary<string,HashSet<int>> captureInside=new Dictionary<string,HashSet<int>>();
  readonly List<KeyValuePair<float,HubGoal>> fuelReturns=new List<KeyValuePair<float,HubGoal>>();
  public readonly List<HubGoal> Hubs=new List<HubGoal>();
  System.Random trainingRandom=new System.Random(10576);
  public int BlueFuel {get;private set;}public int RedFuel {get;private set;}public int FuelShots {get;private set;}public int ShotsFired {get;private set;}public int PickupTrials {get;private set;}public int FuelCollected {get;private set;}
  public int Stored(string kind)=>Project.inventory.TryGetValue(kind,out int value)?value:0;
  public int StoredTotal=>Project.inventory.Values.Sum();
  internal static int DefaultCapacity(string season)=>season=="2026"||season=="practice"?50:1;
  void PrepareInventory(bool changedSeason=false){
   if(Project.training.capacitySeason!=Project.season)Project.training.capacity=DefaultCapacity(Project.season);
   Project.training.capacitySeason=Project.season;int space=Project.training.capacity;
   foreach(var key in Project.inventory.Keys.ToArray()){int count=changedSeason?0:Math.Max(0,Math.Min(space,Project.inventory[key]));Project.inventory[key]=count;space-=count;}
   foreach(var action in Project.actions.Where(a=>a.type=="emit"))action.consume=true;
  }
  void ResetTraining() {
   captureAttempts.Clear();captureInside.Clear();fuelReturns.Clear();Hubs.Clear();BlueFuel=RedFuel=FuelShots=ShotsFired=PickupTrials=FuelCollected=0;trainingRandom=new System.Random(Project.training.seed);
   if(Field.Season!="2026")return;
   foreach(var spec in new[]{new{Alliance="blue",Ids=new[]{18,19,20,21,24,25,26,27}},new{Alliance="red",Ids=new[]{2,3,4,5,8,9,10,11}}}) {
    var tags=Field.Tags.Where(t=>spec.Ids.Contains(t.Id)).ToArray();if(tags.Length==0)continue;
    Hubs.Add(new HubGoal{Alliance=spec.Alliance,Center=new Vector3((tags.Min(t=>t.Position.x)+tags.Max(t=>t.Position.x))*.5f,0,(tags.Min(t=>t.Position.z)+tags.Max(t=>t.Position.z))*.5f)});
   }
   if(Project.training.spawnFuel&&!Environment.GetCommandLineArgs().Contains("--self-test"))PopulateFuel();
  }
  public void PopulateFuel() {
   Pieces.Clear();captureAttempts.Clear();fuelReturns.Clear();
   int count=Math.Min(504,Math.Max(0,Project.training.fuelCount));
   var positions=Field.StagedFuel;
   for(int i=0;i<count;i++) {
    Vector3 position;
    if(i<positions.Count)position=positions[i];
    else {int column=i%24,row=(i/24)%20;position=Frc.Position(new[]{Field.Length/2+(column-11.5)*.165,Field.Width/2+(row-9.5)*.165,.08+(i/480)*.155});}
    Pieces.Spawn("fuel",position,Vector3.zero,Quaternion.identity);
   }
   Notice=count+" FUEL sahaya yerleştirildi.";
  }
  internal int CaptureZone(RobotAction action,bool active) {
   if(!captureAttempts.TryGetValue(action.id,out var tried))captureAttempts[action.id]=tried=new HashSet<int>();
   if(!captureInside.TryGetValue(action.id,out var inside))captureInside[action.id]=inside=new HashSet<int>();inside.Clear();var mount=Nodes[action.partId].content;
   Vector3 center=mount.TransformPoint(Frc.Position(action.spawn));Quaternion inverse=Quaternion.Inverse(mount.rotation);
   Vector3 half=new Vector3((float)action.captureSize[1],(float)action.captureSize[2],(float)action.captureSize[0])*.5f;
   int captured=0;
   for(int pieceIndex=Pieces.Active.Count-1;pieceIndex>=0;pieceIndex--) {
    var piece=Pieces.Active[pieceIndex];
    if(piece==null||piece.Removed||piece.Kind!=action.piece)continue;
    Vector3 local=inverse*(piece.transform.position-center);float radius=piece.Radius;
    bool touching=action.captureShape=="box"?Mathf.Abs(local.x)<=half.x+radius&&Mathf.Abs(local.y)<=half.y+radius&&Mathf.Abs(local.z)<=half.z+radius:local.magnitude<=action.captureRadius+radius;
    if(!touching)continue;int id=piece.GetInstanceID();inside.Add(id);
    if(!active||StoredTotal>=Project.training.capacity||!tried.Add(id))continue;
    PickupTrials++;
    if(trainingRandom.NextDouble()>=action.captureEfficiency)continue;
    Pieces.Remove(piece);Project.inventory[action.piece]=Stored(action.piece)+1;captured++;FuelCollected++;
   }
   tried.IntersectWith(inside);return captured;
  }
  void TrainingTick() {
   if(Hubs.Count==0)return;
   for(int pieceIndex=Pieces.Active.Count-1;pieceIndex>=0;pieceIndex--) {
    var piece=Pieces.Active[pieceIndex];
    if(piece==null||piece.Removed)continue;
    var position=piece.transform.position;
    if(piece.Kind=="fuel")foreach(var hub in Hubs) {
     if(piece.Previous.y>hub.Height&&position.y<=hub.Height&&hub.Contains(position,piece.Radius)) {
      if(hub.Alliance=="blue")BlueFuel++;else RedFuel++;
      Pieces.Remove(piece);if(Project.training.recycleFuel)fuelReturns.Add(new KeyValuePair<float,HubGoal>(Time.time+.5f,hub));break;
     }
    }
    piece.Previous=position;
    if(position.y< -3)Pieces.Remove(piece);
   }
   for(int i=fuelReturns.Count-1;i>=0;i--)if(fuelReturns[i].Key<=Time.time) {
    var hub=fuelReturns[i].Value;float direction=hub.Alliance=="blue"?1:-1;
    var position=hub.Center+Vector3.forward*(direction*.85f)+Vector3.up*.16f;
    Pieces.Spawn("fuel",position,Vector3.forward*(direction*2.2f)+Vector3.right*((float)trainingRandom.NextDouble()-.5f)*1.8f+Vector3.up*.5f,Quaternion.identity);fuelReturns.RemoveAt(i);
   }
  }
  void PublishGameState() {
   if(Nt?.Connected!=true)return;
   _ = Nt.Publish("/AshinaSim/Game/blueFuel",(long)BlueFuel,"int");_ = Nt.Publish("/AshinaSim/Game/redFuel",(long)RedFuel,"int");
   _ = Nt.Publish("/AshinaSim/Game/fuelShots",(long)FuelShots,"int");_ = Nt.Publish("/AshinaSim/Robot/fuelCount",(long)Stored("fuel"),"int");
   foreach(var item in Project.inventory)_ = Nt.Publish("/AshinaSim/Inventory/"+item.Key,(long)item.Value,"int");
  }
  void DrawTrainingHud() {
   if(networkPanel)return;
   GUI.Box(new Rect(leftWidth+12,77,Mathf.Min(440,Screen.width-leftWidth-rightWidth-24),83),"",panel);
   GUI.Label(new Rect(leftWidth+25,86,420,22),Project.season=="2026"?$"REBUILT  ·  Mavi {BlueFuel}   Kırmızı {RedFuel}":Project.season=="2025"?"REEFSCAPE · eşya stoğu":Project.season=="2024"?"CRESCENDO · eşya stoğu":"Antrenman · eşya stoğu",label);
   GUI.Label(new Rect(leftWidth+25,110,420,22),$"Robot stoğu: {StoredTotal} / {Project.training.capacity}  ·  Atış: {ShotsFired}",muted);
   GUI.Label(new Rect(leftWidth+25,132,420,20),Project.training.unlimitedShots?"GÖSTERİM · Sınırsız atış açık":"Stoklu antrenman · toplama ve atış",muted);
  }
  void ShotLimitControl(){
   bool wasEnabled=GUI.enabled;GUI.enabled=!Busy&&!FileDialogs.IsOpen;
   Project.training.unlimitedShots=GUILayout.Toggle(Project.training.unlimitedShots," Sınırsız atış · gösterim");
   Text(Project.training.unlimitedShots?"Stok boş olsa da atış yapılır; mevcut stok azalmaz.":"Her atış stoktan 1 eşya kullanır; stok boşsa atış durur.",true);
   GUI.enabled=wasEnabled;
  }
 }
}
