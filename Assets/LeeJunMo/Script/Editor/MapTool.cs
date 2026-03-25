using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MapTool : EditorWindow
{
    [MenuItem("Tools/문(Door) 관리/ID 중복 검사 및 해결")]
    public static void ResolveDuplicateIDs()
    {
        // 씬 내의 모든 DoorObject 탐색
        DoorObject[] doors = FindObjectsByType<DoorObject>(FindObjectsSortMode.None);

        Dictionary<string, DoorObject> idRegistry = new Dictionary<string, DoorObject>();
        int fixedCount = 0;

        foreach (var door in doors)
        {
            // 1. ID가 아예 비어있는 경우
            if (string.IsNullOrEmpty(door.doorID))
            {
                Undo.RecordObject(door, "Generate Door ID");
                door.GenerateID();
                fixedCount++;
            }
            // 2. 누군가 이미 선점한 중복 ID인 경우
            else if (idRegistry.ContainsKey(door.doorID))
            {
                Undo.RecordObject(door, "Resolve Duplicate ID");
                door.GenerateID(); // 새로운 ID 강제 발급
                fixedCount++;

                Debug.LogWarning($"[중복 발견] '{door.name}'의 ID가 중복되어 새로 발급했습니다 -> {door.doorID}");
            }

            // 안전한 ID를 레지스트리에 등록
            if (!idRegistry.ContainsKey(door.doorID))
            {
                idRegistry.Add(door.doorID, door);
            }
        }

        if (fixedCount > 0)
        {
            Debug.Log($"✅ 완료! {fixedCount}개의 문 ID 문제를 해결했습니다.");
        }
        else
        {
            Debug.Log("✅ 모든 문의 ID가 정상(Unique)입니다.");
        }
    }
}
