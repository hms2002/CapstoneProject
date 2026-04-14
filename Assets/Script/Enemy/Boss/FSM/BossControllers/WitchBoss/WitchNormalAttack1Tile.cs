using System.Collections;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(AttackTelegraphView))]
public class WitchNormalAttack1Tile : MonoBehaviour
{
    private const float HitTime = 0.12f;
    private const float DefaultPresentationLifetimeSeconds = 1f;

    private AttackTelegraphView telegraphView;
    private AttackTelegraphStyle warningStyle;
    private AttackTelegraphStyle hitStyle;
    private GameObject targetObject;
    private CombatHitPayload hitPayload;
    private Vector2 tileSize;
    private float angleDeg;

    [Header("Hit Presentation")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private Vector3 hitEffectLocalOffset = new Vector3(0f, 0f, -0.05f);
    [SerializeField] [Min(0f)] private float hitEffectLifetimeSeconds = 0.35f;
    [SerializeField] private GameObject hitParticlePrefab;
    [SerializeField] private Vector3 hitParticleLocalOffset = new Vector3(0f, 0f, -0.02f);
    [SerializeField] [Min(0f)] private float hitParticleLifetimeOverrideSeconds = 0f;
    [SerializeField] private bool useUnscaledHitParticleTime;
    [SerializeField] private SoundRef hitSound;
    [SerializeField] private CameraShakeHook hitCameraShake = CameraShakeHook.Create(0.12f, 1f, 0.18f, 0.03f);

    private void Awake()
    {
        telegraphView = GetComponent<AttackTelegraphView>();
        warningStyle = MakeWarningStyle();
        hitStyle = MakeHitStyle();
    }

    private void OnDestroy()
    {
        if (warningStyle != null) Destroy(warningStyle);
        if (hitStyle != null) Destroy(hitStyle);
    }

    /// <summary>장판 경고와 타격 순서를 시작합니다.</summary>
    public void Play(GameObject target, CombatHitPayload payload, Vector2 size, float angle, float showDelay, float hitDelay)
    {
        targetObject = target;
        hitPayload = payload;
        tileSize = size;
        angleDeg = angle;

        StopAllCoroutines();
        StartCoroutine(Run(showDelay, hitDelay));
    }

    private IEnumerator Run(float showDelay, float hitDelay)
    {
        float safeShowDelay = Mathf.Max(0f, showDelay);
        float safeHitDelay = Mathf.Max(safeShowDelay, hitDelay);
        float warningTime = safeHitDelay - safeShowDelay;

        if (safeShowDelay > 0f)
            yield return new WaitForSeconds(safeShowDelay);

        if (warningTime > 0f)
        {
            ShowWarning(warningTime);
            yield return new WaitForSeconds(warningTime);
        }

        ShowHit();
        TryHit();
        yield return new WaitForSeconds(HitTime);
        Destroy(gameObject);
    }

    /// <summary>빨간 경고 장판을 표시합니다.</summary>
    private void ShowWarning(float duration)
    {
        if (telegraphView == null) return;

        telegraphView.Show(MakeSpec(duration, warningStyle));
    }

    /// <summary>타격 장판을 표시합니다.</summary>
    private void ShowHit()
    {
        if (telegraphView == null) return;

        telegraphView.Show(MakeSpec(HitTime, hitStyle));
        PlayHitPresentation();
    }

    /// <summary>장판 안의 플레이어를 공격합니다.</summary>
    private void TryHit()
    {
        if (targetObject == null || hitPayload == null || !hitPayload.IsValid()) return;

        Collider2D[] hits = Physics2D.OverlapBoxAll(transform.position, tileSize, angleDeg);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i];
            if (ShouldIgnoreColliderForDamage(hitCollider))
                continue;

            GameObject hitObject = GetHitObject(hitCollider);
            if (hitObject != targetObject) continue;

            CombatHitPayloadApplier.Apply(hitObject, hitPayload, transform.position);
            return;
        }
    }

    /// <summary>사각형 장판 정보를 만듭니다.</summary>
    private AttackTelegraphSpec MakeSpec(float duration, AttackTelegraphStyle style)
    {
        return AttackTelegraphSpec.CreateRectangle(
            transform.position,
            tileSize,
            angleDeg,
            duration,
            style);
    }

    /// <summary>충돌한 대상의 본체를 찾습니다.</summary>
    private GameObject GetHitObject(Collider2D hitCollider)
    {
        if (hitCollider == null) return null;

        if (hitCollider.attachedRigidbody != null) return hitCollider.attachedRigidbody.gameObject;

        return hitCollider.transform.root.gameObject;
    }

    /// <summary>공격체/무기 히트박스 콜라이더는 장판 피해 후보에서 제외합니다.</summary>
    private static bool ShouldIgnoreColliderForDamage(Collider2D hitCollider)
    {
        if (hitCollider == null)
            return true;

        return hitCollider.GetComponentInParent<AttackBase>() != null;
    }

    /// <summary>경고 색상 스타일을 만듭니다.</summary>
    private AttackTelegraphStyle MakeWarningStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        style.fillColorStart = new Color(1f, 0f, 0f, 0.2f);
        style.fillColorEnd = new Color(1f, 0f, 0f, 0.2f);
        style.borderColorStart = new Color(1f, 0f, 0f, 1f);
        style.borderColorEnd = new Color(1f, 0f, 0f, 1f);
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 1f;
        style.blinkFrequency = 0f;
        style.blinkAlphaMin = 1f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }

    /// <summary>타격 색상 스타일을 만듭니다.</summary>
    private AttackTelegraphStyle MakeHitStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        style.fillColorStart = new Color(0.95f, 0f, 1f, 0.8f);
        style.fillColorEnd = new Color(0.95f, 0f, 1f, 0.8f);
        style.borderColorStart = new Color(0.95f, 0f, 1f, 1f);
        style.borderColorEnd = new Color(0.95f, 0f, 1f, 1f);
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 1f;
        style.blinkFrequency = 0f;
        style.blinkAlphaMin = 1f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }

    private void PlayHitPresentation()
    {
        Vector3 hitDirection = ResolveHitDirection();
        SpawnPresentationPrefab(hitEffectPrefab, hitEffectLocalOffset, hitEffectLifetimeSeconds, useUnscaledTime: false);
        SpawnPresentationPrefab(hitParticlePrefab, hitParticleLocalOffset, hitParticleLifetimeOverrideSeconds, useUnscaledHitParticleTime);
        SoundPlaybackUtility.Play(
            hitSound,
            instigator: hitPayload != null ? hitPayload.sourceSystem != null ? hitPayload.sourceSystem.gameObject : null : null,
            causer: hitPayload != null ? hitPayload.causer : gameObject,
            target: targetObject,
            position: transform.position,
            sourceObject: this);
        hitCameraShake.TryPlay(gameObject, hitDirection, debugReason: "WitchNormalAttack1Tile.Hit");
    }

    private void SpawnPresentationPrefab(
        GameObject prefab,
        Vector3 localOffset,
        float lifetimeOverrideSeconds,
        bool useUnscaledTime)
    {
        if (prefab == null)
            return;

        Quaternion spawnRotation = Quaternion.Euler(0f, 0f, angleDeg) * prefab.transform.rotation;
        GameObject instance = Instantiate(prefab, ResolvePresentationPosition(localOffset), spawnRotation);
        if (instance == null)
            return;

        ConfigureSpawnedPresentation(instance, useUnscaledTime);

        float lifetime = ResolvePresentationLifetime(instance, lifetimeOverrideSeconds);
        if (lifetime > 0f)
            Destroy(instance, lifetime);
    }

    private Vector3 ResolvePresentationPosition(Vector3 localOffset)
    {
        return transform.TransformPoint(localOffset);
    }

    private Vector3 ResolveHitDirection()
    {
        Vector3 direction = Quaternion.Euler(0f, 0f, angleDeg) * Vector3.right;
        direction.z = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return Vector3.up;

        return direction.normalized;
    }

    private static void ConfigureSpawnedPresentation(GameObject instance, bool useUnscaledTime)
    {
        if (instance == null)
            return;

        instance.SetActive(true);

        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            if (useUnscaledTime)
            {
                var main = particleSystem.main;
                main.useUnscaledTime = true;
            }

            particleSystem.Play(withChildren: true);
        }

        Animation[] animations = instance.GetComponentsInChildren<Animation>(includeInactive: true);
        for (int i = 0; i < animations.Length; i++)
        {
            Animation animationComponent = animations[i];
            if (animationComponent == null)
                continue;

            animationComponent.Play();
        }
    }

    private static float ResolvePresentationLifetime(GameObject instance, float lifetimeOverrideSeconds)
    {
        if (lifetimeOverrideSeconds > 0f)
            return lifetimeOverrideSeconds;

        float particleLifetime = ResolveParticleLifetime(instance);
        if (particleLifetime > 0f)
            return particleLifetime;

        float animationLifetime = ResolveAnimatorLifetime(instance);
        if (animationLifetime > 0f)
            return animationLifetime;

        return DefaultPresentationLifetimeSeconds;
    }

    private static float ResolveParticleLifetime(GameObject instance)
    {
        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        if (particleSystems == null || particleSystems.Length == 0)
            return 0f;

        float maxLifetime = 0f;
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            var main = particleSystem.main;
            if (main.loop)
                return DefaultPresentationLifetimeSeconds;

            float startDelay = ResolveCurveMax(main.startDelay);
            float startLifetime = ResolveCurveMax(main.startLifetime);
            maxLifetime = Mathf.Max(maxLifetime, startDelay + main.duration + startLifetime);
        }

        return maxLifetime > 0f ? maxLifetime + 0.25f : 0f;
    }

    private static float ResolveAnimatorLifetime(GameObject instance)
    {
        float maxLifetime = 0f;

        Animator[] animators = instance.GetComponentsInChildren<Animator>(includeInactive: true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || animator.runtimeAnimatorController == null)
                continue;

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
            {
                AnimationClip clip = clips[clipIndex];
                if (clip == null)
                    continue;

                maxLifetime = Mathf.Max(maxLifetime, clip.length);
            }
        }

        Animation[] animations = instance.GetComponentsInChildren<Animation>(includeInactive: true);
        for (int i = 0; i < animations.Length; i++)
        {
            Animation animationComponent = animations[i];
            if (animationComponent == null)
                continue;

            foreach (AnimationState state in animationComponent)
            {
                if (state?.clip == null)
                    continue;

                maxLifetime = Mathf.Max(maxLifetime, state.clip.length);
            }
        }

        return maxLifetime > 0f ? maxLifetime + 0.05f : 0f;
    }

    private static float ResolveCurveMax(ParticleSystem.MinMaxCurve curve)
    {
        return curve.mode switch
        {
            ParticleSystemCurveMode.Constant => curve.constant,
            ParticleSystemCurveMode.TwoConstants => curve.constantMax,
            ParticleSystemCurveMode.Curve => curve.curveMultiplier,
            ParticleSystemCurveMode.TwoCurves => curve.curveMultiplier,
            _ => Mathf.Max(curve.constant, curve.constantMax)
        };
    }
}
