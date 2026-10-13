"""Verify the native typography edit against this task's prefab snapshot."""
import json
import pathlib
import re

root = pathlib.Path.cwd()
evidence = root / "outputs/encyclopedia-text-spacing-2026-10-11"
before = (evidence / "before/EncyclopediaUI.prefab").read_text(encoding="utf-8-sig")
prefab = root / "Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab"
after = prefab.read_text(encoding="utf-8-sig")
pattern = re.compile(r"--- !u!(\d+) &(-?\d+)[\s\S]*?(?=\n--- !u!|\Z)")
old = {m[2]: m[0] for m in pattern.finditer(before)}
new = {m[2]: m[0] for m in pattern.finditer(after)}
allowed = set((evidence / "authoring-allowed-ids.txt").read_text().splitlines())
assert old.keys() == new.keys(), "Typography must not add/remove prefab objects."
changed = {key for key in old if old[key] != new[key]}
unexpected = changed - allowed
assert not unexpected, "Unexpected serialized record changes: " + repr(sorted(unexpected))
for key in changed:
    for field in ("m_fontAsset", "m_sharedMaterial", "m_Sprite", "m_AspectMode", "m_AspectRatio"):
        pattern_field = re.compile(r"^  " + re.escape(field) + r":.*$", re.M)
        assert pattern_field.findall(old[key]) == pattern_field.findall(new[key]), field + " changed: " + key
report = {
    "baselineRecords": len(old),
    "authorizedRecords": len(allowed),
    "changedRecords": sorted(changed),
    "hierarchyAndRecordIdsUnchanged": True,
    "allOtherBaselineRecordsPreserved": True,
    "fontAndSpriteReferencesAndImageFitUnchanged": True,
}
(evidence / "serialized-preservation-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps({k: len(v) if isinstance(v, list) else v for k, v in report.items()}))
