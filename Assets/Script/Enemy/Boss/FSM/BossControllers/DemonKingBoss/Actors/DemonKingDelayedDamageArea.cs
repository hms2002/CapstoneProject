using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

public enum DemonKingDelayedExplosionVfxKind
{
    Default,
    DarkLordExplosion2
}

public sealed class DemonKingDelayedDamageArea : MonoBehaviour
{
    private static readonly Color WarningColor = new(1f, 0.15f, 0.08f, 0.35f);
    private static readonly Color AttackColor = new(1f, 0.85f, 0.2f, 0.65f);
    private const float AttackFlashSeconds = 0.12f;

    public static void SpawnCircle(
        DemonKingController owner,
        Vector2 center,
        float diameter,
        float warningSeconds,
        float damage,
        bool ignoreOwnerGroggy = false,
        DemonKingDelayedExplosionVfxKind explosionVfxKind = DemonKingDelayedExplosionVfxKind.Default,
        SoundRef impactSound = default,
        CameraShakeHook impactCameraShake = default,
        DemonKingVfxCueRef explosionVfxCue = default)
    {
        if (owner == null)
            return;

        GameObject runner = new("DemonKing_DelayedCircle");
        DemonKingDelayedDamageArea area = runner.AddComponent<DemonKingDelayedDamageArea>();
        area.StartCoroutine(area.RunCircle(
            owner,
            center,
            diameter,
            warningSeconds,
            damage,
            ignoreOwnerGroggy,
            explosionVfxKind,
            impactSound,
            impactCameraShake,
            explosionVfxCue));
    }

    public static void SpawnRectangle(
        DemonKingController owner,
        Vector2 center,
        Vector2 size,
        float rotationDeg,
        float warningSeconds,
        float damage,
        bool ignoreOwnerGroggy = false)
    {
        if (owner == null)
            return;

        GameObject runner = new("DemonKing_DelayedRectangle");
        DemonKingDelayedDamageArea area = runner.AddComponent<DemonKingDelayedDamageArea>();
        area.StartCoroutine(area.RunRectangle(owner, center, size, rotationDeg, warningSeconds, damage, ignoreOwnerGroggy));
    }

    public static void SpawnCircleCluster(
        DemonKingController owner,
        IReadOnlyList<Vector2> centers,
        float diameter,
        float warningSeconds,
        float damage,
        bool ignoreOwnerGroggy = false,
        DemonKingDelayedExplosionVfxKind explosionVfxKind = DemonKingDelayedExplosionVfxKind.Default,
        SoundRef impactSound = default,
        CameraShakeHook impactCameraShake = default,
        DemonKingVfxCueRef explosionVfxCue = default)
    {
        if (owner == null || centers == null || centers.Count == 0)
            return;

        GameObject runner = new("DemonKing_DelayedCircleCluster");
        DemonKingDelayedDamageArea area = runner.AddComponent<DemonKingDelayedDamageArea>();
        area.StartCoroutine(area.RunCircleCluster(
            owner,
            centers,
            diameter,
            warningSeconds,
            damage,
            ignoreOwnerGroggy,
            explosionVfxKind,
            impactSound,
            impactCameraShake,
            explosionVfxCue));
    }

    private IEnumerator RunCircle(
        DemonKingController owner,
        Vector2 center,
        float diameter,
        float warningSeconds,
        float damage,
        bool ignoreOwnerGroggy,
        DemonKingDelayedExplosionVfxKind explosionVfxKind,
        SoundRef impactSound,
        CameraShakeHook impactCameraShake,
        DemonKingVfxCueRef explosionVfxCue)
    {
        owner.GetTelegraphService()?.SpawnDetachedView(
            AttackTelegraphSpecUtility.WithThinWarningOutline(
                DemonKingCombatUtil.CreateTopDownCircleWarningSpec(owner, center, diameter, warningSeconds)));

        if (warningSeconds > 0f)
            yield return new WaitForSeconds(warningSeconds);

        if (owner != null && !owner.IsDead && (ignoreOwnerGroggy || !owner.HasGroggyTag()))
        {
            CombatHitPayload payload = DemonKingCombatUtil.MakePayload(owner, owner.DefaultDamageEffect, damage);
            bool presentationPlayed = false;
            void PlayImpactPresentationOnce()
            {
                if (presentationPlayed)
                    return;

                presentationPlayed = true;
                PlayImpactPresentation(owner, center, impactSound, impactCameraShake, "DemonKing.DelayedCircleImpact");
            }

            bool timed = TrySpawnTimedExplosionVfx(
                owner,
                center,
                diameter,
                AttackColor,
                "DemonKing_ExplosionCircleAttack",
                explosionVfxKind,
                explosionVfxCue,
                payload,
                null,
                PlayImpactPresentationOnce,
                out _);

            if (!timed)
            {
                PlayImpactPresentationOnce();
                DemonKingCombatUtil.ApplyTopDownEllipseDamage(
                    owner,
                    center,
                    diameter,
                    owner.DefaultDamageEffect,
                    damage);
            }
        }

        Destroy(gameObject);
    }

