using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

[Category("Diagnostic")]
public sealed class RunStartInventoryDiagnosticsPlayModeTests
{
    private const string HubSceneName = "ProtoTypeHub";
    private const string FallbackWeaponId = "Weapon.New1";
    private const string FallbackRelicId = "AttackBonusRelic";
    private const int RepetitionCount = 30;
    private const int SettleFrameCount = 5;
    private const int WaitFrameLimit = 240;

    [UnityTest]
    public IEnumerator HubRunStart_PreservesInventory_AcrossRepeatedTransitions()
    {
        for (int iteration = 1; iteration <= RepetitionCount; iteration++)
        {
            yield return ResetRuntimeAndLoadHub();

            MonoBehaviour player = null;
            yield return WaitForCondition(
                () => TryGetCurrentPlayer(out player),
                $"Iteration {iteration}: player was not found in {HubSceneName}.");

            Assert.That(player, Is.Not.Null, $"Iteration {iteration}: player reference is null after loading {HubSceneName}.");

            MonoBehaviour captureBridge = GetComponentByTypeName(player.gameObject, "PlayerRuntimeCaptureBridge");
            Assert.That(captureBridge, Is.Not.Null, $"Iteration {iteration}: PlayerRuntimeCaptureBridge is missing on the player.");

            MonoBehaviour weaponInventory = GetComponentByTypeName(player.gameObject, "WeaponInventory2D");
            MonoBehaviour relicInventory = GetComponentByTypeName(player.gameObject, "RelicInventory");
            MonoBehaviour consumableInventory = GetComponentByTypeName(player.gameObject, "PlayerConsumableInventory");

            Assert.That(weaponInventory, Is.Not.Null, $"Iteration {iteration}: WeaponInventory2D is missing on the player.");
            Assert.That(relicInventory, Is.Not.Null, $"Iteration {iteration}: RelicInventory is missing on the player.");
            Assert.That(consumableInventory, Is.Not.Null, $"Iteration {iteration}: PlayerConsumableInventory is missing on the player.");

            SeedResult seed = SeedPlayerInventory(weaponInventory, relicInventory, consumableInventory);
            InventorySnapshot beforeTravel = CaptureSnapshot(weaponInventory, relicInventory, consumableInventory);

            Assert.That(
                beforeTravel.HasAnyItem,
                Is.True,
                $"Iteration {iteration}: seeded inventory is still empty. Seed={seed.ToSummary()}");

            MonoBehaviour runStartPortal = FindRunStartPortal();
            Assert.That(runStartPortal, Is.Not.Null, $"Iteration {iteration}: could not find a HubToRunStart ScenePortal in {HubSceneName}.");

            bool travelStarted = InvokeStaticBool("ScenePortalTravelService", "TryTravel", runStartPortal);
            Assert.That(travelStarted, Is.True, $"Iteration {iteration}: ScenePortalTravelService.TryTravel returned false.");

            MonoBehaviour gameplayManager = FindBehaviourByTypeName("GamePlayDataManager");
            object pendingState = gameplayManager != null
                ? InvokeInstance(gameplayManager, "PeekPendingPlayerState")
                : null;

            InventorySnapshot pendingSnapshot = InventorySnapshot.FromRuntimeState(pendingState);

            string loadedRunScene = null;
            yield return WaitForCondition(
                () =>
                {
                    loadedRunScene = SceneManager.GetActiveScene().name;
                    return !string.IsNullOrEmpty(loadedRunScene) && loadedRunScene != HubSceneName;
                },
                $"Iteration {iteration}: active scene never changed away from {HubSceneName} after RunStart travel.");

            MonoBehaviour restoreBootstrapper = null;
            yield return WaitForCondition(
                () =>
                {
                    restoreBootstrapper = FindBehaviourByTypeName("PlayerSceneRestoreBootstrapper");
                    return restoreBootstrapper != null;
                },
                $"Iteration {iteration}: PlayerSceneRestoreBootstrapper was not found in run scene '{loadedRunScene}'.");

            MonoBehaviour restoredPlayer = null;
            yield return WaitForCondition(
                () => TryGetCurrentPlayer(out restoredPlayer),
                $"Iteration {iteration}: restored player was not found in run scene '{loadedRunScene}'.");

            Assert.That(restoredPlayer, Is.Not.Null, $"Iteration {iteration}: restored player reference is null in run scene '{loadedRunScene}'.");

            MonoBehaviour restoredWeaponInventory = GetComponentByTypeName(restoredPlayer.gameObject, "WeaponInventory2D");
            MonoBehaviour restoredRelicInventory = GetComponentByTypeName(restoredPlayer.gameObject, "RelicInventory");
            MonoBehaviour restoredConsumableInventory = GetComponentByTypeName(restoredPlayer.gameObject, "PlayerConsumableInventory");

            Assert.That(restoredWeaponInventory, Is.Not.Null, $"Iteration {iteration}: restored player is missing WeaponInventory2D.");
            Assert.That(restoredRelicInventory, Is.Not.Null, $"Iteration {iteration}: restored player is missing RelicInventory.");
            Assert.That(restoredConsumableInventory, Is.Not.Null, $"Iteration {iteration}: restored player is missing PlayerConsumableInventory.");

            InventorySnapshot afterTravel = CaptureSnapshot(
                restoredWeaponInventory,
                restoredRelicInventory,
                restoredConsumableInventory);

            if (!beforeTravel.EqualsTo(afterTravel))
            {
                bool captureMatchesSeed = beforeTravel.EqualsTo(pendingSnapshot);
                string inference = captureMatchesSeed
                    ? "pendingPlayerState는 정상적으로 저장됐지만, 새 씬에서 복원되거나 스폰 직후 유지되는 과정에서 인벤토리가 달라졌습니다. restore/bootstrapper/spawn policy 쪽을 먼저 보세요."
                    : "ScenePortalTravelService가 저장한 pendingPlayerState 자체가 이미 시드 상태와 달랐습니다. capture bridge 또는 travel 직전 캡처 경로를 먼저 보세요.";

                Assert.Fail(BuildFailureMessage(
                    iteration,
                    loadedRunScene,
                    inference,
                    beforeTravel,
                    pendingSnapshot,
                    afterTravel,
                    seed,
                    restoreBootstrapper != null));
            }

            if (gameplayManager != null)
                InvokeEndRunNone(gameplayManager);
        }
    }

