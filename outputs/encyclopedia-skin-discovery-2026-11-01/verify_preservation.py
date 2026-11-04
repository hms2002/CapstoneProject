"""Read-only verification of the authorized art/prefab change against task snapshots."""
from pathlib import Path
import hashlib
import json
import re
import subprocess
from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = Path(__file__).resolve().parent
BASE = json.loads((EVIDENCE / "before-hashes.json").read_text())
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
changed = [p for p, h in BASE.items() if sha(ROOT / p) != h]
checks = {}
checks["existing_metadata_unchanged"] = all(sha(ROOT / p) == h for p, h in BASE.items() if p.endswith(".meta"))

prefab = "Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab"
def blocks(path):
    parts = re.split(r"(?=^--- !u!)", path.read_text(encoding="utf-8"), flags=re.M)
    return {int(re.match(r"--- !u!\d+ &(\d+)", p)[1]): p for p in parts if p.startswith("--- !u!")}
before, after = blocks(EVIDENCE / "before" / prefab), blocks(ROOT / prefab)
allowed = set(map(int, (EVIDENCE / "authoring-allowed-ids.txt").read_text().splitlines()))
checks["existing_prefab_ids_preserved"] = before.keys() <= after.keys()
checks["unrelated_prefab_records_preserved"] = all(before[k] == after.get(k) for k in before if k not in allowed)
font = re.compile(r"^  (?:m_fontAsset|m_sharedMaterial|m_fontSharedMaterials):.*(?:\n  -.*)*", re.M)
checks["original_fonts_and_materials_preserved"] = all(font.findall(v) == font.findall(after.get(k, "")) for k, v in before.items())

art = ROOT / "Assets/_Project/Art/Sprites/UI/Encyclopedia/PixelSkin"
approved = ROOT / "outputs/encyclopedia-pixel-skin-preview-2026-10-31/revision-6-depth-boss-gradient"
art_matches = {}
for p in art.iterdir():
    if p.suffix in (".png", ".aseprite", ".json"):
        matches = list(approved.rglob(p.name))
        art_matches[p.name] = len(matches) == 1 and sha(p) == sha(matches[0])
checks["approved_art_exact"] = all(art_matches.values())

book = "Assets/_Project/Art/Sprites/UI/Encyclopedia/Updated_Paper_Book/Sprites/Inventory Book SpriteSheet.png"
a, b = Image.open(EVIDENCE / "before" / book).convert("RGBA"), Image.open(ROOT / book).convert("RGBA")
checks["book_size_preserved"] = a.size == b.size
checks["book_uses_original_palette"] = {c for n, c in b.getcolors(1000000)} <= {c for n, c in a.getcolors(1000000)}
delta = ImageChops.difference(a, b).convert("RGB")
changed_pixels = sum(n for n, c in delta.getcolors(1000000) if c != (0, 0, 0))
patches = re.findall(r"rect=(\d+),(\d+),(\d+),(\d+)", (EVIDENCE / "book-corner-validation.txt").read_text())
# Clear only an in-memory difference mask; source/output artwork is never edited by this check.
for x, y, w, h in patches:
    x, y, w, h = map(int, (x, y, w, h))
    delta.paste((0, 0, 0), (x, y, x + w, y + h))
checks["book_changes_only_reported_regions"] = delta.getbbox() is None
checks["book_changed_pixels_match_native_report"] = changed_pixels == 15399

diff = subprocess.run(["git", "diff", "--check", "--", *[p for p in changed if p.endswith(".cs")]], cwd=ROOT, text=True, capture_output=True)
# Unity writes empty YAML strings as `m_Name: `; leave native serialization intact.
checks["scoped_csharp_diff_whitespace"] = diff.returncode == 0
report = {"checks": checks, "passed": sum(checks.values()), "total": len(checks),
          "existing_prefab_records": len(before), "new_prefab_records": len(after) - len(before),
          "changed_existing_files": changed, "art_matches": art_matches,
          "book_patches": len(patches), "book_changed_pixels": changed_pixels,
          "diff_check": diff.stdout + diff.stderr}
(EVIDENCE / "preservation-report.json").write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
print(json.dumps({"checks": checks, "records": len(before), "book_patches": len(patches)}, indent=2))
raise SystemExit(0 if all(checks.values()) else 1)
