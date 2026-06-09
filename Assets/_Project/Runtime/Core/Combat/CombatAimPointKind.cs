namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 전투 객체를 겨냥할 때 필요한 대표 기준점 종류를 정의한다.
    /// - 바닥 위치, 몸 중심, 원거리 투사체 목표점, 머리 위 표시점을 호출자가 명확히 구분하게 한다.
    /// </summary>
    public enum CombatAimPointKind
    {
        Root = 0,
        BodyCenter = 1,
        ProjectileTarget = 2,
        Overhead = 3
    }
}
