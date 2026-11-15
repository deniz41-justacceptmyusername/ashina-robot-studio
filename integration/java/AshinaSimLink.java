package frc.robot.sim;

import edu.wpi.first.networktables.*;
import edu.wpi.first.wpilibj.RobotBase;
import edu.wpi.first.wpilibj.Timer;
import edu.wpi.first.wpilibj.simulation.DriverStationSim;

/** Simulation only. Xbox-compatible input for the team's existing command bindings. */
public final class AshinaSimLink implements AutoCloseable {
  private final IntegerSubscriber heartbeat;
  private final BooleanSubscriber enabled, autonomous;
  private final DoubleArraySubscriber[] axes = new DoubleArraySubscriber[2];
  private final IntegerSubscriber[] buttons = new IntegerSubscriber[2], pov = new IntegerSubscriber[2];
  private final BooleanSubscriber[] connected = new BooleanSubscriber[2];
  private final IntegerSubscriber fuel, resetSequence;
  private final DoubleArraySubscriber resetPose;
  private long lastResetSequence;
  private final BooleanPublisher ready;
  private long lastHeartbeat = Long.MIN_VALUE;
  private double lastSeen = -100;
  private boolean owned;

  private static NetworkTableInstance localSimulationServer() {
    var nt=NetworkTableInstance.getDefault();int port=Integer.getInteger("ashina.nt.port",5810);
    if(port!=5810){nt.stopServer();nt.startServer("", "127.0.0.1", 1738, port);}
    return nt;
  }
  public AshinaSimLink() { this(localSimulationServer()); }
  public AshinaSimLink(NetworkTableInstance nt) {
    if (!RobotBase.isSimulation()) throw new IllegalStateException("AshinaSimLink is only for desktop simulation");
    heartbeat = nt.getIntegerTopic("/AshinaSim/DriverStation/heartbeat").subscribe(-1);
    enabled = nt.getBooleanTopic("/AshinaSim/DriverStation/enabled").subscribe(false);
    autonomous = nt.getBooleanTopic("/AshinaSim/DriverStation/autonomous").subscribe(false);
    resetSequence = nt.getIntegerTopic("/AshinaSim/Robot/resetSequence").subscribe(0);
    resetPose = nt.getDoubleArrayTopic("/AshinaSim/Robot/resetPose").subscribe(new double[0]);
    fuel = nt.getIntegerTopic("/AshinaSim/Robot/fuelCount").subscribe(0);
    ready = nt.getBooleanTopic("/AshinaRobot/Bridge/ready").publish();
    for (int i = 0; i < 2; i++) {
      String p = "/AshinaSim/DriverStation/joystick/" + i;
      axes[i] = nt.getDoubleArrayTopic(p + "/axes").subscribe(new double[6]);
      buttons[i] = nt.getIntegerTopic(p + "/buttons").subscribe(0);
      pov[i] = nt.getIntegerTopic(p + "/pov").subscribe(-1);
      connected[i] = nt.getBooleanTopic(p + "/connected").subscribe(false);
    }
  }

  /** Call once every simulationPeriodic, including while disabled. */
  public void update() {
    long h = heartbeat.get();
    if (h >= 0 && h != lastHeartbeat) { lastHeartbeat = h; lastSeen = Timer.getFPGATimestamp(); }
    boolean live = Timer.getFPGATimestamp() - lastSeen < .35;
    if (!live && !owned) { ready.set(false); return; }
    owned = live;
    DriverStationSim.setDsAttached(live);
    DriverStationSim.setAutonomous(live && autonomous.get());
    DriverStationSim.setEnabled(live && enabled.get());
    DriverStationSim.setTest(false);
    for (int i = 0; i < 2; i++) {
      boolean attached = live && connected[i].get();
      double[] a = axes[i].get();
      DriverStationSim.setJoystickIsXbox(i, true);
      DriverStationSim.setJoystickName(i, "Ashina Xbox-compatible controller " + i);
      DriverStationSim.setJoystickAxisCount(i, attached ? 6 : 0);
      DriverStationSim.setJoystickButtonCount(i, attached ? 10 : 0);
      DriverStationSim.setJoystickPOVCount(i, attached ? 1 : 0);
      if (attached) {
        for (int axis = 0; axis < 6; axis++) DriverStationSim.setJoystickAxis(i, axis, axis < a.length && Double.isFinite(a[axis]) ? Math.max(-1, Math.min(1, a[axis])) : 0);
        DriverStationSim.setJoystickButtons(i, (int)buttons[i].get());
        DriverStationSim.setJoystickPOV(i, 0, (int)pov[i].get());
      }
    }
    DriverStationSim.notifyNewData();
    ready.set(live);
  }

  public java.util.Optional<edu.wpi.first.math.geometry.Pose2d> takeResetPose() {
    long sequence=resetSequence.get();if(sequence==lastResetSequence)return java.util.Optional.empty();lastResetSequence=sequence;double[] p=resetPose.get();if(p.length<3||!Double.isFinite(p[0])||!Double.isFinite(p[1])||!Double.isFinite(p[2]))return java.util.Optional.empty();return java.util.Optional.of(new edu.wpi.first.math.geometry.Pose2d(p[0],p[1],new edu.wpi.first.math.geometry.Rotation2d(p[2])));
  }
  public int fuelCount() { return (int)Math.max(0, fuel.get()); }
  @Override public void close() {
    DriverStationSim.setEnabled(false);DriverStationSim.notifyNewData();
    resetSequence.close();resetPose.close();heartbeat.close();enabled.close();autonomous.close();fuel.close();ready.close();
    for (int i = 0; i < 2; i++) {axes[i].close();buttons[i].close();pov[i].close();connected[i].close();}
  }
}
