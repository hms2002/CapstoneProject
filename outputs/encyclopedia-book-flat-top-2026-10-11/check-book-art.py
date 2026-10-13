"""Read-only pixel and protected-reference audit for the book art edit.

Only the JSON report beside this script is written. All pixel comparisons use
Pillow against the saved before image and unchanged Unity slicing metadata.
"""
from collections import Counter
from datetime import datetime, timezone
from functools import reduce
from hashlib import sha256
import json
from pathlib import Path
import re
import sys

from PIL import Image, ImageChops, ImageDraw


EVIDENCE = Path(__file__).resolve().parent
WORKSPACE = EVIDENCE.parent.parent
NAME = "Inventory Book SpriteSheet.png"
ASSET = WORKSPACE / "Assets/_Project/Art/Sprites/UI/Encyclopedia/Updated_Paper_Book/Sprites" / NAME
EXPECTED_FRAMES = [8, 9, 10, 11, 12, 14, 15, 16, 17]
EXPECTED_MOVING_COLUMNS = {10: [412, 688], 11: [410, 560], 12: [339, 398]}
EXPECTED_BAND = {"left": 85, "top": 181, "right": 733, "bottom": 230}
EXPECTED_SIZE = (7344, 3600)


def pixel_diff(a, b):
    channels = ImageChops.difference(a, b).split()
    return reduce(ImageChops.lighter, channels).point(lambda value: 255 if value else 0)


def count(mask):
    return mask.histogram()[255]


def palette(image):
    colors = image.getcolors(maxcolors=65536)
    if colors is None:
        raise ValueError("Unexpectedly large palette")
    return {color for _, color in colors}


def parse_rects(meta, height):
    pattern = (
        r"name: Inventory Book SpriteSheet_(\d+)\s+rect:\s+"
        r"serializedVersion: \d+\s+x: (\d+)\s+y: (\d+)\s+"
        r"width: (\d+)\s+height: (\d+)"
    )
    rects = {}
    for match in re.finditer(pattern, meta):
        frame, x, bottom_y, width, frame_height = map(int, match.groups())
        rects[frame] = {
            "x": x, "y": height - bottom_y - frame_height,
            "width": width, "height": frame_height,
        }
    return rects


def frame_box(rect):
    return (rect["x"], rect["y"], rect["x"] + rect["width"], rect["y"] + rect["height"])


def top_profile(image, left, right):
    pixels = image.load()
    tops = []
    runs = []
    for x in range(left, right):
        y = next((row for row in range(240) if pixels[x, row][3]), None)
        tops.append(y)
        if not runs or runs[-1]["y"] != y:
            runs.append({"left": x, "right": x + 1, "y": y})
        else:
            runs[-1]["right"] = x + 1
    return {
        "columns": [left, right],
        "top_y_histogram": dict(sorted(Counter(tops).items())),
        "top_y_span": max(tops) - min(tops),
        "runs": runs,
    }


def inner_line(image, left, right):
    pixels = image.load()
    paper = (238, 189, 138, 255)
    border_colors = {(195, 137, 93, 255), (202, 142, 97, 255)}
    candidates = []
    for y in range(188, 220):
        colors = Counter(pixels[x, y] for x in range(left, right))
        non_paper = sum(n for rgba, n in colors.items() if rgba[3] and rgba != paper)
        dark_border = sum(n for rgba, n in colors.items() if rgba in border_colors)
        candidates.append((dark_border, non_paper, y, colors))
    dark_border, non_paper, y, colors = max(candidates, key=lambda item: (item[0], -item[2]))
    return {
        "columns": [left, right], "best_line_y": y,
        "dark_border_pixels": dark_border,
        "non_paper_pixels": non_paper, "width": right - left,
        "row_colors": [{"rgba": list(rgba), "count": n} for rgba, n in colors.most_common()],
    }


manifest = json.loads((EVIDENCE / "sheet-edit-manifest.json").read_text(encoding="utf-8-sig"))
protected_before = json.loads((EVIDENCE / "protected-before-hashes.json").read_text(encoding="utf-8-sig"))
before = Image.open(EVIDENCE / "before" / NAME).convert("RGBA")
after = Image.open(ASSET).convert("RGBA")
staged = Image.open(EVIDENCE / "Inventory Book SpriteSheet-flat-top.png").convert("RGBA")
old_tile = Image.open(EVIDENCE / "book-open-before.png").convert("RGBA")
candidate = Image.open(EVIDENCE / "book-open-candidate.png").convert("RGBA")
meta = (EVIDENCE / "before" / (NAME + ".meta")).read_text(encoding="utf-8-sig")
rects = parse_rects(meta, before.height)
checks = []


def check(name, passed, **details):
    checks.append({"name": name, "passed": bool(passed), **details})


check("sheet_size", before.size == after.size == staged.size == EXPECTED_SIZE,
      before=list(before.size), after=list(after.size), staged=list(staged.size))
check("manifest_frame_set", [entry["frame"] for entry in manifest["frames"]] == EXPECTED_FRAMES)
check("manifest_band", manifest["editedBand"] == EXPECTED_BAND)
check("manifest_size", manifest["sheetSize"] == {"width": 7344, "height": 3600})

if before.size != after.size:
    raise ValueError("Cannot compare different sheet dimensions")
