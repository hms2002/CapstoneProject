using System.Collections;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;
using UnityEngine.Serialization;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(AttackTelegraphView))]
public class WitchNormalAttack1Tile : MonoBehaviour
{
    private const float HitTime = 0.12f;

    private AttackTelegraphView telegraphView;
    private AttackTelegraphStyle warningStyle;
    private AttackTelegraphStyle hitStyle;
    private GameObject targetObject;
    private CombatHitPayload hitPayload;
    private Vector2 tileSize;
    private float angleDeg;

    [Header("Hit Presentation")]
    [SerializeField] private WorldPresentationHook hitPresentation;
    [HideInInspector, FormerlySerializedAs("hitEffectPrefab")]
    [SerializeField] private GameObject legacyHitEffectPrefab;
    [HideInInspector, FormerlySerializedAs("hitEffectLocalOffset")]
    [SerializeField] private Vector3 legacyHitEffectLocalOffset = new Vector3(0f, 0f, -0.05f);
    [HideInInspector, FormerlySerializedAs("hitEffectLifetimeSeconds")]
    [SerializeField] private float legacyHitEffectLifetimeSeconds = 0.35f;
    [HideInInspector, FormerlySerializedAs("hitParticlePrefab")]
    [SerializeField] private GameObject legacyHitParticlePrefab;
    [HideInInspector, FormerlySerializedAs("hitParticleLocalOffset")]
    [SerializeField] private Vector3 legacyHitParticleLocalOffset = new Vector3(0f, 0f, -0.02f);
    [HideInInspector, FormerlySerializedAs("hitParticleLifetimeOverrideSeconds")]
    [SerializeField] private float legacyHitParticleLifetimeOverrideSeconds;
    [HideInInspector, FormerlySerializedAs("useUnscaledHitParticleTime")]
    [SerializeField] private bool legacyUseUnscaledHitParticleTime;
    [HideInInspector, FormerlySerializedAs("hitSound")]
    [SerializeField] private SoundRef legacyHitSound;
    [HideInInspector, FormerlySerializedAs("hitCameraShake")]
    [SerializeField] private CameraShakeHook legacyHitCameraShake = CameraShakeHook.Create(0.12f, 1f, 0.18f, 0.03f);

    private void Awake()
    {
        MigrateLegacyHitPresentation();
        telegraphView = GetComponent<AttackTelegraphView>();
        warningStyle = MakeWarningStyle();
        hitStyle = MakeHitStyle();
    }

    private void OnValidate()
    {
        MigrateLegacyHitPresentation();
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
        if (hitCollider == null)
            return null;

        return CombatTargetResolver2D.ResolveDamageTarget(hitCollider);
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
        Quaternion presentationRotation = Quaternion.Euler(0f, 0f, angleDeg);
        WorldPresentationRuntime.Play(
            hitPresentation,
            WorldPresentationContext.AtWorld(
                instigator: hitPayload != null && hitPayload.sourceSystem != null ? hitPayload.sourceSystem.gameObject : gameObject,
                position: transform.position,
                fallbackDirection: hitDirection,
                target: targetObject,
                sourceObject: this,
                rotation: presentationRotation,
                causer: hitPayload != null ? hitPayload.causer : gameObject));
    }

    private Vector3 ResolveHitDirection()
    {
        Vector3 direction = Quaternion.Euler(0f, 0f, angleDeg) * Vector3.right;
        direction.z = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return Vector3.up;

        return direction.normalized;
    }

    private void MigrateLegacyHitPresentation()
    {
        if (legacyHitEffectPrefab != null && !hitPresentation.effect.HasContent)
        {
            hitPresentation.effect.prefab = legacyHitEffectPrefab;
            hitPresentation.effect.localOffset = legacyHitEffectLocalOffset;
            hitPresentation.effect.lifetimeOverrideSeconds = legacyHitEffectLifetimeSeconds;
        }

        if (legacyHitParticlePrefab != null && !hitPresentation.particle.HasContent)
        {
            hitPresentation.particle.prefab = legacyHitParticlePrefab;
            hitPresentation.particle.localOffset = legacyHitParticleLocalOffset;
            hitPresentation.particle.lifetimeOverrideSeconds = legacyHitParticleLifetimeOverrideSeconds;
            hitPresentation.particle.useUnscaledTime = legacyUseUnscaledHitParticleTime;
        }

        if (!hitPresentation.HasSound && legacyHitSound.IsSet)
            hitPresentation.sound = legacyHitSound;

        if (!hitPresentation.HasShake && legacyHitCameraShake.amplitude > 0f)
            hitPresentation.cameraShake = legacyHitCameraShake;
    }

}
