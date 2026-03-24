using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 깃털 1개의 실제 접촉 판정과 타겟별 재타격 쿨다운을 담당한다.
/// - 타겟 해석과 피해 적용은 공용 CombatTargetResolver2D / CombatHitPayloadApplier 규약을 사용한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FeatherOrbitFeather : MonoBehaviour
{
    private FeatherOrbitController _controller;
    private int _index;

    // 같은 적 재타격 제한
    private readonly Dictionary<int, float> _lastHitTimeByTargetId = new();

    [Header("Target Filter (Optional)")]
    public LayerMask targetLayers = ~0;

    public void Bind(FeatherOrbitController controller, int index)
    {
        _controller = controller;
        _index = index;

        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => TryHit(other);
    private void OnTriggerStay2D(Collider2D other) => TryHit(other);

    /// <summary>
    /// 책임 :
    /// - 접촉한 Collider2D를 실제 타격 대상으로 해석하고, 타겟별 쿨다운을 검사한 뒤 payload를 적용한다.
    /// </summary>
    private void TryHit(Collider2D other)
    {
        if (_controller == null || other == null)
            return;

        var system = _controller.System;
        if (system == null)
            return;

        GameObject target = CombatTargetResolver2D.ResolveDamageTarget(other);
        if (target == null)
            return;

        if (!IsTargetLayerMatched(other.gameObject.layer, target.layer))
            return;

        int targetId = target.GetInstanceID();
        float now = Time.time;
        float cooldown = _controller.GetPerTargetHitCooldown();

        if (_lastHitTimeByTargetId.TryGetValue(targetId, out float lastTime) && now - lastTime < cooldown)
            return;

        if (!_controller.TryBuildHitPayload(gameObject, out var payload))
            return;

        if (!CombatHitPayloadApplier.Apply(target, payload))
            return;

        _lastHitTimeByTargetId[targetId] = now;
    }

    /// <summary>
    /// 책임 :
    /// - 자식 Collider layer와 실제 타겟 루트 layer 중 하나라도 필터에 맞는지 검사한다.
    /// - 자식 콜라이더/루트 오브젝트 레이어가 다를 때의 누락을 줄인다.
    /// </summary>
    private bool IsTargetLayerMatched(int colliderLayer, int targetLayer)
    {
        int colliderBit = 1 << colliderLayer;
        int targetBit = 1 << targetLayer;

        return ((targetLayers.value & colliderBit) != 0) ||
               ((targetLayers.value & targetBit) != 0);
    }
}