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
    [SerializeField] private float expansionSpeed   = 5.0f; // 초당 확산 속도 (반경 증가량)
    [SerializeField] private float thickness        = 1.0f; // 도넛 두께 (딜 판정 범위)
    [SerializeField] private float maxDuration      = 2.5f; // 최대 지속 시간

    [Header("Collision Settings")]
    [SerializeField] private LayerMask targetLayer;

    // Variables ============================
    private float   currentRadius   = 0f;
    private float   timer           = 0f;
    private bool    hasHitPlayer    = false;

    private GameplayEffectSpec  damageSpec; // 보스가 적어준 데미지 명세서


    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    // 초기화 함수 (AL에서 호출)
    public void Initialize(GameplayEffectSpec spec)
    {
        damageSpec = spec;

        // LineRenderer 초기 설정
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
        timer           += Time.deltaTime;
        currentRadius   += expansionSpeed * Time.deltaTime;

        UpdateVisuals();  // DrawCircle 로직
        CheckLifeTime();  // 수명 검사

        if (hasHitPlayer) return;

        DetectCollision(); // 충돌 검사
    }

    private void UpdateVisuals()
    {
        if (lineRenderer == null) return;

        // LineRenderer 두께 업데이트 (혹시 런타임에 바꾸고 싶을까봐)
        lineRenderer.startWidth = thickness;
        lineRenderer.endWidth   = thickness;

        float angle = 0f;

        for (int i = 0; i <= segments; i++)
        {
            float x = Mathf.Sin(Mathf.Deg2Rad * angle) * currentRadius;
            float y = Mathf.Cos(Mathf.Deg2Rad * angle) * currentRadius;

            lineRenderer.SetPosition(i, new Vector3(x, y, 0f));
            angle += (360.0f / segments);
        }
    }

    private void DetectCollision()
    {
        // 도넛의 바깥쪽 경계까지를 원으로 잡고 검사
        float checkRadius = currentRadius + (thickness * 0.5f);

        Collider2D hit = Physics2D.OverlapCircle(transform.position, checkRadius, targetLayer);

        if (hit != null)
        {
            float distance  = Vector2.Distance(transform.position, hit.transform.position);
            float innerEdge = currentRadius - (thickness * 0.5f);

            // 도넛 안쪽 구멍보다 멀리 있어야 함 (즉, 도넛 위에 있어야 함)
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

    private void CheckLifeTime() { if (timer >= maxDuration) Destroy(gameObject); }
}