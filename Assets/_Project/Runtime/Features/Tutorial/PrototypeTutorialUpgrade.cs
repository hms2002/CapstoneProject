using System;
using UnityEngine;
using UnityEngine.Events;
using UnityGAS;

/// <summary>Scene-local prototype missions. All targets, gates, bullets and UI are authored in the scene.</summary>
[DisallowMultipleComponent]
public sealed class PrototypeTutorialUpgrade : MonoBehaviour
{
    [SerializeField] private Transform movementGoal;
    [SerializeField] private Transform dashGoal;
    [SerializeField] private GameObject[] gates = new GameObject[4];
    [SerializeField] private Transform attackTarget;
    [SerializeField] private Transform[] skillTargets = new Transform[7];
    [SerializeField] private Transform[] bullets = Array.Empty<Transform>();
    [SerializeField] private Transform bulletTop;
    [SerializeField] private Transform bulletBottom;
    [SerializeField] private AbilityDefinition basicAttack;
    [SerializeField] private AbilityDefinition chargeSkill;
    [SerializeField] private AbilityDefinition thrustSkill;
    [SerializeField] private AbilityDefinition dash;
    [SerializeField] private GameplayTag hitConfirmed;
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField] private AttributeDefinition maxHealthAttribute;
    [SerializeField] private TutorialPlayerHealthAutoRecover healthRecovery;
    [SerializeField] private GameObject chestPractice;
    [SerializeField] private UnityEvent<string> onProgressChanged = new();

    [Header("Authored monster encounter")]
    [SerializeField] private GoblinGunner tutorialGunner;
    [SerializeField] private PrototypeTutorialOpeningSequence opening = new();
    private Coroutine openingRoutine;
    public bool IsOpeningCinematic => opening.IsPending;
    [SerializeField] private GoblinWarrior skillMonsterPrefab;
    [SerializeField] private Transform[] skillSpawnPoints = Array.Empty<Transform>();
    [SerializeField] private Transform gunnerRetreatPoint;
    [SerializeField] private Transform bulletFireStart;
    [SerializeField] private DoorObject dodgeEntranceDoor;
    private bool gunnerVolleyStarted;
    [SerializeField] private ChestMonsterKillLock chestLock;
    [SerializeField] private TreasureChest portalDoorChest;
    [SerializeField] private DoorObject bossPortalDoor;
    private Enemy[] skillMonsters;
    private bool[] creditedKills;
    private bool retreating, holdsChestLock;
    private bool gunnerWasHit;
    private int gunnerWalkSide = 1;
    private const float IntroStopDistance = .95f;
    private const float IntroApproachDistance = 2.7f;
    private readonly System.Collections.Generic.List<GameObject> spawnedMonsters = new();

    private AbilitySystem player;
    private AttributeSet[] targetAttributes;
    private Vector3[] targetPositions;
    private Vector3[] bulletPositions;
    private bool[] bulletConsumed;
    private bool[] defeated;
    private float[] respawnAt;
    private IDisposable damageFilter;
    private IDisposable chargeCooldown;
    private IDisposable thrustCooldown;
    private AbilityCancellationToken countedAttack;
    private AbilityCancellationToken dashToken;
    private Vector2 previousPlayerPosition;
    private int dashEndFrame = -1;
    private int stage;
    private int attackHits;
    private int chargeKills;
    private int thrustKills;
    private bool evadedBullet;
    private float nextDamageTime;
    private string lastProgress;

    private enum DashIntroPhase { Ready, ZoomIn, Waiting, Dashing, ZoomOut, Done }
    private DashIntroPhase dashIntro;
    private float introElapsed;
    private bool dashKeyHeld;
    private IGameplayCameraFocusSession focus;
    private GameFlowInputBlocker inputBlocker;
    private PlayerCombatInput2D combatInput;
    private float zoomFrom;
    private float zoomReturnFrom;
    private float introTimeScale;

    public int Stage => stage;
    public bool IsMovementPrompt => stage == 0 || (stage == 1 && !IsDashPromptVisible) || stage >= 6;
    public InputActionId PromptAction => stage switch
    {
        1 when IsDashPromptVisible => InputActionId.Dash,
        2 => InputActionId.PrimaryAttack,
        3 => chargeKills < 4 ? InputActionId.Skill1 : InputActionId.Skill2,
        4 => InputActionId.Interact,
        5 => InputActionId.InventoryToggle,
        _ => InputActionId.MoveUp
    };
    public string ProgressText => lastProgress;
    public string PromptInstruction => stage switch
    {
        2 => $"를 길게 눌러서 공격 {attackHits}/9",
        3 => chargeKills < 4
            ? $"를 길게 눌렀다 떼서 스킬로 적을 처치 {chargeKills}/4"
            : $"를 눌러 스킬로 적을 처치 {thrustKills}/3",
        _ => ProgressText
    };
    public bool IsDashPromptVisible => dashIntro == DashIntroPhase.ZoomIn || dashIntro == DashIntroPhase.Waiting;

    private void Awake()
    {
        if (skillMonsterPrefab != null) skillTargets = new Transform[skillSpawnPoints.Length];
        skillMonsters = new Enemy[skillTargets.Length];
        creditedKills = new bool[skillTargets.Length];
        targetAttributes = new AttributeSet[skillTargets.Length];
        targetPositions = new Vector3[skillTargets.Length];
        defeated = new bool[skillTargets.Length];
        respawnAt = new float[skillTargets.Length];
        for (int i = 0; i < skillTargets.Length; i++)
        {
            if (skillTargets[i] != null) targetAttributes[i] = skillTargets[i].GetComponent<AttributeSet>();
            targetPositions[i] = skillMonsterPrefab != null ? skillSpawnPoints[i].position : skillTargets[i].position;
        }
        bulletPositions = new Vector3[bullets.Length];
        bulletConsumed = new bool[bullets.Length];
        for (int i = 0; i < bullets.Length; i++) bulletPositions[i] = bullets[i].position;
    }

    private void OnEnable()
    {
        if (portalDoorChest != null)
        {
            portalDoorChest.FirstOpenedUi += OpenBossPortalDoor;
            if (bossPortalDoor != null)
            {
                if (portalDoorChest.IsOpened) bossPortalDoor.ForceOpen(immediate: true, playPresentation: false);
                else bossPortalDoor.ForceClose(immediate: true);
            }
        }
        stage = attackHits = chargeKills = thrustKills = 0;
        dashIntro = DashIntroPhase.Ready;
        InputActionQuery.SetPressBlocked(InputActionId.Dash, this, true);
        evadedBullet = false;
        nextDamageTime = 0f;
        lastProgress = null;
        if (skillMonsterPrefab == null)
            for (int i = 0; i < skillTargets.Length; i++) ResetTarget(i);
        retreating = false;
        gunnerWasHit = false;
        gunnerWalkSide = 1;
        gunnerVolleyStarted = false;
        if (dodgeEntranceDoor != null)
            dodgeEntranceDoor.ForceOpen(immediate: true, playPresentation: false);
        if (tutorialGunner != null)
        {
            tutorialGunner.SuppressMonsterLootDrop();
            tutorialGunner.ApplySpawnIdlePause(float.MaxValue);
        }
        if (chestLock != null)
        {
            chestLock.ReservePendingMonster();
            holdsChestLock = true;
        }
        for (int i = 0; i < bullets.Length; i++)
        {
            bullets[i].position = bulletPositions[i];
            bulletConsumed[i] = tutorialGunner != null;
        }
        if (healthRecovery != null) healthRecovery.enabled = true;
        damageFilter = CombatIncomingDamageModifiers.Register(FilterDamage);
        PlayerRuntimeRegistry.PlayerRegistered += BindPlayer;
        PlayerRuntimeRegistry.PlayerUnregistered += UnbindPlayer;
        BindPlayer(PlayerRuntimeRegistry.CurrentPlayer);
        RefreshPresentation();
    }

    private void OnDisable()
    {
        if (portalDoorChest != null) portalDoorChest.FirstOpenedUi -= OpenBossPortalDoor;
        PlayerRuntimeRegistry.PlayerRegistered -= BindPlayer;
        PlayerRuntimeRegistry.PlayerUnregistered -= UnbindPlayer;
        ReleasePlayer();
        foreach (Enemy monster in skillMonsters) if (monster != null) monster.DeathStarted -= OnSkillMonsterDeath;
        foreach (GameObject monster in spawnedMonsters) if (monster != null) Destroy(monster);
        spawnedMonsters.Clear();
        ReleaseChestLock();
        damageFilter?.Dispose();
        damageFilter = null;
        if (healthRecovery != null) healthRecovery.enabled = false;
        foreach (Transform bullet in bullets) if (bullet != null) bullet.gameObject.SetActive(false);
    }

    private void BindPlayer(PlayerInteractor2D registered)
    {
        ReleasePlayer();
        if (registered == null) return;
        player = registered.GetComponent<AbilitySystem>();
        if (player == null) return;
        previousPlayerPosition = player.transform.position;
        player.GameplayEventRaised += OnGameplayEvent;
        player.AbilityExecutionStarted += OnExecutionStarted;
        player.AbilityExecutionEnded += OnExecutionEnded;
        InputActionQuery.SetPressBlocked(InputActionId.Dash, this, dashIntro != DashIntroPhase.Done);
        if (stage < 4)
        {
            chargeCooldown = player.AddScopedCooldownDurationMultiplier(
                definition => definition == chargeSkill, 1f / Mathf.Max(1f, chargeSkill.cooldown));
            thrustCooldown = player.AddScopedCooldownDurationMultiplier(
                definition => definition == thrustSkill, 1f / Mathf.Max(1f, thrustSkill.cooldown));
        }
        if (opening.IsPending)
            openingRoutine = StartCoroutine(opening.Play(this, player, tutorialGunner));
    }

    private void UnbindPlayer(PlayerInteractor2D registered)
    {
        if (player != null && registered != null && player.gameObject == registered.gameObject) ReleasePlayer();
    }

    private void ReleasePlayer()
    {
        if (openingRoutine != null) StopCoroutine(openingRoutine);
        openingRoutine = null;
        opening.Cancel();
        CleanupDashIntro();
        InputActionQuery.SetPressBlocked(InputActionId.Dash, this, false);
        if (player != null)
        {
            player.GameplayEventRaised -= OnGameplayEvent;
            player.AbilityExecutionStarted -= OnExecutionStarted;
            player.AbilityExecutionEnded -= OnExecutionEnded;
        }
        chargeCooldown?.Dispose();
        thrustCooldown?.Dispose();
        chargeCooldown = thrustCooldown = null;
        player = null;
        countedAttack = dashToken = null;
        dashEndFrame = -1;
    }

    private void OnExecutionStarted(AbilitySpec spec)
    {
        if (spec.Definition == dash)
        {
            dashToken = spec.Token;
            // Actual execution starts before the dash logic samples its direction.
            spec.SetInt(UnityGAS.Sample.AbilityLogic_Dash2D.TutorialForwardKey, stage == 1 ? 1 : 0);
        }
    }

    private void OnExecutionEnded(AbilitySpec spec, bool cancelled)
    {
        if (spec.Definition != dash) return;
        // A completed dash can cross a projectile between LateUpdate samples.
        // Keep that final swept segment eligible, but never a cancelled dash.
        dashEndFrame = cancelled ? -1 : Time.frameCount;
        dashToken = null;
    }

    private void LateUpdate()
    {
        if (player == null || IsOpeningCinematic) return;
        TickDashIntro();
        if (TimeScalePausePlayback.IsPaused || Time.deltaTime <= 0f) return;
        TickGunner();
        if (stage == 0 && AtGoal(movementGoal)) Advance();
        if (stage == 1)
        {
            TickBullets();
            if (!opening.IsEscaping && AtGunnerFront()) Advance();
        }
        TickSkillTargets();
        previousPlayerPosition = player.transform.position;
        RefreshPresentation();
    }

    private bool AtGoal(Transform goal)
    {
        Vector2 offset = player.transform.position - goal.position;
        return Mathf.Abs(offset.x) < 1.8f && offset.y >= 0f;
    }

    private bool AtGunnerFront() => tutorialGunner != null
        ? Vector2.Distance(player.transform.position, tutorialGunner.transform.position) <= 2.6f
        : AtGoal(dashGoal);

    private bool IsFullCharge(AbilitySpec spec)
    {
        return spec != null && spec.Definition == chargeSkill &&
            chargeSkill.sourceObject is ApprenticeHeroSwordChargeSpinData data &&
            spec.GetFloat(AbilityLogic_ApprenticeHeroSwordChargeSpin.ChargeSecondsKey, 0f) >= data.MaxChargeSeconds;
    }

    private float FilterDamage(CombatIncomingDamageContext context)
    {
        bool ownAttackTarget = attackTarget != null && context.Target == attackTarget.gameObject;
        int index = Array.FindIndex(skillTargets, t => t != null && t.gameObject == context.Target);
        if (!ownAttackTarget && index < 0) return context.BaseDamage;
        if (player == null || context.DamageSpec?.Context?.Instigator != player.gameObject) return 0f;
        var ability = context.DamageSpec.Context.SourceObject as AbilityDefinition;
        if (ownAttackTarget)
        {
            if (stage != 2 || retreating || ability != basicAttack) return 0f;
            // Keep the same gunner alive until all nine confirmed attacks, regardless of weapon damage.
            if (tutorialGunner != null)
            {
                AttributeSet attributes = tutorialGunner.GetComponent<AttributeSet>();
                float gunnerHealth = attributes.GetAttributeValue(damageEffect.healthAttribute);
                if (gunnerHealth < 2f) attributes.TrySetCurrentValue(damageEffect.healthAttribute, 2f, this);
                return Mathf.Min(context.BaseDamage, Mathf.Max(1f, gunnerHealth - 1f));
            }
            return context.BaseDamage;
        }
        if (stage != 3 || defeated[index]) return 0f;
        bool valid = chargeKills < 4
            ? ability == chargeSkill && IsFullCharge(player.FindSpec(chargeSkill)) && (skillMonsterPrefab != null || index < 4)
            : ability == thrustSkill && (skillMonsterPrefab != null || index >= 4);
        if (skillMonsterPrefab != null && !valid) return context.BaseDamage;
        if (!valid || context.BaseDamage <= 0f) return 0f;
        // Prototype targets deliberately die to one qualifying skill hit.
        float health = targetAttributes[index].GetAttributeValue(damageEffect.healthAttribute);
        float shield = damageEffect.absorbShieldAttribute != null
            ? targetAttributes[index].GetAttributeValue(damageEffect.absorbShieldAttribute) : 0f;
        return Mathf.Max(context.BaseDamage, health + shield + 1f);
    }

    private void OnGameplayEvent(GameplayTag tag, AbilityEventData data)
    {
        if (tag != hitConfirmed || data.Spec == null || data.Target == null || player == null) return;
        if (stage == 2 && !retreating && attackTarget != null && data.Target == attackTarget.gameObject && data.Spec.Definition == basicAttack)
        {
            gunnerWasHit = true;
            AbilityCancellationToken token = data.Spec.Token;
            if (token == null || token == countedAttack) return;
            countedAttack = token;
            // Count each confirmed attack once, independently of combo step or recovery.
            attackHits++;
            if (attackHits >= 9)
            {
                tutorialGunner?.RequestDeath(player.gameObject);
                Advance();
            }
        }
        else if (stage == 3)
        {
            int i = Array.FindIndex(skillTargets, t => t != null && t.gameObject == data.Target);
            if (i < 0 || creditedKills[i] || targetAttributes[i].GetAttributeValue(damageEffect.healthAttribute) > 0f) return;
            bool charged = IsFullCharge(data.Spec);
            bool valid = chargeKills < 4
                ? charged && (skillMonsterPrefab != null || i < 4)
                : data.Spec.Definition == thrustSkill && (skillMonsterPrefab != null || i >= 4);
            if (!valid) return;
            creditedKills[i] = true;
            defeated[i] = true;
            respawnAt[i] = Time.time + 1f;
            if (skillMonsterPrefab != null ? charged : i < 4) chargeKills = Mathf.Min(4, chargeKills + 1);
            else thrustKills = Mathf.Min(3, thrustKills + 1);
            if (chargeKills == 4 && thrustKills == 3) Advance();
        }
        RefreshPresentation();
    }

    private void TickSkillTargets()
    {
        for (int i = 0; i < skillTargets.Length; i++)
        {
            if (!defeated[i]) continue;
            if (skillMonsterPrefab != null)
            {
                if (stage == 3 && Time.time >= respawnAt[i]) SpawnSkillMonster(i);
            }
            else
            {
                if (skillTargets[i] != null) skillTargets[i].gameObject.SetActive(false);
                bool groupComplete = i < 4 ? chargeKills >= 4 : thrustKills >= 3;
                if (stage == 3 && !groupComplete && Time.time >= respawnAt[i]) ResetTarget(i);
            }
        }
    }

    private void ReleaseChestLock()
    {
        if (holdsChestLock && chestLock != null) chestLock.ReleasePendingMonster();
        holdsChestLock = false;
    }

    private void TickGunner()
    {
        if (opening.IsEscaping) return;
        if (tutorialGunner == null || tutorialGunner.IsDead) return;
        if (retreating)
        {
            Vector2 offset = gunnerRetreatPoint.position - tutorialGunner.transform.position;
            AbilityMotionController2D motion = tutorialGunner.GetComponent<AbilityMotionController2D>();
            // Let the existing motor own Rigidbody movement and wall collision checks.
            motion.StartDash(offset, Mathf.Min(4f, offset.magnitude / .1f), .1f);
            // Mob.UpdateAnimation drives the authored child Animator from motor movement.
            if (offset.magnitude < .05f)
            {
                motion.CancelMotion();
                retreating = false;
            }
            return;
        }
        if (stage == 2 && gunnerWasHit)
        {
            Vector2 destination = (Vector2)gunnerRetreatPoint.position + new Vector2(gunnerWalkSide * 1.5f, .5f);
            Vector2 offset = destination - (Vector2)tutorialGunner.transform.position;
            if (offset.magnitude < .15f) gunnerWalkSide = -gunnerWalkSide;
            float speed = tutorialGunner.GetComponent<IStatProvider>().Get(StatId.MoveSpeedFinal) * .65f;
            tutorialGunner.GetComponent<AbilityMotionController2D>().StartDash(offset,
                Mathf.Min(speed, offset.magnitude / .1f), .1f);
            return;
        }
        if (stage != 1 || dashIntro == DashIntroPhase.Dashing || gunnerVolleyStarted) return;
        if (bulletFireStart != null &&
            (player.transform.position.y < bulletFireStart.position.y ||
             Mathf.Abs(player.transform.position.x - bulletFireStart.position.x) > .75f)) return;
        int index = Array.FindIndex(bulletConsumed, consumed => consumed);
        if (index < 0) return;
        gunnerVolleyStarted = true;
        if (dodgeEntranceDoor != null) dodgeEntranceDoor.ForceClose();
        bulletConsumed[index] = false;
        bullets[index].position = tutorialGunner.transform.position + Vector3.down * .65f;
        bullets[index].gameObject.SetActive(true);
        CommonMonsterCombatUtility.TriggerAnimation(tutorialGunner, CommonMonsterAnimationCue.Attack);
        CapstoneAudio.SoundPlaybackUtility.Play(CapstoneAudio.SoundRef.FromKey("sound_goblinGunner_GunShot"),
            causer: tutorialGunner.gameObject, position: tutorialGunner.transform.position, sourceObject: this);
    }

    private void SpawnSkillMonster(int i)
    {
        if (skillMonsters[i] != null) skillMonsters[i].DeathStarted -= OnSkillMonsterDeath;
        GoblinWarrior monster = Instantiate(skillMonsterPrefab, targetPositions[i], Quaternion.identity, transform);
        monster.SuppressMonsterLootDrop();
        monster.ApplySpawnIdlePause(.25f);
        skillMonsters[i] = monster;
        skillTargets[i] = monster.transform;
        targetAttributes[i] = monster.GetComponent<AttributeSet>();
        defeated[i] = creditedKills[i] = false;
        monster.DeathStarted += OnSkillMonsterDeath;
        spawnedMonsters.RemoveAll(item => item == null);
        spawnedMonsters.Add(monster.gameObject);
    }

    private void OnSkillMonsterDeath(Enemy monster)
    {
        int i = Array.IndexOf(skillMonsters, monster);
        if (i < 0) return;
        defeated[i] = true;
        respawnAt[i] = Time.time + 1f;
    }

    private void ResetTarget(int i)
    {
        defeated[i] = creditedKills[i] = false;
        skillTargets[i].position = targetPositions[i];
        AttributeSet attributes = targetAttributes[i];
        if (attributes != null && damageEffect != null && damageEffect.healthAttribute != null)
            attributes.TrySetCurrentValue(damageEffect.healthAttribute,
                attributes.GetAttributeValue(maxHealthAttribute), this);
        skillTargets[i].gameObject.SetActive(true);
    }

    // Normalize only the tutorial gunner projectile width; vertical reach stays unchanged.
    private Vector2 BulletContactOffset(Vector2 offset)
    {
        if (tutorialGunner != null) offset.x *= .5f;
        return offset;
    }

    private void TickBullets()
    {
        Vector2 currentPlayer = player.transform.position;
        bool invulnerable = CombatInvulnerabilityUtil.IsDamageSuppressed(player.gameObject, damageEffect);
        bool dashProtected = (dashToken != null && !dashToken.IsCancelled && invulnerable) ||
            (dashEndFrame == Time.frameCount && dash.sourceObject is UnityGAS.Sample.Dash2DData dashData &&
             dashData.invulnerableTag != null && dashData.invulnerableTag == damageEffect.invulnerableTag);
        for (int i = 0; i < bullets.Length; i++)
        {
            if (tutorialGunner != null && bulletConsumed[i]) continue;
            Transform bullet = bullets[i];
            Vector2 from = bullet.position;
            Vector2 to = dashIntro == DashIntroPhase.Dashing
                ? from : from + Vector2.down * (3f * Time.deltaTime);
            if (dashIntro == DashIntroPhase.ZoomIn || dashIntro == DashIntroPhase.Waiting)
            {
                // Clamp travel before the safety boundary, including a long frame during the intro.
                if (Mathf.Abs(BulletContactOffset(from - currentPlayer).x) <= .48f && from.y >= currentPlayer.y)
                {
                    float dx = BulletContactOffset(from - currentPlayer).x;
                    float safeY = currentPlayer.y + Mathf.Sqrt(IntroStopDistance * IntroStopDistance - dx * dx);
                    to.y = Mathf.Min(from.y, Mathf.Max(to.y, safeY));
                }
            }
            if (to.y < bulletBottom.position.y)
            {
                if (tutorialGunner != null)
                {
                    bulletConsumed[i] = true;
                    bullet.gameObject.SetActive(false);
                    continue;
                }
                to.y = bulletTop.position.y;
                from = to;
                bulletConsumed[i] = false;
            }
            bullet.position = to;
            bullet.gameObject.SetActive(!bulletConsumed[i]);
            if (bulletConsumed[i] || !PrototypeTutorialRules.SweptContact(
                    BulletContactOffset(from - previousPlayerPosition), BulletContactOffset(to - currentPlayer), .48f)) continue;
            if (dashProtected)
            {
                if (!evadedBullet)
                    DamagePopupPlayback.Show(DamagePopupRequest.Text("Evade", player.transform.position, isPlayerTarget: true));
                evadedBullet = true;
                continue;
            }
            if (invulnerable || dashIntro == DashIntroPhase.ZoomIn || dashIntro == DashIntroPhase.Waiting) continue;
            bulletConsumed[i] = true;
            bullet.gameObject.SetActive(false);
            if (Time.time >= nextDamageTime)
            {
                nextDamageTime = Time.time + .5f;
                HazardDamageAction.ApplyDamage(player, player.gameObject, damageEffect, 1f, gameObject, this,
                    ignoreEvasion: true);
            }
        }
    }

    private void TickDashIntro()
    {
        if (dashIntro == DashIntroPhase.Ready)
        {
            if (stage != 1 || evadedBullet || player.IsBusy || dashToken != null ||
                player.GetCooldownRemaining(dash) > 0f || TimeScalePausePlayback.IsPaused) return;
            for (int i = 0; i < bullets.Length; i++)
            {
                Vector2 offset = BulletContactOffset(bullets[i].position - player.transform.position);
                if (!bulletConsumed[i] && offset.y >= 0f &&
                    offset.magnitude <= IntroApproachDistance && Mathf.Abs(offset.x) <= .48f)
                {
                    BeginDashIntro();
                    break;
                }
            }
            return;
        }

        if (dashIntro == DashIntroPhase.ZoomIn && TimeScalePausePlayback.IsPaused) return;
        introElapsed += Time.unscaledDeltaTime;
        if (dashIntro == DashIntroPhase.ZoomIn)
        {
            float distance = IntroApproachDistance;
            for (int i = 0; i < bullets.Length; i++)
                if (!bulletConsumed[i]) distance = Mathf.Min(distance, BulletContactOffset(bullets[i].position - player.transform.position).magnitude);
            introTimeScale = distance <= IntroStopDistance + .01f ? 0f :
                Mathf.Max(.08f, .6f * Mathf.InverseLerp(IntroStopDistance, IntroApproachDistance, distance));
            TimeScalePausePlayback.SetOwnedTimeScale(this, introTimeScale);
            SetIntroZoom(zoomFrom, zoomFrom * .65f, introElapsed / .9f);
            if (introTimeScale == 0f) dashIntro = DashIntroPhase.Waiting;
            TryConfirmDash();
        }
        else if (dashIntro == DashIntroPhase.Waiting)
        {
            SetIntroZoom(zoomFrom, zoomFrom * .65f, introElapsed / .9f);
            TryConfirmDash();
        }
        else if (dashIntro == DashIntroPhase.Dashing)
        {
            if (TimeScalePausePlayback.IsPaused) return;
            // Keep bullets still through the real GAS dash, including its final swept sample.
            if (dashToken != null || dashEndFrame == Time.frameCount) return;
            dashIntro = DashIntroPhase.ZoomOut;
            introElapsed = 0f;
            zoomReturnFrom = focus?.CurrentOrthographicSize ?? zoomFrom;
            TimeScalePausePlayback.Release(this);
        }
        else if (dashIntro == DashIntroPhase.ZoomOut)
        {
            SetIntroZoom(zoomReturnFrom, zoomFrom, introElapsed / .25f);
            if (introElapsed >= .25f) CleanupDashIntro();
        }
    }

    private void BeginDashIntro()
    {
        introTimeScale = .6f;
        if (!TimeScalePausePlayback.SetOwnedTimeScale(this, introTimeScale)) return;
        dashIntro = DashIntroPhase.ZoomIn;
        introElapsed = 0f;
        dashKeyHeld = InputActionQuery.IsPressed(InputActionId.Dash);
        inputBlocker = GameFlowInputBlocker.GetOrAdd(this);
        inputBlocker.Acquire();
        combatInput = player.GetComponent<PlayerCombatInput2D>();
        combatInput?.SetWeaponInputBlocked(this, true);
        InputActionQuery.SetPressBlocked(InputActionId.Dash, this, true);
        InputActionQuery.SetPressBlocked(InputActionId.SwapWeapon, this, true);
        focus = GameplayCameraFocusPlayback.Capture(this);
        zoomFrom = focus?.CachedOrthographicSize ?? 0f;
        focus?.SetTarget(player.transform);
        focus?.SnapToTarget(player.transform);
        previousPlayerPosition = player.transform.position;
    }

    private void TryConfirmDash()
    {
        bool held = InputActionQuery.IsPressed(InputActionId.Dash);
        bool pressed = held && !dashKeyHeld;
        dashKeyHeld = held;
        if (!pressed) return;
        TimeScalePausePlayback.SetOwnedTimeScale(this, .15f);
        // An unrelated full pause still wins.
        if (TimeScalePausePlayback.IsPaused)
        {
            TimeScalePausePlayback.SetOwnedTimeScale(this, introTimeScale);
            return;
        }
        // The UI flow block also applies control tags: release our block before real GAS activation.
        inputBlocker?.Release();
        if (!player.TryActivateAbility(dash))
        {
            inputBlocker?.Acquire();
            TimeScalePausePlayback.SetOwnedTimeScale(this, introTimeScale);
            return;
        }
        dashIntro = DashIntroPhase.Dashing;
        introElapsed = 0f;
    }

    private void SetIntroZoom(float from, float to, float t)
    {
        if (focus != null && focus.HasOrthographicSize)
            focus.SetOrthographicSize(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
    }

    private void CleanupDashIntro()
    {
        if (dashIntro == DashIntroPhase.Ready) return;
        if (TimeScalePausePlayback.IsHeldBy(this)) TimeScalePausePlayback.Release(this);
        focus?.Restore(null);
        focus = null;
        inputBlocker?.Release();
        combatInput?.SetWeaponInputBlocked(this, false);
        combatInput = null;
        InputActionQuery.SetPressBlocked(InputActionId.Dash, this, false);
        InputActionQuery.SetPressBlocked(InputActionId.SwapWeapon, this, false);
        dashIntro = DashIntroPhase.Done;
    }

    private void Advance()
    {
        stage++;
        if (stage == 2 && tutorialGunner != null) retreating = true;
        if (stage == 3 && skillMonsterPrefab != null)
            for (int i = 0; i < skillTargets.Length; i++) SpawnSkillMonster(i);
        if (stage == 4)
        {
            foreach (Enemy monster in skillMonsters) if (monster != null && !monster.IsDead) monster.RequestDeath();
            ReleaseChestLock();
            chargeCooldown?.Dispose();
            thrustCooldown?.Dispose();
            chargeCooldown = thrustCooldown = null;
            if (healthRecovery != null) healthRecovery.enabled = false;
        }
        RefreshPresentation();
    }

    private void RefreshPresentation()
    {
        bool showChest = stage <= 4 && (chestLock != null || stage == 4);
        if (chestPractice != null && chestPractice.activeSelf != showChest)
            chestPractice.SetActive(showChest);
        for (int i = 0; i < gates.Length; i++) gates[i].SetActive(i == 3 ? stage < 6 : stage <= i);
        if (stage != 1)
            foreach (Transform bullet in bullets) bullet.gameObject.SetActive(false);
        string message = stage switch
        {
            0 => "몬스터를 쫓아가자",
            1 => IsDashPromptVisible ? "대시로 공격을 피하세요." : "앞으로 이동하세요.",
            2 => "좌클릭" + PromptInstruction,
            3 => (chargeKills < 4 ? "우클릭" : "Q") + PromptInstruction,
            4 => "상자를 열고 아이템 선택 후 획득하기",
            5 => "인벤토리를 열어 획득한 아이템 확인하기",
            _ => "위쪽 포탈로 이동하기"
        };
        if (message == lastProgress) return;
        lastProgress = message;
        onProgressChanged.Invoke(message);
    }

    public void CompleteChestTutorial()
    {
        if (stage == 4) Advance();
    }

    public void CompleteInventoryTutorial()
    {
        if (stage == 5) Advance();
    }

    private void OpenBossPortalDoor(TreasureChest chest)
    {
        if (chest == portalDoorChest && bossPortalDoor != null)
            bossPortalDoor.ForceOpen(save: false);
    }
}

