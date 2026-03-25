using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 플레이어 상호작용을 받아 포탈 이동을 요청하는 진입점이다.
/// 실제 경로 해석은 현재 런 계획을 가진 PortalRouteManager에 위임한다.
/// </summary>
public sealed class ScenePortal : MonoBehaviour, IInteractable
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

    [Header("Cleanup Before Capture")]
    [SerializeField] private List<GameplayTagSet> sceneTravelCleanupTagSets = new();

    private bool isTransitioning;

    public string PortalId => portalId;
    public TransitionType PortalTransitionType => transitionType;
    public RunRouteCatalogSO StartRunRouteCatalog => startRunRouteCatalog;
    public IReadOnlyList<GameplayTagSet> SceneTravelCleanupTagSets => sceneTravelCleanupTagSets;

    private void Awake()
    {
        EnsurePortalId();
    }

    private void OnEnable()
    {
        EnsurePendingStartRunPlan();
    }

    private void Reset()
    {
        EnsurePortalId();
    }

    private void OnValidate()
    {
        EnsurePortalId();
    }

    public void OnPlayerNearby()
    {
    }

    public void OnPlayerLeave()
    {
        OnUnHighlight();
    }

    public void GetInteract(string text) { }

    public void OnHighlight()
    {
        if (highlightTarget != null)
            highlightTarget.SetActive(true);
    }

    public void OnUnHighlight()
    {
        if (highlightTarget != null)
            highlightTarget.SetActive(false);
    }

    public bool CanInteract(IPlayerInteractor player)
    {
        bool canResolve = PortalRouteManager.Instance != null &&
            PortalRouteManager.Instance.CanResolveRoute(this);

        return
            !isTransitioning &&
            player != null &&
            player.CurrentState == InteractState.Idle &&
            canResolve;
    }

    public void OnPlayerInteract(IPlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        isTransitioning = true;
        player.SetInteractState(InteractState.None);

        if (!ScenePortalTravelService.TryTravel(this))
        {
            isTransitioning = false;
            player.SetInteractState(InteractState.Idle);
        }
    }

    public InteractState GetInteractType() => InteractState.Idle;
    public string GetInteractDescription() => interactPromptText;
    public Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    private void EnsurePortalId()
    {
        if (string.IsNullOrWhiteSpace(portalId) || HasDuplicatePortalId())
            portalId = Guid.NewGuid().ToString("N");
    }

    private void EnsurePendingStartRunPlan()
    {
        if (transitionType != TransitionType.HubToRunStart)
            return;

        if (PortalRouteManager.Instance == null)
            return;

        PortalRouteManager.Instance.EnsurePendingPlan(this);
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
}
