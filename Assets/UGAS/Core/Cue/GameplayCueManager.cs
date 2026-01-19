using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    public class GameplayCueManager : MonoBehaviour
    {
        [SerializeField] private List<GameplayCueDefinition> definitions = new();

        private readonly Dictionary<int, GameplayCueDefinition> defByTagId = new();
        private readonly Dictionary<CueKey, ActiveCueInstance> active = new();

        private static int GetTagKey(GameplayTag tag)
        {
            if (tag == null) return -1;
            // TagRegistry는 현재 tag.name 기반("A.B.C")을 사용
            return TagRegistry.GetIdByPath(tag.name);
        }

        [Serializable]
        private struct CueKey : IEquatable<CueKey>
        {
            public int TagId;
            public int TargetId;
            public int SourceId;

            public bool Equals(CueKey other)
                => TagId == other.TagId && TargetId == other.TargetId && SourceId == other.SourceId;

            public override bool Equals(object obj) => obj is CueKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = 17;
                    h = h * 31 + TagId;
                    h = h * 31 + TargetId;
                    h = h * 31 + SourceId;
                    return h;
                }
            }

            public static CueKey Make(int tagId, GameObject target, UnityEngine.Object sourceObject)
            {
                return new CueKey
                {
                    TagId = tagId,
                    TargetId = target != null ? target.GetInstanceID() : 0,
                    SourceId = sourceObject != null ? sourceObject.GetInstanceID() : 0
                };
            }
        }

        private sealed class ActiveCueInstance
        {
            public GameplayCueDefinition Def;
            public GameObject Instance;
            public GameplayCueNotify Notify;
        }

        private void Awake()
        {
            TagRegistry.EnsureInitialized();
            RebuildIndex();
        }

        private void OnValidate()
        {
            RebuildIndex();
        }

        public void RebuildIndex()
        {
            defByTagId.Clear();
            TagRegistry.EnsureInitialized();

            for (int i = 0; i < definitions.Count; i++)
            {
                var d = definitions[i];
                if (d == null || d.cueTag == null) continue;

                int id = GetTagKey(d.cueTag);
                if (id < 0) continue;

                defByTagId[id] = d;
            }
        }

        public bool HasCue(GameplayTag tag)
        {
            int id = GetTagKey(tag);
            return id >= 0 && defByTagId.ContainsKey(id);
        }

        public void ExecuteCue(GameplayTag tag, GameplayCueParams p)
        {
            int id = GetTagKey(tag);
            if (id < 0) return;

            if (!defByTagId.TryGetValue(id, out var def) || def == null) return;
            SpawnAndNotifyExecute(def, p);
        }

        public void AddCue(GameplayTag tag, GameplayCueParams p)
        {
            int id = GetTagKey(tag);
            if (id < 0) return;

            if (!defByTagId.TryGetValue(id, out var def) || def == null) return;
            if (!def.isPersistent) { ExecuteCue(tag, p); return; }

            GameObject target = p.Target;

            var key = CueKey.Make(id, target, p.SourceObject);
            if (def.uniquePerTarget)
                key = CueKey.Make(id, target, null);

            if (active.TryGetValue(key, out var existing) && existing?.Instance != null)
            {
                existing.Notify?.OnRefresh(p);
                return;
            }

            var inst = SpawnInstance(def, p, isForAdd: true);
            if (inst == null) return;

            active[key] = inst;
            inst.Notify?.OnAdd(p);
        }

        public void RemoveCue(GameplayTag tag, GameplayCueParams p)
        {
            int id = GetTagKey(tag);
            if (id < 0) return;

            if (!defByTagId.TryGetValue(id, out var def) || def == null) return;
            if (!def.isPersistent) return;

            GameObject target = p.Target;

            var key = CueKey.Make(id, target, p.SourceObject);
            if (def.uniquePerTarget)
                key = CueKey.Make(id, target, null);

            if (!active.TryGetValue(key, out var inst) || inst == null) return;

            inst.Notify?.OnRemove(p);

            if (inst.Instance != null)
                Destroy(inst.Instance);

            active.Remove(key);
        }

        // -------------------------
        // Internals
        // -------------------------
        private void SpawnAndNotifyExecute(GameplayCueDefinition def, GameplayCueParams p)
        {
            var inst = SpawnInstance(def, p, isForAdd: false);
            if (inst == null) return;

            inst.Notify?.OnExecute(p);

            if (inst.Instance != null && def.autoDestroySeconds > 0f)
                Destroy(inst.Instance, def.autoDestroySeconds);
        }

        private ActiveCueInstance SpawnInstance(GameplayCueDefinition def, GameplayCueParams p, bool isForAdd)
        {
            var result = new ActiveCueInstance { Def = def };

            if (def.cuePrefab != null)
            {
                var go = Instantiate(def.cuePrefab);
                result.Instance = go;
                result.Notify = go.GetComponentInChildren<GameplayCueNotify>();
                Place(go.transform, def, p);
                return result;
            }

            if (def.vfxPrefab != null)
            {
                var go = Instantiate(def.vfxPrefab);
                result.Instance = go;
                Place(go.transform, def, p);

                if (!isForAdd && def.autoDestroySeconds > 0f)
                    Destroy(go, def.autoDestroySeconds);
            }

            if (def.sfx != null)
                AudioSource.PlayClipAtPoint(def.sfx, p.Position);

            return result.Instance != null ? result : null;
        }

        private void Place(Transform t, GameplayCueDefinition def, GameplayCueParams p)
        {
            if (t == null) return;

            if (def.attachToTarget && p.Target != null)
            {
                t.SetParent(p.Target.transform, worldPositionStays: false);
                t.localPosition = def.localOffset;
                t.localRotation = Quaternion.identity;
            }
            else
            {
                t.SetParent(null);
                t.position = p.Position + def.localOffset;
                t.rotation = Quaternion.identity;
            }
        }
    }
}
