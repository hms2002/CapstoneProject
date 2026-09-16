using System;
using UnityEngine;
using UnityGAS;

public enum LevelRewardEffectLifetime
{
    Persistent = 0,
    InstantOnce = 1,
}

/// <summary>
/// 책임: 레벨업 효과가 현재 플레이어 상태에서 선택 가능한지 판단할 때 필요한 읽기 전용 문맥을 제공한다.
/// </summary>
public readonly struct LevelRewardEligibilityContext
{
    public LevelRewardEligibilityContext(PlayerInteractor2D player, LevelProgressionState progression)
    {
        Player = player;
        Progression = progression;
    }

    public PlayerInteractor2D Player { get; }
    public LevelProgressionState Progression { get; }
}

/// <summary>
/// 책임: 효과 적용 시 필요한 플레이어, 선택 상태, 효과별 JSON 상태를 한 경계로 전달한다.
/// </summary>
public readonly struct LevelRewardApplyContext
{
    public LevelRewardApplyContext(
        PlayerInteractor2D player,
        LevelProgressionState progression,
        LevelRewardSelectionState selectionState,
        LevelRewardEffectState effectState,
        bool isReapply)
    {
        Player = player;
        Progression = progression;
        SelectionState = selectionState;
        EffectState = effectState;
        IsReapply = isReapply;
    }

    public PlayerInteractor2D Player { get; }
    public LevelProgressionState Progression { get; }
    public LevelRewardSelectionState SelectionState { get; }
    public LevelRewardEffectState EffectState { get; }
    public bool IsReapply { get; }
}

/// <summary>
/// 책임: 플레이어/씬에 붙은 레벨업 효과의 live 구독과 modifier를 한 경로로 정리한다.
/// </summary>
public interface ILevelRewardEffectHandle : IDisposable
{
}

/// <summary>
/// 책임: 레벨업 보상에 조합 가능한 효과 하나의 선택 조건, 적용 방식, live cleanup 계약을 정의한다.
/// </summary>
public abstract class LevelRewardEffectSO : ScriptableObject
{
    [SerializeField] private string effectId;

    public string EffectId => effectId;
    public abstract LevelRewardEffectLifetime Lifetime { get; }

    public virtual bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        failureReason = null;
        return context.Player != null;
    }

    /// <summary>
    /// Persistent 효과는 live 구독/modifier를 정리할 handle을 반환한다.
    /// InstantOnce 효과는 즉시 적용 후 null을 반환할 수 있다.
    /// </summary>
    public abstract ILevelRewardEffectHandle Apply(LevelRewardApplyContext context);
}

/// <summary>
/// 레벨업 저주의 "직접 피해"를 현재 장착 무기가 부여한 능력의 피해로 한정한다.
/// 화상, 전기 발현, 유물 자동 공격, 반사 피해는 이 경로에 포함되지 않는다.
/// </summary>
internal static class LevelRewardDirectDamageUtility
{
    public static bool IsDirectWeaponDamage(
        CombatOutgoingDamageContext context,
        AbilitySystem playerAbilities,
        WeaponInventory2D inventory)
    {
        AbilityDefinition ability = context.SourceSpec?.Definition;
        if (context.SourceSystem != playerAbilities || inventory == null || ability == null)
            return false;

        for (int slot = 0; slot < inventory.SlotCount; slot++)
        {
            WeaponDefinition weapon = inventory.GetWeaponInSlot(slot);
            if (weapon == null)
                continue;

            foreach (AbilityDefinition grantedAbility in weapon.EnumerateGrantedAbilities())
            {
                if (grantedAbility == ability)
                    return true;
            }
        }

        return false;
    }
}
