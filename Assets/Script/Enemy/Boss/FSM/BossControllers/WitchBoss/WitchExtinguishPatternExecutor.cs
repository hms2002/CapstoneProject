using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

public sealed class WitchExtinguishPatternExecutor : MonoBehaviour
{
    // 이 클래스의 책임:
    // 마녀 보스의 촛불 끄기 패턴 1회 실행에서 촛대 선택, 경고, 폭발 판정, 안개/연출 생성을 전담한다.

    private readonly List<AttackTelegraphView> activeWarningViews = new();
    private Witch owner;

    private void Awake()
    {
        owner = GetComponent<Witch>();
    }

    /// <summary>촛불 끄기 패턴 시작을 시도하고 경고 시간을 실행 지속시간으로 반환합니다.</summary>
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

    /// <summary>현재 저장된 촛불 끄기 패턴 선택 데이터를 기반으로 폭발을 마무리합니다.</summary>
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
            return selections;

        selections.Add(nearestCandle);

        Candlestick randomCandle = GetRandomAvailableCandleExcluding(nearestCandle);
        if (randomCandle != null)
            selections.Add(randomCandle);

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

    /// <summary>촛대 위치에 Fog를 생성합니다.</summary>
    private bool SpawnFog(Vector3 center)
    {
        if (owner == null || owner.FogPrefab == null)
            return false;

        GameObject fogInstance = Instantiate(owner.FogPrefab, owner.GetFogSpawnPosition(center), Quaternion.identity);
        if (fogInstance == null)
            return false;

        fogInstance.transform.localScale = Vector3.Scale(fogInstance.transform.localScale, owner.ResolveExtinguishFogSpawnScaleMultiplierValue());
        return true;
    }

    /// <summary>촛불 끄기 패턴의 폭발 비주얼/파티클/사운드/카메라 셰이크를 함께 재생합니다.</summary>
    private bool PlayExplosionPresentation(Vector3 center)
    {
        if (owner == null)
            return false;

        Transform parent = owner.ExtinguishExplosionVisualSocket;
        bool hasSpawnedVisual = SpawnPresentationPrefab(
            owner.ResolveExtinguishExplosionVisualPrefabValue(),
            center + owner.ResolveExtinguishExplosionVisualOffsetValue(),
            owner.ResolveExtinguishExplosionVisualScaleValue(),
            parent);
        bool hasSpawnedParticle = SpawnPresentationPrefab(
            owner.ResolveExtinguishExplosionParticlePrefabValue(),
            center + owner.ResolveExtinguishExplosionParticleOffsetValue(),
            owner.ResolveExtinguishExplosionParticleScaleValue(),
            parent);

        SoundPlaybackUtility.Play(
            owner.ResolveExtinguishExplosionSoundValue(),
            instigator: owner.gameObject,
            causer: owner.gameObject,
            target: owner.CurrentTarget != null ? owner.CurrentTarget.gameObject : null,
            position: center,
            sourceObject: this);

        Vector3 shakeDirection = owner.CurrentTarget != null ? owner.CurrentTarget.position - center : Vector3.up;
        owner.ResolveExtinguishExplosionCameraShakeValue().TryPlay(owner.gameObject, shakeDirection, debugReason: "Witch.ExtinguishExplosion");
        return hasSpawnedVisual || hasSpawnedParticle;
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
