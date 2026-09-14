using System;
using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>
/// 책임: 내려찍기 후 십자 경고와 순차 폭발의 생성/피해 문맥/회수를 소유하며, 기존 술통 낙하와 병행한다.
/// 이펙트 내부의 피해 활성 타이밍은 공용 ITimedHitEffect2D 계약과 애니메이션 이벤트에 위임한다.
/// 별도 파편의 재생과 회수는 공용 presentation 서비스가 소유한다.
/// 착지 자세는 마지막 폭발 애니메이션까지 유지하고, 완료/취소 시 해제한다.
/// </summary>
public sealed partial class AbilityLogic_DragonSlam
{
    private static readonly Vector2[] CrossDirections = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
    private readonly RaycastHit2D[] crossWallHits = new RaycastHit2D[8];

    [Header("Cross Explosion")]
    [SerializeField] private GameObject crossExplosionPrefab;
    [SerializeField] private SpawnedPresentationHook crossExplosionParticle = new()
    {
        scaleMultiplier = Vector3.one,
        lifetimeMode = PresentationLifetimeMode.AutoDetect,
    };
    [SerializeField, Min(0f)] private float crossWarningSeconds = 0.3f;
    [SerializeField, Min(0.1f)] private float crossMaxDistance = 40f;
    [SerializeField, Min(0.1f)] private float crossExplosionDiameter = 1.35f;
    [SerializeField, Min(0.1f)] private float crossExplosionSpacing = 1.35f;
    [SerializeField, Min(0f)] private float crossExplosionStepInterval = 0.04f;
    [SerializeField, Min(0.01f)] private float crossExplosionLifetime = 0.5f;
    [SerializeField, Min(0f)] private float crossExplosionDamage = 1f;
    [SerializeField, Min(0f)] private float crossExplosionKnockback = 12f;
    [SerializeField] private LayerMask crossExplosionWallLayers;
    [SerializeField, Min(0f)] private float crossExplosionWallSkin = 0.03f;
    [SerializeField] private SoundRef crossExplosionSound;

    private IEnumerator RunLandingFollowups(
        DragonController dragon, AbilitySystem system, IAttackTelegraphPresenter telegraphs,
        Vector2 origin, AbilitySpec spec)
    {
        IEnumerator crossRoutine = RunCrossExplosionWave(dragon, telegraphs, origin, spec);
        Coroutine crossCoroutine = null;
        try
        {
            crossCoroutine = system.StartCoroutine(crossRoutine);
            yield return ScatterKegsAfterImpact(dragon, system, telegraphs, origin, spec);
            if (crossCoroutine != null)
                yield return crossCoroutine;
        }
        finally
        {
            if (system != null && crossCoroutine != null)
                system.StopCoroutine(crossCoroutine);
            // StopCoroutine alone does not guarantee disposal of a separately started iterator.
            (crossRoutine as IDisposable)?.Dispose();
        }
    }

    private IEnumerator RunCrossExplosionWave(
        DragonController dragon, IAttackTelegraphPresenter telegraphs, Vector2 origin, AbilitySpec spec)
    {
        if (dragon == null || crossExplosionPrefab == null || IsAbilityCancelled(spec))
            yield break;

        GameObject prefab = PresentationAssetPlayback.ResolvePrefab(crossExplosionPrefab);
        if (prefab.GetComponent<ITimedHitEffect2D>() == null || prefab.GetComponent<CircleCollider2D>() == null)
        {
            Debug.LogWarning("[DragonSlam] Cross explosion prefab requires a root timed hit effect and circle collider.", this);
            yield break;
        }

        float[] lengths = BuildCrossExplosionLengths(origin);
        float spacing = Mathf.Max(0.1f, crossExplosionSpacing);
        int maxSteps = 0;
        var warnings = new List<IAttackTelegraphHandle>(4);
        var explosions = new List<GameObject>();
        var registry = new SharedHitRegistry2D();
        CombatHitPayload payload = MakeCrossExplosionPayload(dragon, spec);
        bool holdingLandingPose = false;
        try
        {
            for (int i = 0; i < CrossDirections.Length; i++)
            {
                int steps = Mathf.FloorToInt(lengths[i] / spacing);
                maxSteps = Mathf.Max(maxSteps, steps);
                if (steps > 0 && telegraphs != null && crossWarningSeconds > 0f)
                {
                    warnings.Add(telegraphs.SpawnDetachedView(CreateCrossWarning(origin, CrossDirections[i], steps)));
                }
            }

            if (maxSteps == 0)
                yield break;

            dragon.SetLandingPoseHeld(true);
            holdingLandingPose = true;
            yield return WaitForSecondsUnlessCancelled(crossWarningSeconds, spec);
            HideCrossWarnings(warnings);
            for (int step = 1; step <= maxSteps; step++)
            {
                if (IsAbilityCancelled(spec) || dragon == null)
                    yield break;

                bool soundPlayed = false;
                void PlayStepSound()
                {
                    if (soundPlayed || dragon == null || IsAbilityCancelled(spec))
                        return;
                    soundPlayed = true;
                    SoundPlaybackUtility.Play(crossExplosionSound, instigator: dragon.gameObject,
                        causer: dragon.gameObject, position: origin, sourceObject: this);
                }

                float distance = step * spacing;
                for (int i = 0; i < CrossDirections.Length; i++)
                {
                    if (distance > lengths[i])
                        continue;
                    Vector2 center = origin + CrossDirections[i] * distance;
                    GameObject explosion = SpawnCrossExplosion(prefab, center,
                        payload, registry, ResolveTargetMask(dragon), PlayStepSound);
                    explosions.Add(explosion);
                    // Detached one-shots keep their particle tail after the short damage animation ends.
                    if (crossExplosionParticle.HasContent)
                        WorldPresentationPlayback.SpawnOneShot(crossExplosionParticle,
                            WorldPresentationContext.AtWorld(dragon.gameObject, center, Vector3.up, sourceObject: this));
                }

                if (step < maxSteps)
                    yield return WaitForSecondsUnlessCancelled(crossExplosionStepInterval, spec);
            }

            // Keep cleanup ownership until the final hit window and animation have finished.
            yield return WaitForSecondsUnlessCancelled(crossExplosionLifetime, spec);
        }
        finally
        {
            if (holdingLandingPose && dragon != null)
                dragon.SetLandingPoseHeld(false);
            HideCrossWarnings(warnings);
            foreach (GameObject explosion in explosions)
            {
                if (explosion == null)
                    continue;
                explosion.SetActive(false);
                Object.Destroy(explosion);
            }
        }
    }

