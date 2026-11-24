# Validation record · v0.4.6

Built locally with Unity **6000.1.13f1**: **0 errors, 0 warnings**.

| Suite | Passed | Scope |
| --- | ---: | --- |
| General native player | 131 | CAD, project/motion persistence, stock, fields, real NT4 and Blockly, one-main-model workflow |
| Configured team robot | 13 | Java/YAGSL, Xbox input, intake, shooter, HUB entry, odometry and PhotonVision |
| Chassis/cage physics | 13 | Ramp tilt, settling, FUEL response, airborne traction and six swinging cages |
| Home and general shots | 31 | Home state, setup fit, generic live parameters, moving muzzle, targets, regions, power, scatter, dropping and a real HUB shot |
| **Total** | **188** | Local native checks |

Records are in [verification](../verification/). The configured robot scenario collected 7 FUEL, fired 5 and scored 1 HUB goal. Its private robot fixture is not distributed.

## New shot evidence

The feature suite used a real WPILib NT4 server. It verified degree/radian/turn conversion, a field-referenced moving mechanism while the chassis turns, picker assignment, live angle/speed precedence, and velocity inherited from both observed WPILib motion and rotation at an offset muzzle.

Point assistance preserved the configured elevation and included gravity/damping. Insufficient power created no shot and consumed no stock. Two field regions selected different targets, including a backward feed. The suite checked region fallback, persistence, invalid input, bounded scatter and zero-speed dropping.

An actual Rigidbody projectile passed within **about 3 cm** of a configured test point in the final run. A separate fixed-angle shot entered the official REBUILT HUB and incremented the goal counter through the real field collision geometry. These are representative checks, not an accuracy guarantee for uncalibrated real shooters.

## Home and visual review

Normal startup stays on the home screen and cannot enable the robot silently. Continuing opens the workspace; returning home stops and hides it. Native screenshots of the home screen, setup overlay and general shot editor were inspected. The existing button styling is retained. Source screenshots suppress personal recent-project names.

## Retained physics and integration

The chassis uses four simplified support contacts and a fixed 55 kg training mass. The real ramp produces about 12.4 degrees of chassis tilt. Loose FUEL is physically displaced, not used as a wheel-ray support platform. Six REEFSCAPE cages have hollow compound colliders and constrained pendulum suspension with estimated 12 kg mass. Individual chain links and automatic climb attachment are not modeled.

In NT observe mode, robot code owns XY/yaw and Unity fits terrain height/tilt. Use chassis-request physics mode when Unity should own collisions. Positive yaw requests follow FRC coordinate direction.

## Practical limits

The 5–10 minute setup goal assumes prepared CAD and published simulation values. Camera extrinsics/FOV, motor gearing, transfer efficiency and the real shooter's calibration need correct inputs. Vision is geometric rather than image processing. Generic bindings cannot infer unpublished code internals.

Online rooms and six-player matches are **not implemented**; see [the design](online-design.md). Full FMS/officiating, exhaustive controller-brand testing and detailed motor/electrical models remain outside this release.

GitHub Actions checks repository integrity and package hygiene; it does not run the licensed Unity Editor or private robot fixture.
