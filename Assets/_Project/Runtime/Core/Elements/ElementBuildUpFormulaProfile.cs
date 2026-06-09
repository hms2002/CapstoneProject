using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    [Serializable]
    public sealed class ElementBuildUpFormulaEntry
    {
        [Tooltip("Element type tag. e.g. Element.Bleed / Element.Poison")]
        public GameplayTag elementType;

        [Tooltip("공격자의 최종 속성 스탯을 조회할 StatId")]
        public StatId sourceStatId;

        [Min(0f)] public float multiplier = 1f;
        public float flatBonus = 0f;

        [Tooltip("체크 해제 시 이 엔트리는 자동 누적 계산에서 제외됨 => 이 속성 활성화 하기 싫으면 false하면 됨")]
        public bool enabled = true;
    }

    [CreateAssetMenu(menuName = "Combat/Element BuildUp Formula Profile")]
    public sealed class ElementBuildUpFormulaProfile : ScriptableObject
    {
        [Header("Soft Cap Formula")]
        public float baseValue = 10f;
        public float maxCap = 60f;
        public float curveConstant = 25f;

        [Header("Target State Multiplier")]
        public GameplayTag groggyTag;
        public float groggyMultiplier = 2f;

        [Header("Entries")]
        public ElementBuildUpFormulaEntry[] formulas;
    }
    
}
