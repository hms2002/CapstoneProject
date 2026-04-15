using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;

namespace UnityGAS
{
    [CreateAssetMenu(fileName = "NewGameplayCue", menuName = "GAS/Gameplay Cue Definition")]
    public class GameplayCueDefinition : ScriptableObject
    {
        public enum ExecutionMode
        {
            TransformOnly,
            TargetNotify,
            SpawnPrefab
        }

        public enum SpawnAnchorPolicy
        {
            TargetPivot,
            TargetSpriteCenter,
            TargetSpriteTop,
            TargetSpriteBottom,
            TargetColliderCenter
        }

        [Header("Key")]
        public GameplayTag cueTag;

        [Header("Execution")]
        public ExecutionMode mode = ExecutionMode.SpawnPrefab;

        [Header("Target Notify")]
        [Tooltip("Used when mode is TargetNotify. The manager adds or reuses a GameplayCueNotify on the target.")]
        public GameObject cueNotifyHostPrefab;

        [Header("Spawn Prefab")]
        [Tooltip("Used when mode is SpawnPrefab. Preferred cue prefab with a GameplayCueNotify.")]
        public GameObject cuePrefab;

        [Tooltip("Fallback visual prefab when cuePrefab is not assigned.")]
        public GameObject vfxPrefab;

        [Header("Audio (Optional)")]
        public SoundRef audioOnExecute;
        public SoundRef audioWhileActive;
        public SoundRef audioOnRemove;

        [Header("Camera Shake (Optional)")]
        public CameraShakeHook cameraShakeOnExecute;
        public CameraShakeHook cameraShakeWhileActive;
        public CameraShakeHook cameraShakeOnRemove;

        [Header("Spawned Presentation (Optional)")]
        public WorldPresentationHook presentationOnExecute;
        public WorldPresentationHook presentationWhileActive;
        public WorldPresentationHook presentationOnRemove;

        [Header("Spawn Options")]
        public bool attachToTarget = true;
        [Tooltip("Use the explicit hit/world position supplied by gameplay events when available.")]
        public bool useExplicitHitPoint = false;
        public SpawnAnchorPolicy spawnAnchorPolicy = SpawnAnchorPolicy.TargetPivot;
        [Tooltip("Apply localOffset in target local space instead of world space.")]
        public bool applyOffsetInTargetLocalSpace = true;
        public Vector3 localOffset = Vector3.zero;

        [Tooltip("Auto destroy spawned execute-only visuals after this many seconds. 0 disables auto destroy.")]
        public float autoDestroySeconds = 2.0f;

        [Header("Persistence")]
        [Tooltip("Whether AddCue/RemoveCue should manage a persistent runtime instance.")]
        public bool isPersistent = true;

        [Tooltip("If true, only one cue instance per target is kept.")]
        public bool uniquePerTarget = true;

        [Header("Transform Only")]
        public Vector3 addLocalPosition = Vector3.zero;
        public Vector3 addLocalEuler = Vector3.zero;
        public Vector3 mulLocalScale = Vector3.one;
        public float transformExecuteDuration = 0f;
    }
}
