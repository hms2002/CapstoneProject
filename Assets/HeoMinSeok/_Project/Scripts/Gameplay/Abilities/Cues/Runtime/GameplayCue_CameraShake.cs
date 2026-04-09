using Unity.Cinemachine;
using UnityEngine;

namespace UnityGAS
{
    [DisallowMultipleComponent]
    public sealed class GameplayCue_CameraShake : GameplayCueNotify
    {
        [SerializeField, Min(0f)] private float baseAmplitude = 0.08f;
        [SerializeField, Min(0f)] private float maxAmplitude = 1.5f;
        [SerializeField, Min(0f)] private float minIntervalSeconds = 0.02f;
        [SerializeField] private bool scaleByCueMagnitude = true;

        private static float s_lastEmitTime = -999f;

        public override void OnExecute(GameplayCueParams p)
        {
            if (!GameSettingsService.IsScreenShakeEnabled())
                return;

            float cueMagnitude = scaleByCueMagnitude ? Mathf.Max(0f, p.Magnitude) : 1f;
            float amplitude = Mathf.Min(maxAmplitude, baseAmplitude * cueMagnitude);
            if (amplitude <= 0f)
                return;

            float now = Time.unscaledTime;
            if (minIntervalSeconds > 0f && now - s_lastEmitTime < minIntervalSeconds)
                return;

            Camera camera = CameraBootstrap.GetMainCamera();
            if (camera == null)
                camera = Camera.main;
            if (camera == null)
                return;

            s_lastEmitTime = now;

            var impulseSource = CameraBootstrap.EnsureImpulseSource(camera.gameObject);

            impulseSource.GenerateImpulse(ResolveDirection(p) * amplitude);
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
