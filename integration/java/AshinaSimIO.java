package frc.robot;

import edu.wpi.first.math.geometry.Pose2d;
import edu.wpi.first.math.geometry.Rotation2d;
import edu.wpi.first.networktables.DoubleArrayPublisher;
import edu.wpi.first.networktables.DoubleArraySubscriber;
import edu.wpi.first.networktables.NetworkTableInstance;
import edu.wpi.first.networktables.PubSubOption;

/** Optional adapter for a WPILib desktop simulation, SI units and robot-relative speeds. */
public final class AshinaSimIO implements AutoCloseable {
  private final DoubleArrayPublisher request;
  private final DoubleArraySubscriber pose;

  public AshinaSimIO(NetworkTableInstance nt) {
    request = nt.getDoubleArrayTopic("/AshinaRobot/Drive/request")
        .publish(PubSubOption.keepDuplicates(true));
    pose = nt.getDoubleArrayTopic("/AshinaSim/Robot/pose").subscribe(new double[0]);
  }

  /** Call from simulationPeriodic every 20 ms. Forward m/s, left m/s, CCW rad/s. */
  public void drive(double vx, double vy, double omega) {
    request.set(new double[] {vx, vy, omega});
  }

  /** The simulator publishes its field truth; decide explicitly how to feed your estimator. */
  public Pose2d fieldTruthOr(Pose2d fallback) {
    double[] p = pose.get();
    if (p.length < 3 || !Double.isFinite(p[0]) || !Double.isFinite(p[1]) || !Double.isFinite(p[2])) {
      return fallback;
    }
    return new Pose2d(p[0], p[1], new Rotation2d(p[2]));
  }

  @Override public void close() { request.close(); pose.close(); }
}
