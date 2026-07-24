using UnityEngine;

namespace UnityGAS
{
    public class GameplayCue_SlashHit : GameplayCueNotify
    {
        [SerializeField] private float baseRotationZ = 28f;
        [SerializeField] private float randomRotationJitter = 3f;
        [SerializeField] private bool mirrorWhenTargetIsLeft = true;

        private Vector3 initialScale;

        private void Awake()
        {
            initialScale = transform.localScale;
        }

        public override void OnExecute(GameplayCueParams p)
        {
            Transform target = p.Target != null ? p.Target.transform : null;
            Transform instigator = p.Instigator != null ? p.Instigator.transform : null;

            if (instigator == null || target == null)
                return;

            Vector3 delta = target.position - instigator.position;
            float horizontal = Mathf.Max(Mathf.Abs(delta.x), 0.001f);
            float signedVerticalAngle = Mathf.Atan2(delta.y, horizontal) * Mathf.Rad2Deg;
            float rotationZ = baseRotationZ + signedVerticalAngle + Random.Range(-randomRotationJitter, randomRotationJitter);
            transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);

            if (mirrorWhenTargetIsLeft)
            {
                Vector3 scale = initialScale;
                scale.x = delta.x < 0f ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                transform.localScale = scale;
            }
        }
    }
}
