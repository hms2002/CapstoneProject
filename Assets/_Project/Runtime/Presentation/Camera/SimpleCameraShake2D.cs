using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Legacy helper that now exposes the shared camera shake hook in the inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimpleCameraShake2D : MonoBehaviour
    {
        [Header("Camera Shake")]
        [SerializeField] private CameraShakeHook shake = CameraShakeHook.Create(
            amplitude: 1f,
            amplitudeMultiplier: 1f,
            maxAmplitude: 0f,
            minIntervalSeconds: 0f);

        [HideInInspector, SerializeField] private float duration = 0.12f;
        [HideInInspector, SerializeField] private float frequency = 25f;
        [HideInInspector, SerializeField] private Transform target;

        public void Shake(float amplitude)
        {
            shake.TryPlayOverrideAmplitude(
                amplitude,
                gameObject,
                Vector3.up,
                nameof(SimpleCameraShake2D));
        }

        private void OnValidate()
        {
            duration = Mathf.Max(0f, duration);
            frequency = Mathf.Max(0f, frequency);
            if (target == transform)
                target = null;
        }
    }
}
