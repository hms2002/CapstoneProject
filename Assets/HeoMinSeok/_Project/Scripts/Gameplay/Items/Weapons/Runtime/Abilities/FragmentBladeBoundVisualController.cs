using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 파편검에 결속된 조각 sprite들의 표시 상태를 FragmentBladeRuntimeData와 동기화한다.
/// - Bound 상태의 조각만 보이고 Detached/Returning/Piercing 조각은 검에서 빠진 것처럼 숨긴다.
/// </summary>
[DisallowMultipleComponent]
public sealed class FragmentBladeBoundVisualController : MonoBehaviour
{
    [SerializeField] private FragmentBladeRuntimeState runtimeState;
    [SerializeField] private List<SpriteRenderer> boundShardRenderers = new();

    private void Awake()
    {
        CacheRuntimeState();
        AutoCollectRenderersIfEmpty();
        ApplyState();
    }

    private void OnEnable()
    {
        ApplyState();
    }

    private void LateUpdate()
    {
        ApplyState();
    }

    /// <summary>
    /// 책임 :
    /// - 현재 자식 SpriteRenderer를 결속 조각 목록으로 자동 수집한다.
    /// - 프리팹 구성 직후 수동 연결을 놓쳤을 때 최소한의 authoring 안전망을 제공한다.
    /// </summary>
    public void AutoCollectRenderers()
    {
        boundShardRenderers.Clear();
        GetComponentsInChildren(includeInactive: true, boundShardRenderers);
    }

    private void ApplyState()
    {
        FragmentBladeRuntimeData data = runtimeState != null ? runtimeState.BoundData : null;
        if (data == null)
        {
            SetAllVisible(true);
            return;
        }

        IReadOnlyList<FragmentBladeRuntimeData.ShardRuntimeState> shards = data.Shards;
        int count = Mathf.Min(boundShardRenderers.Count, shards.Count);

        for (int i = 0; i < count; i++)
        {
            SpriteRenderer renderer = boundShardRenderers[i];
            if (renderer == null || shards[i] == null)
                continue;

            renderer.enabled = shards[i].IsBound;
        }

        for (int i = count; i < boundShardRenderers.Count; i++)
        {
            if (boundShardRenderers[i] != null)
                boundShardRenderers[i].enabled = false;
        }
    }

    private void SetAllVisible(bool visible)
    {
        for (int i = 0; i < boundShardRenderers.Count; i++)
        {
            if (boundShardRenderers[i] != null)
                boundShardRenderers[i].enabled = visible;
        }
    }

    private void CacheRuntimeState()
    {
        if (runtimeState == null)
            runtimeState = GetComponentInParent<FragmentBladeRuntimeState>(true);
    }

    private void AutoCollectRenderersIfEmpty()
    {
        if (boundShardRenderers == null)
            boundShardRenderers = new List<SpriteRenderer>();

        if (boundShardRenderers.Count == 0)
            AutoCollectRenderers();
    }
}
