# Standalone PathPlanner autonomous practice

In the Windows studio, open **Saha → PathPlanner otonomu → PathPlanner .auto / .path yükle**. For a complete routine, select the `.auto` from `src/main/deploy/pathplanner/autos` and leave its named `.path` files in the sibling `paths` folder. For a route by itself, select the `.path`. The route appears in blue on the 3D field.

1. Select the alliance. Red rotates the route and heading 180° about the current field center.
2. Select **Rota başlangıcına yerleştir** when the robot should start at the first waypoint. The initial selection reflects `resetOdom` for `.auto` files.
3. For each named command or path marker, select an existing Ashina action. An exact action-name match is used by default. Use **Eylem seç** to leave a command unassigned.
4. Select **Bağımsız otonomu başlat**. The simulator drives the chassis using the existing collision physics. Press **Esc** to end the session. Saving the robot embeds the parsed route and mappings in its version-2 project.

The importer supports PathPlanner 2025 and 2026 file formats, cubic waypoints, linear constraints, holonomic rotations, waits, named commands and sequential or nested command groups with one active drive branch. A timed path marker can hold an Ashina action over its configured span. Ashina actions replace Java named-command bodies during standalone practice. Named-command timing and the calculated velocity profile are training approximations. Choreo trajectories, dynamic pathfinding and PathPlanner point-toward overrides require their corresponding runtime setup. The standalone player runs this route in individual practice. Online match autonomous remains available through the existing WPILib chooser.

Malformed routines or missing path dependencies leave the previously loaded routine in place. The app validates the route against the selected field before starting.

PathPlanner references [Editing Paths and Autos](https://pathplanner.dev/gui-editing-paths-and-autos.html) and [Named Commands](https://pathplanner.dev/pplib-named-commands.html).
