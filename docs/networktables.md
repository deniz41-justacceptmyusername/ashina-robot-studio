# WPILib + NetworkTables

The standalone application can drive a generic robot directly. To run a team's actual commands and estimators, start the team's WPILib desktop simulation and connect the application to its NT4 server. The usual local endpoint is `127.0.0.1:5810`; another host and port can be selected.

## 1. Add the controller bridge

Copy `AshinaSimLink.java` from [integration/java](../integration/java/) into `src/main/java/frc/robot/sim/`. Enable desktop support in the GradleRIO project. Merge these calls into existing simulation hooks:

```java
private frc.robot.sim.AshinaSimLink studio;

@Override public void simulationInit() {
  studio = new frc.robot.sim.AshinaSimLink();
}

@Override public void simulationPeriodic() {
  studio.update();
}
```

The helper uses `DriverStationSim` to feed two Xbox-compatible controller slots. Existing `CommandXboxController(0)` bindings can remain in place. Unity's enabled/autonomous state is forwarded; a lost heartbeat disables the simulated robot. If Unity has never connected, the bridge leaves normal Sim GUI input available.

In the application, enable controller forwarding. Optional keyboard forwarding maps WASD/QE to Xbox axes. Standard Unity Input System gamepad layouts are normalized; physical coverage varies by device and Windows driver.

## 2. Choose a drive-physics owner

### Keep an existing robot simulation

Publish its simulated field pose, then select **WPILib konumunu izle**. Supported inputs include Field2d, Pose2d/Pose3d, and configured numeric arrays. Field2d's heading is interpreted in degrees; the pose-array contract below uses radians.

Keep simulated ground truth and odometry/estimated pose as separate values. Unity can visualize ground truth while the team's odometry and pose estimator continue to operate in robot code.

### Let Unity apply simple chassis requests

Select **Hız isteğini Unity'de sür** and publish robot-relative `[vx, vy, omega]` requests every 20 ms. `AshinaSwerveSim.java` is an optional ideal sensor IO adapter: provide the team's `SwerveDriveKinematics`, call `drive(...)`, and use its wheel distances and heading as simulation inputs to the existing odometry code.

This is a small IO integration, not automatic emulation of every CAN device. `AshinaSimIO.java` offers a smaller pose/request adapter and uses the `frc.robot` package; the other supplied helpers use `frc.robot.sim`.

## 3. Map mechanisms and inventory

Publish intake enable/output, shooter readiness, and desired projectile speed from the team's simulation. Select a capture or emission action in Unity and map the appropriate NT field. A scalar, boolean, array element, or supported struct field can be used.

For a shooter, map the readiness/feed output to the emission trigger. Map a speed value through a configurable scale and offset to obtain m/s. Set trajectory and initial body attitude separately. The selected geometry and contact region do not require detailed motor/gear simulation.

Normal emission consumes stock. The capture region, probability, and capacity belong to the robot project. `studio.fuelCount()` exposes the current virtual FUEL count to a simulated beam-break or inventory IO implementation. Demonstration mode can be enabled to emit without consuming stock.

## 4. Feed geometric vision

For a compatible PhotonVision 2026 Java project, copy `AshinaPhotonBridge.java`. Create it in `simulationInit()` using the same camera name as the team's `PhotonCamera`, then call `update()` in `simulationPeriodic()`.

Unity publishes visible tag IDs, camera-to-target transforms, field camera pose, sequence, and latency. The bridge converts the frame into PhotonVision NT results consumed by existing PhotonCamera/PhotonPoseEstimator code. Match the camera name, mounting transform, FOV, latency, and Welded/AndyMark field layout on both sides.

Limelight-style `tv`, `tx`, `ty`, `ta`, `tid`, `tl`, `cl`, `botpose_wpiblue`, and `botpose_orb_wpiblue` topics are also produced. This is geometric vision with occlusion and latency, not a camera image-processing pipeline or complete firmware emulation.

## 5. Launching Java code from the application

The application can select a `build.gradle` project and launch `simulateJava`. It uses the installed WPILib JDK or `JAVA_HOME`. To support its selected local NT port, add:

```groovy
tasks.withType(JavaExec).configureEach {
    if (project.hasProperty("ashinaNtPort"))
        jvmArgs "-Dashina.nt.port=" + project.property("ashinaNtPort")
}
```

If desired, set the Sim GUI default with:

```groovy
wpi.sim.addGui().defaultEnabled = !project.hasProperty("ashinaStudio")
```

Launching from the application passes `ashinaStudio` and selects an available local port. A robot launched separately in VS Code can be connected directly instead. The application tracks and stops only the process tree that it launches.

## Topic contract

| Topic | Type / meaning |
| --- | --- |
| `/AshinaRobot/Drive/request` | double[] `[vx, vy, omega]`, robot-relative, m/s and rad/s |
| `/AshinaSim/Robot/pose` | double[] `[x, y, yaw]`, blue-origin metres and radians |
| `/AshinaSim/Robot/velocity` | field-relative chassis velocity |
| `/AshinaSim/DriverStation/...` | enabled, autonomous, heartbeat, and joystick values |
| `/AshinaRobot/Bridge/ready` | controller bridge readiness |
| `/AshinaSim/Robot/fuelCount` | virtual FUEL inventory |
| `/AshinaSim/Inventory/<kind>` | inventory by game-piece kind |
| `/AshinaSim/Robot/resetPose`, `resetSequence` | reset request to bind to the team's simulation reset |
| `/AshinaSim/Vision/<camera>/frame` | geometric vision JSON used by the PhotonVision bridge |
| `/AshinaSim/Game/blueFuel`, `redFuel`, `fuelShots` | REBUILT practice counters |

The browser receives published topics and reconnects automatically. It cannot read unpublished motor internals. Java helpers were exercised with WPILib/PhotonVision 2026; other releases and C++/Python code need an equivalent adaptation.

## Online shared-world mode

Online rooms automatically select chassis-request physics. Keep each NT connection local and send the desired robot-relative chassis speeds. Feed the shared-world wheel/gyro IO back to existing odometry with `AshinaSwerveSim`; handle `AshinaSimLink.takeResetPose()` at match resets. The updated controller bridge also forwards alliance, station and match time. Existing mechanism/shot topics stay local and their resolved inputs are sent to the assigned room robot. [Online setup](online.md).
