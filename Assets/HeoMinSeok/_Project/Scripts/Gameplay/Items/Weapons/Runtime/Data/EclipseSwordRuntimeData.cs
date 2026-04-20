using UnityEngine;

/// <summary>
/// 책임 :
/// - 월식도가 슬롯 단위로 유지해야 하는 자세/누적/Bloom 가능 상태를 인벤토리 소유 plain data로 보관한다.
/// - 선택 전략, 장착 중 live adapter, 저장/복원이 모두 같은 상태 소스를 읽도록 진실한 저장소 역할을 맡는다.
/// </summary>
public sealed class EclipseSwordRuntimeData : WeaponRuntimeData, IWeaponRuntimeStatePersistence
{
    /// <summary>
    /// 책임 :
    /// - 월식도 runtime data의 저장/복원에 사용하는 직렬화 payload를 정의한다.
    /// - config와 현재 상태를 함께 담아 다음 씬에서도 같은 규칙으로 이어지게 만든다.
    /// </summary>
    [System.Serializable]
    private sealed class PersistencePayload
    {
        public bool startsInEclipseStance;
        public bool alternateStanceAttacks = true;
        public int attacksRequiredForBloom = 2;
        public bool isInEclipseStance;
        public int nextStanceAttackIndex;
        public int currentStanceAttackCount;
        public bool canUseBloomFinish;
    }

    private bool startsInEclipseStance;
    private bool alternateStanceAttacks = true;
    private int attacksRequiredForBloom = 2;
    private bool isInEclipseStance;
    private int nextStanceAttackIndex;
    private int currentStanceAttackCount;
    private bool canUseBloomFinish;

    public string StateType => nameof(EclipseSwordRuntimeData);
    public bool IsInEclipseStance => isInEclipseStance;
    public int NextStanceAttackIndex => nextStanceAttackIndex;
    public int CurrentStanceAttackCount => currentStanceAttackCount;
    public bool CanUseBloomFinish => canUseBloomFinish;
    public bool AlternateStanceAttacks => alternateStanceAttacks;

    /// <summary>
    /// 책임 :
    /// - 월식도 loadout이 정의한 기본 규칙을 새 슬롯 data에 주입하고 초기 상태를 만든다.
    /// - 슬롯에 처음 배치된 월식도가 프리팹 유무와 관계없이 같은 기본 상태로 시작하게 만든다.
    /// </summary>
    public void ApplyDefaults(EclipseSwordLoadout loadout)
    {
        startsInEclipseStance = loadout != null && loadout.StartsInEclipseStance;
        alternateStanceAttacks = loadout == null || loadout.AlternateStanceAttacks;
        attacksRequiredForBloom = loadout != null
            ? Mathf.Max(1, loadout.AttacksRequiredForBloom)
            : 2;

        ResetState();
    }

    /// <summary>
    /// 책임 :
    /// - 월식 자세 진입 시점에 자세 내부 누적 상태를 초기화한다.
    /// - Enter Stance 성공 후 후속 선택 규칙이 항상 같은 시작점에서 열리게 만든다.
    /// </summary>
    public void EnterStance()
    {
        isInEclipseStance = true;
        nextStanceAttackIndex = 0;
        currentStanceAttackCount = 0;
        canUseBloomFinish = false;
    }

    /// <summary>
    /// 책임 :
    /// - Exit/Bloom Finish 이후 월식도 상태를 기본 상태로 되돌린다.
    /// - 슬롯에 저장된 지속 상태도 동일한 종료 규칙을 따르게 만든다.
    /// </summary>
    public void ExitStance()
    {
        ResetState();
    }

    /// <summary>
    /// 책임 :
    /// - 자세 중 공격 성공 이후 다음 공격 인덱스와 Bloom 가능 여부를 누적 규칙에 따라 갱신한다.
    /// - 상태 기반 선택 전략이 다음 Attack/Skill1 분기를 안정적으로 계산하게 만든다.
    /// </summary>
    public void AdvanceStanceAttackState(int nextAttackIndex)
    {
        if (!alternateStanceAttacks)
            return;

        currentStanceAttackCount++;
        nextStanceAttackIndex = nextAttackIndex;

        if (currentStanceAttackCount >= Mathf.Max(1, attacksRequiredForBloom))
            canUseBloomFinish = true;
    }

    public string CaptureStateJson()
    {
        return JsonUtility.ToJson(new PersistencePayload
        {
            startsInEclipseStance = startsInEclipseStance,
            alternateStanceAttacks = alternateStanceAttacks,
            attacksRequiredForBloom = attacksRequiredForBloom,
            isInEclipseStance = isInEclipseStance,
            nextStanceAttackIndex = nextStanceAttackIndex,
            currentStanceAttackCount = currentStanceAttackCount,
            canUseBloomFinish = canUseBloomFinish
        });
    }

    public void RestoreStateJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        var payload = JsonUtility.FromJson<PersistencePayload>(json);
        if (payload == null)
            return;

        startsInEclipseStance = payload.startsInEclipseStance;
        alternateStanceAttacks = payload.alternateStanceAttacks;
        attacksRequiredForBloom = Mathf.Max(1, payload.attacksRequiredForBloom);
        isInEclipseStance = payload.isInEclipseStance;
        nextStanceAttackIndex = payload.nextStanceAttackIndex;
        currentStanceAttackCount = payload.currentStanceAttackCount;
        canUseBloomFinish = payload.canUseBloomFinish;
    }

    private void ResetState()
    {
        isInEclipseStance = startsInEclipseStance;
        nextStanceAttackIndex = 0;
        currentStanceAttackCount = 0;
        canUseBloomFinish = false;
    }
}
