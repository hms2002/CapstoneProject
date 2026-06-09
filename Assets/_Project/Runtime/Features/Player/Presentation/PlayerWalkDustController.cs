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

        [Header("Gameplay Presentation (Optional)")]
        [SerializeField] private GameplayPresentationDefinition gameplayPresentation;

        private GameObject dustInstance;
        private ParticleSystem[] particleSystems;
        private bool isEmitting;
        private GameplayPresentationRuntime presentationRuntime;

        private void Awake()
        {
            if (movementMotor == null)
                movementMotor = GetComponent<MovementMotor2D>();

            if (dustAnchor == null)
                dustAnchor = transform;

            presentationRuntime = new GameplayPresentationRuntime(gameObject);
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
            presentationRuntime?.Stop(gameplayPresentation, BuildPresentationParams(), playRemove: false);
            isEmitting = false;
        }

        private void RefreshDustState(bool force)
        {
            bool shouldEmit = movementMotor != null &&
                              movementMotor.LastIntentVelocity.sqrMagnitude >= minIntentSpeed * minIntentSpeed;
            bool wasEmitting = isEmitting;

            if (!force && shouldEmit == wasEmitting)
                return;

            if (shouldEmit)
            {
                if (!wasEmitting)
                    presentationRuntime?.Start(gameplayPresentation, BuildPresentationParams());

                PlayDust();
            }
            else
            {
                StopDust(clearOnStop);

                if (wasEmitting)
                    presentationRuntime?.Stop(gameplayPresentation, BuildPresentationParams(), playRemove: true);
            }

            isEmitting = shouldEmit;
        }

        private GameplayCueParams BuildPresentationParams()
        {
            Vector3 position = dustAnchor != null ? dustAnchor.position : transform.position;
            return presentationRuntime.BuildParams(
                target: gameObject,
                sourceObject: this,
                explicitPosition: position,
                hasExplicitPosition: dustAnchor != null);
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
