using System;
using UnityEngine;

namespace CapstonePresentation
{
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
