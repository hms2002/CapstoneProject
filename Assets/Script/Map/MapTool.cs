using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MapTool : EditorWindow
{
    [MenuItem("Tools/문(Door) 관리/ID 중복 검사 및 해결")]
    public static void ResolveDuplicateIDs()
    {
        // [수정됨] 최신 API 사용 (정렬 안 함 = 더 빠름)
        DoorObject[] doors = FindObjectsByType<DoorObject>(FindObjectsSortMode.None);

        // ID별로 누가 쓰고 있는지 기록
        Dictionary<string, DoorObject> idRegistry = new Dictionary<string, DoorObject>();
        int fixedCount = 0;

        foreach (var door in doors)
        {
            // 1. ID가 비어있으면 생성
            if (string.IsNullOrEmpty(door.doorID))
            {
                Undo.RecordObject(door, "Generate Door ID");
                door.GenerateID();
                fixedCount++;
            }

            // 2. 이미 누가 쓰고 있는 ID라면? (중복 발생!)
            else if (idRegistry.ContainsKey(door.doorID))
            {
                Undo.RecordObject(door, "Resolve Duplicate ID");

                // 새로 발급
                door.GenerateID();
                fixedCount++;

                Debug.LogWarning($"[중복 발견] {door.name}의 ID가 중복되어 새로 발급했습니다 -> {door.doorID}");
            }

            // 레지스트리에 등록 (이제 이 ID는 점유됨)
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