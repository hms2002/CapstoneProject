/// <summary>
/// 책임 :
/// - 마녀 보스 보호막 전용 타격을 받는 대상의 최소 계약을 정의한다.
/// - 일반 HP 피해와 분리된 보호막 단계 감소 규칙을 투사체/기믹 쪽에서 안전하게 호출할 수 있게 한다.
/// </summary>
public interface IWitchShieldReceiver
{
    bool HasShield { get; }
    int CurrentShieldStage { get; }
    int MaxShieldStage { get; }

    void ActivateShield(int stageCount = 4);
    bool TryApplyShieldHit(int amount = 1);
    void BreakShield();
    void ClearShield();
}
