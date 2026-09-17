#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityGAS;
using Object = UnityEngine.Object;

public sealed class PotionRewardPlayModeTests
{
    private const string Rewards = "Assets/_Project/Data/Progression/Leveling/Rewards/";
    private GameObject owner;
    private AttributeSet attributes;
    private PlayerConsumableInventory inventory;
    private ConsumableDefinition potion;
    private AttributeCatalogSO catalog;
    private AttributeDefinition attackSpeed;
    private AttributeDefinition moveSpeed;
    private ILevelRewardEffectHandle handle;
    private GamePlayDataManager runManager;

    [SetUp]
    public void SetUp()
    {
        runManager = GamePlayDataManager.EnsureInstance();
        runManager.ResetForDevelopmentStart();
        runManager.StartRun();
        potion = Load<ConsumableDefinition>("Assets/_Project/Data/Items/Consumables/CD_HealPotion.asset");
        attackSpeed = LoadGuid<AttributeDefinition>("40277bede661488094dd09d158341e30");
        moveSpeed = LoadGuid<AttributeDefinition>("23db6468e859c6f42b0230a5af7ca81c");
        catalog = ScriptableObject.CreateInstance<AttributeCatalogSO>();
        SetField(catalog, "attributes", new[] { potion.TargetAttribute, attackSpeed, moveSpeed });
        owner = new GameObject("PotionRewardTestOwner");
        owner.SetActive(false);
        attributes = owner.AddComponent<AttributeSet>();
        SetField(attributes, "attributeCatalog", catalog);
        inventory = owner.AddComponent<PlayerConsumableInventory>();
        owner.AddComponent<PlayerInteractor2D>().enabled = false;
        owner.SetActive(true);
        Assert.That(attributes.TrySetBaseValue(potion.TargetAttribute, 50f, potion), Is.True);
    }

    [TearDown]
    public void TearDown()
    {
        handle?.Dispose();
        handle = null;
        Object.DestroyImmediate(owner);
        Object.DestroyImmediate(catalog);
        runManager.ResetForDevelopmentStart();
    }

