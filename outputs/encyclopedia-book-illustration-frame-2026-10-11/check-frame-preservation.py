"""One-off preservation evidence for the approved Illustration frame enlargement.

Reads the pre-authoring prefab and protected hash manifest. Writes only a JSON
report beside this script. Run after Unity authoring/import has completed.
This is evidence tooling, not a Unity test or a production asset editor.
"""

import hashlib
import json
import re
import sys
from pathlib import Path


EVIDENCE = Path(__file__).resolve().parent
ROOT = EVIDENCE.parent.parent
BEFORE = EVIDENCE / "before" / "EncyclopediaUI.prefab"
CURRENT = ROOT / "Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab"
REPORT = EVIDENCE / "serialized-preservation-report.json"
ILLUSTRATION_ID = "3202883555615465603"
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)[^\n]*\n", re.MULTILINE)


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def records(path):
    source = path.read_text(encoding="utf-8-sig")
    matches = list(HEADER.finditer(source))
    if not matches:
        raise ValueError(f"No Unity records found: {path}")
    result, order = {}, []
    for index, match in enumerate(matches):
        record_id = match.group(2)
        if record_id in result:
            raise ValueError(f"Duplicate record ID {record_id}: {path}")
        end = matches[index + 1].start() if index + 1 < len(matches) else len(source)
        result[record_id] = {"class": match.group(1), "text": source[match.start():end]}
        order.append(record_id)
    return source[:matches[0].start()], order, result


def field(record, name):
    match = re.search(r"^  " + re.escape(name) + r":\s*([^\n]*)$", record["text"], re.MULTILINE)
    if match is None:
        raise ValueError(f"Missing serialized field {name}")
    return match.group(1).strip()


def field_block(record, name):
    lines = record["text"].splitlines()
    for index, line in enumerate(lines):
        if not line.startswith("  " + name + ":"):
            continue
        end = index + 1
        while end < len(lines) and not re.match(r"^  [^ -][^:]*:", lines[end]):
            end += 1
        return "\n".join(lines[index:end])
    return None


def reference(record, name):
    match = re.search(r"\bfileID:\s*(-?\d+)", field(record, name))
    if match is None:
        raise ValueError(f"Missing fileID in {name}")
    return match.group(1)


def by_identifier(all_records, identifier):
    matches = [key for key, record in all_records.items()
               if re.search(r"^  m_EditorClassIdentifier: " + re.escape(identifier) + r"$",
                            record["text"], re.MULTILINE)]
    if len(matches) != 1:
        raise ValueError(f"Expected one {identifier}, found {len(matches)}")
    return matches[0]


def rect_for_object(all_records, object_id):
    components = re.findall(r"\bfileID:\s*(-?\d+)", field_block(all_records[object_id], "m_Component") or "")
    rects = [key for key in components if all_records[key]["class"] == "224"]
    if len(rects) != 1:
        raise ValueError(f"Expected one RectTransform for {object_id}, found {len(rects)}")
    if reference(all_records[rects[0]], "m_GameObject") != object_id:
        raise ValueError(f"RectTransform does not point back to {object_id}")
    return rects[0]


def rect_for_component(all_records, component_id):
    return rect_for_object(all_records, reference(all_records[component_id], "m_GameObject"))


def layout_ids(all_records):
    tab = all_records[by_identifier(all_records, "UI::EncyclopediaCreatureTab")]
    detail = all_records[by_identifier(all_records, "UI::EncyclopediaCreatureDetailView")]
    item = all_records[by_identifier(all_records, "Assembly-CSharp::EncyclopediaItemLeftPage")]

    def detail_rect(name):
        return rect_for_component(all_records, reference(detail, name))

    portrait = detail_rect("characterImage")
    monster = detail_rect("monsterImage")
    name = detail_rect("characterTitleText")
    affection = detail_rect("affectionLabelText")
    item_title = rect_for_component(all_records, reference(item, "titleText"))
    return {
        "illustration": reference(all_records[portrait], "m_Father"),
        "character_portrait": portrait,
        "character_name": name,
        "left_title": rect_for_component(all_records, reference(tab, "titleText")),
        "monster_title": detail_rect("monsterTitleText"),
        "monster_preview": reference(all_records[monster], "m_Father"),
        "item_title_group": reference(all_records[item_title], "m_Father"),
        "affection": reference(all_records[affection], "m_Father"),
        "character_scroll": detail_rect("characterScrollRect"),
        "monster_grid": rect_for_object(all_records, reference(tab, "monsterGridRoot")),
        "character_grid": rect_for_object(all_records, reference(tab, "characterGridRoot")),
        "left_page": rect_for_object(all_records, reference(tab, "leftContentRoot")),
        "right_page": rect_for_object(all_records, reference(tab, "rightContentRoot")),
        "count_object": reference(all_records[reference(tab, "entryCountText")], "m_GameObject"),
        "full_view_rect": detail_rect("fullViewButton"),
        "book": rect_for_component(all_records, "577073705051753878"),
    }


