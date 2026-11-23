# Validation record · v0.4.5

Built locally with Unity **6000.1.13f1** on Windows: **0 errors, 0 warnings**.

| Suite | Passed | Scope |
| --- | ---: | --- |
| General native player | 131 | CAD import, recorded motion, persistence, fields, inventory, release attitude, real NT4 server and Blockly UI |
| Configured team robot | 13 | Java/YAGSL launch, Xbox input, intake, shooter, HUB goal, odometry and PhotonVision pose processing |
| Chassis and cage physics | 13 | Dynamic mass, settling, FUEL collisions, airborne traction, real REBUILT ramp and six suspended REEFSCAPE cages |
| **Total** | **157** | Rerun locally on the delivered 0.4.5 build |

Machine-readable records are in [verification](../verification/). The robot scenario recorded **7 pickups, 5 shots, 1 HUB goal**. The private robot CAD/code fixture is not distributed.

## Physical behavior

The dedicated scenario measured a maximum chassis tilt of approximately **12.4°** while traversing the official REBUILT ramp. The chassis settled after leaving it. Seven loose FUEL pieces were placed in its path; bumper contacts displaced at least four by more than 0.3 m, without turning the balls into a support platform. The airborne test verified gravity and loss of drive/yaw traction.

All six REEFSCAPE cages have dynamic bodies, four narrow uprights and two plate colliders. An impulse moved a deep cage while its suspension point remained attached. The static collision bake excludes the cages, preventing duplicate immovable geometry.

The legacy terrain test now drives through the same running mode as a user. Editor-only CAD picking colliders are disabled before chassis inertia is recalculated. Physics tests run with a headless renderer; screenshots and UI review were performed separately in the rendered Windows player.

## UI review

Native screenshots of the final player verify the restored button styling, a single main robot, and the two primary model/add-part controls. Earlier interactive inspection covered the intake action and driving dashboard. The final single-main-model workflow has six automated checks covering replacement, optional mechanisms, invalid-input rollback and project persistence. The logo was verified in the executable Windows icon resource.

## Retained coverage

The general suite still checks limited stock, optional unlimited shots, season capacities, one intake probability attempt per contact entry, independent projectile trajectory/body attitude, free coral motion after release, nested NT structs, reconnect and real Blockly integration. The robot suite exercises existing command and pose-estimation paths.

GitHub Actions checks repository integrity and public-package hygiene. It does not run the licensed Unity Editor or the private robot scenario.

## Practical limits

The chassis uses a fixed 55 kg training model, four simplified spring/damper contacts and bounded traction. Cages use an estimated 12 kg mass and a constrained pendulum joint. Individual chain links and automatic robot-climb attachment are not simulated. In NT **observe** mode, robot code owns XY/yaw and Unity fits visual height/tilt to the terrain; use NT **physics** mode when Unity should resolve chassis collisions.

Vision is geometric rather than pixel-based. Universal controller coverage, full FMS/refereeing and detailed electrical/motor dynamics remain outside this release.
