using System;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 런의 남은 제한 시간을 진실 소스로 관리하고 GamePlayDataManager와 동기화한다.
/// - 현재 스테이지 정책에 따라 시간 감소 여부를 판정하고, 시간 초과 시 런 실패를 트리거한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RunTimeLimitSystem : MonoBehaviour
{
    [Header("Binding")]
    [SerializeField] private RunTimeLimitConfig config;
    [SerializeField] private MonoBehaviour stageTimerPolicySource;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging;

    public event Action<float> OnRemainingTimeChanged;
    public event Action OnTimeExpired;

    public float RemainingSeconds => remainingSeconds;
    public bool IsRunning => isRunning;
    public bool IsLowTime => config != null && remainingSeconds <= config.LowTimeWarningSeconds;

    private IStageTimerPolicy stageTimerPolicy;
    private float remainingSeconds;
    private bool isRunning;
    private bool hasInitializedFromRun;

    private void Awake()
    {
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

    private void Update()
    {
        if (!isRunning || config == null)
            return;

        if (stageTimerPolicy != null && !stageTimerPolicy.ShouldTick())
            return;

        Tick(Time.deltaTime);
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
}
