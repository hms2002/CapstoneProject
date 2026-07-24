using System;
using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
public class WitchShieldController : MonoBehaviour, IWitchShieldReceiver
{
    // 이 클래스의 책임:
    // 마녀 보스 보호막의 단계값, 보호막 상태 태그, 무적 태그를 함께 관리한다.

    private const string ShieldedTagResourcePath = "Tags/State.Status.Shielded";
    private const string InvulnerableTagResourcePath = "Tags/State.Invulnerable";

    [SerializeField] private TagSystem tagSystem;
    [SerializeField] private GameplayTag shieldedTag;
    [SerializeField] private GameplayTag invulnerableTag;
    [SerializeField] private int defaultShieldStageCount = 4;

    private int maxShieldStage;
    private int currentShieldStage;
    private bool hasAppliedShieldedTag;
    private bool hasAppliedInvulnerableTag;

    public int CurrentShieldStage => currentShieldStage;
    public int MaxShieldStage => maxShieldStage;
    public bool HasShield => currentShieldStage > 0;
    public float NormalizedShieldRatio => maxShieldStage > 0 ? (float)currentShieldStage / maxShieldStage : 0f;

    public event Action<int, int> ShieldStageChanged;
    public event Action ShieldBroken;

    private void Awake()
    {
        if (tagSystem == null)
            tagSystem = GetComponent<TagSystem>();

        if (shieldedTag == null)
            shieldedTag = Resources.Load<GameplayTag>(ShieldedTagResourcePath);

        if (invulnerableTag == null)
            invulnerableTag = Resources.Load<GameplayTag>(InvulnerableTagResourcePath);
    }

    /// <summary>
    /// 책임 :
    /// - 보호막을 지정 단계수로 활성화한다.
    /// - 보호막이 살아 있는 동안 필요한 상태 태그를 보장한다.
    /// </summary>
    public void ActivateShield(int stageCount = 4)
    {
        int resolvedStageCount = stageCount > 0 ? stageCount : Mathf.Max(1, defaultShieldStageCount);
        maxShieldStage = resolvedStageCount;
        currentShieldStage = resolvedStageCount;

        EnsureActiveTags();
        ShieldStageChanged?.Invoke(currentShieldStage, maxShieldStage);
    }

    /// <summary>
    /// 책임 :
    /// - 보호막 전용 타격을 적용해 단계값을 감소시킨다.
    /// - 단계가 0이 되면 보호막 파괴 처리까지 이어서 수행한다.
    /// </summary>
    public bool TryApplyShieldHit(int amount = 1)
    {
        if (!HasShield)
            return false;

        int resolvedAmount = Mathf.Max(1, amount);
        currentShieldStage = Mathf.Max(0, currentShieldStage - resolvedAmount);
        ShieldStageChanged?.Invoke(currentShieldStage, maxShieldStage);

        if (currentShieldStage == 0)
        {
            RemoveActiveTags();
            ShieldBroken?.Invoke();
        }

        return true;
    }

    /// <summary>
    /// 책임 :
    /// - 보호막을 강제로 깨뜨리고 파괴 이벤트를 발생시킨다.
    /// - 촛불 재점화 성공처럼 "정상 파훼"를 표현할 때 사용한다.
    /// </summary>
    public void BreakShield()
    {
        if (!HasShield && !hasAppliedShieldedTag && !hasAppliedInvulnerableTag)
            return;

        currentShieldStage = 0;
        ShieldStageChanged?.Invoke(currentShieldStage, maxShieldStage);
        RemoveActiveTags();
        ShieldBroken?.Invoke();
    }

    /// <summary>
    /// 책임 :
    /// - 보호막 상태를 조용히 정리한다.
    /// - 패턴 강제 종료나 타임아웃 종료처럼 파괴 연출 없이 정리할 때 사용한다.
    /// </summary>
    public void ClearShield()
    {
        if (!HasShield && !hasAppliedShieldedTag && !hasAppliedInvulnerableTag)
            return;

        currentShieldStage = 0;
        ShieldStageChanged?.Invoke(currentShieldStage, maxShieldStage);
        RemoveActiveTags();
    }

    /// <summary>
    /// 책임 :
    /// - 보호막 활성 상태에 필요한 상태 태그를 중복 없이 부여한다.
    /// </summary>
    private void EnsureActiveTags()
    {
        if (tagSystem == null)
            return;

        if (shieldedTag != null && !hasAppliedShieldedTag)
        {
            tagSystem.AddTag(shieldedTag, 1);
            hasAppliedShieldedTag = true;
        }

        if (invulnerableTag != null && !hasAppliedInvulnerableTag)
        {
            tagSystem.AddTag(invulnerableTag, 1);
            hasAppliedInvulnerableTag = true;
        }
    }

    /// <summary>
    /// 책임 :
    /// - 이 컨트롤러가 직접 부여했던 보호막 관련 태그만 안전하게 회수한다.
    /// </summary>
    private void RemoveActiveTags()
    {
        if (tagSystem == null)
            return;

        if (shieldedTag != null && hasAppliedShieldedTag)
        {
            tagSystem.RemoveTag(shieldedTag, 1);
            hasAppliedShieldedTag = false;
        }

        if (invulnerableTag != null && hasAppliedInvulnerableTag)
        {
            tagSystem.RemoveTag(invulnerableTag, 1);
            hasAppliedInvulnerableTag = false;
        }
    }
}
