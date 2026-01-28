using UnityEngine;

namespace UnityGAS
{
    [CreateAssetMenu(fileName = "NewGameplayCue", menuName = "GAS/Gameplay Cue Definition")]
    public class GameplayCueDefinition : ScriptableObject
    {
        [Header("Key")]
        public GameplayTag cueTag;

        [Header("Prefab Notify (Optional)")]
        [Tooltip("있으면 이 프리팹을 Spawn해서 GameplayCueNotify 콜백을 호출합니다.")]
        public GameObject cuePrefab;

        [Header("Simple VFX/SFX Fallback (Optional)")]
        [Tooltip("cuePrefab이 없거나 Notify가 없을 때 사용")]
        public GameObject vfxPrefab;
        public AudioClip sfx;

        [Header("Spawn Options")]
        public bool attachToTarget = true;
        public Vector3 localOffset = Vector3.zero;

        [Tooltip("ExecuteCue로 Spawn된 오브젝트 자동 파괴 시간(초). 0이면 파괴 안함.")]
        public float autoDestroySeconds = 2.0f;

        [Header("Persistence")]
        [Tooltip("AddCue/RemoveCue로 유지되는 지속 큐를 지원할지. true면 Manager가 인스턴스를 유지 관리.")]
        public bool isPersistent = true;

        [Tooltip("Target 하나당 이 Cue를 1개만 유지할지(권장). false면 AddCue 호출마다 새로 생성(관리 복잡).")]
        public bool uniquePerTarget = true;
    }
}