// Scene-authored opening only. The mission director retains all objective/progress ownership.
[Serializable]
public sealed class PrototypeTutorialOpeningSequence
{
    [SerializeField] private bool enabled;
    [SerializeField] private Transform gunnerVisual;
    [SerializeField] private MonoBehaviour gunnerSpeech;
    [SerializeField] private Vector3 gunnerStart;
    [SerializeField] private Vector3[] escapePoints = Array.Empty<Vector3>();
    [SerializeField, Min(.1f)] private float escapeSpeed = 6f;
    [SerializeField] private Transform openingCameraPoint;

    [NonSerialized] private bool completed;
    [NonSerialized] private bool landed;
    [NonSerialized] private bool canWake;
    [NonSerialized] private bool capturedVisual;
    [NonSerialized] private Vector3 visualPosition;
    [NonSerialized] private PlayerHubSpawnPresentation2D arrival;
    [NonSerialized] private PlayerCinematicProtection protection;
    [NonSerialized] private ICinematicLetterboxOverlayHandle letterbox;
    [NonSerialized] private GoblinGunner gunner;
    [NonSerialized] private GoblinGunnerShotRunner shot;
    [NonSerialized] private AbilityMotionController2D motion;
    [NonSerialized] private bool restoreGunnerEnabled;
    [NonSerialized] private LightBeadProjectile2D openingProjectile;
    [NonSerialized] private GameFlowInputBlocker openingInputBlocker;
    [NonSerialized] private IGameplayCameraFocusSession openingCamera;
    [NonSerialized] private Transform playerTransform;
    [NonSerialized] private MonoBehaviour coroutineHost;
    [NonSerialized] private Coroutine letterboxExit;
    [NonSerialized] private bool escaping;

