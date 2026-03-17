using UnityEngine;

namespace UnityGAS
{
    public struct DamageRequest
    {
        public float HpDamage;
        public int TokenDamage;

        public GameObject Instigator;
        public GameObject Causer;
        public Object SourceObject;

        public float KnockbackImpulse;
        public float StunSeconds;
        public float CameraShake;
        public static DamageRequest Create(
    float hpDamage,
    int tokenDamage,
    GameObject instigator,
    GameObject causer,
    Object sourceObject,
    float knockbackImpulse,
    float stunSeconds,
    float cameraShake)
        {
            return new DamageRequest
            {
                HpDamage = hpDamage,
                TokenDamage = tokenDamage,
                Instigator = instigator,
                Causer = causer,
                SourceObject = sourceObject,
                KnockbackImpulse = knockbackImpulse,
                StunSeconds = stunSeconds,
                CameraShake = cameraShake
            };
        }
    }
}