    private static IEnumerator ResetRuntimeAndLoadHub()
    {
        MonoBehaviour gameplayManager = FindBehaviourByTypeName("GamePlayDataManager");
        if (gameplayManager != null)
        {
            InvokeEndRunNone(gameplayManager);
            InvokeInstance(gameplayManager, "ClearPendingPlayerState");
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(HubSceneName, LoadSceneMode.Single);
        Assert.That(operation, Is.Not.Null, $"Could not start loading scene '{HubSceneName}'.");

        while (!operation.isDone)
            yield return null;

        for (int i = 0; i < SettleFrameCount; i++)
            yield return null;
    }

    private static IEnumerator WaitForCondition(Func<bool> predicate, string failureMessage)
    {
        for (int frame = 0; frame < WaitFrameLimit; frame++)
        {
            if (predicate())
                yield break;

            yield return null;
        }

        Assert.Fail(failureMessage);
    }

    private static bool TryGetCurrentPlayer(out MonoBehaviour player)
    {
        player = FindBehaviourByTypeName("PlayerInteractor2D");
        return player != null;
    }

    private static SeedResult SeedPlayerInventory(
        MonoBehaviour weaponInventory,
        MonoBehaviour relicInventory,
        MonoBehaviour consumableInventory)
    {
        ClearWeaponInventory(weaponInventory);
        ClearRelicInventory(relicInventory);
        ClearConsumableInventory(consumableInventory);

        MonoBehaviour itemManager = FindBehaviourByTypeName("ItemManager");
        Assert.That(itemManager, Is.Not.Null, "ItemManager.Instance is required for diagnostic inventory seeding.");

        object weapon = InvokeInstance(itemManager, "GetWeaponData", FallbackWeaponId);
        object relic = InvokeInstance(itemManager, "GetRelicData", FallbackRelicId);
        object consumable = null;

        object consumables = InvokeInstance(itemManager, "GetAllConsumables");
        if (consumables is IList list && list.Count > 0)
            consumable = list[0];

        Assert.That(weapon, Is.Not.Null, $"Could not resolve weapon definition '{FallbackWeaponId}' from ItemManager.");
        Assert.That(relic, Is.Not.Null, $"Could not resolve relic definition '{FallbackRelicId}' from ItemManager.");
        Assert.That(consumable, Is.Not.Null, "Could not resolve any consumable definition from ItemManager.");

        bool weaponSeeded = InvokeBool(weaponInventory, "TrySetWeaponSlot", 0, weapon, false);
        bool relicSeeded = InvokeBool(relicInventory, "TrySetRelicSlot", 0, relic);
        bool consumableSeeded = InvokeBool(consumableInventory, "TrySetConsumableSlot", 0, consumable);

        Assert.That(weaponSeeded, Is.True, $"Failed to seed weapon slot 0 with '{GetStringMember(weapon, "weaponId")}'.");
        Assert.That(relicSeeded, Is.True, $"Failed to seed relic slot 0 with '{GetStringMember(relic, "relicId")}'.");
        Assert.That(consumableSeeded, Is.True, $"Failed to seed consumable slot 0 with '{GetStringMember(consumable, "consumableId")}'.");

        return new SeedResult(
            GetStringMember(weapon, "weaponId"),
            GetStringMember(relic, "relicId"),
            GetStringMember(consumable, "consumableId"));
    }

    private static void ClearWeaponInventory(MonoBehaviour weaponInventory)
    {
        if (weaponInventory == null)
            return;

        int slotCount = GetIntMember(weaponInventory, "SlotCount");
        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            InvokeBool(weaponInventory, "TrySetWeaponSlot", slotIndex, null, false);

        InvokeInstance(weaponInventory, "Unequip");
    }

    private static void ClearRelicInventory(MonoBehaviour relicInventory)
    {
        if (relicInventory == null)
            return;

        int capacity = GetIntMember(relicInventory, "Capacity");
        for (int slotIndex = 0; slotIndex < capacity; slotIndex++)
            InvokeBool(relicInventory, "TrySetRelicSlot", slotIndex, null);
    }

    private static void ClearConsumableInventory(MonoBehaviour consumableInventory)
    {
        if (consumableInventory == null)
            return;

        int slotCount = GetIntMember(consumableInventory, "SlotCount");
        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            InvokeBool(consumableInventory, "TrySetConsumableSlot", slotIndex, null);
    }

    private static InventorySnapshot CaptureSnapshot(
        MonoBehaviour weaponInventory,
        MonoBehaviour relicInventory,
        MonoBehaviour consumableInventory)
    {
        var snapshot = new InventorySnapshot();
        snapshot.ActiveWeaponSlotIndex = GetIntMember(weaponInventory, "ActiveIndex");
        snapshot.WeaponSlotIds = CaptureWeaponSlots(weaponInventory);
        snapshot.RelicSlots = CaptureRelicSlots(relicInventory);
        snapshot.ConsumableSlotIds = CaptureConsumableSlots(consumableInventory);
        return snapshot;
    }

    private static string[] CaptureWeaponSlots(MonoBehaviour weaponInventory)
    {
        if (weaponInventory == null)
            return new string[0];

        int slotCount = GetIntMember(weaponInventory, "SlotCount");
        var slots = new string[slotCount];

        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            object weapon = InvokeInstance(weaponInventory, "GetWeaponInSlot", slotIndex);
            slots[slotIndex] = weapon != null ? GetStringMember(weapon, "weaponId") : null;
        }

        return slots;
    }

