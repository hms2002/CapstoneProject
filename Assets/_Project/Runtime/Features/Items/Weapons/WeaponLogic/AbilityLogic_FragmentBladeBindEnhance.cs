using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 파편검 Skill2가 GAS 실행 경로를 정상 통과하게 하는 빈 실행 로직이다.
    /// - 실제 강화 타이머 시작과 조각 소모 방지는 성공 발동 후 FragmentBladeRuntimeState가 처리한다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_FragmentBladeBindEnhance", menuName = "GAS/Weapon/Fragment Blade/Bind Enhance Logic")]
    public sealed class AbilityLogic_FragmentBladeBindEnhance : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            yield break;
        }
    }
}
