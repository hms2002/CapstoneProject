"""Read-only audit against the task's pre-authoring backups. Writes evidence only."""
import csv
import hashlib
import io
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = Path(__file__).resolve().parent
REPORT = ROOT / "Library/CodexSpiriAlcohol/authoring.json"


def read(path):
    return Path(path).read_text(encoding="utf-8-sig")


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()


def load(name):
    return json.loads(read(OUTPUT / name))


def normalize(value):
    # Unity flow mappings contain only scalar references/vectors here. Ordering is cosmetic.
    def flow(match):
        pieces = [part.strip() for part in match.group(1).split(",")]
        return "{" + ",".join(sorted(pieces)) + "}"
    return re.sub(r"\{([^{}]*)\}", flow, "\n".join(line.rstrip() for line in value.strip().splitlines()))


def records(path):
    source = read(path)
    headers = list(re.finditer(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\s*$", source, re.M))
    result = {}
    for index, header in enumerate(headers):
        body = source[header.end():headers[index + 1].start() if index + 1 < len(headers) else len(source)]
        properties = list(re.finditer(r"^  ([A-Za-z_][\w]*):", body, re.M))
        fields = {}
        for number, prop in enumerate(properties):
            fields[prop.group(1)] = normalize(body[prop.start():properties[number + 1].start() if number + 1 < len(properties) else len(body)])
        result[header.group(2)] = {"class": int(header.group(1)), "fields": fields}
    return result


def differences(before, after, exclude=()):
    return [key for key in sorted(set(before) | set(after)) if key not in exclude and before.get(key) != after.get(key)]


def hash_group(rows):
    changed = []
    for row in rows:
        path = Path(row["path"])
        if not path.exists() or digest(path) != row["hash"]:
            changed.append(str(path))
    return {"checked": len(rows), "unchanged": len(rows) - len(changed), "changed": changed, "passed": not changed}


def audit_player(row):
    before = records(row["backup"])
    after = records(row["path"])
    root_id = next(key for key, rec in before.items() if rec["class"] == 1 and rec["fields"].get("m_Name") == "m_Name: PF Player")
    transform_id = next(key for key, rec in before.items() if rec["class"] == 4 and rec["fields"].get("m_GameObject") == "m_GameObject: {fileID: " + root_id + "}")
    guid = re.search(r"^guid: (\w+)$", read(ROOT / "Assets/_Project/Runtime/Features/Player/Input/PlayerIntentInput2D.cs.meta"), re.M).group(1)
    input_id = next(key for key, rec in before.items() if guid in rec["fields"].get("m_Script", ""))
    missing = list(set(before) - set(after))
    original_order_preserved = [key for key in after if key in before] == list(before)
    changed_records, unexpected, cosmetic = [], [], []
    allowed = {root_id: ("m_Component",), transform_id: ("m_Children",), input_id: ("confusionTag",)}
    for key, record in before.items():
        if key not in after:
            continue
        changed = differences(record["fields"], after[key]["fields"])
        if not changed:
            continue
        changed_records.append({"id": key, "class": record["class"], "fields": changed})
        extra = [field for field in changed if field not in allowed.get(key, ())]
        if extra:
            unexpected.append({"id": key, "fields": extra})
    append_preserved = {}
    for key, field in ((root_id, "m_Component"), (transform_id, "m_Children")):
        old = before[key]["fields"][field].splitlines()[1:]
        new = after.get(key, {}).get("fields", {}).get(field, "").splitlines()[1:]
        append_preserved[field] = new[:len(old)] == old
    return {"original_records": len(before), "new_records": len(after) - len(before), "missing": missing,
            "original_id_order_preserved": original_order_preserved, "allowed_record_changes": changed_records,
            "original_lists_preserved": append_preserved, "unexpected_record_changes": unexpected,
            "passed": not missing and not unexpected and original_order_preserved and all(append_preserved.values())}


def audit_asset(row, allowed, expected=None):
    before, after = records(row["backup"]), records(row["path"])
    changes = []
    for key, rec in before.items():
        diff = differences(rec["fields"], after.get(key, {}).get("fields", {}))
        changes.extend(diff)
    serialization_only = []
    for key in changes:
        old = next(iter(before.values()))["fields"].get(key, "")
        new = next(iter(after.values()))["fields"].get(key, "")
        if key == "roleIcon" and not old and new == "roleIcon: {fileID: 0}":
            serialization_only.append({"field": key, "reason": "Existing NPCData roleIcon default null reference materialized by Unity."})
        elif key == "description":
            def scalar(value):
                text = value.split(":", 1)[1].strip()
                if text.startswith('"'):
                    return json.loads(re.sub(r"\n\s+", " ", text))
                return text
            if scalar(old) == scalar(new):
                serialization_only.append({"field": key, "reason": "Same Korean text; Unity escaped/folded the YAML scalar."})
        elif key.startswith("audio") and re.search(r"^\s*Id:\s*$", old, re.M) and re.search(r"^\s*key:\s*$", new, re.M):
            if inactive_defaults(new):
                serialization_only.append({"field": key, "reason": "Legacy empty sound reference saved as the current empty SoundRef structure."})
        elif key.startswith("cameraShake") and "Use: 0" in old and "Preset: {fileID: 0}" in old and inactive_defaults(new):
            serialization_only.append({"field": key, "reason": "Legacy disabled camera shake saved as current zero-amplitude structure."})
        elif key.startswith("presentation") and "Use: 0" in old and "Prefab: {fileID: 0}" in old and inactive_defaults(new):
            serialization_only.append({"field": key, "reason": "Legacy disabled presentation saved as current empty sound/effect/particle structure."})
    accepted = set(allowed) | {entry["field"] for entry in serialization_only}
    passed = set(before) == set(after) and all(key in accepted for key in changes)
    if expected is not None:
        passed = passed and any(rec["fields"].get(expected[0]) == expected[1] for rec in after.values())
    return {"raw_fields_changed": changes, "serialization_only_changes": serialization_only,
            "behavior_fields_changed": [key for key in changes if key not in {entry["field"] for entry in serialization_only}], "passed": passed}


def inactive_defaults(value):
    for line in value.splitlines()[1:]:
        if ":" not in line:
            continue
        scalar = line.split(":", 1)[1].strip()
        if scalar.startswith("{"):
            if any(number != "0" for number in re.findall(r":\s*(-?\d+(?:\.\d+)?)", scalar)):
                return False
        elif scalar not in ("", "0", "[]"):
            return False
    return True


def pairs(source):
    return dict(re.findall(r'\{ "([^"\r\n]+)", "([^"\r\n]+)" \}', source))


def audit_localization(rows, authoring):
    keyset = {row["key"] for row in authoring["texts"]}
    output = {}
    for row in rows:
        path = Path(row["path"])
        before, after = read(row["backup"]), read(path)
        if path.name == "GameText.csv":
            old = list(csv.DictReader(io.StringIO(before)))
            new = list(csv.DictReader(io.StringIO(after)))
            output["csv"] = {"old_rows": len(old), "added_rows": len(new) - len(old),
                "passed": new[:len(old)] == old and {entry["key"] for entry in new[len(old):]} == keyset}
        elif path.name == "AssetInventory.json":
            old, new = json.loads(before)["rows"], json.loads(after)["rows"]
            output["inventory"] = {"old_rows": len(old), "added_rows": len(new) - len(old),
                "passed": new[:len(old)] == old and {entry["key"] for entry in new[len(old):]} == keyset}
        elif path.name == "GameTextAssetKeys.g.cs":
            old, new = pairs(before), pairs(after)
            output["runtime_key_map"] = {"old_entries": len(old), "added_entries": len(new) - len(old),
                "passed": all(new.get(key) == value for key, value in old.items()) and set(new.values()) - set(old.values()) == keyset}
        else:
            old, new = records(row["backup"]), records(path)
            rec_old, rec_new = next(iter(old.values()))["fields"], next(iter(new.values()))["fields"]
            list_name = "m_Entries" if "Shared" in path.name else "m_TableData"
            other_fields = differences(rec_old, rec_new, (list_name,))
            def entries(value):
                matches = list(re.finditer(r"^  - m_Id: (\d+)$", value, re.M))
                return {match.group(1): normalize(value[match.start():matches[index + 1].start() if index + 1 < len(matches) else len(value)]) for index, match in enumerate(matches)}
            old_entries, new_entries = entries(rec_old[list_name]), entries(rec_new[list_name])
            changed = [key for key, value in old_entries.items() if new_entries.get(key) != value]
            output[path.name] = {"old_entries": len(old_entries), "added_entries": len(new_entries) - len(old_entries),
                "old_entries_changed": changed, "other_fields_changed": other_fields,
                "passed": not changed and not other_fields and len(new_entries) - len(old_entries) == len(keyset)}
    output["passed"] = all(entry["passed"] for entry in output.values())
    return output


def audit_guid():
    asset_paths = [
        "Assets/_Project/Resources/Tags/State.Confusion.asset", "Assets/_Project/Data/Abilities/Effects/GE_Puddle_Confusion.asset",
        "Assets/_Project/Data/Abilities/Effects/SHD_Puddle_Confusion.asset", "Assets/_Project/Data/Abilities/Effects/CBD_Puddle_Confusion.asset",
        "Assets/_Project/Data/Dialogue/Affection/SpiriAlcoholPuddleReward.asset", "Assets/_Project/Art/Materials/M_AlcoholMugFill.mat",
        "Assets/_Project/Art/Materials/M_AlcoholMugOverflowDrop.mat", "Assets/_Project/Art/Shaders/AlcoholMugFill.shader",
        "Assets/_Project/Runtime/Features/Player/Status/PlayerAlcoholExposure.cs", "Assets/_Project/Runtime/Presentation/Map/Puddles/AlcoholExposurePlayerVisual.cs",
        "Assets/_Project/Editor/Tools/Data/SpiriAlcoholRewardAuthoring.cs", "Assets/_Project/Tests/PlayMode/Procedural/AlcoholExposurePlayModeTests.cs",
        "Assets/_Project/Tests/PlayMode/Procedural/AlcoholExposurePlayerVisualPlayModeTests.cs", "Assets/_Project/Tests/PlayMode/Procedural/AlcoholPuddleContactPlayModeTests.cs",
    ]
    missing, guids = [], {}
    for path in asset_paths:
        meta = ROOT / (path + ".meta")
        if not meta.exists():
            missing.append(str(meta))
            continue
        guid = re.search(r"^guid: (\w+)$", read(meta), re.M).group(1)
        guids[path] = guid
    args = ["rg", "-l", "-F", "--glob", "*.meta"]
    for guid in guids.values():
        args += ["-e", "guid: " + guid]
    search_roots = ["Assets/_Project/Resources/Tags", "Assets/_Project/Data/Abilities/Effects", "Assets/_Project/Data/Dialogue/Affection",
        "Assets/_Project/Art/Materials", "Assets/_Project/Art/Shaders", "Assets/_Project/Runtime/Features/Player/Status",
        "Assets/_Project/Runtime/Presentation/Map/Puddles", "Assets/_Project/Editor/Tools/Data", "Assets/_Project/Tests/PlayMode/Procedural"]
    search = subprocess.run(args + search_roots, cwd=ROOT, check=False, capture_output=True, text=True)
    matches = search.stdout.splitlines()
    collisions = []
    for path in matches:
        guid = re.search(r"^guid: (\w+)$", read(ROOT / path), re.M).group(1)
        expected = [key + ".meta" for key, value in guids.items() if value == guid]
        if len(expected) != 1 or Path(path).as_posix() != expected[0]:
            collisions.append(path)
    return {"new_metadata_checked": len(guids), "search_roots": search_roots, "match_count": len(matches),
            "missing_metadata": missing, "collisions": collisions, "guids": guids,
            "passed": search.returncode == 0 and len(matches) == len(guids) and not missing and not collisions and len(set(guids.values())) == len(guids)}


def main():
    if not REPORT.exists():
        raise SystemExit("Authoring report is not present. Run only after coordinated authoring completion.")
    authoring = json.loads(read(REPORT))
    assets = load("approved-assets-before.json")
    baseline = load("baseline.json")
    result = {"authoring_report": str(REPORT), "existing_metadata": hash_group(load("existing-metadata.json")),
        "protected_settings_scenes": hash_group(baseline["protected"]), "assets": {},
        "localization": audit_localization(load("localization-before.json"), authoring), "new_guid_uniqueness": audit_guid()}
    for row in assets:
        filename = Path(row["path"]).name
        if filename == "PF Player.prefab": result["assets"][filename] = audit_player(row)
        elif filename == "GE_Puddle_AlcoholBuff.asset": result["assets"][filename] = audit_asset(row, ("duration",), ("duration", "duration: 5"))
        elif filename == "DragonBossNpc.asset": result["assets"][filename] = audit_asset(row, ("affectionRewards",))
        else: result["assets"][filename] = {"passed": digest(row["path"]) == row["hash"], "unchanged": digest(row["path"]) == row["hash"]}
    result["passed"] = result["existing_metadata"]["passed"] and result["protected_settings_scenes"]["passed"] and result["localization"]["passed"] and result["new_guid_uniqueness"]["passed"] and all(row["passed"] for row in result["assets"].values())
    (OUTPUT / "preservation-audit.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"passed": result["passed"], "metadata": result["existing_metadata"], "protected": result["protected_settings_scenes"], "asset_checks": result["assets"], "localization_passed": result["localization"]["passed"], "new_guid_passed": result["new_guid_uniqueness"]["passed"]}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
