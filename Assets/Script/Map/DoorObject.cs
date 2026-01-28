using UnityEngine;
using DG.Tweening;

public class DoorObject : MonoBehaviour, IInteractable
{
    // [변경] Affection 타입 추가
    public enum DoorType
    {
        Normal,     // 그냥 열림
        OneWay,     // 한쪽에서만 열림
        Locked,     // 외부 장치(레버/석상)로만 열림
        Affection   // [New] 호감도 조건 만족 시 자동 개방
    }

    [Header("데이터 (고정형 ID)")]
    public string mapID;
    public string doorID;

    [Header("기본 설정")]
    public DoorType doorType = DoorType.Locked;
    public bool isPermanent = true;

    [Header("호감도 문 전용 설정")]
    public int targetBossID;       // 감시할 보스 ID
    public int requiredAffection;  // 필요 호감도

    [Header("연결 객체")]
    public Transform model;
    public Collider2D obstacleCollider;
    public Transform uiPopupPoint;

    [Header("단방향 문 전용")]
    public Collider2D openZone;
    public Collider2D blockZone;

    public bool IsOpen { get; private set; } = false;

    // =========================================================
    // 초기화 및 이벤트 구독
    // =========================================================

    private void Awake()
    {
        if (string.IsNullOrEmpty(mapID))
            mapID = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (string.IsNullOrEmpty(doorID)) GenerateID();
    }

    private void Start()
    {
        // 1. 이미 저장된 문이면 열기
        if (isPermanent && GameDataManager.Instance.IsShortcutUnlocked(mapID, doorID))
        {
            ForceOpen(true);
            return;
        }

        // 2. 호감도 문이라면? -> 이벤트 구독 및 초기 체크
        if (doorType == DoorType.Affection)
        {
            // (A) 시작하자마자 조건 만족하는지 체크
            CheckAffectionAndAutoOpen(AffectionManager.Instance.GetAffection(targetBossID));

            // (B) 호감도 변경 이벤트 구독 (Update 대체)
            if (AffectionManager.Instance != null)
            {
                AffectionManager.Instance.OnAffectionChanged += OnAffectionChanged;
            }
        }
    }

    private void OnDestroy()
    {
        // 구독 해제 (메모리 누수 방지)
        if (AffectionManager.Instance != null)
        {
            AffectionManager.Instance.OnAffectionChanged -= OnAffectionChanged;
        }
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
    // [핵심] 호감도 자동 개방 로직 (이벤트 방식)
    // =========================================================

    // 호감도가 변경될 때마다 호출됨
    private void OnAffectionChanged(int npcId, int currentAmount)
    {
        if (npcId == targetBossID)
        {
            CheckAffectionAndAutoOpen(currentAmount);
        }
    }

    private void CheckAffectionAndAutoOpen(int currentAmount)
    {
        if (!IsOpen && currentAmount >= requiredAffection)
        {
            // 조건 만족 -> 즉시 개방 및 저장
            Debug.Log($"[Door] 호감도 조건 충족! ({currentAmount}/{requiredAffection}) -> 문 개방");
            ForceOpen(immediate: false, save: true);

            // 열렸으니 구독 해제
            AffectionManager.Instance.OnAffectionChanged -= OnAffectionChanged;
        }
    }

    // =========================================================
    // IInteractable 구현 (플레이어 F키)
    // =========================================================

    public void OnPlayerInteract(TempPlayer player)
    {
        if (IsOpen) return;

        // 호감도 문은 플레이어가 눌러서 여는 게 아님. (살펴보기 용도)
        if (doorType == DoorType.Affection)
        {
            int cur = AffectionManager.Instance.GetAffection(targetBossID);
            Debug.Log($"[살펴보기] {targetBossID}번 보스의 호감도가 부족해 닫혀있다. (현재:{cur}/필요:{requiredAffection})");
            PlayShakeAnimation();
            return;
        }

        // 나머지 문들 처리
        Collider2D playerCol = player.GetComponent<Collider2D>();
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

    public string GetInteractDescription()
    {
        if (IsOpen) return "";
        switch (doorType)
        {
            case DoorType.Affection: return "살펴보기"; // 상호작용 텍스트 변경
            case DoorType.OneWay: return "반대편에서만 열림";
            case DoorType.Locked: return "굳게 잠겨있다";
            default: return "열기";
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

    // =========================================================
    // 문 열기 동작
    // =========================================================

    public void ForceOpen(bool immediate = false, bool save = false)
    {
        if (IsOpen) return;
        IsOpen = true;

        if (save) GameDataManager.Instance.UnlockShortcut(mapID, doorID);

        if (obstacleCollider != null) obstacleCollider.enabled = false;
        if (openZone != null) openZone.enabled = false;
        if (blockZone != null) blockZone.enabled = false;

        if (immediate)
        {
            if (model != null) model.localPosition += Vector3.up * 3f;
        }
        else
        {
            if (model != null) model.DOLocalMoveY(3f, 1f).SetRelative().SetEase(Ease.OutQuart);
        }
    }

    public void PlayShakeAnimation()
    {
        if (model != null) model.DOShakePosition(0.5f, 0.1f);
    }

    // 필수 인터페이스 구현
    public void OnPlayerNearby() { }
    public void OnPlayerLeave() { }
    public void OnHighlight() { }
    public void OnUnHighlight() { }
    public bool CanInteract(TempPlayer player) => !IsOpen;
    public InteractState GetInteractType() => InteractState.Idle;
    public void GetInteract(string text) { }
}