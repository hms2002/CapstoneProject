using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityGAS;

public enum GameOverCauseKind
{
    Monster,
    Trap,
    TimeOver
}

public struct GameOverPresentationRequest
{
    public GameOverCauseKind CauseKind;
    public string CauseName;
    public float RemainingSeconds;
    public string LocationName;
    public string HubSceneName;
    public Transform PlayerTransform;
    public bool EndRunOnReturn;
    public RunEndReason EndRunReason;
    public bool UseSceneTransitionService;

    public static GameOverPresentationRequest Defeat(
        Transform playerTransform,
        string causeName,
        GameOverCauseKind causeKind,
        string hubSceneName,
        bool useSceneTransitionService)
    {
        return new GameOverPresentationRequest
        {
            CauseKind = causeKind == GameOverCauseKind.TimeOver ? GameOverCauseKind.Monster : causeKind,
            CauseName = causeName,
            RemainingSeconds = ResolveRemainingSeconds(),
            LocationName = ResolveCurrentLocationName(),
            HubSceneName = hubSceneName,
            PlayerTransform = playerTransform,
            EndRunOnReturn = true,
            EndRunReason = RunEndReason.Defeat,
            UseSceneTransitionService = useSceneTransitionService
        };
    }

    public static GameOverPresentationRequest TimeOver(
        Transform playerTransform,
        string hubSceneName,
        bool useSceneTransitionService)
    {
        return new GameOverPresentationRequest
        {
            CauseKind = GameOverCauseKind.TimeOver,
            CauseName = "마왕의 인내심",
            RemainingSeconds = ResolveRemainingSeconds(),
            LocationName = ResolveCurrentLocationName(),
            HubSceneName = hubSceneName,
            PlayerTransform = playerTransform,
            EndRunOnReturn = true,
            EndRunReason = RunEndReason.TimeOver,
            UseSceneTransitionService = useSceneTransitionService
        };
    }

    private static float ResolveRemainingSeconds()
    {
        if (RunTimeLimitSystem.Instance != null)
            return Mathf.Max(0f, RunTimeLimitSystem.Instance.RemainingSeconds);

        return GamePlayDataManager.Instance != null
            ? GamePlayDataManager.Instance.GetRunRemainingSeconds()
            : 0f;
    }

    private static string ResolveCurrentLocationName()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrWhiteSpace(sceneName))
            return "알 수 없는 장소";

        string normalized = sceneName.ToLowerInvariant();
        bool isBossRoom = normalized.Contains("boss");

        if (TryResolveRouteSetLocationName(sceneName, out string routeLocationName))
            return routeLocationName;

        if (normalized.Contains("demon") || normalized.Contains("king"))
            return "마왕의 알현실";

        if (normalized.Contains("dragon") || normalized.Contains("dragon") ||
            sceneName == "ProtoTypeCorridor 1" || sceneName == "ProtoTypeBoss 2")
        {
            return isBossRoom ? "드래곤의 방" : "보물창고";
        }

        if (normalized.Contains("shadow") || normalized.Contains("chloe") ||
            sceneName == "ProtoTypeCorridor 2" || sceneName == "ProtoTypeBoss 3")
        {
            return isBossRoom ? "클로에의 방" : "그림자 감옥";
        }

        if (normalized.Contains("slime") || normalized.Contains("melta") ||
            sceneName == "ProtoTypeCorridor 3" || sceneName == "ProtoTypeBoss 4")
        {
            return isBossRoom ? "멜타의 방" : "슬라임 왕국";
        }

        if (sceneName.StartsWith("ProtoTypeCorridor", System.StringComparison.OrdinalIgnoreCase))
            return "보물창고";

        if (sceneName.StartsWith("ProtoTypeBoss", System.StringComparison.OrdinalIgnoreCase))
            return "보스룸";

        return sceneName;
    }

    private static bool TryResolveRouteSetLocationName(string sceneName, out string locationName)
    {
        locationName = null;

        PortalRouteManager routeManager = PortalRouteManager.Instance;
        CorridorBossRouteSetSO currentStageSet = routeManager != null ? routeManager.CurrentStageSet : null;
        return currentStageSet != null && currentStageSet.TryResolveLocationName(sceneName, out locationName);
    }
}

[DisallowMultipleComponent]
public sealed class GameOverPresentationController : MonoBehaviour
{
    private const string DefaultHubSceneName = "ProtoTypeHub";
    private static readonly SoundRef ReturnToHubSound = SoundRef.FromKey("sound_ui_AfterPlayerDieReturnToHub");
    private const int SystemCursorPriority = 350;
    private const float FadeToBlackSeconds = 0.85f;
    private const float InfoFadeSeconds = 0.35f;
    private const float ReturnInfoFadeSeconds = 0.25f;
    private const float PitOpenSeconds = 0.45f;
    private const float FallSeconds = 0.55f;
    private const float PitCloseSeconds = 0.35f;
    private const float ReturnRisePixels = 45f;
    private const float ReturnFallPixels = 260f;
    private const float DefaultHoleOpenWidth = 180f;
    private const float DefaultHoleHeight = 44f;
    private const float DefaultReturnPlayerSize = 96f;
    private const float SnapshotToAuthoredSeconds = 0.25f;

    private static readonly string[] MonsterDeathPhrases =
    {
        "의 자비 없는 공격에 정신을 차리지 못했습니다.",
        "무자비하게 박살 났습니다.",
        "차가운 바닥으로 내동댕이쳐졌습니다.",
        "영혼까지 탈탈 털려버렸습니다.",
        "압도적인 힘의 차이를 뼈저리게 느꼈습니다."
    };

