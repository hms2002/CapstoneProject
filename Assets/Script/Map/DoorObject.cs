using UnityEngine;
using DG.Tweening;

public class DoorObject : MonoBehaviour, IInteractable
{
    public enum DoorType
    {
        Normal,     // 그냥 열림
        OneWay,     // 한쪽에서만 열림
        Locked      // 외부 장치(레버/석상)로만 열림
    }

    [Header("데이터 (고정형 ID)")]
    public string mapID;
    public string doorID;

    [Header("기본 설정")]
    public DoorType doorType = DoorType.Locked;
    public bool isPermanent = true;

    [Header("연결 객체")]
    public Transform model;
    public Animator animator;
    public Collider2D obstacleCollider;

    [Header("단방향 문 전용")]
    public Collider2D openZone;
    public Collider2D blockZone;

    public bool IsOpen { get; private set; } = false;

#if UNITY_EDITOR
    // 에디터에서 프리팹을 씬에 배치하거나 값이 바뀔 때 1회 자동 발급
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(doorID) && !UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                GenerateID(); // 아래 복구된 함수를 호출합니다.
            };
        }
    }

    // [복구 완료] MapTool(에디터 스크립트)에서 호출할 수 있도록 다시 살려두었습니다!
    public void GenerateID()
    {
        string cleanName = name.Replace("(Clone)", "").Trim();
        string guid = System.Guid.NewGuid().ToString().Substring(0, 8);
        doorID = $"{cleanName}_{guid}";
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private void Awake()
    {
        if (string.IsNullOrEmpty(mapID))
            mapID = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // 런타임에는 절대 자동 발급하지 않음. 에러로 경고만 띄움.
        if (string.IsNullOrEmpty(doorID))
            Debug.LogError($"[DoorObject] 치명적 에러: '{gameObject.name}'의 Door ID가 없습니다!");

        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        // 이미 저장된 문이면 즉시 열린 상태로 만들기
        if (isPermanent && GameDataManager.Instance != null && GameDataManager.Instance.IsShortcutUnlocked(mapID, doorID))
        {
            ForceOpen(immediate: true);
        }
    }

    public void OnPlayerInteract(IPlayerInteractor player)
    {
        if (IsOpen) return;

        Collider2D playerCol = player.Transform.GetComponent<Collider2D>();
        if (playerCol != null && CheckConditionByCollider(playerCol))
        {
            ForceOpen(immediate: false, save: isPermanent);
        }
        else
        {
            PlayShakeAnimation();
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

    public void ForceOpen(bool immediate = false, bool save = false)
    {
        if (IsOpen) return;
        IsOpen = true;

        if (save && GameDataManager.Instance != null)
            GameDataManager.Instance.UnlockShortcut(mapID, doorID);

        if (openZone != null) openZone.enabled = false;
        if (blockZone != null) blockZone.enabled = false;

        if (animator != null)
        {
            if (immediate)
            {
                animator.Play("Open", 0, 1.0f);
                DisableObstacle();
            }
            else animator.SetTrigger("Open");
        }
        else
        {
            if (model != null)
            {
                if (immediate) model.localPosition += Vector3.up * 3f;
                else model.DOLocalMoveY(3f, 1f).SetRelative().SetEase(Ease.OutQuart);
            }
            DisableObstacle();
        }
    }

    public void OnOpenAnimationComplete() => DisableObstacle();
    private void DisableObstacle() { if (obstacleCollider != null) obstacleCollider.enabled = false; }
    public void PlayShakeAnimation() { if (model != null) model.DOShakePosition(0.5f, 0.1f); }

    // IInteractable 
    public void OnPlayerNearby() { }
    public void OnPlayerLeave() { }
    public void OnHighlight() { }
    public void OnUnHighlight() { }
    public InteractState GetInteractType() => InteractState.Idle;
    public void GetInteract(string text) { }
    public bool CanInteract(IPlayerInteractor player) => !IsOpen;
    public string GetInteractDescription() => IsOpen ? "" : (doorType == DoorType.Locked ? "굳게 잠겨있다" : "열기");
}