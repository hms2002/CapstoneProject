using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGAS;

/// <summary>
/// 책임 : 시연 치트 실행 결과와 사용자에게 표시할 알림 문구를 함께 전달한다.
/// 치트 적용 여부와 UI 알림 표시를 분리해 알림 UI가 없어도 치트 로직이 안전하게 끝나도록 한다.
/// </summary>
public readonly struct DemoCheatResult
{
    public readonly bool Success;
    public readonly string Message;

    public DemoCheatResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    public static DemoCheatResult Succeeded(string message) => new(true, message);
    public static DemoCheatResult Failed(string message) => new(false, message);
}

/// <summary>
/// 책임 : 시연용 치트의 실제 효과를 기존 런타임 시스템 API를 통해 적용한다.
/// AttributeSet, AbilitySystem, WeaponInventory2D, MovementMotor2D를 직접 우회하지 않고 공식 진입점을 사용한다.
/// </summary>
public sealed class DemoCheatService
{
    private static readonly HashSet<AbilityDefinition> AbilityBuffer = new();
    private static readonly List<RunSpecialNpcInteractor> RunSpecialNpcBuffer = new();
    private readonly UnityEngine.Object logContext;
    private string lastRunSpecialNpcSceneName;
    private int nextRunSpecialNpcIndex;

    public DemoCheatService(UnityEngine.Object logContext)
    {
        this.logContext = logContext;
    }

    public DemoCheatResult AddMagicStone(DemoCheatSettingsSO settings)
    {
        if (CurrencyManager.Instance == null)
            return Fail("마정석 시스템을 찾을 수 없습니다.");

        int amount = Mathf.Max(1, settings.MagicStoneAddAmount);
        CurrencyManager.Instance.AddMagicStone(amount);

        Log($"마정석 추가. amount={amount}, total={CurrencyManager.Instance.GetMagicStone()}");
        return DemoCheatResult.Succeeded($"마정석을 {amount}개 획득했습니다.");
    }

    public DemoCheatResult WarpPlayerToNextRunSpecialNpc(DemoCheatSettingsSO settings)
    {
        if (!TryResolvePlayer(out Transform player))
            return Fail("플레이어를 찾을 수 없습니다.");

        RunSpecialNpcInteractor npc = FindNextRunSpecialNpc();
        if (npc == null)
            return Fail("이 씬에서 Runtime Special NPC를 찾을 수 없습니다.");

        Transform anchor = npc.GetPromptAnchor();
        Vector3 targetPosition = anchor != null ? anchor.position : npc.transform.position;
        targetPosition.z = player.position.z;

        MovementMotor2D movementMotor = player.GetComponent<MovementMotor2D>();
        if (movementMotor != null)
        {
            movementMotor.WarpTo(targetPosition, clearExternalMovement: true, clearMotion: true);
        }
        else
        {
            SetPlayerPositionImmediate(player, targetPosition);
        }

        Log($"Runtime Special NPC 앞으로 워프. scene={SceneManager.GetActiveScene().name}, npc={npc.name}");
        return DemoCheatResult.Succeeded("Runtime Special NPC 앞으로 이동했습니다.");
    }

    public DemoCheatResult RefillPlayerHealth(DemoCheatSettingsSO settings)
    {
        if (!TryResolvePlayer(out Transform player))
            return Fail("플레이어를 찾을 수 없습니다.");

        AttributeSet attributeSet = player.GetComponent<AttributeSet>();
        if (attributeSet == null || settings.HealthAttribute == null || settings.MaxHealthAttribute == null)
        {
            return Fail("체력 치트 설정이 올바르지 않습니다.");
        }

        float maxHealth = attributeSet.GetAttributeValue(settings.MaxHealthAttribute);
        bool applied = attributeSet.TrySetCurrentValue(settings.HealthAttribute, maxHealth, logContext);
        if (!applied)
        {
            return Fail("체력을 회복하지 못했습니다.");
        }

        Log($"체력 MAX 적용. hp={maxHealth:0.###}");
        return DemoCheatResult.Succeeded("체력을 최대치로 회복했습니다.");
    }

