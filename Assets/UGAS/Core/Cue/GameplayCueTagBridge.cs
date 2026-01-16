using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    public class GameplayCueTagBridge : MonoBehaviour
    {
        [SerializeField] private GameplayCueManager cueManager;
        [SerializeField] private TagSystem tagSystem;

        [Header("Mirror these tags as persistent cues (Add/Remove)")]
        [SerializeField] private List<GameplayTag> tagsToMirrorAsCues = new List<GameplayTag>();

        private void Awake()
        {
            if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
            if (cueManager == null) cueManager = FindObjectOfType<GameplayCueManager>();

            if (tagSystem != null)
            {
                tagSystem.OnTagAdded += OnTagAdded;
                tagSystem.OnTagRemoved += OnTagRemoved;
            }
        }

        private void OnDestroy()
        {
            if (tagSystem != null)
            {
                tagSystem.OnTagAdded -= OnTagAdded;
                tagSystem.OnTagRemoved -= OnTagRemoved;
            }
        }

        private bool ShouldMirror(GameplayTag tag)
        {
            if (tag == null) return false;
            for (int i = 0; i < tagsToMirrorAsCues.Count; i++)
                if (tagsToMirrorAsCues[i] == tag) return true;
            return false;
        }

        private void OnTagAdded(GameplayTag tag)
        {
            if (!ShouldMirror(tag) || cueManager == null) return;

            var p = GameplayCueParams.FromTarget(gameObject);
            p.Target = gameObject;
            p.Instigator = gameObject;
            p.Causer = gameObject;

            cueManager.AddCue(tag, p);
        }

        private void OnTagRemoved(GameplayTag tag)
        {
            if (!ShouldMirror(tag) || cueManager == null) return;

            var p = GameplayCueParams.FromTarget(gameObject);
            p.Target = gameObject;
            p.Instigator = gameObject;
            p.Causer = gameObject;

            cueManager.RemoveCue(tag, p);
        }
    }
}
