using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 씬에서 직접 연결한 보스 엔티티에서 이름, HP, 그로기 정보를 읽어 각 보스 HUD 뷰에 배포한다.
/// - 보스 참조가 없으면 HUD 전체를 비활성화해 잘못된 정보 노출을 막는다.
/// - 각 개별 UI 뷰는 표현만 담당하고, 어떤 값을 언제 뿌릴지는 이 컨트롤러가 조율한다.
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

    private StaggerGaugeSystem _staggerGaugeSystem;
    private GameplayEffectRunner _effectRunner;
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
        if (targetBoss == null)
        {
            SetHudVisible(false);
            return;
        }

        RefreshAll();
    }

    /// <summary>
    /// 책임 :
    /// - 씬 로드 직후 현재 씬의 보스 엔티티를 다시 탐색해 DDOL HUD와 재바인딩한다.
    /// - 보스가 없는 씬에서는 HUD만 숨기고, 컴포넌트는 살아 있어 다음 씬에서 자동 복구되게 한다.
    /// </summary>
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveBossBinding();
    }

    /// <summary>
    /// 책임 :
    /// - 현재 씬의 보스 엔티티를 수동 참조 또는 자동 탐색으로 결정하고 관련 런타임 참조를 갱신한다.
    /// - 바인딩 성공 시 HUD를 즉시 갱신하고, 실패 시에는 안전하게 숨김 상태로 전환한다.
    /// </summary>
    private void ResolveBossBinding()
    {
        ResolveSlideRoot();

        if (targetBoss == null && ShouldUseAutoFindFallback())
            targetBoss = FindAnyObjectByType<BossControllerBase>();

        if (targetBoss == null)
        {
            _staggerGaugeSystem = null;
            _effectRunner = null;
            SetHudVisible(false);
            return;
        }

        _staggerGaugeSystem = targetBoss.GetComponent<StaggerGaugeSystem>();
        _effectRunner = targetBoss.GetComponent<GameplayEffectRunner>();

        SetHudVisible(true);
        ApplyStaticVisuals();
        RefreshAll();
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
        targetBoss = boss;
        ResolveBossBinding();
    }

    /// <summary>
    /// 책임 :
    /// - 현재 HUD에 연결된 보스가 해제될 때만 안전하게 참조를 비우고 HUD를 숨긴다.
    /// - 다른 보스가 이미 등록된 상황에서 잘못된 해제가 들어와도 현재 HUD 바인딩을 보호한다.
    /// </summary>
    public void UnbindBoss(BossControllerBase boss)
    {
        if (boss == null || targetBoss != boss)
            return;

        targetBoss = null;
        _staggerGaugeSystem = null;
        _effectRunner = null;
        SetHudVisible(false);
    }

    /// <summary>
    /// 책임 :
    /// - 보스가 바뀌지 않는 정보(표시 이름)를 뷰에 한 번 반영한다.
    /// - 이름 텍스트의 fallback 규칙을 HUD 컨트롤러 안에 모아 authoring 부담을 줄인다.
    /// </summary>
    private void ApplyStaticVisuals()
    {
        if (bossNameText == null)
        {
            ApplyGroggyLabel(false);
            return;
        }

        string resolvedBossName = string.IsNullOrWhiteSpace(displayNameOverride)
            ? targetBoss.EnemyName
            : displayNameOverride;

        if (string.IsNullOrWhiteSpace(resolvedBossName))
            resolvedBossName = targetBoss.gameObject.name;

        bossNameText.text = resolvedBossName;
        ApplyGroggyLabel(targetBoss != null && targetBoss.HasGroggyTag());
    }

    private void RefreshAll()
    {
        ApplyGroggyLabel(targetBoss != null && targetBoss.HasGroggyTag());

        if (healthBarUI != null)
            healthBarUI.SetHealthRatio(targetBoss.CurrentHealthRatio);

        if (groggyBarUI != null)
        {
            bool isGroggy = targetBoss.HasGroggyTag();
            groggyBarUI.SetVisible(true);
            groggyBarUI.SetGroggyMode(isGroggy);
            groggyBarUI.SetGroggyRatio(GetGroggyRatio());
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

    /// <summary>
    /// 책임 :
    /// - 기절 중이면 경과 시간 비율(0→1)을 반환해 남은 시간 표현을 기존과 반대로 뒤집는다.
    /// - 기절 중이 아니면 스태거 게이지 누적의 역비율(1→0)을 반환해 피격될수록 슬라이더가 줄어들게 만든다.
    /// </summary>
    private float GetGroggyRatio()
    {
        if (_staggerGaugeSystem == null) return 0f;

        // 기절 중 : 경과 시간 비율(남은 시간 표현 반전)
        if (_effectRunner != null)
        {
            GameplayEffect groggyEffect = _staggerGaugeSystem.staggeredEffect;
            if (groggyEffect != null && groggyEffect.duration > 0f)
            {
                float remaining = _effectRunner.GetRemainingTime(groggyEffect, targetBoss.gameObject);
                if (remaining > 0.001f)
                {
                    float elapsedNormalized = 1f - Mathf.Clamp01(remaining / groggyEffect.duration);
                    return elapsedNormalized;
                }
            }
        }

        // 기절 아닐 때 : 스태거 게이지 누적 역비율
        if (targetBoss.AttributeSet == null) return 0f;
        if (_staggerGaugeSystem.currentGaugeAttribute == null || _staggerGaugeSystem.maxGaugeAttribute == null) return 0f;

        float current = targetBoss.AttributeSet.GetAttributeValue(_staggerGaugeSystem.currentGaugeAttribute);
        float max = targetBoss.AttributeSet.GetAttributeValue(_staggerGaugeSystem.maxGaugeAttribute);

        return max > 0f ? 1f - Mathf.Clamp01(current / max) : 0f;
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
            if (groggyStateText != null)
                groggyStateText.gameObject.SetActive(visible && targetBoss != null && targetBoss.HasGroggyTag());
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
