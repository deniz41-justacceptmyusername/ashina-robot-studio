# Moving-shot research

Reviewed for the 0.4.6 design. These sources informed the integration contract; their robot code is not copied into this project.

## Team 254 Sideways (2022)

Sideways has a rotating shooter and an adjustable hood. Its superstructure calculates wheel RPM, hood position, mechanism angle and rotational feedforward. Field-relative aiming is converted through the robot's current orientation. The robot's shot logic accounts for motion instead of assuming one fixed world direction.

Simulator mapping: bind the moving geometry to measured angles, keep the nozzle under that part, and read its current transform when emitting. Inherit velocity at the nozzle, including chassis rotation and relative mechanism motion. The simulator should accept the team's calculated shot rather than introduce a team-specific turret controller.

Sources: [robot overview](https://www.team254.com/frc/2022/), [Superstructure.java](https://github.com/Team254/FRC-2022-Public/blob/main/src/main/java/com/team254/frc2022/subsystems/Superstructure.java), [Turret.java](https://github.com/Team254/FRC-2022-Public/blob/main/src/main/java/com/team254/frc2022/subsystems/Turret.java).

## Mechanical Advantage Darwin (2026)

The launch calculator distinguishes scoring and passing targets. It uses separate calibration maps and a lookahead calculation involving robot motion and time of flight. This reinforces that target choice, angle, speed and feed permission are independent data channels.

Simulator mapping: the same emitter can score or feed. Robot-owned shot outputs take priority when connected; standalone regional targets provide a simpler practice alternative. A point target must respect the configured elevation and available launch speed rather than force a successful hit.

Source: [LaunchCalculator.java](https://github.com/Mechanical-Advantage/RobotCode2026Public/blob/main/src/main/java/org/littletonrobotics/frc2026/subsystems/launcher/LaunchCalculator.java).

## Implemented general behavior

- Optional position/rotation bindings for any part, without predefined robot subsystems.
- Live shot pitch/yaw/speed and trigger selection with units and reference frames.
- Updated mechanism transforms before emission, with muzzle-point velocity inheritance.
- Offline fixed-elevation point targets, regional targets, maximum power and scatter.
- Normal free-flight rigid-body physics after release; no automatic hit guarantee.

The native feature checks exercise these contracts with a real WPILib NT4 server and an actual simulated projectile. They are not a claim that either team's complete robot has been imported and validated here.
