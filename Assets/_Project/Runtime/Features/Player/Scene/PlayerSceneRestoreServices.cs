using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGAS;

internal static class PlayerSceneRestoreExecutionService
{
    public static PlayerRuntimeRestoreResult CreateResult(
        PlayerRuntimeRestoreRequest request,
        IPlayerRuntimeResolver resolver,
        Object logOwner)
    {
        if (!PlayerSceneRestorePlanner.CanResolvePendingEquipment(request.PendingState, resolver, logOwner))
            return CreateFailureResult(request, resolver);

        if (!PlayerSceneRestorePlanner.TryGatherPlayerComponents(request.Player, logOwner, out var context))
            return CreateFailureResult(request, resolver);

        return PlayerSceneRestorePlanner.CreateResult(request, resolver, context);
    }

    private static PlayerRuntimeRestoreResult CreateFailureResult(
        PlayerRuntimeRestoreRequest request,
        IPlayerRuntimeResolver resolver)
    {
        return new PlayerRuntimeRestoreResult(
            false,
            request.Player,
            request.Gameplay,
            request.PendingState,
            resolver,
            default);
    }
}

internal readonly struct PlayerRuntimeRestoreRequest
{
    public readonly GameObject Player;
    public readonly GamePlayDataManager Gameplay;
    public readonly PlayerRuntimeState PendingState;

    public PlayerRuntimeRestoreRequest(
        GameObject player,
        GamePlayDataManager gameplay,
        PlayerRuntimeState pendingState)
    {
        Player = player;
        Gameplay = gameplay;
        PendingState = pendingState;
    }
}

internal readonly struct PlayerRuntimeRestoreResult
{
    public readonly bool Succeeded;
    public readonly GameObject Player;
    public readonly GamePlayDataManager Gameplay;
    public readonly PlayerRuntimeState PendingState;
    public readonly IPlayerRuntimeResolver Resolver;
    public readonly PlayerSystemContext Context;

    public PlayerRuntimeRestoreResult(
        bool succeeded,
        GameObject player,
        GamePlayDataManager gameplay,
        PlayerRuntimeState pendingState,
        IPlayerRuntimeResolver resolver,
        PlayerSystemContext context)
    {
        Succeeded = succeeded;
        Player = player;
        Gameplay = gameplay;
        PendingState = pendingState;
        Resolver = resolver;
        Context = context;
    }
}

internal static class PlayerSceneRestorePlanner
{
    public static GameObject FindPlayer()
    {
        if (PlayerRuntimeRegistry.CurrentPlayer != null)
            return PlayerRuntimeRegistry.CurrentPlayer.gameObject;

        var taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
            return taggedPlayer;

        return null;
    }

    public static PlayerRuntimeRestoreResult CreateResult(
        PlayerRuntimeRestoreRequest request,
        IPlayerRuntimeResolver resolver,
        PlayerSystemContext context)
    {
        return new PlayerRuntimeRestoreResult(
            request.Player != null
            && request.Gameplay != null
            && request.PendingState != null
            && resolver != null,
            request.Player,
            request.Gameplay,
            request.PendingState,
            resolver,
            context);
    }

    public static bool TryGatherPlayerComponents(
        GameObject player,
        Object logOwner,
        out PlayerSystemContext ctx)
    {
        if (player == null)
        {
            ctx = default;
            return false;
        }

        ctx = new PlayerSystemContext
        {
            weaponInventory = player.GetComponent<WeaponInventory2D>(),
            consumableInventory = player.GetComponent<PlayerConsumableInventory>(),
            relicInventory = player.GetComponent<RelicInventory>(),
            attributeSet = player.GetComponent<AttributeSet>(),
            effectRunner = player.GetComponent<GameplayEffectRunner>(),
            tagSystem = player.GetComponent<TagSystem>(),
            abilitySystem = player.GetComponent<AbilitySystem>()
        };

        if (ctx.weaponInventory == null || ctx.consumableInventory == null || ctx.relicInventory == null)
        {
            Debug.LogWarning(
                "[PlayerSceneRestoreBootstrapper] Player inventory components are missing. Pending PlayerRuntimeState restore will wait.",
                logOwner);
            return false;
        }

        return true;
    }

