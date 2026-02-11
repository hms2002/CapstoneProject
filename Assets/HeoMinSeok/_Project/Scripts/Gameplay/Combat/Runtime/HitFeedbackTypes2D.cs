using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Read-only hit feedback payload delivered to the target when damage is applied.
    /// GE_Damage_Spec emits this based on SetByCaller keys (stun, camera shake, etc.).
    /// </summary>
    public struct HitFeedbackPayload
    {
        public GameObject Causer;
        public float StunSeconds;
        public float CameraShake;
        public HitFeedbackPayload(GameObject causer, float stunSeconds, float cameraShake)
        {
            Causer = causer;
            StunSeconds = stunSeconds;
            CameraShake = cameraShake;
        }
    }

    public interface IHitFeedbackReceiver2D
    {
        void OnHitFeedback(HitFeedbackPayload payload);
    }
}
