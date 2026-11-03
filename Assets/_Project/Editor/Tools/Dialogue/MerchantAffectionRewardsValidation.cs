using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Pure/native checks and a separate preview scene; never registers fake global services or accesses save slots.</summary>
public static class MerchantAffectionRewardsValidation
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> Checks = new();

    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run validation outside Play Mode.");
        Checks.Clear();
        UnityEngine.Random.State random = UnityEngine.Random.state;
        try
        {
            VerifyEntitlements();
            VerifyGold();
            VerifyOffers();
            VerifyRarity();
            VerifyLocalization();
            RenderWorld();
            File.WriteAllLines(MerchantAffectionRewardsAuthoring.Output + "/validation.txt", Checks.Concat(new[] {
                $"PASS: {Checks.Count} native checks. Real user save slots and global service registrations were not touched.",
                "Full manual Play Mode walkthrough and Player build were not executed." }));
            Debug.Log($"MERCHANT_REWARDS_PASS: {Checks.Count} checks.");
        }
        finally { UnityEngine.Random.state = random; }
    }

    public static void RunBatch()
    {
        try { Run(); if (Application.isBatchMode) EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); if (Application.isBatchMode) EditorApplication.Exit(1); }
    }

    public static void AuthorAndRunBatch()
    {
        try { MerchantAffectionRewardsAuthoring.Apply(); RunBatch(); }
        catch (Exception error) { Debug.LogException(error); if (Application.isBatchMode) EditorApplication.Exit(1); }
    }

    private static void VerifyEntitlements()
    {
        var npc = AssetDatabase.LoadAssetAtPath<NPCData>(NpcAffectionDialogueAuthoring.MerchantPath);
        Check(npc.affectionRewards.Select(reward => reward.targetLevel).SequenceEqual(new[] { 1, 2, 3, 4 }), "Authored reward thresholds are 1/2/3/4");
        Check(NpcAffectionDialogue.GetMaximumAffection(npc) == 4, "Maximum affection is now four");
        Check(npc.affectionDialogues.All(entry => entry.affectionLevel <= 1) && npc.affectionDialogues.Count == 4,
            "Four reviewed cumulative candidates remain available through all reward levels");
        string json = File.ReadAllText(Path.ChangeExtension(NpcAffectionDialogueAuthoring.InkPath, ".json"));
        foreach (var entry in npc.affectionDialogues)
        {
            var story = new Ink.Runtime.Story(json); story.ChoosePathString(entry.startPath);
            while (story.canContinue) story.Continue();
            Check(story.currentChoices.Count == 2, "Authored candidate has choices: " + entry.affectionLevel + "/" + entry.startPath);
        }
        var host = new GameObject("Merchant aggregation validation") { hideFlags = HideFlags.HideAndDontSave };
        var database = ScriptableObject.CreateInstance<NPCDatabase>();
        try
        {
            var manager = host.AddComponent<NPCManager>();
            database.npcList.Add(npc); Set(manager, "database", database);
            for (int level = 0; level <= 4; level++)
            {
                var profile = new GameData(); profile.affectionData.affectionRecords.Add(new AffectionRecord(npc.id, level));
                profile = JsonUtility.FromJson<GameData>(JsonUtility.ToJson(profile));
                var delta = Aggregate(manager, profile, new GamePlayData());
                Check(delta.specialDiscountMinPercent == (level >= 1 ? 10 : 0) && delta.specialDiscountMaxPercent == (level >= 1 ? 20 : 0), "Reloaded tier " + level + " special-offer entitlement");
                Check(delta.goldRetentionLimit == (level >= 2 ? 500 : 0), "Reloaded tier " + level + " gold-retention entitlement");
                Check(Mathf.Approximately(delta.rareRelicWeightBonus, level >= 3 ? .1f : 0f) && Mathf.Approximately(delta.epicRelicWeightBonus, level >= 3 ? .15f : 0f), "Reloaded tier " + level + " rarity entitlement");
                Check(Mathf.Approximately(delta.affectionDiscountRate, level >= 4 ? .05f : 0f), "Reloaded tier " + level + " global-discount entitlement");
                Check(Aggregate(manager, profile, new GamePlayData()).affectionDiscountRate == delta.affectionDiscountRate, "Repeated rebuild is non-compounding at tier " + level);
            }
            var saved = new GameData(); saved.affectionData.affectionRecords.Add(new AffectionRecord(npc.id, 1));
            var pending = new GamePlayData(); pending.pendingRunAffectionChanges.Add(new PendingRunAffectionChange(npc.id, 1));
            Check(Aggregate(manager, saved, pending).goldRetentionLimit == 500, "Pending current-run tier unlock participates in benefits");
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(database); }
    }

    private static ShopRunModifierDelta Aggregate(NPCManager manager, GameData profile, GamePlayData run)
    {
        Assembly assembly = typeof(RunModifierService).Assembly;
        object request = Activator.CreateInstance(assembly.GetType("RunModifierAggregationRequest"),
            new UpgradeSaveData(), Array.Empty<UpgradeNodeSO>(), manager, profile, run);
        object result = assembly.GetType("RunModifierAggregationService").GetMethod("Aggregate").Invoke(null, new[] { request });
        return (ShopRunModifierDelta)result.GetType().GetProperty("ShopModifiers").GetValue(result);
    }

    private static void VerifyGold()
    {
        Assembly assembly = typeof(GamePlayDataManager).Assembly;
        Type policy = assembly.GetType("RunSessionProgressCommitPolicy");
        Type requestType = assembly.GetType("RunSessionProgressCommitRequest");
        foreach (RunEndReason reason in new[] { RunEndReason.Defeat, RunEndReason.TimeOver, RunEndReason.Victory, RunEndReason.Abort })
            foreach (int amount in new[] { 0, 60, 100, 180, 300, 499, 500, 501, 800 })
            {
                var run = new GamePlayData { runGold = amount, lastRunEndReason = reason };
                var profile = new GameData { magicStone = 321 };
                object request = Activator.CreateInstance(requestType, run, profile, 500);
                policy.GetMethod("Commit").Invoke(null, new[] { request });
                int expected = reason == RunEndReason.Defeat || reason == RunEndReason.TimeOver ? Math.Min(amount, 500) : 0;
                Check(profile.retainedRunGold == expected, reason + " with " + amount + " gold retains " + expected);
                Check(profile.magicStone == 321, "Gold retention never changes magic stones: " + reason + "/" + amount);
                var reloaded = JsonUtility.FromJson<GameData>(JsonUtility.ToJson(profile));
                int transfer = (int)policy.GetMethod("ConsumeRetainedGold").Invoke(null, new object[] { reloaded });
                Check(transfer == expected && reloaded.retainedRunGold == 0, "Reloaded credit is consumed once: " + reason + "/" + amount);
                Check((int)policy.GetMethod("ConsumeRetainedGold").Invoke(null, new object[] { reloaded }) == 0, "Repeated transfer cannot duplicate gold: " + reason + "/" + amount);
            }
        var legacy = JsonUtility.FromJson<GameData>("{\"magicStone\":99}");
        Check(legacy.retainedRunGold == 0, "Old profile JSON defaults retained gold to zero");
        var noReward = new GameData();
        policy.GetMethod("Commit").Invoke(null, new[] { Activator.CreateInstance(requestType,
            new GamePlayData { runGold = 180, lastRunEndReason = RunEndReason.Defeat }, noReward, 0) });
        Check(noReward.retainedRunGold == 0, "Locked retention tier grants no carried gold");
    }

    private static void VerifyOffers()
    {
        Check(MerchantOfferPricing.Calculate(100, .1f, 20) == 72, "Global and special discounts combine multiplicatively");
        Check(MerchantOfferPricing.Calculate(1200, .05f, 20) == 912, "Tier-four price uses five-percent global and twenty-percent special");
        Check(MerchantOfferPricing.Calculate(650, .05f, 0) == 618, "Only the final price is rounded with Unity's integer policy");
        Check(MerchantOfferPricing.Calculate(1200, 0f, 20) == 960, "Removing global discount preserves the special discount");
        var state = Stock();
        MerchantOfferPricing.AssignSpecialOffer(state, 4, 0, 0);
        Check(!state.specialOfferAssigned && state.slots.All(entry => entry.specialDiscountPercent == 0), "Locked offer does not consume future assignment");
        state.slots[0].isSold = true;
        MerchantOfferPricing.AssignSpecialOffer(state, 4, 10, 20);
        Check(state.specialOfferAssigned && state.slots.Count(entry => entry.specialDiscountPercent > 0) == 1 && state.slots[0].specialDiscountPercent == 0, "Exactly one unsold item is selected");
        var special = state.slots.Single(entry => entry.specialDiscountPercent > 0);
        Check(special.specialDiscountPercent >= 10 && special.specialDiscountPercent <= 20, "Rolled special percentage is within 10-20 inclusive");
        string snapshot = JsonUtility.ToJson(state);
        for (int i = 0; i < 20; i++) MerchantOfferPricing.AssignSpecialOffer(state, 4, 10, 20);
        Check(JsonUtility.ToJson(state) == snapshot, "Repeated initialization does not reroll the selected item or percentage");
        state = JsonUtility.FromJson<MerchantRuntimeState>(snapshot);
        MerchantOfferPricing.AssignSpecialOffer(state, 4, 10, 20);
        Check(JsonUtility.ToJson(state) == snapshot, "DTO restore/reentry retains the same special offer");
        state.slots.Single(entry => entry.specialDiscountPercent > 0).isSold = true;
        MerchantOfferPricing.AssignSpecialOffer(state, 4, 10, 20);
        Check(state.slots.Count(entry => entry.specialDiscountPercent > 0) == 1 && state.slots.Single(entry => entry.specialDiscountPercent > 0).isSold, "Buying the special does not select another item");
        bool refreshed = new MerchantRunStateService().TryRefreshState(state, 1, 4, (_, excluded) => Stock().slots);
        Check(refreshed && !state.specialOfferAssigned, "A real stock refresh resets special-offer assignment");
        MerchantOfferPricing.AssignSpecialOffer(state, 4, 10, 20);
        Check(state.slots.Count(entry => entry.specialDiscountPercent > 0) == 1, "Newly refreshed stock receives one offer");
        var missingFlag = Stock(); missingFlag.slots[2].specialDiscountPercent = 15;
        MerchantOfferPricing.AssignSpecialOffer(missingFlag, 4, 10, 20);
        Check(missingFlag.slots.Count(entry => entry.specialDiscountPercent > 0) == 1, "Existing per-item offer is preserved even with an older assignment flag");
        var empty = new MerchantRuntimeState("empty", new List<MerchantStockEntryState> { MerchantStockEntryState.Empty() });
        MerchantOfferPricing.AssignSpecialOffer(empty, 1, 10, 20);
        Check(empty.specialOfferAssigned && empty.slots[0].specialDiscountPercent == 0, "Empty stock cannot receive a discount");
    }

    private static MerchantRuntimeState Stock() => new MerchantRuntimeState("test", Enumerable.Range(0, 4)
        .Select(index => new MerchantStockEntryState(InventoryItemKind.Relic, "item" + index, 1000) { undiscountedPrice = 1000 }).ToList());

    private static void VerifyRarity()
    {
        MethodInfo weight = typeof(ShopInventoryRoll).GetMethod("RelicWeight", BindingFlags.Static | BindingFlags.NonPublic);
        Check(Mathf.Approximately((float)weight.Invoke(null, new object[] { ItemRarity.Common, .1f, .15f }), 1f), "Common relic weight stays one");
        Check(Mathf.Approximately((float)weight.Invoke(null, new object[] { ItemRarity.Rare, .1f, .15f }), 1.1f), "Rare relic relative weight is 1.10");
        Check(Mathf.Approximately((float)weight.Invoke(null, new object[] { ItemRarity.Epic, .1f, .15f }), 1.15f), "Epic relic relative weight is 1.15");
        var relics = new[] { ScriptableObject.CreateInstance<RelicDefinition>(), ScriptableObject.CreateInstance<RelicDefinition>(), ScriptableObject.CreateInstance<RelicDefinition>() };
        try
        {
            relics[0].rarity = ItemRarity.Common; relics[1].rarity = ItemRarity.Rare; relics[2].rarity = ItemRarity.Epic;
            MethodInfo draw = typeof(ShopInventoryRoll).GetMethod("DrawRelicFromPool", BindingFlags.Static | BindingFlags.NonPublic);
            for (int seed = 0; seed < 100; seed++)
            {
                UnityEngine.Random.InitState(seed); int index = UnityEngine.Random.Range(0, 3);
                UnityEngine.Random.InitState(seed); var pool = relics.ToList();
                Check(ReferenceEquals(draw.Invoke(null, new object[] { pool, 0f, 0f }), relics[index]) && pool.Count == 2, "No-bonus roll preserves legacy RNG/removal: " + seed);
            }
            int[] counts = new int[3]; UnityEngine.Random.InitState(42110);
            for (int i = 0; i < 20000; i++)
            {
                var selected = draw.Invoke(null, new object[] { relics.ToList(), .1f, .15f });
                counts[Array.IndexOf(relics, selected)]++;
            }
            float[] expected = { 1f / 3.25f, 1.1f / 3.25f, 1.15f / 3.25f };
            for (int i = 0; i < counts.Length; i++) Check(Mathf.Abs(counts[i] / 20000f - expected[i]) < .02f, "Weighted candidate sampling matches normalized weight: " + relics[i].rarity);
            File.WriteAllText(MerchantAffectionRewardsAuthoring.Output + "/rarity-sample.txt", "20,000 seeded rolls, one eligible candidate per rarity: " + string.Join(", ", counts));
        }
        finally { foreach (var relic in relics) Object.DestroyImmediate(relic); }
    }

    private static void VerifyLocalization()
    {
        var npc = AssetDatabase.LoadAssetAtPath<NPCData>(NpcAffectionDialogueAuthoring.MerchantPath);
        var collection = LocalizationEditorSettings.GetStringTableCollection(GameText.TableName);
        foreach (var reward in npc.affectionRewards)
        {
            string key = GameText.AssetKey(reward.effect, "rewardText");
            Check(!string.IsNullOrEmpty(key), "Reward has a compiled runtime localization identity: " + reward.targetLevel);
            foreach (var table in collection.StringTables)
                Check(!string.IsNullOrWhiteSpace(table.GetEntry(key)?.Value), "Localized reward " + reward.targetLevel + "/" + table.LocaleIdentifier.Code);
        }
        foreach (var table in collection.StringTables)
            Check(table.GetEntry("shop.special_offer")?.Value.Contains("{0}") == true, "Special tag preserves numeric token: " + table.LocaleIdentifier.Code);
        var keys = npc.affectionRewards.Select(reward => GameText.AssetKey(reward.effect, "rewardText"))
            .Concat(new[] { "shop.special_offer" }).ToHashSet();
        string[] codes = { "ko", "en", "ja", "zh-Hans", "zh-Hant" };
        using var reader = File.OpenText("DataSheets/Localization/GameText.csv");
        foreach (var row in LocalizationSheetSetup.ReadRows(reader).Skip(1).Where(row => keys.Contains(row[0])))
            for (int i = 0; i < codes.Length; i++)
                Check(collection.StringTables.Single(table => table.LocaleIdentifier.Code == codes[i]).GetEntry(row[0])?.Value == row[i + 1],
                    "Localized reward/tag matches CSV: " + row[0] + "/" + codes[i]);
    }

    private static void RenderWorld()
    {
        foreach (string path in MerchantAffectionRewardsAuthoring.SlotPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var data = new SerializedObject(prefab.GetComponent<ShopSlot>());
            Check(data.FindProperty("originalPriceText").objectReferenceValue != null && data.FindProperty("specialOfferText").objectReferenceValue != null && data.FindProperty("specialOfferRoot").objectReferenceValue != null, "Authored price-tag bindings: " + Path.GetFileName(path));
        }
        var preview = new PreviewRenderUtility();
        // Neutral preview lighting for URP sprite assets when no live scene lighting is present.
        var unlitSprite = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NpcAffectionDialogueAuthoring.PrefabPath));
        preview.AddSingleGO(root);
        try
        {
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
            var merchant = root.GetComponentInChildren<MerchantNPC>(true);
            var merchantData = new SerializedObject(merchant);
            var anchors = merchantData.FindProperty("slotAnchors");
            string[] paths = { "Assets/_Project/Data/Items/Weapons/Definitions/WD_ApprenticeHeroSword.asset", "Assets/_Project/Data/Items/Relics/Definitions/RD_BronzeDice.asset", "Assets/_Project/Data/Items/Relics/Definitions/RD_BurningFeather.asset", "Assets/_Project/Data/Items/Consumables/CD_HealPotion.asset" };
            int[] bases = { 1200, 650, 950, 900 };
            for (int i = 0; i < 4; i++)
            {
                var anchor = (Transform)anchors.GetArrayElementAtIndex(i).FindPropertyRelative("anchor").objectReferenceValue;
                var slotObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MerchantAffectionRewardsAuthoring.SlotPaths[0]), anchor);
                slotObject.transform.localPosition = Vector3.zero;
                var slot = slotObject.GetComponent<ShopSlot>();
                foreach (var binding in slot.GetComponentsInChildren<LocalizeTmpFontEvent>(true))
                {
                    string key = binding.AssetReference.TableEntryReference.Key;
                    var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(key.Substring("font.".Length)));
                    binding.ApplyLocalizedFont(font);
                }
                var definition = AssetDatabase.LoadAssetAtPath<ScriptableObject>(paths[i]);
                var entry = new MerchantStockEntryState(definition.AsDef().Kind, definition.AsDef().ItemId, 0)
                { undiscountedPrice = bases[i], specialDiscountPercent = i == 2 ? 20 : 0 };
                entry.price = MerchantOfferPricing.Calculate(entry.undiscountedPrice, .05f, entry.specialDiscountPercent);
                Set(slot, "currentState", entry); Set(slot, "currentDefinition", definition); Call(slot, "RefreshView");
                var slotData = new SerializedObject(slot);
                var original = (TMP_Text)slotData.FindProperty("originalPriceText").objectReferenceValue;
                var final = (TMP_Text)slotData.FindProperty("priceText").objectReferenceValue;
                var badge = (TMP_Text)slotData.FindProperty("specialOfferText").objectReferenceValue;
                if (i == 2)
                {
                    var badgeBackground = badge.transform.Find("Background").GetComponent<SpriteRenderer>();
                    Check(badgeBackground.sprite != null && badgeBackground.sortingOrder < badge.GetComponent<Renderer>().sortingOrder,
                        "Special badge has an authored background behind its glyph renderer");
                }
                foreach (var label in slot.GetComponentsInChildren<TMP_Text>(true))
                    if (label.gameObject.activeInHierarchy)
                    {
                        label.ForceMeshUpdate(true);
                        Check(label.textInfo.characterCount > 0 && label.GetComponent<Renderer>().bounds.size.x > 0f, "Native localized label has visible glyphs: slot " + i + "/" + label.name);
                    }
                Check(original.gameObject.activeSelf && original.text.Contains(bases[i].ToString("N0")), "Actual original price text: slot " + i);
                Check(final.text.Contains(entry.price.ToString("N0")) && badge.gameObject.activeSelf == (i == 2), "Displayed payable price and special status agree with stock: slot " + i);
                if (i == 0)
                {
                    entry.isSold = true; Call(slot, "RefreshView");
                    Check(!original.gameObject.activeSelf && !badge.gameObject.activeSelf && final.text == "SOLD", "Sold state hides original/special and preserves existing SOLD label");
                    Set(slot, "currentState", null); Call(slot, "RefreshView");
                    Check(!original.gameObject.activeSelf && !badge.gameObject.activeSelf && string.IsNullOrEmpty(final.text), "Empty state hides prices and special tag");
                    Set(slot, "currentState", entry);
                    entry.isSold = false; Call(slot, "RefreshView");
                }
            }
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.sharedMaterial != null && (renderer.sharedMaterial.name == "Sprite-Lit-Default" || renderer.sharedMaterial.name == "Sprite-Unlit-Default")) renderer.sharedMaterial = unlitSprite;
            preview.camera.orthographic = true; preview.camera.orthographicSize = 2.65f;
            preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 100f;
            preview.camera.cullingMask = -1;
            preview.camera.scene = root.scene;
            preview.camera.transform.position = new Vector3(2.75f, .05f, -20f);
            preview.camera.transform.rotation = Quaternion.identity;
            preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.14f, .15f, .18f);
            preview.BeginStaticPreview(new Rect(0, 0, 1280, 720));
            File.WriteAllLines(MerchantAffectionRewardsAuthoring.Output + "/preview-renderers.txt",
                root.GetComponentsInChildren<Renderer>(true).Select(renderer =>
                    $"{renderer.name}: active={renderer.gameObject.activeInHierarchy}, enabled={renderer.enabled}, bounds={renderer.bounds}, material={renderer.sharedMaterial?.name}"));
            preview.Render(true);
            Texture2D image = preview.EndStaticPreview();
            File.WriteAllBytes(MerchantAffectionRewardsAuthoring.Output + "/merchant-price-tags-native.png", image.EncodeToPNG());
            var pixels = image.GetPixels32();
            var background = pixels[0];
            int painted = pixels.Count(pixel => Math.Abs(pixel.r - background.r) + Math.Abs(pixel.g - background.g) + Math.Abs(pixel.b - background.b) > 20);
            int shaderErrorPixels = pixels.Count(pixel => pixel.r > 220 && pixel.b > 220 && pixel.g < 50);
            Object.DestroyImmediate(image);
            Check(painted > 10000, "Rendered visible merchant/carpet/item price tags in an isolated native preview scene");
            Check(shaderErrorPixels < 20, "Native render contains no magenta shader-error surface");
        }
        finally { preview.Cleanup(); Object.DestroyImmediate(unlitSprite); }
    }

    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Hidden).SetValue(owner, value);
    private static void Call(object owner, string method) => owner.GetType().GetMethod(method, Hidden).Invoke(owner, null);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Checks.Add("PASS " + message); }
}
