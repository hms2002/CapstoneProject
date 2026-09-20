using System;
using System.Collections;
using System.Collections.Generic;
using Cainos.PixelArtTopDown_Basic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>Scene-owned scribe introduction, return greetings and audience reveal.</summary>
[DisallowMultipleComponent]
public sealed class GrandHallScribeSequence : InteractableBase, INPCFeature
{
    private const string IntroSeenId = "grandhall_scribe_intro_seen";
    private static readonly int OutlineEnabledId = Shader.PropertyToID("_OutlineEnabled");
    private SpriteRenderer outlineRenderer;
    private MaterialPropertyBlock outlineProperties;
    // Dialogue owns its canvas; the common letterbox handles Gameplay/Boss HUD fading.
    private static readonly GlobalCanvasLayer[] PresentationFadedLayers =
    {
        GlobalCanvasLayer.Popup,
        GlobalCanvasLayer.Hover,
        GlobalCanvasLayer.Prompt,
        GlobalCanvasLayer.Reward,
        GlobalCanvasLayer.DamagePopup,
        GlobalCanvasLayer.BossHUD,
    };
    [SerializeField] private NPCData npc;
    [SerializeField] private TextAsset story;
    [SerializeField] private NPCFeatureController features;
    [SerializeField] private MonoBehaviour speechBubble;
    [SerializeField] private GameFlowInputBlocker inputBlocker;
    [SerializeField] private CinemachineCamera focusCamera;
    [SerializeField] private Transform scribeFocus;
    [SerializeField] private Transform dragonPortal;
    [SerializeField] private Transform shadowPortal;
    [SerializeField] private Transform slimePortal;
    [SerializeField] private Transform demonPortal;
    [SerializeField] private GameObject trainingDummy;
    [SerializeField, Min(0.1f)] private float moveSeconds = 1f;
    [SerializeField, Min(1f)] private float closeSize = 4.8f;
    [SerializeField, Min(1f)] private float portalPadding = 2.5f;
    [SerializeField, Min(0.1f)] private float greetingSeconds = 3f;

    private ICinematicLetterboxOverlayHandle letterbox;
    private PlayerCinematicProtection protection;
    private CinemachineBrain brain;
    private CameraFollow legacyFollow;
    private bool oldLegacyEnabled;
    private bool oldIgnoreTimeScale;
    private int oldPriority;
    private bool cameraOwned;
    private bool busy = true;
    private bool introduction;
    private bool storyCompleted;
    private int cueIndex;
    private GamePlayData run;
    private GameData slot;
    private ISpeechBubblePlayback Speech => speechBubble as ISpeechBubblePlayback;
    public string FeatureName => "scribe_cue";
    // The saved completion cue precedes camera restoration; wait for the whole entry flow.
    public bool IsPortalGuidanceReady => isActiveAndEnabled && !busy && IsCurrentSlot &&
        TutorialProgressStore.IsCompleted(IntroSeenId);

    private void Awake()
    {
        outlineRenderer = GetComponent<SpriteRenderer>();
        OnUnHighlight();
        HideTrainingDummyForFirstEntry();
    }

    private void HideTrainingDummyForFirstEntry()
    {
        // Keep it hidden for this entire visit, even after the intro completion is saved.
        // Boss-clear returns and later runs load the authored active prefab and skip this hide.
        if (trainingDummy != null && GameDataStore.IsAvailable && GameDataStore.Data != null &&
            !TutorialProgressStore.IsCompleted(IntroSeenId))
            trainingDummy.SetActive(false);
    }

    private IEnumerator Start()
    {
        while (!GameDataStore.IsAvailable || GameDataStore.Data == null)
            yield return null;

        HideTrainingDummyForFirstEntry();
        while (PlayerRuntimeRegistry.GetPlayerTransform() == null || !DialoguePlayback.HasActiveController ||
               SceneTransitionPlayback.IsTransitionActive || DialoguePlayback.IsPlaying)
            yield return null;

        if (!ValidateReferences()) yield break;
        run = RunSessionStore.Data;
        slot = GameDataStore.Data;
        busy = true;
        if (!TutorialProgressStore.IsCompleted(IntroSeenId))
            yield return PlayCinematic(true);

        int stage = RunOfficerQuestProgress.CountDefeated(run);
        if (IsCurrentRun && stage > run.grandHallScribeReturnStage)
        {
            if (stage >= 3)
                yield return PlayCinematic(false);
            else
            {
                bool hidden = false;
                Speech.Speak(stage == 1
                    ? "첫번째 승리군요.\n생각보다 빠르십니다."
                    : "이제 마지막 간부입니다.\n마왕님께서 기다리고 계십니다.\n서두르시길.",
                    greetingSeconds, null, () => hidden = true);
                while (!hidden && IsCurrentRun) yield return null;
                if (IsCurrentRun && hidden) run.grandHallScribeReturnStage = stage;
            }
        }
        busy = false;
    }

