"""Search only exact new GUIDs in Assets metadata; print/store bounded matches."""
import json
import re
import subprocess
from pathlib import Path

OUTPUT = Path(__file__).resolve().parent
ROOT = OUTPUT.parents[1]
report = json.loads((OUTPUT / "preservation-audit.json").read_text(encoding="utf-8"))
guids = report["new_guid_uniqueness"]["guids"]
arguments = ["rg", "-l", "-F", "--glob", "*.meta"]
for guid in guids.values():
    arguments += ["-e", "guid: " + guid]
search = subprocess.run(arguments + ["Assets"], cwd=ROOT, capture_output=True, text=True)
matches = search.stdout.splitlines()
collisions = []
for path in matches:
    text = (ROOT / path).read_text(encoding="utf-8-sig")
    guid = re.search(r"^guid: (\w+)$", text, re.M).group(1)
    expected = [name + ".meta" for name, value in guids.items() if value == guid]
    if len(expected) != 1 or Path(path).as_posix() != expected[0]:
        collisions.append(path)
result = {"search_root": "Assets", "new_guids": len(guids), "matched_metadata": len(matches),
          "matched_paths": matches, "collisions": collisions,
          "passed": search.returncode == 0 and len(matches) == len(guids) and not collisions}
(OUTPUT / "global-guid-audit.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({key: value for key, value in result.items() if key != "matched_paths"}, ensure_ascii=False))
