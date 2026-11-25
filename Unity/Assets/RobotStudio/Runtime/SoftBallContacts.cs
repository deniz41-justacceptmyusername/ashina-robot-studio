using System.Collections.Concurrent;
using Unity.Collections;
using UnityEngine;

namespace AshinaStudio {
 internal static class SoftBallContacts {
  // PhysX invokes modification on worker threads. Keep only the collider IDs here.
  static readonly ConcurrentDictionary<int,byte> bumpers=new ConcurrentDictionary<int,byte>();
  static readonly ConcurrentDictionary<int,byte> balls=new ConcurrentDictionary<int,byte>();

  static SoftBallContacts(){
   Physics.ContactModifyEvent+=Modify;
   Physics.ContactModifyEventCCD+=Modify;
  }

  internal static int AddBumper(Collider bumper){
   int id=bumper.GetInstanceID();
   bumpers.TryAdd(id,0);
   bumper.hasModifiableContacts=true;
   return id;
  }

  internal static int AddBall(Collider ball){
   int id=ball.GetInstanceID();
   balls.TryAdd(id,0);
   return id;
  }

  internal static void RemoveBumper(int id)=>bumpers.TryRemove(id,out _);
  internal static void RemoveBall(int id)=>balls.TryRemove(id,out _);

  static void Modify(PhysicsScene scene,NativeArray<ModifiableContactPair> pairs){
   foreach(var pair in pairs){
    var contact=pair;
    int first=contact.colliderInstanceID, second=contact.otherColliderInstanceID;
    // A packed pile of foam can yield and roll away. The rigid-sphere solver
    // otherwise lets it transmit enough force to lever the chassis off the carpet.
    if(bumpers.ContainsKey(first)&&balls.ContainsKey(second)){
     var mass=contact.massProperties;
     mass.inverseMassScale=0;
     mass.inverseInertiaScale=0;
     contact.massProperties=mass;
    }else if(bumpers.ContainsKey(second)&&balls.ContainsKey(first)){
     var mass=contact.massProperties;
     mass.otherInverseMassScale=0;
     mass.otherInverseInertiaScale=0;
     contact.massProperties=mass;
    }
   }
  }
 }
}
