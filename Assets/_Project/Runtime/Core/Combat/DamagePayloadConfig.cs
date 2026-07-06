using System;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 한 타격에서 사용할 원소 누적 legacy 공식을 직렬화한다.
    /// - 현재 적용 경로는 공격자 ElementOffenseSource를 우선 사용하지만, 기존 데이터 호환을 위해 보관한다.
    /// </summary>
    [Serializable]
    public sealed class ElementFormulaEntry
    {
        public GameplayTag elementType;
        public ScaledStatFormula formula;
    }

    /// <summary>
    /// 책임 :
    /// - 한 타격의 피해 채널과 선택적 스태거/원소 누적 설정을 직렬화한다.
    /// - Core 피해 스냅샷 빌더와 Gameplay 무기 데이터가 공유하는 payload 계약이다.
    /// </summary>
    [Serializable]
    public sealed class DamagePayloadConfig
    {
        [Header("Channels")]
        public bool includeStaggerBuildUp = true;
        public bool includeElementBuildUp = true;

        [Header("Optional Formulas")]
        public ScaledStatFormula staggerFormula;

        [Tooltip("Legacy per-hit element formulas. Applied build-up is resolved from ElementOffenseSource.")]
        public ElementFormulaEntry[] elementFormulas;

        [Tooltip("Legacy flag for per-hit element formulas. Ignored by the applied build-up path.")]
        public bool critAffectsElement = true;

        public bool HasElementFormulas => elementFormulas != null && elementFormulas.Length > 0;
    }
}
