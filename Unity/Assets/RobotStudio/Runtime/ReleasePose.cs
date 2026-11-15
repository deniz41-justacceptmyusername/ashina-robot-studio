using UnityEngine;
namespace AshinaStudio {
 public partial class StudioApp {
  internal static Quaternion ReleaseOrientation(RobotAction action,Transform mount,Vector3 flightDirection){
   if(action.orientationMode!="custom")return action.piece=="coral"?Quaternion.FromToRotation(mount.up,flightDirection)*mount.rotation:mount.rotation;
   Vector3 forward=Frc.Direction(action.piecePitch,action.pieceYaw);
   Vector3 right=Frc.Direction(0,action.pieceYaw-90);
   // An explicit perpendicular up keeps the frame stable even for vertical release angles.
   Quaternion frame=Quaternion.LookRotation(forward,Vector3.Cross(forward,right))*Quaternion.AngleAxis((float)action.pieceRoll,Vector3.forward);
   Quaternion modelBasis=action.piece=="coral"?Quaternion.FromToRotation(Vector3.up,Vector3.forward):Quaternion.identity;
   return mount.rotation*frame*modelBasis;
  }
 }
}
