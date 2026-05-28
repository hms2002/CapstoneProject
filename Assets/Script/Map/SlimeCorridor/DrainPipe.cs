using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer), typeof(CombatHurtbox2D))]
public sealed class DrainPipe : MonoBehaviour, IDamageReceiver
{
    private const float PhaseTwoBossDrainSeconds = 4f;
    private const float PhaseTwoBossExitJumpSeconds = 0.45f;
    private const float PhaseTwoBossExitJumpArcHeight = 0.85f;

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

    [Tooltip("한 번에 검사할 흡입 대상 콜라이더 최대 개수입니다. Pawn 슬라임과 P2 슬라임 여왕 범위 검사에 함께 사용합니다.")]
    [SerializeField] private int maxPawnChecks = 64;

    [Header("Phase 2 Boss Suction")]
    [Tooltip("파괴된 배수관이 P2 슬라임 여왕을 끌어당기기 시작하는 반지름입니다. Pawn 흡입 반경과 별도로 조절합니다.")]
    [SerializeField] private float phaseTwoBossSuctionRadius = 2f;

    [Header("Temporary Visual")]
    [Tooltip("임시 코르크 마개 색상입니다. 실제 스프라이트 적용 시 제거할 임시 연출입니다.")]
    [SerializeField] private Color corkColor = new Color(0.56f, 0.34f, 0.17f, 1f);

    [Tooltip("3회 피격 후 표시할 임시 파괴 색상입니다. 실제 스프라이트 적용 시 제거할 임시 연출입니다.")]
    [SerializeField] private Color brokenColor = Color.black;

    [Header("Sound")]
    [Tooltip("배수관이 열릴 때 재생할 사운드입니다.")]
    [SerializeField] private SoundRef openSound = SoundRef.FromKey("sound_drainPipe_Open");
    [Tooltip("배수관 흡입이 활성화된 동안 반복 재생할 사운드입니다.")]
    [SerializeField] private SoundRef waterfallLoopSound = SoundRef.FromKey("sound_drainPipe_WtaerFall");
    [Tooltip("Pawn 슬라임이 배수관에 빨려 들어갈 때 무작위로 재생할 사운드 후보입니다.")]
    [SerializeField] private SoundRef[] slimeFallSounds =
    {
        SoundRef.FromKey("sound_drainPipe_SlimeFall1"),
        SoundRef.FromKey("sound_drainPipe_SlimeFall2"),
        SoundRef.FromKey("sound_drainPipe_SlimeFall3"),
        SoundRef.FromKey("sound_drainPipe_SlimeFall4")
    };
    [Tooltip("2페이즈 근거리 슬라임 퀸이 배수구에서 복귀하기 직전에 재생할 사운드입니다.")]
    [SerializeField] private SoundRef phaseTwoBossReturnSound = SoundRef.FromKey("sound_slimeQueen_Return");

    private static Texture2D sharedTemporaryCircleTexture;
    private static Sprite sharedTemporaryCircleSprite;

    private readonly List<Pawn> suctionTargets = new List<Pawn>();
    private Collider2D[] overlapColliders;
    private SlimeQueenPhaseTwoBase phaseTwoBossTarget;
    private Coroutine phaseTwoBossDrainCoroutine;
    private PhaseTwoBossDrainContext activePhaseTwoBossDrainContext;
    private AudioHandle waterfallLoopHandle;
    private int currentHitCount;
    private bool isBroken;

    private void Awake()
    {
        CacheReferences();
        EnsureOverlapBuffer();
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
        phaseTwoBossSuctionRadius = Mathf.Max(0f, phaseTwoBossSuctionRadius);

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
        StopWaterfallLoop();

        if (phaseTwoBossTarget != null)
            phaseTwoBossTarget.EndDrainControlLock();

        phaseTwoBossTarget = null;
    }

    private void FixedUpdate()
    {
        if (!isBroken)
            return;

        EnsureOverlapBuffer();

        if (phaseTwoBossDrainCoroutine == null)
        {
            AcquirePhaseTwoBossTarget();
            PullPhaseTwoBossTarget();
        }

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
        SoundPlaybackUtility.Play(openSound, causer: gameObject, position: transform.position, sourceObject: this);
        StartWaterfallLoop();
    }

