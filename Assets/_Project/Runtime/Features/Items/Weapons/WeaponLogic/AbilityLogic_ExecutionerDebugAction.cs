using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 대검 처형자 1차 구조 검증용으로 Ready / Finish / Fallback 실행을 로그로 명확하게 남긴다.
    /// - 실제 전투 판정이 아닌 실행 분기 검증이 목적일 때 최소 비용의 AbilityLogic 구현을 제공한다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_ExecutionerDebugAction", menuName = "GAS/Weapon/Executioner/Debug Action")]
    public sealed class AbilityLogic_ExecutionerDebugAction : AbilityLogic
    {
        [SerializeField] private string debugLabel = "Executioner Debug Action";

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            string ownerName = system != null ? system.gameObject.name : "<null>";
            string abilityName = spec?.Definition != null ? spec.Definition.abilityName : "<null>";
            Debug.Log($"[ExecutionerGreatsword] {debugLabel} activated by {ownerName} via {abilityName}.", system);
            yield break;
        }
    }
}
