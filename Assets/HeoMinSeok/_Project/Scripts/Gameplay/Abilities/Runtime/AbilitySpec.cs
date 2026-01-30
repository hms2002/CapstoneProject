using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 캐릭터(AbilitySystem) 당 AbilityDefinition 1개를 소유할 때 생기는 "런타임 상태"를 담는 그릇.
    /// - Definition(SO)은 불변 데이터
    /// - Spec은 캐릭터별 고유 상태(쿨다운 남은 시간, 레벨, 차지/스택, 임시 런타임 변수 등)
    /// </summary>
    public sealed class AbilitySpec
    {
        public AbilityDefinition Definition { get; }
        public int Level { get; set; }

        /// <summary>남은 쿨다운(초). 0 이하이면 쿨다운 없음.</summary>
        public float CooldownRemaining { get; internal set; }
        public AbilityCancellationToken Token { get; internal set; }
        /// <summary>
        /// GA 전용 런타임 값(최소 구현용 블랙보드).
        /// 필요하면 int/bool 등 다른 타입 딕셔너리도 추가해도 됨.
        /// </summary>
        public readonly Dictionary<string, float> FloatVars = new Dictionary<string, float>();
        public readonly Dictionary<string, int> IntVars = new Dictionary<string, int>();
        public readonly Dictionary<string, bool> BoolVars = new Dictionary<string, bool>();

        public AbilitySpec(AbilityDefinition definition, int level = 1)
        {
            Definition = definition;
            Level = level;
            CooldownRemaining = 0f;
        }

        public override string ToString()
            => Definition != null ? $"{Definition.abilityName} (Lv.{Level})" : "Null AbilitySpec";

        internal bool TryGetFloat(string v, out float outValue)
        {
            return FloatVars.TryGetValue(v, out outValue);
        }

        internal int GetInt(string key, int v)
        {
            if (IntVars.TryGetValue(key, out int value))
            {
                return value;
            }
            else return v;
        }

        internal float GetFloat(string key, float v)
        {
            if (FloatVars.TryGetValue(key, out float value))
            {
                return value;
            }
            else return v;
        }

        internal void SetFloat(string key, float value)
        {
            FloatVars[key] = value;
        }

        internal void SetInt(string key, int value)
        {
            IntVars[key] = value; // ✅
        }

    }
}
