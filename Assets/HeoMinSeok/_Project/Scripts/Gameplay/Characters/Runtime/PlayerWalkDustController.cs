using UnityEngine;

namespace UnityGAS
{
    [DisallowMultipleComponent]
    public sealed class PlayerWalkDustController : MonoBehaviour
    {
        [SerializeField] private MovementMotor2D movementMotor;
        [SerializeField] private GameObject walkDustPrefab;
        [SerializeField] private Transform dustAnchor;
        [SerializeField] private Vector3 localOffset = Vector3.zero;
        [SerializeField, Min(0f)] private float minIntentSpeed = 0.05f;
        [SerializeField] private bool clearOnStop = false;

        private GameObject dustInstance;
        private ParticleSystem[] particleSystems;
        private bool isEmitting;

        private void Awake()
        {
            if (movementMotor == null)
                movementMotor = GetComponent<MovementMotor2D>();

            if (dustAnchor == null)
                dustAnchor = transform;

            EnsureDustInstance();
            StopDust(clear: true);
        }

        private void OnEnable()
        {
            RefreshDustState(force: true);
        }

        private void Update()
        {
            RefreshDustState(force: false);
        }

        private void OnDisable()
        {
            StopDust(clear: true);
            isEmitting = false;
        }

        private void RefreshDustState(bool force)
        {
            bool shouldEmit = movementMotor != null &&
                              movementMotor.LastIntentVelocity.sqrMagnitude >= minIntentSpeed * minIntentSpeed;

            if (!force && shouldEmit == isEmitting)
                return;

            if (shouldEmit)
                PlayDust();
            else
                StopDust(clearOnStop);

            isEmitting = shouldEmit;
        }

        private void EnsureDustInstance()
        {
            if (dustInstance != null || walkDustPrefab == null)
                return;

            Transform parent = dustAnchor != null ? dustAnchor : transform;
            dustInstance = Instantiate(walkDustPrefab, parent);
            dustInstance.name = walkDustPrefab.name;
            dustInstance.transform.localPosition = localOffset;
            dustInstance.transform.localRotation = Quaternion.identity;
            dustInstance.transform.localScale = Vector3.one;
            particleSystems = dustInstance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        }

        private void PlayDust()
        {
            EnsureDustInstance();
            if (particleSystems == null)
                return;

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem system = particleSystems[i];
                if (system == null)
                    continue;

                system.Play(true);
            }
        }

        private void StopDust(bool clear)
        {
            if (particleSystems == null)
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
    }
}
