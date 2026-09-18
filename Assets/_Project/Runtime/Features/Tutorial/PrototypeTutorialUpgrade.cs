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
        targetAttributes = new AttributeSet[skillTargets.Length];
        targetPositions = new Vector3[skillTargets.Length];
        defeated = new bool[skillTargets.Length];
        respawnAt = new float[skillTargets.Length];
        for (int i = 0; i < skillTargets.Length; i++)
        {
            targetAttributes[i] = skillTargets[i].GetComponent<AttributeSet>();
            targetPositions[i] = skillTargets[i].position;
        }
        bulletPositions = new Vector3[bullets.Length];
        bulletConsumed = new bool[bullets.Length];
        for (int i = 0; i < bullets.Length; i++) bulletPositions[i] = bullets[i].position;
    }

    private void OnEnable()
    {
        stage = attackHits = chargeKills = thrustKills = 0;
        evadedBullet = false;
        nextDamageTime = 0f;
        lastProgress = null;
        for (int i = 0; i < skillTargets.Length; i++) ResetTarget(i);
        for (int i = 0; i < bullets.Length; i++)
        {
            bullets[i].position = bulletPositions[i];
            bulletConsumed[i] = false;
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
        PlayerRuntimeRegistry.PlayerRegistered -= BindPlayer;
        PlayerRuntimeRegistry.PlayerUnregistered -= UnbindPlayer;
        ReleasePlayer();
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
        if (stage == 0 && AtGoal(movementGoal)) Advance();
        if (stage == 1)
        {
            TickBullets();
            if (evadedBullet && AtGoal(dashGoal)) Advance();
        }
        if (stage == 2 && !InputActionQuery.IsPressed(InputActionId.PrimaryAttack))
        {
            attackHits = PrototypeTutorialRules.CompleteTriples(attackHits);
            heldAttack = null;
        }
        for (int i = 0; i < skillTargets.Length; i++)
        {
            if (!defeated[i]) continue;
            skillTargets[i].gameObject.SetActive(false);
            bool groupComplete = i < 4 ? chargeKills >= 4 : thrustKills >= 3;
            if (stage == 3 && !groupComplete && Time.time >= respawnAt[i]) ResetTarget(i);
        }
        previousPlayerPosition = player.transform.position;
        RefreshPresentation();
    }

    private bool AtGoal(Transform goal)
    {
        Vector2 offset = player.transform.position - goal.position;
        return Mathf.Abs(offset.x) < 1.8f && offset.y >= 0f;
    }

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
        if (ownAttackTarget) return stage == 2 && ability == basicAttack ? context.BaseDamage : 0f;
        if (stage != 3 || defeated[index]) return 0f;
        bool valid = index < 4
            ? chargeKills < 4 && ability == chargeSkill && IsFullCharge(player.FindSpec(chargeSkill))
            : thrustKills < 3 && ability == thrustSkill;
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
        if (stage == 2 && data.Target == attackTarget.gameObject && data.Spec.Definition == basicAttack)
        {
            AbilityCancellationToken token = data.Spec.Token;
            if (token == null || token == countedAttack || token != heldAttack) return;
            countedAttack = token;
            attackHits = PrototypeTutorialRules.CountComboHit(attackHits,
                data.Spec.GetInt("Combat.HitFeelIndex", -1), InputActionQuery.IsPressed(InputActionId.PrimaryAttack));
            if (attackHits >= 9) Advance();
        }
        else if (stage == 3)
        {
            int i = Array.FindIndex(skillTargets, t => t != null && t.gameObject == data.Target);
            if (i < 0 || defeated[i] || targetAttributes[i].GetAttributeValue(damageEffect.healthAttribute) > 0f) return;
            bool valid = i < 4 ? IsFullCharge(data.Spec) : data.Spec.Definition == thrustSkill;
            if (!valid) return;
            defeated[i] = true;
            respawnAt[i] = Time.time + 1f;
            if (i < 4) chargeKills = Mathf.Min(4, chargeKills + 1);
            else thrustKills = Mathf.Min(3, thrustKills + 1);
            if (chargeKills == 4 && thrustKills == 3) Advance();
        }
        RefreshPresentation();
    }

    private void ResetTarget(int i)
    {
        defeated[i] = false;
        skillTargets[i].position = targetPositions[i];
        AttributeSet attributes = targetAttributes[i];
        if (attributes != null && damageEffect != null && damageEffect.healthAttribute != null)
            attributes.TrySetCurrentValue(damageEffect.healthAttribute,
                attributes.GetAttributeValue(maxHealthAttribute), this);
        skillTargets[i].gameObject.SetActive(true);
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
            Transform bullet = bullets[i];
            Vector2 from = bullet.position;
            Vector2 to = dashIntro == DashIntroPhase.Dashing
                ? from : from + Vector2.down * (3f * Time.deltaTime);
            if (to.y < bulletBottom.position.y)
            {
                to.y = bulletTop.position.y;
                from = to;
                bulletConsumed[i] = false;
            }
            bullet.position = to;
            bullet.gameObject.SetActive(!bulletConsumed[i]);
            if (bulletConsumed[i] || !PrototypeTutorialRules.SweptContact(
                    from - previousPlayerPosition, to - currentPlayer, .48f)) continue;
            bulletConsumed[i] = true;
            bullet.gameObject.SetActive(false);
            if (dashProtected) evadedBullet = true;
            else if (!invulnerable && Time.time >= nextDamageTime)
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
                Vector2 offset = bullets[i].position - player.transform.position;
                if (!bulletConsumed[i] && offset.y > .48f && offset.y <= 3.4f && Mathf.Abs(offset.x) <= .48f)
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
            introTimeScale = introElapsed >= 2.4f ? 0f :
                Mathf.Max(.001f, .6f * (1f - Mathf.SmoothStep(0f, 1f, introElapsed / 2.4f)));
            TimeScalePausePlayback.SetOwnedTimeScale(this, introTimeScale);
            SetIntroZoom(zoomFrom, zoomFrom * .65f, introElapsed / .9f);
            if (introElapsed >= 2.4f) dashIntro = DashIntroPhase.Waiting;
            TryConfirmDash();
        }
        else if (dashIntro == DashIntroPhase.Waiting)
        {
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
        if (stage == 4)
        {
            chargeCooldown?.Dispose();
            thrustCooldown?.Dispose();
            chargeCooldown = thrustCooldown = null;
            if (healthRecovery != null) healthRecovery.enabled = false;
        }
        RefreshPresentation();
    }

    private void RefreshPresentation()
    {
        if (chestPractice != null && chestPractice.activeSelf != (stage == 4))
            chestPractice.SetActive(stage == 4);
        for (int i = 0; i < gates.Length; i++) gates[i].SetActive(i == 3 ? stage < 5 : stage <= i);
        if (stage != 1)
            foreach (Transform bullet in bullets) bullet.gameObject.SetActive(false);
        string message = stage switch
        {
            0 => "1/5 이동\n위쪽 표시 지점에 도착하기",
            1 => evadedBullet ? "2/5 무적 회피 완료!\n위쪽 출구로 이동하기" : "2/5 대쉬\n탄막을 향해 대쉬로 통과하기",
            2 => $"3/5 일반 공격 · {attackHits}/9\n좌클릭 홀드로 3타 콤보 × 3회\n3타 전 해제 시 콤보 초기화",
            3 => $"4/5 스킬 · 재사용 약 1초\n우클릭 1초 충전 후 놓기: {chargeKills}/4\nQ 스킬: {thrustKills}/3",
            4 => "5/5 상자\n상자를 열고 아이템 선택 후 확정하기",
            _ => "마왕에게 도전\n위쪽 포탈로 이동하기"
        };
        if (message == lastProgress) return;
        lastProgress = message;
        onProgressChanged.Invoke(message);
    }

    public void CompleteChestTutorial()
    {
        if (stage == 4) Advance();
    }
}

internal static class PrototypeTutorialRules
{
    internal static int CompleteTriples(int hits) => hits / 3 * 3;

    internal static int CountComboHit(int hits, int comboIndex, bool holding)
    {
        if (!holding) return CompleteTriples(hits);
        if (comboIndex == hits % 3) return hits + 1;
        return CompleteTriples(hits) + (comboIndex == 0 ? 1 : 0);
    }

    internal static bool SweptContact(Vector2 from, Vector2 to, float radius)
    {
        Vector2 delta = to - from;
        float t = delta.sqrMagnitude > .000001f ? Mathf.Clamp01(-Vector2.Dot(from, delta) / delta.sqrMagnitude) : 0f;
        return (from + delta * t).sqrMagnitude <= radius * radius;
    }
}
