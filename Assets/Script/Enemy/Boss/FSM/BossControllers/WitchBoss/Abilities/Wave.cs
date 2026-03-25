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
    [SerializeField] private float expansionSpeed   = 5.0f;
    [SerializeField] private float thickness        = 1.0f;
    [SerializeField] private float maxDuration      = 2.5f;

    [Header("Collision Settings")]
    [SerializeField] private LayerMask targetLayer;

    // Variables ============================
    private float   currentRadius   = 0f;
    private float   timer           = 0f;
    private bool    hasHitPlayer    = false;

    private GameplayEffectSpec damageSpec;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer != null)
        {
            lineRenderer.useWorldSpace      = false;
            lineRenderer.loop               = true;
            lineRenderer.alignment          = LineAlignment.TransformZ;
            lineRenderer.numCapVertices     = 0;
            lineRenderer.numCornerVertices  = 0;
        }
    }

    public void Initialize(GameplayEffectSpec spec)
    {
        damageSpec = spec;

        // 중심점이 꼬이는 현상 방지
        currentRadius = thickness * 0.5f;

        // 부모의 스케일(Scale)이 찌그러져 있으면 파동도 찌그러짐
        // 로컬 스케일을 무조건 1,1,1 정비율로 강제 고정
        transform.localScale = Vector3.one;
    }

    private void Update()
    {
        timer           += Time.deltaTime;
        currentRadius   += expansionSpeed * Time.deltaTime;

        UpdateVisuals();
        CheckLifeTime();

        if (hasHitPlayer) return;

        DetectCollision();
    }

    private void UpdateVisuals()
    {
        if (lineRenderer == null) return;

        // 런타임에 segments를 바꿔도 즉시 동기화되도록 강제 설정
        lineRenderer.positionCount  = segments;
        lineRenderer.startWidth     = thickness;
        lineRenderer.endWidth       = thickness;

        // 가장 오류가 적고 안정적인 라디안 단위 원 방정식
        float angleStep = (2.0f * Mathf.PI) / segments;

        for (int i = 0; i < segments; i++)
        {
            float x = Mathf.Cos(i * angleStep) * currentRadius;
            float y = Mathf.Sin(i * angleStep) * currentRadius;

            lineRenderer.SetPosition(i, new Vector3(x, y, 0f));
        }
    }

    private void DetectCollision()
    {
        float       checkRadius = currentRadius + (thickness * 0.5f);
        Collider2D  hit         = Physics2D.OverlapCircle(transform.position, checkRadius, targetLayer);

        if (hit != null)
        {
            float distance  = Vector2.Distance(transform.position, hit.transform.position);
            float innerEdge = currentRadius - (thickness * 0.5f);

            if (distance >= innerEdge)
            {
                ApplyDamage(hit.gameObject);
                hasHitPlayer = true;
            }
        }
    }

    private void ApplyDamage(GameObject target)
    {
        AbilitySystem targetASC = target.GetComponent<AbilitySystem>();

        if (targetASC != null && damageSpec != null)
        {
            targetASC.EffectRunner.ApplyEffectSpec(damageSpec, target);
        }
    }

    private void CheckLifeTime()
    {
        if (timer >= maxDuration) Destroy(gameObject);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, currentRadius);
    }
}