using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class GameplayRuntimeDebugWindow : EditorWindow
{
    private UpgradeNodeSO[] upgradeNodes = System.Array.Empty<UpgradeNodeSO>();
    private NPCData[] npcAssets = System.Array.Empty<NPCData>();
    private string[] upgradeNodeNames = System.Array.Empty<string>();
    private string[] npcNames = System.Array.Empty<string>();

    private int selectedUpgradeIndex;
    private int selectedNpcIndex;
    private int magicStoneDelta = 1000;
    private int affectionDelta = 1;
    private Vector2 scrollPosition;

    [MenuItem("Tools/Runtime/Gameplay Debug Window")]
    public static void ShowWindow()
    {
        GetWindow<GameplayRuntimeDebugWindow>("Gameplay Debug");
    }

    private void OnEnable()
    {
        RefreshAssets();
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
    }

    private void OnGUI()
    {
        DrawToolbar();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Play mode only. Use this window to test currency, upgrades, affection, rewards, and run modifiers quickly.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        DrawRuntimeStatus();
        EditorGUILayout.Space(10f);
        DrawCurrencySection();
        EditorGUILayout.Space(10f);
        DrawUpgradeSection();
        EditorGUILayout.Space(10f);
        DrawAffectionSection();
        EditorGUILayout.Space(10f);
        DrawRunModifierSection();
        EditorGUILayout.Space(10f);
        DrawPersistenceSection();

        EditorGUILayout.EndScrollView();

        if (Application.isPlaying)
            Repaint();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Refresh Assets", EditorStyles.toolbarButton))
                RefreshAssets();

            GUILayout.FlexibleSpace();
        }
    }

    private void DrawRuntimeStatus()
    {
        EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);
        DrawStatusLine("GameDataManager", GameDataManager.Instance != null);
        DrawStatusLine("CurrencyManager", CurrencyManager.Instance != null);
        DrawStatusLine("UpgradeManager", UpgradeManager.Instance != null);
        DrawStatusLine("AffectionManager", AffectionManager.Instance != null);
        DrawStatusLine("RewardDisplayService", RewardDisplayService.Instance != null);
        DrawStatusLine("RunModifierService", RunModifierService.Instance != null);
    }

    private void DrawCurrencySection()
    {
        EditorGUILayout.LabelField("Currency", EditorStyles.boldLabel);

        if (CurrencyManager.Instance == null)
        {
            EditorGUILayout.HelpBox("CurrencyManager is not ready.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("Current Magic Stone", CurrencyManager.Instance.GetMagicStone().ToString());
        magicStoneDelta = EditorGUILayout.IntField("Delta", magicStoneDelta);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Stone"))
                CurrencyManager.Instance.AddMagicStone(Mathf.Max(0, magicStoneDelta));

            if (GUILayout.Button("Spend Stone"))
                CurrencyManager.Instance.SpendMagicStone(Mathf.Max(0, magicStoneDelta));
        }
    }

    private void DrawUpgradeSection()
    {
        EditorGUILayout.LabelField("Upgrade", EditorStyles.boldLabel);

        if (UpgradeManager.Instance == null)
        {
            EditorGUILayout.HelpBox("UpgradeManager is not present in the current scene.", MessageType.Warning);
            return;
        }

        selectedUpgradeIndex = DrawAssetPopup("Upgrade Node", selectedUpgradeIndex, upgradeNodeNames);
        UpgradeNodeSO selectedNode = GetSelectedUpgradeNode();

        if (selectedNode == null)
        {
            EditorGUILayout.HelpBox("No UpgradeNodeSO assets found.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Price", selectedNode.price.ToString());
        EditorGUILayout.LabelField("Status", UpgradeManager.Instance.GetNodeStatus(selectedNode.nodeID).ToString());

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Buy Selected"))
                UpgradeManager.Instance.TryBuyUpgrade(selectedNode.nodeID);

            if (GUILayout.Button("Preview Reward"))
                RewardDisplayService.Instance?.ShowReward(selectedNode.effects, null);
        }

        if (GUILayout.Button("Toggle Upgrade UI"))
            UpgradeManager.Instance.ToggleUI();
    }

    private void DrawAffectionSection()
    {
        EditorGUILayout.LabelField("Affection", EditorStyles.boldLabel);

        if (AffectionManager.Instance == null)
        {
            EditorGUILayout.HelpBox("AffectionManager is not ready.", MessageType.Warning);
            return;
        }

        selectedNpcIndex = DrawAssetPopup("NPC", selectedNpcIndex, npcNames);
        NPCData selectedNpc = GetSelectedNpc();

        if (selectedNpc == null)
        {
            EditorGUILayout.HelpBox("No NPCData assets found.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Current Affection", AffectionManager.Instance.GetAffection(selectedNpc.id).ToString());
        affectionDelta = EditorGUILayout.IntField("Delta", affectionDelta);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Affection"))
                AffectionManager.Instance.AddAffection(selectedNpc, affectionDelta);

            if (GUILayout.Button("Preview Reward"))
                PreviewAffectionRewards(selectedNpc);
        }
    }

    private void DrawRunModifierSection()
    {
        EditorGUILayout.LabelField("Run Modifier", EditorStyles.boldLabel);

        if (RunModifierService.Instance == null)
        {
            EditorGUILayout.HelpBox("RunModifierService is not ready.", MessageType.Warning);
            return;
        }

        GraveRunModifierDelta modifiers = RunModifierService.Instance.GraveModifiers;
        ChestRunModifierDelta chestModifiers = RunModifierService.Instance.ChestModifiers;
        EditorGUILayout.LabelField("Weapon Grave Min Bonus", modifiers.weaponGraveMinBonus.ToString());
        EditorGUILayout.LabelField("Weapon Grave Max Bonus", modifiers.weaponGraveMaxBonus.ToString());
        EditorGUILayout.LabelField("Relic Grave Min Bonus", modifiers.relicGraveMinBonus.ToString());
        EditorGUILayout.LabelField("Relic Grave Max Bonus", modifiers.relicGraveMaxBonus.ToString());
        EditorGUILayout.LabelField("Weapon Drop Min Bonus", modifiers.weaponDropMinBonus.ToString());
        EditorGUILayout.LabelField("Weapon Drop Max Bonus", modifiers.weaponDropMaxBonus.ToString());
        EditorGUILayout.LabelField("Relic Drop Min Bonus", modifiers.relicDropMinBonus.ToString());
        EditorGUILayout.LabelField("Relic Drop Max Bonus", modifiers.relicDropMaxBonus.ToString());
        EditorGUILayout.LabelField("Chest Weapon Min Bonus", chestModifiers.chestWeaponMinBonus.ToString());
        EditorGUILayout.LabelField("Chest Weapon Max Bonus", chestModifiers.chestWeaponMaxBonus.ToString());
        EditorGUILayout.LabelField("Chest Relic Min Bonus", chestModifiers.chestRelicMinBonus.ToString());
        EditorGUILayout.LabelField("Chest Relic Max Bonus", chestModifiers.chestRelicMaxBonus.ToString());
        EditorGUILayout.LabelField("Rare Bonus", modifiers.extraRareChance.ToString("0.###"));
        EditorGUILayout.LabelField("Epic Bonus", modifiers.extraEpicChance.ToString("0.###"));
    }

    private void DrawPersistenceSection()
    {
        EditorGUILayout.LabelField("Persistence", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save Now"))
                GameDataManager.Instance?.SaveData();

            if (GUILayout.Button("Reload Data"))
            {
                GameDataManager.Instance?.LoadData();
                RunModifierService.Instance?.ReloadFromSave();
            }
        }
    }

    private void PreviewAffectionRewards(NPCData npcData)
    {
        if (npcData == null || RewardDisplayService.Instance == null)
            return;

        List<AffectionEffect> effects = new List<AffectionEffect>();
        if (npcData.affectionRewards != null)
        {
            foreach (AffectionReward reward in npcData.affectionRewards)
            {
                if (reward.effect != null)
                    effects.Add(reward.effect);
            }
        }

        if (effects.Count > 0)
            RewardDisplayService.Instance.ShowReward(null, effects);
    }

    private static void DrawStatusLine(string label, bool isReady)
    {
        Color previousColor = GUI.color;
        GUI.color = isReady ? new Color(0.7f, 1f, 0.7f) : new Color(1f, 0.7f, 0.7f);
        EditorGUILayout.LabelField(label, isReady ? "Ready" : "Missing");
        GUI.color = previousColor;
    }

    private static int DrawAssetPopup(string label, int currentIndex, string[] names)
    {
        if (names == null || names.Length == 0)
            return -1;

        if (currentIndex < 0 || currentIndex >= names.Length)
            currentIndex = 0;

        return EditorGUILayout.Popup(label, currentIndex, names);
    }

    private UpgradeNodeSO GetSelectedUpgradeNode()
    {
        if (selectedUpgradeIndex < 0 || selectedUpgradeIndex >= upgradeNodes.Length)
            return null;

        return upgradeNodes[selectedUpgradeIndex];
    }

    private NPCData GetSelectedNpc()
    {
        if (selectedNpcIndex < 0 || selectedNpcIndex >= npcAssets.Length)
            return null;

        return npcAssets[selectedNpcIndex];
    }

    private void RefreshAssets()
    {
        upgradeNodes = LoadAssetsByType<UpgradeNodeSO>();
        npcAssets = LoadAssetsByType<NPCData>();
        upgradeNodeNames = BuildNames(upgradeNodes, node => node != null ? node.upgradeName : "(None)");
        npcNames = BuildNames(npcAssets, npc => npc != null ? $"{npc.npcName} ({npc.id})" : "(None)");
        selectedUpgradeIndex = ClampIndex(selectedUpgradeIndex, upgradeNodes.Length);
        selectedNpcIndex = ClampIndex(selectedNpcIndex, npcAssets.Length);
    }

    private static T[] LoadAssetsByType<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        List<T> assets = new List<T>(guids.Length);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                assets.Add(asset);
        }

        assets.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        return assets.ToArray();
    }

    private static string[] BuildNames<T>(T[] assets, System.Func<T, string> selector)
    {
        if (assets == null || assets.Length == 0)
            return System.Array.Empty<string>();

        string[] names = new string[assets.Length];
        for (int i = 0; i < assets.Length; i++)
            names[i] = selector(assets[i]);

        return names;
    }

    private static int ClampIndex(int index, int count)
    {
        if (count <= 0)
            return -1;

        return Mathf.Clamp(index, 0, count - 1);
    }

    private void HandlePlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode)
            Repaint();
    }
}