    public DemoCheatResult WarpPlayerToNearestPortal(DemoCheatSettingsSO settings)
    {
        if (!TryResolvePlayer(out Transform player))
            return Fail("플레이어를 찾을 수 없습니다.");

        if (!IsPortalWarpAllowedScene())
        {
            Log("포탈 워프 무시: Hub/복도 씬에서만 사용할 수 있습니다.");
            return DemoCheatResult.Failed("이 씬에서는 포탈 워프를 사용할 수 없습니다.");
        }

        ScenePortal portal = FindNearestAllowedPortal(player.position);
        if (portal == null)
        {
            return Fail("이동 가능한 포탈을 찾을 수 없습니다.");
        }

        Transform anchor = portal.GetPromptAnchor();
        Vector3 targetPosition = anchor != null ? anchor.position : portal.transform.position;
        targetPosition.z = player.position.z;

        MovementMotor2D movementMotor = player.GetComponent<MovementMotor2D>();
        if (movementMotor != null)
        {
            movementMotor.WarpTo(targetPosition, clearExternalMovement: true, clearMotion: true);
        }
        else
        {
            SetPlayerPositionImmediate(player, targetPosition);
        }

        Log($"포탈 앞으로 워프. scene={SceneManager.GetActiveScene().name}, portal={portal.name}");
        return DemoCheatResult.Succeeded("가장 가까운 포탈 앞으로 이동했습니다.");
    }

    public DemoCheatResult ResetAllOwnedWeaponCooldowns(DemoCheatSettingsSO settings)
    {
        if (!TryResolvePlayer(out Transform player))
            return Fail("플레이어를 찾을 수 없습니다.");

        AbilitySystem abilitySystem = player.GetComponent<AbilitySystem>();
        WeaponInventory2D weaponInventory = player.GetComponent<WeaponInventory2D>();
        if (abilitySystem == null || weaponInventory == null)
        {
            return Fail("초기화할 무기 쿨타임이 없습니다.");
        }

        AbilityBuffer.Clear();
        for (int i = 0; i < weaponInventory.SlotCount; i++)
        {
            WeaponDefinition weapon = weaponInventory.GetWeaponInSlot(i);
            if (weapon == null)
                continue;

            foreach (AbilityDefinition ability in weapon.EnumerateGrantedAbilities())
            {
                if (ability != null)
                    AbilityBuffer.Add(ability);
            }
        }

        if (AbilityBuffer.Count == 0)
            return Fail("초기화할 무기 쿨타임이 없습니다.");

        int resetCount = 0;
        foreach (AbilityDefinition ability in AbilityBuffer)
        {
            if (abilitySystem.TrySetCooldownRemaining(ability, 0f))
                resetCount++;
        }

        Log($"보유 무기 쿨타임 초기화. abilities={resetCount}/{AbilityBuffer.Count}");
        AbilityBuffer.Clear();
        return resetCount > 0
            ? DemoCheatResult.Succeeded("보유 무기 쿨타임을 초기화했습니다.")
            : DemoCheatResult.Failed("초기화할 무기 쿨타임이 없습니다.");
    }

    public DemoCheatResult IncreasePlayerAttack(DemoCheatSettingsSO settings)
    {
        if (!TryResolvePlayer(out Transform player))
            return Fail("플레이어를 찾을 수 없습니다.");

        AttributeSet attributeSet = player.GetComponent<AttributeSet>();
        if (attributeSet == null || settings.AttackAddAttribute == null)
        {
            return Fail("공격력 치트 설정이 올바르지 않습니다.");
        }

        float currentBase = attributeSet.GetBaseValue(settings.AttackAddAttribute);
        float nextBase = currentBase + settings.AttackIncreaseAmount;
        if (!attributeSet.TrySetBaseValue(settings.AttackAddAttribute, nextBase, logContext))
        {
            return Fail("공격력을 증가시키지 못했습니다.");
        }

        AttributeStatSource statSource = player.GetComponent<AttributeStatSource>();
        if (statSource != null)
            statSource.RebuildProvider();

        Log($"공격력 증가. AttackAdd={currentBase:0.###}->{nextBase:0.###}");
        return DemoCheatResult.Succeeded("공격력이 +10 증가했습니다.");
    }

