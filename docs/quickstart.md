# First practice run

## Windows player

Download the ZIP from [Releases](https://github.com/deniz41-justacceptmyusername/ashina-robot-studio/releases/latest), extract it, and run **Start.cmd**. Keep `Windows/`, `examples/`, and the launcher together.

The REBUILT example starts with empty inventory. Click **Sürüşe başla**, drive to the FUEL, and hold **I** to collect. **Space** shoots the stored FUEL. Default intake success is 70% per new contact entry, with a capacity of 50.

**Coral-Lab.cmd** opens a separate REEFSCAPE demonstration. Start driving, then press **Space**. It uses unlimited demonstration mode, a 55° piece attitude, a 25° trajectory, and zero initial angular speed. These are editable example values.

## Create a robot

1. Click **Yeni**, select the chassis, and import a **GLB**, **STL**, or **OBJ** from **Parçalar**.
2. Set the parent, scale, position, and pivot. GLB assemblies can be split into editable subparts.
3. Select a part. In **Eylemler**, add a motion and record its start and end poses.
4. Bind a key or gamepad button. A live NT value can also trigger the action or position it between the endpoints.
5. Add capture and emission actions. Save the result as an `.ashina.json` project.

The current application UI is Turkish; [README.tr.md](../README.tr.md) maps the labels to the workflow.

## Configure an emission

**Uçuşun yukarı açısı**, **Uçuşun yana açısı**, and **Çıkış hızı** specify velocity. Choose **Eşyanın çıkış duruşu → Bağımsız açı** to specify the initial body pose separately.

For coral, custom inclination 0° is horizontal and 90° is vertical relative to the selected part. **Coral dönüş hızı** controls initial angular speed in degrees per second. Zero does not freeze the object: a collision can still rotate it.

**Sınırsız atış · gösterim** ignores stock for emission and leaves existing inventory untouched. Turning it off restores normal stock consumption. The setting is saved with the project.

## Bring in robot code

For a desktop WPILib simulation, follow [the NetworkTables guide](networktables.md). Use a single drive-physics owner, match the camera configuration, and publish the telemetry you want to map.

## Main robot and optional mechanisms

Start with **Ana robot**. **Robot modelini yükle** replaces its model. Use **Parça ekle** only for a mechanism you want to edit or move separately. GLB assemblies stay together unless you explicitly split them. Main-model replacement retains your added mechanisms and actions. A failed import leaves the existing project intact.
