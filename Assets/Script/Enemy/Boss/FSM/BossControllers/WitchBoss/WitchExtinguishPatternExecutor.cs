using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

public sealed class WitchExtinguishPatternExecutor : MonoBehaviour
{
    // 이 클래스의 책임:
    // 마녀 보스의 촛불 끄기 패턴 1회 실행에서 촛대 선택, 경고, 폭발 판정, 안개/연출 생성을 전담한다.

    /// <summary>
    /// 책임 :
    /// - 촛불 끄기 패턴 1회 실행에 필요한 순수 데이터와 연출 파라미터를 executor에 전달한다.
    /// - executor가 Witch 내부 로직 캐스팅이나 개별 Get/Resolve 함수 없이도 패턴을 수행하게 만든다.
    /// </summary>
    public readonly struct PatternContext
    {
        public PatternContext(
            float warningTimeSeconds,
            AttackTelegraphStyle warningStyle,
            GameObject fogPrefab,
            GE_Damage_Spec damageEffect,
            float damageAmount,
            Vector3 fogSpawnScaleMultiplier,
            float attackRadiusMultiplier,
            GameObject explosionVisualPrefab,
            GameObject explosionParticlePrefab,
            Vector3 explosionVisualOffset,
            Vector3 explosionVisualScale,
            Vector3 explosionParticleOffset,
            Vector3 explosionParticleScale,
            SoundRef explosionSound,
            CameraShakeHook explosionCameraShake)
        {
            WarningTimeSeconds = warningTimeSeconds;
            WarningStyle = warningStyle;
            FogPrefab = fogPrefab;
            DamageEffect = damageEffect;
            DamageAmount = damageAmount;
            FogSpawnScaleMultiplier = fogSpawnScaleMultiplier;
            AttackRadiusMultiplier = attackRadiusMultiplier;
            ExplosionVisualPrefab = explosionVisualPrefab;
            ExplosionParticlePrefab = explosionParticlePrefab;
            ExplosionVisualOffset = explosionVisualOffset;
            ExplosionVisualScale = explosionVisualScale;
            ExplosionParticleOffset = explosionParticleOffset;
            ExplosionParticleScale = explosionParticleScale;
            ExplosionSound = explosionSound;
            ExplosionCameraShake = explosionCameraShake;
        }

        public float WarningTimeSeconds { get; }
        public AttackTelegraphStyle WarningStyle { get; }
        public GameObject FogPrefab { get; }
        public GE_Damage_Spec DamageEffect { get; }
        public float DamageAmount { get; }
        public Vector3 FogSpawnScaleMultiplier { get; }
        public float AttackRadiusMultiplier { get; }
        public GameObject ExplosionVisualPrefab { get; }
        public GameObject ExplosionParticlePrefab { get; }
        public Vector3 ExplosionVisualOffset { get; }
        public Vector3 ExplosionVisualScale { get; }
        public Vector3 ExplosionParticleOffset { get; }
        public Vector3 ExplosionParticleScale { get; }
        public SoundRef ExplosionSound { get; }
        public CameraShakeHook ExplosionCameraShake { get; }
    }

    private readonly List<AttackTelegraphView> activeWarningViews = new();
    private Witch owner;

    private void Awake()
    {
        owner = GetComponent<Witch>();
    }

    /// <summary>촛불 끄기 패턴 시작을 시도하고 경고 시간을 실행 지속시간으로 반환합니다.</summary>
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

