<p align="center">
  <img src="docs/assets/hero.svg" alt="Ashina Robot Studio — build, drive, repeat" width="100%">
</p>

<p align="center">
  <a href="https://github.com/deniz41-justacceptmyusername/ashina-robot-studio/releases/latest"><strong>Download for Windows</strong></a>
  &nbsp; · &nbsp; <a href="docs/quickstart.md">Quick start</a>
  &nbsp; · &nbsp; <a href="docs/networktables.md">Connect WPILib</a>
  &nbsp; · &nbsp; <a href="README.tr.md">Türkçe</a>
</p>

<p align="center">
  <img alt="Unity 6.1" src="https://img.shields.io/badge/Unity-6.1-101b2b?style=flat-square&amp;logo=unity&amp;logoColor=white">
  <img alt="Windows x64" src="https://img.shields.io/badge/Windows-x64-1578c8?style=flat-square">
  <img alt="NetworkTables 4" src="https://img.shields.io/badge/NetworkTables-NT4-25b8bb?style=flat-square">
  <img alt="Version 0.4.5" src="https://img.shields.io/badge/version-0.4.5-ff9d57?style=flat-square">
  <a href="LICENSE"><img alt="MIT license" src="https://img.shields.io/badge/license-MIT-8799ba?style=flat-square"></a>
  <a href="https://github.com/deniz41-justacceptmyusername/ashina-robot-studio/actions/workflows/verify.yml"><img alt="Repository checks" src="https://github.com/deniz41-justacceptmyusername/ashina-robot-studio/actions/workflows/verify.yml/badge.svg"></a>
</p>

# Your robot. Your practice field.

**Ashina Robot Studio is a native Unity desktop simulator for FRC driver training.** Assemble a robot from CAD, record mechanism motion, bind controls, and practice with physical game pieces. Connect a real WPILib simulation through NetworkTables when you want your own robot code to drive the session.

The focus is a quick path from a robot idea to a useful practice run: configurable mechanisms, approachable physics, and live robot data.

<table>
<tr>
<td width="33%"><strong>01 / BUILD</strong><br><br>Import GLB, STL, or OBJ. Set parents and pivots. Record motion with the 3D handles.</td>
<td width="33%"><strong>02 / CONNECT</strong><br><br>Browse NT4 topics, map live values to mechanisms, and feed controller input into WPILib.</td>
<td width="33%"><strong>03 / TRAIN</strong><br><br>Drive seasonal fields. Tune intake success, inventory, release attitude, and projectile speed.</td>
</tr>
</table>

![REBUILT field running in the native Unity desktop application](docs/images/rebuilt-training.png)

## Run in a minute

