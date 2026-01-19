using System.Collections.Generic;

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
