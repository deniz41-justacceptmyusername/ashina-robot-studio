# One model, optional mechanisms, one shot action

The 5–10 minute goal assumes a prepared model and already-published simulation values. CAD cleanup, adding simulation IO and real-world shot calibration are separate work. No universal robot-code import is claimed.

1. **Home → Yeni robot hazırla.** Load the main model, choose metre/millimetre scale and fit it to the carpet. Fixed wheels and bumpers stay inside that model.
2. **Optional mechanisms.** Load a separate moving part only when you need its motion. Set its parent, pivot and placement. Any rotation/translation axis can use a live NT value. Select its units; use scale −1 when the code and CAD axis directions differ. Add cameras in **Saha** with the robot code's camera name, extrinsics and FOV.
3. **One emitter.** Choose the part the game piece leaves. Set the local exit point, normal elevation, trigger and speed. The exit must clear the approximate chassis collider; new emitters start outside it. Use the NT picker for live pitch, yaw, speed or fire permission. The speed-unit helper converts wheel RPS/RPM using diameter, gearing and an adjustable transfer factor; this is a starting estimate.
4. **Test and save.** Temporarily enable unlimited shots. Verify axis direction and speed at several distances, then save and restore normal stock rules.

## A rotating mechanism

There is no dedicated turret subsystem in the simulator. Parent a moving part to the main robot and bind `rotation.z` to its yaw value. Bind another part to the vertical axis if a hood moves separately. Degree, radian and turn units are supported. A field-relative yaw value subtracts the parent's current heading, so the mechanism can stay pointed while the chassis turns.

If the geometry already follows the measured joint angles, leave the emitter's extra yaw/pitch at its local nozzle offsets. Do not bind the same angle again at the emitter. If no moving CAD is available, bind the emitter direction directly and select the appropriate part/robot/field reference.

## Offline targets and feeding

Choose **Bu noktaya at**, pick XY in the field, and set target height. The normal elevation remains unchanged. The solver chooses heading and relative launch speed using inherited muzzle motion, gravity and the game's damping. Maximum speed bounds the solution; an unreachable shot creates no piece and consumes no inventory.

Choose **Bölgeye göre hedef** to draw two rectangle corners and then that region's target. Each region has its own target height. Regions are checked in list order; the first match wins. Outside all regions, shots are blocked unless the explicit default-target option is enabled. Feeding toward your side uses this same action with a low target height.

Scatter is a bounded cone around the nominal direction. The nominal elevation remains configured; individual scattered shots can deviate. After launch, pieces receive no target-seeking correction and can hit the field or other objects.

When configured NT pitch/yaw/speed values are available, robot code owns the shot solution and offline point assistance does not replace it. Code-owned angles use explicit units and reference frames. Power and scatter still apply.

## Limits

This is an approachable practice model, not a calibrated flywheel/contact/drag laboratory. A real robot's area-to-speed or distance-to-RPM table still needs real measurements. Camera image processing is geometric simulation. Generic mechanisms and bindings reduce setup steps; they cannot infer unpublished code values or fix arbitrary CAD coordinate systems automatically.
