using UnityEngine;
using DG.Tweening; // Shake 효과를 위해 유지

public class DoorObject : MonoBehaviour, IInteractable
{
    public enum DoorType
    {
        Normal,     // 그냥 열림
        OneWay,     // 한쪽에서만 열림
        Locked,     // 외부 장치(레버/석상)로만 열림
        Affection   // 호감도 조건 만족 시 자동 개방
    }

    [Header("데이터 (고정형 ID)")]
    public string mapID;
    public string doorID;

    [Header("기본 설정")]
    public DoorType doorType = DoorType.Locked;
    public bool isPermanent = true;

    [Header("호감도 문 전용 설정")]
    public int targetBossID;
    public int requiredAffection;

    [Header("연결 객체")]
    public Transform model;         // (이제 Animator가 제어하므로 필수 아님, Shake용으로 사용)
    public Animator animator;       // [New] 애니메이터 연결
    public Collider2D obstacleCollider; // 길막용 콜라이더
    public Transform uiPopupPoint;

    [Header("단방향 문 전용")]
    public Collider2D openZone;
    public Collider2D blockZone;

    public bool IsOpen { get; private set; } = false;

    // =========================================================
    // 초기화
    // =========================================================

    private void Awake()
    {
        if (string.IsNullOrEmpty(mapID))
            mapID = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (string.IsNullOrEmpty(doorID)) GenerateID();

        // Animator 자동 찾기 (없으면 수동 할당 필요)
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        // 1. 이미 저장된 문이면 -> 애니메이션 없이 즉시 열린 상태로 만들기
        if (isPermanent && GameDataManager.Instance.IsShortcutUnlocked(mapID, doorID))
        {
            ForceOpen(immediate: true);
            return;
        }

        // 2. 호감도 문 체크
        if (doorType == DoorType.Affection)
        {
            CheckAffectionAndAutoOpen(AffectionManager.Instance.GetAffection(targetBossID));
            if (AffectionManager.Instance != null)
                AffectionManager.Instance.OnAffectionChanged += OnAffectionChanged;
        }
    }

    private void OnDestroy()
    {
        if (AffectionManager.Instance != null)
            AffectionManager.Instance.OnAffectionChanged -= OnAffectionChanged;
    }

    private void Reset() { GenerateID(); }

    [ContextMenu("ID 새로 발급")]
    public void GenerateID()
    {
        string cleanName = name.Replace("(Clone)", "").Trim();
        string guid = System.Guid.NewGuid().ToString().Substring(0, 8);
        doorID = $"{cleanName}_{guid}";
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    // =========================================================
    // 호감도 로직
    // =========================================================

    private void OnAffectionChanged(int npcId, int currentAmount)
    {
        if (npcId == targetBossID) CheckAffectionAndAutoOpen(currentAmount);
    }

    private void CheckAffectionAndAutoOpen(int currentAmount)
    {
        if (!IsOpen && currentAmount >= requiredAffection)
        {
            Debug.Log($"[Door] 호감도 조건 충족! 문 개방");
            ForceOpen(immediate: false, save: true);
            AffectionManager.Instance.OnAffectionChanged -= OnAffectionChanged;
        }
    }

    // =========================================================
    // 상호작용 (IInteractable)
    // =========================================================

    public void OnPlayerInteract(IPlayerInteractor player)
    {
        Debug.Log($"[Door] 상호작용 시도됨! 문 상태: {IsOpen}, 타입: {doorType}");

        if (IsOpen) return;

        if (doorType == DoorType.Affection)
        {
            int cur = AffectionManager.Instance.GetAffection(targetBossID);
            Debug.Log($"[살펴보기] 호감도 부족 (현재:{cur}/필요:{requiredAffection})");
            PlayShakeAnimation();
            return;
        }

        Collider2D playerCol = player.Transform.GetComponent<Collider2D>();
        if (playerCol != null && CheckConditionByCollider(playerCol))
        {
            ForceOpen(immediate: false, save: isPermanent);
        }
        else
        {
            PlayShakeAnimation();
            Debug.Log(GetInteractDescription());
        }
    }

    private bool CheckConditionByCollider(Collider2D playerCol)
    {
        if (doorType == DoorType.Normal) return true;
        if (doorType == DoorType.OneWay)
        {
            if (openZone != null && openZone.IsTouching(playerCol)) return true;
            if (blockZone != null && blockZone.IsTouching(playerCol)) return false;
        }
        return false;
    }

    public string GetInteractDescription()
    {
        if (IsOpen) return "";
        switch (doorType)
        {
            case DoorType.Affection: return "살펴보기";
            case DoorType.OneWay: return "반대편에서만 열림";
            case DoorType.Locked: return "굳게 잠겨있다";
            default: return "열기";
        }
    }

    // =========================================================
    // [핵심] 문 열기 동작 (Animator 사용)
    // =========================================================

    public void ForceOpen(bool immediate = false, bool save = false)
    {
        if (IsOpen) return;
        IsOpen = true;

        if (save) GameDataManager.Instance.UnlockShortcut(mapID, doorID);

        // 상호작용 존 끄기 (더 이상 상호작용 못하게)
        if (openZone != null) openZone.enabled = false;
        if (blockZone != null) blockZone.enabled = false;

        if (animator != null)
        {
            if (immediate)
            {
                // 즉시 열림: 'Open' 상태로 바로 점프 (애니메이션 재생 X)
                animator.Play("Open", 0, 1.0f); // 1.0f = 끝지점
                DisableObstacle(); // 즉시 콜라이더 끄기
            }
            else
            {
                // 애니메이션 재생: 파라미터 Trigger 발동
                animator.SetTrigger("Open");
                // 주의: 콜라이더는 애니메이션 이벤트(OnOpenAnimationComplete)에서 꺼짐
            }
        }
        else
        {
            // Animator가 없을 경우를 대비한 예비 로직 (Tween 사용)
            if (model != null)
            {
                if (immediate) model.localPosition += Vector3.up * 3f;
                else model.DOLocalMoveY(3f, 1f).SetRelative().SetEase(Ease.OutQuart);
            }
            DisableObstacle();
        }
    }

    // [중요] 애니메이션 클립의 마지막 프레임에 Event로 이 함수를 추가해야 함!
    public void OnOpenAnimationComplete()
    {
        DisableObstacle();
    }

    private void DisableObstacle()
    {
        if (obstacleCollider != null) obstacleCollider.enabled = false;
    }

    public void PlayShakeAnimation()
    {
        if (model != null) model.DOShakePosition(0.5f, 0.1f);
    }

    // IInteractable 필수 구현
    public void OnPlayerNearby() { }
    public void OnPlayerLeave() { }
    public void OnHighlight() { }
    public void OnUnHighlight() { }
    public InteractState GetInteractType() => InteractState.Idle;
    public void GetInteract(string text) { }
    public bool CanInteract(IPlayerInteractor player) => !IsOpen;
}