    private bool IsCurrentSlot => slot != null && ReferenceEquals(slot, GameDataStore.Data);
    private bool IsCurrentRun => IsCurrentSlot && run != null && run.isRunActive &&
        ReferenceEquals(run, RunSessionStore.Data);
    private bool CanContinueCinematic => introduction ? IsCurrentSlot : IsCurrentRun;

    private bool ValidateReferences()
    {
        if (npc != null && story != null && features != null && Speech != null && inputBlocker != null &&
            focusCamera != null && scribeFocus != null && dragonPortal != null &&
            shadowPortal != null && slimePortal != null && demonPortal != null) return true;
        Debug.LogError("[GrandHallScribe] Missing authored NPC, dialogue, speech or camera references.", this);
        return false;
    }

    private IEnumerator PlayCinematic(bool firstMeeting)
    {
        introduction = firstMeeting;
        storyCompleted = false;
        cueIndex = 0;
        inputBlocker.Acquire();
        var player = PlayerRuntimeRegistry.GetPlayerTransform();
        protection = player.GetComponent<PlayerCinematicProtection>();
        if (protection == null) protection = player.gameObject.AddComponent<PlayerCinematicProtection>();
        protection.Acquire(this);
        letterbox = CinematicLetterboxPlayback.CreateOverlay();
        try
        {
            yield return letterbox.PlayIn(0.35f, 0.14f, 0f, PresentationFadedLayers);
            BeginCamera();
            yield return MoveCamera(scribeFocus.position, closeSize);
            if (firstMeeting)
            {
                bool hidden = false;
                Speech.Speak("오셨군요 용사님", greetingSeconds, null, () => hidden = true);
                while (!hidden && CanContinueCinematic) yield return null;
            }
            if (!CanContinueCinematic) yield break;
            yield return HideLetterbox();
            if (!DialoguePlayback.TryStartDialogueSequence(
                    new[] { new DialogueStorySegment(story, firstMeeting ? "intro" : "audience") },
                    new List<NPCData> { npc }, features, DialoguePresentationOptions.Default))
                yield break;
            yield return null;
            while (DialoguePlayback.IsPlaying && CanContinueCinematic) yield return null;
            if (storyCompleted && CanContinueCinematic)
            {
                if (firstMeeting) TutorialProgressStore.MarkCompleted(IntroSeenId);
                else run.grandHallScribeReturnStage = 3;
            }
            focusCamera.Priority = oldPriority;
            yield return CameraCinematicWaitUtility.WaitForCameraSettle(
                brain, null, PlayerRuntimeRegistry.GetPlayerTransform());
            yield return HideLetterbox();
        }
        finally { Cleanup(); }
    }

    // Ink places each blocking feature on its own line, before the next spoken line.
    public void Execute(Action onComplete)
    {
        if (!busy || !cameraOwned) { onComplete?.Invoke(); return; }
        StartCoroutine(PlayCue(onComplete));
    }

    private IEnumerator PlayCue(Action onComplete)
    {
        int cue = cueIndex++;
        if (cue == 0)
        {
            bool upperPanelClosed = false;
            DialoguePlayback.SetUpperPanelHiddenForCameraDialogue(true, () => upperPanelClosed = true);
            DialoguePlayback.SetPortraitsHiddenForCameraDialogue(true);
            while (!upperPanelClosed && CanContinueCinematic)
                yield return null;
            if (!CanContinueCinematic) yield break;
        }
        if (introduction)
        {
            if (cue == 0) yield return MoveCamera(dragonPortal.position, PortalOverviewSize());
            else if (cue == 1) yield return MoveCamera(demonPortal.position, closeSize);
            else if (cue == 2)
            {
                yield return MoveCamera(scribeFocus.position, closeSize);
                DialoguePlayback.SetUpperPanelHiddenForCameraDialogue(false);
                DialoguePlayback.SetPortraitsHiddenForCameraDialogue(false);
            }
            else storyCompleted = true;
        }
        else if (cue == 0)
        {
            yield return MoveCamera(demonPortal.position, closeSize);
            if (IsCurrentRun && RunOfficerQuestProgress.CountDefeated(run) == 3)
                run.grandHallAudienceGranted = true;
            // Keep the upper frame hidden through the final portal-facing line.
            yield return null;
        }
        else storyCompleted = true;
        if (CanContinueCinematic) onComplete?.Invoke();
    }

