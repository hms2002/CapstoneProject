namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 원본 step 데이터를 최종 공격속도 배수에 맞는 런타임 step 데이터로 변환하는 계약을 정의한다.
    /// - 서로 다른 무기 step 구조체도 같은 방식으로 공격속도 보정을 제공하게 만든다.
    /// </summary>
    public interface IAttackSpeedScaledStep<out TScaledStep>
    {
        TScaledStep CreateAttackSpeedScaled(float finalAttackSpeed);
    }
}
