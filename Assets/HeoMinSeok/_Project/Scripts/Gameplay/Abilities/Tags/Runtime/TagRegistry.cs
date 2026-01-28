using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Resources/Tags 아래 GameplayTag들을 로드하고
    /// - tag -> id
    /// - path(prefix) 기반 부모 체인 closure
    /// - closureMask(비트셋)
    /// 을 1회 빌드해서 캐시한다.
    ///
    /// NOTE: 현재 프로젝트의 태그 계층은 GameplayTag.name = "A.B.C" 규칙을 사용한다.
    /// </summary>
    public static class TagRegistry
    {
        private static bool _initialized;

        private static GameplayTag[] _tagsById;
        private static Dictionary<GameplayTag, int> _idByTag;
        private static Dictionary<string, int> _idByPath;

        private static int[][] _closureIds;      // id -> [id, parent, parent...]
        private static ulong[][] _closureMasks;  // id -> bitset words

        private static int _tagCount;
        private static int _wordCount;

        public static int TagCount { get { EnsureInitialized(); return _tagCount; } }
        public static int WordCount { get { EnsureInitialized(); return _wordCount; } }

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            Build();
            _initialized = true;
        }

        public static int GetId(GameplayTag tag)
        {
            EnsureInitialized();
            if (tag == null) return -1;
            return _idByTag.TryGetValue(tag, out var id) ? id : -1;
        }

        public static GameplayTag GetTag(int id)
        {
            EnsureInitialized();
            if (id < 0 || id >= _tagsById.Length) return null;
            return _tagsById[id];
        }

        public static int[] GetClosureIds(int id)
        {
            EnsureInitialized();
            if (id < 0 || id >= _closureIds.Length) return Array.Empty<int>();
            return _closureIds[id];
        }

        public static ulong[] GetClosureMask(int id)
        {
            EnsureInitialized();
            if (id < 0 || id >= _closureMasks.Length) return Array.Empty<ulong>();
            return _closureMasks[id];
        }

        private static void Build()
        {
            // TagEditor가 생성한 Resources/Tags 폴더 규칙을 그대로 사용
            var loaded = Resources.LoadAll<GameplayTag>("Tags");
            if (loaded == null) loaded = Array.Empty<GameplayTag>();

            // 안정적 순서: path(name) 기준 정렬
            Array.Sort(loaded, (a, b) => string.CompareOrdinal(a != null ? a.name : "", b != null ? b.name : ""));

            _idByTag = new Dictionary<GameplayTag, int>(loaded.Length);
            _idByPath = new Dictionary<string, int>(loaded.Length);

            // id는 1부터 (0은 비워두는 편이 디버깅에 좋음)
            int nextId = 1;

            // 먼저 path->id 매핑
            for (int i = 0; i < loaded.Length; i++)
            {
                var tag = loaded[i];
                if (tag == null) continue;

                var path = tag.name;
                if (string.IsNullOrEmpty(path)) continue;

                if (_idByPath.ContainsKey(path))
                {
                    Debug.LogWarning($"[TagRegistry] Duplicate tag path detected: '{path}'. (Ignoring later one)");
                    continue;
                }

                int id = nextId++;
                _idByTag[tag] = id;
                _idByPath[path] = id;
            }

            int maxId = nextId; // 배열 크기
            _tagsById = new GameplayTag[maxId];
            _closureIds = new int[maxId][];
            _closureMasks = new ulong[maxId][];

            // tagsById 채우기
            foreach (var kv in _idByTag)
                _tagsById[kv.Value] = kv.Key;

            _tagCount = _tagsById.Length;
            _wordCount = (_tagCount + 63) >> 6;

            // closureIds 계산: "A.B.C" -> ["A.B.C","A.B","A"] 중 실제 존재하는 것만
            for (int id = 1; id < _tagsById.Length; id++)
            {
                var tag = _tagsById[id];
                if (tag == null)
                {
                    _closureIds[id] = Array.Empty<int>();
                    continue;
                }

                string path = tag.name;
                var list = new List<int>(4);

                // 자기 자신 포함
                list.Add(id);

                // 부모 prefix를 찾아서 포함
                int dot = path.LastIndexOf('.');
                while (dot > 0)
                {
                    string parentPath = path.Substring(0, dot);
                    if (_idByPath.TryGetValue(parentPath, out var pid))
                        list.Add(pid);

                    dot = parentPath.LastIndexOf('.');
                    path = parentPath;
                }

                _closureIds[id] = list.ToArray();
            }

            // closureMask 생성
            for (int id = 1; id < _closureIds.Length; id++)
            {
                var words = new ulong[_wordCount];
                var ids = _closureIds[id];
                for (int i = 0; i < ids.Length; i++)
                {
                    int cid = ids[i];
                    words[cid >> 6] |= 1UL << (cid & 63);
                }
                _closureMasks[id] = words;
            }
        }
        public static int GetIdByPath(string path)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(path)) return -1;
            return _idByPath.TryGetValue(path, out var id) ? id : -1;
        }

    }
}
