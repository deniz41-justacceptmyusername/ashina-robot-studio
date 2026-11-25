# Validation record · v0.4.7

Built locally with Unity **6000.1.13f1**: **0 errors, 0 warnings**.

| Suite | Passed | Scope |
| --- | ---: | --- |
| General native player | 131 | CAD, project/motion persistence, stock, fields, real NT4/Blockly |
| Configured team robot | 13 | Java/YAGSL, Xbox input, intake, shooter, HUB, odometry and PhotonVision |
| Chassis/cage physics | 13 | Ramp tilt, settling, FUEL contact, airborne traction and six swinging cages |
| Home and shots | 31 | Home/setup, generic live parameters, moving muzzle, point/region aiming, power and scatter |
| Online host | 42 | Six-player room, shared collisions/stock/HUB, timeout/disconnect, field dynamics and online shot rules |
| Separate native client | 26 | 456 replicas, real WPILib DS/odometry/camera, autonomous selection and host loss |
| Dedicated server | 20 | Six remote places, ready barrier, clock, control isolation and invalid data rejection |
| **Total** | **276** | Local executed checks |

Receipts are in [verification](../verification/). The private configured robot fixture is excluded from the repository and download.

## Online evidence

The room test used **two separate native Unity processes** plus four lightweight TCP drivers: six robot identities in total. One native client used a real WPILib NT4 server, the supplied controller and swerve IO helpers, and a real WPILib odometry estimator. A TCP proxy added 40 ms in each direction (80 ms round trip). The full 456-piece field reached the client before enable. A second run used a separate native headless host and six TCP drivers. These were local processes; six physical computers and a real provider-to-provider WAN were not tested.

The tests verified stock-limited code firing, competing intakes claiming a piece only once, capacity, HUB updates to all clients, body collisions, stale-input braking, ready/phase barriers, chooser selection and host-loss disabling through DriverStationSim. Shared displacement reached wheel/gyro odometry; camera frames reached local robot code. Field signatures agree before joining. Online point/region shots also retain normal elevation and power limits. The REEFSCAPE checks used two actual field instances and verified that a physically swinging cage's snapshot reaches a kinematic replica.

## Retained physics and shots

The chassis has four support contacts and a fixed 55 kg training mass. The official ramp produced about 12.4 degrees of tilt. Loose FUEL is displaced through collisions; airborne drive has no traction. Six REEFSCAPE cages have hollow colliders and constrained suspension with estimated 12 kg mass.

The shot suite used a real WPILib server for units, references, live values and moving mechanisms. It verified unreachable-target stock preservation, regions/backward feeding, bounded scatter and code precedence. A Rigidbody passed about 2.6 cm from a configured point, and another entered the official HUB through actual collision geometry. Real shooter calibration still needs measurements.

## Visual review and limits

Native home, room entry and room lobby screenshots were inspected. The beveled buttons are retained; online play has a compact match sidebar. General CAD, shot and offline workflows were rerun.

Rooms use direct TCP or self-hosted reachable/VPN addresses. No managed public room service was deployed. Match scoring currently counts REBUILT HUB entries; full active/inactive HUB periods, climb/coral scoring and penalties are not implemented. Vision is geometric and uses configured camera geometry/latency. Prepared CAD and published robot values are required; generic bindings do not infer unpublished internals.

GitHub Actions checks repository integrity and public-package hygiene. It does not run the licensed Unity Editor or the private robot fixture.
