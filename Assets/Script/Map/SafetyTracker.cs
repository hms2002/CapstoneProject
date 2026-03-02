using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement; // 씬 전환 감지를 위해 필요
using UnityGAS;

public class SafetyTracker : MonoBehaviour
{
    [Header("Auto Detection Settings")]
    [Tooltip("이 레이어가 설정된 모든 타일맵을 '땅'으로 인식합니다.")]
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("이 레이어가 설정된 모든 타일맵을 '함정'으로 인식합니다.")]
    [SerializeField] private LayerMask holeLayer;

    [Header("Settings")]
    [SerializeField] private float checkInterval = 0.2f;
    [Tooltip("플레이어 발바닥 위치 보정값 (y: -0.5 ~ -0.8)")]
    [SerializeField] private Vector3 footOffset = new Vector3(0, -0.6f, 0);

    [Header("Unsafe Conditions")]
    [SerializeField] private GameplayTag[] unsafeTags;

    // 자동으로 찾아낸 타일맵들을 저장할 리스트
    private List<Tilemap> groundMaps = new List<Tilemap>();
    private List<Tilemap> holeMaps = new List<Tilemap>();

    public Vector3 LastSafePosition { get; private set; }

    private AbilitySystem abilitySystem;

    private void Awake()
    {
        abilitySystem = GetComponent<AbilitySystem>();

        // 씬 로드 이벤트 등록 (플레이어가 DontDestroyOnLoad여도 대응 가능)
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        // 게임 시작 시 현재 씬의 타일맵들을 찾음
        RefreshTilemaps();

        // 초기 위치 설정 (가장 가까운 땅 타일의 중앙)
        InitializePosition();

        StartCoroutine(TrackSafePositionRoutine());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 씬이 변경될 때마다 타일맵 목록을 갱신
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshTilemaps();
        InitializePosition();
    }

    private void RefreshTilemaps()
    {
        groundMaps.Clear();
        holeMaps.Clear();

        // 현재 씬에 있는 모든 타일맵 컴포넌트를 가져옴
        Tilemap[] allMaps = FindObjectsOfType<Tilemap>(true); // 비활성 객체 포함 여부 선택

        foreach (var map in allMaps)
        {
            // 레이어 마스크 비교 (비트 연산)
            if (IsInLayerMask(map.gameObject.layer, groundLayer))
            {
                groundMaps.Add(map);
            }
            else if (IsInLayerMask(map.gameObject.layer, holeLayer))
            {
                holeMaps.Add(map);
            }
        }

        // 디버깅 로그 (필요시 주석 해제)
        // Debug.Log($"[SafetyTracker] 감지됨 - GroundMaps: {groundMaps.Count}, HoleMaps: {holeMaps.Count}");
    }

    private void InitializePosition()
    {
        // 현재 위치에서 유효한 땅 타일 찾기 시도
        Vector3 currentPos = transform.position;
        if (UpdateSafePositionIfValid(currentPos))
        {
            // 성공적으로 LastSafePosition 갱신됨
        }
        else
        {
            // 실패 시 그냥 현재 위치 사용 (어쩔 수 없음)
            LastSafePosition = currentPos;
        }
    }

    private IEnumerator TrackSafePositionRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(checkInterval);

        while (true)
        {
            yield return wait;

            if (IsStatusUnsafe()) continue;

            // 현재 위치 검사 및 저장
            UpdateSafePositionIfValid(transform.position);
        }
    }

    // 위치가 안전하면 LastSafePosition을 갱신하고 true 반환
    private bool UpdateSafePositionIfValid(Vector3 targetPos)
    {
        Vector3 footPos = targetPos + footOffset;

        // 1. [검사] 어떤 땅 타일맵이라도 해당 위치에 타일이 있는가?
        bool hasGround = false;
        Tilemap validGroundMap = null; // 나중에 좌표 가져올 때 쓸 맵

        foreach (var map in groundMaps)
        {
            Vector3Int cellPos = map.WorldToCell(footPos);
            if (map.HasTile(cellPos))
            {
                hasGround = true;
                validGroundMap = map;
                break; // 땅 하나만 찾으면 OK
            }
        }

        if (!hasGround) return false; // 땅이 없으면 저장 안 함

        // 2. [검사] 어떤 함정 타일맵이라도 해당 위치에 타일이 있는가?
        foreach (var map in holeMaps)
        {
            Vector3Int cellPos = map.WorldToCell(footPos);
            if (map.HasTile(cellPos))
            {
                return false; // 함정 위에 있으면 저장 안 함
            }
        }

        // 3. [성공] 안전지대 확정 -> 해당 타일의 정중앙 좌표 저장
        Vector3Int finalCell = validGroundMap.WorldToCell(footPos);
        LastSafePosition = validGroundMap.GetCellCenterWorld(finalCell);

        return true;
    }

    private bool IsStatusUnsafe()
    {
        if (abilitySystem == null || abilitySystem.TagSystem == null) return false;
        if (unsafeTags == null) return false;

        foreach (var tag in unsafeTags)
        {
            if (abilitySystem.TagSystem.HasTag(tag)) return true;
        }
        return false;
    }

    public Vector3 GetRespawnPosition()
    {
        return LastSafePosition;
    }

    private bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask == (mask | (1 << layer)));
    }

    // [디버깅]
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position + footOffset, 0.1f);
    }
}