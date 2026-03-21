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
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(doorID) && !UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                GenerateID();
            };
        }
    }

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

        if (string.IsNullOrEmpty(doorID))
            Debug.LogError($"[DoorObject] 치명적 에러: '{gameObject.name}'의 Door ID가 없습니다!");

        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        // 이미 저장된 문이면 즉시 열린 상태로 만들기
        // (GameDataManager가 구현되어 있다는 가정 하의 코드)
        if (isPermanent && GameDataManager.Instance != null && GameDataManager.Instance.IsShortcutUnlocked(mapID, doorID))
        {
            ForceOpen(immediate: true);
        }
    }

    public void OnPlayerInteract(IPlayerInteractor player)
    {
        if (IsOpen) return;

        Collider2D playerCol = player.Transform.GetComponent<Collider2D>();

        // 열림 조건 충족 시
        if (playerCol != null && CheckConditionByCollider(playerCol))
        {
            ForceOpen(immediate: false, save: isPermanent);
        }
        else // 열림 조건 불충족 시 (잠김)
        {
            PlayShakeAnimation();

            // 플레이어에게 잠긴 문 대사 출력을 요청합니다!
            SampleTopDownPlayer playerScript = player.Transform.GetComponent<SampleTopDownPlayer>();
            if (playerScript != null)
            {
                playerScript.SpeakSituation(PlayerSpeechSituationEnum.DoorLocked);
            }
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

    // IInteractable 인터페이스 구현
    public void OnPlayerNearby() { }
    public void OnPlayerLeave() { }
    public void OnHighlight() { }
    public void OnUnHighlight() { }
    public InteractState GetInteractType() => InteractState.Idle;
    public void GetInteract(string text) { }
    public bool CanInteract(IPlayerInteractor player) => !IsOpen;
    public string GetInteractDescription() => IsOpen ? "" : (doorType == DoorType.Locked ? "굳게 잠겨있다" : "열기");
}