    public bool IsPending => enabled && !completed;
    public bool IsEscaping => escaping;

    public System.Collections.IEnumerator Play(MonoBehaviour host, AbilitySystem player, GoblinGunner actor)
    {
        // Registration occurs inside the spawner; let spawn/restore and actor Start finish first.
        yield return null;
        arrival = player != null ? player.GetComponent<PlayerHubSpawnPresentation2D>() : null;
        protection = player != null ? player.GetComponent<PlayerCinematicProtection>() : null;
        gunner = actor;
        shot = gunner != null ? gunner.GetComponent<GoblinGunnerShotRunner>() : null;
        motion = gunner != null ? gunner.GetComponent<AbilityMotionController2D>() : null;
        if (arrival == null || protection == null || shot == null || motion == null ||
            gunnerVisual == null || gunnerSpeech is not ISpeechBubblePlayback || escapePoints.Length == 0)
        {
            Debug.LogError("[TutorialOpening] Missing authored arrival, gunner, speech or escape path.", host);
            completed = true;
            Cancel();
            yield break;
        }

        landed = canWake = false;
        coroutineHost = host;
        playerTransform = player.transform;
        visualPosition = gunnerVisual.localPosition;
        capturedVisual = true;
        gunner.transform.position = gunnerStart;
        Rigidbody2D body = gunner.GetComponent<Rigidbody2D>();
        if (body != null) body.position = gunnerStart;
        try
        {
            openingInputBlocker = GameFlowInputBlocker.GetOrAdd(host);
            openingInputBlocker.Acquire();
            letterbox = CinematicLetterboxPlayback.CreateOverlay();
            yield return letterbox.PlayIn(0f, .14f, 0f);
            // Editor direct-start normalization can release protection during letterbox startup.
            // Acquire only after that yielding boundary, immediately before arrival captures input states.
            protection.Acquire(this);
            if (openingCameraPoint != null)
            {
                openingCamera = GameplayCameraFocusPlayback.Capture(host);
                openingCamera?.SetTarget(openingCameraPoint);
                openingCamera?.SnapToTarget(openingCameraPoint);
            }
            if (!arrival.TryPlayScriptedArrival(this, () => landed = true, () => canWake,
                    useExternalCamera: openingCameraPoint != null))
            {
                Debug.LogError("[TutorialOpening] Player arrival is already owned by another sequence.", host);
                completed = true;
                yield break;
            }
            while (!landed && arrival.IsPortalArrivalPlaying(this)) yield return null;
            if (!landed) yield break;

            yield return PlaySurprise();

            // Idle-pause suppression cancels runners. Suspend only the AI component for this single shot.
            restoreGunnerEnabled = gunner.enabled;
            gunner.enabled = false;
            CommonMonsterCombatUtility.TriggerAnimation(gunner, CommonMonsterAnimationCue.AttackReady);
            yield return shot.Run(gunner.GetComponent<AbilitySystem>(), null, player.gameObject, projectile =>
            {
                openingProjectile = projectile;
                projectile.BindPresentationImpact(player.gameObject, () => canWake = true);
            });
            CommonMonsterCombatUtility.TriggerAnimation(gunner, CommonMonsterAnimationCue.Recover);
            ((ISpeechBubblePlayback)gunnerSpeech).HideActive();
            while (!canWake && openingProjectile != null) yield return null;
            if (!canWake)
            {
                Debug.LogError("[TutorialOpening] Opening shot missed the player; check the authored firing lane.", host);
                completed = true;
                yield break;
            }
            // Arrival restores the upright pose, while this sequence still owns control protection.
            while (arrival.IsPortalArrivalPlaying(this)) yield return null;
            player.GetComponent<PlayerInteractor2D>()?.SetInteractState(InteractState.None);
            yield return PlaySurprise();
            gunner.enabled = restoreGunnerEnabled;
            restoreGunnerEnabled = false;
            ((ISpeechBubblePlayback)gunnerSpeech).HideActive();

            escaping = true;
            float totalDistance = 0f;
            Vector2 previousPoint = gunner.transform.position;
            foreach (Vector3 point in escapePoints)
            {
                totalDistance += Vector2.Distance(previousPoint, point);
                previousPoint = point;
            }
            float completedDistance = 0f;
            foreach (Vector3 destination in escapePoints)
            {
                float stalledSeconds = 0f;
                float bestDistance = Vector2.Distance(gunner.transform.position, destination);
                float segmentLength = bestDistance;
                while (Vector2.Distance(gunner.transform.position, destination) > .06f)
                {
                    Vector2 offset = destination - gunner.transform.position;
                    motion.StartDash(offset, Mathf.Min(escapeSpeed, offset.magnitude / .1f), .1f);
                    float distance = offset.magnitude;
                    if (!completed && completedDistance + Mathf.Max(0f, segmentLength - distance) >= totalDistance * .7f)
                    {
                        ReleaseCinematicControl();
                        letterboxExit = host.StartCoroutine(FadeOutLetterbox());
                    }
                    if (distance < bestDistance - .02f) { bestDistance = distance; stalledSeconds = 0f; }
                    else stalledSeconds += Time.deltaTime;
                    if (stalledSeconds > 3f)
                    {
                        Debug.LogError("[TutorialOpening] Escape path is blocked; check authored waypoints and walls.", host);
                        completed = true;
                        yield break;
                    }
                    yield return null;
                }
                motion.CancelMotion();
                completedDistance += segmentLength;
            }
            escaping = false;
            if (!completed)
            {
                ReleaseCinematicControl();
                letterboxExit = host.StartCoroutine(FadeOutLetterbox());
            }
            if (letterboxExit != null) yield return letterboxExit;
        }
        finally
        {
            Cancel();
        }
    }

