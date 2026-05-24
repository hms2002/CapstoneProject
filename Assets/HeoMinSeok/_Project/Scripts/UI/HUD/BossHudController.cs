using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임 :
/// - 현재 보스 HUD source가 만든 snapshot을 읽어 이름, HP, 그로기 뷰에 배포한다.
/// - 보스 참조가 없으면 HUD 전체를 비활성화해 잘못된 정보 노출을 막는다.
/// - 보스별 특수 표시 규칙은 HUD source 쪽에 두고, 공용 HUD는 표시 orchestration만 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossHudController : MonoBehaviour
{
    public static BossHudController Instance { get; private set; }

    [Header("Binding")]
    [Tooltip("씬에서 직접 연결할 보스 엔티티입니다.")]
    [SerializeField] private BossControllerBase targetBoss;
    [SerializeField] private bool autoFindBossOnSceneLoad = true;

    [Tooltip("비어 있으면 보스 오브젝트 이름을 그대로 사용합니다.")]
    [SerializeField] private string displayNameOverride;

    [Header("Views")]
    [SerializeField] private GameObject visibleRoot;
    [SerializeField] private BossHealthBarUI healthBarUI;
    [SerializeField] private BossGroggyBarUI groggyBarUI;
    [SerializeField] private TMP_Text bossNameText;
    [SerializeField] private TMP_Text groggyStateText;

    [Header("Groggy Label")]
    [SerializeField] private string groggyStateLabel = "GROGGY";

    [Header("Slide Presentation")]
    [SerializeField] private RectTransform bossHudSlideRoot;
    [SerializeField] private bool useBossHudSlidePresentation = true;
    [SerializeField] private float hiddenAnchoredPosY = 120f;
    [SerializeField] private float shownAnchoredPosY = 0f;
    [SerializeField, Min(0f)] private float slideDuration = 0.3f;
    [SerializeField] private bool useUnscaledSlideTime = true;

    private readonly SingleBossHudSource singleBossSource = new SingleBossHudSource();
    private IBossHudSource activeSource;
    private Coroutine _slideRoutine;
    private bool _hasAppliedInitialSlideState;
    private bool _lastSlideVisibleState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveBossBinding();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ResolveBossBinding();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (_slideRoutine != null)
        {
            StopCoroutine(_slideRoutine);
            _slideRoutine = null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!TryRefreshActiveSource())
            HideHud();
    }

    /// <summary>
    /// 책임 :
    /// - 씬 로드 직후 현재 씬의 보스 엔티티를 다시 탐색해 DDOL HUD와 재바인딩한다.
    /// - 보스가 없는 씬에서는 HUD만 숨기고, 컴포넌트는 살아 있어 다음 씬에서 자동 복구되게 한다.
    /// </summary>
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearActiveSource();
        ResolveBossBinding();
    }

    /// <summary>
    /// 책임 :
    /// - 현재 씬의 보스 엔티티를 수동 참조 또는 자동 탐색으로 결정하고 HUD source를 갱신한다.
    /// - 바인딩 성공 시 HUD를 즉시 갱신하고, 실패 시에는 안전하게 숨김 상태로 전환한다.
    /// </summary>
    private void ResolveBossBinding()
    {
        ResolveSlideRoot();

        if (!TryBindFallbackBoss() || !TryRefreshActiveSource())
        {
            HideHud();
        }
    }

    private bool ShouldUseAutoFindFallback()
    {
        if (!autoFindBossOnSceneLoad)
            return false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return true;
#else
        return false;
#endif
    }

    /// <summary>
    /// 책임 :
    /// - 활성 보스가 자신을 HUD에 명시적으로 등록할 수 있는 단일 진입점을 제공한다.
    /// - 씬 탐색보다 우선하는 명시적 바인딩 경로를 통해 다중 보스/타이밍 문제를 줄인다.
    /// </summary>
    public void BindBoss(BossControllerBase boss)
    {
        if (boss == null)
            return;

        BindSourceForBoss(boss);
        if (!TryRefreshActiveSource())
            HideHud();
    }

    /// <summary>
    /// 책임 :
    /// - 현재 HUD source가 소유한 보스가 해제될 때만 안전하게 참조를 비운다.
    /// - 다중 보스 source는 남은 보스 snapshot을 만들 수 있으면 HUD 표시를 유지한다.
    /// </summary>
    public void UnbindBoss(BossControllerBase boss)
    {
        if (boss == null || IsSourceMissing(activeSource) || !activeSource.OwnsBoss(boss))
            return;

        if (ReferenceEquals(activeSource, singleBossSource))
        {
            ClearActiveSource();
            HideHud();
            return;
        }

        TryReplaceSourceOwnedByUnbindingBoss(boss);
        if (TryRefreshActiveSource())
            return;

        ClearActiveSource();
        HideHud();
    }

    private bool TryBindFallbackBoss()
    {
        BossControllerBase boss = targetBoss;
        if (boss == null && ShouldUseAutoFindFallback())
            boss = FindAnyObjectByType<BossControllerBase>();

        if (boss == null)
            return false;

        BindSourceForBoss(boss);
        return !IsSourceMissing(activeSource);
    }

    private void BindSourceForBoss(BossControllerBase boss)
    {
        IBossHudSource source = ResolveHudSourceForBoss(boss);
        if (!IsSourceMissing(source))
        {
            activeSource = source;
            singleBossSource.Clear();
            targetBoss = null;
            return;
        }

        targetBoss = boss;
        activeSource = singleBossSource.Bind(boss, displayNameOverride);
    }

    private IBossHudSource ResolveHudSourceForBoss(BossControllerBase boss)
    {
        if (boss == null)
            return null;

        MonoBehaviour[] behaviours = boss.GetComponents<MonoBehaviour>();
        IBossHudSource bestSource = null;
        int bestPriority = int.MinValue;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not IBossHudSource source)
                continue;

            if (!source.OwnsBoss(boss) || source.Priority < bestPriority)
                continue;

            bestSource = source;
            bestPriority = source.Priority;
        }

        return bestSource;
    }

    private bool TryReplaceSourceOwnedByUnbindingBoss(BossControllerBase unbindingBoss)
    {
        if (unbindingBoss == null || activeSource is not MonoBehaviour activeBehaviour)
            return false;

        if (activeBehaviour == null || activeBehaviour.gameObject != unbindingBoss.gameObject)
            return false;

        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
        IBossHudSource bestSource = null;
        int bestPriority = int.MinValue;

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == activeBehaviour)
                continue;

            if (behaviour is not IBossHudSource source)
                continue;

            if (!source.OwnsBoss(unbindingBoss) || source.Priority < bestPriority)
                continue;

            if (!source.TryBuildSnapshot(out BossHudSnapshot snapshot) || !snapshot.IsVisible)
                continue;

            bestSource = source;
            bestPriority = source.Priority;
        }

        if (IsSourceMissing(bestSource))
            return false;

        activeSource = bestSource;
        return true;
    }

    private bool TryRefreshActiveSource()
    {
        if (IsSourceMissing(activeSource))
        {
            activeSource = null;
            singleBossSource.Clear();
        }

        if (activeSource == null && !TryBindFallbackBoss())
            return false;

        if (activeSource == null ||
            !activeSource.TryBuildSnapshot(out BossHudSnapshot snapshot) ||
            !snapshot.IsVisible)
            return false;

        ApplySnapshot(snapshot);
        return true;
    }

    private void ClearActiveSource()
    {
        activeSource = null;
        singleBossSource.Clear();
    }

    private static bool IsSourceMissing(IBossHudSource source)
    {
        if (source == null)
            return true;

        return source is UnityEngine.Object unityObject && unityObject == null;
    }

    private void ApplySnapshot(BossHudSnapshot snapshot)
    {
        SetHudVisible(true);

        if (bossNameText != null)
            bossNameText.text = snapshot.DisplayName;

        ApplyGroggyLabel(snapshot.HasAnyGroggyChannel);
        ApplyHealthSnapshot(snapshot);
        ApplyGroggySnapshot(snapshot);
    }

    private void ApplyHealthSnapshot(BossHudSnapshot snapshot)
    {
        if (healthBarUI == null)
            return;

        if (snapshot.ChannelCount >= 2)
        {
            healthBarUI.SetSplitHealthPresentation(false, null, null);
            healthBarUI.SetDualHealthRatios(
                true,
                snapshot.PrimaryChannel.HealthRatio,
                snapshot.SecondaryChannel.HealthRatio);
            return;
        }

        healthBarUI.SetDualHealthRatios(false, 0f, 0f);
        healthBarUI.SetSplitHealthPresentation(
            snapshot.UseSplitHealthPresentation,
            snapshot.SplitHealthLeftLabel,
            snapshot.SplitHealthRightLabel);
        healthBarUI.SetHealthRatio(snapshot.PrimaryChannel.HealthRatio);
    }

    private void ApplyGroggySnapshot(BossHudSnapshot snapshot)
    {
        if (groggyBarUI == null)
            return;

        if (snapshot.ChannelCount >= 2)
        {
            bool visible = snapshot.PrimaryChannel.HasGroggyGauge || snapshot.SecondaryChannel.HasGroggyGauge;
            groggyBarUI.SetVisible(visible);
            groggyBarUI.SetDualGroggyRatios(
                visible,
                snapshot.PrimaryChannel.GroggyRatio,
                snapshot.PrimaryChannel.IsGroggy,
                snapshot.SecondaryChannel.GroggyRatio,
                snapshot.SecondaryChannel.IsGroggy);
            return;
        }

        BossHudChannelSnapshot channel = snapshot.PrimaryChannel;
        groggyBarUI.SetDualGroggyRatios(false, 0f, false, 0f, false);
        groggyBarUI.SetVisible(channel.HasGroggyGauge);
        if (channel.HasGroggyGauge)
        {
            groggyBarUI.SetGroggyMode(channel.IsGroggy);
            groggyBarUI.SetGroggyRatio(channel.GroggyRatio);
        }
    }

    /// <summary>
    /// 책임 :
    /// - 선택적으로 연결된 그로기 상태 텍스트를 현재 보스 상태에 맞춰 표시/숨김한다.
    /// - HUD 쪽에서 별도 상태 머신 없이 "지금이 딜 타임"이라는 정보를 짧게 읽히게 만든다.
    /// </summary>
    private void ApplyGroggyLabel(bool isGroggy)
    {
        if (groggyStateText == null)
            return;

        groggyStateText.gameObject.SetActive(isGroggy);
        if (isGroggy)
            groggyStateText.text = groggyStateLabel;
    }

    private void HideHud()
    {
        if (healthBarUI != null)
        {
            healthBarUI.SetDualHealthRatios(false, 0f, 0f);
            healthBarUI.SetSplitHealthPresentation(false, null, null);
        }

        if (groggyBarUI != null)
        {
            groggyBarUI.SetDualGroggyRatios(false, 0f, false, 0f, false);
            groggyBarUI.SetVisible(false);
        }

        ApplyGroggyLabel(false);
        SetHudVisible(false);
    }

    /// <summary>
    /// 책임 :
    /// - 보스 HUD의 표시 루트만 켜고 끄며 DDOL 컨트롤러 오브젝트 자체는 계속 살아 있게 유지한다.
    /// - 씬에 보스가 없을 때 HUD를 숨기고, 다음 씬에서 보스를 찾으면 다시 표시할 수 있게 만든다.
    /// </summary>
    private void SetHudVisible(bool visible)
    {
        GameObject targetRoot = visibleRoot != null ? visibleRoot : gameObject;
        if (targetRoot == gameObject)
        {
            if (bossNameText != null)
                bossNameText.gameObject.SetActive(visible);
            if (groggyStateText != null && !visible)
                groggyStateText.gameObject.SetActive(false);
            if (healthBarUI != null)
                healthBarUI.gameObject.SetActive(visible);
            if (groggyBarUI != null)
                groggyBarUI.gameObject.SetActive(visible);
        }
        else if (targetRoot.activeSelf != visible)
            targetRoot.SetActive(visible);

        ApplySlidePresentation(visible);
    }

    private void ResolveSlideRoot()
    {
        if (bossHudSlideRoot == null && transform.parent is RectTransform parentRect)
            bossHudSlideRoot = parentRect;
    }

    private void ApplySlidePresentation(bool visible)
    {
        if (!useBossHudSlidePresentation)
            return;

        ResolveSlideRoot();
        if (bossHudSlideRoot == null)
            return;

        if (!_hasAppliedInitialSlideState)
        {
            SnapSlideRoot(visible);
            _hasAppliedInitialSlideState = true;
            _lastSlideVisibleState = visible;
            return;
        }

        if (_lastSlideVisibleState == visible)
            return;

        _lastSlideVisibleState = visible;

        if (_slideRoutine != null)
        {
            StopCoroutine(_slideRoutine);
            _slideRoutine = null;
        }

        if (visible)
        {
            _slideRoutine = StartCoroutine(AnimateSlideRoot(GetSlideTargetPosition(true)));
            return;
        }

        SnapSlideRoot(false);
    }

    private void SnapSlideRoot(bool visible)
    {
        if (bossHudSlideRoot == null)
            return;

        bossHudSlideRoot.anchoredPosition = GetSlideTargetPosition(visible);
    }

    private Vector2 GetSlideTargetPosition(bool visible)
    {
        Vector2 current = bossHudSlideRoot.anchoredPosition;
        return new Vector2(
            current.x,
            visible ? shownAnchoredPosY : hiddenAnchoredPosY);
    }

    private IEnumerator AnimateSlideRoot(Vector2 targetPosition)
    {
        if (bossHudSlideRoot == null)
            yield break;

        Vector2 startPosition = bossHudSlideRoot.anchoredPosition;
        float duration = Mathf.Max(0f, slideDuration);
        if (duration <= 0f)
        {
            bossHudSlideRoot.anchoredPosition = targetPosition;
            _slideRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += useUnscaledSlideTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);
            bossHudSlideRoot.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, t);
            yield return null;
        }

        bossHudSlideRoot.anchoredPosition = targetPosition;
        _slideRoutine = null;
    }
}