1. Download **Ashina-Robot-Studio-0.4.5-Windows.zip** from [Releases](https://github.com/deniz41-justacceptmyusername/ashina-robot-studio/releases/latest).
2. Extract the entire folder and open **Start.cmd** or **Baslat.cmd**.
3. Click **Sürüşe başla** and drive with **W A S D**, turn with **Q / E**, or use a supported gamepad.

Open **Coral-Lab.cmd** for the REEFSCAPE release-angle demo. Press **Space** after starting the session. The standalone player needs no Unity Editor or Node installation. Running Java robot code additionally requires the matching WPILib development tools.

| Control | Action |
| --- | --- |
| W A S D / left stick | Drive |
| Q / E / right-stick X | Turn |
| I / LB | Collect FUEL in the REBUILT example |
| Space / RB | Shoot FUEL in the REBUILT example |
| Space | Release coral in Coral Lab |
| Right mouse / wheel / middle mouse | Orbit / zoom / pan |
| F / Esc | Find robot / stop driving |

## A better practice feel in 0.4.5

The robot now has a dynamic chassis with four suspension contacts: it pitches and rolls on ramps, pushes loose FUEL through actual collisions, and loses drive traction when airborne. All six REEFSCAPE cages swing from a constrained suspension. Intake is a robot action attached to a mechanism, with its mouth settings tucked into a detail panel. The new compact robot-and-ball mark appears in the application and Windows icon.

## The release angle is yours

**Where a game piece points and where it travels are separate settings.** A coral can leave at a 55° attitude while its velocity follows a 25° trajectory. It can also remain horizontal while moving upward.

![Diagram separating coral attitude, trajectory, and angular speed](docs/assets/release-pose.svg)

In **Eylemler**, select an emission action and open **Ayrıntılar → Çıkış duruşu → Bağımsız açı**. Set the piece's inclination, heading, and axial rotation. These angles are relative to the selected robot part. Optional coral angular speed is separate again; set it to **0** for a release without initial tumbling.

```json
{
  "orientationMode": "custom",
  "piecePitch": 55,
  "pieceYaw": 0,
  "pieceRoll": 0,
  "pitch": 25,
  "yaw": 0,
  "coralSpin": 0
}
```

The 55° and 25° values are demonstration settings. Enter the angles for your own mechanism. After release, collisions and the chosen angular velocity determine the piece's motion.

## One main robot. Add only what moves.

The default project and both examples start with **one main robot**. **Robot modelini yükle** replaces its CAD without adding a second base. **Parça ekle** adds an optional mechanism. Fixed wheels and bumpers do not fill the part list. Existing custom mechanisms survive a main-model replacement; a failed import preserves the current project.

## Bring your own mechanisms

- **CAD workflow:** GLB, STL, and OBJ import; split GLB assemblies into editable parts; configure hierarchy, scale, and pivot.
- **Motion workflow:** record a start and an end pose, then bind the motion to a keyboard key, controller button, Blockly output, or NT value.
- **Intakes:** box or sphere contact regions, success probability per contact entry, and an inventory capacity.
- **Emitters:** game-piece type, release point, trajectory, velocity, independent initial attitude, and optional stock consumption through training mode.
- **Showcase mode:** enable **Sınırsız atış · gösterim** to emit with empty inventory. Turn it off to return to normal stock rules.
- **Portable projects:** save geometry, actions, camera configuration, and mappings in a single `.ashina.json` file.

## Drive your actual WPILib code

Start your robot's desktop simulation, open **WPILib / NT**, and connect to `127.0.0.1:5810` or your chosen server. You can also select a Java Gradle project in the application and launch it there.

| Path | What happens |
| --- | --- |
| Observe robot pose | Unity follows the pose produced by your robot simulation. |
| Apply chassis requests | Unity applies published robot-relative chassis-speed requests to its simple drive model. |
| Map mechanism data | Numeric values, booleans, arrays, and supported struct fields control recorded actions. |
| Forward controllers | The Java bridge exposes gamepads to existing WPILib Xbox-controller bindings. |
| Simulate vision | Geometric AprilTag results can feed PhotonCamera / PhotonPoseEstimator through the supplied bridge. |

NetworkTables carries published data. Your project still needs the appropriate simulation IO and topic mappings. The [integration guide](docs/networktables.md) covers pose ownership, controllers, vision, and shooter setup. Field2d, common pose/speed structs, and published nested struct schemas are supported; unknown raw/protobuf messages are retained without promising custom field decoding for every format.

## Three seasons. One workspace.

| Season | Included practice content |
| --- | --- |
| **2026 · REBUILT** | Official field geometry and tags, 456 physical FUEL positions, intake probability, HUB-entry counters, and optional ball return. |
| **2025 · REEFSCAPE** | Field and tags, physical coral/algae types, and the Coral Lab release-attitude demo. |
| **2024 · CRESCENDO** | Field, tags, and the note game-piece type. |

Default training capacities are **50** for REBUILT and **1** for REEFSCAPE. They are editable. This is free practice; complete FMS timing, officiating, and full competition-rule enforcement are outside the current implementation.

![REEFSCAPE field and the generic demonstration robot](docs/images/coral-lab.png)

## Built, exercised, documented

Version **0.4.5** passed **157 local checks**: 131 general player/physics/NT/Blockly checks, 13 checks using a configured team robot integration, and 13 dedicated chassis/cage physics checks. The build completed with **0 errors and 0 warnings**. [Read the validation record](docs/validation.md).

Repository CI checks source layout, example integrity, documentation links, and public-package hygiene. It does not claim to run the licensed Unity Editor or a team's private robot fixture. Physical controller models have not all been tested. Vision is geometric rather than pixel image processing; drive and game-piece collisions are training approximations.

## Explore the source

```text
Unity/             Unity 6000.1.13f1 project
integration/java/  WPILib controller, vision, and ideal swerve IO helpers
examples/          Generic REBUILT and REEFSCAPE robot projects
docs/              Quick start, architecture, integration, and screenshots
scripts/           Windows build and repository verification
verification/      Sanitized local validation records
```

Open `Unity/` in Unity Hub, or use the [Windows build instructions](docs/building.md). See [CONTRIBUTING.md](CONTRIBUTING.md) for a focused contribution workflow.

## License & credits

Project code is available under the **MIT License**. Third-party components and seasonal assets keep their own licenses; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Built with Unity, [WPILib](https://github.com/wpilibsuite/allwpilib), [AdvantageScope Assets](https://github.com/Mechanical-Advantage/AdvantageScopeAssets), [PhotonVision](https://github.com/PhotonVision/photonvision), and [Blockly](https://github.com/google/blockly). This is an independent community project, unaffiliated with FIRST.
