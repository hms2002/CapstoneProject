using System.Collections.Generic;
using CapstonePresentation;
using UnityEngine;
using UnityGAS;

public sealed class WitchExtinguishPatternExecutor : MonoBehaviour
{
    public readonly struct PatternContext
    {
        public PatternContext(
            float warningTimeSeconds,
            AttackTelegraphStyle warningStyle,
            GE_Damage_Spec damageEffect,
            float damageAmount,
            SpawnedPresentationHook fogPresentation,
            float attackRadiusMultiplier,
            WorldPresentationHook explosionPresentation)
        {
            WarningTimeSeconds = warningTimeSeconds;
            WarningStyle = warningStyle;
            DamageEffect = damageEffect;
            DamageAmount = damageAmount;
            FogPresentation = fogPresentation;
            AttackRadiusMultiplier = attackRadiusMultiplier;
            ExplosionPresentation = explosionPresentation;
        }

        public float WarningTimeSeconds { get; }
        public AttackTelegraphStyle WarningStyle { get; }
        public GE_Damage_Spec DamageEffect { get; }
        public float DamageAmount { get; }
        public SpawnedPresentationHook FogPresentation { get; }
        public float AttackRadiusMultiplier { get; }
        public WorldPresentationHook ExplosionPresentation { get; }
    }

    private readonly List<AttackTelegraphView> activeWarningViews = new();
    private Witch owner;

    private void Awake()
    {
        owner = GetComponent<Witch>();
    }

    public bool TryBeginPattern(in PatternContext context, out float resolvedDurationSeconds)
    {
        resolvedDurationSeconds = Mathf.Max(0f, context.WarningTimeSeconds);

        if (owner == null)
        {
            Debug.LogWarning("[WitchExtinguishPatternExecutor] 시작 실패: owner가 없습니다.", this);
            return false;
        }

        if (owner.ExtinguishTelegraphService == null)
        {
            Debug.LogWarning("[WitchExtinguishPatternExecutor] 시작 실패: ExtinguishTelegraphService가 없습니다.", owner);
            return false;
        }

        if (!context.FogPresentation.HasContent)
        {
            Debug.LogWarning("[WitchExtinguishPatternExecutor] 시작 실패: FogPresentation이 없습니다.", owner);
            return false;
        }

        List<Candlestick> selectedCandles = BuildSelectionBuffer();
        if (selectedCandles.Count == 0)
        {
            Debug.LogWarning(
                $"[WitchExtinguishPatternExecutor] 시작 실패: 선택 가능한 촛대가 없습니다. total={Candlestick.Instances.Count}, sealed={owner.GetSealedCandleCount()}",
                owner);
            return false;
        }

        float attackRadius = CalculateAttackRadius(context.FogPresentation, context.AttackRadiusMultiplier);
        if (attackRadius <= 0f)
        {
            Debug.LogWarning(
                $"[WitchExtinguishPatternExecutor] 시작 실패: attackRadius가 유효하지 않습니다. radius={attackRadius}, multiplier={context.AttackRadiusMultiplier}",
                owner);
            return false;
        }

        List<Vector3> extinguishCenters = new(selectedCandles.Count);
        for (int i = 0; i < selectedCandles.Count; i++)
            extinguishCenters.Add(owner.GetCandleCenter(selectedCandles[i]));

        owner.RuntimeData.SetExtinguishSelections(selectedCandles, extinguishCenters);
        owner.PlayPatternAttackMotion();
        ShowWarnings(extinguishCenters, resolvedDurationSeconds, attackRadius, context.WarningStyle);
        return true;
    }

