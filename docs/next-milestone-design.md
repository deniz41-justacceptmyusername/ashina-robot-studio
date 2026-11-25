# Ashina Robot Studio development milestone

Status: proposed. The PathPlanner delivery is the baseline for this design. Each change should be reviewable on its own. Version-2 robot files and the native Windows player remain supported.

## Candidate evaluation

| Candidate | Evidence in the current source | Decision |
| --- | --- | --- |
| Advanced telemetry | `Nt4Client` already discovers topics and stores typed latest values. `NetworkStudio` provides a picker, live values and mapping. History and a personalized dashboard are absent. | **Select first.** The live-data boundary already exists. |
| Match recording and replay | `MatchRoom` produces authoritative `MatchSnapshot` objects and has separate client visuals. Offline training holds equivalent state in `StudioApp` and `GamePieces`. | **Select second.** Reuse the existing snapshot contract and add persistence. |
| Driver analytics | A few training counters exist. There is no session history or normalized input series. | Sequence after replay so analysis consumes the same events. |
| More realistic mechanisms | `RobotAction` and live axes provide interpretable animation. Drivetrain, pieces and cages already use physics. Joint torque, contact and motor models need a different fidelity budget. | Defer. Select specific mechanisms after collecting evidence from training. |
| Autonomous trajectory visualization | `PathPlannerStudio` draws imported paths and supports alliance selection. | Satisfies basic practice. Keep precise timed or error overlays as follow-up work. |
| PathPlanner project integration | Direct `.auto` and `.path` import, named commands and local following work in individual practice. | Satisfies the immediate integration. Choreo, robot-specific execution and broader import are separate scope. |
| Robot configuration editor | `QuickSetup`, `ActionEditor` and scene handles already cover import, pivots, intake and shot geometry. | Address observed editing costs through recovery and undo. |
| Reusable mechanism presets | Generic parts and example robot projects exist. Intake and shot setup already guide common operations. | Defer until common action bundles can be defined without creating separate mechanisms per team. |
| Practice challenges | `TrainingWorld` owns scoring, staged pieces, resets and inventory. | Sequence after replay or analytics gives objective challenge feedback. |
| Multiplayer spectator improvements | The room and dedicated host provide the basis for shared observation. A dedicated spectator workflow has limited native controls. | Defer. Replay establishes reusable camera and presentation tools. |
| Autosave and undo/redo | `ProjectIO` validates the entire document. `StudioApp.Save` writes an atomic replacement. There is no recoverable draft or edit history. | **Select third.** Reuse validated project persistence and stable edit boundaries. |
| Navigation and onboarding | `LaunchHome`, `HomeMenu`, `QuickSetup` and the shot guide are already available. | Preserve the current workflow. Improve navigation for the selected features where needed. |

## 1. Telemetry workspace

### User experience

**Telemetri** opens a full-width native workspace beside the ordinary robot controls. The connection header shows the selected server and distinguishes connected, reconnecting, disconnected, and receiving no recent data. Add a panel, select announced NT topics, and choose a value card, boolean status, or a shared chart. Every row carries its actual topic, type, optional display unit, last update and text status. Rename, move or remove a panel. Charts include a live range, pause, clear, time-window controls and zoom/scroll. Unannounced values show an empty state. Discontinuities remain visible as gaps. Closing the view preserves configuration and bounded history for reopening.

### Architecture, data and integration

`Nt4Client` remains the sole NT socket, subscriber and topic cache. It publishes one lightweight reception timestamp and exposes its connection generation. The dashboard samples selected topics from `Nt4Client.Get`. `NtData.Scalar` handles numeric extraction with the same interpretation used by mechanism mappings. A single `TelemetryWorkspace` owns selected-topic history, while `TelemetryStudio` hosts the native view in the existing `StudioApp` partial pattern.

| Model | Fields and responsibility |
| --- | --- |
| `TelemetryConfig` | Version, ordered panels and default window. Separate `telemetry-v1.json` under the application's data directory. |
| `TelemetryPanel` | Stable ID, name, display kind and ordered series. |
| `TelemetrySeriesSpec` | Topic, optional struct field, unit, display label and color slot. |
| `TelemetrySample` | Local receipt time, numeric value and connection generation. Non-numeric events use readable current status. |
| `TelemetryHistory` | One fixed-capacity time ring per selected topic/field. Multiple panels reuse it. |

The flow is WPILib publisher → existing NT4 transport and decode → latest typed value → selected-topic sample ring → cards and cached chart. No dashboard component owns a socket or modifies robot control publications. Topic watchers added by the UI are logical only. Removing the last user of a series releases its history.