    [Test]
    public void SuccessfulUse_EmitsOnceAfterSlotConsumption_AndFullHealthDoesNotConsume()
    {
        int count = 0;
        inventory.ConsumableUsed += used =>
        {
            Assert.That(used, Is.SameAs(potion));
            Assert.That(inventory.GetConsumableInSlot(0), Is.Null);
            count++;
        };
        Assert.That(inventory.TryAcquire(potion), Is.True);
        Assert.That(count, Is.Zero);
        Assert.That(inventory.TryUseAt(0), Is.True);
        Assert.That(count, Is.EqualTo(1));
        Assert.That(attributes.GetCurrentValue(potion.TargetAttribute), Is.EqualTo(51f));
        Assert.That(inventory.TryUseAt(0), Is.False);
        attributes.TrySetBaseValue(potion.TargetAttribute, potion.TargetAttribute.maxValue, potion);
        inventory.TryAcquire(potion);
        Assert.That(inventory.TryUseAt(0), Is.False);
        Assert.That(inventory.GetConsumableInSlot(0), Is.SameAs(potion));
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void RecoveryPendant_IsPotionOnly_AndRestoreAttachDoesNotStack()
    {
        var relic = Load<RelicDefinition>("Assets/_Project/Data/Items/Relics/Definitions/RD_RecoveryPendant.asset");
        var ctx = new RelicContext { owner = owner, token = relic, relicDef = relic, level = 1 };
        var other = Object.Instantiate(potion);
        try
        {
            relic.logic.OnEquipped(ctx);
            relic.logic.OnRestoreAttached(ctx);
            Assert.That(inventory.GetRestoreAmount(potion), Is.EqualTo(2));
            Assert.That(inventory.GetRestoreAmount(other), Is.EqualTo(1));
            Assert.That(potion.RestoreAmount, Is.EqualTo(1), "Never mutate shared potion data.");
            inventory.TryAcquire(potion);
            Assert.That(inventory.TryUseAt(0), Is.True);
            Assert.That(attributes.GetCurrentValue(potion.TargetAttribute), Is.EqualTo(52f));
            attributes.TryModifyAttributeValue(potion.TargetAttribute, 1f, potion);
            Assert.That(attributes.GetCurrentValue(potion.TargetAttribute), Is.EqualTo(53f), "Other heals stay unchanged.");
        }
        finally
        {
            relic.logic.OnUnequipped(ctx);
            Object.DestroyImmediate(other);
        }
        Assert.That(inventory.GetRestoreAmount(potion), Is.EqualTo(1));
        relic.logic.OnRestoreAttached(ctx);
        relic.logic.OnRestoreDetached(ctx);
        Assert.That(inventory.GetRestoreAmount(potion), Is.EqualTo(1));
    }

    [Test]
    public void Growth_CountsOnlySuccessfulPotionUsesAfterAcquisition()
    {
        var effect = Load<PotionGrowthLevelRewardEffectSO>(Rewards + "Effects/Effect_PotionGrowth.asset");
        var state = new LevelRewardEffectState(effect.EffectId);
        float baseline = attributes.GetAttributeValue(attackSpeed);
        inventory.TryAcquire(potion);
        inventory.TryUseAt(0); // Drinking before selection must not be counted.
        handle = effect.Apply(Context(effect, state));
        Assert.That(attributes.GetAttributeValue(attackSpeed), Is.EqualTo(baseline));
        for (int i = 1; i <= 3; i++)
        {
            inventory.TryAcquire(potion);
            Assert.That(inventory.TryUseAt(0), Is.True);
            Assert.That(attributes.GetAttributeValue(attackSpeed), Is.EqualTo(baseline * (1f + i * 0.02f)).Within(0.0001f));
        }
        Assert.That(state.json, Does.Contain("\"uses\":3"));
        attributes.TrySetBaseValue(potion.TargetAttribute, potion.TargetAttribute.maxValue, potion);
        inventory.TryAcquire(potion);
        Assert.That(inventory.TryUseAt(0), Is.False);
        Assert.That(state.json, Does.Contain("\"uses\":3"));
    }

    [Test]
    public void Cheer_TwoPotionUsesRefreshWithoutStacking_AndDisposeClearsBuff()
    {
        var effect = Load<PotionCheerLevelRewardEffectSO>(Rewards + "Effects/Effect_PotionCheer.asset");
        float baseAttack = attributes.GetAttributeValue(attackSpeed);
        float baseMove = attributes.GetAttributeValue(moveSpeed);
        var state = new LevelRewardEffectState(effect.EffectId);
        handle = effect.Apply(Context(effect, state));
        Assert.That(owner.GetComponent<PlayerStatusRuntime>().ActiveStatusCount, Is.Zero);
        for (int i = 0; i < 2; i++)
        {
            inventory.TryAcquire(potion);
            Assert.That(inventory.TryUseAt(0), Is.True);
            Assert.That(attributes.GetAttributeValue(attackSpeed), Is.EqualTo(baseAttack * 1.15f).Within(0.0001f));
            Assert.That(attributes.GetAttributeValue(moveSpeed), Is.EqualTo(baseMove * 1.3f).Within(0.0001f));
            Assert.That(owner.GetComponent<PlayerStatusRuntime>().ActiveStatusCount, Is.EqualTo(1));
        }
        handle.Dispose();
        handle = null;
        Assert.That(attributes.GetAttributeValue(attackSpeed), Is.EqualTo(baseAttack).Within(0.0001f));
        Assert.That(attributes.GetAttributeValue(moveSpeed), Is.EqualTo(baseMove).Within(0.0001f));
        Assert.That(owner.GetComponent<PlayerStatusRuntime>().ActiveStatusCount, Is.Zero);
    }

    [Test]
    public void Growth_RestoresRunCountWithoutCompounding_AndFreshRunStartsAtZero()
    {
        var effect = Load<PotionGrowthLevelRewardEffectSO>(Rewards + "Effects/Effect_PotionGrowth.asset");
        float baseline = attributes.GetAttributeValue(attackSpeed);
        var state = new LevelRewardEffectState(effect.EffectId) { json = "{\"uses\":5}" };
        handle = effect.Apply(Context(effect, state));
        Assert.That(attributes.GetAttributeValue(attackSpeed), Is.EqualTo(baseline * 1.1f).Within(0.0001f));
        handle.Dispose();
        Assert.That(attributes.GetAttributeValue(attackSpeed), Is.EqualTo(baseline).Within(0.0001f));
        handle = effect.Apply(Context(effect, state));
        Assert.That(attributes.GetAttributeValue(attackSpeed), Is.EqualTo(baseline * 1.1f).Within(0.0001f));
        handle.Dispose();
        handle = effect.Apply(Context(effect, new LevelRewardEffectState(effect.EffectId)));
        Assert.That(attributes.GetAttributeValue(attackSpeed), Is.EqualTo(baseline).Within(0.0001f));
    }

    [UnityTest]
    public IEnumerator Cheer_ReapplyPreservesDeadline_AndExpirationRemovesBothStatsAndHud()
    {
        var effect = Load<PotionCheerLevelRewardEffectSO>(Rewards + "Effects/Effect_PotionCheer.asset");
        float baseAttack = attributes.GetAttributeValue(attackSpeed);
        float baseMove = attributes.GetAttributeValue(moveSpeed);
        string deadline = (Time.time + 0.25f).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var state = new LevelRewardEffectState(effect.EffectId) { json = "{\"expiresAt\":" + deadline + "}" };
        handle = effect.Apply(Context(effect, state));
        Assert.That(attributes.GetAttributeValue(attackSpeed), Is.EqualTo(baseAttack * 1.15f).Within(0.0001f));
        Assert.That(attributes.GetAttributeValue(moveSpeed), Is.EqualTo(baseMove * 1.3f).Within(0.0001f));
        Assert.That(owner.GetComponent<PlayerStatusRuntime>().ActiveStatusCount, Is.EqualTo(1));
        handle.Dispose();
        handle = effect.Apply(Context(effect, state));
        Assert.That(owner.GetComponent<PlayerStatusRuntime>().ActiveStatusCount, Is.EqualTo(1));
        yield return new WaitForSeconds(0.35f);
        Assert.That(attributes.GetAttributeValue(attackSpeed), Is.EqualTo(baseAttack).Within(0.0001f));
        Assert.That(attributes.GetAttributeValue(moveSpeed), Is.EqualTo(baseMove).Within(0.0001f));
        Assert.That(owner.GetComponent<PlayerStatusRuntime>().ActiveStatusCount, Is.Zero);
    }

    [Test]
    public void AuthoredCards_AreUniquePersistentTriggers_WithImmediateArtwork()
    {
        var rewardCatalog = Load<LevelRewardCatalogSO>(Rewards + "Catalog/LevelRewardCatalog.asset");
        var original = Load<LevelRewardDefinitionSO>(Rewards + "Definitions/Reward_SoulHeartGrant.asset");
        foreach (string name in new[] { "PotionGrowth", "PotionCheer" })
        {
            var reward = Load<LevelRewardDefinitionSO>(Rewards + "Definitions/Reward_" + name + ".asset");
            Assert.That(rewardCatalog.Rewards, Does.Contain(reward));
            Assert.That(reward.AllowMultipleSelections, Is.False);
            Assert.That(reward.Icon, Is.SameAs(original.Icon));
            Assert.That(reward.Effects[0].Lifetime, Is.EqualTo(LevelRewardEffectLifetime.Persistent));
        }
    }

    private LevelRewardApplyContext Context(LevelRewardEffectSO effect, LevelRewardEffectState state)
        => new LevelRewardApplyContext(owner.GetComponent<PlayerInteractor2D>(), new LevelProgressionState(),
            new LevelRewardSelectionState(effect.EffectId), state, true);

    private static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.That(asset, Is.Not.Null, path);
        return asset;
    }

    private static T LoadGuid<T>(string guid) where T : Object => Load<T>(AssetDatabase.GUIDToAssetPath(guid));
    private static void SetField(object target, string name, object value)
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
#endif
