using System;
using System.Linq;
using UnityEngine;

namespace AshinaStudio {
 [Serializable] public sealed class MatchFieldState {public int id;public float[] p,q;}
 // Dynamic field members, such as the six REEFSCAPE cages, share the same authority as the robots.
 public sealed class MatchFieldSync : IDisposable {
  readonly HangingCage[] cages;readonly bool authority;
  public int Count=>cages.Length;
  public MatchFieldSync(FieldWorld field,bool authority){this.authority=authority;cages=field.Root.GetComponentsInChildren<HangingCage>(true).OrderBy(c=>c.Suspension.connectedAnchor.x).ThenBy(c=>c.Suspension.connectedAnchor.z).ThenBy(c=>c.Suspension.connectedAnchor.y).ToArray();if(!authority)foreach(var cage in cages){cage.Body.isKinematic=true;cage.Body.interpolation=RigidbodyInterpolation.None;}}
  public MatchFieldState[] Snapshot()=>cages.Select((c,i)=>new MatchFieldState{id=i,p=MatchProtocol.V(c.transform.position),q=MatchProtocol.Q(c.transform.rotation)}).ToArray();
  public void Apply(MatchFieldState[] states){if(authority||states==null)return;foreach(var state in states)if(state.id>=0&&state.id<cages.Length){var body=cages[state.id].Body;body.position=MatchProtocol.Vector(state.p);body.rotation=MatchProtocol.Rotation(state.q);body.transform.SetPositionAndRotation(body.position,body.rotation);}Physics.SyncTransforms();}
  public void Reset(){if(!authority)return;foreach(var cage in cages){cage.Body.position=cage.Suspension.connectedAnchor-cage.Suspension.anchor;cage.Body.rotation=Quaternion.identity;cage.Body.linearVelocity=Vector3.zero;cage.Body.angularVelocity=Vector3.zero;}Physics.SyncTransforms();}
  public void Dispose(){if(!authority)foreach(var cage in cages)if(cage!=null){cage.Body.isKinematic=false;cage.Body.interpolation=RigidbodyInterpolation.Interpolate;}}
 }
}
