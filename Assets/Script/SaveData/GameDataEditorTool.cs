using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class GameDataEditorTool : EditorWindow
{
    private string savePath;
    private ItemDatabase itemDatabase;

    [MenuItem("Tools/Game Data Manager")]
    public static void ShowWindow()
    {
        GetWindow<GameDataEditorTool>("Data Reset Tool");
    }

    private void OnEnable()
    {
        savePath = Path.Combine(Application.persistentDataPath, "GameSave.json");
        // 프로젝트 내의 ItemDatabase 에셋을 자동으로 찾아 연결 시도
        string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            itemDatabase = AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Database Reference", EditorStyles.boldLabel);
        itemDatabase = (ItemDatabase)EditorGUILayout.ObjectField("Target Database", itemDatabase, typeof(ItemDatabase), false);

        if (itemDatabase == null)
        {
            EditorGUILayout.HelpBox("ItemDatabase 에셋을 연결해야 초기화가 가능합니다!", MessageType.Warning);
        }

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("Save File Status", EditorStyles.boldLabel);
        bool fileExists = File.Exists(savePath);
        EditorGUILayout.LabelField("Path:", savePath, EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("Exists:", fileExists ? "YES" : "NO");

        EditorGUILayout.Space(10);
        DrawLine();
        EditorGUILayout.Space(10);

        // --- 섹션별 초기화 버튼들 ---
        EditorGUILayout.LabelField("Partial Resets", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Affection Reset", GUILayout.Height(30))) ResetSection("Affection");
            if (GUILayout.Button("Shortcut Reset", GUILayout.Height(30))) ResetSection("Shortcuts");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Item Unlock Reset", GUILayout.Height(30))) ResetSection("Items");
            if (GUILayout.Button("Upgrade Reset", GUILayout.Height(30))) ResetSection("Upgrades");
        }

        EditorGUILayout.Space(20);
        DrawLine();
        EditorGUILayout.Space(10);

        // --- 전체 초기화 및 DB 동기화 ---
        GUI.color = Color.cyan;
        if (GUILayout.Button("Initialize Database & Sync Save", GUILayout.Height(40)))
        {
            InitializeDatabaseAndSave();
        }

        EditorGUILayout.Space(10);
        GUI.color = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("TOTAL RESET (Delete File)", GUILayout.Height(40)))
        {
            TotalReset();
        }
        GUI.color = Color.white;
    }

    private void DrawLine()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        rect.height = 1;
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
    }

    // ---------------------------------------------------------------------------
    // 로직 부분
    // ---------------------------------------------------------------------------

    private void ResetSection(string sectionName)
    {
        if (!File.Exists(savePath)) { Debug.LogWarning("세이브 파일이 없어 초기화할 수 없습니다."); return; }
        if (!EditorUtility.DisplayDialog($"{sectionName} 초기화", $"정말로 {sectionName} 데이터를 초기화하시겠습니까?", "확인", "취소")) return;

        string json = File.ReadAllText(savePath);
        GameData data = JsonUtility.FromJson<GameData>(json);

        switch (sectionName)
        {
            case "Affection":
                // 호감도 딕셔너리 초기화 (GameData 내부 구조에 따라 수정 필요)
                // data.npcAffectionDic.Clear(); 
                break;
            case "Shortcuts":
                data.mapData = new MapSaveData(); // 숏컷 포함 맵 데이터 초기화
                break;
            case "Items":
                data.itemData = new ItemSaveData(); // 언락 리스트 초기화
                break;
            case "Upgrades":
                data.upgradeData = new UpgradeSaveData(); // 업그레이드 초기화
                break;
        }

        File.WriteAllText(savePath, JsonUtility.ToJson(data, true));
        Debug.Log($"[Tool] {sectionName} 데이터 초기화 완료.");
    }

    private void InitializeDatabaseAndSave()
    {
        if (itemDatabase == null) return;
        if (!EditorUtility.DisplayDialog("DB 동기화", "아이템 DB의 기본 상태를 세이브 파일에 적용하시겠습니까?", "확인", "취소")) return;

        GameData data = File.Exists(savePath) ? JsonUtility.FromJson<GameData>(File.ReadAllText(savePath)) : new GameData();

        // DB에 설정된 기본 해금 아이템들만 추출하여 세이브 데이터에 주입
        data.itemData.unlockedWeaponIDs.Clear();
        data.itemData.unlockedRelicIDs.Clear();

        // 예: itemDatabase.defaultUnlockedWeapons가 있다면 루프
        // foreach(var w in itemDatabase.defaultUnlockedWeapons) data.itemData.unlockedWeaponIDs.Add(w.weaponId);

        File.WriteAllText(savePath, JsonUtility.ToJson(data, true));
        Debug.Log("[Tool] 데이터베이스 기반 세이브 초기화 완료.");
    }

    private void TotalReset()
    {
        if (!File.Exists(savePath)) return;
        if (!EditorUtility.DisplayDialog("전체 초기화", "모든 세이브 파일을 삭제하고 초기화합니다. 복구할 수 없습니다!", "삭제", "취소")) return;

        File.Delete(savePath);
        // 메타 파일도 있을 수 있으나 persistentDataPath는 수동 삭제로 충분
        Debug.Log("[Tool] 세이브 파일 삭제 완료 (공장 초기화).");
    }
}