    public static bool IsRestoreAllowedForCurrentScene(GamePlayDataManager gameplay)
    {
        if (gameplay == null)
            return false;

        SceneTransitionContext transition = gameplay.PeekPendingTransition();
        if (transition == null || string.IsNullOrEmpty(transition.toScene))
            return true;

        string activeSceneName = SceneManager.GetActiveScene().name;
        return string.Equals(activeSceneName, transition.toScene, System.StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsItemRestoreReady()
    {
        if (ItemManager.Instance == null)
            return false;

        return ItemManager.Instance.IsReady;
    }

    public static bool CanResolvePendingEquipment(
        PlayerRuntimeState pendingState,
        IPlayerRuntimeResolver runtimeResolver,
        Object logOwner)
    {
        if (pendingState == null || runtimeResolver == null)
            return false;

        if (!CanResolvePendingWeapons(pendingState.weaponInventory, runtimeResolver, logOwner))
            return false;

        if (!CanResolvePendingRelics(pendingState.relicInventory, runtimeResolver, logOwner))
            return false;

        if (!CanResolvePendingConsumables(pendingState.consumableInventory, runtimeResolver, logOwner))
            return false;

        return true;
    }

    public static bool MatchesPendingEquipmentState(PlayerRuntimeState pendingState, PlayerSystemContext ctx)
    {
        if (!MatchesPendingWeapons(pendingState.weaponInventory, ctx.weaponInventory))
            return false;

        if (!MatchesPendingRelics(pendingState.relicInventory, ctx.relicInventory))
            return false;

        if (!MatchesPendingConsumables(pendingState.consumableInventory, ctx.consumableInventory))
            return false;

        return true;
    }

    private static bool CanResolvePendingWeapons(
        WeaponInventoryState state,
        IPlayerRuntimeResolver runtimeResolver,
        Object logOwner)
    {
        if (state?.slotWeaponIds == null)
            return true;

        for (int i = 0; i < state.slotWeaponIds.Length; i++)
        {
            string weaponId = state.slotWeaponIds[i];
            if (string.IsNullOrEmpty(weaponId))
                continue;

            if (runtimeResolver.ResolveWeapon(weaponId) != null)
                continue;

            Debug.LogWarning($"[PlayerSceneRestoreBootstrapper] 臾닿린 蹂듭썝??蹂대쪟?⑸땲?? ?꾩쭅 ?댁꽍?????녿뒗 weaponId={weaponId}, slot={i}", logOwner);
            return false;
        }

        return true;
    }

    private static bool CanResolvePendingRelics(
        RelicInventoryState state,
        IPlayerRuntimeResolver runtimeResolver,
        Object logOwner)
    {
        if (state?.slots == null)
            return true;

        for (int i = 0; i < state.slots.Length; i++)
        {
            var slot = state.slots[i];
            if (slot == null || string.IsNullOrEmpty(slot.relicId))
                continue;

            if (runtimeResolver.ResolveRelic(slot.relicId) != null)
                continue;

            Debug.LogWarning($"[PlayerSceneRestoreBootstrapper] ?좊Ъ 蹂듭썝??蹂대쪟?⑸땲?? ?꾩쭅 ?댁꽍?????녿뒗 relicId={slot.relicId}, slot={i}", logOwner);
            return false;
        }

        return true;
    }

    private static bool CanResolvePendingConsumables(
        ConsumableInventoryState state,
        IPlayerRuntimeResolver runtimeResolver,
        Object logOwner)
    {
        if (state?.slots == null)
            return true;

        for (int i = 0; i < state.slots.Length; i++)
        {
            var slot = state.slots[i];
            if (slot == null || string.IsNullOrEmpty(slot.consumableId))
                continue;

            if (runtimeResolver.ResolveConsumable(slot.consumableId) != null)
                continue;

            Debug.LogWarning($"[PlayerSceneRestoreBootstrapper] consumable 蹂듭썝??蹂대쪟?⑸땲?? ?꾩쭅 ?댁꽍?????녿뒗 consumableId={slot.consumableId}, slot={i}", logOwner);
            return false;
        }

        return true;
    }

    private static bool MatchesPendingWeapons(WeaponInventoryState pending, WeaponInventory2D inventory)
    {
        if (pending == null)
            return true;

        if (inventory == null)
            return false;

        var current = inventory.CaptureInventoryState();
        if (current == null)
            return false;

        if (current.activeSlotIndex != pending.activeSlotIndex)
            return false;

        int pendingCount = pending.slotWeaponIds != null ? pending.slotWeaponIds.Length : 0;
        int currentCount = current.slotWeaponIds != null ? current.slotWeaponIds.Length : 0;
        if (pendingCount != currentCount)
            return false;

        for (int i = 0; i < pendingCount; i++)
        {
            if (!string.Equals(current.slotWeaponIds[i], pending.slotWeaponIds[i], System.StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static bool MatchesPendingRelics(RelicInventoryState pending, RelicInventory inventory)
    {
        if (pending == null)
            return true;

        if (inventory == null)
            return false;

        var current = inventory.CaptureInventoryState();
        if (current == null)
            return false;

        int pendingCount = pending.slots != null ? pending.slots.Length : 0;
        int currentCount = current.slots != null ? current.slots.Length : 0;
        if (pendingCount != currentCount)
            return false;

        for (int i = 0; i < pendingCount; i++)
        {
            var pendingSlot = pending.slots[i];
            var currentSlot = current.slots[i];

            string pendingId = pendingSlot != null ? pendingSlot.relicId : null;
            string currentId = currentSlot != null ? currentSlot.relicId : null;
            int pendingLevel = pendingSlot != null ? pendingSlot.level : 0;
            int currentLevel = currentSlot != null ? currentSlot.level : 0;

            if (!string.Equals(currentId, pendingId, System.StringComparison.Ordinal))
                return false;

            if (currentLevel != pendingLevel)
                return false;
        }

        return true;
    }

    private static bool MatchesPendingConsumables(ConsumableInventoryState pending, PlayerConsumableInventory inventory)
    {
        if (pending == null)
            return true;

        if (inventory == null)
            return false;

        var current = inventory.CaptureInventoryState();
        if (current == null)
            return false;

        int pendingCount = pending.slots != null ? pending.slots.Length : 0;
        int currentCount = current.slots != null ? current.slots.Length : 0;
        if (pendingCount != currentCount)
            return false;

        for (int i = 0; i < pendingCount; i++)
        {
            var pendingSlot = pending.slots[i];
            var currentSlot = current.slots[i];

            string pendingId = pendingSlot != null ? pendingSlot.consumableId : null;
            string currentId = currentSlot != null ? currentSlot.consumableId : null;

            if (!string.Equals(currentId, pendingId, System.StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}
