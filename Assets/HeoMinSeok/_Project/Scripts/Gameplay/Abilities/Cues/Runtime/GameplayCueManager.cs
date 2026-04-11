using System;
using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnityGAS
{
    public class GameplayCueManager : MonoBehaviour
    {
        [SerializeField] private GameplayCueDatabase cueDatabase;
        [FormerlySerializedAs("definitions")]
        [SerializeField, HideInInspector] private List<GameplayCueDefinition> legacyDefinitions = new();

        private readonly Dictionary<int, GameplayCueDefinition> defByTagId = new();
        private readonly Dictionary<CueKey, ActiveCueInstance> active = new();
        private readonly HashSet<string> warnedMissingDefinitionKeys = new();

        // cueNotifyHostPrefab?먯꽌 Notify ??낆쓣 戮묒븘?ㅻ뒗 鍮꾩슜??以꾩씠湲??꾪븳 罹먯떆
        private readonly Dictionary<int, System.Type> notifyTypeCacheByDefId = new();

        // 珥덇린???щ?瑜?泥댄겕?섎뒗 ?뚮옒洹?
        private bool isIndexBuilt = false;
        private bool hasWarnedAboutLegacyDefinitions = false;

        // ----------------------------------------------------------------
        // [ID 議고쉶 ?ы띁]
        // ----------------------------------------------------------------
        private static int GetTagKey(GameplayTag tag)
        {
            if (tag == null) return -1;
            try
            {
                // TagRegistry媛 珥덇린?????먯쑝硫?媛뺤젣 珥덇린??
                TagRegistry.EnsureInitialized();
                return TagRegistry.GetIdByPath(tag.name);
            }
            catch
            {
                return -1;
            }
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

            // Spawned prefab instance (SpawnPrefab 寃쎈줈)
            public GameObject Instance;

            // Notify component (SpawnPrefab 寃쎈줈???몄뒪?댁뒪 ?대?, ?먮뒗 TargetNotify 寃쎈줈濡?Target??吏곸젒 遺숈씤 而댄룷?뚰듃)
            public GameplayCueNotify Notify;

            // Target??AddComponent濡??앹꽦?덈뒗吏(= Remove ??Destroy ???
            public bool CreatedNotifyOnTarget;

            // TransformOnly runtime
            public GameplayCueTransformStack TransformStack;
            public int TransformLayerKey;
            public AudioHandle AudioLoopHandle;
            public bool AudioOnly;

            public bool HasRuntime => Instance != null
                                      || Notify != null
                                      || TransformStack != null
                                      || AudioLoopHandle.IsValid
                                      || AudioOnly;
        }

        private void Awake()
        {
            // Awake?먯꽌??媛뺤젣濡??섏? ?딆쓬 (TagRegistry ?섏〈??臾몄젣 ?뚰뵾)
        }

        private void Start()
        {
            // 寃뚯엫 ?쒖옉 ?쒖젏源뚯? ?꾨Т????遺덈??쇰㈃, ?댁젣 珥덇린??
            if (!isIndexBuilt)
            {
                RebuildIndex();
            }
        }

        // [?듭떖] ?몃뜳??鍮뚮뱶 ?⑥닔
        public void RebuildIndex()
        {
            defByTagId.Clear();
            warnedMissingDefinitionKeys.Clear();
            TagRegistry.EnsureInitialized(); // ?쒓렇 ?쒖뒪??以鍮?

            IReadOnlyList<GameplayCueDefinition> definitions = GetDefinitions();
            for (int i = 0; i < definitions.Count; i++)
            {
                var d = definitions[i];
                if (d == null || d.cueTag == null) continue;

                int id = GetTagKey(d.cueTag);
                if (id >= 0)
                {
                    defByTagId[id] = d;
                }
            }

            isIndexBuilt = true;
            Debug.Log($"[GameplayCueManager] Cue index rebuilt. ({defByTagId.Count} definitions)");
        }
        // ----------------------------------------------------------------
        // Public API
        // ----------------------------------------------------------------

        public bool HasCue(GameplayTag tag)
        {
            if (!isIndexBuilt) RebuildIndex();
            int id = GetTagKey(tag);
            return id >= 0 && defByTagId.ContainsKey(id);
        }

        public void ExecuteCue(GameplayTag tag, GameplayCueParams p)
        {
            if (!isIndexBuilt) RebuildIndex();

            int id = GetTagKey(tag);

            // ID 議고쉶 ?ㅽ뙣 ??由ъ뒪??吏곸젒 寃??(?덉쟾?μ튂)
            if (id < 0 || !defByTagId.ContainsKey(id))
            {
                var fallbackDef = FindDefinitionFallback(tag);
                if (fallbackDef != null)
                {
                    SpawnAndNotifyExecute(fallbackDef, p, layerKey: MakeLayerKey(fallbackDef, id, p, isPersistent: false));
                    return;
                }
            }

            if (id >= 0 && defByTagId.TryGetValue(id, out var def))
            {
                SpawnAndNotifyExecute(def, p, layerKey: MakeLayerKey(def, id, p, isPersistent: false));
                return;
            }

            WarnMissingDefinition(tag, "Execute");
        }

        public void AddCue(GameplayTag tag, GameplayCueParams p)
        {
            if (!isIndexBuilt) RebuildIndex();

            int id = GetTagKey(tag);
            GameplayCueDefinition def = null;

            // 1) Look up the indexed definition first.
            if (id >= 0) defByTagId.TryGetValue(id, out def);

            // 2) Fall back to a direct definition scan.
            if (def == null)
            {
                def = FindDefinitionFallback(tag);
                if (def != null && id >= 0 && !defByTagId.ContainsKey(id))
                    defByTagId[id] = def;
            }

            if (def == null)
            {
                Debug.LogError($"[GameplayCueManager] Missing definition for cue tag: {tag?.name}. Check the cue database registration.");
                return;
            }

            if (!def.isPersistent)
            {
                ExecuteCue(tag, p);
                return;
            }

            GameObject target = p.Target;
            int safeId = (id >= 0) ? id : tag.GetInstanceID();

            var key = CueKey.Make(safeId, target, p.SourceObject);
            if (def.uniquePerTarget)
                key = CueKey.Make(safeId, target, null);

            if (active.TryGetValue(key, out var existing) && existing != null && existing.HasRuntime)
            {
                existing.Notify?.OnRefresh(p);
                EnsureCueLoopAudio(existing, p);

                // Keep the transform contribution fresh when a persistent cue is refreshed.
                if (existing.TransformStack != null)
                {
                    var c = MakeTransformContribution(existing.Def);
                    existing.TransformStack.AddOrUpdate(existing.TransformLayerKey, c);
                }

                return;
            }

            int layerKey = key.GetHashCode();
            var inst = SpawnInstance(def, p, isForAdd: true, layerKey: layerKey);
            if (inst == null) return;

            active[key] = inst;
            EnsureCueLoopAudio(inst, p);
            inst.Notify?.OnAdd(p);
        }

        public void RemoveCue(GameplayTag tag, GameplayCueParams p)
        {
            if (!isIndexBuilt) RebuildIndex();

            int id = GetTagKey(tag);
            GameplayCueDefinition def = null;

            if (id >= 0) defByTagId.TryGetValue(id, out def);
            if (def == null) def = FindDefinitionFallback(tag);

            if (def == null || !def.isPersistent) return;

            int safeId = (id >= 0) ? id : tag.GetInstanceID();
            var key = CueKey.Make(safeId, p.Target, def.uniquePerTarget ? null : p.SourceObject);

            if (!active.TryGetValue(key, out var inst) || inst == null) return;

            inst.Notify?.OnRemove(p);
            StopCueLoopAudio(inst);
            PlayCueRemoveAudio(def, p);

            // Release TransformOnly state.
            if (inst.TransformStack != null)
            {
                inst.TransformStack.Remove(inst.TransformLayerKey);
            }

            // Remove a notify that was created directly on the target for this cue.
            if (inst.CreatedNotifyOnTarget && inst.Notify != null)
                Destroy(inst.Notify);

            if (inst.Instance != null)
                Destroy(inst.Instance);

            active.Remove(key);
        }

        // ----------------------------------------------------------------
        // Internals
        // ----------------------------------------------------------------

        private GameplayCueDefinition FindDefinitionFallback(GameplayTag tag)
        {
            IReadOnlyList<GameplayCueDefinition> definitions = GetDefinitions();
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] != null && definitions[i].cueTag == tag)
                    return definitions[i];
            }
            return null;
        }

        private void WarnMissingDefinition(GameplayTag tag, string phase)
        {
            string tagName = tag != null ? tag.name : "<null>";
            string warningKey = $"{phase}:{tagName}";
            if (!warnedMissingDefinitionKeys.Add(warningKey))
                return;

            Debug.LogWarning(
                $"[GameplayCueManager] Missing cue definition for {phase}: {tagName}. " +
                "Check the GameplayCueDatabase asset and confirm the cue definition is registered.");
        }

        private IReadOnlyList<GameplayCueDefinition> GetDefinitions()
        {
            GameplayCueDatabase database = cueDatabase != null ? cueDatabase : GameplayCueDatabase.LoadDefault();
            if (database != null)
            {
                cueDatabase = database;
                return database.Definitions ?? Array.Empty<GameplayCueDefinition>();
            }

            if (!hasWarnedAboutLegacyDefinitions && legacyDefinitions.Count > 0)
            {
                hasWarnedAboutLegacyDefinitions = true;
                Debug.LogWarning("[GameplayCueManager] GameplayCueDatabase was not assigned, so legacy scene definitions are being used as a fallback.");
            }

            return legacyDefinitions;
        }
        private void SpawnAndNotifyExecute(GameplayCueDefinition def, GameplayCueParams p, int layerKey)
        {
            var inst = SpawnInstance(def, p, isForAdd: false, layerKey: layerKey);
            PlayCueExecuteAudio(def, p);
            if (inst == null) return;

            inst.Notify?.OnExecute(p);

            // Execute-time TransformOnly cues are removed after their configured duration.
            if (inst.TransformStack != null)
            {
                float dur = def.transformExecuteDuration;
                StartCoroutine(RemoveTransformAfter(inst.TransformStack, inst.TransformLayerKey, dur));
            }

            // Execute-time target notifies are transient, so remove them right away.
            // If a notify needs lifecycle callbacks over time, prefer Add/Remove based cues.
            if (inst.CreatedNotifyOnTarget && inst.Notify != null)
                Destroy(inst.Notify);

            if (inst.Instance != null && def.autoDestroySeconds > 0f)
            {
                if (inst.Notify is not GameplayCue_HitSparkParticles)
                    Destroy(inst.Instance, def.autoDestroySeconds);
            }
        }

        private IEnumerator RemoveTransformAfter(GameplayCueTransformStack stack, int layerKey, float duration)
        {
            if (stack == null) yield break;

            if (duration <= 0f)
                yield return null; // Apply for one frame.
            else
                yield return new WaitForSeconds(duration);

            if (stack != null)
                stack.Remove(layerKey);
        }

        private ActiveCueInstance SpawnInstance(GameplayCueDefinition def, GameplayCueParams p, bool isForAdd, int layerKey)
        {
            var result = new ActiveCueInstance
            {
                Def = def,
                AudioOnly = HasAudioRuntime(def, isForAdd)
            };

            switch (def.mode)
            {
                case GameplayCueDefinition.ExecutionMode.TransformOnly:
                {
                    if (p.Target == null) return result.AudioOnly ? result : null;

                    var stack = p.Target.GetComponent<GameplayCueTransformStack>();
                    if (stack == null) stack = p.Target.AddComponent<GameplayCueTransformStack>();

                    var c = MakeTransformContribution(def);
                    stack.AddOrUpdate(layerKey, c);

                    result.TransformStack = stack;
                    result.TransformLayerKey = layerKey;
                    return result;
                }

                case GameplayCueDefinition.ExecutionMode.TargetNotify:
                {
                    if (def.cueNotifyHostPrefab == null || p.Target == null)
                        return result.AudioOnly ? result : null;

                    var type = GetOrCacheNotifyType(def);
                    if (type == null)
                    {
                        Debug.LogWarning($"[GameplayCueManager] cueNotifyHostPrefab does not contain a GameplayCueNotify: {def.name}");
                        return result.AudioOnly ? result : null;
                    }

                    // Reuse an existing notify to avoid duplicate AddComponent calls.
                    var existing = p.Target.GetComponent(type) as GameplayCueNotify;
                    if (existing != null)
                    {
                        result.Notify = existing;
                        result.CreatedNotifyOnTarget = false;
                        return result;
                    }

                    var added = p.Target.AddComponent(type) as GameplayCueNotify;
                    if (added == null)
                    {
                        Debug.LogWarning($"[GameplayCueManager] Failed to add target notify component: {type.FullName}");
                        return result.AudioOnly ? result : null;
                    }

                    result.Notify = added;
                    result.CreatedNotifyOnTarget = true;
                    return result;
                }

                case GameplayCueDefinition.ExecutionMode.SpawnPrefab:
                default:
                {
                    // 1) Prefab Notify 寃쎈줈 (Instantiate)
                    if (def.cuePrefab != null)
                    {
                        var go = AcquireCuePrefabInstance(def.cuePrefab);
                        result.Instance = go;
                        result.Notify = go.GetComponentInChildren<GameplayCueNotify>();
                        if (!Place(go.transform, def, p))
                        {
                            ReleaseCuePrefabInstance(go, result.Notify);
                            result.Instance = null;
                            result.Notify = null;
                            return result.AudioOnly ? result : null;
                        }
                        return result;
                    }

                    // 2) VFX/SFX Fallback
                    if (def.vfxPrefab != null)
                    {
                        var go = Instantiate(def.vfxPrefab);
                        result.Instance = go;
                        if (!Place(go.transform, def, p))
                        {
                            Destroy(go);
                            result.Instance = null;
                            return result.AudioOnly ? result : null;
                        }

                        if (!isForAdd && def.autoDestroySeconds > 0f)
                            Destroy(go, def.autoDestroySeconds);
                    }
                    return result.HasRuntime ? result : null;
                }
            }
        }

        private static GameplayCueTransformStack.Contribution MakeTransformContribution(GameplayCueDefinition def)
        {
            return new GameplayCueTransformStack.Contribution
            {
                AddLocalPos = def.addLocalPosition,
                AddLocalEuler = def.addLocalEuler,
                MulLocalScale = (def.mulLocalScale == Vector3.zero) ? Vector3.one : def.mulLocalScale
            };
        }

        /// <summary>
        /// 책임 : cue prefab이 자체 풀링을 지원하면 재사용 인스턴스를 우선 받고, 아니면 새 인스턴스를 생성한다.
        /// </summary>
        private static GameObject AcquireCuePrefabInstance(GameObject cuePrefab)
        {
            if (cuePrefab != null && cuePrefab.GetComponent<GameplayCue_HitSparkParticles>() != null)
                return GameplayCue_HitSparkParticles.AcquireInstance(cuePrefab);

            return Instantiate(cuePrefab);
        }

        /// <summary>
        /// 책임 : cue 배치에 실패한 인스턴스를 적절한 수명 정책으로 정리한다.
        /// HitSpark처럼 자체 풀링을 쓰는 경우에는 비활성화만 하여 풀로 되돌린다.
        /// </summary>
        private static void ReleaseCuePrefabInstance(GameObject instance, GameplayCueNotify notify)
        {
            if (instance == null)
                return;

            if (notify is GameplayCue_HitSparkParticles)
            {
                instance.SetActive(false);
                return;
            }

            Destroy(instance);
        }

        private System.Type GetOrCacheNotifyType(GameplayCueDefinition def)
        {
            if (def == null || def.cueNotifyHostPrefab == null) return null;

            int key = def.GetInstanceID();
            if (notifyTypeCacheByDefId.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var notify = def.cueNotifyHostPrefab.GetComponentInChildren<GameplayCueNotify>(true);
            var type = notify != null ? notify.GetType() : null;

            notifyTypeCacheByDefId[key] = type;
            return type;
        }

        private static int MakeLayerKey(GameplayCueDefinition def, int tagId, GameplayCueParams p, bool isPersistent)
        {
            unchecked
            {
                // Execute??留ㅻ쾲 ?덈줈 ?곸슜/?댁젣媛 ?꾩슂?섎?濡?sourceObject源뚯? ?ы븿?댁꽌 ?ㅻ? 留뚮뱺??
                int safeTag = (tagId >= 0) ? tagId : (def != null && def.cueTag != null ? def.cueTag.GetInstanceID() : 0);
                int targetId = p.Target != null ? p.Target.GetInstanceID() : 0;
                int sourceId = (isPersistent || def == null || def.uniquePerTarget) ? 0 : (p.SourceObject != null ? p.SourceObject.GetInstanceID() : 0);

                int h = 17;
                h = h * 31 + safeTag;
                h = h * 31 + targetId;
                h = h * 31 + sourceId;
                return h;
            }
        }

        private bool Place(Transform t, GameplayCueDefinition def, GameplayCueParams p)
        {
            if (t == null)
                return false;

            if (!TryResolveAnchorWorldPosition(def, p, out Vector3 anchorWorld))
                return false;

            Vector3 worldOffset = ResolveWorldOffset(def, p);

            if (def.attachToTarget && p.Target != null)
            {
                Transform targetTransform = p.Target.transform;
                Vector3 localAnchor = targetTransform.InverseTransformPoint(anchorWorld);
                Vector3 localOffset = def.applyOffsetInTargetLocalSpace
                    ? def.localOffset
                    : targetTransform.InverseTransformVector(worldOffset);

                t.SetParent(p.Target.transform, worldPositionStays: false);
                t.localPosition = localAnchor + localOffset;
                t.localRotation = Quaternion.identity;
            }
            else
            {
                Vector3 worldPosition = anchorWorld + worldOffset;
                t.SetParent(null);
                t.position = worldPosition;
                t.rotation = Quaternion.identity;
            }

            return true;
        }

        private static Vector3 ResolveWorldOffset(GameplayCueDefinition def, GameplayCueParams p)
        {
            if (p.Target == null || !def.applyOffsetInTargetLocalSpace)
                return def.localOffset;

            return p.Target.transform.TransformVector(def.localOffset);
        }

        private static bool TryResolveAnchorWorldPosition(GameplayCueDefinition def, GameplayCueParams p, out Vector3 anchorWorld)
        {
            if (p.Target == null)
            {
                if (p.HasExplicitPosition)
                {
                    anchorWorld = p.Position;
                    return true;
                }

                anchorWorld = default;
                return false;
            }

            if (def.useExplicitHitPoint && p.HasExplicitPosition)
            {
                anchorWorld = p.Position;
                return true;
            }

            switch (def.spawnAnchorPolicy)
            {
                case GameplayCueDefinition.SpawnAnchorPolicy.TargetPivot:
                    anchorWorld = p.Target.transform.position;
                    return true;

                case GameplayCueDefinition.SpawnAnchorPolicy.TargetSpriteCenter:
                    if (TryGetCombinedSpriteBounds(p.Target, out Bounds spriteBounds))
                    {
                        anchorWorld = spriteBounds.center;
                        return true;
                    }
                    break;

                case GameplayCueDefinition.SpawnAnchorPolicy.TargetSpriteTop:
                    if (TryGetCombinedSpriteBounds(p.Target, out Bounds topBounds))
                    {
                        anchorWorld = new Vector3(topBounds.center.x, topBounds.max.y, topBounds.center.z);
                        return true;
                    }
                    break;

                case GameplayCueDefinition.SpawnAnchorPolicy.TargetSpriteBottom:
                    if (TryGetCombinedSpriteBounds(p.Target, out Bounds bottomBounds))
                    {
                        anchorWorld = new Vector3(bottomBounds.center.x, bottomBounds.min.y, bottomBounds.center.z);
                        return true;
                    }
                    break;

                case GameplayCueDefinition.SpawnAnchorPolicy.TargetColliderCenter:
                    if (TryGetCombinedColliderBounds(p.Target, out Bounds colliderBounds))
                    {
                        anchorWorld = colliderBounds.center;
                        return true;
                    }
                    break;
            }

            anchorWorld = default;
            return false;
        }

        private static bool TryGetCombinedSpriteBounds(GameObject target, out Bounds bounds)
        {
            SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>(includeInactive: false);
            if (renderers != null && renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                return true;
            }

            bounds = default;
            return false;
        }

        private static bool TryGetCombinedColliderBounds(GameObject target, out Bounds bounds)
        {
            Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>(includeInactive: false);
            if (colliders != null && colliders.Length > 0)
            {
                bounds = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++)
                    bounds.Encapsulate(colliders[i].bounds);
                return true;
            }

            bounds = default;
            return false;
        }

        private static bool HasAudioRuntime(GameplayCueDefinition def, bool isForAdd)
        {
            if (def == null)
                return false;

            return isForAdd
                ? def.audioWhileActive.IsSet || def.audioOnRemove.IsSet
                : def.audioOnExecute.IsSet;
        }

        private static SoundPlaybackContext BuildSoundContext(GameplayCueParams p)
        {
            return new SoundPlaybackContext
            {
                Instigator = p.Instigator,
                Causer = p.Causer,
                Target = p.Target,
                Position = p.Position,
                SourceObject = p.SourceObject
            };
        }

        private static void PlayCueExecuteAudio(GameplayCueDefinition def, GameplayCueParams p)
        {
            if (def == null || !def.audioOnExecute.IsSet)
                return;

            SoundManager.EnsureInstance().Play(def.audioOnExecute, BuildSoundContext(p));
        }

        private static void PlayCueRemoveAudio(GameplayCueDefinition def, GameplayCueParams p)
        {
            if (def == null || !def.audioOnRemove.IsSet)
                return;

            SoundManager.EnsureInstance().Play(def.audioOnRemove, BuildSoundContext(p));
        }

        private static void EnsureCueLoopAudio(ActiveCueInstance inst, GameplayCueParams p)
        {
            if (inst == null || inst.Def == null || !inst.Def.audioWhileActive.IsSet)
                return;

            SoundManager manager = SoundManager.EnsureInstance();
            if (manager.IsPlaying(inst.AudioLoopHandle))
                return;

            inst.AudioLoopHandle = manager.Play(inst.Def.audioWhileActive, BuildSoundContext(p));
        }

        private static void StopCueLoopAudio(ActiveCueInstance inst)
        {
            if (inst == null || !inst.AudioLoopHandle.IsValid)
                return;

            SoundManager.EnsureInstance().Stop(inst.AudioLoopHandle);
            inst.AudioLoopHandle = AudioHandle.Invalid;
        }
    }
}