def vector2(record, name):
    values = dict(re.findall(r"([xy]):\s*([-+\d.eE]+)", field(record, name)))
    if set(values) != {"x", "y"}:
        raise ValueError(f"Invalid Vector2 in {name}")
    return [float(values["x"]), float(values["y"])]


def without_fields(record, names):
    return "\n".join(line for line in record["text"].splitlines()
                     if not any(line.startswith("  " + name + ":") for name in names))


def hierarchy_projection(all_records):
    names = ("m_Component", "m_GameObject", "m_Children", "m_Father", "m_RootOrder")
    return {key: {name: field_block(record, name) for name in names}
            for key, record in all_records.items() if record["class"] in {"1", "4", "224"}}


def reference_projection(all_records):
    return {key: [line for line in record["text"].splitlines() if re.search(r"\b(?:fileID|guid):", line)]
            for key, record in all_records.items()}


def presentation_projection(all_records):
    result = {}
    for key, record in all_records.items():
        names = re.findall(r"^  ([^ :]+):", record["text"], re.MULTILINE)
        fields = {name: field_block(record, name) for name in names
                  if re.search(r"font|sprite|aspect", name, re.IGNORECASE)
                  or name in {"m_Material", "m_sharedMaterial"}}
        if fields:
            result[key] = fields
    return result


