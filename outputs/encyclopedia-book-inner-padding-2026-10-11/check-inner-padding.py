"""One-off asset-preservation evidence for the approved book padding authoring.

Run only after Unity authoring/import completes. This is not a Unity test case.
Reads the task snapshot, persistent prefab, allowlist and protected-file hashes;
writes serialized-preservation-report.json beside this script. Exit 0: passed;
exit 1: preservation check failed; exit 2: missing or invalid input.
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
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)[^\n]*\n", re.MULTILINE)


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def records(path):
    # Only normalize BOM/line endings; compare all serialized fields and values.
    text = path.read_text(encoding="utf-8-sig")
    matches = list(HEADER.finditer(text))
    if not matches:
        raise ValueError(f"No Unity records found: {path}")
    result = {}
    order = []
    for index, match in enumerate(matches):
        record_id = match.group(2)
        if record_id in result:
            raise ValueError(f"Duplicate record ID {record_id}: {path}")
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        result[record_id] = {"class": match.group(1), "text": text[match.start():end]}
        order.append(record_id)
    return text[:matches[0].start()], order, result


def field(record, name):
    match = re.search(r"^  " + re.escape(name) + r":\s*([^\n]*)$",
                      record["text"], re.MULTILINE)
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
    matches = [record_id for record_id, record in all_records.items()
               if re.search(r"^  m_EditorClassIdentifier: " + re.escape(identifier) + r"$",
                            record["text"], re.MULTILINE)]
    if len(matches) != 1:
        raise ValueError(f"Expected one {identifier}, found {len(matches)}")
    return matches[0]


def rect_for_object(all_records, object_id):
    component_block = field_block(all_records[object_id], "m_Component")
    if component_block is None:
        raise ValueError(f"Missing component list: {object_id}")
    components = re.findall(r"\bfileID:\s*(-?\d+)", component_block)
    rect_ids = [record_id for record_id in components
                if all_records[record_id]["class"] == "224"]
    if len(rect_ids) != 1:
        raise ValueError(f"Expected one RectTransform for {object_id}, found {len(rect_ids)}")
    if reference(all_records[rect_ids[0]], "m_GameObject") != object_id:
        raise ValueError(f"RectTransform does not point back to object {object_id}")
    return rect_ids[0]


def rect_for_component(all_records, component_id):
    return rect_for_object(all_records, reference(all_records[component_id], "m_GameObject"))


def ancestor_with_component(all_records, rect_id, identifier):
    while rect_id != "0":
        object_id = reference(all_records[rect_id], "m_GameObject")
        components = re.findall(r"\bfileID:\s*(-?\d+)", field_block(all_records[object_id], "m_Component"))
        if any(re.search(r"^  m_EditorClassIdentifier: " + re.escape(identifier) + r"$",
                         all_records[component_id]["text"], re.MULTILINE)
               for component_id in components):
            return rect_id
        rect_id = reference(all_records[rect_id], "m_Father")
    raise ValueError(f"No ancestor with {identifier}")


def layout_ids(all_records):
    tab = all_records[by_identifier(all_records, "UI::EncyclopediaCreatureTab")]
    detail = all_records[by_identifier(all_records, "UI::EncyclopediaCreatureDetailView")]
    item_left = all_records[by_identifier(all_records, "Assembly-CSharp::EncyclopediaItemLeftPage")]

    def detail_rect(name):
        return rect_for_component(all_records, reference(detail, name))

    character_image = detail_rect("characterImage")
    monster_image = detail_rect("monsterImage")
    item_title = rect_for_component(all_records, reference(item_left, "titleText"))
    affection_label = detail_rect("affectionLabelText")
    count_component = reference(tab, "entryCountText")
    return {
        "left_title": rect_for_component(all_records, reference(tab, "titleText")),
        "monster_title": detail_rect("monsterTitleText"),
        "illustration": reference(all_records[character_image], "m_Father"),
        "monster_preview": reference(all_records[monster_image], "m_Father"),
        "item_title_group": reference(all_records[item_title], "m_Father"),
        "character_name": detail_rect("characterTitleText"),
        "affection": reference(all_records[affection_label], "m_Father"),
        "character_scroll": detail_rect("characterScrollRect"),
        "monster_scroll": ancestor_with_component(all_records, detail_rect("monsterDescriptionText"),
                                                   "UnityEngine.UI::UnityEngine.UI.ScrollRect"),
        "monster_grid": rect_for_object(all_records, reference(tab, "monsterGridRoot")),
        "character_grid": rect_for_object(all_records, reference(tab, "characterGridRoot")),
        "left_page": rect_for_object(all_records, reference(tab, "leftContentRoot")),
        "right_page": rect_for_object(all_records, reference(tab, "rightContentRoot")),
        "count_object": reference(all_records[count_component], "m_GameObject"),
        "full_view_rect": rect_for_component(all_records, reference(detail, "fullViewButton")),
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
    return {record_id: {name: field_block(record, name) for name in names}
            for record_id, record in all_records.items() if record["class"] in {"1", "4", "224"}}


def reference_projection(all_records):
    return {record_id: [line for line in record["text"].splitlines()
                        if re.search(r"\b(?:fileID|guid):", line)]
            for record_id, record in all_records.items()}


def presentation_projection(all_records):
    result = {}
    for record_id, record in all_records.items():
        names = re.findall(r"^  ([^ :]+):", record["text"], re.MULTILINE)
        selected = {name: field_block(record, name) for name in names
                    if re.search(r"font|sprite|aspect", name, re.IGNORECASE)
                    or name in {"m_Material", "m_sharedMaterial"}}
        if selected:
            result[record_id] = selected
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
              before_count=len(old), current_count=len(new), added=sorted(new_ids - old_ids),
              removed=sorted(old_ids - new_ids))
        check("record_order_and_yaml_prefix_preserved", old_order == new_order and old_prefix == new_prefix)
        check("record_classes_preserved", all(old[key]["class"] == new[key]["class"]
                                              for key in old_ids & new_ids))
        old_layout = layout_ids(old)
        layout = layout_ids(new)
        report["layout_record_ids"] = layout
        check("layout_component_references_preserved", old_layout == layout)

        allowed_lines = (EVIDENCE / "authoring-allowed-ids.txt").read_text(encoding="utf-8-sig").splitlines()
        allowed_entries = [line.strip() for line in allowed_lines if line.strip()]
        if not all(re.fullmatch(r"-?\d+", line) for line in allowed_entries):
            raise ValueError("authoring-allowed-ids.txt contains a non-record-ID line")
        allowed = set(allowed_entries)
        approved_roles = ("left_title", "monster_title", "illustration", "monster_preview", "item_title_group")
        expected = {layout[role] for role in approved_roles}
        check("allowlist_contains_only_five_requested_recttransforms",
              len(allowed_entries) == len(allowed) == 5 and allowed == expected
              and all(new[key]["class"] == "224" for key in allowed),
              allowed_record_ids=sorted(allowed), expected_record_ids=sorted(expected))
        changed = {key for key in old_ids & new_ids if old[key]["text"] != new[key]["text"]}
        report["changed_record_ids"] = sorted(changed)
        check("only_the_five_authorized_recttransform_records_changed", changed == expected == allowed,
              changed_record_ids=sorted(changed), unauthorized=sorted(changed - expected),
              unchanged_record_count=len(old_ids & new_ids) - len(changed))

        for role in approved_roles:
            key = layout[role]
            mutable = {"m_AnchoredPosition", "m_SizeDelta"}
            if role == "monster_preview":
                mutable = {"m_SizeDelta"}
            elif role == "item_title_group":
                mutable = {"m_AnchoredPosition"}
            check(role + "_preserves_all_fields_except_approved_geometry",
                  without_fields(old[key], mutable) == without_fields(new[key], mutable), record_id=key)

        geometry = {}
        for role in approved_roles:
            rect = new[layout[role]]
            geometry[role] = {name: vector2(rect, name) for name in
                              ("m_AnchorMin", "m_AnchorMax", "m_Pivot", "m_AnchoredPosition", "m_SizeDelta")}
        report["authored_geometry"] = geometry
        for role in ("left_title", "monster_title"):
            vectors = geometry[role]
            top = -vectors["m_AnchoredPosition"][1] - vectors["m_SizeDelta"][1] / 2
            check(role + "_has_550_by_52_rect_with_25_unit_top_inset",
                  vectors["m_AnchorMin"] == vectors["m_AnchorMax"] == [0.5, 1.0]
                  and vectors["m_Pivot"] == [0.5, 0.5]
                  and vectors["m_AnchoredPosition"] == [0.0, -51.0]
                  and vectors["m_SizeDelta"] == [550.0, 52.0] and top == 25.0,
                  top_inset=top)
        illustration = geometry["illustration"]
        illustration_bottom = -illustration["m_AnchoredPosition"][1] + illustration["m_SizeDelta"][1]
        old_illustration = old[layout["illustration"]]
        old_bottom = -vector2(old_illustration, "m_AnchoredPosition")[1] + vector2(old_illustration, "m_SizeDelta")[1]
        check("illustration_has_25_unit_top_inset_and_preserves_396_unit_bottom",
              illustration["m_AnchorMin"] == illustration["m_AnchorMax"] == [0.5, 1.0]
              and illustration["m_Pivot"] == [0.5, 1.0]
              and illustration["m_AnchoredPosition"] == [0.0, -25.0]
              and illustration["m_SizeDelta"] == [550.0, 371.0]
              and illustration_bottom == old_bottom == 396.0,
              before_bottom=old_bottom, current_bottom=illustration_bottom)
        preview = geometry["monster_preview"]
        check("monster_preview_width_is_550_with_unchanged_vertical_geometry",
              preview["m_SizeDelta"][0] == 550.0
              and preview["m_SizeDelta"][1] == vector2(old[layout["monster_preview"]], "m_SizeDelta")[1]
              and preview["m_AnchoredPosition"] == vector2(old[layout["monster_preview"]], "m_AnchoredPosition"))
        item_group = geometry["item_title_group"]
        check("item_title_group_is_at_322_5_with_unchanged_x",
              item_group["m_AnchoredPosition"][1] == 322.5
              and item_group["m_AnchoredPosition"][0] == vector2(old[layout["item_title_group"]], "m_AnchoredPosition")[0])
        stable_roles = ("character_name", "affection", "character_scroll", "monster_scroll",
                        "monster_grid", "character_grid", "left_page", "right_page")
        check("native_name_lower_affection_scroll_grids_and_page_geometry_preserved",
              all(old[layout[role]]["text"] == new[layout[role]]["text"] for role in stable_roles),
              preserved_records={role: layout[role] for role in stable_roles})

        count = new[layout["count_object"]]
        check("hidden_count_preserved", field(count, "m_Name") == "Count"
              and field(count, "m_IsActive") == "0"
              and old[layout["count_object"]]["text"] == count["text"])
        full_view = new[layout["full_view_rect"]]
        check("full_view_one_unit_bottom_right_inset_preserved",
              vector2(full_view, "m_AnchorMin") == vector2(full_view, "m_AnchorMax")
              == vector2(full_view, "m_Pivot") == [1.0, 0.0]
              and vector2(full_view, "m_AnchoredPosition") == [-1.0, 1.0]
              and old[layout["full_view_rect"]]["text"] == full_view["text"])
        check("hierarchy_and_component_order_preserved", hierarchy_projection(old) == hierarchy_projection(new))
        check("all_serialized_fileid_and_guid_references_preserved", reference_projection(old) == reference_projection(new))
        old_presentation = presentation_projection(old)
        check("all_font_sprite_material_and_aspect_fit_fields_preserved",
              old_presentation == presentation_projection(new),
              inspected_record_count=len(old_presentation),
              inspected_field_count=sum(len(fields) for fields in old_presentation.values()))

        protected = json.loads((EVIDENCE / "protected-before-hashes.json").read_text(encoding="utf-8-sig"))
        if not isinstance(protected, list) or len(protected) != 10:
            raise ValueError("Protected hash manifest must contain the ten baseline files")
        protected_results = []
        for entry in protected:
            path = Path(entry["Path"])
            expected_hash = entry["Hash"].upper()
            actual_hash = sha256(path) if path.is_file() else None
            protected_results.append({"path": str(path), "expected_sha256": expected_hash,
                                      "actual_sha256": actual_hash, "unchanged": actual_hash == expected_hash})
        check("ten_protected_runtime_item_art_catalog_global_ui_and_test_files_unchanged",
              all(entry["unchanged"] for entry in protected_results), files=protected_results)
        report["passed"] = all(entry["passed"] for entry in report["checks"])
        exit_code = 0 if report["passed"] else 1
    except (OSError, ValueError, KeyError, TypeError) as error:
        report["error"] = f"{type(error).__name__}: {error}"
    REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    failed = [entry["name"] for entry in report["checks"] if not entry["passed"]]
    print(json.dumps({"passed": report["passed"], "report": str(REPORT),
                      "failed_checks": failed, "error": report.get("error")}, ensure_ascii=False))
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
