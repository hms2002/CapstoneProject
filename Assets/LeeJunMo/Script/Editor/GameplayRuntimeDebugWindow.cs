using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class GameplayRuntimeDebugWindow : EditorWindow
{
    private static readonly string[] ItemTabNames = { "Weapon", "Relic", "Consumable" };
    private const float ItemCardWidth = 220f;

    private UpgradeNodeSO[] upgradeNodes = System.Array.Empty<UpgradeNodeSO>();
    private NPCData[] npcAssets = System.Array.Empty<NPCData>();
    private ItemDatabase[] itemDatabases = System.Array.Empty<ItemDatabase>();
    private WeaponDefinition[] weaponAssets = System.Array.Empty<WeaponDefinition>();
    private RelicDefinition[] relicAssets = System.Array.Empty<RelicDefinition>();
    private ConsumableDefinition[] consumableAssets = System.Array.Empty<ConsumableDefinition>();
    private string[] upgradeNodeNames = System.Array.Empty<string>();
    private string[] npcNames = System.Array.Empty<string>();
    private string[] itemDatabaseNames = System.Array.Empty<string>();

    private int selectedUpgradeIndex;
    private int selectedNpcIndex;
    private int selectedItemDatabaseIndex;
    private int selectedItemTabIndex;
    private int magicStoneDelta = 1000;
    private int affectionDelta = 1;
    private Vector2 scrollPosition;
    private Vector2 itemGridScrollPosition;
    private string itemGrantStatus = "No item grant action yet.";

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
        DrawItemGrantSection();
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
        DrawStatusLine("Item Grant Assets", HasAnyGrantableItemAssets());
        DrawStatusLine("Current Player", ResolveCurrentPlayer() != null);
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
                RewardDisplayService.Instance?.ShowUpgradeReward(selectedNode);
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

    private void DrawItemGrantSection()
    {
        EditorGUILayout.LabelField("Item Grant", EditorStyles.boldLabel);

        if (itemDatabases.Length > 1)
        {
            int previousDatabaseIndex = selectedItemDatabaseIndex;
            selectedItemDatabaseIndex = DrawAssetPopup("Item Database", selectedItemDatabaseIndex, itemDatabaseNames);
            if (selectedItemDatabaseIndex != previousDatabaseIndex)
                RefreshItemAssets();
        }

        ItemDatabase database = GetSelectedItemDatabase();
        EditorGUILayout.LabelField("Database", database != null ? database.name : "None");
        EditorGUILayout.HelpBox("Lists include selected ItemDatabase entries plus unregistered item definition assets found in the project.", MessageType.Info);
        EditorGUILayout.HelpBox(itemGrantStatus, MessageType.None);

        if (!HasAnyGrantableItemAssets())
        {
            EditorGUILayout.HelpBox("No item definition assets found. Refresh assets after creating or importing item definitions.", MessageType.Warning);
            return;
        }

        selectedItemTabIndex = GUILayout.Toolbar(selectedItemTabIndex, ItemTabNames);
        itemGridScrollPosition = EditorGUILayout.BeginScrollView(itemGridScrollPosition, GUILayout.MinHeight(260f));

        switch (selectedItemTabIndex)
        {
            case 0:
                DrawWeaponGrantGrid();
                break;
            case 1:
                DrawRelicGrantGrid();
                break;
            case 2:
                DrawConsumableGrantGrid();
                break;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawWeaponGrantGrid()
    {
        if (weaponAssets.Length == 0)
        {
            EditorGUILayout.HelpBox("No WeaponDefinition assets found.", MessageType.Info);
            return;
        }

        DrawItemGrid(
            weaponAssets,
            weapon => weapon != null ? GetDisplayName(weapon.DisplayName, weapon.name) : "(Missing Weapon)",
            weapon => weapon != null ? weapon.weaponId : string.Empty,
            BuildWeaponInventoryStatus,
            GrantWeapon);
    }

    private void DrawRelicGrantGrid()
    {
        if (relicAssets.Length == 0)
        {
            EditorGUILayout.HelpBox("No RelicDefinition assets found.", MessageType.Info);
            return;
        }

        DrawItemGrid(
            relicAssets,
            relic => relic != null ? GetDisplayName(relic.DisplayName, relic.name) : "(Missing Relic)",
            relic => relic != null ? relic.relicId : string.Empty,
            BuildRelicInventoryStatus,
            GrantRelic);
    }

    private void DrawConsumableGrantGrid()
    {
        if (consumableAssets.Length == 0)
        {
            EditorGUILayout.HelpBox("No ConsumableDefinition assets found.", MessageType.Info);
            return;
        }

        DrawItemGrid(
            consumableAssets,
            consumable => consumable != null ? GetDisplayName(consumable.DisplayName, consumable.name) : "(Missing Consumable)",
            consumable => consumable != null ? consumable.consumableId : string.Empty,
            BuildConsumableInventoryStatus,
            GrantConsumable);
    }

    private void DrawRunModifierSection()
    {
        EditorGUILayout.LabelField("Run Modifier", EditorStyles.boldLabel);

        if (RunModifierService.Instance == null)
        {
            EditorGUILayout.HelpBox("RunModifierService is not ready.", MessageType.Warning);
            return;
        }

        RunRewardModifierSnapshot snapshot = RunModifierService.CurrentRewardSnapshot;
        GraveRunModifierDelta modifiers = snapshot.GraveModifiers;
        ChestRunModifierDelta chestModifiers = snapshot.ChestModifiers;
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

    private void DrawItemGrid<T>(
        IReadOnlyList<T> items,
        System.Func<T, string> nameSelector,
        System.Func<T, string> idSelector,
        System.Func<T, string> statusSelector,
        System.Action<T> grantAction)
        where T : ScriptableObject
    {
        int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 48f) / ItemCardWidth));

        for (int i = 0; i < items.Count; i++)
        {
            if (i % columns == 0)
                EditorGUILayout.BeginHorizontal();

            T item = items[i];
            using (new EditorGUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(ItemCardWidth)))
            {
                EditorGUILayout.LabelField(nameSelector(item), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(idSelector(item), EditorStyles.miniLabel);
                EditorGUILayout.LabelField(statusSelector(item), EditorStyles.miniLabel);

                EditorGUI.BeginDisabledGroup(item == null);
                if (GUILayout.Button("Grant"))
                    grantAction(item);
                EditorGUI.EndDisabledGroup();
            }

            if (i % columns == columns - 1 || i == items.Count - 1)
                EditorGUILayout.EndHorizontal();
        }
    }

    private string BuildWeaponInventoryStatus(WeaponDefinition weapon)
    {
        if (weapon == null)
            return "Invalid definition";

        WeaponInventory2D inventory = ResolvePlayerComponent<WeaponInventory2D>();
        if (inventory == null)
            return "WeaponInventory2D missing";

        List<string> ids = inventory.GetAllWeaponIDs();
        string heldStatus = ids.Contains(weapon.weaponId) ? "Held" : "Not held";
        return $"{heldStatus} / {BuildRegistrationStatus(weapon, GetSelectedItemDatabase()?.allWeapons)}";
    }

    private string BuildRelicInventoryStatus(RelicDefinition relic)
    {
        if (relic == null)
            return "Invalid definition";

        RelicInventory inventory = ResolvePlayerComponent<RelicInventory>();
        if (inventory == null)
            return "RelicInventory missing";

        string heldStatus = inventory.TryGetRelicLevelById(relic.relicId, out int level)
            ? $"Level {level}/{Mathf.Max(1, relic.maxLevel)}"
            : "Not held";
        return $"{heldStatus} / {BuildRegistrationStatus(relic, GetSelectedItemDatabase()?.allRelics)}";
    }

    private string BuildConsumableInventoryStatus(ConsumableDefinition consumable)
    {
        if (consumable == null)
            return "Invalid definition";

        PlayerConsumableInventory inventory = ResolvePlayerComponent<PlayerConsumableInventory>();
        if (inventory == null)
            return "PlayerConsumableInventory missing";

        return $"Held {inventory.CountConsumable(consumable)}/{inventory.Capacity} / {BuildRegistrationStatus(consumable, GetSelectedItemDatabase()?.allConsumables)}";
    }

    private void GrantWeapon(WeaponDefinition weapon)
    {
        if (weapon == null)
        {
            itemGrantStatus = "Weapon grant failed: invalid definition.";
            return;
        }

        WeaponInventory2D inventory = ResolvePlayerComponent<WeaponInventory2D>();
        if (inventory == null)
        {
            itemGrantStatus = "Weapon grant failed: current player has no WeaponInventory2D.";
            return;
        }

        WeaponInventory2D.AcquireResult result = inventory.TryAcquireWithoutReplacementDetailed(weapon);
        itemGrantStatus = $"Weapon '{GetDisplayName(weapon.DisplayName, weapon.name)}' grant result: {FormatWeaponAcquireResult(result)}";
    }

    private void GrantRelic(RelicDefinition relic)
    {
        if (relic == null)
        {
            itemGrantStatus = "Relic grant failed: invalid definition.";
            return;
        }

        RelicInventory inventory = ResolvePlayerComponent<RelicInventory>();
        if (inventory == null)
        {
            itemGrantStatus = "Relic grant failed: current player has no RelicInventory.";
            return;
        }

        RelicInventory.AcquireResult result = inventory.TryAcquireOrUpgradeDetailed(relic);
        itemGrantStatus = $"Relic '{GetDisplayName(relic.DisplayName, relic.name)}' grant result: {FormatRelicAcquireResult(result)}";
    }

    private void GrantConsumable(ConsumableDefinition consumable)
    {
        if (consumable == null)
        {
            itemGrantStatus = "Consumable grant failed: invalid definition.";
            return;
        }

        PlayerConsumableInventory inventory = ResolvePlayerComponent<PlayerConsumableInventory>();
        if (inventory == null)
        {
            itemGrantStatus = "Consumable grant failed: current player has no PlayerConsumableInventory.";
            return;
        }

        PlayerConsumableInventory.AcquireResult result = inventory.TryAcquireDetailed(consumable);
        itemGrantStatus = $"Consumable '{GetDisplayName(consumable.DisplayName, consumable.name)}' grant result: {FormatConsumableAcquireResult(result)}";
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

    private ItemDatabase GetSelectedItemDatabase()
    {
        if (selectedItemDatabaseIndex < 0 || selectedItemDatabaseIndex >= itemDatabases.Length)
            return null;

        return itemDatabases[selectedItemDatabaseIndex];
    }

    private void RefreshAssets()
    {
        upgradeNodes = LoadAssetsByType<UpgradeNodeSO>();
        npcAssets = LoadAssetsByType<NPCData>();
        itemDatabases = LoadAssetsByType<ItemDatabase>();
        upgradeNodeNames = BuildNames(upgradeNodes, node => node != null ? node.upgradeName : "(None)");
        npcNames = BuildNames(npcAssets, npc => npc != null ? $"{npc.npcName} ({npc.id})" : "(None)");
        itemDatabaseNames = BuildNames(itemDatabases, database => database != null ? database.name : "(None)");
        selectedUpgradeIndex = ClampIndex(selectedUpgradeIndex, upgradeNodes.Length);
        selectedNpcIndex = ClampIndex(selectedNpcIndex, npcAssets.Length);
        selectedItemDatabaseIndex = ClampIndex(selectedItemDatabaseIndex, itemDatabases.Length);
        RefreshItemAssets();
    }

    private void RefreshItemAssets()
    {
        ItemDatabase database = GetSelectedItemDatabase();
        if (database == null)
        {
            weaponAssets = ToSortedGrantAssetArray<WeaponDefinition>(null, weapon => GetDisplayName(weapon.DisplayName, weapon.name));
            relicAssets = ToSortedGrantAssetArray<RelicDefinition>(null, relic => GetDisplayName(relic.DisplayName, relic.name));
            consumableAssets = ToSortedGrantAssetArray<ConsumableDefinition>(null, consumable => GetDisplayName(consumable.DisplayName, consumable.name));
            return;
        }

        weaponAssets = ToSortedGrantAssetArray(database.allWeapons, weapon => GetDisplayName(weapon.DisplayName, weapon.name));
        relicAssets = ToSortedGrantAssetArray(database.allRelics, relic => GetDisplayName(relic.DisplayName, relic.name));
        consumableAssets = ToSortedGrantAssetArray(database.allConsumables, consumable => GetDisplayName(consumable.DisplayName, consumable.name));
    }

    private bool HasAnyGrantableItemAssets()
    {
        return weaponAssets.Length > 0 || relicAssets.Length > 0 || consumableAssets.Length > 0;
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

    private static T[] ToSortedNonNullArray<T>(IEnumerable<T> source, System.Func<T, string> sortKeySelector)
        where T : ScriptableObject
    {
        if (source == null)
            return System.Array.Empty<T>();

        List<T> result = new List<T>();
        foreach (T item in source)
        {
            if (item != null)
                result.Add(item);
        }

        result.Sort((a, b) => string.Compare(sortKeySelector(a), sortKeySelector(b), System.StringComparison.Ordinal));
        return result.ToArray();
    }

    private static T[] ToSortedGrantAssetArray<T>(IEnumerable<T> databaseEntries, System.Func<T, string> sortKeySelector)
        where T : ScriptableObject
    {
        List<T> result = new List<T>();
        AddUniqueAssets(result, databaseEntries);
        AddUniqueAssets(result, LoadAssetsByType<T>());
        result.Sort((a, b) => string.Compare(sortKeySelector(a), sortKeySelector(b), System.StringComparison.Ordinal));
        return result.ToArray();
    }

    private static void AddUniqueAssets<T>(List<T> target, IEnumerable<T> source)
        where T : ScriptableObject
    {
        if (target == null || source == null)
            return;

        foreach (T item in source)
        {
            if (item != null && !ContainsAssetReference(target, item))
                target.Add(item);
        }
    }

    private static bool ContainsAssetReference<T>(IEnumerable<T> source, T candidate)
        where T : ScriptableObject
    {
        if (source == null || candidate == null)
            return false;

        string candidatePath = AssetDatabase.GetAssetPath(candidate);
        foreach (T item in source)
        {
            if (item == null)
                continue;

            if (ReferenceEquals(item, candidate))
                return true;

            string itemPath = AssetDatabase.GetAssetPath(item);
            if (!string.IsNullOrEmpty(candidatePath) && candidatePath == itemPath)
                return true;
        }

        return false;
    }

    private static string BuildRegistrationStatus<T>(T item, IEnumerable<T> databaseEntries)
        where T : ScriptableObject
    {
        return ContainsAssetReference(databaseEntries, item) ? "Registered" : "Unregistered";
    }

    private static PlayerInteractor2D ResolveCurrentPlayer()
    {
        if (PlayerRuntimeRegistry.CurrentPlayer != null)
            return PlayerRuntimeRegistry.CurrentPlayer;

        return PlayerRuntimeRegistry.GetPlayerComponent<PlayerInteractor2D>();
    }

    private static T ResolvePlayerComponent<T>() where T : Component
    {
        PlayerInteractor2D player = PlayerRuntimeRegistry.CurrentPlayer;
        if (player != null)
        {
            T component = player.GetComponent<T>();
            if (component != null)
                return component;
        }

        return PlayerRuntimeRegistry.GetPlayerComponent<T>();
    }

    private static string GetDisplayName(string displayName, string fallback)
    {
        return !string.IsNullOrWhiteSpace(displayName) ? displayName : fallback;
    }

    private static string FormatWeaponAcquireResult(WeaponInventory2D.AcquireResult result)
    {
        return result switch
        {
            WeaponInventory2D.AcquireResult.Success => "Success",
            WeaponInventory2D.AcquireResult.InvalidDefinition => "Failed - invalid weapon definition",
            WeaponInventory2D.AcquireResult.InventoryFull => "Failed - weapon inventory is full",
            WeaponInventory2D.AcquireResult.DuplicateRejected => "Failed - duplicate weapon rejected",
            _ => $"Failed - {result}"
        };
    }

    private static string FormatRelicAcquireResult(RelicInventory.AcquireResult result)
    {
        return result switch
        {
            RelicInventory.AcquireResult.Success => "Success",
            RelicInventory.AcquireResult.InvalidDefinition => "Failed - invalid relic definition",
            RelicInventory.AcquireResult.InventoryFull => "Failed - relic inventory is full",
            RelicInventory.AcquireResult.AlreadyMaxLevel => "Failed - relic is already at max level",
            _ => $"Failed - {result}"
        };
    }

    private static string FormatConsumableAcquireResult(PlayerConsumableInventory.AcquireResult result)
    {
        return result switch
        {
            PlayerConsumableInventory.AcquireResult.Success => "Success",
            PlayerConsumableInventory.AcquireResult.InvalidDefinition => "Failed - invalid consumable definition",
            PlayerConsumableInventory.AcquireResult.InventoryFull => "Failed - consumable inventory is full",
            _ => $"Failed - {result}"
        };
    }

    private void HandlePlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode)
            Repaint();
    }
}
