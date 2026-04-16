using System.Collections.Generic;
using CapstonePresentation;
using UnityEngine;
using UnityGAS;

public sealed class WitchExtinguishPatternExecutor : MonoBehaviour
{
    private readonly List<AttackTelegraphView> activeWarningViews = new();
    private Witch owner;

    private void Awake()
    {
        owner = GetComponent<Witch>();
    }

    public bool TryBeginPattern(float warningTime, out float resolvedDurationSeconds)
    {
        resolvedDurationSeconds = Mathf.Max(0f, warningTime);

        if (owner == null || owner.ExtinguishTelegraphService == null || owner.FogPrefab == null)
            return false;

        List<Candlestick> selectedCandles = BuildSelectionBuffer();
        if (selectedCandles.Count == 0)
            return false;

        if (owner.GetExtinguishAttackRadiusValue() <= 0f)
            return false;

        List<Vector3> extinguishCenters = new(selectedCandles.Count);
        for (int i = 0; i < selectedCandles.Count; i++)
            extinguishCenters.Add(owner.GetCandleCenter(selectedCandles[i]));

        owner.RuntimeData.SetExtinguishSelections(selectedCandles, extinguishCenters);
        owner.PlayPatternAttackMotion();
        ShowWarnings(extinguishCenters, resolvedDurationSeconds);
        return true;
    }

    public void CompletePattern()
    {
        if (owner == null || !owner.RuntimeData.HasActiveExtinguishSelection)
            return;

        IReadOnlyList<Candlestick> extinguishCandles = owner.RuntimeData.SelectedCandles;
        IReadOnlyList<Vector3> extinguishCenters = owner.RuntimeData.SelectedCenters;

        for (int i = 0; i < extinguishCenters.Count; i++)
        {
            Vector3 extinguishCenter = extinguishCenters[i];
            TryHitPlayer(extinguishCenter);
            SpawnFog(extinguishCenter);
            PlayExplosionPresentation(extinguishCenter);
        }

        for (int i = 0; i < extinguishCandles.Count; i++)
        {
            Candlestick candle = extinguishCandles[i];
            if (candle != null)
                candle.Seal();
        }

        CancelPattern();
    }

    public void CancelPattern()
    {
        DestroyWarningViews();
        owner?.RuntimeData.ClearExtinguishSelection();
    }

    private List<Candlestick> BuildSelectionBuffer()
    {
        List<Candlestick> selections = new();
        if (owner == null)
            return selections;

        Candlestick nearestCandle = owner.GetNearestCandle();
        if (nearestCandle == null)
            return selections;

        selections.Add(nearestCandle);

        Candlestick randomCandle = GetRandomAvailableCandleExcluding(nearestCandle);
        if (randomCandle != null)
            selections.Add(randomCandle);

        return selections;
    }

    private Candlestick GetRandomAvailableCandleExcluding(Candlestick excludedCandle)
    {
        List<Candlestick> availableCandles = new();

        for (int i = 0; i < Candlestick.Instances.Count; i++)
        {
            Candlestick candle = Candlestick.Instances[i];
            if (candle == null || candle.IsSealed || candle == excludedCandle)
                continue;

            availableCandles.Add(candle);
        }

        if (availableCandles.Count == 0)
            return null;

        int randomIndex = Random.Range(0, availableCandles.Count);
        return availableCandles[randomIndex];
    }

    private void ShowWarnings(IReadOnlyList<Vector3> centers, float warningTime)
    {
        DestroyWarningViews();

        if (owner == null || owner.ExtinguishTelegraphService == null || centers == null)
            return;

        float warningDiameter = owner.GetExtinguishAttackRadiusValue() * 2f;
        float clampedWarningTime = Mathf.Max(0f, warningTime);

        for (int i = 0; i < centers.Count; i++)
        {
            AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
                centers[i],
                warningDiameter,
                clampedWarningTime,
                owner.ExtinguishWarningStyle);

            AttackTelegraphView view = owner.ExtinguishTelegraphService.SpawnDetachedView(spec);
            if (view != null)
                activeWarningViews.Add(view);
        }
    }

    private void DestroyWarningViews()
    {
        for (int i = 0; i < activeWarningViews.Count; i++)
        {
            AttackTelegraphView view = activeWarningViews[i];
            if (view != null)
                Destroy(view.gameObject);
        }

        activeWarningViews.Clear();
    }

    private bool TryHitPlayer(Vector3 center)
    {
        if (owner == null || owner.CurrentTarget == null || owner.AbilitySystem == null || owner.ExtinguishDamageEffect == null)
            return false;

        float fogRadius = owner.GetExtinguishAttackRadiusValue();
        Vector2 toTarget = (Vector2)(owner.CurrentTarget.position - center);
        if (toTarget.sqrMagnitude > fogRadius * fogRadius)
            return false;

        CombatDamageSnapshot snapshot = new(
            finalHpDamage: owner.ExtinguishDamage,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            elementBuildUps: null,
            isCriticalHit: false);

        CombatHitPayload payload = CombatHitPayload.FromSnapshot(
            sourceSystem: owner.AbilitySystem,
            sourceSpec: null,
            damageEffect: owner.ExtinguishDamageEffect,
            knockbackEffect: null,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: owner.gameObject);

        return CombatHitPayloadApplier.Apply(owner.CurrentTarget.gameObject, payload, center);
    }

    private bool SpawnFog(Vector3 center)
    {
        if (owner == null)
            return false;

        SpawnedPresentationHook fogPresentation = owner.ResolveExtinguishFogPresentationValue();
        if (!fogPresentation.HasContent)
            return false;

        GameObject fogInstance = PresentationSpawnService.SpawnPersistent(
            fogPresentation,
            WorldPresentationContext.AtWorld(
                instigator: owner.gameObject,
                position: owner.GetFogSpawnPosition(center),
                fallbackDirection: Vector3.up,
                target: owner.CurrentTarget != null ? owner.CurrentTarget.gameObject : null,
                sourceObject: this,
                rotation: Quaternion.identity,
                causer: owner.gameObject));

        return fogInstance != null;
    }

    private bool PlayExplosionPresentation(Vector3 center)
    {
        if (owner == null)
            return false;

        WorldPresentationHook explosionPresentation = owner.ResolveExtinguishExplosionPresentationValue();
        if (!explosionPresentation.HasAnyContent)
            return false;

        Vector3 shakeDirection = owner.CurrentTarget != null
            ? owner.CurrentTarget.position - center
            : Vector3.up;
        if (shakeDirection.sqrMagnitude <= 0.0001f)
            shakeDirection = Vector3.up;

        WorldPresentationRuntime.Play(
            explosionPresentation,
            WorldPresentationContext.AtWorld(
                instigator: owner.gameObject,
                position: center,
                fallbackDirection: shakeDirection,
                target: owner.CurrentTarget != null ? owner.CurrentTarget.gameObject : null,
                sourceObject: this,
                rotation: Quaternion.identity,
                causer: owner.gameObject));
        return true;
    }
}
