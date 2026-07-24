using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 월식도 1차 구조 검증용으로 발동된 액션의 라벨을 콘솔에 기록한다.
    /// - 실제 판정보다 먼저 자세 진입/종료와 공격 선택 흐름이 기대한 AD로 연결되는지 확인하게 돕는다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_EclipseDebugAction", menuName = "GAS/Samples/AbilityLogic/Eclipse Debug Action")]
    public sealed class AbilityLogic_EclipseDebugAction : AbilityLogic
    {
        [SerializeField] private string debugLabel = "EclipseAction";

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            string ownerName = system != null ? system.gameObject.name : "<null>";
            string abilityName = spec != null && spec.Definition != null ? spec.Definition.abilityName : "<unknown>";
            Debug.Log($"[EclipseSword] {debugLabel} activated by {ownerName} via {abilityName}.", system);
            yield break;
        }
    }
}