        if (context.FogPrefab == null)
        {
            Debug.LogWarning("[WitchExtinguishPatternExecutor] 시작 실패: FogPrefab이 없습니다.", owner);
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

        float attackRadius = CalculateAttackRadius(context.FogPrefab, context.FogSpawnScaleMultiplier, context.AttackRadiusMultiplier);
        if (attackRadius <= 0f)
        {
            Debug.LogWarning(
                $"[WitchExtinguishPatternExecutor] 시작 실패: attackRadius가 유효하지 않습니다. radius={attackRadius}, multiplier={context.AttackRadiusMultiplier}, fogScale={context.FogSpawnScaleMultiplier}",
                owner);
            return false;
        }

        List<Vector3> extinguishCenters = new(selectedCandles.Count);
        for (int i = 0; i < selectedCandles.Count; i++)
            extinguishCenters.Add(owner.GetCandleCenter(selectedCandles[i]));

        owner.RuntimeData.SetExtinguishSelections(selectedCandles, extinguishCenters);
        owner.PlayPatternAttackMotion();
        ShowWarnings(extinguishCenters, resolvedDurationSeconds, attackRadius, context.WarningStyle);
        Debug.Log(
            $"[WitchExtinguishPatternExecutor] 촛불 끄기 시작 성공: selected={selectedCandles.Count}, total={Candlestick.Instances.Count}, sealed={owner.GetSealedCandleCount()}, radius={attackRadius}",
            owner);
        return true;
    }

    /// <summary>현재 저장된 촛불 끄기 패턴 선택 데이터를 기반으로 폭발을 마무리합니다.</summary>
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

    /// <summary>촛불 끄기 패턴 경고와 선택 상태를 즉시 정리합니다.</summary>
    public void CancelPattern()
    {
        DestroyWarningViews();
        owner?.RuntimeData.ClearExtinguishSelection();
    }

    /// <summary>가장 가까운 촛대와 추가 랜덤 촛대를 선택 버퍼로 구성합니다.</summary>
    private List<Candlestick> BuildSelectionBuffer()
    {
        List<Candlestick> selections = new();
        if (owner == null)
            return selections;

        Candlestick nearestCandle = owner.GetNearestCandle();
        if (nearestCandle == null)
        {
            Debug.LogWarning(
                $"[WitchExtinguishPatternExecutor] nearest candle을 찾지 못했습니다. total={Candlestick.Instances.Count}, sealed={owner.GetSealedCandleCount()}",
                owner);
            return selections;
        }

        selections.Add(nearestCandle);

        Candlestick randomCandle = GetRandomAvailableCandleExcluding(nearestCandle);
        if (randomCandle != null)
            selections.Add(randomCandle);
        else
            Debug.Log(
                "[WitchExtinguishPatternExecutor] 추가 랜덤 촛대가 없어 가장 가까운 촛대만 사용합니다.",
                owner);

        return selections;
    }

    /// <summary>지정한 촛대를 제외하고 미봉인 촛대 하나를 랜덤으로 고릅니다.</summary>
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

    /// <summary>촛불 끄기 패턴의 원형 경고를 여러 개 동시에 표시합니다.</summary>
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

    /// <summary>촛불 끄기 패턴에서 사용한 경고 뷰들을 정리합니다.</summary>
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

