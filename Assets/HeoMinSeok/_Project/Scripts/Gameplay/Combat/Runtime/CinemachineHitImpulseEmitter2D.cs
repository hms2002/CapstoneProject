using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Emits hit shake using the shared camera shake hook.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CinemachineHitImpulseEmitter2D : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private Vector3 defaultDirection = new Vector3(0f, 1f, 0f);
        [SerializeField] private CameraShakeHook shake = CameraShakeHook.Create(
            amplitude: 1f,
            amplitudeMultiplier: 1f,
            maxAmplitude: 0f,
            minIntervalSeconds: 0f);

        [HideInInspector, SerializeField, Min(0f)] private float amplitudeMultiplier = 1f;
        [HideInInspector, SerializeField] private bool legacyShakeMigrated;

        private void Awake()
        {
            MigrateLegacyShakeIfNeeded();
        }

        private void OnValidate()
        {
            MigrateLegacyShakeIfNeeded();
        }

        public void Emit(GameObject causer, float amplitude)
        {
            MigrateLegacyShakeIfNeeded();
            shake.TryPlayOverrideAmplitude(
                amplitude,
                gameObject,
                ResolveDirection(causer),
                nameof(CinemachineHitImpulseEmitter2D));
        }

        private void MigrateLegacyShakeIfNeeded()
        {
            if (legacyShakeMigrated)
                return;

            shake = CameraShakeHook.Create(
                amplitude: 1f,
                amplitudeMultiplier: amplitudeMultiplier,
                maxAmplitude: 0f,
                minIntervalSeconds: 0f);
            legacyShakeMigrated = true;
        }

        private Vector3 ResolveDirection(GameObject causer)
        {
            if (causer != null)
            {
                Vector3 delta = transform.position - causer.transform.position;
                delta.z = 0f;

                if (delta.sqrMagnitude > 0.0001f)
                    return delta.normalized;
            }

            if (defaultDirection.sqrMagnitude > 0.0001f)
                return defaultDirection.normalized;

            return Vector3.up;
        }
    }
}
