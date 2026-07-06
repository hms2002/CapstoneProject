using System.Collections;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;
using UnityEngine.Serialization;
using UnityGAS;

[DisallowMultipleComponent]
// 이 클래스의 책임:
// - 마녀 보스 평타1의 개별 타일 경고, 타격 표시, 피해 판정을 같은 기하 데이터로 실행한다.
// - 경고/타격/피해 범위가 어긋날 때 진단할 수 있도록 타일 단위 로그를 제공한다.
public class WitchNormalAttack1Tile : MonoBehaviour
{
    private const float HitTime = 0.12f;

    private IAttackTelegraphHandle telegraphView;
    private AttackTelegraphStyle warningStyle;
    private AttackTelegraphStyle hitStyle;
    private GameObject targetObject;
    private CombatHitPayload hitPayload;
    private Vector2 tileSize;
    private float angleDeg;
    private int debugTileIndex = -1;
    private float debugShowDelay;
    private float debugHitDelay;

    [Header("Hit Presentation")]
    [SerializeField] private WorldPresentationHook hitPresentation;
    [Header("Debug")]
    [SerializeField] private bool logGeometryDebug;
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
        telegraphView = ResolveTelegraphView();
    }

    private void OnValidate()
    {
        MigrateLegacyHitPresentation();
    }

    /// <summary>장판 경고와 타격 순서를 시작합니다.</summary>
    public void Play(
        GameObject target,
        CombatHitPayload payload,
        Vector2 size,
        float angle,
        float showDelay,
        float hitDelay,
        AttackTelegraphStyle warningTelegraphStyle,
        AttackTelegraphStyle hitTelegraphStyle,
        int tileIndex = -1)
    {
        targetObject = target;
        hitPayload = payload;
        tileSize = size;
        angleDeg = angle;
        warningStyle = warningTelegraphStyle;
        hitStyle = hitTelegraphStyle;
        debugTileIndex = tileIndex;
        debugShowDelay = showDelay;
        debugHitDelay = hitDelay;

        StopAllCoroutines();
        StartCoroutine(Run(showDelay, hitDelay));
    }

    private IEnumerator Run(float showDelay, float hitDelay)
    {
        float safeShowDelay = Mathf.Max(0f, showDelay);
        float safeHitDelay = Mathf.Max(safeShowDelay, hitDelay);
        float warningTime = safeHitDelay - safeShowDelay;

        LogGeometry("run start", MakeSpec(warningTime, warningStyle), warningTime);

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

        AttackTelegraphSpec warningSpec = AttackTelegraphSpecUtility.WithThinWarningOutlineOnly(MakeSpec(duration, warningStyle));
        LogGeometry("show warning", warningSpec, duration);
        telegraphView.Show(warningSpec);
    }

    /// <summary>타격 장판을 표시합니다.</summary>
    private void ShowHit()
    {
        if (telegraphView == null) return;

        AttackTelegraphSpec hitSpec = MakeSpec(HitTime, hitStyle);
        LogGeometry("show hit", hitSpec, HitTime);
        telegraphView.Show(hitSpec);
        PlayHitPresentation();
    }

    /// <summary>장판 안의 플레이어를 공격합니다.</summary>
    private void TryHit()
    {
        if (targetObject == null || hitPayload == null || !hitPayload.IsValid()) return;

        Collider2D[] hits = Physics2D.OverlapBoxAll(transform.position, tileSize, angleDeg);
        int consideredCount = 0;
        string hitNames = string.Empty;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i];
            if (ShouldIgnoreColliderForDamage(hitCollider))
                continue;

            GameObject hitObject = GetHitObject(hitCollider);
            consideredCount++;
            if (logGeometryDebug)
                hitNames += $"{hitCollider.name}->{(hitObject != null ? hitObject.name : "null")}; ";
            if (hitObject != targetObject) continue;

            LogDamageProbe(hits.Length, consideredCount, hitNames, true);
            CombatHitPayloadApplier.Apply(hitObject, hitPayload, transform.position);
            return;
        }

        LogDamageProbe(hits.Length, consideredCount, hitNames, false);
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

    private void PlayHitPresentation()
    {
        Vector3 hitDirection = ResolveHitDirection();
        Quaternion presentationRotation = Quaternion.Euler(0f, 0f, angleDeg);
        LogHitPresentation(presentationRotation);
        WorldPresentationPlayback.PlayDeferredAsync(
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

    private void LogGeometry(string phase, AttackTelegraphSpec spec, float phaseDuration)
    {
        if (!logGeometryDebug)
            return;

        Vector3[] corners = BuildRectangleCorners(transform.position, tileSize, angleDeg);
        Vector3 targetPosition = targetObject != null ? targetObject.transform.position : Vector3.zero;
        string targetName = targetObject != null ? targetObject.name : "null";
        string styleName = spec.style != null ? spec.style.name : "null";
        Debug.Log(
            $"[WitchNormalAttack1Tile] {phase}. " +
            $"tile={debugTileIndex}, object={name}, time={Time.time:0.000}, " +
            $"center={transform.position}, specCenter={spec.center}, size={tileSize}, angle={angleDeg:0.0}, " +
            $"duration={phaseDuration:0.000}, showDelay={debugShowDelay:0.000}, hitDelay={debugHitDelay:0.000}, " +
            $"meshOutline={spec.useMeshOutline}, wallClip={spec.useWallClipping}, wallMask={spec.wallClipLayers.value}, style={styleName}, " +
            $"target={targetName}, targetPos={targetPosition}, " +
            $"corners=[{corners[0]}, {corners[1]}, {corners[2]}, {corners[3]}]",
            this);
    }

    private void LogDamageProbe(int rawHitCount, int consideredCount, string hitNames, bool applied)
    {
        if (!logGeometryDebug)
            return;

        Debug.Log(
            $"[WitchNormalAttack1Tile] damage probe. " +
            $"tile={debugTileIndex}, object={name}, time={Time.time:0.000}, " +
            $"center={transform.position}, size={tileSize}, angle={angleDeg:0.0}, " +
            $"rawHits={rawHitCount}, considered={consideredCount}, applied={applied}, hits={hitNames}",
            this);
    }

    private void LogHitPresentation(Quaternion rotation)
    {
        if (!logGeometryDebug)
            return;

        string effectName = hitPresentation.effect.prefab != null ? hitPresentation.effect.prefab.name : "null";
        string particleName = hitPresentation.particle.prefab != null ? hitPresentation.particle.prefab.name : "null";
        Debug.Log(
            $"[WitchNormalAttack1Tile] hit presentation. " +
            $"tile={debugTileIndex}, object={name}, time={Time.time:0.000}, " +
            $"position={transform.position}, rotationZ={rotation.eulerAngles.z:0.0}, " +
            $"effect={effectName}, effectScale={hitPresentation.effect.EffectiveScaleMultiplier}, " +
            $"particle={particleName}, particleScale={hitPresentation.particle.EffectiveScaleMultiplier}",
            this);
    }

    private IAttackTelegraphHandle ResolveTelegraphView()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IAttackTelegraphHandle handle)
                return handle;
        }

        Debug.LogError($"[{nameof(WitchNormalAttack1Tile)}] 공격 타일에 텔레그래프 뷰 계약 구현이 없습니다.", this);
        return null;
    }

    private static Vector3[] BuildRectangleCorners(Vector3 center, Vector2 size, float angle)
    {
        Vector2 half = size * 0.5f;
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
        return new[]
        {
            center + rotation * new Vector3(-half.x, -half.y, 0f),
            center + rotation * new Vector3(-half.x, half.y, 0f),
            center + rotation * new Vector3(half.x, half.y, 0f),
            center + rotation * new Vector3(half.x, -half.y, 0f)
        };
    }

}
