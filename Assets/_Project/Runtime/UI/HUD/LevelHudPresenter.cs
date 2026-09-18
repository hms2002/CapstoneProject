using System.Collections;
using CapstoneAudio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 현재 런의 레벨/EXP와 레벨업 보상 선택 가능 상태를 authored HUD에 투영한다.
/// 경험치 진행률과 보상 선택 가능 표시는 서로 독립적으로 갱신한다.
/// Positions the authored reward prompt above the current player without reparenting the HUD.
/// </summary>
[DisallowMultipleComponent]
public sealed class LevelHudPresenter : MonoBehaviour, IDefaultHudVisibilityTarget
{
    [Header("State")]
    [SerializeField] private LevelProgressionConfigSO progressionConfig;
    [SerializeField] private LevelRewardSessionController rewardSessionController;

    [Header("View")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Image experienceFill;
    [SerializeField] private GameObject rewardReadyBorder;
    [SerializeField] private GameObject levelUpPrompt;
    [SerializeField] private TMP_Text levelUpPromptText;

    [Header("Presentation")]
    [SerializeField, Min(0f)] private float fillAnimationDuration = 0.2f;
    [SerializeField] private SoundRef levelUpReadySound;
    [SerializeField] private Vector3 levelUpPromptWorldOffset = new Vector3(0f, 1.4f, 0f);
    [SerializeField] private Vector2 levelUpPromptUiOffset = new Vector2(-40f, 0f);

    private Coroutine fillAnimation;
    private int visualLevel = 1;
    private float visualFill;
    private bool skipNextStateSnap;
    private bool isRewardReadyVisible;
    private float promptColorElapsed;
    private RectTransform promptRect;
    private Vector2 promptRestPosition;
    private Canvas promptCanvas;

    private void Awake()
    {
        promptRect = levelUpPrompt != null ? levelUpPrompt.transform as RectTransform : null;
        if (promptRect != null)
        {
            promptRestPosition = promptRect.anchoredPosition;
            promptCanvas = promptRect.GetComponentInParent<Canvas>();
        }
    }

    private void OnEnable()
    {
        RunLevelProgression.ExperienceGranted += HandleExperienceGranted;
        RunLevelProgression.StateChanged += HandleStateChanged;
        RefreshImmediate();
    }

    private void OnDisable()
    {
        RunLevelProgression.ExperienceGranted -= HandleExperienceGranted;
        RunLevelProgression.StateChanged -= HandleStateChanged;
        StopFillAnimation();
        SetRewardReadyVisible(false, false);
        if (promptRect != null)
            promptRect.anchoredPosition = promptRestPosition;
    }

    private void Update()
    {
        RefreshRewardAvailability();
        AnimateRewardPrompt();
    }

    private void LateUpdate()
    {
        if (!isRewardReadyVisible || promptRect == null)
            return;

        bool hasPosition = TryGetPromptPosition(out Vector3 position);
        if (levelUpPrompt.activeSelf != hasPosition)
            levelUpPrompt.SetActive(hasPosition);
        if (hasPosition)
            promptRect.position = position;
    }

    private bool TryGetPromptPosition(out Vector3 position)
    {
        position = default;
        Transform player = PlayerRuntimeRegistry.GetPlayerTransform();
        Camera worldCamera = Camera.main;
        if (player == null || worldCamera == null || promptCanvas == null ||
            promptRect == null || promptRect.parent is not RectTransform parentRect)
            return false;

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(player.position + levelUpPromptWorldOffset);
        if (screenPoint.z <= 0f || !worldCamera.pixelRect.Contains((Vector2)screenPoint))
            return false;

        Canvas rootCanvas = promptCanvas.rootCanvas;
        Camera uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : rootCanvas.worldCamera;
        if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && uiCamera == null)
            return false;

        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(
                parentRect, screenPoint, uiCamera, out position))
            return false;