    private IEnumerator RunCircleCluster(
        DemonKingController owner,
        IReadOnlyList<Vector2> centers,
        float diameter,
        float warningSeconds,
        float damage,
        bool ignoreOwnerGroggy,
        DemonKingDelayedExplosionVfxKind explosionVfxKind,
        SoundRef impactSound,
        CameraShakeHook impactCameraShake,
        DemonKingVfxCueRef explosionVfxCue)
    {
        for (int i = 0; i < centers.Count; i++)
        {
            Vector2 center = centers[i];
            owner.GetTelegraphService()?.SpawnDetachedView(
                AttackTelegraphSpecUtility.WithThinWarningOutline(
                    DemonKingCombatUtil.CreateTopDownCircleWarningSpec(owner, center, diameter, warningSeconds)));
        }

        if (warningSeconds > 0f)
            yield return new WaitForSeconds(warningSeconds);

        if (owner != null && !owner.IsDead && (ignoreOwnerGroggy || !owner.HasGroggyTag()))
        {
            CombatHitPayload payload = DemonKingCombatUtil.MakePayload(owner, owner.DefaultDamageEffect, damage);
            TimedAnimatedHitEffect2D.SharedHitRegistry sharedHitRegistry = new();
            bool presentationPlayed = false;
            void PlayImpactPresentationOnce()
            {
                if (presentationPlayed)
                    return;

                presentationPlayed = true;
                Vector2 presentationCenter = centers.Count > 0 ? centers[0] : owner.transform.position;
                PlayImpactPresentation(owner, presentationCenter, impactSound, impactCameraShake, "DemonKing.DelayedCircleClusterImpact");
            }

            int timedCount = 0;
            bool anyVisualSpawned = false;
            for (int i = 0; i < centers.Count; i++)
            {
                if (TrySpawnTimedExplosionVfx(
                    owner,
                    centers[i],
                    diameter,
                    AttackColor,
                    "DemonKing_ExplosionCircleAttack",
                    explosionVfxKind,
                    explosionVfxCue,
                    payload,
                    sharedHitRegistry,
                    PlayImpactPresentationOnce,
                    out bool visualSpawned))
                {
                    timedCount++;
                }

                anyVisualSpawned |= visualSpawned;
            }

            if (timedCount > 0)
            {
                Destroy(gameObject);
                yield break;
            }

            PlayImpactPresentationOnce();
            HashSet<GameObject> damagedTargets = new();
            for (int i = 0; i < centers.Count; i++)
            {
                Vector2 center = centers[i];
                DemonKingCombatUtil.ApplyTopDownEllipseDamage(
                    owner,
                    center,
                    diameter,
                    owner.DefaultDamageEffect,
                    damage,
                    damagedTargets);

                if (!anyVisualSpawned)
                {
                    SpawnExplosionFallbackVfx(
                        center,
                        diameter,
                        AttackColor,
                        "DemonKing_ExplosionCircleAttack",
                        explosionVfxKind,
                        explosionVfxCue);
                }
            }
        }

        Destroy(gameObject);
    }

