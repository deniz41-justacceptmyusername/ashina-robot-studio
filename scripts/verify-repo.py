"""Portable repository verification; this does not run the native Unity test suite."""
import json
import math
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
errors = []
IGNORED_DIRS = {".git", "library", "temp", "logs", "usersettings", "obj", "build", "__pycache__"}


def public_file(file):
    return file.name != "unity-build-result.json" and not any(part.lower() in IGNORED_DIRS for part in file.relative_to(ROOT).parts)

def check(ok, message):
    if not ok:
        errors.append(message)

def read_json(file):
    return json.loads(file.read_text(encoding="utf-8-sig"))

required = ["LICENSE", "README.md", "README.tr.md", "THIRD_PARTY_NOTICES.md",
            "docs/images/rebuilt-training.png", "docs/images/coral-lab.png",
            "docs/assets/hero.svg", "docs/assets/release-pose.svg", "docs/images/home.png", "docs/images/quick-setup.png", "docs/images/target-practice.png",
            "Unity/Assets/RobotStudio/Runtime/StudioApp.cs",
            "Unity/Assets/RobotStudio/Runtime/ReleasePose.cs",
            "Unity/Assets/RobotStudio/Runtime/MatchRoom.cs", "Unity/Assets/RobotStudio/Runtime/MatchTransport.cs", "Unity/Assets/RobotStudio/Runtime/MatchFieldSync.cs", "docs/online.md", "docs/images/online-entry.png", "docs/images/online-lobby.png",
            "Unity/Packages/manifest.json", "Unity/ProjectSettings/ProjectVersion.txt"]
for item in required:
    check((ROOT / item).is_file(), f"Missing file: {item}")

for file in ROOT.rglob("*.json"):
    if not public_file(file):
        continue
    try:
        read_json(file)
    except (ValueError, UnicodeError) as exc:
        errors.append(f"Invalid JSON: {file.relative_to(ROOT)}: {type(exc).__name__}")

for file in (ROOT / "examples").glob("*.ashina.json"):
    project = read_json(file)
    check(project.get("format") == "ashina-robot-studio" and project.get("version") == 2,
          f"Invalid example format: {file.name}")
    parts = {part["id"]: part for part in project["parts"]}
    check(len(parts) == 1, f"Public example should have one main robot: {file.name}")
    check("chassis" in parts, f"Missing chassis: {file.name}")
    check(len(parts) == len(project["parts"]), f"Duplicate part ID: {file.name}")
    for part in parts.values():
        seen = set()
        current = part["id"]
        while current is not None:
            if current in seen or current not in parts:
                errors.append(f"Invalid hierarchy: {file.name}/{part['id']}")
                break
            seen.add(current)
            current = parts[current].get("parent")
    ids = set()
    for action in project["actions"]:
        check(action["id"] not in ids, f"Duplicate action ID: {file.name}")
        ids.add(action["id"])
        check(action["partId"] in parts, f"Unknown action part: {file.name}")
        check(action.get("orientationMode", "auto") in ("auto", "custom"),
              f"Invalid attitude mode: {file.name}")
        for key in ("pitch", "yaw", "piecePitch", "pieceYaw", "pieceRoll", "coralSpin"):
            check(math.isfinite(action.get(key, 0)), f"Non-finite {key}: {file.name}")
    check(sum(project["inventory"].values()) == 0, f"Public example should start empty: {file.name}")
    check(project["training"]["capacity"] >= 1, f"Invalid capacity: {file.name}")
    check(not project["network"].get("autoConnect", False), f"Example auto-connects: {file.name}")
    check(not project.get("assets"), f"Unexpected custom CAD payload: {file.name}")

release = read_json(ROOT / "verification/release.json")
general = read_json(ROOT / "verification/player-checks.json")
robot = read_json(ROOT / "verification/robot-integration.json")
check(general["success"] and len(general["passed"]) == general["count"] == 131,
      "General validation receipt does not match release")
check(robot["success"] and len(robot["passed"]) == robot["count"] == 13,
      "Robot validation receipt does not match release")
physics = read_json(ROOT / "verification/physics-polish.json")
check(physics["success"] and len(physics["passed"]) == physics["count"] == release["physicsChecks"], "Physics validation receipt mismatch")
features = read_json(ROOT / "verification/home-and-shots.json")
check(features["success"] and len(features["passed"]) == features["count"] == release["featureChecks"], "Home/shot validation receipt mismatch")
online = [read_json(ROOT / ("verification/" + name)) for name in ("online-host.json", "online-client.json", "online-dedicated.json")]
for suite in online:
    check(suite["success"] and len(suite["passed"]) == suite["count"], "Online validation receipt mismatch")
check(release["onlineChecks"] == sum(suite["count"] for suite in online), "Online total mismatch")
internet = read_json(ROOT / "verification/online-internet.json")
check(internet["success"] and internet["host"]["success"] and internet["client"]["success"], "Internet room validation receipt mismatch")
check(release["multiplayerImplemented"] and release["onlinePlayers"] == 6, "Online release metadata mismatch")
check(release["totalChecks"] == general["count"] + robot["count"] + physics["count"] + features["count"] + release["onlineChecks"],
      "Validation total mismatch")
check(('bundleVersion="'+release['version']+'"') in (ROOT / "Unity/Assets/RobotStudio/Editor/BatchBuild.cs").read_text(),
      "Source version does not match release")

for file in ROOT.rglob("*.md"):
    if not public_file(file):
        continue
    for target in re.findall(r"!?\[[^\]]*\]\(([^)\s]+)\)", file.read_text(encoding="utf-8-sig")):
        if target.startswith(("http://", "https://", "#", "mailto:")):
            continue
        local = target.split("#", 1)[0]
        check((file.parent / local).exists(), f"Broken local link: {file.relative_to(ROOT)} -> {local}")

html = ROOT / "Unity/Assets/StreamingAssets/blockly/index.html"
for target in re.findall(r'(?:src|href)="([^"]+)"', html.read_text(encoding="utf-8-sig")):
    if target.startswith(("http", "#", "data:")):
        continue
    check((html.parent / target.split("?", 1)[0]).exists(), f"Missing Blockly runtime asset: {target}")

private = re.compile(r"Users[\\/]+nsyls|harun-obj|github_pat_[A-Za-z0-9_]+|gh[pousr]_[A-Za-z0-9]{20,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----", re.I)
for file in ROOT.rglob("*"):
    if not file.is_file() or not public_file(file):
        continue
    relative = file.relative_to(ROOT)
    check(file.stat().st_size < 95_000_000, f"Oversized Git file: {relative}")
    if file.suffix.lower() in (".cs", ".java", ".js", ".json", ".md", ".ps1", ".yml"):
        text = file.read_text(encoding="utf-8-sig", errors="replace")
        # The scanner's own regex contains an intentional local-path sentinel.
        check(not private.search(text), f"Personal path or credential pattern: {relative}")
check(not (ROOT / "SavedRobots").exists(), "Personal saved robots included")
check(not (ROOT / "integration/YAGSL-Simulation").exists(), "Team robot fixture included")
check((ROOT / "Unity/Assets/StreamingAssets/licenses/AdvantageScopeAssets.txt").exists(),
      "Missing seasonal-asset license")

if errors:
    raise SystemExit("\n".join(f"FAIL: {error}" for error in errors))
print("PASS: source layout, 3 robot examples, runtime assets, docs, native and online receipts, and public-package hygiene")
