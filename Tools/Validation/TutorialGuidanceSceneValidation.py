"""Validate tutorial objective/spotlight authoring without modifying Unity assets."""
from pathlib import Path
import re

scene = Path("Assets/_Project/Scenes/PrototypeTutorialUpgradeScene.unity")
text = scene.read_text(encoding="utf-8-sig")
parts = re.split(r"(?=^--- !u!)", text, flags=re.M)[1:]
blocks = {re.search(r"^--- !u!\d+ &(-?\d+)", p)[1]: p for p in parts}
assert len(parts) == len(blocks), "Duplicate local IDs"
assert all(i == "0" or i in blocks for i in re.findall(r"\{fileID: (-?\d+)\}", text)), "Missing local reference"
for i in ("8200000149", "8200000168", "8800000003", "8800000024", "8800000045", "8800000066"):
    assert "m_IsActive: 0" in blocks[i], "Obsolete fixed guide still active: " + i
view = blocks["8100000060"]
assert "tutorial: {fileID: 8100000003}" in view
images = ("8300000004", "9200000104", "9200000114", "9200000124")
for image in images:
    assert "fileID: " + image in view, "Movement glyph not wired"
    assert "m_RaycastTarget: 0" in blocks[image], "Overhead glyph blocks pointer input"
for i in ("8100000058", "8400000032"):
    assert "guid: bc5dc7440ad16f045b3a71f996721562" in blocks[i], "Outline material missing"
guide = blocks["8400000035"]
assert len(re.findall(r"^  - \{fileID:", guide, re.M)) == 4, "Four authored spotlight panels required"
assert "m_RaycastTarget: 0" in blocks["8100000058"], "Objective text blocks pointer input"
assert "tutorialPotion: {fileID: 11400000, guid: 6b22cb4e0ab0a194b968acb6b169cbfb, type: 2}" in guide
for field, component in (("potionRangeEndGlyph", 9610000103), ("potionRangeSeparator", 9610000113),
                         ("potionUseGlyph", 9610000123), ("potionUseInstruction", 9610000133)):
    assert f"{field}: {{fileID: {component}}}" in guide, field + " is not wired"
    assert "m_RaycastTarget: 0" in blocks[str(component)], field + " intercepts input"
    assert f"  - {{fileID: {component - 2}}}" in blocks["8400000006"], field + " is outside guidance"
    assert "m_IsActive: 0" in blocks[str(component - 3)], field + " starts visible"
print("TUTORIAL_GUIDANCE_SCENE_PASS: local IDs/references, disabled fixed guides, four glyphs, outline materials and four-panel spotlight.")
