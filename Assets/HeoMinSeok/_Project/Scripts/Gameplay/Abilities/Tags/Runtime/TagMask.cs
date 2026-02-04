using System.Collections.Generic;
using System.Numerics;


namespace UnityGAS
{
    /// <summary>
    /// "쿼리 조건"을 미리 컴파일한 비트마스크.
    /// 런타임에서는 HasAll/HasAny를 비트연산으로 끝내기 위함.
    /// </summary>
    public sealed class TagMask
    {
        public readonly ulong[] Words;

        public TagMask(int wordCount)
        {
            Words = new ulong[wordCount];
        }

        public void Add(GameplayTag tag)
        {
            TagRegistry.EnsureInitialized();
            int id = TagRegistry.GetId(tag);
            if (id < 0) return;

            var m = TagRegistry.GetClosureMask(id);
            for (int w = 0; w < Words.Length; w++)
                Words[w] |= m[w];
        }
        public void AddExact(GameplayTag tag)
        {
            TagRegistry.EnsureInitialized();
            int id = TagRegistry.GetId(tag);
            AddExactId(id);
        }

        // TagMask.cs에 추가
        public void AddId(int id)
        {
            TagRegistry.EnsureInitialized();
            if (id <= 0) return;

            var m = TagRegistry.GetClosureMask(id);
            for (int w = 0; w < Words.Length; w++)
                Words[w] |= m[w];
        }
        public void AddExactId(int id)
        {
            TagRegistry.EnsureInitialized();
            if (id <= 0) return;

            int w = id >> 6;
            int b = id & 63;
            if ((uint)w >= (uint)Words.Length) return; // 안전
            Words[w] |= 1UL << b;
        }
        public void AddPath(string path) => AddId(TagRegistry.GetIdByPath(path));

        public static TagMask Compile(IEnumerable<GameplayTag> tags)
        {
            TagRegistry.EnsureInitialized();
            var mask = new TagMask(TagRegistry.WordCount);
            if (tags == null) return mask;

            foreach (var t in tags)
                if (t != null) mask.Add(t);

            return mask;
        }


    }
}
