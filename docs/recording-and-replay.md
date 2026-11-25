# Session recording and replay

Select **Kayıtlar** from the studio or online toolbar. Enter a name and select **Kaydı başlat**, then close the panel and drive. The recording continues while the panel is closed. Select **Kaydı bitir** to finish. An optional checkbox includes the local primary controller's axes, buttons and POV. Leave it unchecked for recordings that only need simulation state.

Return to editing or leave the room before opening a recording. Choose a saved session in **Kayıtlar**, or open an `.ashina-replay` file. Use **Oynat**, **Duraklat**, **Baştan başlat** or **Durdur**. Drag the timeline or click an event tick or event button to seek. Speeds range from ¼× to 4×. Switch between a follow camera and an overhead camera. The displayed speed, score and stock come from the recorded frames.

The video-like view interpolates recorded transforms. Commands, physics and NetworkTables are not rerun by playback. If the associated local project is open, replay can use its CAD and mechanism hierarchy. Other robots and missing CAD use a chassis-sized representation. The recorded season and AprilTag layout determine the field. The replay uses the static seasonal field geometry.

## File format · 1

The file is UTF-8 with newline-delimited records. A tab separates the record kind and its payload. Timed records place a decimal timestamp in seconds between the kind and the JSON payload. Coordinates and quaternions use the native Unity world frame used by Ashina match snapshots. The recording format is independent of the version-2 `.ashina.json` format.

| Record | Payload |
| --- | --- |
| `H` | Version, format name, display name, UTC creation time, season, field layout, source mode, project association and robot dimensions. |
| `F` | Strictly increasing timestamp, phase, scores, each robot's pose and velocity, local mechanism transforms, stock, shot counters, game-piece identities, poses and velocity. The local controller record is optional. |
| `E` | Timestamp, event kind, label and optional actor or added/removed piece IDs. Events describe phase, roster, score, shots, stock and piece lifecycle changes. |
| `Z` | Final frame count and duration. A clean close moves the file from `.partial` to `.ashina-replay`. |

The application samples up to ten times per second. The writer queue holds at most 48 pending frames and writes on a worker thread. Recording stops at the configured safety bounds of 30 minutes, about 350 MiB, or 18,001 frames. A writer that cannot keep up reports the problem and leaves a recoverable partial file. A partial recording can open through its complete prefix. An empty recording has no playable state. Unknown formats and invalid frame data produce an error.

An index stores time and byte offsets. Playback reads a nearby pair of frames and reuses game-piece visuals. The 3D replay has a separate camera and mesh-only objects. Playback leaves the live project's model, inventory, and objects in their own scene.

Recorded states describe the simulation at its capture cadence. Very short events between samples and exact physics reproduction are outside this format.

## Validation

Native checks create an actual practice recording, exercise stop, reload, seek, playback speed, both cameras and live-world isolation. Additional fixtures cover malformed, empty, interrupted and longer recordings and a missing robot asset. The direct six-player match test records the host and verifies shared robot identities and scoring events in the saved file. [Validation receipts](../verification/).