    private static GameOverPresentationController activeController;

    [Header("Authored UI")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private Graphic blackoutGraphic;
    [SerializeField] private CanvasGroup infoGroup;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text locationText;
    [SerializeField] private Button returnButton;

    [Header("Authored Death Text")]
    [TextArea(1, 3)]
    [SerializeField] private string timeOverMessage = "마왕의 인내심이 한계에 도달했습니다.";
    [SerializeField] private string defaultTrapCauseName = "구덩이";
    [TextArea(1, 3)]
    [SerializeField] private string trapMessageFormat = "{0}에 빠져 속절없이 추락했습니다.";
    [SerializeField] private string defaultMonsterCauseName = "알 수 없는 적";
    [TextArea(1, 3)]
    [SerializeField] private string[] monsterDeathMessageFormats =
    {
        "{0}의 자비 없는 공격에 정신을 차리지 못했습니다.",
        "{0}에게 무자비하게 박살 났습니다.",
        "{0}에게 차가운 바닥으로 내동댕이쳐졌습니다.",
        "{0}에게 영혼까지 탈탈 털려버렸습니다.",
        "{0}에게 압도적인 힘의 차이를 뼈저리게 느꼈습니다."
    };

    [Header("Authored Return Presentation")]
    [SerializeField] private CanvasGroup returnPresentationGroup;
    [SerializeField] private Image returnPlayerImage;
    [SerializeField] private Image returnHoleImage;
    [SerializeField] private Image returnHoleOccluderImage;
    [SerializeField] private Image returnHoleDownMaskImage;
    [SerializeField] private RectTransform returnPlayerRect;
    [SerializeField] private RectTransform returnHoleRect;
    [SerializeField] private RectTransform returnHoleOccluderRect;
    [SerializeField] private RectTransform returnHoleDownMaskRect;

    [Header("Legacy World Return Presentation")]
    [SerializeField] private Transform returnPitTransform;
    [SerializeField] private SpriteRenderer returnPitRenderer;

    private Coroutine activeRoutine;
    private Coroutine returnRoutine;
    private GameOverPresentationRequest request;
    private bool timerPauseApplied;
    private bool listenerBound;
    private bool hideOnNextSceneLoaded;
    private bool hasCapturedAuthoredReturnPose;
    private PlayerCinematicProtection acquiredProtection;
    private readonly List<SpriteRenderer> hiddenWorldPlayerRenderers = new List<SpriteRenderer>();
    private Vector2 returnPlayerAuthoredAnchorMin = new Vector2(0.5f, 0.5f);
    private Vector2 returnPlayerAuthoredAnchorMax = new Vector2(0.5f, 0.5f);
    private Vector2 returnPlayerAuthoredPivot = new Vector2(0.5f, 0.5f);
    private Vector2 returnPlayerAuthoredPosition;
    private Vector2 returnPlayerAuthoredSize;
    private Vector3 returnPlayerAuthoredScale = Vector3.one;
    private Quaternion returnPlayerAuthoredRotation = Quaternion.identity;
    private Vector2 returnHoleAuthoredPosition;
    private Vector2 returnHoleAuthoredSize;
    private Vector3 returnHoleAuthoredScale = Vector3.one;
    private Quaternion returnHoleAuthoredRotation = Quaternion.identity;
    private Vector2 returnHoleOccluderAuthoredPosition;
    private Vector2 returnHoleOccluderAuthoredSize;
    private Vector3 returnHoleOccluderAuthoredScale = Vector3.one;
    private Quaternion returnHoleOccluderAuthoredRotation = Quaternion.identity;
    private Vector2 returnHoleDownMaskAuthoredPosition;
    private Vector2 returnHoleDownMaskAuthoredSize;
    private Vector3 returnHoleDownMaskAuthoredScale = Vector3.one;
    private Quaternion returnHoleDownMaskAuthoredRotation = Quaternion.identity;

    public static bool IsShowing => activeController != null;

    public static bool TryShow(GameOverPresentationRequest request)
    {
        if (activeController != null)
            return true;

        GameOverPresentationController controller = FindSceneController();
        if (controller == null)
        {
            Debug.LogWarning("[GameOverPresentationController] No authored scene controller was found. Game-over presentation was skipped.");
            return false;
        }

        activeController = controller;
        if (!controller.gameObject.activeSelf)
            controller.gameObject.SetActive(true);

        controller.Begin(request);
        return true;
    }

    private static GameOverPresentationController FindSceneController()
    {
        GameOverPresentationController[] controllers = Resources.FindObjectsOfTypeAll<GameOverPresentationController>();
        for (int i = 0; i < controllers.Length; i++)
        {
            GameOverPresentationController candidate = controllers[i];
            if (candidate == null || !candidate.gameObject.scene.IsValid())
                continue;

            return candidate;
        }

        return null;
    }

    private void Awake()
    {
        ResolveReferences();
        CaptureAuthoredReturnPose();
        BindReturnButton();
        SetPresentationVisible(false);
        SetReturnPresentationVisible(false);
        SetPitVisible(false);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        MouseCursorService.Instance?.ClearDomain(this);
    }

    private void OnDestroy()
    {
        RestoreHiddenWorldPlayerRenderers();
        ReleasePlayerProtection();
        ReleaseTimerPause();
        MouseCursorService.Instance?.ClearDomain(this);

        if (activeController == this)
            activeController = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!hideOnNextSceneLoaded)
            return;

        ResetPresentationState();
    }

