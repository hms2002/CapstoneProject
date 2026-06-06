/// <summary>
/// 책임 :
/// - 프로젝트가 기본으로 사용하는 pair interaction rule 집합을 한 곳에서 생성하고 노출한다.
/// - interaction layer가 조합 규칙 구성을 직접 하드코딩하지 않고, 규칙 등록 책임을 별도 레지스트리로 분리하게 만든다.
/// </summary>
public static class WeaponPairInteractionRuleRegistry
{
    public static WeaponPairInteractionRule[] CreateDefaultRules()
    {
        return new WeaponPairInteractionRule[]
        {
            new MarkSwordExecutionGunInteractionRule(),
            new SunMoonInteractionRule()
        };
    }
}
