using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 씬에 존재하는 DialogueController와 전역 대화 재생 요청을 연결한다.
/// - 대화 재생 중 입력 잠금, 비대화 UI 숨김, 런 타이머 일시정지를 일관되게 관리한다.
/// </summary>
public sealed class DialogueService : MonoBehaviour, IDialoguePlaybackBackend
{
    public static DialogueService Instance { get; private set; }

    private const float DefaultNonDialogueUiFadeSeconds = 0.2f;
    private static readonly object DialoguePlaybackUiSuppressionOwner = new object();
    private static bool s_isQuitting;
    private static readonly GlobalCanvasLayer[] HiddenDuringDialogueLayers =
    {
        GlobalCanvasLayer.GameplayHUD,
        GlobalCanvasLayer.Popup,
        GlobalCanvasLayer.Hover,
        GlobalCanvasLayer.Prompt,
        GlobalCanvasLayer.Reward,
        GlobalCanvasLayer.DamagePopup,
        GlobalCanvasLayer.BossHUD,
    };

    private readonly List<HiddenLayerState> hiddenLayerStates = new();
    private readonly List<object> nonDialogueUiSuppressionOwners = new();

    private DialogueController activeController;
    private GameFlowInputBlocker inputBlocker;
    private Coroutine nonDialogueUiFadeRoutine;
    private bool wasDialoguePlaying;

    /// <summary>
    /// 책임:
    /// - 대화 중 숨긴 전역 UI 레이어의 원래 표시/입력 상태를 복구할 수 있게 저장한다.
    /// </summary>
    private sealed class HiddenLayerState
    {
        public GlobalCanvasLayer Layer;
        public GameObject Root;
        public CanvasGroup Group;
        public bool WasActive;
        public float OriginalAlpha = 1f;
        public bool OriginalInteractable = true;
        public bool OriginalBlocksRaycasts = true;
        public bool AddedCanvasGroup;
        public bool TemporarilyVisible;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        EnsureInstance();
    }

    public static DialogueService EnsureInstance()
    {
        if (Instance != null)
        {
            DialoguePlayback.RegisterBackend(Instance);
            return Instance;
        }

        if (s_isQuitting)
            return Instance;

        DialogueService existing = Object.FindFirstObjectByType<DialogueService>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            DialoguePlayback.RegisterBackend(existing);
            return Instance;
        }

