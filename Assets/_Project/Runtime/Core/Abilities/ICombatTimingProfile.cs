namespace UnityGAS
{
    /// <summary>
    /// 책임 : Core 전투 타이밍 보정 서비스가 구체 몬스터 컴포넌트 없이 슬롯별 보정 여부를 질의하게 하는 계약이다.
    /// </summary>
    public interface ICombatTimingProfile
    {
        bool TryResolveTimingSlotScale(CombatTimingSlot slot, bool globalValue, out bool resolvedValue);
    }
}
