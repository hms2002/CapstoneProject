using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;
#if UNITY_EDITOR
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// 책임 : 플레이어 상호작용을 받아 포탈 이동을 요청하는 진입점이다.
/// 실제 경로 해석은 현재 런 계획을 가진 PortalRouteManager에 위임한다.
/// </summary>
public sealed class ScenePortal : InteractableBase
{
    [SerializeField, HideInInspector] private string portalId;

    [Header("Transition Semantic")]
    [SerializeField] private TransitionType transitionType = TransitionType.None;

    [Header("Start Run Route Catalog")]
    [SerializeField] private RunRouteCatalogSO startRunRouteCatalog;

    [Header("Interact")]
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "이동하기";

    [Header("Optional Visual")]
    [SerializeField] private GameObject highlightTarget;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Cleanup Before Capture")]
    [SerializeField] private List<GameplayTagSet> sceneTravelCleanupTagSets = new();

    private bool isTransitioning;

    private MaterialPropertyBlock propBlock;
    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");

#if UNITY_EDITOR
    private float nextEditorDiagnosticLogTime;
    private string lastEditorDiagnosticMessage;
#endif

    public string PortalId => portalId;
    public TransitionType PortalTransitionType => transitionType;
    public RunRouteCatalogSO StartRunRouteCatalog => startRunRouteCatalog;
    public IReadOnlyList<GameplayTagSet> SceneTravelCleanupTagSets => sceneTravelCleanupTagSets;

    private void Awake()
    {
        EnsurePortalId();

        propBlock = new MaterialPropertyBlock();
        OnUnHighlight();
    }

    private void OnEnable()
    {
        EnsurePendingStartRunPlan();
    }

    private void Reset()
    {
        EnsurePortalId();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnValidate()
    {
        EnsurePortalId();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public override void OnPlayerLeave()
    {
        OnUnHighlight();
    }

    public override void OnHighlight()
    {
        SetOutline(true);

        if (highlightTarget != null)
            highlightTarget.SetActive(true);
    }

    public override void OnUnHighlight()
    {
        SetOutline(false);

        if (highlightTarget != null)
            highlightTarget.SetActive(false);
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        bool hasPlayer = player != null;
        bool isIdle = hasPlayer && player.CurrentState == InteractState.Idle;
        PortalRouteManager routeManager = PortalRouteManager.EnsureInstance();
        bool canResolve = routeManager != null &&
                          routeManager.CanResolveRoute(this);

        bool canInteract =
            !isTransitioning &&
            hasPlayer &&
            isIdle &&
            canResolve;

#if UNITY_EDITOR
        EmitEditorDiagnosticIfBlocked(player, hasPlayer, isIdle, canResolve, canInteract);
#endif

        return canInteract;
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        isTransitioning = true;
        OnUnHighlight();

        player.SetInteractState(InteractState.None);

        if (!ScenePortalTravelService.TryTravel(this))
        {
            isTransitioning = false;
            player.SetInteractState(InteractState.Idle);
        }
    }

    public override InteractState GetInteractType() => InteractState.Idle;
    public override string GetInteractDescription() => interactPromptText;
    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    private void SetOutline(bool enabled)
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(OutlineEnabledID, enabled ? 1f : 0f);
        spriteRenderer.SetPropertyBlock(propBlock);
    }

    private void EnsurePortalId()
    {
        if (string.IsNullOrWhiteSpace(portalId) || HasDuplicatePortalId())
            portalId = Guid.NewGuid().ToString("N");
    }

    private void EnsurePendingStartRunPlan()
    {
        if (transitionType != TransitionType.HubToRunStart)
            return;

        PortalRouteManager routeManager = PortalRouteManager.EnsureInstance();
        if (routeManager == null)
            return;

        routeManager.EnsurePendingPlan(this);
    }

    private bool HasDuplicatePortalId()
    {
        if (string.IsNullOrWhiteSpace(portalId))
            return false;

        var portals = FindObjectsByType<ScenePortal>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < portals.Length; i++)
        {
            var other = portals[i];
            if (other == null || other == this)
                continue;

            if (other.portalId == portalId)
                return true;
        }

        return false;
    }

#if UNITY_EDITOR
    private void EmitEditorDiagnosticIfBlocked(
        IPlayerInteractor player,
        bool hasPlayer,
        bool isIdle,
        bool canResolve,
        bool canInteract)
    {
        if (canInteract || !Application.isPlaying || transitionType != TransitionType.HubToRunStart)
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.Equals(activeScene.name, "ProtoTypeHub", StringComparison.OrdinalIgnoreCase))
            return;

        float now = Time.unscaledTime;
        if (now < nextEditorDiagnosticLogTime)
            return;

        PortalRouteManager routeManager = PortalRouteManager.EnsureInstance();
        string routeDebug = routeManager != null
            ? routeManager.GetDebugResolveStatus(this)
            : "manager=null";

        string message =
            $"[ScenePortal] Hub start portal blocked. portal={name}, " +
            $"isTransitioning={isTransitioning}, hasPlayer={hasPlayer}, " +
            $"playerState={(hasPlayer ? player.CurrentState.ToString() : "<none>")}, " +
            $"isIdle={isIdle}, canResolve={canResolve}, route={routeDebug}";

        if (string.Equals(lastEditorDiagnosticMessage, message, StringComparison.Ordinal))
        {
            nextEditorDiagnosticLogTime = now + 1f;
            return;
        }

        lastEditorDiagnosticMessage = message;
        nextEditorDiagnosticLogTime = now + 1f;
        Debug.LogWarning(message, this);
    }
#endif
}
