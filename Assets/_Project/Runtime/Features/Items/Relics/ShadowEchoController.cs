using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;
using UnityGAS.Sample;

/// <summary>실제 대시 실행 시작/종료를 구독하고 출발점에 생성한 잔향을 token 수명에 묶는다.</summary>
[DisallowMultipleComponent]
public sealed class ShadowEchoController : MonoBehaviour
{
    private readonly List<ShadowEchoActor> echoes = new();
    private readonly Dictionary<AbilitySpec, ShadowEchoActor> pendingDashes = new();
    private UnityEngine.Object token;
    private AbilitySystem system;
    private RelicLogic_ShadowEcho settings;
    private bool subscribed;

    public void Attach(UnityEngine.Object nextToken, AbilitySystem owner, RelicLogic_ShadowEcho data)
    {
        Unsubscribe();
        ClearEchoes();
        token = nextToken;
        system = owner;
        settings = data;
        Subscribe();
    }

    public void Detach(UnityEngine.Object ownerToken)
    {
        if (token != null && ownerToken != null && token != ownerToken) return;
        Unsubscribe();
        ClearEchoes();
        token = null;
        system = null;
        settings = null;
    }

    private void Subscribe()
    {
        if (subscribed || !isActiveAndEnabled || token == null || system == null) return;
        system.AbilityExecutionStarted += OnExecutionStarted;
        system.AbilityExecutionEnded += OnExecutionEnded;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (subscribed && system != null)
        {
            system.AbilityExecutionStarted -= OnExecutionStarted;
            system.AbilityExecutionEnded -= OnExecutionEnded;
        }
        subscribed = false;
    }

    private void OnExecutionStarted(AbilitySpec spec)
    {
        if (token == null || settings == null || spec == null || spec.Definition != settings.dashAbility ||
            CombatTargetDeathUtility.IsPlayerDeathSequenceRunning(transform)) return;
        var body = GetComponentInChildren<PlayerAnimatorController2D>();
        SpriteRenderer source = body != null ? body.PrimaryVisualRenderer : null;
        IStatProvider stats = AbilityStatProviderFactory.Create(system);
        if (source == null || source.sprite == null || stats == null) return;

        ShadowEchoActor echo = Instantiate(settings.echoPrefab, transform.position, Quaternion.identity);
        echoes.Add(echo);
        pendingDashes[spec] = echo;
        echo.Initialize(system, settings, source, new CombatHitPayload
        {
            sourceSystem = system,
            damageEffect = settings.damageEffect,
            causer = echo.gameObject,
            finalHpDamage = Mathf.Max(0f, stats.Get(StatId.AttackFinal)),
            isCriticalHit = false,
            elementBuildUps = Array.Empty<ElementDamageResult>(),
            elementHpDamages = Array.Empty<ElementDamageResult>(),
            hasResolvedElementBuildUps = true
        });
    }

    private void OnExecutionEnded(AbilitySpec spec, bool cancelled)
    {
        if (spec == null || !pendingDashes.TryGetValue(spec, out ShadowEchoActor echo)) return;
        pendingDashes.Remove(spec);
        if (echo == null) return;
        // The start event precedes the dash logic. Invalid/zero-motion executions must never explode.
        if (cancelled || spec.GetInt(AbilityLogic_Dash2D.CompletedKey, 0) == 0)
            echo.Cancel();
        else echo.ConfirmDash();
    }

    private void Update()
    {
        if (token == null || system == null || CombatTargetDeathUtility.IsPlayerDeathSequenceRunning(transform))
        {
            ClearEchoes();
            return;
        }
        for (int i = echoes.Count - 1; i >= 0; i--)
            if (echoes[i] == null) echoes.RemoveAt(i);
    }

    private void ClearEchoes()
    {
        foreach (ShadowEchoActor echo in echoes)
            if (echo != null) echo.Cancel();
        echoes.Clear();
        pendingDashes.Clear();
    }

    private void OnEnable() => Subscribe();
    private void OnDisable() { Unsubscribe(); ClearEchoes(); }
    private void OnDestroy() { Unsubscribe(); ClearEchoes(); }
}
