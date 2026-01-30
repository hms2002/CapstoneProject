using System.Collections;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// UE의 UGameplayAbility에 해당하는 "실행 로직".
    /// ScriptableObject로 만들어두면 디자이너/기획자도 쉽게 교체 가능.
    /// (원하면 C# class + reflection 방식으로 바꿔도 됨)
    /// </summary>
    public abstract class AbilityLogic : ScriptableObject
    {
        public abstract IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget);
    }
}