        GameObject root = new GameObject(nameof(DialogueService));
        return root.AddComponent<DialogueService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DialoguePlayback.RegisterBackend(this);
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        SyncDialogueFlowState();
    }

    private void OnDisable()
    {
        ReleaseDialogueFlowState();
    }

    private void OnDestroy()
    {
        ReleaseDialogueFlowState();

        if (Instance == this)
            DialoguePlayback.RegisterBackend(null);

        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    public bool IsPlaying => activeController != null && activeController.isPlaying;
    public bool HasActiveController => activeController != null;

    public void AcquireNonDialogueUiSuppression(object owner, float fadeSeconds = -1f)
    {
        if (IsInvalidSuppressionOwner(owner) || HasNonDialogueUiSuppressionOwner(owner))
            return;

        nonDialogueUiSuppressionOwners.Add(owner);
        if (nonDialogueUiSuppressionOwners.Count == 1)
            HideNonDialogueUi(ResolveNonDialogueUiFadeSeconds(fadeSeconds));
    }

    public void ReleaseNonDialogueUiSuppression(object owner, float fadeSeconds = -1f)
    {
        if (owner == null)
            return;

        if (!RemoveNonDialogueUiSuppressionOwner(owner))
            return;

        if (nonDialogueUiSuppressionOwners.Count == 0)
            RestoreNonDialogueUi(ResolveNonDialogueUiFadeSeconds(fadeSeconds));
    }

    public void ReleaseNonDialogueUiSuppressionWithoutRestore(object owner)
    {
        if (owner == null)
            return;

        if (!RemoveNonDialogueUiSuppressionOwner(owner))
            return;

        if (nonDialogueUiSuppressionOwners.Count == 0)
            ClearNonDialogueUiSuppressionStateWithoutRestore();
    }

    public bool SetSuppressedNonDialogueUiLayerVisible(
        GlobalCanvasLayer layer,
        bool visible,
        bool interactable = false,
        bool blocksRaycasts = false)
    {
        HiddenLayerState state = FindCapturedNonDialogueUiLayer(layer);
        if (state == null)
            return false;

        state.TemporarilyVisible = visible;
        if (visible)
        {
            RevealCapturedNonDialogueUiState(state, interactable, blocksRaycasts);
            return true;
        }

        HideCapturedNonDialogueUiState(state);
        return true;
    }

    public void RegisterController(DialogueController controller)
    {
        if (controller == null)
            return;

        activeController = controller;
    }

    public void UnregisterController(DialogueController controller)
    {
        if (activeController == controller)
            activeController = null;
    }

    public bool TryStartDialogue(
        TextAsset inkJSON,
        List<NPCData> participants,
        NPCFeatureController featureController = null)
    {
        return TryStartDialogueSequence(
            new List<DialogueStorySegment> { new DialogueStorySegment(inkJSON) },
            participants,
            featureController);
    }

    public bool TryStartDialogue(
        TextAsset inkJSON,
        List<NPCData> participants,
        string startPath,
        NPCFeatureController featureController = null)
    {
        return TryStartDialogueSequence(
            new List<DialogueStorySegment> { new DialogueStorySegment(inkJSON, startPath) },
            participants,
            featureController);
    }

    public bool TryStartDialogue(
        TextAsset inkJSON,
        List<NPCData> participants,
        NPCFeatureController featureController,
        string startPath)
    {
        return TryStartDialogueSequence(
            new List<DialogueStorySegment> { new DialogueStorySegment(inkJSON, startPath) },
            participants,
            featureController);
    }

    public bool TryStartDialogueSequence(
        IReadOnlyList<DialogueStorySegment> storySegments,
        List<NPCData> participants,
        NPCFeatureController featureController = null)
    {
        return TryStartDialogueSequence(
            storySegments,
            participants,
            featureController,
            DialoguePresentationOptions.Default);
    }

    public bool TryStartDialogueSequence(
        IReadOnlyList<DialogueStorySegment> storySegments,
        List<NPCData> participants,
        DialoguePresentationOptions presentationOptions)
    {
        return TryStartDialogueSequence(storySegments, participants, null, presentationOptions);
    }

    public bool TryStartDialogueSequence(
        IReadOnlyList<DialogueStorySegment> storySegments,
        List<NPCData> participants,
        NPCFeatureController featureController,
        DialoguePresentationOptions presentationOptions)
    {
        if (activeController == null)
        {
            ResolveActiveController();
            if (activeController == null)
            {
                Debug.LogError("[DialogueService] 현재 씬에 등록된 DialogueController가 없어 대화를 시작할 수 없습니다.");
                return false;
            }
        }

        activeController.EnterDialogueSequence(storySegments, participants, featureController, presentationOptions);
        SyncDialogueFlowState();
        return true;
    }

    private void ResolveActiveController()
    {
        if (activeController != null)
            return;

        DialogueController controller = Object.FindFirstObjectByType<DialogueController>(FindObjectsInactive.Exclude);
        if (controller != null)
            RegisterController(controller);
    }

    private void SyncDialogueFlowState()
    {
        bool isDialoguePlaying = IsPlaying;
        if (wasDialoguePlaying == isDialoguePlaying)
            return;

        wasDialoguePlaying = isDialoguePlaying;
        SetDialogueInputBlocked(isDialoguePlaying);
        SetDialogueOwnedNonDialogueUiSuppression(isDialoguePlaying);

        if (RunTimeLimitSystem.Instance == null)
            return;

        RunTimeLimitSystem.Instance.SetExternalPause(this, isDialoguePlaying);
    }

    private void SetDialogueInputBlocked(bool blocked)
    {
        if (blocked)
        {
            inputBlocker = GameFlowInputBlocker.GetOrAdd(this);
            inputBlocker?.Acquire();
            return;
        }

        inputBlocker?.Release();
        inputBlocker = null;
    }

    private void SetDialogueOwnedNonDialogueUiSuppression(bool hidden)
    {
        if (hidden)
        {
            AcquireNonDialogueUiSuppression(DialoguePlaybackUiSuppressionOwner);
            return;
        }

        ReleaseNonDialogueUiSuppression(DialoguePlaybackUiSuppressionOwner);
    }

    private void HideNonDialogueUi(float fadeSeconds)
    {
        StopNonDialogueUiFade();

        if (hiddenLayerStates.Count == 0)
            CaptureNonDialogueUiStates();

        if (hiddenLayerStates.Count == 0)
            return;

        nonDialogueUiFadeRoutine = StartCoroutine(FadeNonDialogueUiRoutine(
            targetAlpha: 0f,
            duration: fadeSeconds,
            deactivateWhenDone: true,
            restoreWhenDone: false));
    }

    private void CaptureNonDialogueUiStates()
    {
        for (int i = 0; i < HiddenDuringDialogueLayers.Length; i++)
        {
            Canvas canvas = GlobalCanvasPlayback.GetCanvas(HiddenDuringDialogueLayers[i]);
            if (canvas == null)
                continue;

            CaptureUiRoot(HiddenDuringDialogueLayers[i], canvas.gameObject);
        }
    }

    private void CaptureUiRoot(GlobalCanvasLayer layer, GameObject root)
    {
        if (root == null || HasCapturedNonDialogueUiRoot(root))
            return;

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        bool addedCanvasGroup = false;
        bool wasActive = root.activeSelf;

        if (group == null && wasActive)
        {
            group = root.AddComponent<CanvasGroup>();
            addedCanvasGroup = true;
        }

        hiddenLayerStates.Add(new HiddenLayerState
        {
            Layer = layer,
            Root = root,
            Group = group,
            WasActive = wasActive,
            OriginalAlpha = group != null ? group.alpha : 1f,
            OriginalInteractable = group == null || group.interactable,
            OriginalBlocksRaycasts = group == null || group.blocksRaycasts,
            AddedCanvasGroup = addedCanvasGroup,
        });

        if (group != null)
        {
            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }

    private bool HasCapturedNonDialogueUiRoot(GameObject root)
    {
        for (int i = 0; i < hiddenLayerStates.Count; i++)
        {
            if (hiddenLayerStates[i].Root == root)
                return true;
        }

        return false;
    }

    private void RestoreNonDialogueUi(float fadeSeconds)
    {
        StopNonDialogueUiFade();

        if (hiddenLayerStates.Count == 0)
            return;

        nonDialogueUiFadeRoutine = StartCoroutine(FadeNonDialogueUiRoutine(
            targetAlpha: 1f,
            duration: fadeSeconds,
            deactivateWhenDone: false,
            restoreWhenDone: true));
    }

    private IEnumerator FadeNonDialogueUiRoutine(
        float targetAlpha,
        float duration,
        bool deactivateWhenDone,
        bool restoreWhenDone)
    {
        PrepareNonDialogueUiFade(restoreWhenDone);

        float[] startAlphas = new float[hiddenLayerStates.Count];
        float[] targetAlphas = new float[hiddenLayerStates.Count];
        for (int i = 0; i < hiddenLayerStates.Count; i++)
        {
            HiddenLayerState state = hiddenLayerStates[i];
            startAlphas[i] = state.Group != null ? state.Group.alpha : 1f;
            targetAlphas[i] = restoreWhenDone ? state.OriginalAlpha : targetAlpha;
        }

        if (duration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                ApplyNonDialogueUiFadeFrame(startAlphas, targetAlphas, normalized, restoreWhenDone);
                yield return null;
            }
        }

        ApplyNonDialogueUiFadeFrame(startAlphas, targetAlphas, 1f, restoreWhenDone);
        CompleteNonDialogueUiFade(deactivateWhenDone, restoreWhenDone);
        nonDialogueUiFadeRoutine = null;
    }

    private void PrepareNonDialogueUiFade(bool restoreWhenDone)
    {
        for (int i = 0; i < hiddenLayerStates.Count; i++)
        {
            HiddenLayerState state = hiddenLayerStates[i];
            if (state.Root == null)
                continue;

            if (restoreWhenDone && state.WasActive && !state.Root.activeSelf)
                state.Root.SetActive(true);

            if (state.Group == null)
                continue;

            state.Group.interactable = false;
            state.Group.blocksRaycasts = false;
        }
    }

    private void ApplyNonDialogueUiFadeFrame(float[] startAlphas, float[] targetAlphas, float normalized, bool restoreWhenDone)
    {
        for (int i = 0; i < hiddenLayerStates.Count; i++)
        {
            HiddenLayerState state = hiddenLayerStates[i];
            if (!restoreWhenDone && state.TemporarilyVisible)
                continue;

            if (state.Group == null)
                continue;

            state.Group.alpha = Mathf.Lerp(startAlphas[i], targetAlphas[i], normalized);
        }
    }

    private void CompleteNonDialogueUiFade(bool deactivateWhenDone, bool restoreWhenDone)
    {
        for (int i = hiddenLayerStates.Count - 1; i >= 0; i--)
        {
            HiddenLayerState state = hiddenLayerStates[i];
            if (state.Root == null)
                continue;

            if (restoreWhenDone)
            {
                RestoreNonDialogueUiState(state);
                continue;
            }

            if (state.TemporarilyVisible)
                continue;

            if (state.Group != null)
            {
                state.Group.alpha = 0f;
                state.Group.interactable = false;
                state.Group.blocksRaycasts = false;
            }

            if (deactivateWhenDone && state.WasActive && state.Root.activeSelf)
                state.Root.SetActive(false);
        }

        if (restoreWhenDone)
            hiddenLayerStates.Clear();
    }

    private void RestoreNonDialogueUiState(HiddenLayerState state)
    {
        if (state.Group != null)
        {
            state.Group.alpha = state.OriginalAlpha;
            state.Group.interactable = state.OriginalInteractable;
            state.Group.blocksRaycasts = state.OriginalBlocksRaycasts;

            if (state.AddedCanvasGroup)
                Destroy(state.Group);
        }

        if (state.Root != null && state.Root.activeSelf != state.WasActive)
            state.Root.SetActive(state.WasActive);
    }

    private void StopNonDialogueUiFade()
    {
        if (nonDialogueUiFadeRoutine == null)
            return;

        StopCoroutine(nonDialogueUiFadeRoutine);
        nonDialogueUiFadeRoutine = null;
    }

    private void ClearNonDialogueUiSuppressionStateWithoutRestore()
    {
        StopNonDialogueUiFade();
        hiddenLayerStates.Clear();
    }

    private HiddenLayerState FindCapturedNonDialogueUiLayer(GlobalCanvasLayer layer)
    {
        for (int i = 0; i < hiddenLayerStates.Count; i++)
        {
            if (hiddenLayerStates[i].Layer == layer)
                return hiddenLayerStates[i];
        }

        return null;
    }

    private void RevealCapturedNonDialogueUiState(HiddenLayerState state, bool interactable, bool blocksRaycasts)
    {
        if (state == null || state.Root == null)
            return;

        if (!state.Root.activeSelf)
            state.Root.SetActive(true);

        if (state.Group == null)
        {
            state.Group = state.Root.GetComponent<CanvasGroup>();
            if (state.Group == null)
            {
                state.Group = state.Root.AddComponent<CanvasGroup>();
                state.AddedCanvasGroup = true;
            }
        }

        state.Group.alpha = 1f;
        state.Group.interactable = interactable;
        state.Group.blocksRaycasts = blocksRaycasts;
    }

    private void HideCapturedNonDialogueUiState(HiddenLayerState state)
    {
        if (state == null || state.Root == null)
            return;

        if (state.Group != null)
        {
            state.Group.alpha = 0f;
            state.Group.interactable = false;
            state.Group.blocksRaycasts = false;
        }

        if (state.Root.activeSelf)
            state.Root.SetActive(false);
    }

    private void RestoreNonDialogueUiImmediate()
    {
        StopNonDialogueUiFade();

        for (int i = hiddenLayerStates.Count - 1; i >= 0; i--)
        {
            HiddenLayerState state = hiddenLayerStates[i];
            if (state.Root != null)
                RestoreNonDialogueUiState(state);
        }

        hiddenLayerStates.Clear();
    }

    private bool HasNonDialogueUiSuppressionOwner(object owner)
    {
        for (int i = 0; i < nonDialogueUiSuppressionOwners.Count; i++)
        {
            if (ReferenceEquals(nonDialogueUiSuppressionOwners[i], owner))
                return true;
        }

        return false;
    }

    private bool RemoveNonDialogueUiSuppressionOwner(object owner)
    {
        for (int i = nonDialogueUiSuppressionOwners.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(nonDialogueUiSuppressionOwners[i], owner))
                continue;

            nonDialogueUiSuppressionOwners.RemoveAt(i);
            return true;
        }

        return false;
    }

    private static bool IsInvalidSuppressionOwner(object owner)
    {
        if (owner == null)
            return true;

        return owner is UnityEngine.Object unityObject && unityObject == null;
    }

    private static float ResolveNonDialogueUiFadeSeconds(float fadeSeconds)
    {
        return fadeSeconds >= 0f ? fadeSeconds : DefaultNonDialogueUiFadeSeconds;
    }

    private void ReleaseDialogueFlowState()
    {
        inputBlocker?.Release();
        inputBlocker = null;

        nonDialogueUiSuppressionOwners.Clear();
        RestoreNonDialogueUiImmediate();

        if (RunTimeLimitSystem.Instance != null)
            RunTimeLimitSystem.Instance.SetExternalPause(this, false);

        wasDialoguePlaying = false;
    }
}
