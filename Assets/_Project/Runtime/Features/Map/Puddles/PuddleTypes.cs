namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 장판의 전투 속성 타입을 공통 enum으로 표현한다.
    /// - 보스 흡수, 변환, DOT/버프 분기가 문자열 비교에 기대지 않게 한다.
    /// </summary>
    public enum PuddleElementType
    {
        Alcohol,
        Fire
    }

    /// <summary>
    /// 책임 :
    /// - 장판 actor가 현재 어떤 판정 모드로 동작하는지 표현한다.
    /// - 바닥 판정과 흡수 탄막 판정을 명확히 분리한다.
    /// </summary>
    public enum PuddleAreaMode
    {
        Ground = 0,
        Igniting = 1,
        AbsorbPreparing = 2,
        AbsorbProjectile = 3,
        Consumed = 4
    }
}
