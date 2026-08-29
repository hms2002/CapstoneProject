using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// R키 비전투 진입, 후보 세션의 pause/input lock, 연속 미수령 보상 처리를 담당한다.
/// 실제 카드 UI는 이 컴포넌트의 이벤트와 공개 명령에 연결된 authored UI가 소유한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class LevelRewardSessionController : MonoBehaviour
{
    [Header("Reward Data")]
    [SerializeField] private LevelRewardCatalogSO rewardCatalog;
    [SerializeField, Min(0)] private int maxRerollsPerOffer = 5;

    [Header("Open Rule")]
    [SerializeField] private KeyCode openKey = KeyCode.R;
    [SerializeField, Min(0f)] private float combatGraceSeconds = 3f;
    [SerializeField] private string combatBlockedMessage = "전투가 끝난 뒤 할 수 있어";

    private GameFlowInputBlocker inputBlocker;
    private RunTimeLimitSystem pausedTimer;
    private readonly List<LevelRewardDefinitionSO> eligibilityBuffer = new();
    private float lastPlayerCombatRealtime = float.NegativeInfinity;
    private bool isSessionOpen;

    public event Action SessionOpened;
    public event Action SessionChanged;
    public event Action SessionClosed;
    public event Action<string> OpenRejected;

    public bool IsSessionOpen => isSessionOpen;
    public IReadOnlyList<LevelRewardDefinitionSO> Candidates => RunLevelRewardOffers.CurrentCandidates;
    public int PendingRewardCount => RunLevelProgression.State?.pendingRewardCount ?? 0;
    public int OfferSeed => RunLevelProgression.State?.rewardRandomSeed ?? 0;
    public int OfferSequence => RunLevelProgression.State?.activeRewardOffer?.offerSequence ?? 0;
    public int RerollsUsed => RunLevelRewardOffers.RerollsUsed;
    public int MaxRerolls => RunLevelRewardOffers.MaxRerolls;
    public bool CanReroll => RunLevelRewardOffers.CanReroll;
    public bool CanOpenSession => !isSessionOpen && EvaluateOpenEligibility(out _, out _);

    private void OnEnable()
    {
        RunLevelRewards.RegisterCatalog(rewardCatalog);
        CombatActivityEvents.DamageApplied += HandleDamageApplied;
        RunSessionStore.OnRunEnded += HandleRunEnded;
        PlayerRuntimeRegistry.PlayerUnregistered += HandlePlayerUnregistered;
    }

    private void OnDisable()
    {
        CombatActivityEvents.DamageApplied -= HandleDamageApplied;
        RunSessionStore.OnRunEnded -= HandleRunEnded;
        PlayerRuntimeRegistry.PlayerUnregistered -= HandlePlayerUnregistered;
        CloseSession();
    }

    private void Update()
    {
        if (!isSessionOpen && openKey != KeyCode.None && InputActionQuery.WasKeyPressedThisFrame(openKey))
            TryOpenSession(out _);
    }

    public bool TryOpenSession(out string failureReason)
    {
        failureReason = null;
        if (isSessionOpen)
            return true;

        if (!EvaluateOpenEligibility(out failureReason, out bool showCombatWarning))
            return Reject(failureReason, showCombatWarning);

        if (!RunLevelRewardOffers.TryEnsureOffer(maxRerollsPerOffer, out failureReason))
            return Reject(failureReason, showCombatWarning: false);

        inputBlocker ??= GameFlowInputBlocker.GetOrAdd(this);
        inputBlocker?.Acquire();
        TimeScalePausePlayback.Acquire(this);
        pausedTimer = RunTimeLimitSystem.Instance;
        pausedTimer?.SetExternalPause(this, true);
        isSessionOpen = true;
        SessionOpened?.Invoke();
        SessionChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 현재 R 입력으로 보상 세션을 시작할 수 있는지 상태를 변경하지 않고 검사한다.
    /// HUD는 이 결과만 투영하며 전투/UI 판정을 복제하지 않는다.
    /// </summary>
    public bool EvaluateOpenEligibility(out string failureReason)
    {
        if (isSessionOpen)
        {
            failureReason = "레벨업 선택 세션이 이미 열려 있습니다.";
            return false;
        }

        return EvaluateOpenEligibility(out failureReason, out _);
    }

    public bool TrySelectCandidate(string rewardId, out string failureReason)
    {
        failureReason = null;
        if (!isSessionOpen)
        {
            failureReason = "레벨업 선택 세션이 열려 있지 않습니다.";
            return false;
        }

        if (!RunLevelRewardOffers.TrySelectCandidate(rewardId, out failureReason))
            return false;

        if (PendingRewardCount > 0)
        {
            if (!RunLevelRewardOffers.TryEnsureOffer(maxRerollsPerOffer, out failureReason))
            {
                CloseSession();
                return false;
            }

            SessionChanged?.Invoke();
            return true;
        }

        SessionChanged?.Invoke();
        return true;
    }

    public bool TryReroll(out string failureReason)
    {
        if (!isSessionOpen)
        {
            failureReason = "레벨업 선택 세션이 열려 있지 않습니다.";
            return false;
        }

        bool rerolled = RunLevelRewardOffers.TryReroll(out failureReason);
        if (rerolled)
            SessionChanged?.Invoke();
        return rerolled;
    }

    public bool TryPushSessionUI(IStackableUI ui)
    {
        return isSessionOpen && inputBlocker != null && inputBlocker.TryPushOwnedUI(ui);
    }

    public void CloseSession()
    {
        bool wasOpen = isSessionOpen;
        isSessionOpen = false;
        if (pausedTimer != null)
            pausedTimer.SetExternalPause(this, false);
        pausedTimer = null;
        TimeScalePausePlayback.Release(this);
        inputBlocker?.Release();
        if (wasOpen)
            SessionClosed?.Invoke();
    }

    private bool Reject(string failureReason, bool showCombatWarning)
    {
        if (showCombatWarning)
            WarningPopupPlayback.ShowMessage(string.IsNullOrWhiteSpace(failureReason) ? combatBlockedMessage : failureReason);
        OpenRejected?.Invoke(failureReason);
        return false;
    }

    private bool EvaluateOpenEligibility(out string failureReason, out bool showCombatWarning)
    {
        failureReason = null;
        showCombatWarning = false;

        if (!RunSessionStore.IsRunActive || PendingRewardCount <= 0)
        {
            failureReason = "선택 가능한 레벨업 보상이 없습니다.";
            return false;
        }

        if (PlayerRuntimeRegistry.CurrentPlayer == null)
        {
            failureReason = "현재 플레이어가 등록되지 않았습니다.";
            return false;
        }

        if (DialoguePlayback.IsPlaying || UiInteractionStateQuery.HasBlockingUI() || TimeScalePausePlayback.IsPaused)
        {
            failureReason = "다른 대화 또는 UI가 진행 중입니다.";
            return false;
        }

        bool recentlyInCombat = Time.unscaledTime - lastPlayerCombatRealtime < Mathf.Max(0f, combatGraceSeconds);
        if (recentlyInCombat || Enemy.IsAnyEnemyRecognizingPlayer())
        {
            failureReason = combatBlockedMessage;
            showCombatWarning = true;
            return false;
        }

        if (!HasSelectableRewardCandidate())
        {
            failureReason = "현재 선택 가능한 레벨업 효과가 없습니다.";
            return false;
        }

        return true;
    }

    private bool HasSelectableRewardCandidate()
    {
        LevelRewardOfferState offer = RunLevelProgression.State?.activeRewardOffer;
        if (offer is { isActive: true } && offer.candidateRewardIds is { Count: > 0 })
            return RunLevelRewardOffers.CurrentCandidates.Count > 0;

        RunLevelRewards.CollectEligibleDefinitions(eligibilityBuffer);
        return eligibilityBuffer.Count > 0;
    }

    private void HandleDamageApplied(GameObject source, GameObject target, float amount)
    {
        PlayerInteractor2D player = PlayerRuntimeRegistry.CurrentPlayer;
        if (player == null) return;
        if (IsPlayerOwned(source, player) || IsPlayerOwned(target, player))
            lastPlayerCombatRealtime = Time.unscaledTime;
    }

    private static bool IsPlayerOwned(GameObject candidate, PlayerInteractor2D player)
    {
        if (candidate == null || player == null) return false;
        Transform transform = candidate.transform;
        return transform == player.transform || transform.IsChildOf(player.transform) ||
               candidate.GetComponentInParent<PlayerInteractor2D>() == player;
    }

    private void HandleRunEnded(RunEndReason reason)
    {
        CloseSession();
    }

    private void HandlePlayerUnregistered(PlayerInteractor2D player)
    {
        CloseSession();
    }
}
