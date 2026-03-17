using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public class Wave : MonoBehaviour
{
    // Components =============================
    private LineRenderer lineRenderer;

    [Header("Visual Settings")]
    [Tooltip("원을 몇 개의 점으로 그릴지 (높을수록 부드러움)")]
    [SerializeField] private int segments = 60;

    [Header("Motion Settings")]
    [SerializeField] private float expansionSpeed = 5.0f; // 초당 확산 속도 (반경 증가량)
    [SerializeField] private float thickness = 1.0f;      // 도넛 두께 (딜 판정 범위)
    [SerializeField] private float maxDuration = 2.5f;    // 최대 지속 시간

    [Header("Collision Settings")]
    [SerializeField] private LayerMask targetLayer;

    // Runtime =============================
    private float currentRadius = 0f;
    private float timer = 0f;
    private bool hasHitTarget = false;
    private bool isInitialized = false;

    // Damage Context =====================
    private AbilitySystem sourceSystem;
    private AbilitySpec sourceSpec;
    private GameplayEffect damageEffect;
    private GE_Knockback_Spec knockbackEffect;
    private CombatDamageSnapshot damageSnapshot;
    private GameplayTag hitConfirmedTag;
    private GameObject causer;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    /// <summary>
    /// AL / 스포너에서 호출.
    /// 이제 Wave는 GameplayEffectSpec을 직접 들고 있지 않고,
    /// 공식 전투 적용 경로에 필요한 정보만 보관한다.
    /// </summary>
    public void Initialize(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GE_Knockback_Spec knockbackEffect,
        CombatDamageSnapshot snapshot,
        GameplayTag hitConfirmedTag,
        GameObject causer = null)
    {
        sourceSystem = system;
        sourceSpec = spec;
        this.damageEffect = damageEffect;
        this.knockbackEffect = knockbackEffect;
        damageSnapshot = snapshot;
        this.hitConfirmedTag = hitConfirmedTag;
        this.causer = causer != null ? causer : (system != null ? system.gameObject : null);

        isInitialized = true;

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = segments + 1;
            lineRenderer.useWorldSpace = false;
            lineRenderer.startWidth = thickness;
            lineRenderer.endWidth = thickness;
        }
    }

    private void Update()
    {
        if (!isInitialized)
            return;

        timer += Time.deltaTime;
        currentRadius += expansionSpeed * Time.deltaTime;

        UpdateVisuals();
        CheckLifeTime();

        if (hasHitTarget)
            return;

        DetectCollision();
    }

    private void UpdateVisuals()
    {
        if (lineRenderer == null)
            return;

        lineRenderer.startWidth = thickness;
        lineRenderer.endWidth = thickness;

        float angle = 0f;

        for (int i = 0; i <= segments; i++)
        {
            float x = Mathf.Sin(Mathf.Deg2Rad * angle) * currentRadius;
            float y = Mathf.Cos(Mathf.Deg2Rad * angle) * currentRadius;

            lineRenderer.SetPosition(i, new Vector3(x, y, 0f));
            angle += 360.0f / segments;
        }
    }

    private void DetectCollision()
    {
        float outerRadius = currentRadius + (thickness * 0.5f);
        float innerRadius = Mathf.Max(0f, currentRadius - (thickness * 0.5f));

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, outerRadius, targetLayer);
        if (hits == null || hits.Length == 0)
            return;

        var visited = new HashSet<GameObject>();

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null)
                continue;

            GameObject target = ResolveTargetRoot(hit);
            if (target == null)
                continue;

            if (target == causer)
                continue;

            if (!visited.Add(target))
                continue;

            // 실제로 데미지를 받을 수 있는 대상만
            if (target.GetComponent<AttributeSet>() == null)
                continue;

            float distance = Vector2.Distance(transform.position, target.transform.position);

            // 도넛의 구멍 안쪽이면 제외
            if (distance < innerRadius)
                continue;

            ApplyDamage(target);
            hasHitTarget = true;
            break;
        }
    }

    private void ApplyDamage(GameObject target)
    {
        if (sourceSystem == null || damageEffect == null || target == null)
            return;

        CombatDamageAction.ApplyDamageAndEmitHit(
            system: sourceSystem,
            spec: sourceSpec,
            damageEffect: damageEffect,
            knockbackEffect: knockbackEffect,
            target: target,
            finalHpDamage: damageSnapshot.FinalHpDamage,
            finalStaggerBuildUp: damageSnapshot.FinalStaggerBuildUp,
            elementBuildUps: damageSnapshot.ElementBuildUps,
            finalKnockbackImpulse: damageSnapshot.FinalKnockbackImpulse,
            hitConfirmedTag: hitConfirmedTag,
            causer: causer
        );
    }

    private static GameObject ResolveTargetRoot(Collider2D hit)
    {
        if (hit == null)
            return null;

        if (hit.attachedRigidbody != null)
            return hit.attachedRigidbody.gameObject;

        var attr = hit.GetComponentInParent<AttributeSet>();
        if (attr != null)
            return attr.gameObject;

        return hit.gameObject;
    }

    private void CheckLifeTime()
    {
        if (timer >= maxDuration)
            Destroy(gameObject);
    }
}