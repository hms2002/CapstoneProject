/// <summary>
/// 책임 :
/// - WeaponRuntimeData의 시간 경과, 자동 만료, 패시브 진행 같은 변화 규칙을 데이터 바깥 계층에서 적용한다.
/// - data가 Update/Tick 책임을 직접 갖지 않도록 슬롯별 지속 상태 갱신 로직을 processor 계층으로 분리한다.
/// </summary>
public abstract class WeaponRuntimeProcessor
{
    /// <summary>
    /// 책임 :
    /// - 슬롯이 갱신 주기 동안 유지해야 할 상태 변화 규칙을 적용한다.
    /// - 기본 구현은 아무 작업도 하지 않으며, 시간 경과 로직이 필요한 무기만 override 한다.
    /// </summary>
    public virtual void Tick(in WeaponRuntimeProcessContext context, float deltaTime)
    {
    }
}
