using UnityEngine;
using DG.Tweening;

public class DoorObject : InteractableBase
{
    public enum DoorType
    {
        Normal,
        OneWay,
        Locked
    }

    [Header("데이터 (고정형 ID)")]
    public string mapID;
    public string doorID;

    [Header("기본 설정")]
    public DoorType doorType = DoorType.Locked;
    public bool isPermanent = true;

    [Header("프롬프트")]
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string openPromptText = "열기";
    [SerializeField] private string lockedPromptText = "굳게 잠겨있다";

    [Header("연결 객체")]
    public Transform model;
    public Animator animator;
    public Collider2D obstacleCollider;

    [Header("단방향 문 전용")]
    public Collider2D openZone;
    public Collider2D blockZone;

    public bool IsOpen { get; private set; }

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

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        if (isPermanent && ShortcutProgressService.Instance != null && ShortcutProgressService.Instance.IsShortcutUnlocked(mapID, doorID))
            ForceOpen(immediate: true);
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (IsOpen || player == null)
            return;

        Collider2D playerCol = player.Transform.GetComponent<Collider2D>();

        if (playerCol != null && CheckConditionByCollider(playerCol))
        {
            ForceOpen(immediate: false, save: isPermanent);
        }
        else
        {
            PlayShakeAnimation();

        PlayerInteractor2D playerScript = player.Transform.GetComponent<PlayerInteractor2D>();
            if (playerScript != null)
                playerScript.SpeakSituation(PlayerSpeechSituationEnum.DoorLocked);
        }
    }

    private bool CheckConditionByCollider(Collider2D playerCol)
    {
        if (doorType == DoorType.Normal)
            return true;

        if (doorType == DoorType.OneWay)
        {
            if (openZone != null && openZone.IsTouching(playerCol)) return true;
            if (blockZone != null && blockZone.IsTouching(playerCol)) return false;
        }

        return false;
    }

    public void ForceOpen(bool immediate = false, bool save = false)
    {
        if (IsOpen)
            return;

        IsOpen = true;

        if (save && ShortcutProgressService.Instance != null)
            ShortcutProgressService.Instance.UnlockShortcut(mapID, doorID);

        if (openZone != null) openZone.enabled = false;
        if (blockZone != null) blockZone.enabled = false;

        if (animator != null)
        {
            if (immediate)
            {
                animator.Play("Open", 0, 1.0f);
                DisableObstacle();
            }
            else
            {
                animator.SetTrigger("Open");
            }
        }
        else
        {
            if (model != null)
            {
                if (immediate)
                    model.localPosition += Vector3.up * 3f;
                else
                    model.DOLocalMoveY(3f, 1f).SetRelative().SetEase(Ease.OutQuart);
            }

            DisableObstacle();
        }
    }

    public void OnOpenAnimationComplete() => DisableObstacle();

    private void DisableObstacle()
    {
        if (obstacleCollider != null)
            obstacleCollider.enabled = false;
    }

    public void PlayShakeAnimation()
    {
        if (model != null)
            model.DOShakePosition(0.5f, 0.1f);
    }

    public override InteractState GetInteractType() => InteractState.Idle;
    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;
    public override bool CanInteract(IPlayerInteractor player) => player != null && player.CurrentState == InteractState.Idle && !IsOpen;
    public override string GetInteractDescription() => IsOpen ? string.Empty : (doorType == DoorType.Locked ? lockedPromptText : openPromptText);
}
