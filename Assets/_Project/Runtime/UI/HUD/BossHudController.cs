using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임 :
/// - 전투 중인 보스들의 HUD 표시 요청을 등록 목록으로 관리한다.
/// - 등록된 보스마다 슬롯 프리팹을 배정하고 이름, HP, 그로기 표시 값을 배포한다.
/// - 씬 전환, 보스 파괴, 강제 해제 상황에서 남은 HUD 슬롯을 안전하게 정리한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossHudController : MonoBehaviour, IBossHudBackend
{
    public static BossHudController Instance { get; private set; }

    [Header("Binding")]
    [Tooltip("씬에서 직접 연결할 보스 엔티티입니다.")]
    [SerializeField] private MonoBehaviour targetBoss;
    [SerializeField] private bool autoFindBossOnSceneLoad = true;

    [Tooltip("비어 있으면 보스 오브젝트 이름을 그대로 사용합니다.")]
    [SerializeField] private string displayNameOverride;

    [Header("Views")]
    [SerializeField] private GameObject visibleRoot;
    [Tooltip("보스 HUD 슬롯들이 배치될 부모입니다. HorizontalLayoutGroup을 붙이는 것을 권장합니다.")]
    [SerializeField] private RectTransform slotRoot;
    [Tooltip("보스 한 체를 표시하는 HUD 슬롯 프리팹입니다.")]
    [SerializeField] private BossHudSlotView slotPrefab;
    [Tooltip("slotRoot에 HorizontalLayoutGroup이 없으면 자동으로 추가합니다.")]
    [SerializeField] private bool ensureHorizontalLayoutGroup = true;
    [Tooltip("슬롯 간격입니다. 자동 생성된 HorizontalLayoutGroup에만 적용됩니다.")]
    [SerializeField, Min(0f)] private float slotSpacing = 10f;

    [Header("Legacy Single Slot Fallback")]
    [Tooltip("slotPrefab이 없을 때 기존 단일 HUD 참조를 임시로 사용합니다.")]
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
    [SerializeField, Min(0f)] private float slideDuration = 0.8f;
    [SerializeField] private bool useUnscaledSlideTime = true;
    [SerializeField] private bool logSlidePresentation;

    private readonly List<BossHudRegistration> registrations = new List<BossHudRegistration>();
    private Coroutine _slideRoutine;
    private bool _hasAppliedInitialSlideState;
    private bool _lastSlideVisibleState;

    /// <summary>
    /// 책임 :
    /// - HUD 컨트롤러가 추적하는 보스와 그 보스에 배정된 슬롯 상태를 묶어 보관한다.
    /// - 사망 표시 유지와 실제 슬롯 제거 타이밍을 분리한다.
    /// </summary>
    private sealed class BossHudRegistration
    {
        public IBossHudSource Boss;
        public BossHudSlotView Slot;
        public string DisplayNameOverride;
        public BossHudHealthBarTheme HealthBarTheme;
        public bool IsDefeated;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BossHudPlayback.RegisterBackend(this);
        EnsureSlotLayout();
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

        BossHudPlayback.UnregisterBackend(this);
    }

    private void Update()
    {
        if (!TryRefreshRegisteredBosses(allowFallbackBind: false))
            HideHud();
    }

    /// <summary>
    /// 책임 :
    /// - 씬 로드 직후 현재 씬의 보스 엔티티를 다시 탐색해 DDOL HUD와 재바인딩한다.
    /// - 보스가 없는 씬에서는 HUD만 숨기고, 컴포넌트는 살아 있어 다음 씬에서 자동 복구되게 한다.
    /// </summary>
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearAllBosses();
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
        EnsureSlotLayout();

        if (!TryBindFallbackBoss() || !TryRefreshRegisteredBosses())
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
    public void BindBoss(IBossHudSource boss)
    {
        RegisterBoss(boss);
        if (!TryRefreshRegisteredBosses(allowFallbackBind: false))
            HideHud();
    }

    /// <summary>
    /// 책임 :
    /// - 현재 HUD source가 소유한 보스가 해제될 때만 안전하게 참조를 비운다.
    /// - 다중 보스 source는 남은 보스 snapshot을 만들 수 있으면 HUD 표시를 유지한다.
    /// </summary>
    public void UnbindBoss(IBossHudSource boss)
    {
        UnregisterBoss(boss);
        if (TryRefreshRegisteredBosses(allowFallbackBind: false))
            return;

        HideHud();
    }

    /// <summary>
    /// 책임 :
    /// - 호출자가 display name 없이 체력바 테마만 요청할 때 인자 순서를 헷갈리지 않는 진입점을 제공한다.
    /// - 내부 등록 정책은 문자열 override를 받는 기본 RegisterBoss 경로로 모은다.
    /// </summary>
    public void RegisterBoss(IBossHudSource boss, BossHudHealthBarTheme healthBarTheme)
    {
        RegisterBoss(boss, null, healthBarTheme);
    }

    /// <summary>
    /// 책임 :
    /// - 보스 전투 시작 시 HUD 슬롯 표시를 요청하는 공식 진입점입니다.
    /// - 이미 등록된 보스는 중복 슬롯 없이 사망 표시만 해제합니다.
    /// </summary>
    public void RegisterBoss(
        IBossHudSource boss,
        string bossDisplayNameOverride = null,
        BossHudHealthBarTheme healthBarTheme = null)
    {
        if (boss == null)
        {
            return;
        }

        BossHudHealthBarTheme resolvedTheme = healthBarTheme != null
            ? healthBarTheme
            : boss.HudHealthBarTheme;

        BossHudRegistration registration = FindRegistration(boss);
        if (registration == null)
        {
            registration = new BossHudRegistration
            {
                Boss = boss,
                Slot = CreateSlotView(),
                DisplayNameOverride = bossDisplayNameOverride,
                HealthBarTheme = resolvedTheme,
                IsDefeated = false
            };
            registrations.Add(registration);
        }
        else
        {
            registration.IsDefeated = false;
            if (!string.IsNullOrWhiteSpace(bossDisplayNameOverride))
                registration.DisplayNameOverride = bossDisplayNameOverride;

            if (resolvedTheme != null)
                registration.HealthBarTheme = resolvedTheme;
        }

        TryRefreshRegisteredBosses(allowFallbackBind: false);
    }

    /// <summary>
    /// 책임 :
    /// - 보스 사망 시작 시 HUD 슬롯을 제거하지 않고 체력 0 상태로 유지하도록 표시한다.
    /// - 처치 연출과 실제 HUD 제거 타이밍을 분리해 다중 보스 피드백을 안정화한다.
    /// </summary>
    public void MarkBossDefeated(IBossHudSource boss)
    {
        if (boss == null)
            return;

        BossHudRegistration registration = FindRegistration(boss);
        if (registration == null)
        {
            RegisterBoss(boss);
            registration = FindRegistration(boss);
        }

        if (registration == null)
            return;

        registration.IsDefeated = true;
        TryRefreshRegisteredBosses(allowFallbackBind: false);
    }

    /// <summary>
    /// 책임 :
    /// - 보스 처치 연출 종료, Destroy, 강제 전투 종료 시 해당 보스 슬롯을 실제로 제거한다.
    /// - 남은 보스가 없으면 HUD 전체를 숨긴다.
    /// </summary>
    public void UnregisterBoss(IBossHudSource boss)
    {
        if (boss == null)
        {
            return;
        }

        for (int i = registrations.Count - 1; i >= 0; i--)
        {
            BossHudRegistration registration = registrations[i];
            if (registration == null || registration.Boss != boss)
                continue;

            DestroySlot(registration.Slot);
            registrations.RemoveAt(i);
        }
    }

    /// <summary>
    /// 책임 :
    /// - 씬 전환이나 Global UI 재생성 같은 전역 상황에서 모든 보스 HUD 슬롯을 즉시 제거한다.
    /// </summary>
    public void ClearAllBosses()
    {
        for (int i = registrations.Count - 1; i >= 0; i--)
        {
            BossHudRegistration registration = registrations[i];
            if (registration != null)
                DestroySlot(registration.Slot);
        }

        registrations.Clear();
    }

    private bool TryBindFallbackBoss()
    {
        IBossHudSource boss = targetBoss as IBossHudSource;
        if (boss == null && ShouldUseAutoFindFallback())
            boss = FindAnyBossHudSource();

        if (boss == null)
            return false;

        if (!boss.IsCombatActive)
            return false;

        RegisterBoss(boss, displayNameOverride);
        return registrations.Count > 0;
    }

    private bool TryRefreshRegisteredBosses(bool allowFallbackBind = true)
    {
        RemoveMissingRegistrations();

        if (registrations.Count <= 0 && allowFallbackBind && !TryBindFallbackBoss())
        {
            return false;
        }

        if (registrations.Count <= 0)
        {
            return false;
        }

        SetHudVisible(true);
        for (int i = 0; i < registrations.Count; i++)
            ApplyRegistration(registrations[i]);

        return true;
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
        for (int i = 0; i < registrations.Count; i++)
            registrations[i]?.Slot?.ResetSlot();

        if (healthBarUI != null)
        {
            healthBarUI.SetSplitHealthPresentation(false, null, null);
        }

        if (groggyBarUI != null)
        {
            groggyBarUI.SetVisible(false);
        }

        ApplyGroggyLabel(false);
        SetHudVisible(false);
    }

    private void ApplyRegistration(BossHudRegistration registration)
    {
        if (registration == null || IsBossSourceMissing(registration.Boss))
        {
            return;
        }

        if (registration.Slot == null)
        {
            registration.Slot = CreateSlotView();
        }

        BossHudSlotSnapshot snapshot = BossHudSlotSnapshot.FromBoss(
            registration.Boss,
            registration.DisplayNameOverride,
            registration.IsDefeated || registration.Boss.IsBossHudDead,
            registration.HealthBarTheme);

        if (registration.Slot != null)
        {
            registration.Slot.Apply(snapshot);
            return;
        }

        ApplyLegacySingleSlot(snapshot);
    }

    private void ApplyLegacySingleSlot(BossHudSlotSnapshot snapshot)
    {
        if (registrations.Count > 1)
            return;

        if (bossNameText != null)
            bossNameText.text = snapshot.DisplayName;

        if (healthBarUI != null)
        {
            healthBarUI.SetSplitHealthPresentation(false, null, null);
            healthBarUI.SetHealthRatio(snapshot.HealthRatio);
        }

        if (groggyBarUI != null)
        {
            groggyBarUI.SetVisible(snapshot.HasGroggyGauge);
            if (snapshot.HasGroggyGauge)
            {
                groggyBarUI.SetGroggyMode(snapshot.IsGroggy);
                groggyBarUI.SetGroggyRatio(snapshot.GroggyRatio);
            }
        }

        ApplyGroggyLabel(snapshot.IsGroggy);
    }

    private BossHudRegistration FindRegistration(IBossHudSource boss)
    {
        if (boss == null)
            return null;

        for (int i = 0; i < registrations.Count; i++)
        {
            BossHudRegistration registration = registrations[i];
            if (registration != null && registration.Boss == boss)
                return registration;
        }

        return null;
    }

    private void RemoveMissingRegistrations()
    {
        for (int i = registrations.Count - 1; i >= 0; i--)
        {
            BossHudRegistration registration = registrations[i];
            if (registration == null || IsBossSourceMissing(registration.Boss))
            {
                if (registration != null)
                    DestroySlot(registration.Slot);

                registrations.RemoveAt(i);
            }
        }
    }

    private static bool IsBossSourceMissing(IBossHudSource boss)
    {
        return boss == null || boss.HudSourceComponent == null;
    }

    private IBossHudSource FindAnyBossHudSource()
    {
#if UNITY_2023_1_OR_NEWER
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>();
#endif

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IBossHudSource source)
                return source;
        }

        return null;
    }

    private BossHudSlotView CreateSlotView()
    {
        if (slotPrefab == null)
        {
            return null;
        }

        RectTransform parent = slotRoot != null ? slotRoot : transform as RectTransform;
        BossHudSlotView slot = Instantiate(slotPrefab, parent);
        slot.ResetSlot();
        return slot;
    }

    private void DestroySlot(BossHudSlotView slot)
    {
        if (slot == null)
            return;

        Destroy(slot.gameObject);
    }

    private void EnsureSlotLayout()
    {
        if (slotRoot == null)
            slotRoot = transform as RectTransform;

        if (!ensureHorizontalLayoutGroup || slotRoot == null)
            return;

        HorizontalLayoutGroup layoutGroup = slotRoot.GetComponent<HorizontalLayoutGroup>();
        if (layoutGroup == null)
            layoutGroup = slotRoot.gameObject.AddComponent<HorizontalLayoutGroup>();

        layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.spacing = slotSpacing;
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
        {
            LogSlidePresentation($"skip disabled. visible={visible}");
            return;
        }

        ResolveSlideRoot();
        if (bossHudSlideRoot == null)
        {
            LogSlidePresentation($"skip no slide root. visible={visible}");
            return;
        }

        LogSlidePresentation(
            $"request visible={visible}, root={bossHudSlideRoot.name}, currentY={bossHudSlideRoot.anchoredPosition.y:F2}, hiddenY={hiddenAnchoredPosY:F2}, shownY={shownAnchoredPosY:F2}, initialized={_hasAppliedInitialSlideState}, lastVisible={_lastSlideVisibleState}");

        if (!_hasAppliedInitialSlideState)
        {
            _hasAppliedInitialSlideState = true;

            if (!visible)
            {
                SnapSlideRoot(false);
                _lastSlideVisibleState = false;
                LogSlidePresentation($"initial hidden snap. currentY={bossHudSlideRoot.anchoredPosition.y:F2}");
                return;
            }

            SnapSlideRoot(false);
            _lastSlideVisibleState = false;
            LogSlidePresentation($"initial visible starts from hidden. currentY={bossHudSlideRoot.anchoredPosition.y:F2}");
        }

        if (_lastSlideVisibleState == visible)
        {
            LogSlidePresentation($"skip same visibility. visible={visible}, currentY={bossHudSlideRoot.anchoredPosition.y:F2}");
            return;
        }

        _lastSlideVisibleState = visible;

        if (_slideRoutine != null)
        {
            StopCoroutine(_slideRoutine);
            _slideRoutine = null;
        }

        if (visible)
        {
            Vector2 targetPosition = GetSlideTargetPosition(true);
            LogSlidePresentation(
                $"start slide in. fromY={bossHudSlideRoot.anchoredPosition.y:F2}, toY={targetPosition.y:F2}, duration={slideDuration:F2}");
            _slideRoutine = StartCoroutine(AnimateSlideRoot(targetPosition));
            return;
        }

        SnapSlideRoot(false);
        LogSlidePresentation($"snap hidden. currentY={bossHudSlideRoot.anchoredPosition.y:F2}");
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
            LogSlidePresentation($"slide instant. targetY={targetPosition.y:F2}");
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
        LogSlidePresentation($"slide complete. targetY={targetPosition.y:F2}");
    }

    private void LogSlidePresentation(string message)
    {
        if (!logSlidePresentation)
            return;

        Debug.Log($"[BossHudSlide] {name}: {message}", this);
    }

}
