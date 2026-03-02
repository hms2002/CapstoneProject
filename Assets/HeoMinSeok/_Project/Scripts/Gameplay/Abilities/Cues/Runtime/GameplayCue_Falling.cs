using UnityEngine;
using UnityEngine.Tilemaps;
using UnityGAS;
using DG.Tweening;

public class GameplayCue_Falling : GameplayCueNotify
{
    [Header("Visual Settings")]
    [SerializeField] private float animDuration = 0.8f;
    [SerializeField] private Ease fallEase = Ease.InBack;
    [SerializeField] private float rotateSpeed = 720f;
    [Tooltip("플레이어 발바닥 오프셋 (보통 -0.5 ~ -0.8)")]
    [SerializeField] private Vector3 footOffset = new Vector3(0, -0.5f, 0);

    private Vector3 originalScale;
    private Quaternion originalRotation;
    private RigidbodyType2D originalBodyType;
    private Vector2 originalVelocity;

    private Tween scaleTween;
    private Tween rotateTween;
    private Tween moveTween;

    public override void OnAdd(GameplayCueParams p)
    {
        Debug.Log($"[Cue] OnAdd 호출됨! Target: {p.Target}, Causer: {p.Causer}"); // 로그 확인

        GameObject target = p.Target;
        GameObject trap = p.Causer; // HoleTrap 오브젝트 (Tilemap이 붙어있어야 함)

        if (target == null)
        {
            Debug.LogError("[Cue] Target이 없습니다!");
            return;
        }

        // 1. 상태 저장 및 물리 끄기
        SaveAndDisablePhysics(target);

        // 2. [핵심] 떨어질 타일의 정확한 중심 찾기
        Vector3 targetHolePos = GetCorrectHolePosition(target, trap);

        // 3. 연출 시작
        scaleTween = target.transform.DOScale(Vector3.zero, animDuration).SetEase(fallEase);
        rotateTween = target.transform.DORotate(new Vector3(0, 0, rotateSpeed), animDuration, RotateMode.FastBeyond360)
            .SetEase(Ease.InCubic).SetLoops(-1, LoopType.Incremental);
        moveTween = target.transform.DOMove(targetHolePos, animDuration).SetEase(Ease.OutQuart);
    }

    private Vector3 GetCorrectHolePosition(GameObject player, GameObject trap)
    {
        Vector3 playerPos = player.transform.position;
        if (trap == null) return playerPos;

        // 함정 오브젝트에서 타일맵 가져오기
        Tilemap tilemap = trap.GetComponent<Tilemap>();
        if (tilemap == null) tilemap = trap.GetComponentInChildren<Tilemap>();

        // 타일맵이 없는 단일 함정이면 그냥 함정 위치 리턴
        if (tilemap == null) return trap.transform.position;

        // A. 플레이어 발밑 좌표 확인
        Vector3 footPos = playerPos + footOffset;
        Vector3Int cellPos = tilemap.WorldToCell(footPos);

        // B. 해당 좌표에 타일이 있으면 그게 정답
        if (tilemap.HasTile(cellPos))
        {
            return tilemap.GetCellCenterWorld(cellPos);
        }

        // C. [오류 수정] 발밑에 타일이 없다면? (가장자리를 밟은 경우)
        // 주변 8방향(혹은 4방향)을 검색해서 가장 가까운 '함정 타일'을 찾는다.
        Vector3Int[] neighbors = new Vector3Int[]
        {
            cellPos + Vector3Int.up,
            cellPos + Vector3Int.down,
            cellPos + Vector3Int.left,
            cellPos + Vector3Int.right,
             // 대각선도 체크 (필요시)
            cellPos + new Vector3Int(1, 1, 0),
            cellPos + new Vector3Int(1, -1, 0),
            cellPos + new Vector3Int(-1, 1, 0),
            cellPos + new Vector3Int(-1, -1, 0)
        };

        float minDistance = float.MaxValue;
        Vector3 bestPos = playerPos; // 못 찾으면 제자리

        foreach (var neighbor in neighbors)
        {
            if (tilemap.HasTile(neighbor))
            {
                Vector3 centerWorld = tilemap.GetCellCenterWorld(neighbor);
                float dist = Vector3.Distance(footPos, centerWorld);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestPos = centerWorld;
                }
            }
        }

        return bestPos;
    }

    private void SaveAndDisablePhysics(GameObject target)
    {
        originalScale = target.transform.localScale;
        originalRotation = target.transform.localRotation;

        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            originalBodyType = rb.bodyType;
            originalVelocity = rb.linearVelocity;
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    public override void OnRemove(GameplayCueParams p)
    {
        // ... (기존과 동일: 트윈 킬 및 상태 복구) ...
        GameObject target = p.Target;
        if (target == null) return;

        scaleTween?.Kill(); rotateTween?.Kill(); moveTween?.Kill();

        if (originalScale.sqrMagnitude < 0.01f) originalScale = Vector3.one;
        target.transform.localScale = originalScale;
        target.transform.localRotation = originalRotation;

        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = originalBodyType;
            rb.linearVelocity = Vector2.zero;
        }
    }
}