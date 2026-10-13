"""Final source/document/runtime preservation checks; writes evidence only."""
import hashlib
import json
import re
import subprocess
from pathlib import Path
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent

docs = [
    "Docs/ActiveTasks/spiri-alcohol-exposure.md",
    "Docs/StructureMemory/AlcoholExposure.md",
    "Docs/RefactorBacklog/AlcoholPuddleLegacyAuthoring.md",
    "outputs/spiri-alcohol-exposure-2026-10-12/README.md",
]
broken = []
checked_links = 0
for name in docs:
    path = ROOT / name
    for target in re.findall(r"\]\(([^)]+)\)", path.read_text(encoding="utf-8-sig")):
        target = target.strip("<>").split("#", 1)[0]
        if not target or "://" in target:
            continue
        checked_links += 1
        if not (path.parent / unquote(target)).resolve().exists():
            broken.append({"document": name, "target": target})

sources = [
    "Assets/_Project/Runtime/Features/Map/Puddles/AlcoholPuddleArea.cs",
    "Assets/_Project/Runtime/Features/Player/Input/PlayerIntentInput2D.cs",
    "Assets/_Project/Runtime/Features/Player/Scene/PlayerRuntimeCaptureCoordinator.cs",
    "Assets/_Project/Runtime/Features/Player/Status/CombatBuffDebuffApplier.cs",
    "Assets/_Project/Runtime/Features/Player/Status/PlayerAlcoholExposure.cs",
    "Assets/_Project/Runtime/Presentation/Map/Puddles/AlcoholExposurePlayerVisual.cs",
    "Assets/_Project/Art/Shaders/AlcoholMugFill.shader",
    "Assets/_Project/Editor/Tools/Data/SpiriAlcoholRewardAuthoring.cs",
    "Assets/_Project/Tests/PlayMode/Procedural/AlcoholExposurePlayModeTests.cs",
    "Assets/_Project/Tests/PlayMode/Procedural/AlcoholExposurePlayerVisualPlayModeTests.cs",
    "Assets/_Project/Tests/PlayMode/Procedural/AlcoholPuddleContactPlayModeTests.cs",
]
trailing = []
for name in sources + docs:
    for number, line in enumerate((ROOT / name).read_text(encoding="utf-8-sig").splitlines(), 1):
        if line.rstrip() != line:
            trailing.append({"path": name, "line": number})
diff = subprocess.run(["git", "diff", "--check", "--", *sources,
    "Assets/_Project/Prefabs/Player/PF Player.prefab",
    "Assets/_Project/Data/Abilities/Effects/GE_Puddle_AlcoholBuff.asset",
    "Assets/_Project/Data/Dialogue/NPC/DragonBossNpc.asset"],
    cwd=ROOT, capture_output=True, text=True)

runtime = json.loads((OUT / "preserved-runtime.json").read_text(encoding="utf-8-sig"))
runtime_unchanged = all(hashlib.sha256(Path(row["path"]).read_bytes()).hexdigest().upper() == row["hash"] for row in runtime)
preservation = json.loads((OUT / "preservation-audit.json").read_text(encoding="utf-8-sig"))
guids = json.loads((OUT / "global-guid-audit.json").read_text(encoding="utf-8-sig"))
tests = json.loads((OUT / "native-test-summary.json").read_text(encoding="utf-8-sig"))

result = {
    "markdown_links": {"checked": checked_links, "broken": broken},
    "source_trailing_whitespace": trailing,
    "scoped_diff_check": {"exit_code": diff.returncode, "output": diff.stdout},
    "runtime_preserved": {"checked": len(runtime), "passed": runtime_unchanged},
    "preservation_audit_passed": preservation["passed"],
    "global_guid_audit_passed": guids["passed"],
    "native_unique_cases": tests["uniqueCases"],
    "native_passed_cases": tests["passed"],
    "msbuild": {"Editor.csproj": 0, "ProceduralPlayModeTests.csproj": 0,
        "evidence": "Final command exits observed after native execution; editor-msbuild-final.log/tests-msbuild-final.log."},
}
result["passed"] = not broken and not trailing and diff.returncode == 0 and runtime_unchanged and preservation["passed"] and guids["passed"] and tests["passed"] == tests["uniqueCases"]
(OUT / "final-checks.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(result, ensure_ascii=False, indent=2))
raise SystemExit(0 if result["passed"] else 1)
