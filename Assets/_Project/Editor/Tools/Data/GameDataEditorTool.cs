using UnityEditor;
using UnityEngine;

public class GameDataEditorTool : EditorWindow
{
    private GameDataRepository repository;
    private ItemDatabase itemDatabase;

    [MenuItem("Tools/Game Data Manager")]
    public static void ShowWindow()
    {
        GetWindow<GameDataEditorTool>("Data Reset Tool");
    }

    private void OnEnable()
    {
        repository = new GameDataRepository();

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
            EditorGUILayout.HelpBox("ItemDatabase 에셋을 연결해야 초기화가 가능합니다.", MessageType.Warning);

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("Save File Status", EditorStyles.boldLabel);
        bool fileExists = repository != null && repository.Exists();
        EditorGUILayout.LabelField("Path:", repository != null ? repository.SavePath : string.Empty, EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("Exists:", fileExists ? "YES" : "NO");

        EditorGUILayout.Space(10);
        DrawLine();
        EditorGUILayout.Space(10);

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

        GUI.color = Color.cyan;
        if (GUILayout.Button("Initialize Database & Sync Save", GUILayout.Height(40)))
            InitializeDatabaseAndSave();

        EditorGUILayout.Space(10);
        GUI.color = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("TOTAL RESET (Delete File)", GUILayout.Height(40)))
            TotalReset();

        GUI.color = Color.white;
    }

    private void DrawLine()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        rect.height = 1;
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1f));
    }

    private void ResetSection(string sectionName)
    {
        if (repository == null || !repository.Exists())
        {
            Debug.LogWarning("세이브 파일이 없어 초기화할 수 없습니다.");
            return;
        }

        if (!EditorUtility.DisplayDialog($"{sectionName} 초기화", $"정말로 {sectionName} 데이터를 초기화하시겠습니까?", "확인", "취소"))
            return;

        GameData data = repository.LoadOrCreate();

        switch (sectionName)
        {
            case "Affection":
                data.affectionData = new AffectionSaveData();
                break;
            case "Shortcuts":
                data.mapData = new MapSaveData();
                break;
            case "Items":
                data.itemData = new ItemSaveData();
                break;
            case "Upgrades":
                data.upgradeData = new UpgradeSaveData();
                break;
        }

        repository.Save(data);
        Debug.Log($"[GameDataEditorTool] {sectionName} 데이터 초기화 완료.");
    }

    private void InitializeDatabaseAndSave()
    {
        if (itemDatabase == null)
            return;

        if (!EditorUtility.DisplayDialog("DB 동기화", "ItemDatabase의 기본 해금 상태를 저장 파일에 반영하시겠습니까?", "확인", "취소"))
            return;

        GameData data = repository != null && repository.Exists() ? repository.LoadOrCreate() : new GameData();

        if (data.itemData == null)
            data.itemData = new ItemSaveData();

        data.itemData.unlockedWeaponIDs.Clear();
        data.itemData.unlockedRelicIDs.Clear();

        if (itemDatabase.defaultUnlockedWeapons != null)
        {
            foreach (var weapon in itemDatabase.defaultUnlockedWeapons)
            {
                if (weapon != null && !string.IsNullOrWhiteSpace(weapon.weaponId))
                    data.itemData.unlockedWeaponIDs.Add(weapon.weaponId);
            }
        }

        if (itemDatabase.defaultUnlockedRelics != null)
        {
            foreach (var relic in itemDatabase.defaultUnlockedRelics)
            {
                if (relic != null && !string.IsNullOrWhiteSpace(relic.relicId))
                    data.itemData.unlockedRelicIDs.Add(relic.relicId);
            }
        }

        repository.Save(data);
        Debug.Log("[GameDataEditorTool] ItemDatabase 기준으로 저장 데이터를 동기화했습니다.");
    }

    private void TotalReset()
    {
        if (repository == null || !repository.Exists())
            return;

        if (!EditorUtility.DisplayDialog("전체 초기화", "모든 세이브 파일을 삭제합니다. 되돌릴 수 없습니다.", "삭제", "취소"))
            return;

        repository.Delete();
        Debug.Log("[GameDataEditorTool] 세이브 파일 삭제 완료.");
    }
}