    private static RelicSlotSnapshot[] CaptureRelicSlots(MonoBehaviour relicInventory)
    {
        if (relicInventory == null)
            return new RelicSlotSnapshot[0];

        int capacity = GetIntMember(relicInventory, "Capacity");
        var slots = new RelicSlotSnapshot[capacity];

        for (int slotIndex = 0; slotIndex < capacity; slotIndex++)
        {
            object relic = InvokeInstance(relicInventory, "GetRelicInSlot", slotIndex);
            int level = Convert.ToInt32(InvokeInstance(relicInventory, "GetRelicLevelInSlot", slotIndex));
            slots[slotIndex] = new RelicSlotSnapshot(
                relic != null ? GetStringMember(relic, "relicId") : null,
                level);
        }

        return slots;
    }

    private static string[] CaptureConsumableSlots(MonoBehaviour consumableInventory)
    {
        if (consumableInventory == null)
            return new string[0];

        int slotCount = GetIntMember(consumableInventory, "SlotCount");
        var slots = new string[slotCount];

        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            object consumable = InvokeInstance(consumableInventory, "GetConsumableInSlot", slotIndex);
            slots[slotIndex] = consumable != null ? GetStringMember(consumable, "consumableId") : null;
        }

        return slots;
    }

    private static MonoBehaviour FindRunStartPortal()
    {
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour.GetType().Name != "ScenePortal")
                continue;

            object transition = GetMemberValue(behaviour, "PortalTransitionType");
            if (transition != null && string.Equals(transition.ToString(), "HubToRunStart", StringComparison.Ordinal))
                return behaviour;
        }

        return null;
    }

    private static string BuildFailureMessage(
        int iteration,
        string loadedRunScene,
        string inference,
        InventorySnapshot beforeTravel,
        InventorySnapshot pendingSnapshot,
        InventorySnapshot afterTravel,
        SeedResult seed,
        bool hasRestoreBootstrapper)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Iteration {iteration}: inventory changed after Hub -> RunStart transition.");
        builder.AppendLine($"Run scene: {loadedRunScene}");
        builder.AppendLine($"Restore bootstrapper found: {hasRestoreBootstrapper}");
        builder.AppendLine($"Seed: {seed.ToSummary()}");
        builder.AppendLine($"Inference: {inference}");
        builder.AppendLine();
        builder.AppendLine("Before travel:");
        builder.AppendLine(beforeTravel.ToMultilineString());
        builder.AppendLine();
        builder.AppendLine("Stored pendingPlayerState:");
        builder.AppendLine(pendingSnapshot.ToMultilineString());
        builder.AppendLine();
        builder.AppendLine("After travel:");
        builder.AppendLine(afterTravel.ToMultilineString());
        return builder.ToString();
    }

    private static void InvokeEndRunNone(MonoBehaviour gameplayManager)
    {
        MethodInfo method = gameplayManager.GetType().GetMethod("EndRun", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(method, Is.Not.Null, "GamePlayDataManager.EndRun method was not found.");

        ParameterInfo[] parameters = method.GetParameters();
        Assert.That(parameters.Length, Is.EqualTo(1), "GamePlayDataManager.EndRun signature was not expected.");

        object noneValue = Enum.Parse(parameters[0].ParameterType, "None");
        method.Invoke(gameplayManager, new[] { noneValue });
    }

    private static bool InvokeStaticBool(string typeName, string methodName, params object[] args)
    {
        Type type = FindTypeByName(typeName);
        Assert.That(type, Is.Not.Null, $"Type '{typeName}' was not found.");

        MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, $"Static method '{typeName}.{methodName}' was not found.");

        object result = method.Invoke(null, args);
        return result is bool flag && flag;
    }

    private static object InvokeInstance(object target, string methodName, params object[] args)
    {
        Assert.That(target, Is.Not.Null, $"Cannot invoke '{methodName}' on a null target.");

        MethodInfo method = FindCompatibleMethod(target.GetType(), methodName, args);
        Assert.That(method, Is.Not.Null, $"Method '{target.GetType().Name}.{methodName}' was not found.");
        return method.Invoke(target, args);
    }

    private static bool InvokeBool(object target, string methodName, params object[] args)
    {
        object result = InvokeInstance(target, methodName, args);
        return result is bool flag && flag;
    }

    private static MethodInfo FindCompatibleMethod(Type type, string methodName, object[] args)
    {
        MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo candidate = methods[i];
            if (!string.Equals(candidate.Name, methodName, StringComparison.Ordinal))
                continue;

            ParameterInfo[] parameters = candidate.GetParameters();
            if (parameters.Length != args.Length)
                continue;

            bool isMatch = true;
            for (int j = 0; j < parameters.Length; j++)
            {
                object arg = args[j];
                Type parameterType = parameters[j].ParameterType;

                if (arg == null)
                {
                    if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) == null)
                    {
                        isMatch = false;
                        break;
                    }

                    continue;
                }

                if (!parameterType.IsInstanceOfType(arg))
                {
                    isMatch = false;
                    break;
                }
            }

            if (isMatch)
                return candidate;
        }

        return null;
    }

    private static MonoBehaviour FindBehaviourByTypeName(string typeName)
    {
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour != null && behaviour.GetType().Name == typeName)
                return behaviour;
        }

        return null;
    }

    private static MonoBehaviour GetComponentByTypeName(GameObject target, string typeName)
    {
        if (target == null)
            return null;

        MonoBehaviour[] behaviours = target.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour != null && behaviour.GetType().Name == typeName)
                return behaviour;
        }

        return null;
    }

    private static Type FindTypeByName(string typeName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type[] types;
            try
            {
                types = assemblies[i].GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }

            if (types == null)
                continue;

            for (int j = 0; j < types.Length; j++)
            {
                Type type = types[j];
                if (type != null && type.Name == typeName)
                    return type;
            }
        }

        return null;
    }

    private static object GetMemberValue(object target, string memberName)
    {
        if (target == null)
            return null;

        Type type = target.GetType();

        PropertyInfo property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        if (property != null)
            return property.GetValue(target);

        FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        if (field != null)
            return field.GetValue(target);

        return null;
    }

    private static int GetIntMember(object target, string memberName)
    {
        object value = GetMemberValue(target, memberName);
        return value != null ? Convert.ToInt32(value) : 0;
    }

    private static string GetStringMember(object target, string memberName)
    {
        object value = GetMemberValue(target, memberName);
        return value as string;
    }

    private sealed class SeedResult
    {
        public SeedResult(string weaponId, string relicId, string consumableId)
        {
            WeaponId = weaponId;
            RelicId = relicId;
            ConsumableId = consumableId;
        }

        public string WeaponId { get; private set; }
        public string RelicId { get; private set; }
        public string ConsumableId { get; private set; }

        public string ToSummary()
        {
            return $"weapon={WeaponId ?? "(null)"}, relic={RelicId ?? "(null)"}, consumable={ConsumableId ?? "(null)"}";
        }
    }

    private sealed class RelicSlotSnapshot
    {
        public RelicSlotSnapshot(string relicId, int level)
        {
            RelicId = relicId;
            Level = level;
        }

        public string RelicId { get; private set; }
        public int Level { get; private set; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(RelicId) ? "(empty)" : $"{RelicId}@Lv{Level}";
        }
    }

    private sealed class InventorySnapshot
    {
        public int ActiveWeaponSlotIndex;
        public string[] WeaponSlotIds;
        public RelicSlotSnapshot[] RelicSlots;
        public string[] ConsumableSlotIds;

        public bool HasAnyItem
        {
            get
            {
                return HasAnyNonEmpty(WeaponSlotIds) ||
                       HasAnyRelic(RelicSlots) ||
                       HasAnyNonEmpty(ConsumableSlotIds);
            }
        }

        public static InventorySnapshot FromRuntimeState(object state)
        {
            if (state == null)
                return new InventorySnapshot();

            object weaponInventory = GetMemberValue(state, "weaponInventory");
            object relicInventory = GetMemberValue(state, "relicInventory");
            object consumableInventory = GetMemberValue(state, "consumableInventory");

            var snapshot = new InventorySnapshot();
            snapshot.ActiveWeaponSlotIndex = weaponInventory != null ? GetIntMember(weaponInventory, "activeSlotIndex") : -1;
            snapshot.WeaponSlotIds = CloneStringArray(GetMemberValue(weaponInventory, "slotWeaponIds") as string[]);
            snapshot.RelicSlots = CreateRelicSnapshots(GetMemberValue(relicInventory, "slots") as Array);
            snapshot.ConsumableSlotIds = CreateConsumableIds(GetMemberValue(consumableInventory, "slots") as Array);
            return snapshot;
        }

        public bool EqualsTo(InventorySnapshot other)
        {
            if (other == null)
                return false;

            return ActiveWeaponSlotIndex == other.ActiveWeaponSlotIndex &&
                   SequenceEqual(WeaponSlotIds, other.WeaponSlotIds) &&
                   SequenceEqual(RelicSlots, other.RelicSlots) &&
                   SequenceEqual(ConsumableSlotIds, other.ConsumableSlotIds);
        }

        public string ToMultilineString()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"activeWeaponSlotIndex={ActiveWeaponSlotIndex}");
            builder.AppendLine($"weaponSlots=[{JoinStrings(WeaponSlotIds)}]");
            builder.AppendLine($"relicSlots=[{JoinRelics(RelicSlots)}]");
            builder.AppendLine($"consumableSlots=[{JoinStrings(ConsumableSlotIds)}]");
            return builder.ToString().TrimEnd();
        }

        private static string[] CloneStringArray(string[] source)
        {
            return source != null ? (string[])source.Clone() : new string[0];
        }

        private static RelicSlotSnapshot[] CreateRelicSnapshots(Array source)
        {
            if (source == null)
                return new RelicSlotSnapshot[0];

            var result = new RelicSlotSnapshot[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                object slot = source.GetValue(i);
                result[i] = slot == null
                    ? new RelicSlotSnapshot(null, 0)
                    : new RelicSlotSnapshot(GetStringMember(slot, "relicId"), GetIntMember(slot, "level"));
            }

            return result;
        }

        private static string[] CreateConsumableIds(Array source)
        {
            if (source == null)
                return new string[0];

            var result = new string[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                object slot = source.GetValue(i);
                result[i] = slot != null ? GetStringMember(slot, "consumableId") : null;
            }

            return result;
        }

        private static bool HasAnyNonEmpty(string[] values)
        {
            if (values == null)
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrEmpty(values[i]))
                    return true;
            }

            return false;
        }

        private static bool HasAnyRelic(RelicSlotSnapshot[] values)
        {
            if (values == null)
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null && !string.IsNullOrEmpty(values[i].RelicId))
                    return true;
            }

            return false;
        }

        private static bool SequenceEqual(string[] left, string[] right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null || left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        private static bool SequenceEqual(RelicSlotSnapshot[] left, RelicSlotSnapshot[] right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null || left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                string leftId = left[i] != null ? left[i].RelicId : null;
                string rightId = right[i] != null ? right[i].RelicId : null;
                int leftLevel = left[i] != null ? left[i].Level : 0;
                int rightLevel = right[i] != null ? right[i].Level : 0;

                if (leftId != rightId || leftLevel != rightLevel)
                    return false;
            }

            return true;
        }

        private static string JoinStrings(string[] values)
        {
            if (values == null || values.Length == 0)
                return string.Empty;

            var builder = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    builder.Append(", ");

                builder.Append(string.IsNullOrEmpty(values[i]) ? "(empty)" : values[i]);
            }

            return builder.ToString();
        }

        private static string JoinRelics(RelicSlotSnapshot[] values)
        {
            if (values == null || values.Length == 0)
                return string.Empty;

            var builder = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    builder.Append(", ");

                builder.Append(values[i] != null ? values[i].ToString() : "(empty)");
            }

            return builder.ToString();
        }
    }
}