diff = pixel_diff(before, after)
allowed = Image.new("L", before.size, 0)
draw_allowed = ImageDraw.Draw(allowed)
frame_reports = []
for entry in manifest["frames"]:
    frame = entry["frame"]
    rect = rects[frame]
    moving = EXPECTED_MOVING_COLUMNS.get(frame)
    check("frame_%s_manifest_rect" % frame, entry["rect"] == rect)
    check("frame_%s_manifest_moving_columns" % frame, entry["protectedMovingColumns"] == moving)
    x, y = rect["x"], rect["y"]
    draw_allowed.rectangle((x + 85, y + 181, x + 732, y + 229), fill=255)
    if moving:
        draw_allowed.rectangle((x + moving[0], y + 181, x + moving[1] - 1, y + 229), fill=0)
    tile_diff = diff.crop(frame_box(rect))
    changed = count(tile_diff)
    lower_diff = count(tile_diff.crop((0, 230, 816, 720)))
    moving_diff = count(tile_diff.crop((moving[0], 0, moving[1], 720))) if moving else 0
    check("frame_%s_changed_count" % frame, changed == entry["changedPixels"], measured=changed,
          manifest=entry["changedPixels"])
    check("frame_%s_lower_pixels_preserved" % frame, lower_diff == 0, changed_pixels=lower_diff)
    check("frame_%s_moving_columns_preserved" % frame, moving_diff == 0, changed_pixels=moving_diff)
    frame_reports.append({
        "frame": frame, "changed_pixels": changed, "local_diff_bbox": tile_diff.getbbox(),
        "lower_changed_pixels": lower_diff, "protected_column_changed_pixels": moving_diff,
    })

outside = ImageChops.subtract(diff, allowed)
check("all_changes_inside_approved_mask", count(outside) == 0,
      changed_pixels_outside_mask=count(outside), outside_diff_bbox=outside.getbbox())
other_frames = []
for frame, rect in rects.items():
    if frame not in EXPECTED_FRAMES:
        changed = count(diff.crop(frame_box(rect)))
        if changed:
            other_frames.append({"frame": frame, "changed_pixels": changed})
check("other_sliced_frames_preserved", not other_frames, changed_frames=other_frames)
check("production_matches_staged_png", count(pixel_diff(after, staged)) == 0)

before_palette, after_palette = palette(before), palette(after)
new_colors = sorted(after_palette - before_palette)
check("no_new_rgba_colors_in_sheet", not new_colors,
      before_color_count=len(before_palette), after_color_count=len(after_palette), new_colors=new_colors)
check("tile_dimensions", old_tile.size == candidate.size == (816, 720))
check("candidate_lower_pixels_preserved", count(pixel_diff(old_tile, candidate).crop((0, 230, 816, 720))) == 0)
tile_new_colors = sorted(palette(candidate) - palette(old_tile))
check("no_new_rgba_colors_in_tile", not tile_new_colors, new_colors=tile_new_colors)
check("production_idle_matches_candidate", count(pixel_diff(after.crop(frame_box(rects[8])), candidate)) == 0)

protected_reports = []
for entry in protected_before:
    path = Path(entry["Path"])
    actual = sha256(path.read_bytes()).hexdigest().upper() if path.exists() else None
    protected_reports.append({"path": str(path), "expected_sha256": entry["Hash"],
                              "actual_sha256": actual, "unchanged": actual == entry["Hash"]})
check("all_nine_protected_files_unchanged", len(protected_reports) == 9 and all(x["unchanged"] for x in protected_reports),
      protected_file_count=len(protected_reports))

visual_analysis = {
    "before_main_top": top_profile(old_tile, 90, 728),
    "candidate_main_top": top_profile(candidate, 90, 728),
    "candidate_full_modified_columns_top": top_profile(candidate, 85, 733),
    "left_inner_line": inner_line(candidate, 120, 365),
    "right_inner_line": inner_line(candidate, 455, 700),
    "visual_inspection": [
        "Main upper silhouette is horizontal. Original lower binding, corners and decoration remain intact.",
        "Upper inner border is horizontal across both pages; the edit retains the central spine.",
        "Boundary at local y230 returns to original pixels. Small detail joins should also be checked at in-game scale.",
        "Moving-page exclusions intentionally retain original curved geometry while the page turns.",
    ],
    "scope": "Read-only Pillow inspection of before/candidate PNGs and production sheet; no Unity rendering was run by this audit.",
}
check("candidate_main_upper_edge_horizontal", visual_analysis["candidate_main_top"]["top_y_span"] == 0)
check("both_inner_lines_horizontal", visual_analysis["left_inner_line"]["dark_border_pixels"] == 245
      and visual_analysis["right_inner_line"]["dark_border_pixels"] == 245
      and visual_analysis["left_inner_line"]["best_line_y"] == visual_analysis["right_inner_line"]["best_line_y"])

report = {
    "generated_utc": datetime.now(timezone.utc).isoformat(),
    "asset": str(ASSET), "bbox_convention": "left/top included; right/bottom excluded",
    "all_checks_passed": all(c["passed"] for c in checks),
    "total_changed_pixels": count(diff), "sheet_diff_bbox": diff.getbbox(),
    "checks": checks, "frames": frame_reports,
    "protected_files": protected_reports, "visual_analysis": visual_analysis,
    "unity_validation": "not executed by this audit",
}
report_path = EVIDENCE / "book-art-validation.json"
report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"all_checks_passed": report["all_checks_passed"],
                  "check_count": len(checks), "changed_pixels": count(diff),
                  "failed_checks": [c for c in checks if not c["passed"]],
                  "main_top": visual_analysis["candidate_main_top"],
                  "inner_lines": [visual_analysis["left_inner_line"], visual_analysis["right_inner_line"]],
                  "report": str(report_path)}, ensure_ascii=False))
sys.exit(0 if report["all_checks_passed"] else 1)