    private void ReleaseCinematicControl()
    {
        completed = true;
        openingCamera?.Restore(playerTransform);
        openingCamera = null;
        openingInputBlocker?.Release();
        openingInputBlocker = null;
        if (protection != null) protection.Release(this);
        protection = null;
    }

    private System.Collections.IEnumerator FadeOutLetterbox()
    {
        yield return letterbox.PlayOut(.25f);
        letterbox.Dispose();
        letterbox = null;
        letterboxExit = null;
    }

    private System.Collections.IEnumerator PlaySurprise()
    {
        ((ISpeechBubblePlayback)gunnerSpeech).Speak("!", .85f);
        for (float elapsed = 0f; elapsed < .4f; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / .4f);
            gunnerVisual.localPosition = visualPosition + Vector3.up * (4f * t * (1f - t) * .45f);
            yield return null;
        }
        gunnerVisual.localPosition = visualPosition;
        yield return new WaitForSeconds(.2f);
    }

    public void Cancel()
    {
        escaping = false;
        if (letterboxExit != null && coroutineHost != null) coroutineHost.StopCoroutine(letterboxExit);
        letterboxExit = null;
        if (openingProjectile != null)
        {
            openingProjectile.BindPresentationImpact(null, null);
            UnityEngine.Object.Destroy(openingProjectile.gameObject);
        }
        openingProjectile = null;
        shot?.Cancel();
        motion?.CancelMotion();
        if (gunner != null && restoreGunnerEnabled) gunner.enabled = true;
        restoreGunnerEnabled = false;
        if (capturedVisual && gunnerVisual != null) gunnerVisual.localPosition = visualPosition;
        capturedVisual = false;
        if (gunnerSpeech is ISpeechBubblePlayback speech) speech.HideActive();
        // Arrival restores its input snapshot before the outer cinematic owner restores normal controls.
        if (arrival != null) arrival.CancelPortalArrival(this);
        openingCamera?.Restore(playerTransform);
        openingCamera = null;
        openingInputBlocker?.Release();
        openingInputBlocker = null;
        if (protection != null) protection.Release(this);
        letterbox?.Dispose();
        letterbox = null;
        arrival = null;
        protection = null;
        shot = null;
        motion = null;
        gunner = null;
    }
}

internal static class PrototypeTutorialRules
{
    internal static bool SweptContact(Vector2 from, Vector2 to, float radius)
    {
        Vector2 delta = to - from;
        float t = delta.sqrMagnitude > .000001f ? Mathf.Clamp01(-Vector2.Dot(from, delta) / delta.sqrMagnitude) : 0f;
        return (from + delta * t).sqrMagnitude <= radius * radius;
    }
}
