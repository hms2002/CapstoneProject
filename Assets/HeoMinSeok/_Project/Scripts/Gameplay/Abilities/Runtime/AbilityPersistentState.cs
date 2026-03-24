using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - Ability 하나의 영속 런타임 상태를 저장/복원하기 위한 DTO다.
    /// - 씬 이동, 무기 드롭/픽업 등 소유자가 바뀌는 경로에서 공통으로 사용한다.
    /// </summary>
    [Serializable]
    public sealed class AbilityPersistentState
    {
        public string abilityId;
        public int level;
        public float cooldownRemaining;
        public int chargesRemaining;

        public List<AbilityIntStateEntry> intVars = new();
        public List<AbilityFloatStateEntry> floatVars = new();
        public List<AbilityBoolStateEntry> boolVars = new();
    }

    /// <summary>
    /// 책임 :
    /// - Ability 영속 상태의 int 값 1개를 직렬화 가능한 형태로 담는다.
    /// </summary>
    [Serializable]
    public sealed class AbilityIntStateEntry
    {
        public string key;
        public int value;
    }

    /// <summary>
    /// 책임 :
    /// - Ability 영속 상태의 float 값 1개를 직렬화 가능한 형태로 담는다.
    /// </summary>
    [Serializable]
    public sealed class AbilityFloatStateEntry
    {
        public string key;
        public float value;
    }

    /// <summary>
    /// 책임 :
    /// - Ability 영속 상태의 bool 값 1개를 직렬화 가능한 형태로 담는다.
    /// </summary>
    [Serializable]
    public sealed class AbilityBoolStateEntry
    {
        public string key;
        public bool value;
    }
}