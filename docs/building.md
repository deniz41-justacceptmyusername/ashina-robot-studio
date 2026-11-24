# Build the Windows player

## Requirements

- Unity **6000.1.13f1** with Windows Build Support.
- A suitable activated Unity Editor license.
- PowerShell for the convenience script.
- WPILib 2026 only when building/running Java integration projects.

Open `Unity/` through Unity Hub and allow the declared packages to resolve. The package manifest includes glTFast, Newtonsoft JSON, and Unity Input System.

## Batch build

```powershell
./scripts/build-windows.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.1.13f1\Editor\Unity.exe"
```

The script calls `AshinaStudio.Editor.BatchBuild.Run` and writes the player under `build/Windows/`. It creates a portable bundle under `build/Ashina-Robot-Studio/` with the EXE at the root, plus generic examples and Java helpers. The Editor is launched hidden and the script checks the build exit code.

The source entry point can also be called directly with Unity's `-executeMethod`. `ASHINA_UNITY_OUTPUT` controls the target EXE path.

## Repository checks

```powershell
python scripts/verify-repo.py
```

This verifies the public source/package layout, example graph integrity, local documentation links, recorded test totals, embedded runtime references, and accidental personal paths or credential strings. It runs in GitHub Actions without a Unity license.

## Runtime checks

The player has opt-in local `--self-test --test-dir <directory>` and robot integration check entry points. The full player suite needs a real WPILib NT fixture and a Blockly client. A team-specific robot integration check additionally needs a configured robot project/model fixture. See [validation](validation.md) for what was actually run; repository CI does not substitute for those native checks.

Unity-generated caches, build outputs, local saved robots, and archives are ignored by Git. Keep your own CAD and team robot code in your chosen local workspace or repository.