    private IEnumerator RunRectangle(
        DemonKingController owner,
        Vector2 center,
        Vector2 size,
        float rotationDeg,
        float warningSeconds,
        float damage,
        bool ignoreOwnerGroggy)
    {
        owner.GetTelegraphService()?.SpawnDetachedView(
            AttackTelegraphSpecUtility.WithThinWarningOutline(
                AttackTelegraphSpec.CreateRectangle(center, size, rotationDeg, warningSeconds, owner.DefaultWarningStyle)));

        if (warningSeconds > 0f)
            yield return new WaitForSeconds(warningSeconds);

        if (owner != null && !owner.IsDead && (ignoreOwnerGroggy || !owner.HasGroggyTag()))
        {
            DemonKingCombatUtil.ApplyRectangleDamage(
                owner,
                center,
                size,
                rotationDeg,
                owner.DefaultDamageEffect,
                damage);

            DemonKingPrimitiveVisual.SpawnSquare(
                center,
                size,
                rotationDeg,
                AttackFlashSeconds,
                AttackColor,
                "DemonKing_RectSquareAttack");
        }

        Destroy(gameObject);
    }

    private static bool TrySpawnTimedExplosionVfx(
        DemonKingController owner,
        Vector2 center,
        float diameter,
        Color fallbackColor,
        string fallbackName,
        DemonKingDelayedExplosionVfxKind explosionVfxKind,
        DemonKingVfxCueRef explosionVfxCue,
        CombatHitPayload payload,
        TimedAnimatedHitEffect2D.SharedHitRegistry sharedHitRegistry,
        System.Action onHitWindowOpened,
        out bool spawnedVisual)
    {
        DemonKingAnimationClipVisual visual = explosionVfxCue.IsConfigured
            ? DemonKingPatternVfx.SpawnCueOneShot(
                explosionVfxCue,
                center,
                diameter,
                Vector2.down,
                "DemonKing_DelayedExplosionCue")
            : explosionVfxKind == DemonKingDelayedExplosionVfxKind.DarkLordExplosion2
                ? DemonKingPatternVfx.SpawnDarkLordExplosion2(center, diameter)
                : DemonKingPatternVfx.SpawnExplosion(center, diameter);
        spawnedVisual = visual != null;

        if (visual == null)
        {
            DemonKingPrimitiveVisual.SpawnCircle(center, diameter, 0.12f, fallbackColor, fallbackName);
            return false;
        }

        return DemonKingPatternVfx.TryPlayCircleTimedHit(
            visual,
            owner,
            diameter,
            payload,
            sharedHitRegistry,
            onHitWindowOpened);
    }

    private static void SpawnExplosionFallbackVfx(
        Vector2 center,
        float diameter,
        Color fallbackColor,
        string fallbackName,
        DemonKingDelayedExplosionVfxKind explosionVfxKind,
        DemonKingVfxCueRef explosionVfxCue)
    {
        if (explosionVfxCue.IsConfigured)
        {
            if (DemonKingPatternVfx.SpawnCueOneShot(
                    explosionVfxCue,
                    center,
                    diameter,
                    Vector2.down,
                    fallbackName) == null)
            {
                DemonKingPrimitiveVisual.SpawnCircle(center, diameter, 0.12f, fallbackColor, fallbackName);
            }

            return;
        }

        if (explosionVfxKind == DemonKingDelayedExplosionVfxKind.DarkLordExplosion2)
        {
            DemonKingPatternVfx.SpawnDarkLordExplosion2OrFallbackCircle(center, diameter, fallbackColor, fallbackName);
            return;
        }

        DemonKingPatternVfx.SpawnExplosionOrFallbackCircle(center, diameter, fallbackColor, fallbackName);
    }

    private static void PlayImpactPresentation(
        DemonKingController owner,
        Vector2 center,
        SoundRef sound,
        CameraShakeHook shake,
        string debugReason)
    {
        if (owner == null)
            return;

        if (sound.IsSet)
        {
            SoundPlaybackUtility.Play(
                sound,
                instigator: owner.gameObject,
                causer: owner.gameObject,
                target: owner.CurrentTarget != null ? owner.CurrentTarget.gameObject : null,
                position: center,
                sourceObject: owner);
        }

        shake.TryPlay(owner.gameObject, Vector3.down, debugReason: debugReason);
    }
}
