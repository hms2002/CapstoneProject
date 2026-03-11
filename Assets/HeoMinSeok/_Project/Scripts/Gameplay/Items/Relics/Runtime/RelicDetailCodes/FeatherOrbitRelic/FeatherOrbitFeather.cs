using Ink.Parsed;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[RequireComponent(typeof(Collider2D))]
public class FeatherOrbitFeather : MonoBehaviour
{
    private FeatherOrbitController _controller;
    private int _index;

    // 같은 적 재타격 제한
    private readonly Dictionary<GameObject, float> _lastHitTime = new();

    [Header("Target Filter (Optional)")]
    public LayerMask targetLayers = ~0; // 필요하면 Enemy 레이어만 지정

    public void Bind(FeatherOrbitController controller, int index)
    {
        _controller = controller;
        _index = index;

        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => TryHit(other);
    private void OnTriggerStay2D(Collider2D other) => TryHit(other);

    private void TryHit(Collider2D other)
    {
        if (_controller == null) return;
        var system = _controller.System;
        if (system == null) return;

        if (((1 << other.gameObject.layer) & targetLayers.value) == 0)
            return;

        // 타겟 결정: 보통 AttributeSet/AbilitySystem/Health가 붙은 루트가 실제 타겟
        GameObject target = FindDamageTarget(other);
        if (target == null) return;

        float now = Time.time;
        float cd = _controller.GetPerTargetHitCooldown();

        if (_lastHitTime.TryGetValue(target, out float t) && now - t < cd)
            return;
        _lastHitTime[target] = now;

        // 데미지 계산
        float hpDmg = _controller.ComputeHpDamage();
        if (hpDmg <= 0f) return;

        // 너희 시그니처에 맞춰 호출
        CombatDamageAction.ApplyDamageAndEmitHit(
            system: system,
            spec: null,                        // 깃털은 특정 AbilitySpec이 없으니 null로 OK
            damageEffect: _controller.DamageEffect,
            target: target,
            finalHpDamage: hpDmg,
            finalStaggerBuildUp: 0f,
            elementBuildUps: null,
            finalKnockbackImpulse: _controller.KnockbackImpulse,
            hitConfirmedTag: _controller.HitConfirmedTag,
            causer: gameObject
        );
    }

    private static GameObject FindDamageTarget(Collider2D other)
    {
        // 1) AttributeSet이 있는 부모를 우선
        var attrs = other.GetComponentInParent<AttributeSet>();
        if (attrs != null) return attrs.gameObject;

        // 2) AbilitySystem이 있는 부모
        var asys = other.GetComponentInParent<AbilitySystem>();
        if (asys != null) return asys.gameObject;

        // 3) 그냥 해당 오브젝트
        return other.gameObject;
    }
}
