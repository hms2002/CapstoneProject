using UnityEngine;
using Unity.Cinemachine;

namespace Cainos.PixelArtTopDown_Basic
{
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float lerpSpeed = 10f;
        [SerializeField] private bool autoBindToSpawnedPlayer = true;
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 0f, -10f);
        [SerializeField] private bool snapWhenTargetBound = true;

        [Header("Cinemachine")]
        [SerializeField] private CinemachineCamera controlledCamera;
        [SerializeField] private bool autoResolveControlledCamera = true;
        [SerializeField] private bool bindLookAtToTarget = true;

        private bool hasLoggedMissingCamera;

        private void OnEnable()
        {
            PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
            PlayerRuntimeRegistry.PlayerUnregistered += HandlePlayerUnregistered;

            ResolveControlledCamera();
            TryResolveTarget();
        }

        private void OnDisable()
        {
            PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
            PlayerRuntimeRegistry.PlayerUnregistered -= HandlePlayerUnregistered;
        }

        private void HandlePlayerRegistered(PlayerInteractor2D player)
        {
            if (!autoBindToSpawnedPlayer || player == null)
                return;

            BindTarget(player.transform, snapWhenTargetBound);
        }

        private void HandlePlayerUnregistered(PlayerInteractor2D player)
        {
            if (player != null && target == player.transform)
            {
                ClearBinding(player.transform);
                target = null;
            }
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
            ApplyBinding(snap);
        }

        public void SnapToTarget()
        {
            if (target == null || !ResolveControlledCamera())
                return;

            SyncControlledCameraSettings();
            controlledCamera.ForceCameraPosition(target.position + followOffset, controlledCamera.transform.rotation);
        }

        private void ApplyBinding(bool snap)
        {
            if (target == null || !ResolveControlledCamera())
                return;

            controlledCamera.Follow = target;
            if (bindLookAtToTarget)
                controlledCamera.LookAt = target;

            SyncControlledCameraSettings();

            if (snap)
                SnapToTarget();
        }

        private void ClearBinding(Transform boundTarget)
        {
            if (!ResolveControlledCamera() || boundTarget == null)
                return;

            if (controlledCamera.Follow == boundTarget)
                controlledCamera.Follow = null;

            if (bindLookAtToTarget && controlledCamera.LookAt == boundTarget)
                controlledCamera.LookAt = null;
        }

        private bool ResolveControlledCamera()
        {
            if (controlledCamera != null)
            {
                SyncControlledCameraSettings();
                return true;
            }

            if (!autoResolveControlledCamera)
                return false;

            controlledCamera = FindBestPlayerCamera();
            if (controlledCamera == null)
            {
                if (!hasLoggedMissingCamera)
                {
                    Debug.LogWarning("[CameraFollow] Could not find an unbound CinemachineCamera to control.", this);
                    hasLoggedMissingCamera = true;
                }

                return false;
            }

            hasLoggedMissingCamera = false;
            SyncControlledCameraSettings();
            return true;
        }

        private CinemachineCamera FindBestPlayerCamera()
        {
            var cameras = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            CinemachineCamera bestCamera = null;
            int bestPriority = int.MinValue;

            for (int i = 0; i < cameras.Length; i++)
            {
                var candidate = cameras[i];
                if (candidate == null || !candidate.isActiveAndEnabled)
                    continue;

                var trackingTarget = candidate.Target.TrackingTarget;
                bool canBindCandidate = trackingTarget == null || trackingTarget == target;
                if (!canBindCandidate)
                    continue;

                int priority = candidate.Priority;
                if (bestCamera == null || priority > bestPriority)
                {
                    bestCamera = candidate;
                    bestPriority = priority;
                }
            }

            return bestCamera;
        }

        private void SyncControlledCameraSettings()
        {
            if (controlledCamera == null)
                return;

            var follow = controlledCamera.GetComponent<CinemachineFollow>();
            if (follow != null)
            {
                follow.FollowOffset = followOffset;

                var trackerSettings = follow.TrackerSettings;
                float damping = lerpSpeed > 0f ? 1f / lerpSpeed : 0f;
                trackerSettings.PositionDamping = new Vector3(damping, damping, damping);
                follow.TrackerSettings = trackerSettings;
            }

            EnsureImpulseListener(controlledCamera);
        }

        private static void EnsureImpulseListener(CinemachineCamera camera)
        {
            if (camera == null)
                return;

            var listener = camera.GetComponent<CinemachineImpulseListener>();
            if (listener != null)
                return;

            listener = camera.gameObject.AddComponent<CinemachineImpulseListener>();
            listener.ChannelMask = 1;
            listener.Gain = 1f;
            listener.Use2DDistance = true;
        }
    }
}
