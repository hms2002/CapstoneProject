using UnityEngine;

namespace UnityGAS
{
    public interface IDamageReceiver
    {
        bool TryApplyDamage(DamageRequest request);
    }
}