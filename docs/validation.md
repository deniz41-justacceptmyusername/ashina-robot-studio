# Validation record · v0.4.12

Built locally with Unity **6000.1.13f1**: **0 errors, 0 warnings**.

| Suite | Passed | Scope |
| --- | ---: | --- |
| General native player | 131 | CAD, project/motion persistence, stock, fields, real NT4/Blockly |
| Configured team robot | 13 | Java/YAGSL, Xbox input, intake, shooter, HUB, odometry and PhotonVision |
| Chassis/cage physics | 18 | Packed FUEL at 40 kg, low and tall TRENCH clearance, ramps, airborne traction and cages |
| Home and shots | 59 | Home/setup, region aiming, live parameters, standalone PathPlanner import, event mapping and physical following |
| Online host | 43 | Six-player room, shared collisions/stock/HUB, timeout/disconnect, field dynamics and online shot rules |
| Separate native client | 26 | 456 replicas, real WPILib DS/odometry/camera, autonomous selection and host loss |
| Dedicated server | 20 | Six remote places, ready barrier, clock, control isolation and invalid data rejection |
| **Total** | **310** | Local executed checks |

Receipts are in [verification](../verification/). The private configured robot fixture is excluded from the repository and download.

## Online evidence

The full Internet-room receipt is from v0.4.11. On v0.4.12 a native client joined through Cloudflare and the host observed both shots. The client timed out waiting for the updated stock and shot count. The direct six-player suite passed on v0.4.12.

The room test used **two separate native Unity processes** plus four lightweight TCP drivers: six robot identities in total. One native client used a real WPILib NT4 server, the supplied controller and swerve IO helpers, and a real WPILib odometry estimator. A TCP proxy added 40 ms in each direction (80 ms round trip). The full 456-piece field reached the client before enable. A second run used a separate native headless host and six TCP drivers. These were local processes; six physical computers and a real provider-to-provider WAN were not tested.

The tests verified stock-limited code firing, competing intakes claiming a piece only once, capacity, HUB updates to all clients, body collisions, stale-input braking, ready/phase barriers, chooser selection and host-loss disabling through DriverStationSim. Shared displacement reached wheel/gyro odometry; camera frames reached local robot code. Field signatures agree before joining. Online point/region shots also retain normal elevation and power limits. The REEFSCAPE checks used two actual field instances and verified that a physically swinging cage's snapshot reaches a kinematic replica.

## Retained physics and shots

The chassis has four support contacts and a fixed 55 kg training mass. The official ramp produced about 12.4 degrees of tilt. Loose FUEL is displaced through collisions. A packed FUEL pile lifted the 40 kg validation chassis about 6.2 cm before the fix and less than 1 mm after it. The four REBUILT TRENCH arms preserve 56.52 cm of clearance. Separately, airborne drive has no traction. Six REEFSCAPE cages have hollow colliders and constrained suspension with estimated 12 kg mass.

The shot suite used a real WPILib server for units, references, live values and moving mechanisms. It verified unreachable-target stock preservation, regions/backward feeding, bounded scatter and code precedence. The suite checks that a Rigidbody passes near a configured point and enters the official HUB through actual collision geometry. Real shooter calibration still needs measurements.

## Visual review and limits

The redesigned native home was visually inspected with empty bleachers, a translucent lower-left menu, large buttons and the regional hall/lighting. The home uses a separate mesh-only presentation, so the simulated robot stays disabled while the menu is open. Venue meshes contain no colliders or rigidbodies; the physics, vision, CAD, Blockly and online suites were rerun. Workspace button styling remains unchanged.

Rooms support temporary public invitations through Cloudflare Quick Tunnels, direct TCP and self-hosted reachable/VPN addresses. No permanent public host was deployed. Match scoring currently counts REBUILT HUB entries; full active/inactive HUB periods, climb/coral scoring and penalties are not implemented. Vision is geometric and uses configured camera geometry/latency. Prepared CAD and published robot values are required; generic bindings do not infer unpublished internals.

GitHub Actions checks repository integrity and public-package hygiene. It does not run the licensed Unity Editor or the private robot fixture.
