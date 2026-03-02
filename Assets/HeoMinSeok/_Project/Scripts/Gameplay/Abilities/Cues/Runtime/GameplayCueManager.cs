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

        // ... (CueKey, ActiveCueInstance 구조체는 기존 유지) ...
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
            // Debug.Log($"[GameplayCueManager] 인덱스 빌드 완료. ({defByTagId.Count}개)");
        }

        // ----------------------------------------------------------------
        // [핵심 변경] 모든 요청 메서드 첫 줄에 초기화 체크 추가
        // ----------------------------------------------------------------

        public bool HasCue(GameplayTag tag)
        {
            if (!isIndexBuilt) RebuildIndex(); // <--- 늦은 초기화
            int id = GetTagKey(tag);
            return id >= 0 && defByTagId.ContainsKey(id);
        }

        public void ExecuteCue(GameplayTag tag, GameplayCueParams p)
        {
            if (!isIndexBuilt) RebuildIndex(); // <--- 늦은 초기화

            int id = GetTagKey(tag);
            // ID 조회 실패 시 리스트 직접 검색 (안전장치)
            if (id < 0 || !defByTagId.ContainsKey(id))
            {
                var fallbackDef = FindDefinitionFallback(tag);
                if (fallbackDef != null)
                {
                    SpawnAndNotifyExecute(fallbackDef, p);
                    return;
                }
            }

            if (id >= 0 && defByTagId.TryGetValue(id, out var def))
            {
                SpawnAndNotifyExecute(def, p);
            }
        }

        public void AddCue(GameplayTag tag, GameplayCueParams p)
        {
            // [핵심] 누가 Start보다 빨리 불렀다면, 지금 즉시 초기화한다.
            if (!isIndexBuilt) RebuildIndex();

            int id = GetTagKey(tag);
            GameplayCueDefinition def = null;

            // 1. 딕셔너리 검색
            if (id >= 0) defByTagId.TryGetValue(id, out def);

            // 2. 실패 시 리스트 직접 검색 (Fallback)
            if (def == null)
            {
                def = FindDefinitionFallback(tag);
                // 찾았으면 다음을 위해 ID 갱신 시도
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

            if (active.TryGetValue(key, out var existing) && existing?.Instance != null)
            {
                existing.Notify?.OnRefresh(p);
                return;
            }

            var inst = SpawnInstance(def, p, isForAdd: true);
            if (inst == null)
            {
                // 인스턴스 생성 실패는 로그 띄우지 않고 조용히 리턴 (Prefab이 없는 경우 등)
                return;
            }

            active[key] = inst;
            inst.Notify?.OnAdd(p);
        }

        public void RemoveCue(GameplayTag tag, GameplayCueParams p)
        {
            if (!isIndexBuilt) RebuildIndex(); // <--- 늦은 초기화

            int id = GetTagKey(tag);
            GameplayCueDefinition def = null;

            if (id >= 0) defByTagId.TryGetValue(id, out def);
            if (def == null) def = FindDefinitionFallback(tag);

            if (def == null || !def.isPersistent) return;

            int safeId = (id >= 0) ? id : tag.GetInstanceID();
            var key = CueKey.Make(safeId, p.Target, def.uniquePerTarget ? null : p.SourceObject);

            if (!active.TryGetValue(key, out var inst) || inst == null) return;

            inst.Notify?.OnRemove(p);

            if (inst.Instance != null)
                Destroy(inst.Instance);

            active.Remove(key);
        }

        // [헬퍼] 리스트 직접 검색 (ID 시스템 고장 대비)
        private GameplayCueDefinition FindDefinitionFallback(GameplayTag tag)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] != null && definitions[i].cueTag == tag)
                    return definitions[i];
            }
            return null;
        }

        // -------------------------
        // Internals (기존 유지)
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