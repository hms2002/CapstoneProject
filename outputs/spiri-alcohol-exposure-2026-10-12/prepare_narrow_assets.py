"""Stage exact approved asset deltas outside Assets; never write production assets."""
import hashlib
import json
import re
from pathlib import Path

OUTPUT = Path(__file__).resolve().parent
rows = json.loads((OUTPUT / "approved-assets-before.json").read_text(encoding="utf-8-sig"))
result = []
for row in rows:
    path = Path(row["path"])
    old = Path(row["backup"]).read_bytes()
    current = path.read_bytes()
    if path.name == "GE_Puddle_AlcoholBuff.asset":
        assert old.count(b"  duration: 0.35") == 1
        staged = old.replace(b"  duration: 0.35", b"  duration: 5")
    elif path.name == "DragonBossNpc.asset":
        match = re.search(rb"^  affectionRewards:.*?(?=^  spriteLibraryAsset:)", current, re.M | re.S)
        assert match and b"targetLevel: 1" in match.group() and old.count(b"  affectionRewards: []") == 1
        newline = b"\r\n" if b"\r\n" in old else b"\n"
        block = match.group().replace(b"\r\n", b"\n").rstrip(b"\n").replace(b"\n", newline)
        staged = old.replace(b"  affectionRewards: []", block)
    else:
        continue
    staged_path = OUTPUT / (path.name + ".narrow-final")
    staged_path.write_bytes(staged)
    result.append({"path": str(path), "staged": str(staged_path),
                   "authoring_hash": hashlib.sha256(current).hexdigest(),
                   "staged_hash": hashlib.sha256(staged).hexdigest()})
(OUTPUT / "narrow-assets-proposal.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("Staged exact duration/reward deltas for", len(result), "assets; production assets unchanged.")
