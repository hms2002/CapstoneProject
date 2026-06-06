using System;
using UnityEngine;

namespace UnityGAS
{
    [DisallowMultipleComponent]
    public sealed class GameplayCue_PlayParticleSystems : GameplayCueNotify
    {
        [SerializeField] private ParticleSystem[] particleSystems = Array.Empty<ParticleSystem>();
        [SerializeField] private bool clearBeforePlay = true;
        [SerializeField] private bool playOnExecute = true;
        [SerializeField] private bool playOnAdd = false;
        [SerializeField] private bool stopOnRemove = true;
        [SerializeField] private bool clearOnRemove = false;
        [SerializeField] private bool orientToImpactDirection = false;
        [SerializeField] private float rotationOffsetZ = 0f;

        private void Awake()
        {
            CacheParticleSystemsIfNeeded();
        }

        public override void OnExecute(GameplayCueParams p)
        {
            if (!playOnExecute)
                return;

            PlayInternal(p);
        }

        public override void OnAdd(GameplayCueParams p)
        {
            if (!playOnAdd)
                return;

            PlayInternal(p);
        }

        public override void OnRemove(GameplayCueParams p)
        {
            if (!stopOnRemove)
                return;

            StopAll(clearOnRemove);
        }

        private void PlayInternal(GameplayCueParams p)
        {
            CacheParticleSystemsIfNeeded();
            if (particleSystems == null || particleSystems.Length == 0)
                return;

            if (orientToImpactDirection)
                ApplyRotation(ResolveImpactDirection(p));

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem system = particleSystems[i];
                if (system == null)
                    continue;

                if (clearBeforePlay)
                {
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    system.Clear(true);
                }

                system.Play(true);
            }
        }

        private void StopAll(bool clear)
        {
            CacheParticleSystemsIfNeeded();
            if (particleSystems == null || particleSystems.Length == 0)
                return;

            ParticleSystemStopBehavior behavior = clear
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem system = particleSystems[i];
                if (system == null)
                    continue;

                system.Stop(true, behavior);
            }
        }

        private void CacheParticleSystemsIfNeeded()
        {
            if (particleSystems != null && particleSystems.Length > 0)
                return;

            particleSystems = GetComponentsInChildren<ParticleSystem>(includeInactive: true);
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
            Transform attacker = p.Causer != null ? p.Causer.transform : null;
            if (attacker == null && p.Instigator != null)
                attacker = p.Instigator.transform;

            Transform target = p.Target != null ? p.Target.transform : null;
            if (attacker != null && target != null)
            {
                Vector2 direction = (Vector2)(target.position - attacker.position);
                if (direction.sqrMagnitude > 0.0001f)
                    return direction.normalized;
            }

            return Vector2.right;
        }
    }
}