    private IEnumerator HideLetterbox()
    {
        if (letterbox == null) yield break;
        yield return letterbox.PlayOut(0.35f);
        letterbox.Dispose();
        letterbox = null;
    }

    private float PortalOverviewSize()
    {
        Vector3 center = dragonPortal.position;
        float halfHeight = closeSize;
        float aspect = Camera.main != null ? Mathf.Max(0.1f, Camera.main.aspect) : 16f / 9f;
        foreach (Transform portal in new[] { dragonPortal, shadowPortal, slimePortal })
        {
            Vector3 delta = portal.position - center;
            halfHeight = Mathf.Max(halfHeight, (Mathf.Abs(delta.y) + portalPadding) / 0.72f,
                (Mathf.Abs(delta.x) + portalPadding) / aspect);
        }
        return halfHeight;
    }

    private void BeginCamera()
    {
        var output = Camera.main;
        brain = output != null ? output.GetComponent<CinemachineBrain>() : null;
        legacyFollow = output != null ? output.GetComponent<CameraFollow>() : null;
        oldPriority = focusCamera.Priority;
        if (brain != null) { oldIgnoreTimeScale = brain.IgnoreTimeScale; brain.IgnoreTimeScale = true; }
        if (legacyFollow != null) { oldLegacyEnabled = legacyFollow.enabled; legacyFollow.enabled = false; }
        if (output != null)
        {
            focusCamera.transform.position = output.transform.position;
            var lens = focusCamera.Lens;
            lens.OrthographicSize = output.orthographicSize;
            focusCamera.Lens = lens;
        }
        cameraOwned = true;
        focusCamera.Priority = 10000;
    }

    private IEnumerator MoveCamera(Vector3 target, float size)
    {
        Vector3 start = focusCamera.transform.position;
        target.z = start.z;
        float startSize = focusCamera.Lens.OrthographicSize;
        float elapsed = 0f;
        while (elapsed < moveSeconds && CanContinueCinematic)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / moveSeconds));
            focusCamera.transform.position = Vector3.Lerp(start, target, t);
            var lens = focusCamera.Lens;
            lens.OrthographicSize = Mathf.Lerp(startSize, size, t);
            focusCamera.Lens = lens;
            yield return null;
        }
        yield return CameraCinematicWaitUtility.WaitForCameraSettle(brain, null, null);
    }

    private void Cleanup()
    {
        DialoguePlayback.SetPortraitsHiddenForCameraDialogue(false);
        DialoguePlayback.SetUpperPanelHiddenForCameraDialogue(false);
        Speech?.HideActive();
        if (cameraOwned)
        {
            if (focusCamera != null) focusCamera.Priority = oldPriority;
            if (brain != null) brain.IgnoreTimeScale = oldIgnoreTimeScale;
            if (legacyFollow != null) legacyFollow.enabled = oldLegacyEnabled;
            cameraOwned = false;
        }
        letterbox?.Dispose();
        letterbox = null;
        protection?.Release(this);
        protection = null;
        inputBlocker?.Release();
    }

    private void OnDisable()
    {
        OnUnHighlight();
        StopAllCoroutines();
        if (busy && DialoguePlayback.IsPlaying) features?.RequestDialogueExit?.Invoke();
        Cleanup();
        busy = false;
    }

    public override void OnHighlight() => SetOutline(true);
    public override void OnUnHighlight() => SetOutline(false);

    private void SetOutline(bool enabled)
    {
        if (outlineRenderer == null) return;
        outlineProperties ??= new MaterialPropertyBlock();
        outlineRenderer.GetPropertyBlock(outlineProperties);
        outlineProperties.SetFloat(OutlineEnabledId, enabled ? 1f : 0f);
        outlineRenderer.SetPropertyBlock(outlineProperties);
    }

    public override bool CanInteract(IPlayerInteractor player) => !busy && player != null &&
        player.CurrentState == InteractState.Idle && npc != null && story != null &&
        DialoguePlayback.IsAvailable && !DialoguePlayback.IsPlaying && !SceneTransitionPlayback.IsTransitionActive;
    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (CanInteract(player))
            DialoguePlayback.TryStartDialogue(story, new List<NPCData> { npc }, "repeat", features);
    }
    public override InteractState GetInteractType() => InteractState.Talking;
    public override string GetInteractDescription() => "대화";
}