    private void Begin(GameOverPresentationRequest incomingRequest)
    {
        request = NormalizeRequest(incomingRequest);
        ResolveReferences();
        CaptureAuthoredReturnPose();
        BindReturnButton();
        ValidateAuthoredReferences();
        PrepareGameplayState(request.PlayerTransform);
        CenterCameraOnPlayer(request.PlayerTransform);
        CaptureReturnPlayerSnapshot(request.PlayerTransform);
        MouseCursorService.Instance?.SetDomain(this, MouseCursorDomain.SystemUi, priority: SystemCursorPriority);
        SetPresentationVisible(true);
        SetPitVisible(false);

        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(CoShow());
    }

    private GameOverPresentationRequest NormalizeRequest(GameOverPresentationRequest incomingRequest)
    {
        if (string.IsNullOrWhiteSpace(incomingRequest.HubSceneName))
            incomingRequest.HubSceneName = DefaultHubSceneName;

        if (string.IsNullOrWhiteSpace(incomingRequest.LocationName))
            incomingRequest.LocationName = SceneManager.GetActiveScene().name;

        if (incomingRequest.RemainingSeconds < 0f)
            incomingRequest.RemainingSeconds = 0f;

        return incomingRequest;
    }

    private IEnumerator CoShow()
    {
        ApplyText(request);
        ResetReturnPresentationPose(showPlayer: true, showHole: false);
        SpriteRenderer snapshotRenderer = CaptureReturnPlayerSnapshot(request.PlayerTransform);
        bool matchedWorldSnapshot = TryMatchReturnPlayerToWorldSprite(snapshotRenderer);
        if (snapshotRenderer != null)
            HideWorldPlayerRenderers(request.PlayerTransform);

        SetGraphicAlpha(blackoutGraphic, 0f);
        SetInfoVisible(alpha: 0f, interactable: false);

        yield return FadeBlackAndMoveSnapshotToAuthored(snapshotRenderer, matchedWorldSnapshot);
        yield return FadeCanvasGroup(infoGroup, 1f, InfoFadeSeconds);

        SetInfoVisible(alpha: 1f, interactable: true);
        activeRoutine = null;
    }

    private void HandleReturnClicked()
    {
        if (returnRoutine != null)
            return;

        SoundPlaybackUtility.Play(ReturnToHubSound, sourceObject: this);
        returnRoutine = StartCoroutine(CoReturnToHub());
    }

    private IEnumerator CoReturnToHub()
    {
        if (returnButton != null)
            returnButton.interactable = false;

        SetInfoVisible(infoGroup != null ? infoGroup.alpha : 0f, interactable: false);

        yield return FadeCanvasGroup(infoGroup, 0f, ReturnInfoFadeSeconds);
        yield return PlayReturnUiPresentation();

        if (request.EndRunOnReturn && GamePlayDataManager.Instance != null)
            GamePlayDataManager.Instance.EndRun(request.EndRunReason);

        LoadHubScene();
    }

    private void LoadHubScene()
    {
        string hubSceneName = string.IsNullOrWhiteSpace(request.HubSceneName)
            ? DefaultHubSceneName
            : request.HubSceneName;

        if (request.UseSceneTransitionService)
        {
            SceneTransitionCoordinator coordinator = SceneTransitionCoordinator.Instance;
            if (coordinator != null && coordinator.TryLoadScene(hubSceneName))
            {
                hideOnNextSceneLoaded = true;
                return;
            }
        }

        hideOnNextSceneLoaded = true;
        SceneManager.LoadScene(hubSceneName);
    }

