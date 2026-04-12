using UnityEngine;

namespace UnityGAS
{
    [DisallowMultipleComponent]
    public sealed class GameplayCue_CameraShake : GameplayCueNotify
    {
        [Header("Camera Shake")]
        [SerializeField] private CameraShakeHook shake = CameraShakeHook.Create(
            amplitude: 0.08f,
            amplitudeMultiplier: 1f,
            maxAmplitude: 1.5f,
            minIntervalSeconds: 0.02f);
        [SerializeField] private bool scaleByCueMagnitude = true;

        [HideInInspector, SerializeField, Min(0f)] private float baseAmplitude = 0.08f;
        [HideInInspector, SerializeField, Min(0f)] private float maxAmplitude = 1.5f;
        [HideInInspector, SerializeField, Min(0f)] private float minIntervalSeconds = 0.02f;
        [HideInInspector, SerializeField] private bool legacyShakeMigrated;

        private void Awake()
        {
            MigrateLegacyShakeIfNeeded();
        }

        private void OnValidate()
        {
            MigrateLegacyShakeIfNeeded();
        }

        public override void OnExecute(GameplayCueParams p)
        {
            MigrateLegacyShakeIfNeeded();

            float cueMagnitude = scaleByCueMagnitude ? Mathf.Max(0f, p.Magnitude) : 1f;
            shake.TryPlay(
                p.Causer != null ? p.Causer : p.Instigator,
                ResolveDirection(p),
                cueMagnitude,
                nameof(GameplayCue_CameraShake));
        }

        private void MigrateLegacyShakeIfNeeded()
        {
            if (legacyShakeMigrated)
                return;

            shake = CameraShakeHook.Create(
                amplitude: baseAmplitude,
                amplitudeMultiplier: 1f,
                maxAmplitude: maxAmplitude,
                minIntervalSeconds: minIntervalSeconds);
            legacyShakeMigrated = true;
        }

        private static Vector3 ResolveDirection(GameplayCueParams p)
        {
            GameObject origin = p.Causer != null ? p.Causer : p.Instigator;
            if (p.Target != null && origin != null)
            {
                Vector3 delta = p.Target.transform.position - origin.transform.position;
                delta.z = 0f;
                if (delta.sqrMagnitude > 0.0001f)
                    return delta.normalized;
            }

            return Vector3.up;
        }
    }
}
