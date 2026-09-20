// Run in an isolated Unity Editor project with current production assemblies.
// Pass -sashaInk <absolute MerchantDialogue_Animated.ink path>.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class SashaAffectionRegression
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run()
    {
        try
        {
            var args = Environment.GetCommandLineArgs();
            string inkPath = args[Array.IndexOf(args, "-sashaInk") + 1];
            var compiled = new Ink.Compiler(File.ReadAllText(inkPath)).Compile();
            Check(compiled != null, "Ink compilation");
            string json = File.ReadAllText(Path.ChangeExtension(inkPath, ".json"));
            Check(compiled.ToJson() == json, "Committed JSON matches source compilation");
            List<string> successTags = GetChoiceTags(json, 0);
            List<string> failureTags = GetChoiceTags(json, 1);
            Check(successTags.Contains("add_aff: 1") && !successTags.Contains("choice_fail"), "Choice 1 succeeds");
            Check(failureTags.Contains("choice_fail") && !failureTags.Exists(x => x.StartsWith("add_aff")), "Choice 2 fails without loss/gain");

            var save = new MemorySave();
            GameDataStore.RegisterBackend(save);
            var session = Component<GamePlayDataManager>();
            RunSessionStore.RegisterBackend(session);
            var affection = Component<AffectionManager>();
            var modifiers = Component<RunModifierService>();
            var npcManager = Component<NPCManager>();
            var npcDatabase = ScriptableObject.CreateInstance<NPCDatabase>();
            var sasha = ScriptableObject.CreateInstance<NPCData>();
            sasha.id = 1001;
            var effect = ScriptableObject.CreateInstance<ShopAffectionDiscountEffect>();
            sasha.affectionRewards = new List<AffectionReward> { new AffectionReward { targetLevel = 1, effect = effect } };
            npcDatabase.npcList.Add(sasha);
            Set(npcManager, "database", npcDatabase);
            var presentation = new GainView();
            affection.SetLinkedUI(presentation);
            var reward = new RewardView(session);
            RewardDisplayPlayback.RegisterBackend(reward);
            var handler = new GameObject("Tags").AddComponent<DialogueTagHandler>();
            handler.OnAffectionRequested += (npc, amount, done) => affection.AddAffection(npc, amount, done);
            int failures = 0;
            handler.OnChoiceFailureRequested += done => { failures++; done(); };
            int continuations = 0;
            handler.ProcessTags(failureTags, sasha, () => continuations++);
            Check(failures == 1 && continuations == 1 && affection.GetAffection(1001) == 0, "Failure continues without changing affection");
            Check(session.Data.affectionGainNpcIds.Count == 0, "Failure does not consume allowance");
            handler.ProcessTags(successTags, sasha, () => continuations++);
            Check(affection.GetAffection(1001) == 1 && reward.Count == 1 && presentation.Count == 1, "Hub first success grants one level/reward/presentation");
            Check(continuations == 2 && save.Data.affectionData.affectionRecords[0].amount == 1, "Hub success continues and persists");
            Check(Mathf.Approximately(modifiers.ShopModifiers.affectionDiscountRate, 0.2f), "Level 1 reward enables 20 percent discount");
            handler.ProcessTags(successTags, sasha, () => continuations++);
            Check(continuations == 3 && reward.Count == 1 && presentation.Count == 1, "Repeat dialogue continues without reward/animation");
            session.StartRun();
            handler.ProcessTags(successTags, sasha, () => continuations++);
            Check(continuations == 4 && affection.GetAffection(1001) == 1, "Hub allowance survives starting run");

            // Recreate DTO as during session restoration; a net loss must not restore the allowance.
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(session.Data), session.Data);
            Check(!affection.AddAffection(sasha, 1), "Serialized session retains allowance");
            affection.AddAffection(sasha, -1);
            Check(!affection.AddAffection(sasha, 1) && affection.GetAffection(1001) == 0, "Negative change cannot re-enable gain");
            var other = ScriptableObject.CreateInstance<NPCData>();
            other.id = 1002;
            affection.AddAffection(other, 1);
            Check(affection.GetAffection(1002) == 1, "Allowance is per NPC");
            session.EndRun(RunEndReason.Defeat);
            Check(session.Data.affectionGainNpcIds.Count == 0, "Run end resets allowance");
            int previous = affection.GetAffection(1001);
            affection.AddAffection(sasha, 1);
            Check(affection.GetAffection(1001) == previous + 1, "Next preparation cycle can gain again");

            // A fresh run earns level 1 while permanent profile remains unchanged until commit.
            session.ResetForDevelopmentStart();
            save.Data = new GameData();
            Call(affection, "LoadAffectionData");
            session.StartRun();
            affection.AddAffection(sasha, 1);
            Check(session.Data.pendingRunAffectionChanges.Count == 1, "Active run queues affection delta");
            Check(Mathf.Approximately(modifiers.ShopModifiers.affectionDiscountRate, 0.2f), "Pending run reward applies immediately");
            int rewardsBefore = reward.Count;
            affection.AddAffection(sasha, 1);
            Check(reward.Count == rewardsBefore && session.Data.pendingRunAffectionChanges[0].delta == 1, "Run repeat cannot stack reward/delta");
            CommitProgress(session.Data, save.Data);
            modifiers.ReloadFromSave();
            Check(save.Data.affectionData.affectionRecords[0].amount == 1 && Mathf.Approximately(modifiers.ShopModifiers.affectionDiscountRate, 0.2f), "Committed level restores discount");
            session.EndRun(RunEndReason.Defeat);
            affection.AddAffection(sasha, 1);
            modifiers.ReloadFromSave();
            Check(reward.Count == rewardsBefore && Mathf.Approximately(modifiers.ShopModifiers.affectionDiscountRate, 0.2f), "Level 2 does not repeat level 1 reward or stack discount");

            VerifyPrices(modifiers);
            Debug.Log("SASHA_AFFECTION_PASS: Ink branches; failure/repeat continuation; per-NPC hub/run allowance; session round-trip; negative change; run-end reset; pending/committed reward; non-stacking prices; legacy gold upgrade isolation.");
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    private static void VerifyPrices(RunModifierService modifiers)
    {
        var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
        weapon.weaponId = "Test.Sasha";
        var items = ScriptableObject.CreateInstance<ItemDatabase>();
        items.allWeapons = new List<WeaponDefinition> { weapon };
        var itemManager = Component<ItemManager>();
        Set(itemManager, "database", items);
        items.InitializeCache();
        var definition = ScriptableObject.CreateInstance<ShopDefinitionSO>();
        Set(definition, "usesRunGold", true);
        var shop = new GameObject("Shop").AddComponent<MerchantNPC>();
        Set(shop, "shopDefinition", definition);
        var stock = new MerchantRuntimeState("Sasha", new List<MerchantStockEntryState>
        {
            new MerchantStockEntryState(InventoryItemKind.Weapon, weapon.weaponId, 1200),
            new MerchantStockEntryState(InventoryItemKind.Weapon, weapon.weaponId, 1150) { isSold = true }
        });
        ShopRunModifierDelta source = modifiers.ShopModifiers;
        source.discountRate = 0.2f;
        source.shopSlotBonus = 5;
        source.shopRefreshCount = 3;
        Set(modifiers, "shopModifiers", source);
        var goldModifiers = (ShopRunModifierDelta)Call(shop, "ResolveShopModifiers");
        Check(goldModifiers.discountRate == 0 && goldModifiers.shopSlotBonus == 0 && goldModifiers.shopRefreshCount == 0, "Gold excludes legacy upgrades");
        var policy = MerchantShopPolicy.Resolve(definition, goldModifiers, 3);
        for (int i = 0; i < 4; i++) Call(shop, "ApplyEffectivePrices", stock, policy);
        Check(stock.slots[0].price == 960 && stock.slots[0].undiscountedPrice == 1200, "Repeated recalculation stays at 80 percent");
        Check(stock.slots[1].price == 920 && stock.slots[1].isSold, "Repricing preserves sold state");
        stock = JsonUtility.FromJson<MerchantRuntimeState>(JsonUtility.ToJson(stock));
        Call(shop, "ApplyEffectivePrices", stock, policy);
        Check(stock.slots[0].price == 960, "Restored stock does not compound discount");
        Call(shop, "ApplyEffectivePrices", stock, MerchantShopPolicy.Resolve(definition, default, 3));
        Check(stock.slots[0].price == 1200, "Removing discount restores original price");
        stock.slots[0].undiscountedPrice = 0;
        Call(shop, "ApplyEffectivePrices", stock, policy);
        Check(stock.slots[0].price == 960, "Old DTO zero default adopts original price");
        Set(definition, "usesRunGold", false);
        var hubPolicy = MerchantShopPolicy.Resolve(definition, new ShopRunModifierDelta { affectionDiscountRate = 0.2f }, 3);
        Call(shop, "ApplyEffectivePrices", stock, hubPolicy);
        Check(stock.slots[0].price == 96, "Hub base 120 price becomes 96");
        var combined = MerchantShopPolicy.Resolve(definition, source, 3);
        Check(Mathf.Approximately(combined.DiscountRate, 0.4f), "Hub combines existing upgrade and affection discounts additively");
    }

    private static List<string> GetChoiceTags(string json, int index)
    {
        var story = new Ink.Runtime.Story(json);
        while (story.canContinue) story.Continue();
        Check(story.currentChoices.Count == 2, "Two merchant choices");
        story.ChooseChoiceIndex(index);
        var tags = new List<string>();
        while (story.canContinue) { story.Continue(); tags.AddRange(story.currentTags); }
        return tags;
    }

    private static void CommitProgress(GamePlayData run, GameData profile)
    {
        Assembly assembly = typeof(GamePlayDataManager).Assembly;
        Type request = assembly.GetType("RunSessionProgressCommitRequest");
        object value = Activator.CreateInstance(request, run, profile);
        assembly.GetType("RunSessionProgressCommitPolicy").GetMethod("Commit").Invoke(null, new[] { value });
    }

    private static T Component<T>() where T : UnityEngine.Component
    {
        T instance = new GameObject(typeof(T).Name).AddComponent<T>();
        typeof(T).GetProperty("Instance").SetValue(null, instance);
        return instance;
    }
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Hidden).SetValue(owner, value);
    private static object Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Hidden).Invoke(owner, args);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    private sealed class GainView : IAffectionPresentationView
    {
        public int Count;
        public bool IsPresentationActive => true;
        public void Setup(int level) { }
        public void PlayGainAnimation(int from, int to, Action done) { Count++; done(); }
    }
    private sealed class RewardView : IRewardDisplayBackend
    {
        public int Count;
        public UnityEngine.Component BackendComponent { get; }
        public RewardView(UnityEngine.Component host) { BackendComponent = host; }
        public void ShowUpgradeReward(UpgradeNodeSO node, Action done) => done?.Invoke();
        public void ShowFlowOwnedReward(List<UpgradeEffectSO> upgrades, List<AffectionEffect> affection, Action done)
        { Count++; done?.Invoke(); }
    }
    private sealed class MemorySave : IGameDataStoreBackend
    {
        public GameData Data { get; set; } = new GameData();
        public int ActiveSlotIndex => 0;
        public event Action<GameData, int> OnDataLoaded { add { } remove { } }
        public GameData EnsureData() => Data;
        public void SaveData() { }
        public void RequestImmediateSave(UnityEngine.Object requester) { }
        public void RequestDeferredSave(UnityEngine.Object requester) { }
        public void FlushSave(UnityEngine.Object requester) { }
    }
}