### Failure, performance and compatibility

Sample at up to 25 Hz and repaint graph data at a lower rate. Allocate rings and plotting buffers on configuration changes. Cache the latest data status and graph axes. Cap panel and series counts. Reconnection starts a new generation and preserves earlier samples as history. Topic disappearance or stale periodic updates shows missing or stale status. A stable boolean can be old while still representing its last reported value. Configuration loading validates bounds, falls back to an empty dashboard on corruption, and writes through a temporary file. No project schema change or dependency is required. A source update rate above chart sampling is aggregated into the latest available sample.

### Testing

Use pure tests for a ring overwrite, time lookup, units metadata and configuration validation. Use native PlayMode coverage for workspace lifecycle and range controls. Use a real WPILib NT4 fixture for typed numeric and boolean updates, reconnect, missing and stale topics, high-rate sampling and concurrent control mappings. Manually check Turkish text, keyboard focus, readable chart legends at 1024 px, and color-independent boolean states.

## 2. Match recording and playback

### User experience

Start or stop a named recording from the practice HUD or a room session. A session browser shows name, date, season, length, mode and whether it ended cleanly. Open a recording while editing. Play, pause, restart, stop or drag the timeline. Select follow-robot or overhead camera and adjust playback speed. Click event markers to jump to score changes, stock changes or phase transitions. Missing models fall back to measured chassis silhouettes with identity labels.

### Architecture, data and integration

At about 10 Hz, capture detached data from the existing authoritative `MatchRoom.State` or from offline `StudioApp`, `RobotChassis`, `Nodes`, and `GamePieces.Active`. Reuse snapshot primitives from `MatchProtocol` and the current IDs for pieces. Delta events come from score, inventory, roster and lifecycle changes. Capture only local driver input when opted in. A bounded writer queue receives data with no Unity object references. A background writer appends to a partial file. A reader builds a sparse time-to-offset index off-thread. The replay presents transform-only visuals in an isolated preview; it never calls `MatchRobot.Tick` or `StudioApp.ApplyActions`.

| Model | Fields and responsibility |
| --- | --- |
| `ReplayHeader` | Format version, UTC creation time, name, season, layout, source mode, robot reference and baseline dimensions. |
| `ReplayFrame` | Monotonic elapsed time, phase, per-robot pose and velocity, mechanism poses, per-piece pose, score and inventories. |
| `ReplayEvent` | Elapsed time, kind, actor IDs and bounded typed fields for score, stock, phase and entity changes. |
| `ReplayFooter` | Clean termination status, duration and optional counts. Partial files can omit it. |
| `ReplayIndex` | Bounded frame offsets, event offsets and key metadata used for seeking. |

WPILib continues to use the live simulation and existing NT4 instance. Recording observes their results from the simulated world. Replay renders recorded transforms and does not publish DriverStation, read NT, recreate contacts or rerun commands.

### Failure, performance and compatibility

Bound clip size, recording length, queued snapshots, row size and entity counts. Stop recording with a visible cause on I/O failure or overflow. A reader accepts a valid prefix of an interrupted file and clearly marks it incomplete. Reject invalid schema versions and bad frame shapes. Decode only nearby frames while seeking. Reuse visual shells by stable IDs and cap their number. Move file I/O and encoding away from the main thread. Relevant field and part assets are associated with the clip, with a clear fallback if missing. Existing robot projects and online wire format remain compatible. This is recorded-state interpolation.

### Testing

Use pure tests for prefix recovery, invalid records, ordering, index seeks and buffer bounds. Native tests cover short, long, empty and interrupted files, replay opening, speed, seek and missing assets. Validate both a practice session and the host or a client observing authoritative online state. Verify live stock, field state and DriveStation remain independent of the playback. Manually check both cameras and event markers on a visible recording.

## 3. Project recovery and bounded undo/redo

### User experience

After an interrupted edit session, the home screen offers **Taslağı kurtar** with the last good save time and project name. The main workspace exposes **Geri al** and **Yinele** plus keyboard shortcuts. One drag, completed number edit or accepted action configuration maps to one undo step. Save continues to show the actual selected project filename. Reopened recovery drafts are offered separately from the user's saved project.

### Architecture, data and integration

Reuse `ProjectIO.Validate`, `StudioApp.Save`, `LaunchHome.RememberProject`, and file restoration through `LoadProject`. Capture committed changes at `RuntimeGizmo` drag completion, `RecordNode`, action editor commits, and import completion. Begin with bounded metadata changes to parts and actions. Reference imported asset IDs rather than duplicating large CAD base64 for every undo step. Persist a validated recovery checkpoint separately at an idle boundary.

