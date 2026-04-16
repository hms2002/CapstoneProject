using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 런의 남은 제한 시간을 진실 소스로 관리하고 GamePlayDataManager와 동기화한다.
/// - 현재 스테이지 정책에 따라 시간 감소 여부를 판정하고, 시간 초과 시 런 실패를 트리거한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RunTimeLimitSystem : MonoBehaviour
{
    public static RunTimeLimitSystem Instance { get; private set; }
    public static event Action<RunTimeLimitSystem> InstanceChanged;

    [Header("Binding")]
    [SerializeField] private RunTimeLimitConfig config;
    [SerializeField] private MonoBehaviour stageTimerPolicySource;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging;

    public event Action<float> OnRemainingTimeChanged;
    public event Action OnTimeExpired;

    public float RemainingSeconds => remainingSeconds;
    public float InactivePreviewSeconds => config != null ? Mathf.Max(0f, config.InitialLimitSeconds) : 0f;
    public bool IsRunning => isRunning;
    public bool IsLowTime => config != null && remainingSeconds <= config.LowTimeWarningSeconds;
    public bool IsExternallyPaused => HasExternalPauseBlockers();
    public bool IsPausedByStagePolicy => ShouldPauseByStagePolicy();
    public bool IsVisuallyPaused => IsExternallyPaused || IsPausedByStagePolicy;

    private IStageTimerPolicy stageTimerPolicy;
    private float remainingSeconds;
    private bool isRunning;
    private bool hasInitializedFromRun;
    private readonly Dictionary<int, UnityEngine.Object> externalPauseOwners = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InstanceChanged?.Invoke(this);
        DontDestroyOnLoad(gameObject);

        stageTimerPolicy = stageTimerPolicySource as IStageTimerPolicy;
        if (stageTimerPolicy == null && stageTimerPolicySource != null)
        {
            Debug.LogWarning("[RunTimeLimitSystem] Assigned stage timer policy source does not implement IStageTimerPolicy.", this);
        }
    }

    private void OnEnable()
    {
        if (GamePlayDataManager.Instance != null)
        {
            GamePlayDataManager.Instance.OnRunStarted += HandleRunStarted;
            GamePlayDataManager.Instance.OnRunEnded += HandleRunEnded;
        }
    }

    private void Start()
    {
        TryRestoreOrBootstrapActiveRun();
    }

    private void OnDisable()
    {
        if (GamePlayDataManager.Instance != null)
        {
            GamePlayDataManager.Instance.OnRunStarted -= HandleRunStarted;
            GamePlayDataManager.Instance.OnRunEnded -= HandleRunEnded;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            InstanceChanged?.Invoke(null);
        }
    }

    private void Update()
    {
        if (!isRunning || config == null)
            return;

        if (HasExternalPauseBlockers())
            return;

        if (ShouldPauseByStagePolicy())
            return;

        Tick(Time.deltaTime);
    }

    /// <summary>
    /// 책임 :
    /// - 외부 시스템이 런 제한 시간 감소를 일시정지/해제할 수 있는 공용 진입점을 제공한다.
    /// - owner 단위로 pause 요청을 집계해, 여러 시스템이 동시에 시간을 멈춰도 안전하게 관리한다.
    /// </summary>
    public void SetExternalPause(UnityEngine.Object owner, bool paused)
    {
        if (owner == null)
            return;

        int ownerId = owner.GetInstanceID();
        if (paused)
        {
            externalPauseOwners[ownerId] = owner;
            return;
        }

        externalPauseOwners.Remove(ownerId);
    }

    /// <summary>
    /// 책임 :
    /// - 런 도중 씬이 바뀐 뒤에도 남은 시간을 복원해 타이머를 끊김 없이 이어간다.
    /// - 최초 시작 시 저장된 시간이 없으면 config 초기값으로 런 타이머를 부트스트랩한다.
    /// </summary>
    private void TryRestoreOrBootstrapActiveRun()
    {
        if (hasInitializedFromRun || config == null || GamePlayDataManager.Instance == null)
            return;

        GamePlayData data = GamePlayDataManager.Instance.Data;
        if (data == null || !data.isRunActive)
            return;

        float savedRemaining = GamePlayDataManager.Instance.GetRunRemainingSeconds();
        if (savedRemaining > 0f)
        {
            SetRemainingTimeInternal(savedRemaining, persistToGamePlayData: false);
            isRunning = true;
            hasInitializedFromRun = true;
            return;
        }

        InitializeRemainingTime(config.InitialLimitSeconds);
    }

    private void HandleRunStarted()
    {
        if (config == null)
        {
            Debug.LogWarning("[RunTimeLimitSystem] RunTimeLimitConfig is missing. Timer will not start.", this);
            return;
        }

        InitializeRemainingTime(config.InitialLimitSeconds);
    }

    private void HandleRunEnded(RunEndReason reason)
    {
        isRunning = false;
        hasInitializedFromRun = false;
        SetRemainingTimeInternal(0f, persistToGamePlayData: false);

        if (verboseLogging)
            Debug.Log($"[RunTimeLimitSystem] Run ended. reason={reason}", this);
    }

    private void InitializeRemainingTime(float initialSeconds)
    {
        isRunning = true;
        hasInitializedFromRun = true;
        SetRemainingTimeInternal(initialSeconds, persistToGamePlayData: true);

        if (verboseLogging)
            Debug.Log($"[RunTimeLimitSystem] Initialized remaining time. seconds={remainingSeconds:0.00}", this);
    }

    private void Tick(float deltaTime)
    {
        float clampedDelta = Mathf.Max(0f, deltaTime);
        if (clampedDelta <= 0f)
            return;

        float next = Mathf.Max(0f, remainingSeconds - clampedDelta);
        SetRemainingTimeInternal(next, persistToGamePlayData: true);

        if (GamePlayDataManager.Instance != null)
            GamePlayDataManager.Instance.TickRunTimer(clampedDelta);

        if (remainingSeconds > 0f)
            return;

        isRunning = false;
        OnTimeExpired?.Invoke();

        if (verboseLogging)
            Debug.Log("[RunTimeLimitSystem] Time expired. Ending run.", this);

        if (GamePlayDataManager.Instance != null && GamePlayDataManager.Instance.Data.isRunActive)
            GamePlayDataManager.Instance.EndRun(config.TimeoutReason);
    }

    private void SetRemainingTimeInternal(float seconds, bool persistToGamePlayData)
    {
        remainingSeconds = Mathf.Max(0f, seconds);

        if (persistToGamePlayData && GamePlayDataManager.Instance != null)
            GamePlayDataManager.Instance.SetRunRemainingSeconds(remainingSeconds);

        OnRemainingTimeChanged?.Invoke(remainingSeconds);
    }

    /// <summary>
    /// 책임 :
    /// - 현재 살아 있는 외부 pause 요청자가 있는지 판정한다.
    /// - 파괴된 owner를 정리해, 일시정지 해제가 누락돼도 타이머가 영구 정지되지 않게 보완한다.
    /// </summary>
    private bool HasExternalPauseBlockers()
    {
        if (externalPauseOwners.Count == 0)
            return false;

        List<int> deadOwnerIds = null;
        foreach (KeyValuePair<int, UnityEngine.Object> pair in externalPauseOwners)
        {
            if (pair.Value != null)
                return true;

            deadOwnerIds ??= new List<int>();
            deadOwnerIds.Add(pair.Key);
        }

        if (deadOwnerIds != null)
        {
            for (int i = 0; i < deadOwnerIds.Count; i++)
                externalPauseOwners.Remove(deadOwnerIds[i]);
        }

        return false;
    }

    /// <summary>
    /// 책임 :
    /// - 현재 스테이지 정책이 런 제한 시간을 멈춰야 하는 상태인지 판정한다.
    /// - HUD와 타이머 본체가 동일한 기준으로 "지금 시간이 흐르지 않는다"를 해석하게 만든다.
    /// </summary>
    private bool ShouldPauseByStagePolicy()
    {
        return stageTimerPolicy != null && !stageTimerPolicy.ShouldTick();
    }
}
