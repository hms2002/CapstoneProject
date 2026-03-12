using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    public class GameplayCueManager : MonoBehaviour
    {
        [SerializeField] private List<GameplayCueDefinition> definitions = new();

        private readonly Dictionary<int, GameplayCueDefinition> defByTagId = new();
        private readonly Dictionary<CueKey, ActiveCueInstance> active = new();

        // cueNotifyHostPrefab에서 Notify 타입을 뽑아오는 비용을 줄이기 위한 캐시
        private readonly Dictionary<int, System.Type> notifyTypeCacheByDefId = new();

        // 초기화 여부를 체크하는 플래그
        private bool isIndexBuilt = false;

        // ----------------------------------------------------------------
        // [ID 조회 헬퍼]
        // ----------------------------------------------------------------
        private static int GetTagKey(GameplayTag tag)
        {
            if (tag == null) return -1;
            try
            {
                // TagRegistry가 초기화 안 됐으면 강제 초기화
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

            // Spawned prefab instance (SpawnPrefab 경로)
            public GameObject Instance;

            // Notify component (SpawnPrefab 경로의 인스턴스 내부, 또는 TargetNotify 경로로 Target에 직접 붙인 컴포넌트)
            public GameplayCueNotify Notify;

            // Target에 AddComponent로 생성했는지(= Remove 시 Destroy 대상)
            public bool CreatedNotifyOnTarget;

            // TransformOnly runtime
            public GameplayCueTransformStack TransformStack;
            public int TransformLayerKey;

            public bool HasRuntime => Instance != null || Notify != null || TransformStack != null;
        }

        private void Awake()
        {
            // Awake에서는 강제로 하지 않음 (TagRegistry 의존성 문제 회피)
        }

        private void Start()
        {
            // 게임 시작 시점까지 아무도 안 불렀으면, 이제 초기화
            if (!isIndexBuilt)
            {
                RebuildIndex();
            }
        }

        // [핵심] 인덱스 빌드 함수
        public void RebuildIndex()
        {
            defByTagId.Clear();
            TagRegistry.EnsureInitialized(); // 태그 시스템 준비

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

            isIndexBuilt = true; // 초기화 완료 표시
            Debug.Log($"[GameplayCueManager] 인덱스 빌드 완료. ({defByTagId.Count}개)");
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

            // ID 조회 실패 시 리스트 직접 검색 (안전장치)
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
            }
        }

        public void AddCue(GameplayTag tag, GameplayCueParams p)
        {
            if (!isIndexBuilt) RebuildIndex();

            int id = GetTagKey(tag);
            GameplayCueDefinition def = null;

            // 1) 딕셔너리 검색
            if (id >= 0) defByTagId.TryGetValue(id, out def);

            // 2) 실패 시 리스트 직접 검색 (Fallback)
            if (def == null)
            {
                def = FindDefinitionFallback(tag);
                if (def != null && id >= 0 && !defByTagId.ContainsKey(id))
                    defByTagId[id] = def;
            }

            if (def == null)
            {
                Debug.LogError($"[Manager] 정의(Definition)를 찾을 수 없음: {tag?.name}. Manager 리스트를 확인하세요.");
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

                // TransformOnly는 Refresh 시에도 contribution을 갱신할 수 있게 한다.
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

            // TransformOnly 해제
            if (inst.TransformStack != null)
            {
                inst.TransformStack.Remove(inst.TransformLayerKey);
            }

            // Target에 붙인 Notify는 Remove 시 제거(생성한 경우에만)
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
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] != null && definitions[i].cueTag == tag)
                    return definitions[i];
            }
            return null;
        }

        private void SpawnAndNotifyExecute(GameplayCueDefinition def, GameplayCueParams p, int layerKey)
        {
            var inst = SpawnInstance(def, p, isForAdd: false, layerKey: layerKey);
            if (inst == null) return;

            inst.Notify?.OnExecute(p);

            // TransformOnly Execute는 duration 후 자동 해제
            if (inst.TransformStack != null)
            {
                float dur = def.transformExecuteDuration;
                StartCoroutine(RemoveTransformAfter(inst.TransformStack, inst.TransformLayerKey, dur));
            }

            // Execute에서 Target에 임시로 붙인 Notify는 바로 제거한다.
            // (주의) Notify가 시간에 걸친 연출을 코루틴/트윈으로 돌린다면, Execute 대신 Add/Remove 기반으로 쓰는 것을 권장.
            if (inst.CreatedNotifyOnTarget && inst.Notify != null)
                Destroy(inst.Notify);

            if (inst.Instance != null && def.autoDestroySeconds > 0f)
                Destroy(inst.Instance, def.autoDestroySeconds);
        }

        private IEnumerator RemoveTransformAfter(GameplayCueTransformStack stack, int layerKey, float duration)
        {
            if (stack == null) yield break;

            if (duration <= 0f)
                yield return null; // 1프레임만 적용
            else
                yield return new WaitForSeconds(duration);

            if (stack != null)
                stack.Remove(layerKey);
        }

        private ActiveCueInstance SpawnInstance(GameplayCueDefinition def, GameplayCueParams p, bool isForAdd, int layerKey)
        {
            var result = new ActiveCueInstance { Def = def };

            switch (def.mode)
            {
                case GameplayCueDefinition.ExecutionMode.TransformOnly:
                {
                    if (p.Target == null) return null;

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
                        return null;

                    var type = GetOrCacheNotifyType(def);
                    if (type == null)
                    {
                        Debug.LogWarning($"[GameplayCueManager] cueNotifyHostPrefab에 GameplayCueNotify가 없습니다: {def.name}");
                        return null;
                    }

                    // 이미 붙어있으면 재사용 (중복 AddComponent 방지)
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
                        Debug.LogWarning($"[GameplayCueManager] Target에 Notify AddComponent 실패: {type.FullName}");
                        return null;
                    }

                    result.Notify = added;
                    result.CreatedNotifyOnTarget = true;
                    return result;
                }

                case GameplayCueDefinition.ExecutionMode.SpawnPrefab:
                default:
                {
                    // 1) Prefab Notify 경로 (Instantiate)
                    if (def.cuePrefab != null)
                    {
                        var go = Instantiate(def.cuePrefab);
                        result.Instance = go;
                        result.Notify = go.GetComponentInChildren<GameplayCueNotify>();
                        Place(go.transform, def, p);
                        return result;
                    }

                    // 2) VFX/SFX Fallback
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

                    return result.Instance != null || result.Notify != null ? result : null;
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
                // Execute는 매번 새로 적용/해제가 필요하므로 sourceObject까지 포함해서 키를 만든다.
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
