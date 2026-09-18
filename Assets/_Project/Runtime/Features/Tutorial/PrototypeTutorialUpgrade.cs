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
    private AbilityCancellationToken heldAttack;
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
    public string ProgressText => lastProgress;
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
        if (stage < 4)
        {
            chargeCooldown = player.AddScopedCooldownDurationMultiplier(
                definition => definition == chargeSkill, 1f / Mathf.Max(1f, chargeSkill.cooldown));
            thrustCooldown = player.AddScopedCooldownDurationMultiplier(
                definition => definition == thrustSkill, 1f / Mathf.Max(1f, thrustSkill.cooldown));
        }
    }

    private void UnbindPlayer(PlayerInteractor2D registered)
    {
        if (player != null && registered != null && player.gameObject == registered.gameObject) ReleasePlayer();
    }

    private void ReleasePlayer()
    {
        CleanupDashIntro();
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
        heldAttack = countedAttack = dashToken = null;
        dashEndFrame = -1;
    }

    private void OnExecutionStarted(AbilitySpec spec)
    {
        if (spec.Definition == dash) dashToken = spec.Token;
        if (spec.Definition == basicAttack)
            heldAttack = InputActionQuery.IsPressed(InputActionId.PrimaryAttack) ? spec.Token : null;
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
        if (player == null) return;
        TickDashIntro();
        if (TimeScalePausePlayback.IsPaused || Time.deltaTime <= 0f) return;
        TickGunner();
        if (stage == 0 && AtGoal(movementGoal)) Advance();
        if (stage == 1)
        {
            TickBullets();
            if (AtGunnerFront()) Advance();
        }
        if (stage == 2 && !InputActionQuery.IsPressed(InputActionId.PrimaryAttack))
        {
            heldAttack = null;
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
        bool valid = skillMonsterPrefab != null
            ? (ability == chargeSkill && IsFullCharge(player.FindSpec(chargeSkill))) || ability == thrustSkill
            : index < 4 ? chargeKills < 4 && ability == chargeSkill && IsFullCharge(player.FindSpec(chargeSkill))
            : thrustKills < 3 && ability == thrustSkill;
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
            if (token == null || token == countedAttack || token != heldAttack) return;
            countedAttack = token;
            // Commit a whole combo at its final confirmed hit, not at animation completion.
            attackHits = PrototypeTutorialRules.CountComboHit(attackHits,
                data.Spec.GetInt("Combat.HitFeelIndex", -1), holding: true);
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
            bool valid = skillMonsterPrefab != null ? charged || data.Spec.Definition == thrustSkill
                : i < 4 ? charged : data.Spec.Definition == thrustSkill;
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
            if (dashProtected) { evadedBullet = true; continue; }
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
        if (chestPractice != null && chestPractice.activeSelf != (chestLock != null || stage == 4))
            chestPractice.SetActive(chestLock != null || stage == 4);
        for (int i = 0; i < gates.Length; i++) gates[i].SetActive(i == 3 ? stage < 5 : stage <= i);
        if (stage != 1)
            foreach (Transform bullet in bullets) bullet.gameObject.SetActive(false);
        string message = stage switch
        {
            0 => "길을 따라 마왕성에 진입하자.",
            1 => "회피하며 전진하자.",
            2 => $"좌클릭 홀드로 3타 콤보 × 3회: {attackHits}/9\n마지막 3타 명중 시 카운트 +3",
            3 => $"재사용 약 1초\n우클릭 1초 충전 후 놓기: {chargeKills}/4\nQ 스킬: {thrustKills}/3",
            4 => "상자를 열고 아이템 선택 후 확정하기",
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

    private void OpenBossPortalDoor(TreasureChest chest)
    {
        if (chest == portalDoorChest && bossPortalDoor != null)
            bossPortalDoor.ForceOpen(save: false);
    }
}

internal static class PrototypeTutorialRules
{
    internal static int CountComboHit(int hits, int comboIndex, bool holding)
    {
        return holding && comboIndex == 2 ? hits + 3 : hits;
    }

    internal static bool SweptContact(Vector2 from, Vector2 to, float radius)
    {
        Vector2 delta = to - from;
        float t = delta.sqrMagnitude > .000001f ? Mathf.Clamp01(-Vector2.Dot(from, delta) / delta.sqrMagnitude) : 0f;
        return (from + delta * t).sqrMagnitude <= radius * radius;
    }
}
