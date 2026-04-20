using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임 :
/// - Canvas에 붙어 GraphicRaycaster 활성 상태를 상태 기반으로 동기화하는 공통 흐름을 제공한다.
/// - 하위 클래스가 '지금 raycast를 켜야 하는가'만 판단하도록 만들고, 실제 적용과 캐시 관리는 공통으로 맡는다.
/// </summary>
public abstract class CanvasRaycastGateBase : MonoBehaviour
{
    [SerializeField] private GraphicRaycaster graphicRaycaster;

    private bool? lastAppliedState;

    protected virtual void Awake()
    {
        ResolveGraphicRaycaster();
        RefreshRaycastState();
    }

    protected virtual void OnEnable()
    {
        RefreshRaycastState();
    }

    protected virtual void LateUpdate()
    {
        RefreshRaycastState();
    }

    protected void RefreshRaycastState()
    {
        GraphicRaycaster raycaster = ResolveGraphicRaycaster();
        if (raycaster == null)
            return;

        bool shouldEnable = ShouldEnableRaycast();
        if (lastAppliedState.HasValue && lastAppliedState.Value == shouldEnable)
            return;

        raycaster.enabled = shouldEnable;
        lastAppliedState = shouldEnable;
    }

    protected abstract bool ShouldEnableRaycast();

    private GraphicRaycaster ResolveGraphicRaycaster()
    {
        if (graphicRaycaster == null)
            graphicRaycaster = GetComponent<GraphicRaycaster>();

        if (graphicRaycaster == null)
            Debug.LogWarning($"[{GetType().Name}] GraphicRaycaster reference is missing.", this);

        return graphicRaycaster;
    }
}
