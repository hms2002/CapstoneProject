using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer), typeof(CombatHurtbox2D))]
public sealed class DrainPipe : MonoBehaviour, IDamageReceiver
{
    [Header("Refs")]
    [Tooltip("임시 배수관 원형 스프라이트를 표시할 렌더러입니다.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Hit")]
    [Tooltip("배수관이 파괴되기까지 필요한 피격 횟수입니다.")]
    [SerializeField] private int hitCountToBreak = 3;

    [Header("Suction")]
    [Tooltip("파괴된 배수관이 Pawn 슬라임을 끌어당기는 반지름입니다.")]
    [SerializeField] private float suctionRadius = 7f;

    [Tooltip("Pawn 슬라임을 배수관 중심으로 끌어당기는 속도입니다.")]
    [SerializeField] private float suctionSpeed = 5f;

    [Tooltip("Pawn 슬라임이 배수관 중심에 이 거리만큼 가까워지면 소멸합니다.")]
    [SerializeField] private float consumeDistance = 0.2f;

    [Tooltip("한 번에 검사할 Pawn 슬라임 콜라이더 최대 개수입니다.")]
    [SerializeField] private int maxPawnChecks = 64;

    [Header("Temporary Visual")]
    [Tooltip("임시 코르크 마개 색상입니다. 실제 스프라이트 적용 시 제거할 임시 연출입니다.")]
    [SerializeField] private Color corkColor = new Color(0.56f, 0.34f, 0.17f, 1f);

    [Tooltip("3회 피격 후 표시할 임시 파괴 색상입니다. 실제 스프라이트 적용 시 제거할 임시 연출입니다.")]
    [SerializeField] private Color brokenColor = Color.black;

    private static Texture2D sharedTemporaryCircleTexture;
    private static Sprite sharedTemporaryCircleSprite;

    private readonly List<Pawn> suctionTargets = new List<Pawn>();
    private Collider2D[] pawnColliders;
    private int currentHitCount;
    private bool isBroken;

    private void Awake()
    {
        CacheReferences();
        EnsurePawnBuffer();
        EnsureTemporaryCircleSprite();
        SyncVisual();
    }

    private void OnValidate()
    {
        hitCountToBreak = Mathf.Max(1, hitCountToBreak);
        suctionRadius = Mathf.Max(0f, suctionRadius);
        suctionSpeed = Mathf.Max(0f, suctionSpeed);
        consumeDistance = Mathf.Max(0.01f, consumeDistance);
        maxPawnChecks = Mathf.Max(1, maxPawnChecks);

        CacheReferences();
        SyncVisual();
    }

    private void FixedUpdate()
    {
        if (!isBroken)
            return;

        EnsurePawnBuffer();
        AcquirePawnTargets();
        PullPawnTargets();
    }

    /// <summary>배수관 피격 횟수를 누적하고 3회 이상이면 파괴 상태로 전환합니다.</summary>
    public bool TryApplyDamage(DamageRequest request)
    {
        if (isBroken)
            return false;

        int hitAmount = Mathf.Max(1, request.TokenDamage);
        currentHitCount = Mathf.Min(hitCountToBreak, currentHitCount + hitAmount);

        if (currentHitCount >= hitCountToBreak)
            BreakPipe();

        return true;
    }

