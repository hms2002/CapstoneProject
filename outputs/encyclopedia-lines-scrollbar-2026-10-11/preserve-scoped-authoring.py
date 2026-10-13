"""Stage only Unity-authored creature changes, preserving task-baseline records."""
import json
import pathlib
import re

root = pathlib.Path.cwd()
evidence = root / "outputs/encyclopedia-lines-scrollbar-2026-10-11"
prefab = root / "Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab"
before = (evidence / "before/EncyclopediaUI.prefab").read_text(encoding="utf-8-sig")
current = prefab.read_text(encoding="utf-8-sig")
pattern = re.compile(r"--- !u!(\d+) &(-?\d+)[\s\S]*?(?=\n--- !u!|\Z)")
original = {m[2]: m[0] for m in pattern.finditer(before)}
authored = {m[2]: m[0] for m in pattern.finditer(current)}
allowed = set((evidence / "authoring-allowed-ids.txt").read_text().splitlines())
assert set(original) <= set(authored), "Existing prefab records lost."
assert allowed <= set(original), "Approved mutation must identify a baseline record."
restored = []

def preserve(match):
    key = match[2]
    if key in original and key not in allowed:
        if match[0] != original[key]:
            restored.append(key)
        return original[key]
    return match[0]

result = pattern.sub(preserve, current)
final = {m[2]: m[0] for m in pattern.finditer(result)}
changed = [key for key in original if original[key] != final[key]]
assert set(changed) <= allowed
staged = evidence / "EncyclopediaUI-preserved.prefab"
staged.write_text(result, encoding="utf-8", newline="\n")
report = {
    "baselineRecords": len(original),
    "approvedExistingRecords": sorted(allowed),
    "changedExistingRecords": changed,
    "newUnityAuthoredRecords": len(set(final) - set(original)),
    "restoredImportSideEffectRecords": restored,
    "allOtherBaselineRecordsPreserved": True,
}
(evidence / "serialized-preservation-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps({k: len(v) if isinstance(v, list) else v for k, v in report.items()}))