| Model | Fields and responsibility |
| --- | --- |
| `ProjectRecoveryEnvelope` | Version, project identity, original save reference, edit revision and last update timestamp. Payload is a validated version-2 project. |
| `ProjectEdit` | Operation kind, entity ID and small before/after payload. |
| `ProjectEditHistory` | Bounded undo/redo stacks, committed revision and asset references required by retained entries. |

Editor commit → capture changed state → validate draft → update scene through existing edit paths → mark recovery revision dirty → write checkpoint in the background. Debounce edits and schedule only one recovery write at a time. Swerve control, NT4 session state and Blender/CAD source files remain outside edit history. Action wiring stays attached to stable IDs.

### Failure, performance and compatibility

Use atomic replacement with the last known good backup. Avoid copying full CAD data on IMGUI frames. Decline stale recovery results after a new project is loaded. Cap retained edits and draft count. Handle import failure by retaining the previous scene and undo stack. Disable structural undo during a live match or driving session. Missing fields in older version-2 projects retain current behavior. Remove the recovery component independently if its feature is rolled back, retaining manual save and open.

### Testing

Test bounded history, a coalesced drag, redo invalidation, autosave queuing and recovery after a truncated file. Native tests cover recovering a project with CAD, settings or event mappings. Manual acceptance checks autosave after a model import, loss during a shot setup and repeated undo while switching selected parts.

## Reviewable implementation milestones

All paths in the table are relative to the repository. Runtime C# files live under `Unity/Assets/RobotStudio/Runtime/`.

| Milestone | Files to modify | Files to create | Dependencies and expected behavior | Main risk and acceptance | Rollback |
| --- | --- | --- | --- | --- | --- |
| T1. Typed telemetry and bounded history | `Nt4Client.cs`, existing native check entry point | `TelemetryWorkspace.cs`, `TelemetryChecks.cs` | Current NT4 receive cache and `NtData`. Reopening does not subscribe again. | Avoid stale values after generations change. Real fixture reconnect and high-rate bounded-ring checks pass. | Remove the optional workspace sampler and its timestamp field. |
| T2. Native telemetry view and persistence | `StudioGui.cs`, `StudioApp.cs`, `docs/networktables.md` | `TelemetryStudio.cs`, dedicated telemetry guide | T1. Add Turkish cards, graphs, diagnostics and panel editing. | Focus or draw overhead. Validate normal driving, small-window layout and persisted panel order. | Remove the top-bar entry and partial view. Saved configuration can remain inert. |
| R1. Recording format and storage | `StudioApp.cs`, `MatchStudio.cs` | `ReplayRecording.cs`, `ReplayFormat.cs`, `ReplayChecks.cs` | Current snapshots and state sources. Produce properly terminated files and recoverable incomplete prefixes. | Large queues or Unity objects crossing threads. Simulate sustained and interrupted recordings within bounds. | Remove optional sampling calls. No mutation to live snapshots. |
| R2. Playback presentation | `StudioGui.cs`, `StudioApp.cs` | `ReplayStudio.cs`, `ReplayPlayback.cs`, replay usage and format guide | R1. Timeline, markers, speed, play controls and camera modes. | Replay objects acquiring gameplay colliders. Verify isolated state and no NT4 writes from playback. | Disable playback entry and keep recorded files readable offline. |
| E1. Atomic recovery checkpoints | `StudioApp.cs`, `LaunchHome.cs`, `QuickSetup.cs` | `ProjectRecovery.cs`, recovery checks | Existing parse/validate and atomic persistence. Offer explicit recovery of a validated draft. | Accidentally replacing a normal save or large serialization stall. Test both interrupted write and near-limit project. | Remove scheduling and recovery entry. Preserve saved projects and existing drafts. |
| E2. Bounded reversible editor operations | `RuntimeGizmo.cs`, `StudioApp.cs`, `ActionEditor.cs` | `ProjectEditHistory.cs`, editor history checks | E1 revision model. Cover transform and action edits first. | Out-of-order IDs and invalidation by imported models. Round-trip multiple undo/redo sequences in a real player. | Disable edit history commands and retain existing manual editing. |

T1 is the first recommended implementation because it has a narrow read boundary and verifies the acquisition model before adding UI. R1 and E1 are independent of telemetry. Each later view waits for the storage and data contract it consumes.
