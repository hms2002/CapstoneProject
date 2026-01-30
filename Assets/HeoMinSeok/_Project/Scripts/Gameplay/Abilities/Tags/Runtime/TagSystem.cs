using System;
using System.Collections.Generic;
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

    }
}
