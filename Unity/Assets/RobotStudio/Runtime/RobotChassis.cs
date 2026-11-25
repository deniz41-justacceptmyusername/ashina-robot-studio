using UnityEngine;

namespace AshinaStudio {
 // A lightweight four-contact suspension. Only field surfaces support a wheel;
 // loose game pieces collide with the chassis and never become a ground ray.
 public sealed class RobotChassis : MonoBehaviour {
  public Rigidbody Body {get;private set;}
  internal Collider Bumper=>bumper;
  public Vector3 GroundNormal {get;private set;}=Vector3.up;
  public int GroundedWheels {get;private set;}
  public float Acceleration=6;
  Vector3[] wheels=new Vector3[4];BoxCollider bumper;PhysicsMaterial contact;int bumperId;
  void Awake(){EnsureBody();}
  void EnsureBody(){
   if(Body!=null)return;
   Body=gameObject.AddComponent<Rigidbody>();Body.mass=55;Body.isKinematic=true;
   Body.interpolation=RigidbodyInterpolation.None;
   Body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
   Body.solverIterations=12;Body.solverVelocityIterations=6;
   Body.linearDamping=.03f;Body.angularDamping=2;Body.maxAngularVelocity=12;
   bumper=gameObject.AddComponent<BoxCollider>();
   bumperId=SoftBallContacts.AddBumper(bumper);
   contact=new PhysicsMaterial("Robot bumper"){staticFriction=.35f,dynamicFriction=.25f,bounciness=.02f,frictionCombine=PhysicsMaterialCombine.Minimum,bounceCombine=PhysicsMaterialCombine.Minimum};
   bumper.material=contact;
  }
  public void Configure(DriveData drive){
   EnsureBody();
   float width=(float)drive.dimensions[1],length=(float)drive.dimensions[0],height=(float)drive.dimensions[2];
   bumper.size=new Vector3(width,Mathf.Max(.08f,height-.09f),length);
   bumper.center=new Vector3(0,.09f+bumper.size.y/2,0);
   Body.centerOfMass=new Vector3(0,Mathf.Clamp(height*.4f,.12f,.5f),0);
   int i=0;foreach(float x in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})wheels[i++]=new Vector3(x*width*.38f,.5f,z*length*.38f);
   Acceleration=(float)drive.acceleration;
  }
  void OnEnable(){if(Body!=null){Body.isKinematic=false;Body.interpolation=RigidbodyInterpolation.Interpolate;}}
  void OnDisable(){if(Body!=null){Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;Body.isKinematic=true;Body.interpolation=RigidbodyInterpolation.None;}}
  public void Teleport(Vector3 position,Quaternion rotation){
   transform.SetPositionAndRotation(position,rotation);Body.position=position;Body.rotation=rotation;
   if(!Body.isKinematic){Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;}
   Physics.SyncTransforms();
  }
  public void Move(Vector3 displacement,float turn){
   if(!enabled)enabled=true;
   float dt=Time.fixedDeltaTime;GroundedWheels=0;Vector3 normals=Vector3.zero;
   foreach(var wheel in wheels){
    var origin=transform.TransformPoint(wheel);
    if(!Physics.Raycast(origin,Vector3.down,out var hit,.72f,1,QueryTriggerInteraction.Ignore)||hit.normal.y<.5f)continue;
    float compression=.5f-hit.distance;
    float speed=Body.GetPointVelocity(origin).y;
    float force=Body.mass*Physics.gravity.magnitude/4+compression*3200-speed*300;
    if(force<=0)continue;
    Body.AddForceAtPosition(Vector3.up*Mathf.Min(force,Body.mass*Physics.gravity.magnitude*.9f),transform.TransformPoint(wheel-Vector3.up*.5f));
    normals+=hit.normal;GroundedWheels++;
   }
   GroundNormal=GroundedWheels>0?normals.normalized:Vector3.up;
   if(GroundedWheels==0)return; // No steering/traction in the air.
   Vector3 desired=Vector3.ProjectOnPlane(displacement/dt,GroundNormal);
   Vector3 current=Vector3.ProjectOnPlane(Body.linearVelocity,GroundNormal);
   var acceleration=Vector3.ClampMagnitude((desired-current)/dt,Mathf.Min(Acceleration,9.5f)*GroundedWheels/4f);
   Body.AddForce(acceleration,ForceMode.Acceleration);
   // FRC positive yaw turns left; Unity positive Y torque turns right.
   float yawError=-turn/dt-Vector3.Dot(Body.angularVelocity,GroundNormal);
   Body.AddTorque(GroundNormal*Mathf.Clamp(yawError*10,-12,12),ForceMode.Acceleration);
  }
  public void ObservePose(Vector3 position,float heading){
   enabled=false;var yaw=Frc.Rotation(new[]{0.0,0,heading*180/System.Math.PI});
   var points=new Vector3[4];int count=0;float height=0;
   for(int i=0;i<4;i++){
    var origin=position+yaw*(wheels[i]-Vector3.up*.5f)+Vector3.up*.8f;
    if(Physics.Raycast(origin,Vector3.down,out var hit,1.6f,1,QueryTriggerInteraction.Ignore)&&hit.normal.y>.5f&&hit.point.y<.6f){points[i]=hit.point;height+=hit.point.y;count++;}
   }
   var normal=count==4?Vector3.Cross(points[1]+points[3]-points[0]-points[2],points[2]+points[3]-points[0]-points[1]).normalized:Vector3.up;
   if(normal.y<.5f)normal=Vector3.up;
   if(count==4)position.y=height/4;
   Teleport(position,Quaternion.FromToRotation(Vector3.up,normal)*yaw);
  }
  void OnDestroy(){if(bumperId!=0)SoftBallContacts.RemoveBumper(bumperId);if(contact!=null)Lifetime.Destroy(contact);}
 }
}