    /// <summary>플레이어에게 폭발 피해를 적용합니다.</summary>
    private bool TryHitPlayer(Vector3 center, in PatternContext context)
    {
        if (owner == null || owner.CurrentTarget == null || owner.AbilitySystem == null || context.DamageEffect == null)
            return false;

        float fogRadius = CalculateAttackRadius(context.FogPrefab, context.FogSpawnScaleMultiplier, context.AttackRadiusMultiplier);
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

    /// <summary>촛대 위치에 Fog를 생성합니다.</summary>
    private bool SpawnFog(Vector3 center, in PatternContext context)
    {
        if (context.FogPrefab == null)
            return false;

        GameObject fogInstance = Instantiate(
            context.FogPrefab,
            center - GetFogOffset(context.FogPrefab, context.FogSpawnScaleMultiplier),
            Quaternion.identity);
        if (fogInstance == null)
            return false;

        fogInstance.transform.localScale = Vector3.Scale(fogInstance.transform.localScale, context.FogSpawnScaleMultiplier);
        return true;
    }

    /// <summary>촛불 끄기 패턴의 폭발 비주얼/파티클/사운드/카메라 셰이크를 함께 재생합니다.</summary>
    private bool PlayExplosionPresentation(Vector3 center, in PatternContext context)
    {
        if (owner == null)
            return false;

        Transform parent = owner.ExtinguishExplosionVisualSocket;
        bool hasSpawnedVisual = SpawnPresentationPrefab(
            context.ExplosionVisualPrefab,
            center + context.ExplosionVisualOffset,
            context.ExplosionVisualScale,
            parent);
        bool hasSpawnedParticle = SpawnPresentationPrefab(
            context.ExplosionParticlePrefab,
            center + context.ExplosionParticleOffset,
            context.ExplosionParticleScale,
            parent);

        SoundPlaybackUtility.Play(
            context.ExplosionSound,
            instigator: owner.gameObject,
            causer: owner.gameObject,
            target: owner.CurrentTarget != null ? owner.CurrentTarget.gameObject : null,
            position: center,
            sourceObject: this);

        Vector3 shakeDirection = owner.CurrentTarget != null ? owner.CurrentTarget.position - center : Vector3.up;
        context.ExplosionCameraShake.TryPlay(owner.gameObject, shakeDirection, debugReason: "Witch.ExtinguishExplosion");
        return hasSpawnedVisual || hasSpawnedParticle;
    }

    /// <summary>지정한 Fog 프리팹 기준으로 촛불 끄기 패턴의 실제 공격 반경을 계산합니다.</summary>
    private static float CalculateAttackRadius(GameObject fogPrefab, Vector3 fogSpawnScaleMultiplier, float attackRadiusMultiplier)
    {
        float fogRadius = GetFogRadius(fogPrefab, fogSpawnScaleMultiplier);
        return fogRadius * Mathf.Max(0f, attackRadiusMultiplier);
    }

    /// <summary>지정한 Fog 프리팹의 실반경을 배율 보정까지 포함해 반환합니다.</summary>
    private static float GetFogRadius(GameObject fogPrefab, Vector3 fogSpawnScaleMultiplier)
    {
        if (fogPrefab == null)
            return 0f;

        CircleCollider2D fogCollider = fogPrefab.GetComponent<CircleCollider2D>();
        if (fogCollider == null)
            return 0f;

        Vector3 scale = Vector3.Scale(fogPrefab.transform.localScale, fogSpawnScaleMultiplier);
        float xRadius = fogCollider.radius * Mathf.Abs(scale.x);
        float yRadius = fogCollider.radius * Mathf.Abs(scale.y);
        return Mathf.Max(xRadius, yRadius);
    }

    /// <summary>지정한 Fog 프리팹의 오프셋을 배율 보정까지 포함해 계산합니다.</summary>
    private static Vector3 GetFogOffset(GameObject fogPrefab, Vector3 fogSpawnScaleMultiplier)
    {
        if (fogPrefab == null)
            return Vector3.zero;

        CircleCollider2D fogCollider = fogPrefab.GetComponent<CircleCollider2D>();
        if (fogCollider == null)
            return Vector3.zero;

        Vector3 scale = Vector3.Scale(fogPrefab.transform.localScale, fogSpawnScaleMultiplier);
        return new Vector3(
            fogCollider.offset.x * scale.x,
            fogCollider.offset.y * scale.y,
            0f);
    }

    /// <summary>촛불 끄기 패턴용 연출 프리팹을 생성하고 배율 보정을 적용합니다.</summary>
    private static bool SpawnPresentationPrefab(GameObject prefab, Vector3 position, Vector3 scaleMultiplier, Transform parent)
    {
        if (prefab == null)
            return false;

        GameObject instance = Instantiate(prefab, position, Quaternion.identity, parent);
        if (instance == null)
            return false;

        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, scaleMultiplier);
        return true;
    }
}