    /// <summary>배수구 흡입 범위 안에 들어온 2페이즈 슬라임 여왕을 배수구 대상으로 등록합니다.</summary>
    private void AcquirePhaseTwoBossTarget()
    {
        if (!isBroken || phaseTwoBossTarget != null || phaseTwoBossDrainCoroutine != null || phaseTwoBossSuctionRadius <= 0f)
            return;

        ContactFilter2D filter = CreateSuctionFilter();
        int hitCount = Physics2D.OverlapCircle(transform.position, phaseTwoBossSuctionRadius, filter, overlapColliders);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = overlapColliders[i];
            if (hitCollider == null)
                continue;

            SlimeQueenPhaseTwoBase slimeQueen = hitCollider.GetComponentInParent<SlimeQueenPhaseTwoBase>();
            if (!CanAcquirePhaseTwoBoss(slimeQueen))
                continue;

            activePhaseTwoBossDrainContext = new PhaseTwoBossDrainContext(slimeQueen);
            PreparePhaseTwoBossForSuction(slimeQueen, activePhaseTwoBossDrainContext);
            phaseTwoBossTarget = slimeQueen;
            return;
        }
    }

    /// <summary>등록된 2페이즈 슬라임 여왕을 배수구 중심으로 끌어당기고 중심에 닿으면 4초 잠금으로 전환합니다.</summary>
    private void PullPhaseTwoBossTarget()
    {
        if (phaseTwoBossTarget == null)
            return;

        if (!IsPhaseTwoBossAlive(phaseTwoBossTarget))
        {
            activePhaseTwoBossDrainContext?.Restore();
            activePhaseTwoBossDrainContext = null;
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
            PhaseTwoBossDrainContext drainContext = activePhaseTwoBossDrainContext ?? new PhaseTwoBossDrainContext(drainedBoss);
            drainedBoss.transform.position = GetDrainPosition(drainedBoss.transform.position.z);
            ResetBodyVelocity(bossBody);
            phaseTwoBossTarget = null;
            activePhaseTwoBossDrainContext = drainContext;
            phaseTwoBossDrainCoroutine = StartCoroutine(DrainPhaseTwoBossRoutine(drainedBoss, drainContext));
            return;
        }

        Vector2 nextPosition = Vector2.MoveTowards(
            bossPosition,
            drainPosition,
            suctionSpeed * Time.fixedDeltaTime);

        if (bossBody != null && bossBody.simulated)
        {
            bossBody.linearVelocity = Vector2.zero;
            bossBody.MovePosition(nextPosition);
        }
        else
        {
            phaseTwoBossTarget.transform.position = nextPosition;
        }
    }

    /// <summary>2페이즈 슬라임 여왕의 현재 패턴을 중단하고 배수구 흡입 이동만 적용되게 합니다.</summary>
    private static void PreparePhaseTwoBossForSuction(SlimeQueenPhaseTwoBase slimeQueen, PhaseTwoBossDrainContext drainContext)
    {
        if (slimeQueen == null)
            return;

        slimeQueen.BeginDrainControlLock();
        drainContext?.BeginSuction();
        ResetBodyVelocity(slimeQueen.GetComponent<Rigidbody2D>());
    }

    /// <summary>배수구 안에 잠긴 보스를 4초 뒤 복귀시키고 배수구를 원상복구합니다.</summary>
    private IEnumerator DrainPhaseTwoBossRoutine(SlimeQueenPhaseTwoBase slimeQueen, PhaseTwoBossDrainContext drainContext)
    {
        if (slimeQueen != null)
        {
            drainContext ??= new PhaseTwoBossDrainContext(slimeQueen);
            activePhaseTwoBossDrainContext = drainContext;
            slimeQueen.transform.position = GetDrainPosition(slimeQueen.transform.position.z);
            slimeQueen.BeginDrainSinkAnimation();
            drainContext.SetSubmerged(true);
        }

        yield return HoldPhaseTwoBossInDrain(slimeQueen, PhaseTwoBossDrainSeconds);

        if (IsPhaseTwoBossAlive(slimeQueen))
            yield return PlayPhaseTwoBossExitJump(slimeQueen, drainContext);

        drainContext?.Restore();
        activePhaseTwoBossDrainContext = null;
        phaseTwoBossDrainCoroutine = null;
        RestorePipe();
    }

    /// <summary>흡입 범위 안에 들어온 Pawn 슬라임을 흡입 대상으로 등록합니다.</summary>
    private void AcquirePawnTargets()
    {
        ContactFilter2D filter = CreateSuctionFilter();

        int hitCount = Physics2D.OverlapCircle(transform.position, suctionRadius, filter, overlapColliders);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = overlapColliders[i];
            if (hitCollider == null)
                continue;

            Pawn pawn = hitCollider.GetComponentInParent<Pawn>();
            if (pawn == null || suctionTargets.Contains(pawn))
                continue;

            PreparePawnForSuction(pawn);
            suctionTargets.Add(pawn);
            overlapColliders[i] = null;
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
                PlayRandomSlimeFallSound(pawn);
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

    /// <summary>보스가 빠져나온 뒤 배수구를 다시 피격 가능한 초기 상태로 되돌립니다.</summary>
    private void RestorePipe()
    {
        isBroken = false;
        currentHitCount = 0;
        ConsumeRemainingPawnTargets();
        StopWaterfallLoop();
        SyncVisual();
    }

    /// <summary>배수관 흡입 루프 사운드를 시작합니다.</summary>
    private void StartWaterfallLoop()
    {
        if (waterfallLoopHandle.IsValid)
            return;

        waterfallLoopHandle = SoundPlaybackUtility.Play(waterfallLoopSound, causer: gameObject, position: transform.position, sourceObject: this);
    }

    /// <summary>배수관 흡입 루프 사운드를 중단해 씬 전환/비활성 후 잔류를 막습니다.</summary>
    private void StopWaterfallLoop()
    {
        if (!waterfallLoopHandle.IsValid)
            return;

        SoundPlaybackUtility.Stop(waterfallLoopHandle, 0.12f);
        waterfallLoopHandle = AudioHandle.Invalid;
    }

    /// <summary>Pawn 슬라임이 배수관에 빠질 때 후보 중 하나를 무작위로 재생합니다.</summary>
    private void PlayRandomSlimeFallSound(Pawn pawn)
    {
        if (slimeFallSounds == null || slimeFallSounds.Length == 0)
            return;

        SoundRef sound = slimeFallSounds[Random.Range(0, slimeFallSounds.Length)];
        GameObject target = pawn != null ? pawn.gameObject : null;
        SoundPlaybackUtility.Play(sound, causer: gameObject, target: target, position: transform.position, sourceObject: this);
    }

    /// <summary>원상복구 전까지 이미 흡입 대상이 된 Pawn이 비활성 상태로 남지 않도록 정리합니다.</summary>
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
        if (slimeQueen == null || slimeQueen == phaseTwoBossTarget)
            return false;

        if (!IsPhaseTwoBossAlive(slimeQueen))
            return false;

        return slimeQueen.IsCombatActive && slimeQueen.CanTriggerPitFall;
    }

    /// <summary>열린 배수구의 2페이즈 보스 흡입 반경 안에 지정 좌표가 들어오는지 확인합니다.</summary>
    public bool ContainsActivePhaseTwoBossSuctionPoint(Vector3 worldPosition, float extraRadius = 0f)
    {
        if (!isBroken || phaseTwoBossSuctionRadius <= 0f)
            return false;

        float radius = Mathf.Max(0f, phaseTwoBossSuctionRadius + extraRadius);
        return Vector2.Distance(transform.position, worldPosition) <= radius;
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

    /// <summary>배수구 안 4초 그로기 동안 피격 판정은 유지하고 위치만 배수구에 고정합니다.</summary>
    private IEnumerator HoldPhaseTwoBossInDrain(SlimeQueenPhaseTwoBase slimeQueen, float durationSeconds)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0f, durationSeconds);

        while (elapsed < duration && IsPhaseTwoBossAlive(slimeQueen))
        {
            SetPhaseTwoBossPosition(slimeQueen, GetDrainPosition(slimeQueen.transform.position.z));
            ResetBodyVelocity(slimeQueen.GetComponent<Rigidbody2D>());

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>배수구에서 빠져나올 때 내려찍기류 패턴처럼 짧은 포물선 점프를 보여줍니다.</summary>
    private IEnumerator PlayPhaseTwoBossExitJump(SlimeQueenPhaseTwoBase slimeQueen, PhaseTwoBossDrainContext drainContext)
    {
        if (!IsPhaseTwoBossAlive(slimeQueen))
            yield break;

        drainContext?.BeginExitJump();
        slimeQueen.EndDrainSinkAnimation();
        SoundPlaybackUtility.Play(
            phaseTwoBossReturnSound,
            slimeQueen.gameObject,
            gameObject,
            slimeQueen.gameObject,
            transform.position,
            this);

        Vector3 startPosition = GetDrainPosition(slimeQueen.transform.position.z);
        Vector3 landingPosition = startPosition;
        float duration = Mathf.Max(0.01f, PhaseTwoBossExitJumpSeconds);
        float elapsed = 0f;

        while (elapsed < duration && IsPhaseTwoBossAlive(slimeQueen))
        {
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 groundPosition = Vector3.Lerp(startPosition, landingPosition, t);
            float arcOffset = Mathf.Sin(t * Mathf.PI) * PhaseTwoBossExitJumpArcHeight;

            SetPhaseTwoBossPosition(slimeQueen, groundPosition + Vector3.up * arcOffset);
            ResetBodyVelocity(slimeQueen.GetComponent<Rigidbody2D>());

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (IsPhaseTwoBossAlive(slimeQueen))
        {
            SetPhaseTwoBossPosition(slimeQueen, landingPosition);
            ResetBodyVelocity(slimeQueen.GetComponent<Rigidbody2D>());
        }
    }

    private Vector3 GetDrainPosition(float targetZ)
    {
        Vector3 drainPosition = transform.position;
        drainPosition.z = targetZ;
        return drainPosition;
    }

    private static void SetPhaseTwoBossPosition(SlimeQueenPhaseTwoBase slimeQueen, Vector3 position)
    {
        if (slimeQueen == null)
            return;

        slimeQueen.transform.position = position;
    }

    private static void ResetBodyVelocity(Rigidbody2D body)
    {
        if (body == null)
            return;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private static ContactFilter2D CreateSuctionFilter()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        filter.useLayerMask = false;
        return filter;
    }

    /// <summary>흡입 범위 검사에 사용할 콜라이더 버퍼를 준비합니다.</summary>
    private void EnsureOverlapBuffer()
    {
        if (overlapColliders != null && overlapColliders.Length == maxPawnChecks)
            return;

        overlapColliders = new Collider2D[maxPawnChecks];
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
        private readonly RigidbodyConstraints2D wasBodyConstraints;
        private readonly MovementMotor2D movementMotor;
        private readonly bool wasMovementMotorEnabled;
        private readonly Collider2D[] colliders;
        private readonly bool[] colliderEnabledStates;
        private bool isRestored;

        public PhaseTwoBossDrainContext(SlimeQueenPhaseTwoBase slimeQueen)
        {
            this.slimeQueen = slimeQueen;
            body = slimeQueen != null ? slimeQueen.GetComponent<Rigidbody2D>() : null;
            wasBodySimulated = body == null || body.simulated;
            wasBodyConstraints = body != null ? body.constraints : RigidbodyConstraints2D.None;
            movementMotor = slimeQueen != null ? slimeQueen.GetComponent<MovementMotor2D>() : null;
            wasMovementMotorEnabled = movementMotor == null || movementMotor.enabled;
            colliders = slimeQueen != null ? slimeQueen.GetComponentsInChildren<Collider2D>() : new Collider2D[0];
            colliderEnabledStates = new bool[colliders.Length];

            for (int i = 0; i < colliders.Length; i++)
                colliderEnabledStates[i] = colliders[i] != null && colliders[i].enabled;
        }

        public void BeginSuction()
        {
            if (isRestored)
                return;

            if (movementMotor != null)
            {
                movementMotor.StopAllMotion();
                movementMotor.enabled = false;
            }

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        public void SetSubmerged(bool isSubmerged)
        {
            if (!isSubmerged)
                return;

            BeginSuction();

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = true;
                body.constraints = wasBodyConstraints |
                                   RigidbodyConstraints2D.FreezePositionX |
                                   RigidbodyConstraints2D.FreezePositionY |
                                   RigidbodyConstraints2D.FreezeRotation;
            }
        }

        public void BeginExitJump()
        {
            if (isRestored)
                return;

            if (body != null)
            {
                body.simulated = true;
                body.constraints = wasBodyConstraints;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        public void Restore()
        {
            if (isRestored)
                return;

            isRestored = true;
            bool shouldRestoreGameplayComponents = slimeQueen != null &&
                                                   !slimeQueen.IsDead &&
                                                   !slimeQueen.HasDeadTag() &&
                                                   slimeQueen.CurrentHealthValue > 0f;

            if (shouldRestoreGameplayComponents && body != null)
            {
                body.simulated = wasBodySimulated;
                body.constraints = wasBodyConstraints;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            if (shouldRestoreGameplayComponents)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                        colliders[i].enabled = colliderEnabledStates[i];
                }

                if (movementMotor != null)
                    movementMotor.enabled = wasMovementMotorEnabled;
            }

            if (slimeQueen != null)
                slimeQueen.EndDrainControlLock();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;

        Gizmos.color = isBroken ? Color.black : new Color(0.56f, 0.34f, 0.17f, 0.7f);
        Gizmos.DrawWireSphere(center, Mathf.Max(0f, suctionRadius));

        Gizmos.color = new Color(0.1f, 0.75f, 1f, 0.9f);
        Gizmos.DrawWireSphere(center, Mathf.Max(0f, phaseTwoBossSuctionRadius));
    }
}
