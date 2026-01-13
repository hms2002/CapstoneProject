using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    public class TagSystem : MonoBehaviour
    {
        private readonly HashSet<GameplayTag> activeTags = new HashSet<GameplayTag>();

        public void AddTag(GameplayTag tag)
        {
            activeTags.Add(tag);
        }

        public void AddTags(IEnumerable<GameplayTag> tags)
        {
            foreach (var tag in tags)
            {
                activeTags.Add(tag);
            }
        }

        public void RemoveTag(GameplayTag tag)
        {
            activeTags.Remove(tag);
        }

        public void RemoveTags(IEnumerable<GameplayTag> tags)
        {
            foreach (var tag in tags)
            {
                activeTags.Remove(tag);
            }
        }

        public void RemoveAllTags()
        {
            activeTags.Clear();
        }

        public bool HasTag(GameplayTag tag)
        {
            return activeTags.Contains(tag);
        }

        public bool HasAllTags(IEnumerable<GameplayTag> tags)
        {
            foreach (var tag in tags)
            {
                if (!activeTags.Contains(tag))
                {
                    return false;
                }
            }
            return true;
        }

        public bool HasAnyTag(IEnumerable<GameplayTag> tags)
        {
            foreach (var tag in tags)
            {
                if (activeTags.Contains(tag))
                {
                    return true;
                }
            }
            return false;
        }
    }
}