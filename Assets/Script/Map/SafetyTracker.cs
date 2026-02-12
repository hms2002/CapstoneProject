using System.Collections;
using UnityEngine;
using UnityGAS;

public class SafetyTracker : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("안전한 땅으로 인식할 레이어")]
    [SerializeField] private LayerMask groundLayer;

    [Tooltip("확실히 구덩이가 아닌 곳만 체크 (옵션)")]
    [SerializeField] private LayerMask holeLayer;

    [SerializeField] private float checkInterval = 0.2f; // 체크 주기

    [Header("Unsafe Conditions")]
    [Tooltip("이 태그들을 가지고 있으면 위치를 저장하지 않음 (예: Action.Dash, State.Falling)")]
    [SerializeField] private GameplayTag[] unsafeTags;

    // 외부(HoleTrap)에서 읽어갈 속성
    public Vector3 LastSafePosition { get; private set; }

    private AbilitySystem abilitySystem;

    private void Awake()
    {
        abilitySystem = GetComponent<AbilitySystem>();
        LastSafePosition = transform.position;
    }

    private void Start()
    {
        StartCoroutine(TrackSafePositionRoutine());
    }

    private IEnumerator TrackSafePositionRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(checkInterval);

        while (true)
        {
            yield return wait;

            // 1. 상태 체크: GAS 태그 확인 (대쉬 중, 추락 중이면 저장 스킵)
            if (IsStatusUnsafe()) continue;

            // 2. 물리 체크: 땅 위에 있는가?
            if (CheckIsSafeGround())
            {
                LastSafePosition = transform.position;
                // 만약 타일 중앙으로 보정하고 싶다면:
                // LastSafePosition = new Vector3(Mathf.Round(transform.position.x), Mathf.Round(transform.position.y), transform.position.z);
            }
        }
    }

    private bool IsStatusUnsafe()
    {
        if (abilitySystem == null || abilitySystem.TagSystem == null) return false;
        if (unsafeTags == null || unsafeTags.Length == 0) return false;

        foreach (var tag in unsafeTags)
        {
            if (abilitySystem.TagSystem.HasTag(tag)) return true;
        }
        return false;
    }

    private bool CheckIsSafeGround()
    {
        Vector2 pos = transform.position;

        // 내 발 밑(반경 0.1f)에 Ground 레이어가 있는가?
        bool isOnGround = Physics2D.OverlapCircle(pos, 0.1f, groundLayer);

        // 구덩이 트리거와 겹쳐있는가? (레이어가 설정된 경우만)
        bool isOverHole = false;
        if (holeLayer != 0)
        {
            isOverHole = Physics2D.OverlapCircle(pos, 0.1f, holeLayer);
        }

        // 땅 위여야 하고, 동시에 구덩이 위는 아니어야 함
        return isOnGround && !isOverHole;
    }

    public Vector3 GetRespawnPosition()
    {
        return LastSafePosition;
    }
}