    /// <summary>필요한 컴포넌트 참조를 현재 오브젝트에서 가져옵니다.</summary>
    private void CacheReferences()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>파괴 여부에 맞춰 임시 스프라이트 색상을 갱신합니다.</summary>
    private void SyncVisual()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.color = isBroken ? brokenColor : corkColor;
    }

    /// <summary>파괴 상태로 전환하고 흡입 연출이 시작되도록 표시를 갱신합니다.</summary>
    private void BreakPipe()
    {
        isBroken = true;
        SyncVisual();
    }

    /// <summary>흡입 범위 안에 들어온 Pawn 슬라임을 흡입 대상으로 등록합니다.</summary>
    private void AcquirePawnTargets()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        filter.useLayerMask = false;

        int hitCount = Physics2D.OverlapCircle(transform.position, suctionRadius, filter, pawnColliders);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = pawnColliders[i];
            if (hitCollider == null)
                continue;

            Pawn pawn = hitCollider.GetComponentInParent<Pawn>();
            if (pawn == null || suctionTargets.Contains(pawn))
                continue;

            PreparePawnForSuction(pawn);
            suctionTargets.Add(pawn);
            pawnColliders[i] = null;
        }
    }

    /// <summary>등록된 Pawn 슬라임을 배수관 중심으로 끌어당기고 가까워지면 제거합니다.</summary>
    private void PullPawnTargets()
    {
        Vector2 drainPosition = transform.position;

        for (int i = suctionTargets.Count - 1; i >= 0; i--)
        {
            Pawn pawn = suctionTargets[i];
            if (pawn == null)
            {
                suctionTargets.RemoveAt(i);
                continue;
            }

            Rigidbody2D pawnBody = pawn.GetComponent<Rigidbody2D>();
            Vector2 pawnPosition = pawnBody != null ? pawnBody.position : (Vector2)pawn.transform.position;
            Vector2 toDrain = drainPosition - pawnPosition;

            if (toDrain.magnitude <= consumeDistance)
            {
                Destroy(pawn.gameObject);
                suctionTargets.RemoveAt(i);
                continue;
            }

            Vector2 suctionVelocity = toDrain.normalized * suctionSpeed;
            if (pawnBody != null && pawnBody.simulated)
                pawnBody.linearVelocity = suctionVelocity;
            else
                pawn.transform.position = Vector2.MoveTowards(pawnPosition, drainPosition, suctionSpeed * Time.fixedDeltaTime);
        }
    }

    /// <summary>Pawn 슬라임의 기존 AI와 능력 이동을 멈추고 배수관 흡입 이동만 적용되게 합니다.</summary>
    private void PreparePawnForSuction(Pawn pawn)
    {
        if (pawn == null)
            return;

        MobAbilityCoordinator abilityCoordinator = pawn.GetComponent<MobAbilityCoordinator>();
        if (abilityCoordinator != null)
            abilityCoordinator.CancelActiveAbility(true);

        EnemyChaseIntent2D chaseIntent = pawn.GetComponent<EnemyChaseIntent2D>();
        if (chaseIntent != null)
            chaseIntent.enabled = false;

        MovementMotor2D movementMotor = pawn.GetComponent<MovementMotor2D>();
        if (movementMotor != null)
        {
            movementMotor.StopAllMotion();
            movementMotor.enabled = false;
        }

        Rigidbody2D pawnBody = pawn.GetComponent<Rigidbody2D>();
        if (pawnBody != null)
            pawnBody.linearVelocity = Vector2.zero;

        Collider2D[] ownedColliders = pawn.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < ownedColliders.Length; i++)
        {
            if (ownedColliders[i] != null)
                ownedColliders[i].enabled = false;
        }

        pawn.enabled = false;
    }

    /// <summary>흡입 범위 검사에 사용할 콜라이더 버퍼를 준비합니다.</summary>
    private void EnsurePawnBuffer()
    {
        if (pawnColliders != null && pawnColliders.Length == maxPawnChecks)
            return;

        pawnColliders = new Collider2D[maxPawnChecks];
    }

    /// <summary>스프라이트가 비어 있을 때만 런타임 임시 원형 스프라이트를 채웁니다.</summary>
    private void EnsureTemporaryCircleSprite()
    {
        if (spriteRenderer == null || spriteRenderer.sprite != null)
            return;

        if (sharedTemporaryCircleSprite == null)
            CreateTemporaryCircleSprite();

        spriteRenderer.sprite = sharedTemporaryCircleSprite;
    }

    /// <summary>임시 배수관 표시에 사용할 흰색 원형 스프라이트를 생성합니다.</summary>
    private static void CreateTemporaryCircleSprite()
    {
        const int textureSize = 64;
        const float radius = 31.5f;

        sharedTemporaryCircleTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        sharedTemporaryCircleTexture.name = "TemporaryDrainPipeCircle";
        sharedTemporaryCircleTexture.filterMode = FilterMode.Point;
        sharedTemporaryCircleTexture.hideFlags = HideFlags.HideAndDontSave;

        Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                Vector2 pixelPosition = new Vector2(x, y);
                bool isInsideCircle = Vector2.Distance(pixelPosition, center) <= radius;
                sharedTemporaryCircleTexture.SetPixel(x, y, isInsideCircle ? Color.white : Color.clear);
            }
        }

        sharedTemporaryCircleTexture.Apply();
        sharedTemporaryCircleSprite = Sprite.Create(
            sharedTemporaryCircleTexture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize);
        sharedTemporaryCircleSprite.name = "TemporaryDrainPipeCircle";
        sharedTemporaryCircleSprite.hideFlags = HideFlags.HideAndDontSave;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isBroken ? Color.black : new Color(0.56f, 0.34f, 0.17f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, suctionRadius);
    }
}