    private static bool TryResolvePlayer(out Transform player)
    {
        player = PlayerRuntimeRegistry.GetPlayerTransform();
        if (player != null)
            return true;

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        player = taggedPlayer != null ? taggedPlayer.transform : null;
        return player != null;
    }

    private static bool IsPortalWarpAllowedScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(sceneName))
            return false;

        return sceneName.IndexOf("Hub", StringComparison.OrdinalIgnoreCase) >= 0 ||
               sceneName.IndexOf("Corridor", StringComparison.OrdinalIgnoreCase) >= 0 ||
               sceneName.IndexOf("Hallway", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static ScenePortal FindNearestAllowedPortal(Vector3 playerPosition)
    {
        ScenePortal[] portals = UnityEngine.Object.FindObjectsByType<ScenePortal>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        ScenePortal nearest = null;
        float nearestDistanceSqr = float.PositiveInfinity;
        for (int i = 0; i < portals.Length; i++)
        {
            ScenePortal portal = portals[i];
            if (portal == null || !IsAllowedPortalTransition(portal.PortalTransitionType))
                continue;

            float distanceSqr = (portal.transform.position - playerPosition).sqrMagnitude;
            if (distanceSqr >= nearestDistanceSqr)
                continue;

            nearest = portal;
            nearestDistanceSqr = distanceSqr;
        }

        return nearest;
    }

    private RunSpecialNpcInteractor FindNextRunSpecialNpc()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (!string.Equals(lastRunSpecialNpcSceneName, sceneName, StringComparison.Ordinal))
        {
            lastRunSpecialNpcSceneName = sceneName;
            nextRunSpecialNpcIndex = 0;
        }

        RunSpecialNpcBuffer.Clear();
        RunSpecialNpcInteractor[] interactors = UnityEngine.Object.FindObjectsByType<RunSpecialNpcInteractor>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < interactors.Length; i++)
        {
            RunSpecialNpcInteractor interactor = interactors[i];
            if (interactor != null && interactor.isActiveAndEnabled && interactor.gameObject.activeInHierarchy)
                RunSpecialNpcBuffer.Add(interactor);
        }

        if (RunSpecialNpcBuffer.Count == 0)
            return null;

        RunSpecialNpcBuffer.Sort(CompareRunSpecialNpcStableOrder);
        if (nextRunSpecialNpcIndex < 0 || nextRunSpecialNpcIndex >= RunSpecialNpcBuffer.Count)
            nextRunSpecialNpcIndex = 0;

        RunSpecialNpcInteractor selected = RunSpecialNpcBuffer[nextRunSpecialNpcIndex];
        nextRunSpecialNpcIndex = (nextRunSpecialNpcIndex + 1) % RunSpecialNpcBuffer.Count;
        RunSpecialNpcBuffer.Clear();
        return selected;
    }

    private static int CompareRunSpecialNpcStableOrder(RunSpecialNpcInteractor a, RunSpecialNpcInteractor b)
    {
        return string.CompareOrdinal(BuildRunSpecialNpcStableKey(a), BuildRunSpecialNpcStableKey(b));
    }

    private static string BuildRunSpecialNpcStableKey(RunSpecialNpcInteractor interactor)
    {
        if (interactor == null)
            return string.Empty;

        Transform current = interactor.transform;
        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = $"{current.GetSiblingIndex():D4}:{current.name}/{path}";
        }

        return $"{interactor.gameObject.scene.name}/{path}/{interactor.transform.GetSiblingIndex():D4}";
    }

    private static bool IsAllowedPortalTransition(TransitionType transitionType)
    {
        return transitionType == TransitionType.HubToRunStart ||
               transitionType == TransitionType.CorridorToCorridor ||
               transitionType == TransitionType.CorridorToBoss;
    }

    private static void SetPlayerPositionImmediate(Transform player, Vector3 targetPosition)
    {
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = new Vector2(targetPosition.x, targetPosition.y);
            body.linearVelocity = Vector2.zero;
        }

        player.position = targetPosition;
    }

    private void Log(string message)
    {
        Debug.Log($"[DemoCheat] {message}", logContext);
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[DemoCheat] {message}", logContext);
    }

    private DemoCheatResult Fail(string message)
    {
        LogWarning(message);
        return DemoCheatResult.Failed(message);
    }
}