    private void PrepareGameplayState(Transform playerTransform)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseAllPopups();
            UIManager.Instance.HideHoverImmediate();
            UIManager.Instance.HideWorldPrompt();
        }

        if (RunTimeLimitSystem.Instance != null)
        {
            RunTimeLimitSystem.Instance.SetExternalPause(this, true);
            timerPauseApplied = true;
        }

        if (EventSystem.current == null)
            Debug.LogWarning("[GameOverPresentationController] EventSystem is missing. The authored return button may not receive input.", this);

        if (playerTransform == null)
            return;

        PlayerCinematicProtection protection = playerTransform.GetComponent<PlayerCinematicProtection>();
        if (protection != null)
        {
            acquiredProtection = protection;
            protection.Acquire(this);
        }

        WeaponEquipController weaponEquipController = playerTransform.GetComponentInChildren<WeaponEquipController>(true);
        weaponEquipController?.Clear();

        AbilitySystem abilitySystem = playerTransform.GetComponent<AbilitySystem>();
        if (abilitySystem != null)
        {
            abilitySystem.CancelCasting(force: true);
            abilitySystem.CancelExecution(force: true);
        }

        PlayerInteractor2D player = playerTransform.GetComponent<PlayerInteractor2D>();
        player?.SetInteractState(InteractState.None);
    }

    private static void CenterCameraOnPlayer(Transform playerTransform)
    {
        if (playerTransform == null)
            return;

        CameraBootstrap.CenterGameplayCameraOn(playerTransform);
    }

    private void ReleaseTimerPause()
    {
        if (!timerPauseApplied)
            return;

        if (RunTimeLimitSystem.Instance != null)
            RunTimeLimitSystem.Instance.SetExternalPause(this, false);

        timerPauseApplied = false;
    }

    private void ReleasePlayerProtection()
    {
        if (acquiredProtection == null)
            return;

        acquiredProtection.Release(this);
        acquiredProtection = null;
    }

    private IEnumerator PlayReturnUiPresentation()
    {
        ResetReturnPresentationPose(showPlayer: true, showHole: true);
        CaptureReturnPlayerSnapshot(request.PlayerTransform);
        SetGraphicAlpha(blackoutGraphic, 1f);

        if (returnPlayerRect == null || returnHoleRect == null)
            yield break;

        Vector2 playerStart = returnPlayerAuthoredPosition;
        Vector2 playerRise = playerStart + new Vector2(0f, ReturnRisePixels);
        Vector2 playerFall = playerStart + new Vector2(0f, -ReturnFallPixels);
        Quaternion playerStartRotation = returnPlayerAuthoredRotation;
        Quaternion playerFlippedRotation = playerStartRotation * Quaternion.Euler(0f, 0f, 180f);

        Vector2 holeOpenSize = ResolveHoleOpenSize();
        ApplyHoleWidth(0f, holeOpenSize.y);

        float elapsed = 0f;
        while (elapsed < PitOpenSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOut(Mathf.Clamp01(elapsed / PitOpenSeconds));

            returnPlayerRect.anchoredPosition = Vector2.LerpUnclamped(playerStart, playerRise, t);
            ApplyHoleWidth(Mathf.LerpUnclamped(0f, holeOpenSize.x, t), holeOpenSize.y);
            yield return null;
        }

        returnPlayerRect.anchoredPosition = playerRise;
        ApplyHoleWidth(holeOpenSize.x, holeOpenSize.y);

        elapsed = 0f;
        while (elapsed < FallSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseIn(Mathf.Clamp01(elapsed / FallSeconds));

            returnPlayerRect.anchoredPosition = Vector2.LerpUnclamped(playerRise, playerFall, t);
            returnPlayerRect.localRotation = Quaternion.Slerp(playerStartRotation, playerFlippedRotation, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < PitCloseSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseInOut(Mathf.Clamp01(elapsed / PitCloseSeconds));

            ApplyHoleWidth(Mathf.LerpUnclamped(holeOpenSize.x, 0f, t), holeOpenSize.y);
            yield return null;
        }

        // Keep the final return composition intact until the hub scene load hides the whole presentation.
    }

    private void ResolveReferences()
    {
        if (rootCanvas == null)
            rootCanvas = GetComponentInChildren<Canvas>(true);

        if (infoGroup == null)
            infoGroup = GetComponentInChildren<CanvasGroup>(true);

        if (returnPlayerRect == null && returnPlayerImage != null)
            returnPlayerRect = returnPlayerImage.rectTransform;

        if (returnHoleRect == null && returnHoleImage != null)
            returnHoleRect = returnHoleImage.rectTransform;

        if (returnPresentationGroup == null)
        {
            Transform namedGroup = transform.Find("ReturnPresentationGroup");
            if (namedGroup != null)
                returnPresentationGroup = namedGroup.GetComponent<CanvasGroup>();
        }

        if (returnHoleOccluderImage == null && returnPresentationGroup != null)
        {
            Transform namedOccluder = returnPresentationGroup.transform.Find("ReturnHoleOccluder");
            if (namedOccluder != null)
                returnHoleOccluderImage = namedOccluder.GetComponent<Image>();
        }

        if (returnHoleOccluderRect == null && returnHoleOccluderImage != null)
            returnHoleOccluderRect = returnHoleOccluderImage.rectTransform;

        if (returnHoleDownMaskImage == null && returnPresentationGroup != null)
        {
            Transform namedDownMask = returnPresentationGroup.transform.Find("ReturnHoleDownMask");
            if (namedDownMask != null)
                returnHoleDownMaskImage = namedDownMask.GetComponent<Image>();
        }

        if (returnHoleDownMaskRect == null && returnHoleDownMaskImage != null)
            returnHoleDownMaskRect = returnHoleDownMaskImage.rectTransform;

        if (returnPitRenderer == null && returnPitTransform != null)
            returnPitRenderer = returnPitTransform.GetComponent<SpriteRenderer>();
    }

    private void CaptureAuthoredReturnPose()
    {
        if (hasCapturedAuthoredReturnPose)
            return;

        if (returnPlayerRect != null)
        {
            returnPlayerAuthoredAnchorMin = returnPlayerRect.anchorMin;
            returnPlayerAuthoredAnchorMax = returnPlayerRect.anchorMax;
            returnPlayerAuthoredPivot = returnPlayerRect.pivot;
            returnPlayerAuthoredPosition = returnPlayerRect.anchoredPosition;
            returnPlayerAuthoredSize = returnPlayerRect.sizeDelta;
            returnPlayerAuthoredScale = returnPlayerRect.localScale;
            returnPlayerAuthoredRotation = returnPlayerRect.localRotation;
        }

        if (returnHoleRect != null)
        {
            returnHoleAuthoredPosition = returnHoleRect.anchoredPosition;
            returnHoleAuthoredSize = returnHoleRect.sizeDelta;
            returnHoleAuthoredScale = returnHoleRect.localScale;
            returnHoleAuthoredRotation = returnHoleRect.localRotation;
        }

        if (returnHoleOccluderRect != null)
        {
            returnHoleOccluderAuthoredPosition = returnHoleOccluderRect.anchoredPosition;
            returnHoleOccluderAuthoredSize = returnHoleOccluderRect.sizeDelta;
            returnHoleOccluderAuthoredScale = returnHoleOccluderRect.localScale;
            returnHoleOccluderAuthoredRotation = returnHoleOccluderRect.localRotation;
        }

        if (returnHoleDownMaskRect != null)
        {
            returnHoleDownMaskAuthoredPosition = returnHoleDownMaskRect.anchoredPosition;
            returnHoleDownMaskAuthoredSize = returnHoleDownMaskRect.sizeDelta;
            returnHoleDownMaskAuthoredScale = returnHoleDownMaskRect.localScale;
            returnHoleDownMaskAuthoredRotation = returnHoleDownMaskRect.localRotation;
        }

        hasCapturedAuthoredReturnPose = true;
    }

    private SpriteRenderer CaptureReturnPlayerSnapshot(Transform playerTransform)
    {
        if (returnPlayerImage == null || playerTransform == null)
            return null;

        SpriteRenderer spriteRenderer = ResolvePlayerSpriteRenderer(playerTransform);
        if (spriteRenderer == null || spriteRenderer.sprite == null)
            return null;

        returnPlayerImage.sprite = spriteRenderer.sprite;
        returnPlayerImage.enabled = true;
        returnPlayerImage.type = Image.Type.Simple;
        returnPlayerImage.preserveAspect = true;

        Color sourceColor = spriteRenderer.color;
        sourceColor.a = 1f;
        returnPlayerImage.color = sourceColor;

        if (returnPlayerRect != null)
        {
            returnPlayerRect.localScale = ResolveSignedReturnPlayerScale(spriteRenderer, returnPlayerAuthoredScale);
        }

        return spriteRenderer;
    }

    private bool TryMatchReturnPlayerToWorldSprite(SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer == null || returnPlayerRect == null || rootCanvas == null)
            return false;

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        Camera worldCamera = CameraBootstrap.GetMainCamera();
        if (worldCamera == null)
            worldCamera = Camera.main;

        if (canvasRect == null || worldCamera == null)
            return false;

        Bounds bounds = spriteRenderer.bounds;
        if (bounds.size.sqrMagnitude <= Mathf.Epsilon)
            return false;

        Camera uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : rootCanvas.worldCamera != null ? rootCanvas.worldCamera : worldCamera;

        Vector3 minWorld = bounds.min;
        Vector3 maxWorld = bounds.max;
        float centerZ = bounds.center.z;
        Vector3[] worldCorners =
        {
            new Vector3(minWorld.x, minWorld.y, centerZ),
            new Vector3(minWorld.x, maxWorld.y, centerZ),
            new Vector3(maxWorld.x, maxWorld.y, centerZ),
            new Vector3(maxWorld.x, minWorld.y, centerZ)
        };

        Vector2 minLocal = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 maxLocal = new Vector2(float.MinValue, float.MinValue);

        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldCorners[i]);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPoint,
                    uiCamera,
                    out Vector2 localPoint))
            {
                return false;
            }

            minLocal = Vector2.Min(minLocal, localPoint);
            maxLocal = Vector2.Max(maxLocal, localPoint);
        }

        returnPlayerRect.anchorMin = new Vector2(0.5f, 0.5f);
        returnPlayerRect.anchorMax = new Vector2(0.5f, 0.5f);
        returnPlayerRect.pivot = new Vector2(0.5f, 0.5f);
        returnPlayerRect.anchoredPosition = (minLocal + maxLocal) * 0.5f;
        returnPlayerRect.sizeDelta = maxLocal - minLocal;
        returnPlayerRect.localRotation = Quaternion.identity;
        returnPlayerRect.localScale = ResolveSignedReturnPlayerScale(spriteRenderer, Vector3.one);
        return true;
    }

    private IEnumerator FadeBlackAndMoveSnapshotToAuthored(SpriteRenderer spriteRenderer, bool moveSnapshot)
    {
        bool canMoveSnapshot = moveSnapshot && returnPlayerRect != null;
        if (blackoutGraphic == null)
        {
            if (canMoveSnapshot)
                yield return MoveReturnPlayerToAuthoredPose(spriteRenderer, SnapshotToAuthoredSeconds);

            yield break;
        }

        Color color = blackoutGraphic.color;
        float startAlpha = color.a;
        float duration = FadeToBlackSeconds;

        if (duration <= 0f)
        {
            color.a = 1f;
            blackoutGraphic.color = color;
            if (canMoveSnapshot)
                ApplyReturnPlayerAuthoredPose(spriteRenderer);

            yield break;
        }

        Vector2 startPosition = canMoveSnapshot ? returnPlayerRect.anchoredPosition : Vector2.zero;
        Vector2 startSize = canMoveSnapshot ? returnPlayerRect.sizeDelta : Vector2.zero;
        Quaternion startRotation = canMoveSnapshot ? returnPlayerRect.localRotation : Quaternion.identity;
        Vector3 startScale = canMoveSnapshot ? returnPlayerRect.localScale : Vector3.one;
        Vector2 targetSize = ResolveReturnPlayerAuthoredSize();
        Vector3 targetScale = ResolveSignedReturnPlayerScale(spriteRenderer, returnPlayerAuthoredScale);
        float moveDuration = Mathf.Max(0f, SnapshotToAuthoredSeconds);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float fadeT = Mathf.Clamp01(elapsed / duration);
            color.a = Mathf.Lerp(startAlpha, 1f, fadeT);
            blackoutGraphic.color = color;

            if (canMoveSnapshot)
            {
                float moveT = moveDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / moveDuration);
                float easedMoveT = EaseOut(moveT);
                returnPlayerRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, returnPlayerAuthoredPosition, easedMoveT);
                returnPlayerRect.sizeDelta = Vector2.LerpUnclamped(startSize, targetSize, easedMoveT);
                returnPlayerRect.localRotation = Quaternion.Slerp(startRotation, returnPlayerAuthoredRotation, easedMoveT);
                returnPlayerRect.localScale = Vector3.LerpUnclamped(startScale, targetScale, easedMoveT);
            }

            yield return null;
        }

        color.a = 1f;
        blackoutGraphic.color = color;
        if (canMoveSnapshot)
            ApplyReturnPlayerAuthoredPose(spriteRenderer);
    }

    private IEnumerator MoveReturnPlayerToAuthoredPose(SpriteRenderer spriteRenderer, float duration)
    {
        if (returnPlayerRect == null)
            yield break;

        if (duration <= 0f)
        {
            ApplyReturnPlayerAuthoredPose(spriteRenderer);
            yield break;
        }

        Vector2 startPosition = returnPlayerRect.anchoredPosition;
        Vector2 startSize = returnPlayerRect.sizeDelta;
        Quaternion startRotation = returnPlayerRect.localRotation;
        Vector3 startScale = returnPlayerRect.localScale;
        Vector2 targetSize = ResolveReturnPlayerAuthoredSize();
        Vector3 targetScale = ResolveSignedReturnPlayerScale(spriteRenderer, returnPlayerAuthoredScale);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOut(Mathf.Clamp01(elapsed / duration));
            returnPlayerRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, returnPlayerAuthoredPosition, t);
            returnPlayerRect.sizeDelta = Vector2.LerpUnclamped(startSize, targetSize, t);
            returnPlayerRect.localRotation = Quaternion.Slerp(startRotation, returnPlayerAuthoredRotation, t);
            returnPlayerRect.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
            yield return null;
        }

        ApplyReturnPlayerAuthoredPose(spriteRenderer);
    }

    private void ApplyReturnPlayerAuthoredPose(SpriteRenderer spriteRenderer)
    {
        if (returnPlayerRect == null)
            return;

        returnPlayerRect.anchorMin = returnPlayerAuthoredAnchorMin;
        returnPlayerRect.anchorMax = returnPlayerAuthoredAnchorMax;
        returnPlayerRect.pivot = returnPlayerAuthoredPivot;
        returnPlayerRect.anchoredPosition = returnPlayerAuthoredPosition;
        returnPlayerRect.sizeDelta = ResolveReturnPlayerAuthoredSize();
        returnPlayerRect.localRotation = returnPlayerAuthoredRotation;
        returnPlayerRect.localScale = ResolveSignedReturnPlayerScale(spriteRenderer, returnPlayerAuthoredScale);
    }

    private Vector2 ResolveReturnPlayerAuthoredSize()
    {
        float width = returnPlayerAuthoredSize.x > 0f ? returnPlayerAuthoredSize.x : DefaultReturnPlayerSize;
        float height = returnPlayerAuthoredSize.y > 0f ? returnPlayerAuthoredSize.y : DefaultReturnPlayerSize;
        return new Vector2(width, height);
    }

    private static Vector3 ResolveSignedReturnPlayerScale(SpriteRenderer spriteRenderer, Vector3 baseScale)
    {
        if (spriteRenderer == null)
            return baseScale;

        Vector3 scale = baseScale;
        scale.x = Mathf.Abs(scale.x) * (spriteRenderer.flipX ? -1f : 1f);
        scale.y = Mathf.Abs(scale.y) * (spriteRenderer.flipY ? -1f : 1f);
        return scale;
    }

    private static SpriteRenderer ResolvePlayerSpriteRenderer(Transform playerTransform)
    {
        Transform playerRender = playerTransform.Find("PlayerRender");
        if (playerRender != null)
        {
            SpriteRenderer directRenderer = playerRender.GetComponent<SpriteRenderer>();
            if (directRenderer != null)
                return directRenderer;
        }

        SpriteRenderer[] renderers = playerTransform.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (renderer.gameObject.name.IndexOf("shadow", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            return renderer;
        }

        return null;
    }

    private void HideWorldPlayerRenderers(Transform playerTransform)
    {
        RestoreHiddenWorldPlayerRenderers();

        if (playerTransform == null)
            return;

        SpriteRenderer[] renderers = playerTransform.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            hiddenWorldPlayerRenderers.Add(renderer);
            renderer.enabled = false;
        }
    }

    private void RestoreHiddenWorldPlayerRenderers()
    {
        for (int i = 0; i < hiddenWorldPlayerRenderers.Count; i++)
        {
            SpriteRenderer renderer = hiddenWorldPlayerRenderers[i];
            if (renderer != null)
                renderer.enabled = true;
        }

        hiddenWorldPlayerRenderers.Clear();
    }

    private void ResetPresentationState()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        hideOnNextSceneLoaded = false;
        RestoreHiddenWorldPlayerRenderers();
        ReleasePlayerProtection();
        ReleaseTimerPause();
        MouseCursorService.Instance?.ClearDomain(this);

        if (returnButton != null)
            returnButton.interactable = true;

        SetGraphicAlpha(blackoutGraphic, 0f);
        SetInfoVisible(alpha: 0f, interactable: false);
        SetReturnPresentationVisible(false);
        SetPitVisible(false);
        SetPresentationVisible(false);

        if (activeController == this)
            activeController = null;
    }

    private void BindReturnButton()
    {
        if (listenerBound || returnButton == null)
            return;

        returnButton.onClick.AddListener(HandleReturnClicked);
        listenerBound = true;
    }

    private void ValidateAuthoredReferences()
    {
        if (rootCanvas == null)
            Debug.LogWarning("[GameOverPresentationController] Root canvas is not assigned.", this);

        if (blackoutGraphic == null)
            Debug.LogWarning("[GameOverPresentationController] Blackout graphic is not assigned.", this);

        if (infoGroup == null)
            Debug.LogWarning("[GameOverPresentationController] Info CanvasGroup is not assigned.", this);

        if (messageText == null || timeText == null || locationText == null)
            Debug.LogWarning("[GameOverPresentationController] One or more TMP text references are not assigned.", this);

        if (returnButton == null)
            Debug.LogWarning("[GameOverPresentationController] Return button is not assigned.", this);

        if (returnPresentationGroup == null || returnPlayerImage == null || returnHoleImage == null)
            Debug.LogWarning("[GameOverPresentationController] One or more UI return presentation references are not assigned.", this);

        if (returnHoleImage != null && returnHoleOccluderImage == null)
            Debug.LogWarning("[GameOverPresentationController] Return hole occluder is not assigned. The falling player will not be covered by the hole front.", this);

        if (returnHoleImage != null && returnHoleDownMaskImage == null)
            Debug.LogWarning("[GameOverPresentationController] Return hole down mask is not assigned. The falling player may remain visible below the hole.", this);
    }

    private void SetPresentationVisible(bool visible)
    {
        if (rootCanvas != null)
            rootCanvas.enabled = visible;

        if (blackoutGraphic != null)
            blackoutGraphic.gameObject.SetActive(visible);

        if (infoGroup != null)
            infoGroup.gameObject.SetActive(visible);
    }

    private void SetInfoVisible(float alpha, bool interactable)
    {
        if (infoGroup == null)
            return;

        infoGroup.gameObject.SetActive(true);
        infoGroup.alpha = alpha;
        infoGroup.interactable = interactable;
        infoGroup.blocksRaycasts = interactable;
    }

    private void SetReturnPresentationVisible(bool visible)
    {
        if (returnPresentationGroup != null)
        {
            returnPresentationGroup.gameObject.SetActive(visible);
            returnPresentationGroup.alpha = 1f;
            returnPresentationGroup.interactable = false;
            returnPresentationGroup.blocksRaycasts = false;
        }

        SetReturnPlayerVisible(visible);
        SetReturnHoleVisible(visible);
    }

    private void SetReturnPlayerVisible(bool visible)
    {
        if (returnPlayerImage != null)
            returnPlayerImage.gameObject.SetActive(visible);
    }

    private void SetReturnHoleVisible(bool visible)
    {
        if (returnHoleImage != null)
            returnHoleImage.gameObject.SetActive(visible);

        SetReturnHoleOccluderVisible(visible);
        SetReturnHoleDownMaskVisible(visible);
    }

    private void SetReturnHoleOccluderVisible(bool visible)
    {
        if (returnHoleOccluderImage != null)
            returnHoleOccluderImage.gameObject.SetActive(visible);
    }

    private void SetReturnHoleDownMaskVisible(bool visible)
    {
        if (returnHoleDownMaskImage != null)
            returnHoleDownMaskImage.gameObject.SetActive(visible);
    }

    private void ResetReturnPresentationPose(bool showPlayer, bool showHole)
    {
        if (returnPresentationGroup != null)
        {
            returnPresentationGroup.gameObject.SetActive(showPlayer || showHole);
            returnPresentationGroup.alpha = 1f;
            returnPresentationGroup.interactable = false;
            returnPresentationGroup.blocksRaycasts = false;
        }

        ApplyReturnPresentationSiblingOrder();

        if (returnPlayerRect != null)
        {
            ApplyReturnPlayerAuthoredPose(null);
        }

        if (returnHoleRect != null)
        {
            returnHoleRect.anchoredPosition = returnHoleAuthoredPosition;
            returnHoleRect.sizeDelta = ResolveHoleOpenSize();
            returnHoleRect.localRotation = returnHoleAuthoredRotation;
            returnHoleRect.localScale = returnHoleAuthoredScale;
        }

        if (returnHoleOccluderRect != null)
        {
            returnHoleOccluderRect.anchoredPosition = returnHoleOccluderAuthoredPosition;
            returnHoleOccluderRect.sizeDelta = ResolveHoleOccluderOpenSize();
            returnHoleOccluderRect.localRotation = returnHoleOccluderAuthoredRotation;
            returnHoleOccluderRect.localScale = returnHoleOccluderAuthoredScale;
        }

        if (returnHoleDownMaskRect != null)
        {
            returnHoleDownMaskRect.anchoredPosition = returnHoleDownMaskAuthoredPosition;
            returnHoleDownMaskRect.sizeDelta = returnHoleDownMaskAuthoredSize;
            returnHoleDownMaskRect.localRotation = returnHoleDownMaskAuthoredRotation;
            returnHoleDownMaskRect.localScale = returnHoleDownMaskAuthoredScale;
        }

        SetReturnPlayerVisible(showPlayer);
        SetReturnHoleVisible(showHole);
    }

    private void ApplyReturnPresentationSiblingOrder()
    {
        Transform root = returnPresentationGroup != null ? returnPresentationGroup.transform : null;
        if (root == null)
            return;

        int siblingIndex = 0;
        if (returnHoleRect != null && returnHoleRect.parent == root)
            returnHoleRect.SetSiblingIndex(siblingIndex++);

        if (returnPlayerRect != null && returnPlayerRect.parent == root)
            returnPlayerRect.SetSiblingIndex(siblingIndex++);

        if (returnHoleOccluderRect != null && returnHoleOccluderRect.parent == root)
            returnHoleOccluderRect.SetSiblingIndex(siblingIndex++);

        if (returnHoleDownMaskRect != null && returnHoleDownMaskRect.parent == root)
            returnHoleDownMaskRect.SetSiblingIndex(siblingIndex);
    }

    private Vector2 ResolveHoleOpenSize()
    {
        float width = returnHoleAuthoredSize.x > 0f ? returnHoleAuthoredSize.x : DefaultHoleOpenWidth;
        float height = returnHoleAuthoredSize.y > 0f ? returnHoleAuthoredSize.y : DefaultHoleHeight;
        return new Vector2(width, height);
    }

    private Vector2 ResolveHoleOccluderOpenSize()
    {
        Vector2 holeOpenSize = ResolveHoleOpenSize();
        float width = returnHoleOccluderAuthoredSize.x > 0f ? returnHoleOccluderAuthoredSize.x : holeOpenSize.x;
        float height = returnHoleOccluderAuthoredSize.y > 0f ? returnHoleOccluderAuthoredSize.y : holeOpenSize.y;
        return new Vector2(width, height);
    }

    private void ApplyHoleWidth(float width, float height)
    {
        float safeWidth = Mathf.Max(0f, width);

        if (returnHoleRect != null)
            returnHoleRect.sizeDelta = new Vector2(safeWidth, height);

        if (returnHoleOccluderRect != null)
            returnHoleOccluderRect.sizeDelta = new Vector2(safeWidth, ResolveHoleOccluderOpenSize().y);
    }

    private void SetPitVisible(bool visible)
    {
        if (returnPitRenderer != null)
        {
            returnPitRenderer.enabled = visible;
            return;
        }

        if (returnPitTransform != null)
            returnPitTransform.gameObject.SetActive(visible);
    }

    private void ApplyText(GameOverPresentationRequest request)
    {
        if (messageText != null)
            messageText.text = BuildDeathMessage(request);

        if (timeText != null)
            timeText.text = $"남은 시간  {FormatTime(request.RemainingSeconds)}";

        if (locationText != null)
            locationText.text = $"장소  {request.LocationName}";
    }

    private string BuildDeathMessage(GameOverPresentationRequest request)
    {
        switch (request.CauseKind)
        {
            case GameOverCauseKind.TimeOver:
                return ResolveAuthoredText(timeOverMessage, "마왕의 인내심이 한계에 도달했습니다.");

            case GameOverCauseKind.Trap:
                string trapName = string.IsNullOrWhiteSpace(request.CauseName)
                    ? ResolveAuthoredText(defaultTrapCauseName, "구덩이")
                    : request.CauseName;
                return FormatDeathMessage(
                    ResolveAuthoredText(trapMessageFormat, "{0}에 빠져 속절없이 추락했습니다."),
                    trapName);

            case GameOverCauseKind.Monster:
            default:
                string causeName = string.IsNullOrWhiteSpace(request.CauseName)
                    ? ResolveAuthoredText(defaultMonsterCauseName, "알 수 없는 적")
                    : request.CauseName;
                string messageFormat = PickMonsterDeathMessageFormat();
                if (!string.IsNullOrWhiteSpace(messageFormat))
                    return FormatDeathMessage(messageFormat, causeName);

                string phrase = MonsterDeathPhrases[Random.Range(0, MonsterDeathPhrases.Length)];
                return phrase.StartsWith("의", System.StringComparison.Ordinal)
                    ? $"{causeName}{phrase}"
                    : $"{causeName}에게 {phrase}";
        }
    }

    private string PickMonsterDeathMessageFormat()
    {
        if (monsterDeathMessageFormats != null && monsterDeathMessageFormats.Length > 0)
        {
            int startIndex = Random.Range(0, monsterDeathMessageFormats.Length);
            for (int i = 0; i < monsterDeathMessageFormats.Length; i++)
            {
                string candidate = monsterDeathMessageFormats[(startIndex + i) % monsterDeathMessageFormats.Length];
                if (!string.IsNullOrWhiteSpace(candidate))
                    return candidate;
            }
        }

        return null;
    }

    private static string FormatDeathMessage(string format, string causeName)
    {
        string resolvedFormat = ResolveAuthoredText(format, "{0}");
        string resolvedCauseName = ResolveAuthoredText(causeName, string.Empty);
        return resolvedFormat.Contains("{0}")
            ? resolvedFormat.Replace("{0}", resolvedCauseName)
            : resolvedFormat;
    }

    private static string ResolveAuthoredText(string text, string fallback)
    {
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int minutes = totalSeconds / 60;
        int remainderSeconds = totalSeconds % 60;
        return $"{minutes:00}:{remainderSeconds:00}";
    }

    private static IEnumerator FadeGraphic(Graphic graphic, float targetAlpha, float duration)
    {
        if (graphic == null)
            yield break;

        Color color = graphic.color;
        float startAlpha = color.a;
        if (duration <= 0f)
        {
            color.a = targetAlpha;
            graphic.color = color;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            color.a = Mathf.Lerp(startAlpha, targetAlpha, t);
            graphic.color = color;
            yield return null;
        }

        color.a = targetAlpha;
        graphic.color = color;
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float targetAlpha, float duration)
    {
        if (canvasGroup == null)
            yield break;

        float startAlpha = canvasGroup.alpha;
        if (duration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    private static void SetGraphicAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null)
            return;

        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    private static float EaseIn(float t)
    {
        return t * t;
    }

    private static float EaseOut(float t)
    {
        return 1f - ((1f - t) * (1f - t));
    }

    private static float EaseInOut(float t)
    {
        return t < 0.5f
            ? 2f * t * t
            : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
    }
}
