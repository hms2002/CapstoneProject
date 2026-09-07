using System.Collections;
using CapstoneAudio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 현재 런의 레벨/EXP와 레벨업 보상 선택 가능 상태를 authored HUD에 투영한다.
/// 경험치 진행률과 보상 선택 가능 표시는 서로 독립적으로 갱신한다.
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

    [Header("Presentation")]
    [SerializeField, Min(0f)] private float fillAnimationDuration = 0.2f;
    [SerializeField] private SoundRef levelUpReadySound;

    private Coroutine fillAnimation;
    private int visualLevel = 1;
    private float visualFill;
    private bool skipNextStateSnap;
    private bool isRewardReadyVisible;

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
    }

    private void Update()
    {
        RefreshRewardAvailability();
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
                       rewardSessionController.CanOpenSession;
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
