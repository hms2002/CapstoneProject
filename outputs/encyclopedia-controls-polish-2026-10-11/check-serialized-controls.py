"""Verify the approved creature-control prefab changes without parsing or editing YAML.

Run after Unity authoring/import finishes. Uses only the Python standard library and
writes serialized-controls-report.json beside this script. Exit 0 means all checks
passed; exit 1 means a failed check, and exit 2 means missing/invalid input.
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
REPORT = EVIDENCE / "serialized-controls-report.json"
HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)[^\n]*\n", re.MULTILINE)


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def records(path):
    # Normalize only line endings/BOM, retaining every serialized field and value.
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
        result[record_id] = {
            "class": match.group(1),
            "text": text[match.start():end],
        }
        order.append(record_id)
    return text[:matches[0].start()], order, result


def field(record, name):
    match = re.search(r"^  " + re.escape(name) + r":\s*([^\n]*)$", record["text"], re.MULTILINE)
    if match is None:
        raise ValueError(f"Missing serialized field {name}")
    return match.group(1).strip()


def field_block(record, name):
    lines = record["text"].splitlines()
    marker = "  " + name + ":"
    for index, line in enumerate(lines):
        if not line.startswith(marker):
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


def control_ids(all_records):
    tab_id = by_identifier(all_records, "UI::EncyclopediaCreatureTab")
    detail_id = by_identifier(all_records, "UI::EncyclopediaCreatureDetailView")
    count_component_id = reference(all_records[tab_id], "entryCountText")
    count_object_id = reference(all_records[count_component_id], "m_GameObject")
    button_id = reference(all_records[detail_id], "fullViewButton")
    button_object_id = reference(all_records[button_id], "m_GameObject")
    component_block = field_block(all_records[button_object_id], "m_Component")
    if component_block is None:
        raise ValueError("Missing FullView GameObject component list")
    # Nested-prefab stripped RectTransforms have no m_GameObject field. Follow
    # the authored FullView object's own component list instead of scanning them.
    component_ids = re.findall(r"\bfileID:\s*(-?\d+)", component_block)
    rect_ids = [record_id for record_id in component_ids if all_records[record_id]["class"] == "224"]
    if len(rect_ids) != 1:
        raise ValueError(f"Expected one FullView RectTransform, found {len(rect_ids)}")
    if reference(all_records[rect_ids[0]], "m_GameObject") != button_object_id:
        raise ValueError("FullView RectTransform does not point back to its GameObject")
    return {"tab": tab_id, "detail": detail_id, "count_component": count_component_id,
            "count_object": count_object_id, "full_view_button": button_id,
            "full_view_object": button_object_id, "full_view_rect": rect_ids[0]}


def without_fields(record, names):
    return "\n".join(line for line in record["text"].splitlines()
                     if not any(line.startswith("  " + name + ":") for name in names))


def vector2(record, name):
    values = dict(re.findall(r"([xy]):\s*([-+\d.eE]+)", field(record, name)))
    if set(values) != {"x", "y"}:
        raise ValueError(f"Invalid Vector2 in {name}")
    return [float(values["x"]), float(values["y"])]


def hierarchy_projection(all_records):
    names = ("m_Component", "m_GameObject", "m_Children", "m_Father", "m_RootOrder")
    return {record_id: {name: field_block(record, name) for name in names}
            for record_id, record in all_records.items() if record["class"] in {"1", "4", "224"}}


def reference_projection(all_records):
    return {record_id: [line for line in record["text"].splitlines()
                        if re.search(r"\b(?:fileID|guid):", line)]
            for record_id, record in all_records.items()}


def presentation_projection(all_records):
    projection = {}
    for record_id, record in all_records.items():
        names = re.findall(r"^  ([^ :]+):", record["text"], re.MULTILINE)
        selected = {name: field_block(record, name) for name in names
                    if re.search(r"font|sprite|aspect", name, re.IGNORECASE)
                    or name in {"m_Material", "m_sharedMaterial"}}
        if selected:
            projection[record_id] = selected
    return projection


def main():
    report = {"passed": False, "before": str(BEFORE), "current": str(CURRENT), "checks": []}

    def check(name, passed, **details):
        report["checks"].append({"name": name, "passed": bool(passed), **details})

    exit_code = 2
    try:
        before_prefix, before_order, before = records(BEFORE)
        current_prefix, current_order, current = records(CURRENT)
        report["before_sha256"] = sha256(BEFORE)
        report["current_sha256"] = sha256(CURRENT)
        before_ids, current_ids = set(before), set(current)
        check("record_ids_preserved", before_ids == current_ids,
              before_count=len(before), current_count=len(current),
              added=sorted(current_ids - before_ids), removed=sorted(before_ids - current_ids))
        check("record_order_and_yaml_prefix_preserved",
              before_order == current_order and before_prefix == current_prefix)
        check("record_classes_preserved", all(before[key]["class"] == current[key]["class"]
                                              for key in before_ids & current_ids))

        old_controls = control_ids(before)
        controls = control_ids(current)
        report["control_record_ids"] = controls
        check("control_component_references_preserved", old_controls == controls)
        allowed_lines = (EVIDENCE / "authoring-allowed-ids.txt").read_text(encoding="utf-8-sig").splitlines()
        allowed_entries = [line.strip() for line in allowed_lines if line.strip()]
        if not all(re.fullmatch(r"-?\d+", line) for line in allowed_entries):
            raise ValueError("authoring-allowed-ids.txt contains a non-record-ID line")
        allowed = set(allowed_entries)
        expected = {controls["count_object"], controls["full_view_rect"]}
        check("authoring_allowlist_contains_only_two_requested_records",
              len(allowed_entries) == 2 and len(allowed) == 2 and allowed == expected,
              allowed_record_ids=sorted(allowed), expected_record_ids=sorted(expected))
        changed = {key for key in before_ids & current_ids if before[key]["text"] != current[key]["text"]}
        check("only_the_two_authorized_records_changed", changed == expected == allowed,
              changed_record_ids=sorted(changed), unauthorized=sorted(changed - expected))

        count_id = controls["count_object"]
        check("count_gameobject_inactive", current[count_id]["class"] == "1"
              and field(current[count_id], "m_Name") == "Count"
              and field(current[count_id], "m_IsActive") == "0")
        check("count_record_changes_only_active_flag",
              without_fields(before[count_id], {"m_IsActive"})
              == without_fields(current[count_id], {"m_IsActive"}))

        rect_id = controls["full_view_rect"]
        full_rect = current[rect_id]
        actual_vectors = {name: vector2(full_rect, name) for name in
                          ("m_AnchorMin", "m_AnchorMax", "m_Pivot", "m_AnchoredPosition", "m_SizeDelta")}
        check("full_view_is_anchored_at_bottom_right_with_one_unit_inset",
              actual_vectors["m_AnchorMin"] == [1.0, 0.0]
              and actual_vectors["m_AnchorMax"] == [1.0, 0.0]
              and actual_vectors["m_Pivot"] == [1.0, 0.0]
              and actual_vectors["m_AnchoredPosition"] == [-1.0, 1.0], **actual_vectors)
        rect_fields = {"m_AnchorMin", "m_AnchorMax", "m_Pivot", "m_AnchoredPosition"}
        check("full_view_rect_preserves_size_children_parent_and_other_fields",
              without_fields(before[rect_id], rect_fields) == without_fields(full_rect, rect_fields))
        check("hierarchy_and_component_order_preserved",
              hierarchy_projection(before) == hierarchy_projection(current))
        check("all_serialized_fileid_and_guid_references_preserved",
              reference_projection(before) == reference_projection(current))
        old_presentation = presentation_projection(before)
        new_presentation = presentation_projection(current)
        check("fonts_sprites_materials_and_aspect_fit_settings_preserved",
              old_presentation == new_presentation,
              inspected_record_count=len(old_presentation),
              inspected_field_count=sum(len(fields) for fields in old_presentation.values()))

        protected = json.loads((EVIDENCE / "protected-before-hashes.json").read_text(encoding="utf-8-sig"))
        if not isinstance(protected, list) or not protected:
            raise ValueError("Protected hash manifest must be a nonempty list")
        protected_results = []
        for entry in protected:
            path = Path(entry["Path"])
            expected_hash = entry["Hash"].upper()
            actual_hash = sha256(path) if path.is_file() else None
            protected_results.append({"path": str(path), "expected_sha256": expected_hash,
                                      "actual_sha256": actual_hash, "unchanged": actual_hash == expected_hash})
        check("protected_sources_catalog_and_global_ui_unchanged",
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
