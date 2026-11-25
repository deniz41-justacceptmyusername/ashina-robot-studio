using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AshinaStudio {
 public partial class StudioApp {
  bool dedicatedRoom;int dedicatedMinimum=6;double dedicatedFinishedAt;
  static string MatchArgument(string[] args,string key,string fallback=null){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
  async void StartDedicatedRoom(string[] args){
   try{int port=int.Parse(MatchArgument(args,"--match-server","5820"));if(port<1024||port>65535)throw new Exception("Match port must be 1024–65535.");string season=MatchArgument(args,"--season","2026");if(Project.season!=season)await LoadField(season);
    dedicatedMinimum=int.Parse(MatchArgument(args,"--min-players","6"));if(dedicatedMinimum<1||dedicatedMinimum>6)throw new Exception("Player count must be 1–6.");
    dedicatedRoom=true;if(!await HostRoom(port,"Match server","blue",false,true))throw new Exception(Notice);Room.AutoSeconds=ServerDuration(args,"--auto-seconds",Room.AutoSeconds,1,300);Room.TeleopSeconds=ServerDuration(args,"--teleop-seconds",Room.TeleopSeconds,1,600);Room.CountdownSeconds=ServerDuration(args,"--countdown-seconds",3,.1,10);Room.GapSeconds=ServerDuration(args,"--gap-seconds",3,0,5);string key=MatchArgument(args,"--room-key");if(key!=null)Room.ConfigureKey(key);
    string address=MatchArgument(args,"--public-address",LocalRoomAddress());string invitation=address+":"+Room.Port+"/"+Room.Token;string output=MatchArgument(args,"--invite-file");if(output!=null)File.WriteAllText(output,invitation);Debug.Log("ASHINA_MATCH_SERVER_READY "+invitation);
   }catch(Exception e){Debug.LogError("Match server: "+e.Message);Application.Quit(2);}
  }
  static double ServerDuration(string[] args,string key,double fallback,double min,double max){var raw=MatchArgument(args,key);if(raw==null)return fallback;if(!double.TryParse(raw,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var n)||!Finite(n)||n<min||n>max)throw new Exception("Invalid "+key);return n;}
  void DedicatedRoomTick(){
   if(!dedicatedRoom||Room==null)return;
   if(Room.Phase=="lobby"&&Room.Robots.Count>=dedicatedMinimum&&Room.CanStart)Room.StartMatch();
   if(Room.Phase=="finished"){if(dedicatedFinishedAt==0)dedicatedFinishedAt=MatchLink.Clock;if(MatchLink.Clock-dedicatedFinishedAt>=8){Room.ReturnLobby();dedicatedFinishedAt=0;}}else dedicatedFinishedAt=0;
  }
 }
}
