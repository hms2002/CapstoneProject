using UnityEngine;

namespace UnityGAS
{
    // 이 스크립트의 책임 : 
    [CreateAssetMenu(fileName = "NewGameplayCue", menuName = "GAS/Gameplay Cue Definition")]
    public class GameplayCueDefinition : ScriptableObject
    {
        public enum ExecutionMode
        {
            /// <summary>
            /// Instantiate 없이 Target Transform만 조작합니다.
            /// </summary>
            TransformOnly,

            /// <summary>
            /// 프리팹을 Instantiate 하지 않고, Target에 GameplayCueNotify 컴포넌트를 AddComponent/GetComponent 해서 호출합니다.
            /// </summary>
            TargetNotify,

            /// <summary>
            /// cuePrefab / vfxPrefab 등을 Instantiate 해서 연출합니다.
            /// </summary>
            SpawnPrefab
        }

        [Header("Key")]
        public GameplayTag cueTag;

        [Header("Execution")]
        public ExecutionMode mode = ExecutionMode.SpawnPrefab;

        // ------------------------------------------------------------
        // TargetNotify
        // ------------------------------------------------------------
        [Header("Target Notify")]
        [Tooltip("[mode=TargetNotify] 이 프리팹에 붙어있는 GameplayCueNotify 타입을 Target에 AddComponent/GetComponent 해서 호출합니다.\n" +
                 "- 프리팹 Instantiate 없음\n" +
                 "- 프리팹 내부 참조(SerializeField로 자식/VFX를 물고 있음)가 없는 '코드형 Notify'에 적합")]
        public GameObject cueNotifyHostPrefab;

        // ------------------------------------------------------------
        // SpawnPrefab
        // ------------------------------------------------------------
        [Header("Spawn Prefab")]
        [Tooltip("[mode=SpawnPrefab] 있으면 이 프리팹을 Spawn해서 GameplayCueNotify 콜백을 호출합니다.")]
        public GameObject cuePrefab;

        [Tooltip("[mode=SpawnPrefab] cuePrefab이 없거나 Notify가 없을 때 사용")]
        public GameObject vfxPrefab;

        [Tooltip("[mode=SpawnPrefab] 간단 SFX")]
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

        // ------------------------------------------------------------
        // TransformOnly
        // ------------------------------------------------------------
        [Header("Transform Only")]
        [Tooltip("[mode=TransformOnly] 로컬 위치에 더할 값")]
        public Vector3 addLocalPosition = Vector3.zero;

        [Tooltip("[mode=TransformOnly] 로컬 회전에 더할 Euler (deg)")]
        public Vector3 addLocalEuler = Vector3.zero;

        [Tooltip("[mode=TransformOnly] 로컬 스케일에 곱할 값 (1,1,1이면 변화 없음)")]
        public Vector3 mulLocalScale = Vector3.one;

        [Tooltip("[mode=TransformOnly] ExecuteCue에서 Transform을 유지할 시간(초). 0이면 1프레임 적용 후 해제")]
        public float transformExecuteDuration = 0f;
    }
}
