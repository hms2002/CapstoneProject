using UnityEngine;

namespace UnityGAS
{
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class GameplayCue_HitSparkParticles : GameplayCueNotify
    {
        [SerializeField] private ParticleSystem particleSystemRef;
        [SerializeField] private bool clearBeforePlay = true;
        [SerializeField] private bool orientToImpactDirection = true;
        [SerializeField] private float rotationOffsetZ = 0f;
#if UNITY_EDITOR
        [Header("Editor Preview")]
        [SerializeField] private Vector2 previewDirection = new(1f, 0f);
#endif

        private void Awake()
        {
            if (particleSystemRef == null)
                particleSystemRef = GetComponent<ParticleSystem>();
        }

        public override void OnExecute(GameplayCueParams p)
        {
            if (particleSystemRef == null)
                return;

            ResetSystem();

            if (orientToImpactDirection)
                ApplyRotation(ResolveImpactDirection(p));

            particleSystemRef.Play(true);
        }

        private void ResetSystem()
        {
            if (!clearBeforePlay)
                return;

            particleSystemRef.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystemRef.Clear(true);
        }

        private void ApplyRotation(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
                return;

            float zRotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffsetZ;
            transform.rotation = Quaternion.Euler(0f, 0f, zRotation);
        }

        private static Vector2 ResolveImpactDirection(GameplayCueParams p)
        {
            Vector2 direction = Vector2.zero;
            Transform attacker = p.Causer != null ? p.Causer.transform : null;
            if (attacker == null && p.Instigator != null)
                attacker = p.Instigator.transform;

            Transform target = p.Target != null ? p.Target.transform : null;
            if (attacker != null && target != null)
                direction = (Vector2)(target.position - attacker.position);

            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.right;

            return direction.normalized;
        }

#if UNITY_EDITOR
        public void EditorPreviewBurst()
        {
            if (particleSystemRef == null)
                particleSystemRef = GetComponent<ParticleSystem>();

            if (particleSystemRef == null)
                return;

            ResetSystem();

            if (orientToImpactDirection)
            {
                Vector2 direction = previewDirection.sqrMagnitude > 0.0001f
                    ? previewDirection.normalized
                    : Vector2.right;
                ApplyRotation(direction);
            }

            particleSystemRef.Play(true);
        }
#endif
    }
}
