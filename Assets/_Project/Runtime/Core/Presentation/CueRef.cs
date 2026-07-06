using System;
using UnityEngine;

namespace CapstonePresentation
{
    /// <summary>
    /// 책임: 런타임 코드와 에셋이 구체 cue 에셋 참조 대신 catalog key로 presentation cue를 가리키게 한다.
    /// </summary>
    [Serializable]
    public struct CueRef
    {
        public string key;

        public bool IsSet => !string.IsNullOrWhiteSpace(key);

        public static CueRef FromKey(string keyValue)
        {
            return new CueRef
            {
                key = keyValue
            };
        }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(key) ? "<empty cue>" : key;
        }
    }
}