def main():
    report = {"passed": False, "purpose": "one-off serialized asset preservation evidence",
              "before": str(BEFORE), "current": str(CURRENT), "checks": []}

    def check(name, passed, **details):
        report["checks"].append({"name": name, "passed": bool(passed), **details})

    exit_code = 2
    try:
        old_prefix, old_order, old = records(BEFORE)
        new_prefix, new_order, new = records(CURRENT)
        old_ids, new_ids = set(old), set(new)
        report["before_sha256"] = sha256(BEFORE)
        report["current_sha256"] = sha256(CURRENT)
        check("2583_record_ids_preserved", old_ids == new_ids and len(old) == len(new) == 2583,
              before_count=len(old), current_count=len(new), added=sorted(new_ids - old_ids), removed=sorted(old_ids - new_ids))
        check("record_order_and_yaml_prefix_preserved", old_order == new_order and old_prefix == new_prefix)
        check("record_classes_preserved", all(old[key]["class"] == new[key]["class"] for key in old_ids & new_ids))
        old_layout, layout = layout_ids(old), layout_ids(new)
        report["layout_record_ids"] = layout
        check("layout_component_references_preserved", old_layout == layout)
        changed = {key for key in old_ids & new_ids if old[key]["text"] != new[key]["text"]}
        report["changed_record_ids"] = sorted(changed)
        check("exactly_the_requested_illustration_rect_changed", changed == {ILLUSTRATION_ID}
              and layout["illustration"] == ILLUSTRATION_ID and new[ILLUSTRATION_ID]["class"] == "224",
              changed_record_ids=sorted(changed), unchanged_record_count=len(old_ids & new_ids) - len(changed))
        allowed_path = EVIDENCE / "authoring-allowed-ids.txt"
        allowed = [line.strip() for line in allowed_path.read_text(encoding="utf-8-sig").splitlines() if line.strip()]
        authoring_roles = ("left_title", "monster_title", "illustration", "monster_preview", "item_title_group")
        expected_touched = {layout[role] for role in authoring_roles}
        check("native_authoring_touch_list_matches_five_existing_alignment_targets",
              len(allowed) == len(set(allowed)) == 5 and set(allowed) == expected_touched,
              touched_record_ids=allowed, mutable_record_ids=[ILLUSTRATION_ID],
              explanation="The existing alignment authoring touches five targets; only Illustration may differ from baseline.")
        prior, current = old[ILLUSTRATION_ID], new[ILLUSTRATION_ID]
        check("only_position_y_and_size_xy_are_mutable", without_fields(prior, {"m_AnchoredPosition", "m_SizeDelta"})
              == without_fields(current, {"m_AnchoredPosition", "m_SizeDelta"})
              and vector2(prior, "m_AnchoredPosition")[0] == vector2(current, "m_AnchoredPosition")[0] == 0.0)
        vectors = {name: vector2(current, name) for name in
                   ("m_AnchorMin", "m_AnchorMax", "m_Pivot", "m_AnchoredPosition", "m_SizeDelta")}
        report["authored_illustration_geometry"] = vectors
        check("illustration_is_594_by_389_at_top_minus_7", vectors["m_AnchorMin"] == vectors["m_AnchorMax"] == [0.5, 1.0]
              and vectors["m_Pivot"] == [0.5, 1.0] and vectors["m_AnchoredPosition"] == [0.0, -7.0]
              and vectors["m_SizeDelta"] == [594.0, 389.0]
              and vector2(prior, "m_AnchoredPosition") == [0.0, -25.0] and vector2(prior, "m_SizeDelta") == [550.0, 371.0])
        old_bottom = -vector2(prior, "m_AnchoredPosition")[1] + vector2(prior, "m_SizeDelta")[1]
        bottom = -vectors["m_AnchoredPosition"][1] + vectors["m_SizeDelta"][1]
        affection = new[layout["affection"]]
        affection_top = -vector2(affection, "m_AnchoredPosition")[1] - vector2(affection, "m_SizeDelta")[1] * 0.5
        check("bottom_396_and_affection_top_400_preserve_four_unit_clearance", old_bottom == bottom == 396.0
              and affection_top == 400.0, before_bottom=old_bottom, current_bottom=bottom,
              affection_top=affection_top, clearance=affection_top - bottom)
        for role in ("left_title", "monster_title"):
            rect = new[layout[role]]
            top = -vector2(rect, "m_AnchoredPosition")[1] - vector2(rect, "m_SizeDelta")[1] * 0.5
            check(role + "_retains_25_unit_top_inset", old[layout[role]]["text"] == rect["text"]
                  and vector2(rect, "m_AnchoredPosition") == [0.0, -51.0]
                  and vector2(rect, "m_SizeDelta") == [550.0, 52.0] and top == 25.0, top_inset=top)
        stable_roles = tuple(role for role in layout if role != "illustration")
        check("name_portrait_lower_sections_grids_titles_full_view_count_and_pages_unchanged",
              all(old[layout[role]]["text"] == new[layout[role]]["text"] for role in stable_roles),
              preserved_records={role: layout[role] for role in stable_roles})
        portrait = new[layout["character_portrait"]]
        portrait_object = reference(portrait, "m_GameObject")
        components = re.findall(r"\bfileID:\s*(-?\d+)", field_block(new[portrait_object], "m_Component") or "")
        fitters = [key for key in components if "UnityEngine.UI.AspectRatioFitter" in new[key]["text"]]
        check("detail_fit_in_parent_and_bottom_pivot_preserved", len(fitters) == 1
              and field(new[fitters[0]], "m_AspectMode") == "3" and vector2(portrait, "m_Pivot") == [0.5, 0.0],
              aspect_mode=field(new[fitters[0]], "m_AspectMode") if len(fitters) == 1 else None,
              pivot=vector2(portrait, "m_Pivot"))
        full_view = new[layout["full_view_rect"]]
        check("full_view_one_unit_bottom_right_inset_preserved", vector2(full_view, "m_AnchorMin")
              == vector2(full_view, "m_AnchorMax") == vector2(full_view, "m_Pivot") == [1.0, 0.0]
              and vector2(full_view, "m_AnchoredPosition") == [-1.0, 1.0])
        check("hidden_count_preserved", field(new[layout["count_object"]], "m_IsActive") == "0")
        check("hierarchy_and_component_order_preserved", hierarchy_projection(old) == hierarchy_projection(new))
        check("all_serialized_fileid_and_guid_references_preserved", reference_projection(old) == reference_projection(new))
        old_presentation = presentation_projection(old)
        check("all_font_sprite_material_and_aspect_fit_fields_preserved", old_presentation == presentation_projection(new),
              inspected_record_count=len(old_presentation), inspected_field_count=sum(len(fields) for fields in old_presentation.values()))
        book = new[layout["book"]]
        page = new[layout["right_page"]]
        book_width, book_height = vector2(book, "m_SizeDelta")
        page_width, page_height_delta = vector2(page, "m_SizeDelta")
        page_position = vector2(page, "m_AnchoredPosition")
        page_right = book_width * 0.5 + page_position[0]
        page_left = page_right - page_width
        page_top = book_height * 0.5 + page_position[1]
        sprite_width, sprite_height = 816.0, 720.0
        art_scale = min(book_width / sprite_width, book_height / sprite_height)
        art_left, art_top = -sprite_width * art_scale * 0.5, sprite_height * art_scale * 0.5
        # Original sprite8 rules: left x427, right x719, top y196 (top-origin pixels).
        rule_inside_left = art_left + 428.0 * art_scale
        rule_inside_right = art_left + 719.0 * art_scale
        rule_inside_top = art_top - 197.0 * art_scale
        side_inset = (page_width - vectors["m_SizeDelta"][0]) * 0.5
        frame_left, frame_right = page_left + side_inset, page_right - side_inset
        frame_top = page_top + vectors["m_AnchoredPosition"][1]
        screen_scale = 1920.0 / 2400.0  # Existing native fixture CanvasScaler reference.
        ui_gaps = {"left": frame_left - rule_inside_left, "right": rule_inside_right - frame_right,
                   "top": rule_inside_top - frame_top}
        pixel_gaps = {name: value * screen_scale for name, value in ui_gaps.items()}
        expected_gaps = {"left": 1.511111111111, "right": 1.822222222222, "top": 1.644444444444}
        check("native_brown_rule_clearance_is_one_to_two_pixels", all(1.0 <= value <= 2.0 for value in pixel_gaps.values())
              and all(abs(pixel_gaps[name] - expected_gaps[name]) < 1e-6 for name in expected_gaps)
              and [book_width, book_height] == [2320.0, 1480.0]
              and [page_width, book_height + page_height_delta] == [600.0, 745.0],
              measured_from="serialized geometry and original sprite8 rule pixel edges; not a new render measurement",
              ui_gaps=ui_gaps, expected_native_pixel_gaps=pixel_gaps,
              native_fixture_resolution=[1920, 1080], canvas_reference_resolution=[2400, 1350],
              source_sprite_dimensions=[sprite_width, sprite_height], source_rule_pixels={"left": 427, "right": 719, "top": 196},
              page_top=page_top, page_left=page_left, page_right=page_right)
        protected = json.loads((EVIDENCE / "protected-before-hashes.json").read_text(encoding="utf-8-sig"))
        if not isinstance(protected, list) or len(protected) != 11:
            raise ValueError("Protected hash manifest must contain the eleven baseline files")
        hashes = []
        for entry in protected:
            path, expected_hash = Path(entry["Path"]), entry["Hash"].upper()
            actual_hash = sha256(path) if path.is_file() else None
            hashes.append({"path": str(path), "expected_sha256": expected_hash, "actual_sha256": actual_hash,
                           "unchanged": actual_hash == expected_hash})
        check("eleven_protected_runtime_item_art_catalog_global_ui_and_test_files_unchanged",
              all(entry["unchanged"] for entry in hashes), files=hashes)
        report["passed"] = all(entry["passed"] for entry in report["checks"])
        exit_code = 0 if report["passed"] else 1
    except (OSError, ValueError, KeyError, TypeError) as error:
        report["error"] = f"{type(error).__name__}: {error}"
    REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"passed": report["passed"], "checks": len(report["checks"]), "report": str(REPORT),
                      "failed_checks": [entry["name"] for entry in report["checks"] if not entry["passed"]],
                      "error": report.get("error")}, ensure_ascii=False))
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
