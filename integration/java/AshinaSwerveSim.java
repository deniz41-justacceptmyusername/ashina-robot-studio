package frc.robot.sim;

import edu.wpi.first.math.geometry.Pose2d;
import edu.wpi.first.math.geometry.Rotation2d;
import edu.wpi.first.math.kinematics.*;
import edu.wpi.first.networktables.*;

/** Simple optional IO for teams that want Unity to own chassis physics. SI units, no motor model. */
public final class AshinaSwerveSim implements AutoCloseable {
  private final SwerveDriveKinematics kinematics;
  private final DoubleArraySubscriber truth;
  private final DoubleArrayPublisher request;
  private final SwerveModulePosition[] positions;
  private Pose2d previous;

  public AshinaSwerveSim(NetworkTableInstance nt, SwerveDriveKinematics kinematics, int moduleCount) {
    this.kinematics=kinematics;
    truth=nt.getDoubleArrayTopic("/AshinaSim/Robot/pose").subscribe(new double[0]);
    request=nt.getDoubleArrayTopic("/AshinaRobot/Drive/request").publish(PubSubOption.keepDuplicates(true));
    positions=new SwerveModulePosition[moduleCount];for(int i=0;i<positions.length;i++)positions[i]=new SwerveModulePosition();
  }
  /** Send the same desired robot-relative chassis speeds your real drive method receives, every 20 ms. */
  public void drive(ChassisSpeeds speeds) {request.set(new double[]{speeds.vxMetersPerSecond,speeds.vyMetersPerSecond,speeds.omegaRadiansPerSecond});}
  /** Updates ideal wheel-distance and gyro inputs for the existing WPILib odometry / pose estimator. */
  public boolean updateSensors() {
    double[] p=truth.get();if(p.length<3||!Double.isFinite(p[0])||!Double.isFinite(p[1])||!Double.isFinite(p[2]))return false;
    var pose=new Pose2d(p[0],p[1],new Rotation2d(p[2]));
    if(previous!=null) {
      var delta=previous.log(pose);
      // Kinematics is linear: treating each displacement as a unit-duration sample yields wheel deltas.
      var deltas=kinematics.toSwerveModuleStates(new ChassisSpeeds(delta.dx,delta.dy,delta.dtheta));
      if(deltas.length!=positions.length)throw new IllegalArgumentException("Module count must match the team's kinematics");
      for(int i=0;i<positions.length;i++){positions[i].distanceMeters+=deltas[i].speedMetersPerSecond;positions[i].angle=deltas[i].angle;}
    }
    previous=pose;return true;
  }
  public Rotation2d heading(){return previous==null?new Rotation2d():previous.getRotation();}
  public Pose2d fieldTruth(){return previous==null?new Pose2d():previous;}
  public SwerveModulePosition[] modulePositions(){var result=new SwerveModulePosition[positions.length];for(int i=0;i<result.length;i++)result[i]=new SwerveModulePosition(positions[i].distanceMeters,positions[i].angle);return result;}
  public void reset(Pose2d pose){previous=pose;for(var p:positions){p.distanceMeters=0;p.angle=new Rotation2d();}}
  @Override public void close(){truth.close();request.close();}
}
