using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    public struct HitInfo2D
    {
        public GameObject Target;
        public Collider2D Collider;
        public Vector2 Point;
        public Vector2 Normal;
    }

    public sealed class AbilityTargetData2D
    {
        public readonly List<GameObject> Targets = new();
        public readonly List<HitInfo2D> Hits = new();

        public static AbilityTargetData2D FromOverlapBox(
            Vector2 center,
            Vector2 size,
            float angleDeg,
            LayerMask layers,
            GameObject ignore = null)
        {
            var data = new AbilityTargetData2D();

            var cols = Physics2D.OverlapBoxAll(center, size, angleDeg, layers);
            if (cols == null || cols.Length == 0) return data;

            var set = new HashSet<GameObject>();
            foreach (var c in cols)
            {
                if (c == null) continue;

                var go = c.attachedRigidbody != null ? c.attachedRigidbody.gameObject : c.gameObject;
                if (go == null || go == ignore) continue;

                if (!set.Add(go)) continue;

                data.Targets.Add(go);
                data.Hits.Add(new HitInfo2D
                {
                    Target = go,
                    Collider = c,
                    Point = c.ClosestPoint(center),
                    Normal = Vector2.zero
                });
            }
            return data;
        }
    }
}
