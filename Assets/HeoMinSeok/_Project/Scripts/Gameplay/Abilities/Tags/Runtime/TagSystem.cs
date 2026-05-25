using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityGAS
{
    public class TagSystem : MonoBehaviour
    {
        private int[] _counts;         // 부모 포함 최종 카운트
        private int[] _explicitCounts; // 내가 명시적으로 Add한 카운트(정확한 Remove를 위해)
        private ulong[] _bits;

        public int[] TagCount => _counts;

        public event Action<GameplayTag, int, int> OnTagCountChanged;
        public event Action<GameplayTag> OnTagAdded;
        public event Action<GameplayTag> OnTagRemoved;
        // JUST FOR DEBUG CODE
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void PrintHasTags(string title = null)
        {
            Debug.Log(title);
            for(int i = 1; i < _explicitCounts.Length; i++)
            {
                if (_explicitCounts[i] > 0)
                {
                    Debug.Log(TagRegistry.GetTag(i).Name);
                }
            }
        }
#endif
        private void Awake() => EnsureCapacity();

        private void EnsureCapacity()
        {
            TagRegistry.EnsureInitialized();
            int n = TagRegistry.TagCount;

            if (_counts == null || _counts.Length != n)
            {
                _counts = new int[n];
                _explicitCounts = new int[n];
                _bits = new ulong[TagRegistry.WordCount];
            }
        }

        public void AddTag(GameplayTag tag, int amount = 1)
        {
            EnsureCapacity();
            int id = TagRegistry.GetId(tag);
            if (id < 0 || amount <= 0) return;

            _explicitCounts[id] += amount;

            var closure = TagRegistry.GetClosureIds(id);
            for (int i = 0; i < closure.Length; i++)
            {
                int cid = closure[i];
                int oldCount = _counts[cid];
                int newCount = oldCount + amount;
                _counts[cid] = newCount;

                if (oldCount == 0 && newCount > 0) _bits[cid >> 6] |= 1UL << (cid & 63);

                var ctag = TagRegistry.GetTag(cid);
                OnTagCountChanged?.Invoke(ctag, oldCount, newCount);
                if (oldCount == 0 && newCount > 0) OnTagAdded?.Invoke(ctag);
            }
        }
        // TagSystem.cs 안에 추가
        public void AddTagId(int id, int amount = 1)
        {
            EnsureCapacity();
            if (id <= 0 || amount <= 0) return;

            _explicitCounts[id] += amount;

            var closure = TagRegistry.GetClosureIds(id);
            for (int i = 0; i < closure.Length; i++)
            {
                int cid = closure[i];
                int oldCount = _counts[cid];
                int newCount = oldCount + amount;
                _counts[cid] = newCount;

                if (oldCount == 0 && newCount > 0) _bits[cid >> 6] |= 1UL << (cid & 63);

                var ctag = TagRegistry.GetTag(cid);
                OnTagCountChanged?.Invoke(ctag, oldCount, newCount);
                if (oldCount == 0 && newCount > 0) OnTagAdded?.Invoke(ctag);
            }
        }

        public void RemoveTagId(int id, int amount = 1)
        {
            EnsureCapacity();
            if (id <= 0 || amount <= 0) return;

            int have = _explicitCounts[id];
            if (have <= 0) return;

            if (amount > have) amount = have;
            _explicitCounts[id] = have - amount;

            var closure = TagRegistry.GetClosureIds(id);
            for (int i = 0; i < closure.Length; i++)
            {
                int cid = closure[i];
                int oldCount = _counts[cid];
                int newCount = oldCount - amount;
                if (newCount < 0) newCount = 0;
                _counts[cid] = newCount;

                if (oldCount > 0 && newCount == 0) _bits[cid >> 6] &= ~(1UL << (cid & 63));

                var ctag = TagRegistry.GetTag(cid);
                OnTagCountChanged?.Invoke(ctag, oldCount, newCount);
                if (oldCount > 0 && newCount == 0) OnTagRemoved?.Invoke(ctag);
            }
        }

        public void AddTagByPath(string path, int amount = 1)
            => AddTagId(TagRegistry.GetIdByPath(path), amount);

        public void RemoveTagByPath(string path, int amount = 1)
            => RemoveTagId(TagRegistry.GetIdByPath(path), amount);

        public bool HasTagId(int id)
        {
            EnsureCapacity();
            return id > 0 && id < _counts.Length && _counts[id] > 0;
        }

        public void AddTags(List<GameplayTag> tags)
        {
            foreach (GameplayTag tag in tags) AddTag(tag, 1);
        }        
        public void RemoveTag(GameplayTag tag, int amount = 1)
        {
            EnsureCapacity();
            int id = TagRegistry.GetId(tag);
            if (id < 0 || amount <= 0) return;

            int have = _explicitCounts[id];
            if (have <= 0) return;

            if (amount > have) amount = have;
            _explicitCounts[id] = have - amount;

            var closure = TagRegistry.GetClosureIds(id);
            for (int i = 0; i < closure.Length; i++)
            {
                int cid = closure[i];
                int oldCount = _counts[cid];
                int newCount = oldCount - amount;
                if (newCount < 0) newCount = 0;
                _counts[cid] = newCount;

                if (oldCount > 0 && newCount == 0) _bits[cid >> 6] &= ~(1UL << (cid & 63));

                var ctag = TagRegistry.GetTag(cid);
                OnTagCountChanged?.Invoke(ctag, oldCount, newCount);
                if (oldCount > 0 && newCount == 0) OnTagRemoved?.Invoke(ctag);
            }
        }
        public void RemoveTags(List<GameplayTag> tags)
        {
            foreach (GameplayTag tag in tags) RemoveTag(tag, 1);
        }

        public bool HasTag(GameplayTag tag)
        {
            EnsureCapacity();
            int id = TagRegistry.GetId(tag);
            return id >= 0 && _counts[id] > 0;
        }

        /// <summary>
        /// 책임 :
        /// - 부모/자식 closure를 포함하지 않고 직접 AddTag/AddTagId로 부여된 태그만 확인한다.
        /// - 구덩이 무시 태그처럼 하위 상태까지 포괄하면 안 되는 예외 판정에 사용한다.
        /// </summary>
        public bool HasExplicitTag(GameplayTag tag)
        {
            EnsureCapacity();
            int id = TagRegistry.GetId(tag);
            return id >= 0 && id < _explicitCounts.Length && _explicitCounts[id] > 0;
        }

        // ✅ 고속 쿼리(비트마스크)
        public bool HasAll(TagMask required)
        {
            EnsureCapacity();
            var req = required?.Words;
            if (req == null) return true;

            for (int w = 0; w < req.Length; w++)
                if ((_bits[w] & req[w]) != req[w]) return false;

            return true;
        }

        public bool HasAny(TagMask any)
        {
            EnsureCapacity();
            var q = any?.Words;
            if (q == null) return false;

            for (int w = 0; w < q.Length; w++)
                if ((_bits[w] & q[w]) != 0) return true;

            return false;
        }

        // 기존 코드 호환용(AbilityDefinition이 리스트를 넘기던 방식)
        public bool HasAllTags(IEnumerable<GameplayTag> tags)
        {
            if (tags == null) return true;
            foreach (var t in tags) if (t != null && !HasTag(t)) return false;
            return true;
        }

        public bool HasAnyTag(IEnumerable<GameplayTag> tags)
        {
            if (tags == null) return false;
            foreach (var t in tags) if (t != null && HasTag(t)) return true;
            return false;
        }

        public int GetTagCount(GameplayTag tag)
        {
            EnsureCapacity();
            int id = TagRegistry.GetId(tag);
            if (id < 0) return 0;
            return _counts[id];
        }
        /// <summary>
        /// 책임 : 현재 explicit tag들과 각 count를 순회 가능하게 제공한다.
        /// expanded/final count가 아니라, 직접 AddTag/AddTagId로 부여된 원본 상태만 저장 대상으로 내보낸다.
        /// </summary>
        public IEnumerable<ExplicitTagSnapshot> EnumerateExplicitTagsWithCount()
        {
            EnsureCapacity();

            for (int id = 1; id < _explicitCounts.Length; id++)
            {
                int count = _explicitCounts[id];
                if (count <= 0)
                    continue;

                var tag = TagRegistry.GetTag(id);
                if (tag == null)
                    continue;

                yield return new ExplicitTagSnapshot
                {
                    tagName = tag.Name,
                    count = count
                };
            }
        }

        /// <summary>
        /// 책임 : 특정 explicit tag의 현재 count를 조회한다.
        /// 저장/검증/선택적 복원 분기에서 사용한다.
        /// </summary>
        public int GetExplicitTagCount(GameplayTag tag)
        {
            EnsureCapacity();

            if (tag == null)
                return 0;

            int id = TagRegistry.GetId(tag);
            if (id <= 0 || id >= _explicitCounts.Length)
                return 0;

            return _explicitCounts[id];
        }

        /// <summary>
        /// 책임 : 현재 explicit tag를 전부 제거한다.
        /// RemoveTagId를 explicit count만큼 한 번에 호출하여 파생 closure count도 함께 정리한다.
        /// </summary>
        public void ClearAllExplicitTags()
        {
            EnsureCapacity();

            for (int id = 1; id < _explicitCounts.Length; id++)
            {
                int count = _explicitCounts[id];
                if (count <= 0)
                    continue;

                RemoveTagId(id, count);
            }
        }

        /// <summary>
        /// 책임 : 저장된 explicit tag 스냅샷을 그대로 복원한다.
        /// 복원 전에 기존 explicit tag를 비우고, snapshot count만큼 AddTag를 호출한다.
        /// </summary>
        public void RestoreExplicitTagSnapshots(
            IEnumerable<ExplicitTagSnapshot> snapshots,
            Func<string, GameplayTag> resolver)
        {
            EnsureCapacity();

            ClearAllExplicitTags();

            if (snapshots == null || resolver == null)
                return;

            foreach (var entry in snapshots)
            {
                if (entry == null)
                    continue;

                if (string.IsNullOrEmpty(entry.tagName))
                    continue;

                var tag = resolver(entry.tagName);
                if (tag == null)
                {
                    Debug.LogWarning($"[TagSystem] explicit tag 복원 실패: '{entry.tagName}' 을(를) 찾지 못했습니다.", this);
                    continue;
                }

                int count = Mathf.Max(0, entry.count);
                if (count <= 0)
                    continue;

                AddTag(tag, count);
            }
        }

    }
}