    private float[] BuildCrossExplosionLengths(Vector2 origin)
    {
        float maxDistance = Mathf.Max(0.1f, crossMaxDistance);
        var lengths = new float[CrossDirections.Length];
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(crossExplosionWallLayers);
        for (int i = 0; i < CrossDirections.Length; i++)
        {
            float length = maxDistance;
            int count = Physics2D.CircleCast(origin, Mathf.Max(0.1f, crossExplosionDiameter) * 0.5f,
                CrossDirections[i], filter, crossWallHits, maxDistance);
            for (int hitIndex = 0; hitIndex < count; hitIndex++)
                length = Mathf.Min(length, Mathf.Max(0f, crossWallHits[hitIndex].distance - crossExplosionWallSkin));
            lengths[i] = length;
        }
        return lengths;
    }

    private AttackTelegraphSpec CreateCrossWarning(Vector2 origin, Vector2 direction, int steps)
    {
        float spacing = Mathf.Max(0.1f, crossExplosionSpacing);
        float diameter = Mathf.Max(0.1f, crossExplosionDiameter);
        float firstDistance = spacing;
        float lastDistance = steps * spacing;
        var warning = AttackTelegraphSpec.CreateRectangle(
            origin + direction * ((firstDistance + lastDistance) * 0.5f),
            new Vector2(lastDistance - firstDistance + diameter, diameter),
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, crossWarningSeconds, impactTelegraphStyle);
        return AttackTelegraphSpecUtility.WithThinWarningOutlineOnly(warning);
    }

    private GameObject SpawnCrossExplosion(GameObject prefab, Vector2 center, CombatHitPayload payload,
        SharedHitRegistry2D registry, LayerMask targetLayers, Action onHitWindowOpened)
    {
        GameObject explosion = Object.Instantiate(prefab, center, Quaternion.identity);
        float diameter = Mathf.Max(0.1f, crossExplosionDiameter);
        explosion.transform.localScale = Vector3.Scale(explosion.transform.localScale, new Vector3(diameter, diameter, 1f));
        CircleCollider2D hit = explosion.GetComponent<CircleCollider2D>();
        Vector3 scale = explosion.transform.lossyScale;
        hit.radius = diameter * 0.5f / Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), 0.01f);
        // Preserve the sprite pivot and authored foot offset while centering damage on the warned point.
        explosion.transform.position -= explosion.transform.TransformVector(hit.offset);
        ITimedHitEffect2D timed = explosion.GetComponent<ITimedHitEffect2D>();
        timed.ConfigureHitCollision(new Collider2D[] { hit }, targetLayers);
        timed.Play(Mathf.Max(0.01f, crossExplosionLifetime), payload, registry, onHitWindowOpened);
        return explosion;
    }

    private CombatHitPayload MakeCrossExplosionPayload(DragonController dragon, AbilitySpec spec)
    {
        return CombatHitPayload.FromSnapshot(dragon.AbilitySystem, spec, damageEffect, knockbackEffect,
            new CombatDamageSnapshot(Mathf.Max(0f, crossExplosionDamage), 0f, Mathf.Max(0f, crossExplosionKnockback), false),
            null, dragon.gameObject);
    }

    private static void HideCrossWarnings(List<IAttackTelegraphHandle> warnings)
    {
        foreach (IAttackTelegraphHandle warning in warnings)
            warning?.HideImmediate();
        warnings.Clear();
    }
}
