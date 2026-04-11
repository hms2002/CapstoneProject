namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - Ability가 공통 규칙으로 사용할 최종 공격속도 배수를 계산한다.
    /// - Attribute/Stat 바인딩이 없거나 값이 비정상이면 안전한 기본값 1을 반환한다.
    /// </summary>
    public static class AbilityAttackSpeedResolver
    {
        public static float ResolveFinalAttackSpeed(AbilitySystem system)
        {
            IStatProvider statProvider = AbilityStatProviderFactory.Create(system);
            if (statProvider == null)
                return 1f;

            float finalAttackSpeed = statProvider.Get(StatId.AttackSpeedFinal);
            return finalAttackSpeed > 0.0001f ? finalAttackSpeed : 1f;
        }
    }
}