    public void CompletePattern(in PatternContext context)
    {
        if (owner == null || !owner.RuntimeData.HasActiveExtinguishSelection)
            return;

        IReadOnlyList<Candlestick> extinguishCandles = owner.RuntimeData.SelectedCandles;
        IReadOnlyList<Vector3> extinguishCenters = owner.RuntimeData.SelectedCenters;

        for (int i = 0; i < extinguishCenters.Count; i++)
        {
            Vector3 extinguishCenter = extinguishCenters[i];
            TryHitPlayer(extinguishCenter, context);
            SpawnFog(extinguishCenter, context);
            PlayExplosionPresentation(extinguishCenter, context);
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

    private void ShowWarnings(IReadOnlyList<Vector3> centers, float warningTime, float attackRadius, AttackTelegraphStyle warningStyle)
    {
        DestroyWarningViews();

        if (owner == null || owner.ExtinguishTelegraphService == null || centers == null)
            return;

        float warningDiameter = attackRadius * 2f;
        float clampedWarningTime = Mathf.Max(0f, warningTime);

        for (int i = 0; i < centers.Count; i++)
        {
            AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
                centers[i],
                warningDiameter,
                clampedWarningTime,
                warningStyle);

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

    private bool TryHitPlayer(Vector3 center, in PatternContext context)
    {
        if (owner == null || owner.CurrentTarget == null || owner.AbilitySystem == null || context.DamageEffect == null)
            return false;

        float fogRadius = CalculateAttackRadius(context.FogPresentation, context.AttackRadiusMultiplier);
        Vector2 toTarget = (Vector2)(owner.CurrentTarget.position - center);
        if (toTarget.sqrMagnitude > fogRadius * fogRadius)
            return false;

        CombatDamageSnapshot snapshot = new(
            finalHpDamage: context.DamageAmount,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            elementBuildUps: null,
            isCriticalHit: false);

        CombatHitPayload payload = CombatHitPayload.FromSnapshot(
            sourceSystem: owner.AbilitySystem,
            sourceSpec: null,
            damageEffect: context.DamageEffect,
            knockbackEffect: null,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: owner.gameObject);

        return CombatHitPayloadApplier.Apply(owner.CurrentTarget.gameObject, payload, center);
    }

    private bool SpawnFog(Vector3 center, in PatternContext context)
    {
        if (!context.FogPresentation.HasContent)
            return false;

        GameObject fogInstance = PresentationSpawnService.SpawnPersistent(
            context.FogPresentation,
            WorldPresentationContext.AtWorld(
                instigator: owner.gameObject,
                position: GetFogSpawnPosition(context.FogPresentation, center),
                fallbackDirection: Vector3.up,
                target: owner.CurrentTarget != null ? owner.CurrentTarget.gameObject : null,
                sourceObject: this,
                rotation: Quaternion.identity,
                causer: owner.gameObject));

        return fogInstance != null;
    }

    private bool PlayExplosionPresentation(Vector3 center, in PatternContext context)
    {
        if (!context.ExplosionPresentation.HasAnyContent)
            return false;

        Vector3 shakeDirection = owner != null && owner.CurrentTarget != null
            ? owner.CurrentTarget.position - center
            : Vector3.up;
        if (shakeDirection.sqrMagnitude <= 0.0001f)
            shakeDirection = Vector3.up;

        WorldPresentationRuntime.Play(
            context.ExplosionPresentation,
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

    private static float CalculateAttackRadius(SpawnedPresentationHook fogPresentation, float attackRadiusMultiplier)
    {
        float fogRadius = GetFogRadius(fogPresentation);
        return fogRadius * Mathf.Max(0f, attackRadiusMultiplier);
    }

    private static float GetFogRadius(SpawnedPresentationHook fogPresentation)
    {
        GameObject fogPrefab = fogPresentation.prefab;
        if (fogPrefab == null)
            return 0f;

        CircleCollider2D fogCollider = fogPrefab.GetComponent<CircleCollider2D>();
        if (fogCollider == null)
            return 0f;

        Vector3 scale = Vector3.Scale(fogPrefab.transform.localScale, fogPresentation.EffectiveScaleMultiplier);
        float xRadius = fogCollider.radius * Mathf.Abs(scale.x);
        float yRadius = fogCollider.radius * Mathf.Abs(scale.y);
        return Mathf.Max(xRadius, yRadius);
    }

    private static Vector3 GetFogOffset(SpawnedPresentationHook fogPresentation)
    {
        GameObject fogPrefab = fogPresentation.prefab;
        if (fogPrefab == null)
            return Vector3.zero;

        CircleCollider2D fogCollider = fogPrefab.GetComponent<CircleCollider2D>();
        if (fogCollider == null)
            return Vector3.zero;

        Vector3 scale = Vector3.Scale(fogPrefab.transform.localScale, fogPresentation.EffectiveScaleMultiplier);
        return new Vector3(
            fogCollider.offset.x * scale.x,
            fogCollider.offset.y * scale.y,
            0f);
    }

    private static Vector3 GetFogSpawnPosition(SpawnedPresentationHook fogPresentation, Vector3 center)
    {
        return center - GetFogOffset(fogPresentation);
    }
}