        float hoverOffset = 4f * Mathf.Sin(promptColorElapsed * Mathf.PI * 2f / 2.4f);
        position += parentRect.TransformVector((Vector3)levelUpPromptUiOffset + Vector3.up * hoverOffset);
        return true;
    }

    private void AnimateRewardPrompt()
    {
        if (!isRewardReadyVisible || levelUpPromptText == null || !levelUpPromptText.isActiveAndEnabled)
            return;

        const float colorCycleDuration = 2.4f;
        promptColorElapsed = Mathf.Repeat(promptColorElapsed + Time.unscaledDeltaTime, colorCycleDuration);
        float blend = 0.5f - 0.5f * Mathf.Cos(promptColorElapsed * Mathf.PI * 2f / colorCycleDuration);
        // Change hue only; saturation, value and alpha stay constant.
        levelUpPromptText.color = Color.HSVToRGB(Mathf.Lerp(0.23f, 0.38f, blend), 0.62f, 1f);
    }

    private void HandleExperienceGranted(LevelProgressionGrantResult result)
    {
        skipNextStateSnap = true;
        SetLevelText(result.CurrentLevel);

        if (result.LevelsGained > 0 && levelUpReadySound.IsSet)
            SoundPlaybackUtility.Play(levelUpReadySound, sourceObject: this);

        float targetFill = ResolveTargetFill(result.CurrentLevel, result.CurrentExperience);
        StartFillAnimation(result.CurrentLevel, targetFill);
        RefreshRewardAvailability();
    }

    private void HandleStateChanged()
    {
        if (skipNextStateSnap)
        {
            skipNextStateSnap = false;
            RefreshRewardAvailability();
            return;
        }

        RefreshImmediate();
    }

    private void RefreshImmediate()
    {
        StopFillAnimation();

        LevelProgressionState state = RunLevelProgression.State;
        int level = Mathf.Max(1, state?.level ?? 1);
        int experience = Mathf.Max(0, state?.currentExperience ?? 0);

        visualLevel = level;
        visualFill = ResolveTargetFill(level, experience);
        SetLevelText(level);
        SetFill(visualFill);
        RefreshRewardAvailability();
    }

    private void StartFillAnimation(int targetLevel, float targetFill)
    {
        StopFillAnimation();

        if (!isActiveAndEnabled || fillAnimationDuration <= 0f)
        {
            visualLevel = targetLevel;
            visualFill = targetFill;
            SetFill(targetFill);
            return;
        }

        fillAnimation = StartCoroutine(AnimateFill(targetLevel, targetFill));
    }

    private IEnumerator AnimateFill(int targetLevel, float targetFill)
    {
        int levelBoundaries = Mathf.Max(0, targetLevel - visualLevel);
        for (int i = 0; i < levelBoundaries; i++)
        {
            yield return TweenFill(visualFill, 1f);
            visualLevel++;
            visualFill = 0f;
            SetFill(0f);
        }

        yield return TweenFill(visualFill, targetFill);
        visualLevel = targetLevel;
        visualFill = targetFill;
        SetFill(targetFill);
        fillAnimation = null;
    }

    private IEnumerator TweenFill(float from, float to)
    {
        if (Mathf.Approximately(from, to))
        {
            visualFill = to;
            SetFill(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fillAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / fillAnimationDuration);
            visualFill = Mathf.Lerp(from, to, progress);
            SetFill(visualFill);
            yield return null;
        }

        visualFill = to;
        SetFill(to);
    }

    private float ResolveTargetFill(int level, int currentExperience)
    {
        if (progressionConfig == null)
            return 0f;

        if (level >= progressionConfig.MaxLevel)
            return 1f;

        int requiredExperience = progressionConfig.GetRequiredExperience(level);
        return requiredExperience > 0
            ? Mathf.Clamp01((float)currentExperience / requiredExperience)
            : 0f;
    }

    private void RefreshRewardAvailability()
    {
        bool canOpen = rewardSessionController != null &&
                       rewardSessionController.isActiveAndEnabled &&
                       rewardSessionController.CanOpenSession &&
                       TryGetPromptPosition(out _);
        bool hasPendingReward = (RunLevelProgression.State?.pendingRewardCount ?? 0) > 0;
        SetRewardReadyVisible(hasPendingReward, canOpen);
    }

    private void SetRewardReadyVisible(bool borderVisible, bool visible)
    {
        if (isRewardReadyVisible == visible &&
            (rewardReadyBorder == null || rewardReadyBorder.activeSelf == borderVisible) &&
            (levelUpPrompt == null || levelUpPrompt.activeSelf == visible))
        {
            return;
        }

        isRewardReadyVisible = visible;
        promptColorElapsed = 0f;
        if (promptRect != null)
            promptRect.anchoredPosition = promptRestPosition;
        if (levelUpPromptText != null)
            levelUpPromptText.color = Color.HSVToRGB(0.23f, 0.62f, 1f);
        if (rewardReadyBorder != null && rewardReadyBorder.activeSelf != borderVisible)
            rewardReadyBorder.SetActive(borderVisible);
        if (levelUpPrompt != null && levelUpPrompt.activeSelf != visible)
            levelUpPrompt.SetActive(visible);
    }

    private void SetLevelText(int level)
    {
        if (levelText != null)
            levelText.SetText("{0}", Mathf.Max(1, level));
    }

    private void SetFill(float fill)
    {
        if (experienceFill != null)
            experienceFill.fillAmount = Mathf.Clamp01(fill);
    }

    private void StopFillAnimation()
    {
        if (fillAnimation == null)
            return;

        StopCoroutine(fillAnimation);
        fillAnimation = null;
    }
}
