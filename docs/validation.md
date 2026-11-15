# Validation record · v0.4.4

The delivered application was built locally with Unity **6000.1.13f1** on Windows. The final build reported **0 errors and 0 warnings**.

| Suite | Passed | Scope |
| --- | ---: | --- |
| General native player | 125 | CAD import, graph/motion, project persistence, three fields, game-piece physics, inventory, release attitude, NT4 and real Blockly integration |
| Configured team robot | 13 | Actual Java/YAGSL launch, Xbox input, intake, shooter, HUB entry, odometry, and PhotonVision pose processing |
| **Total rerun for 0.4.4** | **138** | Local native checks |

Machine-readable records are in [verification](../verification/). The configured robot scenario recorded 7 pickups, 5 shots, and 1 physical HUB goal. Its private team model/code fixture is not distributed in this public repository. The generic examples demonstrate the workflow and are not claimed to be that fixture.

## Independent initial attitude

The player suite verified:

- A rotated mount with a 12° trajectory and a separate 55° / 35° body attitude.
- Changing trajectory without changing custom body pose.
- Changing body pose without changing linear speed/direction.
- Axial rotation independent of the coral's long-axis direction.
- A horizontal coral launched upward and a vertical coral launched horizontally.
- The same custom pose settings applied to the Note game piece.
- Save/reload of pose settings and compatibility with older files missing those settings.

It also verified free coral rotation and side-on settling after a real ground contact. Default angular speed is zero for new actions; explicitly saved values are retained.

## Inventory and terrain

Tests cover stock-limited emission, optional unlimited mode, capacity 50/1, one intake probability attempt per contact entry, and refilling space after a shot. The actual controller traversed the restored REBUILT ramp and settled on the carpet. The fallback floor is hidden and lowered to avoid overlapping visible carpet surfaces.

## Integration evidence

The general suite used a real WPILib NT4 server and real Blockly UI. It exercised primitive/array types, nested struct schemas, topic removal, reconnect, and data-driven mechanism mapping. The robot suite exercised existing team command and pose-estimation paths.

Twelve Java bridge checks from the earlier 0.4.2 validation remain applicable to unchanged helper code; they are not added to the 138-check 0.4.4 total.

## Practical limits

No exhaustive physical gamepad-brand validation or additional manual UI acceptance is claimed for 0.4.4. Geometric vision does not simulate image formation/calibration noise. Chassis and object contacts are training approximations; full FMS/refereeing, detailed electrical systems, and universal vendor-device emulation are not implemented.

GitHub Actions runs the separate repository verification script. A green repository workflow is not a claim that GitHub ran the native 138-check suite.
