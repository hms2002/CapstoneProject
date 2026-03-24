using UnityEngine;

namespace Cainos.PixelArtTopDown_Basic
{
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float lerpSpeed = 10f;
        [SerializeField] private bool autoBindToSpawnedPlayer = true;
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 0f, -10f);
        [SerializeField] private bool snapWhenTargetBound = true;

        private void OnEnable()
        {
            PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
            PlayerRuntimeRegistry.PlayerUnregistered += HandlePlayerUnregistered;
            TryResolveTarget();
        }

        private void OnDisable()
        {
            PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
            PlayerRuntimeRegistry.PlayerUnregistered -= HandlePlayerUnregistered;
        }

        private void HandlePlayerRegistered(SampleTopDownPlayer player)
        {
            if (!autoBindToSpawnedPlayer || player == null)
                return;

            BindTarget(player.transform, snapWhenTargetBound);
        }

        private void HandlePlayerUnregistered(SampleTopDownPlayer player)
        {
            if (player != null && target == player.transform)
                target = null;
        }

        private void TryResolveTarget()
        {
            if (!autoBindToSpawnedPlayer && target != null)
            {
                if (snapWhenTargetBound)
                    SnapToTarget();
                return;
            }

            var playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
            if (playerTransform != null)
                BindTarget(playerTransform, snapWhenTargetBound);
        }

        public void BindTarget(Transform newTarget, bool snap = true)
        {
            target = newTarget;

            if (target != null && snap)
                SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (target == null)
                return;

            transform.position = target.position + followOffset;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            Vector3 targetPos = target.position + followOffset;
            transform.position = Vector3.Lerp(transform.position, targetPos, lerpSpeed * Time.deltaTime);
        }
    }
}