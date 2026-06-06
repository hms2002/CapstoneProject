using UnityEngine;

namespace UnityGAS
{
    public static class AbilityAimResolver2D
    {
        public static Vector2 Resolve(GameObject owner, Vector2 fallback)
        {
            if (owner == null)
                return fallback;

            var aimSource = owner.GetComponent<IAimDirectionSource2D>();
            if (aimSource != null)
            {
                Vector2 dir = aimSource.GetAimDirection();
                if (dir.sqrMagnitude > 0.0001f)
                    return dir.normalized;
            }

            var facingSource = owner.GetComponent<IFacingDirectionSource2D>();
            if (facingSource != null)
            {
                Vector2 dir = facingSource.GetFacingDirection();
                if (dir.sqrMagnitude > 0.0001f)
                    return dir.normalized;
            }

            Vector2 right = owner.transform.right;
            if (right.sqrMagnitude > 0.0001f)
                return right.normalized;

            return fallback;
        }
    }
}