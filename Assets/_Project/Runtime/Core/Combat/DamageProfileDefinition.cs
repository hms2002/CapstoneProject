using UnityEngine;

namespace UnityGAS
{
    [CreateAssetMenu(fileName = "DamageProfile", menuName = "GAS/Damage Profile")]
    public class DamageProfileDefinition : ScriptableObject
    {
        [Tooltip("ScaledStatFormula에서 StatId 모드를 사용할 때 참조할 Stat 바인딩 정보")]
        public StatTypeBindings statBindings;


        public StatTypeBindings GetStatBindings() => statBindings;
    }
}