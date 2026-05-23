using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer), typeof(CombatHurtbox2D))]
public sealed class DrainPipe : MonoBehaviour, IDamageReceiver
{
    private const float PhaseTwoBossDrainSeconds = 4f;

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
    private SlimeQueenPhaseTwoBase phaseTwoBossTarget;
    private Coroutine phaseTwoBossDrainCoroutine;
    private PhaseTwoBossDrainContext activePhaseTwoBossDrainContext;
    private int currentHitCount;
    private bool isBroken;
    private bool isBlocked;

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

    private void OnDisable()
    {
        if (phaseTwoBossDrainCoroutine != null)
        {
            StopCoroutine(phaseTwoBossDrainCoroutine);
            phaseTwoBossDrainCoroutine = null;
        }

        activePhaseTwoBossDrainContext?.Restore();
        activePhaseTwoBossDrainContext = null;

        if (phaseTwoBossTarget != null)
            phaseTwoBossTarget.EndDrainControlLock();

        phaseTwoBossTarget = null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryAcquirePhaseTwoBossOnContact(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryAcquirePhaseTwoBossOnContact(other);
    }

    private void FixedUpdate()
    {
        if (!isBroken || isBlocked)
            return;

        EnsurePawnBuffer();

        if (phaseTwoBossDrainCoroutine == null)
        {
            PullPhaseTwoBossTarget();
        }

        AcquirePawnTargets();
        PullPawnTargets();
    }

    /// <summary>배수관 피격 횟수를 누적하고 3회 이상이면 파괴 상태로 전환합니다.</summary>
    public bool TryApplyDamage(DamageRequest request)
    {
        if (isBroken || isBlocked)
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

        spriteRenderer.color = isBroken && !isBlocked ? brokenColor : corkColor;
    }

    /// <summary>파괴 상태로 전환하고 흡입 연출이 시작되도록 표시를 갱신합니다.</summary>
    private void BreakPipe()
    {
        if (isBlocked)
            return;

        isBroken = true;
        SyncVisual();
    }

    /// <summary>배수구 트리거에 직접 닿은 2페이즈 슬라임 여왕만 배수구 대상으로 등록합니다.</summary>
    private void TryAcquirePhaseTwoBossOnContact(Collider2D hitCollider)
    {
        if (!isBroken || isBlocked || phaseTwoBossTarget != null || phaseTwoBossDrainCoroutine != null || hitCollider == null)
            return;

        SlimeQueenPhaseTwoBase slimeQueen = hitCollider.GetComponentInParent<SlimeQueenPhaseTwoBase>();
        if (!CanAcquirePhaseTwoBoss(slimeQueen))
            return;

        PreparePhaseTwoBossForSuction(slimeQueen);
        phaseTwoBossTarget = slimeQueen;
    }

    /// <summary>등록된 2페이즈 슬라임 여왕을 배수구 중심으로 끌어당기고 중심에 닿으면 4초 잠금으로 전환합니다.</summary>
    private void PullPhaseTwoBossTarget()
    {
        if (phaseTwoBossTarget == null)
            return;

        if (!IsPhaseTwoBossAlive(phaseTwoBossTarget))
        {
            phaseTwoBossTarget.EndDrainControlLock();
            phaseTwoBossTarget = null;
            return;
        }

        Vector2 drainPosition = transform.position;
        Rigidbody2D bossBody = phaseTwoBossTarget.GetComponent<Rigidbody2D>();
        Vector2 bossPosition = bossBody != null && bossBody.simulated
            ? bossBody.position
            : (Vector2)phaseTwoBossTarget.transform.position;
        Vector2 toDrain = drainPosition - bossPosition;

        if (toDrain.magnitude <= consumeDistance)
        {
            SlimeQueenPhaseTwoBase drainedBoss = phaseTwoBossTarget;
            drainedBoss.transform.position = GetDrainPosition(drainedBoss.transform.position.z);
            ResetBodyVelocity(bossBody);
            phaseTwoBossTarget = null;
            phaseTwoBossDrainCoroutine = StartCoroutine(DrainPhaseTwoBossRoutine(drainedBoss));
            return;
        }

        Vector2 suctionVelocity = toDrain.normalized * suctionSpeed;
        if (bossBody != null && bossBody.simulated)
            bossBody.linearVelocity = suctionVelocity;
        else
            phaseTwoBossTarget.transform.position = Vector2.MoveTowards(
                bossPosition,
                drainPosition,
                suctionSpeed * Time.fixedDeltaTime);
    }

    /// <summary>2페이즈 슬라임 여왕의 현재 패턴을 중단하고 배수구 흡입 이동만 적용되게 합니다.</summary>
    private static void PreparePhaseTwoBossForSuction(SlimeQueenPhaseTwoBase slimeQueen)
    {
        if (slimeQueen == null)
            return;

        slimeQueen.BeginDrainControlLock();
        ResetBodyVelocity(slimeQueen.GetComponent<Rigidbody2D>());
    }

    /// <summary>배수구 안에 잠긴 보스를 4초 뒤 복귀시키고 배수구를 막힘 상태로 전환합니다.</summary>
    private IEnumerator DrainPhaseTwoBossRoutine(SlimeQueenPhaseTwoBase slimeQueen)
    {
        PhaseTwoBossDrainContext drainContext = null;
        if (slimeQueen != null)
        {
            drainContext = new PhaseTwoBossDrainContext(slimeQueen);
            activePhaseTwoBossDrainContext = drainContext;
            slimeQueen.transform.position = GetDrainPosition(slimeQueen.transform.position.z);
            slimeQueen.BeginDrainSinkAnimation();
            drainContext.SetSubmerged(true);
        }

        yield return new WaitForSeconds(PhaseTwoBossDrainSeconds);

        if (slimeQueen != null)
            slimeQueen.transform.position = GetDrainPosition(slimeQueen.transform.position.z);

        drainContext?.Restore();
        activePhaseTwoBossDrainContext = null;
        phaseTwoBossDrainCoroutine = null;
        BlockPipe();
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

    /// <summary>배수구가 한 번 보스를 삼킨 뒤 다시 사용할 수 없도록 막힘 상태로 전환합니다.</summary>
    private void BlockPipe()
    {
        isBlocked = true;
        isBroken = false;
        currentHitCount = hitCountToBreak;
        ConsumeRemainingPawnTargets();
        SyncVisual();
    }

    /// <summary>막힘 전까지 이미 흡입 대상이 된 Pawn이 비활성 상태로 남지 않도록 정리합니다.</summary>
    private void ConsumeRemainingPawnTargets()
    {
        for (int i = suctionTargets.Count - 1; i >= 0; i--)
        {
            Pawn pawn = suctionTargets[i];
            if (pawn != null)
                Destroy(pawn.gameObject);
        }

        suctionTargets.Clear();
    }

    private bool CanAcquirePhaseTwoBoss(SlimeQueenPhaseTwoBase slimeQueen)
    {
        if (isBlocked || slimeQueen == null || slimeQueen == phaseTwoBossTarget)
            return false;

        if (!IsPhaseTwoBossAlive(slimeQueen))
            return false;

        return slimeQueen.IsCombatActive && slimeQueen.CanTriggerPitFall;
    }

    private static bool IsPhaseTwoBossAlive(SlimeQueenPhaseTwoBase slimeQueen)
    {
        return slimeQueen != null &&
               slimeQueen.isActiveAndEnabled &&
               slimeQueen.gameObject.activeInHierarchy &&
               !slimeQueen.IsDead &&
               !slimeQueen.HasDeadTag() &&
               slimeQueen.CurrentHealthValue > 0f;
    }

    private Vector3 GetDrainPosition(float targetZ)
    {
        Vector3 drainPosition = transform.position;
        drainPosition.z = targetZ;
        return drainPosition;
    }

    private static void ResetBodyVelocity(Rigidbody2D body)
    {
        if (body == null)
            return;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
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

    private sealed class PhaseTwoBossDrainContext
    {
        private readonly SlimeQueenPhaseTwoBase slimeQueen;
        private readonly Rigidbody2D body;
        private readonly bool wasBodySimulated;
        private readonly Collider2D[] colliders;
        private readonly bool[] colliderEnabledStates;
        private bool isRestored;

        public PhaseTwoBossDrainContext(SlimeQueenPhaseTwoBase slimeQueen)
        {
            this.slimeQueen = slimeQueen;
            body = slimeQueen != null ? slimeQueen.GetComponent<Rigidbody2D>() : null;
            wasBodySimulated = body == null || body.simulated;
            colliders = slimeQueen != null ? slimeQueen.GetComponentsInChildren<Collider2D>() : new Collider2D[0];
            colliderEnabledStates = new bool[colliders.Length];

            for (int i = 0; i < colliders.Length; i++)
                colliderEnabledStates[i] = colliders[i] != null && colliders[i].enabled;
        }

        public void SetSubmerged(bool isSubmerged)
        {
            if (!isSubmerged)
                return;

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = false;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = false;
            }
        }

        public void Restore()
        {
            if (isRestored)
                return;

            isRestored = true;

            if (body != null)
            {
                body.simulated = wasBodySimulated;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = colliderEnabledStates[i];
            }

            if (slimeQueen != null)
                slimeQueen.EndDrainControlLock();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isBroken && !isBlocked ? Color.black : new Color(0.56f, 0.34f, 0.17f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, suctionRadius);
    }
}
