using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityGAS;
using Object = UnityEngine.Object;

public sealed class CombatFeelLootQuestPlayModeTests
{
    private readonly List<Object> owned = new();
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    [SetUp] public void SetUp()
    {
        // These fixtures do not exercise prewarm tracing; avoid its unrelated Editor JSON writer.
        Type.GetType("PrewarmTraceRuntime, Editor")?.GetMethod("ResetSession", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
    }
    [TearDown] public void TearDown()
    {
        Time.timeScale = 1f;
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }

    [Test] public void Chest_ThirdTransferIsRejected_AndReopenPreservesCount()
    {
        var chest = new ChestInventory(4);
        var item = Own(ScriptableObject.CreateInstance<WeaponDefinition>());
        chest.Set(0, item); chest.Set(1, item); chest.Set(2, item);
        using var source = new ChestContainerAdapter(chest);
        var target = new MemoryContainer();
        Assert.IsTrue(Transfer(source, 0, target, 0));
        Assert.IsTrue(Transfer(source, 1, target, 1));
        Assert.AreEqual(2, chest.AcquiredCount);
        int rejected = 0;
        chest.AcquisitionRejected += () => rejected++;
        using var reopened = new ChestContainerAdapter(chest);
        Assert.IsFalse(Transfer(reopened, 2, target, 2));
        Assert.AreEqual(1, rejected);
        Assert.AreSame(item, chest.Get(2));
        Assert.IsNull(target.Get(2));
        ChestInventory restored = JsonUtility.FromJson<ChestInventory>(JsonUtility.ToJson(chest));
        Assert.AreEqual(2, restored.AcquiredCount);
        Assert.IsFalse(restored.CanAcquire);
    }

    [Test] public void Chest_FailedTransferAndInternalSwapDoNotConsumeAllowance()
    {
        var chest = new ChestInventory(3);
        var item = Own(ScriptableObject.CreateInstance<WeaponDefinition>());
        chest.Set(0, item);
        using var source = new ChestContainerAdapter(chest);
        var target = new MemoryContainer { Reject = true };
        Assert.IsFalse(Transfer(source, 0, target, 0));
        Assert.AreEqual(0, chest.AcquiredCount);
        Assert.IsTrue(Transfer(source, 0, source, 1));
        Assert.AreEqual(0, chest.AcquiredCount);
        chest.RecordAcquisition(item);
        chest.Clear(); // Loot regeneration cannot reset the budget.
        Assert.AreEqual(1, chest.AcquiredCount);
    }

    [Test] public void Chest_ForeignRelicCannotBeDepositedEvenWhenChestContainsSameDefinition()
    {
        var chest = new ChestInventory(3);
        var relic = Own(ScriptableObject.CreateInstance<RelicDefinition>());
        relic.relicId = "deposit_test";
        chest.Set(0, relic);
        var other = Own(ScriptableObject.CreateInstance<WeaponDefinition>());
        chest.RecordAcquisition(other); chest.RecordAcquisition(other);
        using var target = new ChestContainerAdapter(chest);
        var source = new MemoryContainer(); source.TrySet(0, relic);
        Assert.IsFalse(Transfer(source, 0, target, 0));
        Assert.AreSame(relic, source.Get(0));
        Assert.IsNull(chest.Get(1));
        Assert.AreEqual(2, chest.AcquiredCount);
    }

    [Test] public void SealedWeaponSlot_AllowsArrangeAndRemove_ButNeverEquipsSealedSlot()
    {
        var player = Own(new GameObject("InventoryTest"));
        var inventory = player.AddComponent<WeaponInventory2D>();
        var first = Own(ScriptableObject.CreateInstance<WeaponDefinition>());
        var second = Own(ScriptableObject.CreateInstance<WeaponDefinition>());
        first.weaponId = "test_first"; second.weaponId = "test_second";
        Assert.IsTrue(inventory.TrySetWeaponSlot(0, first));
        Assert.IsTrue(inventory.TrySetWeaponSlot(1, second));
        inventory.Equip(0);
        using var seal = inventory.TryAcquireSlotSeal(1);
        Assert.NotNull(seal);
        Assert.IsTrue(inventory.TrySwapWeaponSlots(0, 1));
        Assert.AreSame(second, inventory.ActiveWeapon);
        Assert.AreEqual(0, inventory.ActiveIndex);
        inventory.Equip(1);
        Assert.AreEqual(0, inventory.ActiveIndex);
        Assert.IsTrue(inventory.TrySetWeaponSlot(1, null));
        Assert.IsNull(inventory.GetWeaponInSlot(1));
    }

    [Test] public void LoneFirstWeapon_CannotMoveToSecondSlot_WithOrWithoutSeal()
    {
        var actor = Own(new GameObject("LoneWeaponTest"));
        var inventory = actor.AddComponent<WeaponInventory2D>();
        var first = Own(ScriptableObject.CreateInstance<WeaponDefinition>());
        var second = Own(ScriptableObject.CreateInstance<WeaponDefinition>());
        first.weaponId = "lone_first"; second.weaponId = "lone_second";
        Assert.IsTrue(inventory.TrySetWeaponSlot(0, first));
        Assert.IsFalse(inventory.TrySwapWeaponSlots(0, 1));
        Assert.IsFalse(inventory.TrySwapWeaponSlots(1, 0));
        using var seal = inventory.TryAcquireSlotSeal(1);
        Assert.IsFalse(inventory.TrySwapWeaponSlots(0, 1));
        Assert.AreSame(first, inventory.ActiveWeapon);
        Assert.IsTrue(inventory.TrySetWeaponSlot(1, second));
        Assert.IsTrue(inventory.TrySwapWeaponSlots(0, 1));
        Assert.AreSame(second, inventory.GetWeaponInSlot(0));
    }

    [UnityTest] public IEnumerator HitPause_PatternHoldRestoresPrePauseSpeed_AndBossesAreImmune()
    {
        Assert.IsTrue(typeof(ICombatHitPauseImmune).IsAssignableFrom(typeof(BossControllerBase)));
        Assert.IsTrue(typeof(ICombatHitPauseImmune).IsAssignableFrom(typeof(Boss)));
        var actor = Own(new GameObject("PatternHoldOverlap"));
        var animator = actor.AddComponent<Animator>();
        animator.speed = 0.75f;
        CombatHitPause2D.Apply(actor, 0.05f);
        Assert.AreEqual(0f, animator.speed);
        float patternRestoreSpeed = CombatHitPause2D.GetUnpausedAnimatorSpeed(animator);
        Assert.AreEqual(0.75f, patternRestoreSpeed);
        yield return new WaitForSeconds(0.1f);
        animator.speed = patternRestoreSpeed;
        Assert.AreEqual(0.75f, animator.speed);
        actor.AddComponent<HitPauseImmuneTestActor>();
        CombatHitPause2D.Apply(actor, 0.3f);
        Assert.IsFalse(CombatHitPause2D.IsPausedOn(actor));
        Assert.AreEqual(0.75f, animator.speed);
    }

    [UnityTest] public IEnumerator MeleeControlLock_HeldInputCanTurnAfterMotion_AndLungeUsesRawInput()
    {
        var actor = Own(new GameObject("MeleeControlTest"));
        actor.AddComponent<AttributeSet>();
        actor.AddComponent<TagSystem>();
        actor.AddComponent<GameplayEffectRunner>();
        var abilities = actor.AddComponent<AbilitySystem>();
        var combat = actor.AddComponent<PlayerCombatInput2D>();
        var movement = actor.AddComponent<PlayerIntentInput2D>();
        var motion = actor.AddComponent<AbilityMotionController2D>();
        var input = new HitFeelTestInput { BackendComponent = actor.transform, Held = true };
        var backendField = typeof(InputActionQuery).GetField("backend", BindingFlags.Static | BindingFlags.NonPublic);
        var previous = (IInputActionQueryBackend)backendField.GetValue(null);
        InputActionQuery.RegisterBackend(input);
        try
        {
            Set(combat, "meleeControlLockActive", true);
            Set(combat, "meleeStartFrame", Time.frameCount - 2);
            Set(combat, "meleeFallbackRemaining", 0.05f);
            Set(combat, "meleeMotion", motion);
            motion.StartAttackLunge(Vector2.zero, Vector2.right, 1f, 0.2f);
            yield return new WaitForSeconds(0.1f);
            Assert.IsTrue(combat.IsMeleeControlLocked, "Active lunge still locks controls after the motion window.");
            Assert.AreEqual(Vector2.zero, movement.MoveInput);
            Assert.AreEqual(Vector2.right, AbilityMoveDirectionResolver2D.ResolveMoveThenAim(actor, Vector2.up),
                "Blocked walking must not change the authored move-then-aim lunge direction.");
            motion.CancelMotion();
            yield return null;
            Assert.IsFalse(combat.IsMeleeControlLocked, "Held primary input must not extend the motion lock.");
            yield return null;
            Assert.AreEqual(Vector2.right, movement.MoveInput);
            Set(combat, "meleeControlLockActive", true);
            actor.SetActive(false);
            Assert.IsFalse(combat.IsMeleeControlLocked);
        }
        finally { InputActionQuery.RegisterBackend(previous); }
    }

    [UnityTest] public IEnumerator WorldHitstop_FreezesOtherActors_ExpiresUnscaled_AndPreservesMenuPause()
    {
        var source = Own(new GameObject("HitstopSource"));
        var enemy = Own(new GameObject("HitstopEnemy"));
        enemy.AddComponent<HitPauseImmuneTestActor>();
        var body = enemy.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.right;
        var animator = enemy.AddComponent<Animator>();
        animator.speed = 0.7f;
        var menu = Own(new GameObject("PauseMenuOwner"));
        try
        {
            CombatHitPause2D.ApplyWorldPause(source, 0.08f);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(CombatHitPause2D.IsPausedOn(enemy), "Boss local-stun immunity must not bypass world hitstop.");
            Assert.AreEqual(0.7f, animator.speed, "World hitstop must not overwrite pattern animator speeds.");
            float frozenTime = Time.time;
            Vector2 frozenPosition = body.position;
            yield return new WaitForSecondsRealtime(0.03f);
            Assert.AreEqual(frozenTime, Time.time, 0.001f);
            Assert.AreEqual(frozenPosition, body.position);
            Assert.IsTrue(TimeScalePausePlayback.Acquire(menu));
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.IsFalse(TimeScalePausePlayback.IsHeldBy(source.GetComponent<CombatHitPause2D>()));
            Assert.IsTrue(TimeScalePausePlayback.IsHeldBy(menu));
            Assert.AreEqual(0f, Time.timeScale);
            TimeScalePausePlayback.Release(menu);
            Assert.AreEqual(1f, Time.timeScale);
            CombatHitPause2D.ApplyWorldPause(source, 0.3f);
            source.SetActive(false);
            Assert.AreEqual(1f, Time.timeScale, "Disabling the owner must release its hitstop token.");
        }
        finally
        {
            if (TimeScalePausePlayback.IsHeldBy(menu)) TimeScalePausePlayback.Release(menu);
            source.SetActive(false);
        }
    }

    [Test] public void Chest_ReturnRefundsOnce_AndAllowsExchangeAtTheLimit()
    {
        var chest = new ChestInventory(4);
        var first = Own(ScriptableObject.CreateInstance<WeaponDefinition>());
        var second = Own(ScriptableObject.CreateInstance<WeaponDefinition>());
        var third = Own(ScriptableObject.CreateInstance<WeaponDefinition>());
        chest.Set(0, first); chest.Set(1, second); chest.Set(2, third);
        using var loot = new ChestContainerAdapter(chest);
        var bag = new MemoryContainer();
        Assert.IsTrue(Transfer(loot, 0, bag, 0));
        Assert.IsTrue(Transfer(loot, 1, bag, 1));
        Assert.IsTrue(Transfer(bag, 0, loot, 0));
        Assert.AreEqual(1, chest.AcquiredCount);
        chest.RecordReturn(first);
        Assert.AreEqual(1, chest.AcquiredCount, "Returning the same receipt twice must not refund twice.");
        Assert.IsTrue(Transfer(loot, 2, bag, 2));
        Assert.AreEqual(2, chest.AcquiredCount);
        Assert.IsTrue(Transfer(bag, 1, loot, 0), "Returning one taken item should fund the simultaneous replacement.");
        Assert.AreSame(first, bag.Get(1));
        Assert.AreSame(second, chest.Get(0));
        Assert.AreEqual(2, chest.AcquiredCount);
        Assert.IsFalse(chest.CanReturnAcquisition(second));
        Assert.IsTrue(chest.CanReturnAcquisition(first));
    }

    [Test] public void Chest_FailedReturnKeepsReceipt_AndReceiptSurvivesRestore()
    {
        var chest = new ChestInventory(2);
        var item = Own(ScriptableObject.CreateInstance<WeaponDefinition>());
        chest.Set(0, item);
        using var loot = new ChestContainerAdapter(chest);
        var bag = new MemoryContainer();
        Assert.IsTrue(Transfer(loot, 0, bag, 0));
        bag.Reject = true;
        Assert.IsFalse(Transfer(bag, 0, loot, 0));
        Assert.AreEqual(1, chest.AcquiredCount);
        Assert.IsTrue(chest.CanReturnAcquisition(item));
        var restored = new ChestInventory(2);
        restored.RestoreAcquiredCount(chest.AcquiredCount, chest.OutstandingAcquisitions);
        restored.RecordReturn(item);
        Assert.AreEqual(0, restored.AcquiredCount);
        var jsonRestored = JsonUtility.FromJson<ChestInventory>(JsonUtility.ToJson(chest));
        Assert.IsTrue(jsonRestored.CanReturnAcquisition(item));
        restored.RestoreAcquiredCount(1); // Missing provenance must fail closed.
        restored.RecordReturn(item);
        Assert.AreEqual(1, restored.AcquiredCount);
        Assert.IsFalse(restored.CanReturnAcquisition(item));
    }

    [Test] public void Chest_AllReturnsRestoreRefreshEligibilityRegardlessOfSlotOrReopen()
    {
        var service = RunModifierService.Instance;
        if (service == null) service = Own(new GameObject("RefreshModifiersTest")).AddComponent<RunModifierService>();
        FieldInfo modifierField = typeof(RunModifierService).GetField("chestModifiers", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo loadedField = typeof(RunModifierService).GetField("hasLoadedFromSave", BindingFlags.Instance | BindingFlags.NonPublic);
        object oldModifiers = modifierField.GetValue(service);
        object oldLoaded = loadedField.GetValue(service);
        try
        {
            modifierField.SetValue(service, new ChestRunModifierDelta { chestRefreshCount = 1 });
            loadedField.SetValue(service, true);
            Type policy = typeof(ChestInventory).Assembly.GetType("ChestRewardPolicy", true);
            bool CanRefresh(ChestInventory chest, int used = 0) =>
                (bool)policy.GetMethod("CanRefreshLoot").Invoke(null, new object[] { true, chest, true, used });
            var chest = new ChestInventory(3);
            var item = Own(ScriptableObject.CreateInstance<WeaponDefinition>());
            chest.Set(0, item);
            using var loot = new ChestContainerAdapter(chest);
            var bag = new MemoryContainer();
            Assert.IsTrue(CanRefresh(chest));
            Assert.IsTrue(Transfer(loot, 0, bag, 0));
            Assert.IsFalse(CanRefresh(chest));
            var restored = JsonUtility.FromJson<ChestInventory>(JsonUtility.ToJson(chest));
            using var reopened = new ChestContainerAdapter(restored);
            Assert.IsFalse(CanRefresh(restored));
            Assert.IsTrue(Transfer(bag, 0, reopened, 2));
            Assert.IsTrue(CanRefresh(restored));
            Assert.IsFalse(CanRefresh(restored, 1), "Returning items must not refund the reroll budget.");
        }
        finally
        {
            modifierField.SetValue(service, oldModifiers);
            loadedField.SetValue(service, oldLoaded);
        }
    }

#if UNITY_EDITOR
    [TestCase("DarkLord_Tutorial", 0, "마왕을 토벌하기 위해 전진하자.")]
    [TestCase("ProtoTypeHub", 0, "마왕성 공략 준비를 마치고 위 쪽의 포탈로 이동하자.")]
    [TestCase("Grand Hall", 0, "셋 중 하나의 포탈을 선택해 이동하자.")]
    [TestCase("Grand Hall", 1, "둘 중 하나의 포탈을 선택해 이동하자.")]
    [TestCase("Grand Hall", 2, "마지막 포탈로 이동하자.")]
    [TestCase("Grand Hall", 3, "마왕을 토벌하기 위해 포탈로 이동하자.")]
    [TestCase("ProceduralDragonCorridor", 0, "포탈을 찾아 간부를 토벌하자.")]
    [TestCase("HeoMinSeok_Boss_Dragon", 0, "간부를 토벌하자.")]
    [TestCase("ProceduralDemonkingCorridor", 3, "마왕을 토벌하기 위해 포탈로 이동하자.")]
    [TestCase("LeeJunmo_Boss_DemonKing", 3, "마왕을 토벌하자.")]
    public void MainQuest_ProjectsSceneAndCurrentRunClears(string scene, int defeated, string expected)
    {
        var routes = UnityEditor.AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>("Assets/_Project/Data/SceneFlow/Routes/RunRouteCatalog.asset");
        Type view = Type.GetType("QuestHudView, UI", true);
        Assert.AreEqual(expected, view.GetMethod("ResolveMainQuestText").Invoke(null, new object[] { scene, defeated, routes }));
    }

    [Test] public void AuthoredSkillHitFeel_UsesRequestedChargeAndSpearTimings()
    {
        AbilityDefinition Load(string name) => UnityEditor.AssetDatabase.LoadAssetAtPath<AbilityDefinition>(
            "Assets/_Project/Data/Abilities/Definitions/" + name + ".asset");
        Assert.AreEqual(0.05f, Load("AD_LightningSpearSkill1").ResolveHitFeel(-1).attackerStopSeconds);
        Assert.AreEqual(0.05f, Load("AD_LightningSpearSkill2").ResolveHitFeel(-1).attackerStopSeconds);
        Assert.AreEqual(0.05f, Load("AD_ApprenticeHeroSwordSkill1_ChargeSpin").ResolveHitFeel(-1).attackerStopSeconds);
        Assert.AreEqual(0.3f, Load("AD_ApprenticeHeroSwordSkill1_ChargeSpin").ResolveHitFeel(0).attackerStopSeconds);
        Assert.AreEqual(0.1f, Load("AD_ApprenticeHeroSwordSkill2_DashStab").ResolveHitFeel(-1).attackerStopSeconds);
    }

    [TestCase(0f)]
    [TestCase(0.1f)]
    public void TrainingDummy_InstantHealStillReceivesHitPause(float attackerStop)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Monsters/TrainingDummy.prefab");
        var dummy = Own(Object.Instantiate(prefab, new Vector3(10000f, 10000f), Quaternion.identity));
        var source = Own(new GameObject("DummyAttackSource"));
        source.AddComponent<AttributeSet>();
        source.AddComponent<TagSystem>();
        source.AddComponent<GameplayEffectRunner>();
        var system = source.AddComponent<AbilitySystem>();
        var health = UnityEditor.AssetDatabase.LoadAssetAtPath<AttributeDefinition>(
            UnityEditor.AssetDatabase.GUIDToAssetPath("3ff045849daafe84d97370c69cd17747"));
        var damage = Own(ScriptableObject.CreateInstance<GE_Damage_Spec>());
        damage.healthAttribute = health;
        damage.fallbackDamage = 1f;
        damage.fallbackStunSeconds = 0f;
        damage.fallbackCameraShake = 0f;
        var definition = Own(ScriptableObject.CreateInstance<AbilityDefinition>());
        definition.hitFeel = new CombatHitFeelTiming { targetStunSeconds = 0.1f, attackerStopSeconds = attackerStop };
        float before = dummy.GetComponent<AttributeSet>().GetAttributeValue(health);
        CombatDamageAction.ApplyDamageAndEmitHit(system, new AbilitySpec(definition), damage, dummy, 1f, null, source);
        Assert.AreEqual(before, dummy.GetComponent<AttributeSet>().GetAttributeValue(health), "Dummy immediately restores its HP.");
        Assert.IsTrue(CombatHitPause2D.IsPausedOn(dummy), "Actual HP damage must be detected despite healing.");
        Assert.AreEqual(attackerStop > 0f, CombatHitPause2D.IsPausedOn(source));
    }
#endif

    [Test] public void AttackLunge_KeepsDistance_AndStopsWithoutDecelerationTail()
    {
        var actor = Own(new GameObject("AttackLungeTest"));
        var motion = actor.AddComponent<AbilityMotionController2D>();
        motion.StartAttackLunge(Vector2.zero, Vector2.right, 1f, 0.16f);
        Vector2 distance = Vector2.zero;
        for (int i = 0; i < 4; i++)
        {
            Vector2 velocity = motion.TickAndGetMotionVelocity(0.02f);
            Assert.AreEqual(12.5f, velocity.x, 0.001f);
            distance += velocity * 0.02f;
        }
        Assert.AreEqual(1f, distance.x, 0.001f);
        Assert.IsFalse(motion.HasActiveMotion);
        Assert.AreEqual(Vector2.zero, motion.TickAndGetMotionVelocity(0.02f));
        motion.StartLunge(Vector2.zero, Vector2.right, 1f, 0.16f);
        motion.TickAndGetMotionVelocity(0.08f);
        Assert.IsTrue(motion.HasActiveMotion, "Non-weapon travel keeps its authored timing.");
    }

    [UnityTest] public IEnumerator Heart_ReflectsFromWall_AndNeverLandsInside()
    {
        int layer = LayerMask.NameToLayer("Wall");
        Assert.GreaterOrEqual(layer, 0);
        var wall = Own(new GameObject("TestWall"));
        wall.layer = layer;
        wall.transform.position = new Vector3(1f, 0f);
        var wallCollider = wall.AddComponent<BoxCollider2D>();
        wallCollider.size = new Vector2(0.2f, 10f);
        var heart = Own(new GameObject("TestHeart"));
        var collider = heart.AddComponent<CircleCollider2D>();
        collider.radius = 0.1f;
        var visual = new GameObject("Visual"); visual.transform.SetParent(heart.transform);
        visual.AddComponent<SpriteRenderer>();
        var pickup = heart.AddComponent<FieldHealPickup2D>();
        pickup.PlayDrop(Vector3.zero, Vector3.right * 2f);
        float end = Time.time + 0.7f;
        while (Time.time < end)
        {
            Physics2D.SyncTransforms();
            Assert.IsFalse(collider.Distance(wallCollider).isOverlapped, $"Heart overlapped at {heart.transform.position}; distance={collider.Distance(wallCollider).distance}, bounds={collider.bounds}, wall={wallCollider.bounds}");
            yield return null;
        }
        Assert.Less(heart.transform.position.x, 0.75f);
        Assert.Less(heart.transform.position.x, 0f, "Remaining drop momentum should reflect to the left.");
    }

    [UnityTest] public IEnumerator HitPause_RestoresAnimator_AndDoesNotChangeGlobalTime()
    {
        var actor = Own(new GameObject("HitPauseTest"));
        var animator = actor.AddComponent<Animator>(); animator.speed = 0.7f;
        CombatHitPause2D.Apply(actor, 0.1f);
        Assert.IsTrue(CombatHitPause2D.IsPausedOn(actor));
        Assert.AreEqual(0f, animator.speed);
        Assert.AreEqual(1f, Time.timeScale);
        yield return new WaitForSeconds(0.16f);
        Assert.IsFalse(CombatHitPause2D.IsPausedOn(actor));
        Assert.AreEqual(0.7f, animator.speed, 0.001f);
        CombatHitPause2D.Apply(actor, 0.5f);
        actor.SetActive(false);
        Assert.AreEqual(0.7f, animator.speed, 0.001f);
    }

    [Test] public void HitFeel_UsesAttackOverrideAndDefault()
    {
        var ability = Own(ScriptableObject.CreateInstance<AbilityDefinition>());
        ability.hitFeel = new CombatHitFeelTiming { targetStunSeconds = 0.1f };
        ability.hitFeelByAttack = new[] { new CombatHitFeelTiming { targetStunSeconds = 0.2f } };
        Assert.AreEqual(0.2f, ability.ResolveHitFeel(0).targetStunSeconds);
        Assert.AreEqual(0.1f, ability.ResolveHitFeel(1).targetStunSeconds);
        Assert.AreEqual(0.1f, ability.ResolveHitFeel(-1).targetStunSeconds);
    }

    [UnityTest] public IEnumerator QuestRows_EnterExitAndReflow_AndCanCancelRemoval()
    {
        Type hudType = Type.GetType("QuestHudView, UI", true);
        Type rowType = Type.GetType("QuestHudRowView, UI", true);
        var canvas = Own(new GameObject("QuestCanvas", typeof(RectTransform), typeof(Canvas)));
        var root = new GameObject("QuestHud", typeof(RectTransform)); root.transform.SetParent(canvas.transform);
        root.SetActive(false);
        var rows = new GameObject("Rows", typeof(RectTransform)); rows.transform.SetParent(root.transform);
        var template = new GameObject("Template", typeof(RectTransform)); template.transform.SetParent(rows.transform);
        ((RectTransform)template.transform).sizeDelta = new Vector2(480f, 86f);
        Component row = template.AddComponent(rowType); template.SetActive(false);
        Component hud = root.AddComponent(hudType);
        Set(hud, "rowsRoot", rows.transform); Set(hud, "rowTemplate", row);
        root.SetActive(true);
        void Show(string id) => hudType.GetMethod("ShowQuest").Invoke(hud, new object[] { id, id, "description" });
        void Remove(string id) => hudType.GetMethod("RemoveQuest").Invoke(hud, new object[] { id });
        Show("a"); Show("b");
        yield return new WaitForSecondsRealtime(0.45f);
        Component[] entries = rows.GetComponentsInChildren(rowType, false);
        Assert.AreEqual(2, entries.Length);
        Assert.AreEqual(0f, ((RectTransform)entries[0].transform).anchoredPosition.x, 0.1f);
        Assert.Less(((RectTransform)entries[1].transform).anchoredPosition.y, -80f);
        Remove("a");
        yield return new WaitForSecondsRealtime(0.7f);
        entries = rows.GetComponentsInChildren(rowType, false);
        Assert.AreEqual(1, entries.Length);
        Assert.AreEqual(0f, ((RectTransform)entries[0].transform).anchoredPosition.y, 0.1f);
        Remove("b"); Show("b");
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.AreEqual(1, rows.GetComponentsInChildren(rowType, false).Length);
    }

    [TestCase(0f, 1)]
    [TestCase(0.6f, 1)]
    [TestCase(-0.6f, -1)]
    public void FloweringSlash_MirrorsXWithoutChangingHeight(float y, int sideSign)
    {
        var actor = Own(new GameObject("OffsetTest"));
        var system = actor.AddComponent<AbilitySystem>();
        var data = Own(ScriptableObject.CreateInstance<FloweringAttackData>());
        typeof(FloweringAttackData).GetField("sideOffset", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(data, 0.4f);
        typeof(FloweringAttackData).GetField("sideSign", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(data, sideSign);
        var resolve = typeof(AbilityLogic_FloweringAttack).GetMethod("ResolveHitboxCenter", BindingFlags.Static | BindingFlags.NonPublic);
        Vector2 right = new Vector2(0.8f, y).normalized;
        Vector2 left = new Vector2(-right.x, right.y);
        var a = (Vector2)resolve.Invoke(null, new object[] { system, data, right });
        var b = (Vector2)resolve.Invoke(null, new object[] { system, data, left });
        Assert.AreEqual(-a.x, b.x, 0.0001f);
        Assert.AreEqual(a.y, b.y, 0.0001f);
    }

    [UnityTest] public IEnumerator PlayerHit_StartsPointOneSecondWorldPause_AndReleasesIt()
    {
        var actor = Own(new GameObject("PlayerHitstopTest"));
        var feedback = actor.AddComponent<PlayerHitFeedback2D>();
        // Suppress pose/shake to isolate the damage impact from reaction immunity.
        var tags = actor.AddComponent<TagSystem>();
        var immune = Own(ScriptableObject.CreateInstance<GameplayTag>());
        tags.AddTag(immune);
        Set(feedback, "_tags", tags);
        Set(feedback, "hitReactImmuneTag", immune);
        feedback.OnHitFeedback(new HitFeedbackPayload(null, 0.3f, 0f));
        Assert.AreEqual(0f, Time.timeScale);
        var pause = actor.GetComponent<CombatHitPause2D>();
        float deadline = (float)typeof(CombatHitPause2D).GetField("worldPauseUntil", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pause);
        Assert.AreEqual(0.1f, deadline - Time.unscaledTime, 0.001f);
        yield return new WaitForSecondsRealtime(0.15f);
        Assert.IsFalse(TimeScalePausePlayback.IsHeldBy(pause));
        Assert.AreEqual(1f, Time.timeScale);
    }

    [Test] public void Telegraph_StraightAndCircleIgnoreColliders_WhileSectorAndRingRetainClipping()
    {
        Type viewType = Type.GetType("UnityGAS.AttackTelegraphView, Presentation", true);
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/VFX/Telegraphs/AttackTelegraphView.prefab");
        var actor = Own(Object.Instantiate(prefab));
        Component view = actor.GetComponent(viewType);
        var show = viewType.GetMethod("Show", new[] { typeof(AttackTelegraphSpec), typeof(AttackTelegraphStyle) });
        var update = viewType.GetMethod("UpdateGeometry");
        var clipping = viewType.GetField("activeUseWallClipping", BindingFlags.Instance | BindingFlags.NonPublic);
        var wall = Own(new GameObject("WarningWall", typeof(BoxCollider2D)));
        wall.transform.position = Vector3.right;
        Physics2D.SyncTransforms();
        foreach (var shape in new[] { AttackTelegraphShape.Line, AttackTelegraphShape.Rectangle, AttackTelegraphShape.Circle, AttackTelegraphShape.Sector, AttackTelegraphShape.Ring })
        {
            var spec = AttackTelegraphSpec.CreateLine(Vector3.zero, Vector3.right * 5f, 0.1f, 1f).WithWallClipping(1);
            spec.shape = shape;
            spec.size = new Vector2(5f, 3f);
            spec.sectorAngleDeg = 90f;
            spec.innerDiameter = 1f;
            show.Invoke(view, new object[] { spec, null });
            bool shouldClip = shape == AttackTelegraphShape.Sector || shape == AttackTelegraphShape.Ring;
            Assert.AreEqual(shouldClip, clipping.GetValue(view), shape.ToString());
            spec.lineEnd = Vector3.right * 6f;
            update.Invoke(view, new object[] { spec });
            Assert.AreEqual(shouldClip, clipping.GetValue(view), "Geometry update: " + shape);
            if (shape == AttackTelegraphShape.Line)
                Assert.AreEqual(spec.lineEnd, actor.GetComponentInChildren<LineRenderer>().GetPosition(1));
        }
    }

    [UnityTest] public IEnumerator CameraImpulseClock_ContinuesDuringWorldPause()
    {
        Type managerType = Type.GetType("Unity.Cinemachine.CinemachineImpulseManager, Unity.Cinemachine", true);
        object manager = managerType.GetProperty("Instance").GetValue(null);
        Type serviceType = Type.GetType("CameraShakeService, Presentation", true);
        serviceType.GetMethod("RegisterPlaybackBackend", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        var clock = managerType.GetProperty("CurrentTime");
        var actor = Own(new GameObject("CameraClockPause"));
        CombatHitPause2D.ApplyWorldPause(actor, 0.2f);
        float before = (float)clock.GetValue(manager);
        float scaled = Time.time;
        yield return new WaitForSecondsRealtime(0.04f);
        Assert.Greater((float)clock.GetValue(manager), before);
        Assert.AreEqual(scaled, Time.time);
        actor.SetActive(false);
    }

    [Test] public void BossGold_DropsConfiguredTotalOnlyDuringAnActiveRun()
    {
        var data = RunSessionStore.Data;
        Assert.IsNotNull(data);
        bool wasActive = data.isRunActive;
        var randomState = UnityEngine.Random.state;
        var prefabObject = Own(new GameObject("BossGoldFixture"));
        prefabObject.SetActive(false);
        var prefab = prefabObject.AddComponent<GoldPickup2D>();
        var director = Own(new GameObject("BossGoldDirector")).AddComponent<BossEncounterEndDirector>();
        Set(director, "goldPickupPrefab", prefab);
        Set(director, "baseGoldReward", 800);
        var drop = typeof(BossEncounterEndDirector).GetMethod("HandleGoldReward", BindingFlags.Instance | BindingFlags.NonPublic);
        var before = new HashSet<GoldPickup2D>(Object.FindObjectsByType<GoldPickup2D>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        try
        {
            data.isRunActive = false;
            drop.Invoke(director, new object[] { Vector3.zero });
            Assert.AreEqual(before.Count, Object.FindObjectsByType<GoldPickup2D>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            data.isRunActive = true;
            drop.Invoke(director, new object[] { Vector3.zero });
            int count = 0, total = 0;
            foreach (var pickup in Object.FindObjectsByType<GoldPickup2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (before.Contains(pickup)) continue;
                Own(pickup.gameObject);
                count++;
                total += pickup.GoldAmount;
            }
            Assert.AreEqual(8, count);
            Assert.That(total, Is.InRange(680, 920));
        }
        finally
        {
            data.isRunActive = wasActive;
            UnityEngine.Random.state = randomState;
        }
    }

#if UNITY_EDITOR
    [Test] public void AuthoredVisionMasks_StayOnOverlayLayer_AndDroppedSpearHasLocalScope()
    {
        int overlayLayer = SortingLayer.NameToID("MaskRender");
        foreach (string path in new[] {
            "Assets/_Project/Prefabs/Map/Gimmicks/Witch/PlayerVisionMask.prefab",
            "Assets/_Project/Prefabs/Monsters/ShadowCorridor/StrangeCandlestick/StrangeCandlestick.prefab" })
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);
            foreach (var mask in prefab.GetComponentsInChildren<SpriteMask>(true))
            {
                Assert.IsTrue(mask.isCustomRangeActive, path);
                Assert.AreEqual(overlayLayer, mask.frontSortingLayerID, path);
                Assert.AreEqual(overlayLayer, mask.backSortingLayerID, path);
            }
        }
        var spear = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/Items/Visuals/PF_LightningSpear_ItemDisplayVisual.prefab");
        Assert.IsNotNull(spear.GetComponent<UnityEngine.Rendering.SortingGroup>());
    }
#endif

    [Test] public void RunGold_SpendingAndRunBoundariesAreIndependentOfMagicStone()
    {
        Assert.IsNotNull(RunSessionStore.Data);
        Assert.IsNotNull(CurrencyManager.Instance);
        var data = RunSessionStore.Data;
        bool wasActive = data.isRunActive;
        int previousGold = data.runGold;
        try
        {
            data.isRunActive = true;
            data.runGold = 0;
            int stones = CurrencyManager.Instance.GetMagicStone();
            CurrencyManager.Instance.AddGold(1200);
            Assert.IsTrue(CurrencyManager.Instance.SpendGold(1000));
            Assert.AreEqual(200, CurrencyManager.Instance.GetGold());
            Assert.IsFalse(CurrencyManager.Instance.SpendGold(201));
            Assert.IsFalse(CurrencyManager.Instance.SpendGold(-1));
            CurrencyManager.Instance.AddGold(1000); // same refund path used after failed acquisition
            Assert.AreEqual(1200, CurrencyManager.Instance.GetGold());
            Assert.AreEqual(stones, CurrencyManager.Instance.GetMagicStone());
            data.isRunActive = false;
            Assert.AreEqual(0, CurrencyManager.Instance.GetGold());
            Assert.IsFalse(CurrencyManager.Instance.SpendGold(0));
        }
        finally { data.runGold = previousGold; data.isRunActive = wasActive; }

        var lifecycle = Type.GetType("RunSessionLifecycleService, Infrastructure", true);
        var isolated = new GamePlayData { runGold = 999 };
        lifecycle.GetMethod("StartRun").Invoke(null, new object[] { isolated, null });
        Assert.AreEqual(0, isolated.runGold);
        isolated.runGold = 999;
        isolated.isRunActive = false; // avoid the unrelated persistent-save flush in this isolated fixture
        lifecycle.GetMethod("EndRun").Invoke(null, new object[] { isolated, RunEndReason.None, null, null, null });
        Assert.AreEqual(0, isolated.runGold);
    }

    [Test] public void RunShop_PricesStayInRequestedRanges_AndSlotsAreUnrestricted()
    {
        var shop = UnityEditor.AssetDatabase.LoadAssetAtPath<ShopDefinitionSO>("Assets/_Project/Data/Dialogue/Merchant/ShopDefinition_RunGold.asset");
        Assert.IsTrue(shop.UsesRunGold);
        Assert.AreEqual(4, shop.BaseVisibleSlotCount);
        Assert.AreEqual(3, shop.MaxWeaponSlots);
        Assert.AreEqual(1, shop.MaxConsumableSlots);
        Assert.AreEqual(0, shop.StockRollWeights.consumableWeight);
        var weapon = Own(ScriptableObject.CreateInstance<WeaponDefinition>());
        var relic = Own(ScriptableObject.CreateInstance<RelicDefinition>());
        var potion = Own(ScriptableObject.CreateInstance<ConsumableDefinition>());
        for (int i = 0; i < 256; i++)
        {
            Assert.That(shop.RollGoldPrice(weapon), Is.InRange(1000, 1300));
            Assert.That(shop.RollGoldPrice(potion), Is.InRange(200, 300));
            relic.rarity = ItemRarity.Common;
            Assert.That(shop.RollGoldPrice(relic), Is.InRange(200, 400));
            relic.rarity = ItemRarity.Rare;
            Assert.That(shop.RollGoldPrice(relic), Is.InRange(500, 800));
            relic.rarity = ItemRarity.Epic;
            Assert.That(shop.RollGoldPrice(relic), Is.InRange(900, 1200));
        }
    }

    [Test] public void GoldPickup_CollectsAtDestinationWithoutCollider_OnlyOnce()
    {
        var data = RunSessionStore.Data;
        Assert.IsNotNull(data);
        Assert.IsNotNull(CurrencyManager.Instance);
        bool wasActive = data.isRunActive;
        int previousGold = data.runGold;
        try
        {
            data.isRunActive = true;
            data.runGold = 0;
            var destination = Own(new GameObject("PickupDestination"));
            var pickup = Own(new GameObject("GoldWithoutCollider")).AddComponent<GoldPickup2D>();
            pickup.Initialize(60);
            Set(pickup, "target", destination.transform);
            Set(pickup, "homingStartTime", float.PositiveInfinity);
            var update = typeof(GoldPickup2D).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            update.Invoke(pickup, null);
            Assert.AreEqual(0, data.runGold, "Arrival must respect the initial homing delay.");
            Set(pickup, "homingStartTime", float.NegativeInfinity);
            Set(pickup, "homingSpeed", 0f);
            pickup.transform.position = Vector3.right;
            update.Invoke(pickup, null);
            Assert.AreEqual(0, data.runGold, "Nearby pickups must first reach the destination.");
            pickup.transform.position = destination.transform.position;
            update.Invoke(pickup, null);
            update.Invoke(pickup, null);
            Assert.IsNull(pickup.GetComponent<Collider2D>());
            Assert.AreEqual(60, data.runGold);
        }
        finally { data.runGold = previousGold; data.isRunActive = wasActive; }
    }

    [Test] public void PlayerPopupColors_OverrideDamageStylesButPreserveOtherTargets()
    {
        var profileType = Type.GetType("DamagePopupFormatProfileSO, UI", true);
        var profile = Own(ScriptableObject.CreateInstance(profileType));
        Color PopupColor(DamagePopupRequest request)
        {
            object view = profileType.GetMethod("BuildViewModel").Invoke(profile, new object[] { request });
            return (Color)view.GetType().GetField("TextColor").GetValue(view);
        }
        Color red = new Color32(255, 77, 77, 255);
        Color sky = new Color32(50, 156, 199, 255);
        Assert.AreEqual(red, PopupColor(DamagePopupRequest.Damage(10, Vector3.zero, isPlayerTarget: true)));
        Assert.AreEqual(red, PopupColor(DamagePopupRequest.Damage(10, Vector3.zero, true, true)));
        Assert.AreEqual(red, PopupColor(DamagePopupRequest.Element(10, Vector3.zero, null, true)));
        Assert.AreEqual(sky, PopupColor(DamagePopupRequest.Text("EVADE", Vector3.zero, true)));
        Assert.AreEqual(Color.white, PopupColor(DamagePopupRequest.Damage(10, Vector3.zero)));
        Assert.AreEqual(Color.white, PopupColor(DamagePopupRequest.Text("EVADE", Vector3.zero)));
    }

    [Test] public void OfficerQuest_CompletionWaitsForGrandHall_AndResetsWithRun()
    {
        var data = new GamePlayData { isRunActive = true };
        Assert.IsFalse(RunOfficerQuestProgress.IsVisible(data));
        RunOfficerQuestProgress.EnterScene(data, "Grand Hall");
        Assert.IsTrue(RunOfficerQuestProgress.IsVisible(data));
        foreach (string boss in RunOfficerQuestProgress.BossIds)
        {
            data.defeatedBossIds.Add(boss);
            Assert.IsTrue(RunOfficerQuestProgress.IsVisible(data));
        }
        data.defeatedBossIds.Add("slime");
        data.defeatedBossIds.Add("demonking");
        Assert.AreEqual(3, RunOfficerQuestProgress.CountDefeated(data));
        RunOfficerQuestProgress.CompletePresentation(data, "BossRoom");
        Assert.IsTrue(RunOfficerQuestProgress.IsVisible(data));
        Assert.IsFalse(RunOfficerQuestProgress.CanPresentCompletion(data, "BossRoom"));
        Assert.IsTrue(RunOfficerQuestProgress.CanPresentCompletion(data, "Grand Hall"));
        RunOfficerQuestProgress.CompletePresentation(data, "Grand Hall");
        var restored = JsonUtility.FromJson<GamePlayData>(JsonUtility.ToJson(data));
        RunOfficerQuestProgress.EnterScene(restored, "Grand Hall");
        Assert.IsFalse(RunOfficerQuestProgress.IsVisible(restored));
        Type.GetType("RunSessionLifecycleService, Infrastructure", true).GetMethod("StartRun")
            .Invoke(null, new object[] { restored, null });
        Assert.IsFalse(restored.officerQuestStarted);
        Assert.IsFalse(restored.officerQuestCompletionPresented);
        Assert.AreEqual(0, RunOfficerQuestProgress.CountDefeated(restored));
    }

    private sealed class PortalWarningBackend : IRunRouteBackend
    {
        public WarningPopupCode Warning;
        public ScenePortal QueriedPortal;
        public bool HasActivePlan => false;
        public int CurrentStageIndex => 0;
        public int TotalStageCount => 0;
        public RunRouteCatalogSO ActiveRouteCatalog => null;
        public CorridorBossRouteSetSO CurrentStageSet => null;
        public bool EnsurePendingPlan(ScenePortal portal) => true;
        public bool CanResolveRoute(ScenePortal portal) => true;
        public WarningPopupCode GetTravelBlockWarning(ScenePortal portal)
        {
            QueriedPortal = portal;
            return Warning;
        }
#if UNITY_EDITOR
        public string GetDebugResolveStatus(ScenePortal portal) => "Portal warning fixture";
#endif
    }

    [UnityTest] public IEnumerator ClearedPortal_UpdateUsesInteractionGateWithFinalOnlyCatalog()
    {
        var data = RunSessionStore.Data;
        Assert.IsNotNull(data);
        bool wasActive = data.isRunActive;
        var previousDefeats = data.defeatedBossIds;
        var previousBackend = RunRoutePlayback.Backend;
        var scene = UnityEngine.SceneManagement.SceneManager.CreateScene("Grand Hall");
        var root = Own(new GameObject("ClearedDragonPortal"));
        root.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var renderer = root.AddComponent<SpriteRenderer>();
        var texture = Own(new Texture2D(2, 2));
        var active = Own(Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero));
        var disabled = Own(Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero));
        renderer.sprite = active;
        var portal = root.AddComponent<ScenePortal>();
        var catalog = Own(ScriptableObject.CreateInstance<RunRouteCatalogSO>());
        var dragon = Own(ScriptableObject.CreateInstance<CorridorBossRouteSetSO>());
        typeof(CorridorBossRouteSetSO).GetField("themeId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(dragon, "dragon");
        typeof(RunRouteCatalogSO).GetField("normalRouteSets", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(catalog, new List<CorridorBossRouteSetSO>());
        typeof(RunRouteCatalogSO).GetField("finalRouteSet", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(catalog, dragon);
        Set(portal, "startRunRouteCatalog", catalog);
        var particles = Own(new GameObject("PortalParticles"));
        particles.transform.SetParent(root.transform);
        var requirement = root.AddComponent<RequiredBossClearScenePortalAccessRule>();
        Set(requirement, "requiredBossThemeIds", new List<string> { "slime", "dragon", "shadow" });
        requirement.enabled = false;
        var view = root.AddComponent<GrandHallClearedPortalView>();
        Set(view, "portal", portal);
        Set(view, "portalSprite", renderer);
        Set(view, "disabledSprite", disabled);
        Set(view, "particleRoot", particles);
        var backend = new PortalWarningBackend();
        var update = typeof(GrandHallClearedPortalView).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            data.isRunActive = true;
            RunRoutePlayback.RegisterBackend(backend);
            root.SetActive(true);
            update.Invoke(view, null);
            Assert.AreSame(active, renderer.sprite);
            Assert.IsTrue(particles.activeSelf);
            backend.Warning = WarningPopupCode.BossAlreadyDefeatedThisRun;
            update.Invoke(view, null);
            Assert.AreSame(portal, backend.QueriedPortal);
            Assert.AreSame(disabled, renderer.sprite);
            Assert.IsFalse(particles.activeSelf);
            backend.Warning = WarningPopupCode.None;
            update.Invoke(view, null);
            Assert.AreSame(active, renderer.sprite);
            Assert.IsTrue(particles.activeSelf);
            requirement.enabled = true;
            data.defeatedBossIds = new List<string> { "slime", "dragon" };
            update.Invoke(view, null);
            Assert.IsFalse(requirement.CanAccess(portal, null));
            Assert.AreSame(disabled, renderer.sprite, "Two officers cannot unlock the final portal.");
            Assert.IsFalse(particles.activeSelf);
            data.defeatedBossIds.Add("shadow");
            update.Invoke(view, null);
            Assert.IsTrue(requirement.CanAccess(portal, null));
            Assert.AreSame(active, renderer.sprite);
            Assert.IsTrue(particles.activeSelf);
            backend.Warning = WarningPopupCode.BossAlreadyDefeatedThisRun;
            update.Invoke(view, null);
            Assert.AreSame(disabled, renderer.sprite, "Defeated final boss still blocks a qualified portal.");
            backend.Warning = WarningPopupCode.None;
            data.defeatedBossIds.Clear();
            update.Invoke(view, null);
            Assert.IsFalse(requirement.AreRequirementsMet);
            Assert.AreSame(disabled, renderer.sprite, "New-run clear history must relock the portal.");
            requirement.enabled = false;
            data.isRunActive = false;
            backend.Warning = WarningPopupCode.BossAlreadyDefeatedThisRun;
            update.Invoke(view, null);
            Assert.AreSame(active, renderer.sprite);
        }
        finally
        {
            root.SetActive(false);
            RunRoutePlayback.RegisterBackend(previousBackend);
            data.isRunActive = wasActive;
            data.defeatedBossIds = previousDefeats;
            Object.DestroyImmediate(root);
        }
        yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
    }

    [Test] public void ClearedPortal_ChangesSpriteClearsParticles_AndRestores()
    {
        var root = Own(new GameObject("PortalViewTest"));
        root.SetActive(false);
        var renderer = root.AddComponent<SpriteRenderer>();
        var texture = Own(new Texture2D(2, 2));
        var active = Own(Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero));
        var disabled = Own(Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero));
        renderer.sprite = active;
        var particles = Own(new GameObject("Particles"));
        particles.transform.SetParent(root.transform);
        var system = particles.AddComponent<ParticleSystem>();
        var view = root.AddComponent<GrandHallClearedPortalView>();
        Set(view, "portalSprite", renderer);
        Set(view, "disabledSprite", disabled);
        Set(view, "particleRoot", particles);
        root.SetActive(true);
        system.Emit(3);
        view.ApplyCleared(true);
        Assert.AreSame(disabled, renderer.sprite);
        Assert.IsFalse(particles.activeSelf);
        Assert.AreEqual(0, system.particleCount);
        view.ApplyCleared(false);
        Assert.AreSame(active, renderer.sprite);
        Assert.IsTrue(particles.activeSelf);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void WeaponSkill_HitPreservesCastingAndExecution_ButForcedCancelStillWorks(bool casting)
    {
        var actor = Own(new GameObject("SkillHitProtection"));
        var system = actor.AddComponent<AbilitySystem>();
        var feedback = actor.AddComponent<PlayerHitFeedback2D>();
        var skillTag = Own(ScriptableObject.CreateInstance<GameplayTag>());
        var ability = Own(ScriptableObject.CreateInstance<AbilityDefinition>());
        ability.grantedTagsWhileActive.Add(skillTag);
        var spec = new AbilitySpec(ability);
        var token = new AbilityCancellationToken();
        typeof(AbilitySpec).GetProperty("Token").SetValue(spec, token);
        Set(system, casting ? "currentCastSpec" : "currentExecSpec", spec);
        Set(system, casting ? "isCasting" : "isExecuting", true);
        Set(feedback, "_abilitySystem", system);
        Set(feedback, "activeSkillTag", skillTag);
        Set(feedback, "defaultShake", 0f);
        feedback.OnHitFeedback(new HitFeedbackPayload(null, 0f, 0f));
        Assert.IsFalse(token.IsCancelled);
        Assert.IsNull(typeof(PlayerHitFeedback2D).GetField("_reactionRoutine", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(feedback));
        if (casting) system.CancelCasting(force: true);
        else system.CancelExecution(force: true);
        Assert.IsTrue(token.IsCancelled);
    }

    private static void Set(Component component, string name, object value) =>
        component.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, value);
    private static bool Transfer(IItemContainer source, int from, IItemContainer target, int to)
    {
        Type requestType = Type.GetType("InventoryTransferRequest, UI", true);
        object request = Activator.CreateInstance(requestType, source, from, target, to, 0);
        object result = Type.GetType("InventoryTransferService, UI", true).GetMethod("TryTransfer").Invoke(null, new[] { request });
        return (bool)result.GetType().GetProperty("Succeeded").GetValue(result);
    }
    private sealed class MemoryContainer : IItemContainer
    {
        private readonly ScriptableObject[] items = new ScriptableObject[4];
        public bool Reject;
        public int SlotCount => items.Length;
        public event Action OnChanged;
        public ScriptableObject Get(int index) => items[index];
        public bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1) => !Reject;
        public bool TrySet(int index, ScriptableObject item) { if (Reject) return false; items[index] = item; OnChanged?.Invoke(); return true; }
        public bool TrySwap(int a, int b) { (items[a], items[b]) = (items[b], items[a]); return true; }
    }
}

public sealed class HitPauseImmuneTestActor : MonoBehaviour, ICombatHitPauseImmune { }

public sealed class HitFeelTestInput : IInputActionQueryBackend
{
    public Component BackendComponent { get; set; }
    public bool Held;
    public bool WasPressedThisFrame(InputActionId action) => false;
    public bool WasReleasedThisFrame(InputActionId action) => false;
    public bool IsPressed(InputActionId action) => action == InputActionId.PrimaryAttack && Held;
    public bool IsKeyPressed(KeyCode key) => false;
    public bool WasKeyPressedThisFrame(KeyCode key) => false;
    public bool WasKeyReleasedThisFrame(KeyCode key) => false;
    public Vector2 GetMoveVectorRaw() => Vector2.right;
    public Vector2 GetMoveVectorNormalized() => Vector2.right;
    public Vector3 GetPointerWorldPosition(Camera camera, float z = 0f) => Vector3.right;
}
