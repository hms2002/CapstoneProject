/// <summary>
/// 책임:
/// 취룡 보스 패턴들이 공유하는 전용 런타임 상태를 보관한다.
/// </summary>
public sealed class DragonRuntimeData
{
    public int AbsorbedAlcoholProjectileCount { get; private set; }
    public int AbsorbedFireProjectileCount { get; private set; }
    public int LastDashComboCount { get; private set; }
    public bool HasAbsorbedAnyProjectile => AbsorbedAlcoholProjectileCount > 0 || AbsorbedFireProjectileCount > 0;
    public bool HasAbsorbedFireProjectile => AbsorbedFireProjectileCount > 0;
    public bool HasOnlyAbsorbedAlcoholProjectiles => AbsorbedAlcoholProjectileCount > 0 && AbsorbedFireProjectileCount <= 0;

    /// <summary>흡수 패턴 시작 전에 이전 흡수 결과를 비운다.</summary>
    public void BeginAbsorbPattern()
    {
        AbsorbedAlcoholProjectileCount = 0;
        AbsorbedFireProjectileCount = 0;
    }

    /// <summary>술 탄막이 보스에게 도달했음을 기록한다.</summary>
    public void RecordAlcoholProjectileAbsorbed()
    {
        AbsorbedAlcoholProjectileCount++;
    }

    /// <summary>불 탄막이 보스에게 도달했음을 기록한다.</summary>
    public void RecordFireProjectileAbsorbed()
    {
        AbsorbedFireProjectileCount++;
    }

    /// <summary>마지막 돌진 콤보가 몇 회 실행됐는지 기록한다.</summary>
    public void SetLastDashComboCount(int count)
    {
        LastDashComboCount = count < 0 ? 0 : count;
    }

    /// <summary>패턴 실행 중 누적된 임시 카운터를 초기 상태로 되돌린다.</summary>
    public void ResetPatternCounters()
    {
        LastDashComboCount = 0;
    }
}
