# One model, optional mechanisms, one shot action

The 5–10 minute goal assumes a prepared model and already-published simulation values. CAD cleanup, adding simulation IO and real-world shot calibration are separate work. No universal robot-code import is claimed.

1. **Home → Yeni robot hazırla.** Load the main model, choose metre/millimetre scale and fit it to the carpet. Fixed wheels and bumpers stay inside that model.
2. **Optional mechanisms.** Load a separate moving part only when you need its motion. Set its parent, pivot and placement. Any rotation/translation axis can use a live NT value. Select its units; use scale −1 when the code and CAD axis directions differ. Add cameras in **Saha** with the robot code's camera name, extrinsics and FOV.
3. **One emitter.** Choose the part the game piece leaves. Set the local exit point, normal elevation, trigger and speed. The exit must clear the approximate chassis collider; new emitters start outside it. Use the NT picker for live pitch, yaw, speed or fire permission. The speed-unit helper converts wheel RPS/RPM using diameter, gearing and an adjustable transfer factor; this is a starting estimate.
4. **Test and save.** Temporarily enable unlimited shots. Verify axis direction and speed at several distances, then save and restore normal stock rules.

## A rotating mechanism

There is no dedicated turret subsystem in the simulator. Parent a moving part to the main robot and bind `rotation.z` to its yaw value. Bind another part to the vertical axis if a hood moves separately. Degree, radian and turn units are supported. A field-relative yaw value subtracts the parent's current heading, so the mechanism can stay pointed while the chassis turns.

The nozzle position follows its attached mechanism. For a rotating part, select the mechanism as the horizontal reference and leave the yaw offset at zero if the part already points in the desired direction. Enter or bind the launch elevation measured from horizontal, including the hood angle if the hood moves. You can bind direction directly and choose a robot or field heading when moving CAD is unavailable.

## Offline targets and feeding

Choose **Tek hedefe at**, pick XY in the field, and set target height. The normal elevation remains unchanged. The solver chooses heading and relative launch speed using inherited muzzle motion, gravity and the game's damping. An unreachable shot creates no piece and consumes no inventory.

Choose **Bölgeye göre farklı hedeflere at** to draw two rectangle corners and then select that region's target. A click on the carpet sets a low target. The target height can still be edited. In overlapping regions, the smallest region wins. Equal-size regions favor the last one added. Outside all regions, a shot needs the explicit fallback option.

Scatter is a bounded cone around the nominal direction. Individual shots can deviate from the configured elevation. During release, the piece clears the firing robot's bumper. The field and other robots still collide with the piece, and the launcher's own collision returns once the piece is clear.

Live NT pitch and yaw control shots in **Ayarladığım yönde at**. Point and region modes keep their selected field target and configured elevation. Live speed caps the calculated shot.

## Limits

This is an approachable practice model, not a calibrated flywheel/contact/drag laboratory. A real robot's area-to-speed or distance-to-RPM table still needs real measurements. Camera image processing is geometric simulation. Generic mechanisms and bindings reduce setup steps; they cannot infer unpublished code values or fix arbitrary CAD coordinate systems automatically.

Import a PathPlanner route and practice with no NT4 connection. [Standalone autonomous practice](standalone-auto.md).
