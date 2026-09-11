using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>Edits room-wave order and membership without mutating source prefabs or runtime objects.</summary>
public sealed partial class RoomPieceEditorWindow
{
    [SerializeField] private string selectedWaveId;
    private string waveRemovalDestinationId;
    private ReorderableList waveList;
    private SerializedObject waveAuthoringObject;

    private List<RoomMonsterWaveDefinition> GetEditableWaves() =>
        RoomMonsterWaveDefinition.CopyOrDefault(selectedAuthoring.MonsterWaves);

    private string ResolvePlacementWaveId()
    {
        List<RoomMonsterWaveDefinition> waves = GetEditableWaves();
        return waves.Exists(w => w.id == selectedWaveId) ? selectedWaveId : waves[0].id;
    }

    private bool IsMonsterInSelectedWave(RoomObjectAuthoring monster) =>
        string.IsNullOrEmpty(selectedWaveId) || monster.MonsterWaveId == selectedWaveId;

    private void DrawMonsterWaveSection()
    {
        EditorGUILayout.LabelField("몬스터 웨이브", EditorStyles.boldLabel);
        var waves = GetEditableWaves();
        if (!string.IsNullOrEmpty(selectedWaveId) && !waves.Exists(w => w.id == selectedWaveId))
            selectedWaveId = null;

        var labels = new string[waves.Count + 1];
        labels[0] = "전체 보기";
        int selected = 0;
        for (int i = 0; i < waves.Count; i++)
        {
            labels[i + 1] = $"W{i + 1}: {waves[i].displayName}";
            if (waves[i].id == selectedWaveId) selected = i + 1;
        }
        int next = EditorGUILayout.Popup("표시 / 배치 웨이브", selected, labels);
        if (next != selected)
        {
            selectedWaveId = next == 0 ? null : waves[next - 1].id;
            SceneView.RepaintAll();
        }

        if (selectedAuthoring.MonsterWaves == null || selectedAuthoring.MonsterWaves.Count == 0)
        {
            EditorGUILayout.LabelField($"W1: Wave 1   /   {CountWaveMonsters(waves[0].id)}마리   /   대기 0초 (기존 방)");
            if (GUILayout.Button("1웨이브 설정 편집"))
            {
                Undo.RecordObject(selectedAuthoring, "Edit Default Monster Wave");
                selectedAuthoring.EditorSetMonsterWaves(waves);
                waveList = null;
                MarkWaveAuthoringDirty();
            }
        }
        else
        {
            EnsureWaveList();
            waveAuthoringObject.Update();
            waveList.DoLayoutList();
            if (waveAuthoringObject.ApplyModifiedProperties())
                MarkWaveAuthoringDirty();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ 웨이브 추가"))
            {
                waves.Add(new RoomMonsterWaveDefinition
                {
                    id = Guid.NewGuid().ToString("N"),
                    displayName = $"Wave {waves.Count + 1}",
                    startDelaySeconds = 1f
                });
                Undo.RecordObject(selectedAuthoring, "Add Monster Wave");
                selectedAuthoring.EditorSetMonsterWaves(waves);
                selectedWaveId = waves[waves.Count - 1].id;
                waveList = null;
                MarkWaveAuthoringDirty();
            }
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(selectedWaveId)))
            {
                if (GUILayout.Button("선택한 몬스터를 이 웨이브로 이동"))
                    MoveSelectedMonstersToWave();
            }
        }

        if (waves.Count > 1)
        {
            int destination = Mathf.Max(0, waves.FindIndex(w => w.id == waveRemovalDestinationId));
            var names = new string[waves.Count];
            for (int i = 0; i < names.Length; i++) names[i] = $"W{i + 1}: {waves[i].displayName}";
            destination = EditorGUILayout.Popup("웨이브 삭제 시 이동 대상", destination, names);
            waveRemovalDestinationId = waves[destination].id;
        }
        EditorGUILayout.HelpBox(
            "첫 입장 → W1 → 전멸 → 다음 웨이브 대기/소환 → 마지막 전멸 시 방 클리어. " +
            "목록을 드래그하면 순서만 바뀝니다. 전체 보기에서 새 몬스터는 첫 웨이브에 배치합니다. " +
            "다른 웨이브는 같은 위치를 사용해도 됩니다. 경보 종 이벤트의 웨이브와는 별개입니다.", MessageType.Info);
        DrawWaveOverlapWarnings();
    }

    private void EnsureWaveList()
    {
        if (waveList != null && waveAuthoringObject?.targetObject == selectedAuthoring)
            return;
        waveAuthoringObject = new SerializedObject(selectedAuthoring);
        waveList = new ReorderableList(waveAuthoringObject,
            waveAuthoringObject.FindProperty("monsterWaves"), true, true, false, true);
        waveList.elementHeight = EditorGUIUtility.singleLineHeight * 2f + 8f;
        waveList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "순서 / 이름 / 몬스터 수 / 시작 전 대기 (초)");
        waveList.drawElementCallback = (rect, index, active, focused) =>
        {
            SerializedProperty entry = waveList.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.LabelField(new Rect(rect.x, rect.y, 90f, rect.height),
                $"W{index + 1} ({CountWaveMonsters(entry.FindPropertyRelative("id").stringValue)}마리)");
            EditorGUI.PropertyField(new Rect(rect.x + 95f, rect.y, Mathf.Max(40f, rect.width - 95f), rect.height),
                entry.FindPropertyRelative("displayName"), GUIContent.none);
            rect.y += EditorGUIUtility.singleLineHeight + 2f;
            SerializedProperty delay = entry.FindPropertyRelative("startDelaySeconds");
            delay.floatValue = Mathf.Max(0f, EditorGUI.FloatField(rect, "시작 전 대기", delay.floatValue));
        };
        waveList.onSelectCallback = list =>
        {
            selectedWaveId = list.serializedProperty.GetArrayElementAtIndex(list.index)
                .FindPropertyRelative("id").stringValue;
            SceneView.RepaintAll();
        };
        waveList.onCanRemoveCallback = list => list.count > 1;
        waveList.onRemoveCallback = list => RemoveMonsterWave(list.index);
    }

    private void RemoveMonsterWave(int index)
    {
        waveAuthoringObject.ApplyModifiedProperties();
        var waves = GetEditableWaves();
        if (index < 0 || index >= waves.Count || waves.Count <= 1) return;
        RoomMonsterWaveDefinition removed = waves[index];
        int destination = waves.FindIndex(w => w.id == waveRemovalDestinationId && w.id != removed.id);
        if (destination < 0) destination = index == 0 ? 1 : 0;
        int count = CountWaveMonsters(removed.id);
        int choice = count == 0 ? 0 : EditorUtility.DisplayDialogComplex("웨이브 삭제",
            $"'{removed.displayName}'에 몬스터 {count}마리가 있습니다.\n" +
            $"'{waves[destination].displayName}'로 옮길까요, 몬스터도 삭제할까요?",
            "이동 후 삭제", "취소", "몬스터도 삭제");
        if (choice == 1) return;
        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Remove Monster Wave");
        foreach (RoomObjectAuthoring monster in GetRoomObjects(selectedAuthoring))
        {
            if (monster.Kind != RoomObjectKind.Monster || monster.MonsterWaveId != removed.id) continue;
            if (choice == 2) Undo.DestroyObjectImmediate(monster.gameObject);
            else
            {
                Undo.RecordObject(monster, "Move Monster Wave");
                monster.EditorSetMonsterWaveId(waves[destination].id);
                EditorUtility.SetDirty(monster);
            }
        }
        Undo.RecordObject(selectedAuthoring, "Remove Monster Wave");
        selectedWaveId = waves[destination].id;
        waves.RemoveAt(index);
        selectedAuthoring.EditorSetMonsterWaves(waves);
        Undo.CollapseUndoOperations(undo);
        waveAuthoringObject.Update();
        MarkWaveAuthoringDirty();
    }

    private void DrawMonsterWavePopup(SerializedProperty property)
    {
        var waves = GetEditableWaves();
        string current = RoomMonsterWaveDefinition.ResolveId(property.stringValue);
        int index = waves.FindIndex(w => w.id == current);
        var labels = new string[waves.Count];
        for (int i = 0; i < waves.Count; i++) labels[i] = $"W{i + 1}: {waves[i].displayName}";
        EditorGUI.BeginChangeCheck();
        int next = EditorGUILayout.Popup("웨이브", index, labels);
        if (EditorGUI.EndChangeCheck() && next >= 0)
        {
            property.stringValue = waves[next].id;
            SceneView.RepaintAll();
        }
        if (index < 0) EditorGUILayout.HelpBox($"없는 웨이브 ID: {current}", MessageType.Error);
    }

    private int CountWaveMonsters(string id)
    {
        int count = 0;
        foreach (var item in GetRoomObjects(selectedAuthoring))
            if (item.Kind == RoomObjectKind.Monster && item.MonsterWaveId == id) count++;
        return count;
    }

    private void MoveSelectedMonstersToWave()
    {
        var moved = new HashSet<RoomObjectAuthoring>();
        foreach (GameObject selected in Selection.gameObjects)
        {
            var monster = selected.GetComponentInParent<RoomObjectAuthoring>();
            if (monster == null || monster.Kind != RoomObjectKind.Monster ||
                monster.GetComponentInParent<RoomPieceAuthoring>() != selectedAuthoring || !moved.Add(monster)) continue;
            Undo.RecordObject(monster, "Move Monster Wave");
            monster.EditorSetMonsterWaveId(selectedWaveId);
            EditorUtility.SetDirty(monster);
        }
        MarkWaveAuthoringDirty();
    }

    private void MarkWaveAuthoringDirty()
    {
        EditorUtility.SetDirty(selectedAuthoring);
        RoomAuthoringWorkspace.MarkDirty();
        SceneView.RepaintAll();
        Repaint();
    }

    private void DrawWaveOverlapWarnings()
    {
        RoomObjectAuthoring[] objects = GetRoomObjects(selectedAuthoring);
        int overlapCount = 0;
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i].Kind != RoomObjectKind.Monster || !IsMonsterInSelectedWave(objects[i])) continue;
            for (int j = i + 1; j < objects.Length; j++)
            {
                if (objects[j].Kind == RoomObjectKind.Monster && objects[j].MonsterWaveId == objects[i].MonsterWaveId &&
                    (objects[i].transform.position - objects[j].transform.position).sqrMagnitude < 0.1225f)
                    overlapCount++;
            }
        }
        if (overlapCount > 0)
            EditorGUILayout.HelpBox($"같은 웨이브에서 서로 0.35 유닛 이내로 겹치는 배치가 {overlapCount}쌍 있습니다.", MessageType.Warning);
    }

    private void DrawWaveSceneHandles(SceneView sceneView)
    {
        if (selectedAuthoring == null || currentStep != AuthoringStep.Objects) return;
        var waves = GetEditableWaves();
        Color previous = Handles.color;
        foreach (var item in GetRoomObjects(selectedAuthoring))
        {
            if (item.Kind != RoomObjectKind.Monster) continue;
            int index = waves.FindIndex(w => w.id == item.MonsterWaveId);
            bool selected = IsMonsterInSelectedWave(item);
            Color color = selected ? Color.cyan : new Color(0.45f, 0.45f, 0.45f, 0.45f);
            Handles.color = color;
            Vector3 position = item.transform.position;
            if (!selected)
            {
                // Scene-only dim overlay; never change renderer colors on authored objects/prefabs.
                foreach (var renderer in item.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    Bounds bounds = renderer.bounds;
                    var corners = new[]
                    {
                        new Vector3(bounds.min.x, bounds.min.y, position.z),
                        new Vector3(bounds.min.x, bounds.max.y, position.z),
                        new Vector3(bounds.max.x, bounds.max.y, position.z),
                        new Vector3(bounds.max.x, bounds.min.y, position.z)
                    };
                    Handles.DrawSolidRectangleWithOutline(corners, new Color(0.1f, 0.1f, 0.1f, 0.55f), Color.clear);
                }
            }
            Handles.DrawWireDisc(position, Vector3.forward, selected ? 0.4f : 0.25f);
            var style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = color;
            Handles.Label(position + Vector3.up * 0.4f, $"W{index + 1}  {item.PlacementId}", style);
        }
        Handles.color = previous;
    }

    private static void ValidateMonsterWaves(RoomPieceAuthoring authoring, List<string> errors)
    {
        var waves = RoomMonsterWaveDefinition.CopyOrDefault(authoring.MonsterWaves);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var wave in waves)
        {
            if (string.IsNullOrWhiteSpace(wave.id) || !ids.Add(wave.id))
                errors.Add("웨이브 ID가 비어 있거나 중복됩니다.");
            if (float.IsNaN(wave.startDelaySeconds) || float.IsInfinity(wave.startDelaySeconds) || wave.startDelaySeconds < 0f)
                errors.Add($"{wave.displayName}: 웨이브 대기는 유한한 0 이상의 값이어야 합니다.");
        }
        foreach (var item in GetRoomObjects(authoring))
        {
            if (item.Kind != RoomObjectKind.Monster) continue;
            if (!ids.Contains(item.MonsterWaveId)) errors.Add($"{item.PlacementId}: 없는 웨이브를 참조합니다.");
            if (item.MonsterStageSet == null) continue;
            var prefabs = item.MonsterStageSet.StagePrefabs;
            if (prefabs == null || prefabs.Count == 0) errors.Add($"{item.PlacementId}: 몬스터 세트의 단계가 비어 있습니다.");
            else
                for (int i = 0; i < prefabs.Count; i++)
                    if (!IsPrefabCompatibleWithKind(prefabs[i], RoomObjectKind.Monster))
                        errors.Add($"{item.PlacementId}: 몬스터 세트의 {i + 1}단계에 유효한 Enemy 프리팹이 필요합니다.");
        }
    }
}
