using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cainos.PixelArtTopDown_Basic
{
    //let camera follow target
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public float lerpSpeed = 1.0f;
        public bool autoBindToSpawnedPlayer = true;

        private Vector3 offset;

        private Vector3 targetPos;

        private void OnEnable()
        {
            PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
            PlayerRuntimeRegistry.PlayerUnregistered += HandlePlayerUnregistered;
            TryResolveTarget();
        }

        private void Start()
        {
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

            SetTarget(player.transform);
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
                offset = transform.position - target.position;
                return;
            }

            var playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
            if (playerTransform == null)
                return;

            SetTarget(playerTransform);
        }

        private void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
                offset = transform.position - target.position;
        }

        private void Update()
        {
            if (target == null) return;

            targetPos = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, targetPos, lerpSpeed * Time.deltaTime);
        }

    }
}
