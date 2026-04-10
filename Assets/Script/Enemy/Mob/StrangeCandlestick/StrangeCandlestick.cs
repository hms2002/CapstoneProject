using UnityEngine;

public class StrangeCandlestick : Mob
{
    /// <summary>촛대 괴물은 배치된 자리에서 추적 이동하지 않습니다.</summary>
    public override bool CanUseChaseMovement()
    {
        return false;
    }

    /// <summary>탄막 구현 전까지 기본 Tackle 시도를 비활성화합니다.</summary>
    protected override bool CanUsePrimaryAttack()
    {
        return false;
    }

    /// <summary>촛대 괴물은 Tackle 관련 기즈모를 그리지 않습니다.</summary>
    protected override void DrawAttackGizmos()
    {
    }
}
