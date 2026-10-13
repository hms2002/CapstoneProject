"""Restore pre-existing serialized documents after Unity API authoring.

Keep Unity-generated component GUIDs/fileIDs and the narrowly approved structural
records. Never regenerate IDs or deserialize native object references through JSON.
Run only while the Editor is closed, followed by Unity import/reference tests.
"""
import json
import pathlib
import re
import subprocess

root = pathlib.Path.cwd()
evidence = root / "outputs/encyclopedia-implementation-2026-10-11"
pattern = re.compile(r"--- !u!(\d+) &(-?\d+)[\s\S]*?(?=\n--- !u!|\Z)")

def docs(text):
    return {m[2]: (m[1], m[0]) for m in pattern.finditer(text)}

report = []
ui = pathlib.Path("Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab")
before = (evidence / "before" / ui).read_text(encoding="utf-8-sig")
current = (root / ui).read_text(encoding="utf-8-sig")
original_docs = docs(before)
allowed = {"1600577953579324491", "8462708796356329205", "3783640947135483920",
           "5501019934593851225", "7784228223524498331", "8317086359111449962",
           "6906855235117759964"}
for button_id in ("4250067842432859356", "2435429167609481065"):
    owner = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", original_docs[button_id][1])[1]
    allowed.update(key for key, (kind, record) in original_docs.items()
                   if kind == "224" and "m_GameObject: {fileID: " + owner + "}" in record)
assert set(original_docs).issubset(docs(current)), "Existing UI records were lost."
restored = []
def restore_record(match):
    key = match[2]
    if key in original_docs and key not in allowed:
        restored.append(key)
        return original_docs[key][1]
    return match[0]
result = pattern.sub(restore_record, current)
staged_root = evidence / "preserved-assets"
(staged_root / ui).parent.mkdir(parents=True, exist_ok=True)
(staged_root / ui).write_text(result, encoding="utf-8", newline="\n")
report.append({"path": str(ui), "old_records_restored": len(restored), "approved_structural_records": sorted(allowed)})

baseline_status = (evidence / "status-before.txt").read_text(encoding="utf-8-sig")
for path in sorted((root / "Assets/_Project/Prefabs/Monsters").rglob("*.prefab")):
    rel = path.relative_to(root).as_posix()
    text = path.read_text(encoding="utf-8-sig")
    added = [(key, record) for key, (_, record) in docs(text).items()
             if "Gameplay::MonsterEncyclopediaIdentity" in record]
    if not added:
        continue
    assert rel not in baseline_status, "Preserve an existing local prefab change: " + rel
    original = subprocess.check_output(["git", "show", "HEAD:" + rel]).decode("utf-8-sig")
    key, record = added[0]
    original_records = docs(original)
    assert key not in original_records
    owner = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", record)[1]
    assert owner in original_records and original_records[owner][0] == "1"
    old_go = original_records[owner][1]
    assert "\n  m_Layer:" in old_go
    new_go = old_go.replace("\n  m_Layer:", "\n  - component: {fileID: " + key + "}\n  m_Layer:", 1)
    final = original.replace(old_go, new_go, 1).rstrip() + "\n" + record.rstrip() + "\n"
    assert set(original_records).issubset(docs(final))
    target = staged_root / rel
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(final, encoding="utf-8", newline="\n")
    report.append({"path": rel, "identity_fileID": key, "original_records_preserved": len(original_records)})

(evidence / "serialized-preservation-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print("Preserved original documents:", len(report), "assets; retained Unity-authored identities and UI additions.")
