using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    [CreateAssetMenu(fileName = "NewTagSet", menuName = "GAS/Gameplay Tag Set")]
    public class GameplayTagSet : ScriptableObject
    {
        [Header("Direct Tags")]
        public List<GameplayTag> tags = new();

        [Header("Includes (Optional)")]
        public List<GameplayTagSet> includes = new();

        // TagSet이 수정되면 버전이 올라가서, 이를 참조하는 SO들이 "자동으로 재컴파일"되게 함
        [SerializeField, HideInInspector] private int version = 1;
        public int Version => version;

        private void OnValidate()
        {
            if (version <= 0) version = 1;
            else version++;
        }

        public void AddToMask(TagMask mask, HashSet<GameplayTagSet> visited = null)
        {
            if (mask == null) return;
            visited ??= new HashSet<GameplayTagSet>();
            if (!visited.Add(this)) return; // cycle 방지

            if (tags != null)
                for (int i = 0; i < tags.Count; i++)
                    if (tags[i] != null) mask.Add(tags[i]);

            if (includes != null)
                for (int i = 0; i < includes.Count; i++)
                    if (includes[i] != null) includes[i].AddToMask(mask, visited);
        }

        public static int ComputeVersionHash(List<GameplayTagSet> sets)
        {
            unchecked
            {
                int h = 17;
                if (sets == null) return h;
                for (int i = 0; i < sets.Count; i++)
                {
                    var s = sets[i];
                    h = h * 31 + (s != null ? s.Version : 0);
                }
                return h;
            }
        }
        public void CollectTags(HashSet<GameplayTag> outTags, HashSet<GameplayTagSet> visited = null)
        {
            if (outTags == null) return;
            visited ??= new HashSet<GameplayTagSet>();
            if (!visited.Add(this)) return;

            if (tags != null)
                for (int i = 0; i < tags.Count; i++)
                    if (tags[i] != null) outTags.Add(tags[i]);

            if (includes != null)
                for (int i = 0; i < includes.Count; i++)
                    includes[i]?.CollectTags(outTags, visited);
        }

        public bool ContainsTag(GameplayTag tag, HashSet<GameplayTagSet> visited = null)
        {
            if (tag == null) return false;
            visited ??= new HashSet<GameplayTagSet>();
            if (!visited.Add(this)) return false;

            if (tags != null)
                for (int i = 0; i < tags.Count; i++)
                    if (tags[i] == tag) return true;

            if (includes != null)
                for (int i = 0; i < includes.Count; i++)
                    if (includes[i] != null && includes[i].ContainsTag(tag, visited)) return true;

            return false;
        }

    }
}
