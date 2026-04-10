using UnityEngine;
using DG.Tweening;
using UnityGAS;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class DoorObject : InteractableBase
{
    public enum DoorType
    {
        Normal,
        OneWay,
        Locked
    }

    public enum OneWayOpenSide
    {
        LocalUp,
        LocalDown,
        LocalRight,
        LocalLeft
    }

    [Header("데이터 (고정형 ID)")]
    public string mapID;
    public string doorID;

    [Header("기본 설정")]
    public DoorType doorType = DoorType.Locked;
    public bool isPermanent = true;

    [Header("프롬프트")]
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private bool useRotationIndependentPromptAnchor = true;
    [SerializeField] private Vector3 rotationIndependentPromptOffset = new Vector3(0f, 0.91f, 0f);
    [SerializeField] private string openPromptText = "열기";
    [SerializeField] private string lockedPromptText = "굳게 잠겨있다";

    [Header("연결 객체")]
    public Transform model;
    public Animator animator;
    public Collider2D obstacleCollider;

    [Header("Presentation")]
    [SerializeField] private Transform presentationAnchor;
    [SerializeField] private WorldObjectPresentationDefinition openPresentation = new();

    [Header("단방향 문 전용")]
    [SerializeField] private OneWayOpenSide oneWayOpenSide = OneWayOpenSide.LocalUp;
    [Tooltip("문 중앙 판정선 근처의 애매한 구간. 보통 0.02 ~ 0.1 정도를 권장하며 최대 1까지 허용한다.")]
    [SerializeField, Range(0f, 1f)] private float oneWayOpenThreshold = 0.05f;

    public bool IsOpen { get; private set; }
    private Transform runtimePromptAnchor;
    private Tween shakeTween;
    private Vector3 closedModelLocalPosition;
    private bool hasClosedModelLocalPosition;
    private const float VerticalPromptAngleTolerance = 1f;
    private WorldObjectPresentationRuntime openPresentationRuntime;

#if UNITY_EDITOR
    private const float DefaultOneWayGizmoWidth = 1.2f;
    private const float DefaultOneWayGizmoDepth = 1.2f;
    private const float OneWayGizmoPadding = 0.35f;

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

        oneWayOpenThreshold = Mathf.Clamp(oneWayOpenThreshold, 0f, 1f);
        EnforceIntrinsicConfiguration(hasLinkedShortcut: HasLinkedShortcut());
        EditorSyncConfigurationFromLinkedShortcuts();
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
        EnforceIntrinsicConfiguration(hasLinkedShortcut: HasLinkedShortcut());

        if (string.IsNullOrEmpty(mapID))
            mapID = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (string.IsNullOrEmpty(doorID))
            Debug.LogError($"[DoorObject] 치명적 에러: '{gameObject.name}'의 Door ID가 없습니다!");

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        openPresentationRuntime = new WorldObjectPresentationRuntime(gameObject);

        if (model != null)
        {
            closedModelLocalPosition = model.localPosition;
            hasClosedModelLocalPosition = true;
        }
    }

    private void Start()
    {
        if (isPermanent && ShortcutProgressService.Instance != null && ShortcutProgressService.Instance.IsShortcutUnlocked(mapID, doorID))
            ForceOpen(immediate: true, playPresentation: false);
    }

    private void LateUpdate()
    {
        if (runtimePromptAnchor != null)
            UpdateRuntimePromptAnchorPosition();
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (IsOpen || player == null)
            return;

        if (CanPlayerOpenDoor(player))
        {
            ForceOpen(
                immediate: false,
                save: isPermanent,
                instigator: player.Transform != null ? player.Transform.gameObject : null);
        }
        else
        {
            PlayShakeAnimation();

            PlayerInteractor2D playerScript = player.Transform.GetComponent<PlayerInteractor2D>();
            if (playerScript != null)
                playerScript.SpeakSituation(GetFailedInteractSpeechSituation());
        }
    }

    private bool CanPlayerOpenDoor(IPlayerInteractor player)
    {
        if (player == null)
            return false;

        if (doorType == DoorType.Normal)
            return true;

        if (doorType == DoorType.OneWay)
            return IsPlayerOnAllowedOneWaySide(player.Transform.position);

        return false;
    }

    private PlayerSpeechSituationEnum GetFailedInteractSpeechSituation()
    {
        return doorType == DoorType.OneWay
            ? PlayerSpeechSituationEnum.OneWayDoorLocked
            : PlayerSpeechSituationEnum.DoorLocked;
    }

    private bool IsPlayerOnAllowedOneWaySide(Vector3 playerWorldPosition)
    {
        Vector3 referencePoint = ResolvePlayerReferencePoint(playerWorldPosition);
        Vector3 planeOrigin = ResolveDoorPlaneOrigin();
        Vector3 allowedDirection = GetAllowedWorldDirection();

        float signedDistance = Vector3.Dot(referencePoint - planeOrigin, allowedDirection);
        return signedDistance > oneWayOpenThreshold;
    }

    private Vector3 ResolvePlayerReferencePoint(Vector3 fallbackWorldPosition)
    {
        if (PlayerInteractor2D.Instance != null)
        {
            Collider2D playerCollider = PlayerInteractor2D.Instance.GetComponent<Collider2D>();
            if (playerCollider != null)
            {
                Bounds bounds = playerCollider.bounds;
                return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }
        }

        return fallbackWorldPosition;
    }

    private Vector3 ResolveDoorPlaneOrigin()
    {
        if (obstacleCollider != null)
        {
            Bounds bounds = obstacleCollider.bounds;
            return new Vector3(bounds.center.x, bounds.center.y, transform.position.z);
        }

        return transform.position;
    }

    private Vector3 GetAllowedWorldDirection()
    {
        Vector3 localDirection = oneWayOpenSide switch
        {
            OneWayOpenSide.LocalUp => Vector3.up,
            OneWayOpenSide.LocalDown => Vector3.down,
            OneWayOpenSide.LocalRight => Vector3.right,
            OneWayOpenSide.LocalLeft => Vector3.left,
            _ => Vector3.up
        };

        return transform.TransformDirection(localDirection).normalized;
    }

    public void ForceOpen(bool immediate = false, bool save = false, GameObject instigator = null, bool playPresentation = true)
    {
        if (IsOpen)
            return;

        IsOpen = true;

        if (save && ShortcutProgressService.Instance != null)
            ShortcutProgressService.Instance.UnlockShortcut(mapID, doorID);

        ResetModelAfterShake();
        if (playPresentation)
            PlayOpenPresentation(instigator);

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

    private void PlayOpenPresentation(GameObject instigator)
    {
        openPresentationRuntime?.PlayExecuteOnly(
            openPresentation,
            instigator: instigator,
            target: gameObject,
            anchor: ResolvePresentationAnchor(),
            sourceObject: this);
    }

    public void PlayShakeAnimation()
    {
        if (model == null)
            return;

        ResetModelAfterShake();
        shakeTween = model.DOShakePosition(0.5f, 0.1f).OnComplete(ResetModelAfterShake);
    }

    public override InteractState GetInteractType() => InteractState.Idle;
    public override Transform GetPromptAnchor()
    {
        if (!ShouldUseRotationIndependentPromptAnchor())
            return promptAnchor != null ? promptAnchor : transform;

        EnsureRuntimePromptAnchor();
        UpdateRuntimePromptAnchorPosition();
        return runtimePromptAnchor != null ? runtimePromptAnchor : (promptAnchor != null ? promptAnchor : transform);
    }
    public override bool CanInteract(IPlayerInteractor player)
    {
        if (player == null || player.CurrentState != InteractState.Idle || IsOpen)
            return false;
        return true;
    }

    public override string GetInteractDescription() => IsOpen ? string.Empty : (doorType == DoorType.Locked ? lockedPromptText : openPromptText);

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (doorType != DoorType.OneWay)
            return;

        if (UnityEditor.Selection.activeGameObject == gameObject)
            return;

        DrawOneWayGizmo(selected: false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawPromptAnchorGizmo();
        DrawOneWayGizmo(selected: true);
    }

    private void DrawOneWayGizmo(bool selected)
    {
        if (doorType != DoorType.OneWay)
            return;

        Vector3 origin = ResolveDoorPlaneOrigin();
        Vector3 allowedDirection = GetAllowedWorldDirection();
        Vector3 blockedDirection = -allowedDirection;
        Vector3 perpendicular = Vector3.Cross(allowedDirection, Vector3.forward).normalized;

        float planeWidth = ResolveOneWayGizmoWidth(perpendicular);
        float zoneDepth = ResolveOneWayGizmoDepth(allowedDirection);
        Vector3 thresholdOrigin = origin + allowedDirection * oneWayOpenThreshold;

        Color allowedFill = selected
            ? new Color(0.2f, 1f, 0.35f, 0.28f)
            : new Color(0.2f, 1f, 0.35f, 0.12f);
        Color blockedFill = selected
            ? new Color(1f, 0.35f, 0.35f, 0.28f)
            : new Color(1f, 0.35f, 0.35f, 0.12f);

        DrawOneWayZone(
            origin + allowedDirection * (oneWayOpenThreshold + (zoneDepth * 0.5f)),
            allowedDirection,
            planeWidth,
            zoneDepth,
            allowedFill);

        DrawOneWayZone(
            origin + blockedDirection * (zoneDepth * 0.5f),
            allowedDirection,
            planeWidth,
            zoneDepth,
            blockedFill);

        Gizmos.color = selected ? new Color(1f, 1f, 1f, 0.95f) : new Color(1f, 1f, 1f, 0.6f);
        Gizmos.DrawLine(origin - perpendicular * (planeWidth * 0.5f), origin + perpendicular * (planeWidth * 0.5f));

        Gizmos.color = selected ? new Color(1f, 0.9f, 0.2f, 0.95f) : new Color(1f, 0.9f, 0.2f, 0.55f);
        Gizmos.DrawLine(
            thresholdOrigin - perpendicular * (planeWidth * 0.5f),
            thresholdOrigin + perpendicular * (planeWidth * 0.5f));
        Gizmos.DrawSphere(thresholdOrigin, selected ? 0.07f : 0.05f);

        Gizmos.color = selected ? new Color(0.2f, 1f, 0.35f, 1f) : new Color(0.2f, 1f, 0.35f, 0.7f);
        Gizmos.DrawLine(origin, origin + allowedDirection * (zoneDepth + 0.35f));

        Gizmos.color = selected ? new Color(1f, 0.35f, 0.35f, 1f) : new Color(1f, 0.35f, 0.35f, 0.7f);
        Gizmos.DrawLine(origin, origin + blockedDirection * (zoneDepth + 0.35f));

        if (!selected)
            return;

        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(origin + allowedDirection * (zoneDepth + 0.45f), $"OPEN  {oneWayOpenSide}");
        UnityEditor.Handles.Label(origin + blockedDirection * (zoneDepth + 0.45f), "BLOCK");
        UnityEditor.Handles.Label(thresholdOrigin + perpendicular * ((planeWidth * 0.5f) + 0.15f), $"Threshold {oneWayOpenThreshold:0.00}");
    }

    private void DrawOneWayZone(Vector3 center, Vector3 allowedDirection, float width, float depth, Color fillColor)
    {
        Quaternion rotation = Quaternion.LookRotation(Vector3.forward, allowedDirection);
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.color = fillColor;
        Gizmos.DrawCube(Vector3.zero, new Vector3(width, depth, 0.02f));

        Gizmos.color = new Color(fillColor.r, fillColor.g, fillColor.b, Mathf.Clamp01(fillColor.a + 0.35f));
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(width, depth, 0.02f));

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private float ResolveOneWayGizmoWidth(Vector3 perpendicular)
    {
        if (obstacleCollider == null)
            return DefaultOneWayGizmoWidth;

        Bounds bounds = obstacleCollider.bounds;
        float projectedWidth =
            Mathf.Abs(perpendicular.x) * bounds.size.x +
            Mathf.Abs(perpendicular.y) * bounds.size.y;

        return Mathf.Max(DefaultOneWayGizmoWidth, projectedWidth + OneWayGizmoPadding);
    }

    private float ResolveOneWayGizmoDepth(Vector3 allowedDirection)
    {
        if (obstacleCollider == null)
            return DefaultOneWayGizmoDepth;

        Bounds bounds = obstacleCollider.bounds;
        float projectedDepth =
            Mathf.Abs(allowedDirection.x) * bounds.size.x +
            Mathf.Abs(allowedDirection.y) * bounds.size.y;

        return Mathf.Max(DefaultOneWayGizmoDepth, projectedDepth + OneWayGizmoPadding);
    }

    private void DrawPromptAnchorGizmo()
    {
        Vector3 promptPosition = GetCurrentPromptWorldPosition();

        Gizmos.color = new Color(0.25f, 0.9f, 1f, 0.95f);
        Gizmos.DrawLine(transform.position, promptPosition);
        Gizmos.DrawSphere(promptPosition, 0.06f);

        UnityEditor.Handles.color = new Color(0.25f, 0.9f, 1f, 1f);
        UnityEditor.Handles.Label(promptPosition + Vector3.up * 0.12f, "Prompt");
    }
#endif

    private void EnsureRuntimePromptAnchor()
    {
        if (runtimePromptAnchor != null)
            return;

        GameObject anchorObject = new GameObject($"{name}_RuntimePromptAnchor");
        anchorObject.hideFlags = HideFlags.HideAndDontSave;
        runtimePromptAnchor = anchorObject.transform;
    }

    private void UpdateRuntimePromptAnchorPosition()
    {
        if (!ShouldUseRotationIndependentPromptAnchor() || runtimePromptAnchor == null)
            return;

        runtimePromptAnchor.position = GetCurrentPromptWorldPosition();
    }

    private Vector3 GetCurrentPromptWorldPosition()
    {
        if (ShouldUseRotationIndependentPromptAnchor())
            return transform.position + GetAppliedRotationIndependentPromptOffset();

        return promptAnchor != null ? promptAnchor.position : transform.position;
    }

    private Vector3 GetAppliedRotationIndependentPromptOffset()
    {
        Vector3 appliedOffset = rotationIndependentPromptOffset;

        if (GetSignedDoorZRotation() < 0f)
            appliedOffset.x = -appliedOffset.x;

        return appliedOffset;
    }

    private bool ShouldUseRotationIndependentPromptAnchor()
    {
        return useRotationIndependentPromptAnchor && IsVerticalDoorRotation();
    }

    private bool IsVerticalDoorRotation()
    {
        float zRotation = GetSignedDoorZRotation();
        return Mathf.Abs(Mathf.Abs(zRotation) - 90f) <= VerticalPromptAngleTolerance;
    }

    private float GetSignedDoorZRotation()
    {
        return NormalizeSignedAngle(transform.eulerAngles.z);
    }

    public void ApplyConfigurationFromShortcut(DoorType requiredDoorType, bool requiredDoorIsPermanent, Object source = null)
    {
        bool changed = false;

        if (doorType != requiredDoorType)
        {
            doorType = requiredDoorType;
            changed = true;
        }

        if (isPermanent != requiredDoorIsPermanent)
        {
            isPermanent = requiredDoorIsPermanent;
            changed = true;
        }

#if UNITY_EDITOR
        MarkDirtyIfNeeded(changed);
#endif
    }

#if UNITY_EDITOR
    public void EditorSyncConfigurationFromLinkedShortcuts()
    {
        if (this == null)
            return;

        ShortcutBase[] shortcuts = FindObjectsOfType<ShortcutBase>(true);
        ShortcutBase linkedShortcut = null;
        DoorType? resolvedDoorType = null;
        bool? resolvedIsPermanent = null;

        for (int i = 0; i < shortcuts.Length; i++)
        {
            ShortcutBase shortcut = shortcuts[i];
            if (shortcut == null || shortcut.TargetDoor != this)
                continue;

            if (!shortcut.TryGetRequiredDoorConfiguration(out DoorType shortcutDoorType, out bool shortcutIsPermanent))
                continue;

            if (linkedShortcut == null)
            {
                linkedShortcut = shortcut;
                resolvedDoorType = shortcutDoorType;
                resolvedIsPermanent = shortcutIsPermanent;
                continue;
            }

            if (resolvedDoorType != shortcutDoorType || resolvedIsPermanent != shortcutIsPermanent)
            {
                Debug.LogWarning(
                    $"[DoorObject] Conflicting shortcut configuration sources found on '{name}'. " +
                    $"Keeping '{linkedShortcut.GetType().Name}' and ignoring '{shortcut.GetType().Name}'.",
                    this);
            }
        }

        if (resolvedDoorType.HasValue && resolvedIsPermanent.HasValue)
        {
            ApplyConfigurationFromShortcut(resolvedDoorType.Value, resolvedIsPermanent.Value, linkedShortcut);
            return;
        }

        EnforceIntrinsicConfiguration(hasLinkedShortcut: false);
    }

    private void MarkDirtyIfNeeded(bool changed)
    {
        if (!changed || Application.isPlaying)
            return;

        EditorUtility.SetDirty(this);
    }
#endif

    private void EnforceIntrinsicConfiguration(bool hasLinkedShortcut)
    {
        if (!hasLinkedShortcut && doorType == DoorType.OneWay)
            isPermanent = true;
    }

    private bool HasLinkedShortcut()
    {
        ShortcutBase[] shortcuts = GetComponentsInChildren<ShortcutBase>(true);
        for (int i = 0; i < shortcuts.Length; i++)
        {
            ShortcutBase shortcut = shortcuts[i];
            if (shortcut != null && shortcut.TargetDoor == this)
                return true;
        }

        return false;
    }

    private static float NormalizeSignedAngle(float angle)
    {
        return Mathf.Repeat(angle + 180f, 360f) - 180f;
    }

    private void ResetModelAfterShake()
    {
        if (shakeTween != null && shakeTween.IsActive())
            shakeTween.Kill(false);

        shakeTween = null;

        if (model == null || !hasClosedModelLocalPosition)
            return;

        model.localPosition = closedModelLocalPosition;
    }

    private Transform ResolvePresentationAnchor()
    {
        if (presentationAnchor != null)
            return presentationAnchor;

        if (model != null)
            return model;

        return transform;
    }

    private void OnDestroy()
    {
        ResetModelAfterShake();

        if (runtimePromptAnchor == null)
            return;

        if (Application.isPlaying)
            Destroy(runtimePromptAnchor.gameObject);
        else
            DestroyImmediate(runtimePromptAnchor.gameObject);
    }
}
