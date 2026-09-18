// Run in an isolated Unity project with fresh project DLLs and their dependency closure.
// -batchmode -nographics -executeMethod PrototypeTutorialNativeRegression.Run
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityGAS;

public static class PrototypeTutorialNativeRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object o, string n, object v) => o.GetType().GetField(n, Private).SetValue(o, v);
    private static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, Private).Invoke(o, a);
    private static T Get<T>(object o, string n) => (T)o.GetType().GetField(n, Private).GetValue(o);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    public static void RunCombo()
    {
        try
        {
            Rules();
            GunnerCadenceAndRetreat();
            Debug.Log("TUTORIAL_COMBO_PASS: per-hit increment, duplicate-hit guard, post-hit cancellation, nine hits advance, gunner single-shot/retreat.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    public static void Run()
    {
        try
        {
            Rules();
            Check(!QuestHudView.ShouldHideForCombat("PrototypeTutorialUpgradeScene", true) &&
                !QuestHudView.ShouldHideForCombat("DarkLord_Tutorial", true) &&
                QuestHudView.ShouldHideForCombat("Grand Hall", true) &&
                !QuestHudView.ShouldHideForCombat("Grand Hall", false), "Only tutorial combat should retain mission HUD");
            DamageAndLifecycle();
            DashIntroLifecycle();
            SharedTimeScaleOwnership();
            RealMonsterWaveLifecycle();
            GunnerCadenceAndRetreat();
            Debug.Log("TUTORIAL_PROTOTYPE_PASS: missions; progressive slowdown/zoom, immediate prompt, early action input, stop/retry, frozen-bullet invulnerability, cleanup; shared time-scale ownership; four real warrior instances, delayed replacement, survivor death, chest unlock and portal gate order.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private static void RealMonsterWaveLifecycle()
    {
        var host = new GameObject("Real wave owner"); host.SetActive(false);
        var tutorial = host.AddComponent<PrototypeTutorialUpgrade>();
        var prefabHost = new GameObject("Warrior template"); prefabHost.SetActive(false);
        var prefab = prefabHost.AddComponent<GoblinWarrior>();
        var points = new Transform[4];
        for (int i = 0; i < 4; i++) points[i] = new GameObject("Spawn " + i).transform;
        var chestObject = new GameObject("Locked chest"); chestObject.SetActive(false);
        var chest = chestObject.AddComponent<ChestMonsterKillLock>();
        Set(tutorial, "skillMonsterPrefab", prefab);
        Set(tutorial, "skillSpawnPoints", points);
        Set(tutorial, "chestLock", chest);
        var gates = new GameObject[4];
        for (int i = 0; i < 4; i++) gates[i] = new GameObject("Real gate " + i);
        Set(tutorial, "gates", gates);
        Call(tutorial, "Awake");
        Call(tutorial, "OnEnable");
        Check(!chest.IsUnlocked, "Chest must be locked before combat");
        Set(tutorial, "stage", 2);
        Call(tutorial, "Advance");
        var wave = Get<Enemy[]>(tutorial, "skillMonsters");
        Check(wave.Length == 4 && Array.TrueForAll(wave, e => e is GoblinWarrior), "Expected four real warrior instances");
        Enemy first = wave[0];
        first.RequestDeath();
        Check(Get<bool[]>(tutorial, "defeated")[0], "Real death event did not schedule a replacement");
        Check(Get<float[]>(tutorial, "respawnAt")[0] >= Time.time + .99f, "Replacement is not delayed by one second");
        Call(tutorial, "TickSkillTargets");
        Check(wave[0] == first && !chest.IsUnlocked, "Dead slot respawned early or unlocked the chest");
        Get<float[]>(tutorial, "respawnAt")[0] = Time.time - .01f;
        Call(tutorial, "TickSkillTargets");
        Check(wave[0] != first && !wave[0].IsDead, "Dead warrior was reused instead of replaced");
        Call(tutorial, "Advance");
        Check(chest.IsUnlocked && Array.TrueForAll(wave, e => e.IsDead), "Skill completion must kill survivors and unlock chest");
        Enemy completed = wave[0];
        Get<float[]>(tutorial, "respawnAt")[0] = Time.time - .01f;
        Call(tutorial, "TickSkillTargets");
        Check(wave[0] == completed && gates[3].activeSelf, "Completed wave restarted or portal gate opened early");
        tutorial.CompleteChestTutorial();
        Check(!gates[3].activeSelf, "Chest tutorial did not open the portal room");
        Call(tutorial, "OnDisable");
    }

    private static void GunnerCadenceAndRetreat()
    {
        var host = new GameObject("Gunner tutorial"); host.SetActive(false);
        var tutorial = host.AddComponent<PrototypeTutorialUpgrade>();
        var gunnerHost = new GameObject("Tutorial gunner"); gunnerHost.SetActive(false);
        var gunner = gunnerHost.AddComponent<GoblinGunner>();
        var visual = new GameObject("Visual");
        visual.transform.SetParent(gunnerHost.transform);
        visual.AddComponent<Animator>();
        Check(gunnerHost.GetComponent<Animator>() == null, "Fixture must match the prefab's child-only Animator");
        var motion = gunnerHost.GetComponent<AbilityMotionController2D>();
        if (motion == null) motion = gunnerHost.AddComponent<AbilityMotionController2D>();
        var retreat = new GameObject("Retreat point").transform;
        gunnerHost.transform.position = new Vector3(0, 21, 0);
        retreat.position = new Vector3(0, 27, 0);
        var shots = new Transform[3];
        for (int i = 0; i < shots.Length; i++) shots[i] = new GameObject("Shot " + i).transform;
        Set(tutorial, "skillTargets", Array.Empty<Transform>());
        Set(tutorial, "gates", Array.Empty<GameObject>());
        Set(tutorial, "tutorialGunner", gunner);
        Set(tutorial, "gunnerRetreatPoint", retreat);
        Set(tutorial, "bullets", shots);
        Call(tutorial, "Awake"); Call(tutorial, "OnEnable");
        Set(tutorial, "stage", 1);
        Call(tutorial, "TickGunner");
        Check(Array.FindAll(Get<bool[]>(tutorial, "bulletConsumed"), c => !c).Length == 1, "Gunner must fire exactly one shot");
        Check(Vector3.Distance(shots[0].position, new Vector3(0, 20.35f, 0)) < .001f, "Shot must originate straight below the gunner");
        Call(tutorial, "TickGunner");
        Check(Get<bool[]>(tutorial, "bulletConsumed")[1], "Gunner fired a second shot");
        Get<bool[]>(tutorial, "bulletConsumed")[0] = true;
        Set(tutorial, "evadedBullet", true);
        Call(tutorial, "TickGunner");
        Check(Array.TrueForAll(Get<bool[]>(tutorial, "bulletConsumed"), c => c), "Consumed tutorial shot must not be recycled");
        Call(tutorial, "Advance"); Call(tutorial, "TickGunner");
        Vector2 velocity = motion.TickAndGetMotionVelocity(.02f);
        Check(Mathf.Abs(velocity.x) < .001f && Mathf.Abs(velocity.y - 4f) < .001f, "Retreat must use motor motion toward the authored point");
        gunnerHost.transform.position = retreat.position; Call(tutorial, "TickGunner");
        Check(!Get<bool>(tutorial, "retreating") && !motion.HasActiveMotion, "Retreat did not stop at its destination");
        var playerObject = new GameObject("Attack test player");
        var player = playerObject.AddComponent<AbilitySystem>();
        Call(player, "Awake");
        var basic = ScriptableObject.CreateInstance<AbilityDefinition>();
        var tag = ScriptableObject.CreateInstance<GameplayTag>();
        Set(tutorial, "player", player);
        player.transform.position = gunner.transform.position + Vector3.down * 2.5f;
        Set(tutorial, "evadedBullet", false);
        Check((bool)Call(tutorial, "AtGunnerFront"), "Arrival must not require a recorded invulnerable hit");
        player.transform.position = gunner.transform.position + Vector3.down * 3f;
        Check(!(bool)Call(tutorial, "AtGunnerFront"), "Distant player must not finish the approach mission");
        Set(tutorial, "attackTarget", gunnerHost.transform);
        Set(tutorial, "basicAttack", basic);
        Set(tutorial, "hitConfirmed", tag);
        var moveAttribute = ScriptableObject.CreateInstance<AttributeDefinition>();
        moveAttribute.defaultBaseValue = 4f;
        var catalog = ScriptableObject.CreateInstance<AttributeCatalogSO>();
        Set(catalog, "attributes", new[] { moveAttribute });
        Set(gunnerHost.GetComponent<AttributeSet>(), "attributeCatalog", catalog);
        Set(gunnerHost.GetComponent<AttributeSet>(), "_initialized", false);
        var moveBindings = ScriptableObject.CreateInstance<StatTypeBindings>();
        Set(moveBindings, "bindings", new List<StatTypeBindings.Binding> {
            new StatTypeBindings.Binding { id = StatId.MoveSpeedFinal, attribute = moveAttribute } });
        var stats = gunnerHost.GetComponent<AttributeStatSource>();
        Set(stats, "attributeSet", gunnerHost.GetComponent<AttributeSet>());
        Set(stats, "statBindingsOverride", moveBindings);
        var input = new InputFixture { BackendComponent = host.transform, PrimaryHeld = true };
        InputActionQuery.RegisterBackend(input);
        try
        {
            var spec = new AbilitySpec(basic);
            for (int i = 0; i < 9; i++)
            {
                typeof(AbilitySpec).GetProperty("Token").SetValue(spec, new AbilityCancellationToken());
                spec.SetInt("Combat.HitFeelIndex", i < 4 ? 0 : i % 3);
                Call(tutorial, "OnExecutionStarted", spec);
                var hit = new AbilityEventData { Spec = spec, Target = gunnerHost };
                Call(tutorial, "OnGameplayEvent", tag, hit);
                Call(tutorial, "OnGameplayEvent", tag, hit);
                Call(tutorial, "OnExecutionEnded", spec, true);
                Check(Get<int>(tutorial, "attackHits") == i + 1,
                    "Every hit counts once even when the combo restarts; duplicates and cancellation must not change it");
                if (i == 0)
                {
                    Call(tutorial, "TickGunner");
                    Check(Mathf.Abs(motion.TickAndGetMotionVelocity(.02f).magnitude - 2.6f) < .001f,
                        "First hit must start wandering at 65 percent of the normal speed");
                }
            }
            Check(tutorial.Stage == 3 && gunner.IsDead, "Nine held hits must kill the gunner and advance");
        }
        finally { InputActionQuery.UnregisterBackend(input); }
        Call(tutorial, "OnDisable");
    }

    private static void Rules()
    {
        Type rules = typeof(PrototypeTutorialUpgrade).Assembly.GetType("PrototypeTutorialRules");
        var swept = rules.GetMethod("SweptContact", BindingFlags.Static | BindingFlags.NonPublic);
        Check((bool)swept.Invoke(null, new object[] { new Vector2(0, -3), new Vector2(0, 3), .48f }), "Fast dash crossings must not tunnel");
        Check(!(bool)swept.Invoke(null, new object[] { new Vector2(2, -3), new Vector2(2, 3), .48f }), "Passing beside a bullet must not count");
    }

    private static void DamageAndLifecycle()
    {
        var hp = ScriptableObject.CreateInstance<AttributeDefinition>();
        var max = ScriptableObject.CreateInstance<AttributeDefinition>();
        var damage = ScriptableObject.CreateInstance<GE_Damage_Spec>();
        damage.healthAttribute = hp;
        damage.fallbackDamage = 10;
        damage.fallbackStunSeconds = 0;
        var basic = ScriptableObject.CreateInstance<AbilityDefinition>();
        var charge = ScriptableObject.CreateInstance<AbilityDefinition>();
        var thrust = ScriptableObject.CreateInstance<AbilityDefinition>();
        var chargeData = ScriptableObject.CreateInstance<ApprenticeHeroSwordChargeSpinData>();
        charge.sourceObject = chargeData;
        charge.cooldown = 6;
        thrust.cooldown = 3;
        var playerObject = new GameObject("Test player");
        var player = playerObject.AddComponent<AbilitySystem>();
        Call(player, "Awake");
        AbilitySpec chargeSpec = player.GiveAbility(charge);
        player.GiveAbility(thrust);
        var attackTarget = new GameObject("Attack target").transform;
        Transform[] targets = new Transform[7];
        for (int i = 0; i < targets.Length; i++)
        {
            targets[i] = new GameObject("Skill target " + i).transform;
            var attrs = targets[i].gameObject.AddComponent<AttributeSet>();
            Set(attrs, "maxLinks", new List<AttributeSet.MaxLink> { new AttributeSet.MaxLink { value = hp, max = max, fillToMaxOnInitialize = true } });
            Set(attrs, "_initialized", false);
            attrs.InitializeFromInitialData();
            Check(attrs.GetAttributeValue(hp) == 100f, "Fixture HP");
        }
        var host = new GameObject("Tutorial test");
        host.SetActive(false);
        var tutorial = host.AddComponent<PrototypeTutorialUpgrade>();
        Set(tutorial, "skillTargets", targets);
        Set(tutorial, "attackTarget", attackTarget);
        var gates = new GameObject[4];
        for (int i = 0; i < 4; i++) gates[i] = new GameObject("Gate " + i);
        Set(tutorial, "gates", gates);
        Set(tutorial, "damageEffect", damage);
        Set(tutorial, "maxHealthAttribute", max);
        Set(tutorial, "basicAttack", basic);
        Set(tutorial, "chargeSkill", charge);
        Set(tutorial, "thrustSkill", thrust);
        var hitTag = ScriptableObject.CreateInstance<GameplayTag>();
        Set(tutorial, "hitConfirmed", hitTag);
        Call(tutorial, "Awake");
        Call(tutorial, "OnEnable");
        Set(tutorial, "player", player);
        Set(tutorial, "stage", 3);
        GameplayEffectSpec Spec(AbilityDefinition ability) => player.MakeSpec(damage, playerObject, ability);
        float Hp(int i) => targets[i].GetComponent<AttributeSet>().GetAttributeValue(hp);
        damage.Apply(Spec(basic), targets[0].gameObject);
        Check(Hp(0) == 100f, "Basic attack must not damage skill targets");
        damage.Apply(Spec(thrust), targets[0].gameObject);
        Check(Hp(0) == 100f, "Wrong skill must not damage charge targets");
        chargeSpec.SetFloat(AbilityLogic_ApprenticeHeroSwordChargeSpin.ChargeSecondsKey, .1f);
        damage.Apply(Spec(charge), targets[0].gameObject);
        Check(Hp(0) == 100f, "A quick right click must not kill a charge target");
        chargeSpec.SetFloat(AbilityLogic_ApprenticeHeroSwordChargeSpin.ChargeSecondsKey, chargeData.MaxChargeSeconds);
        damage.Apply(Spec(charge), targets[0].gameObject);
        Check(Hp(0) == 0f, "Full charge must apply lethal damage through GAS");
        var evt = new AbilityEventData { Spec = chargeSpec, Target = targets[0].gameObject };
        Call(tutorial, "OnGameplayEvent", hitTag, evt);
        Call(tutorial, "OnGameplayEvent", hitTag, evt);
        Check(Get<int>(tutorial, "chargeKills") == 1, "Repeated hit events must not double count a kill");
        Call(tutorial, "ResetTarget", 0);
        Check(Hp(0) == 100f && !Get<bool[]>(tutorial, "defeated")[0], "Respawn restores HP and eligibility");
        damage.Apply(Spec(thrust), targets[4].gameObject);
        Check(Hp(4) == 0f, "Q must defeat its own target group");
        Set(tutorial, "chargeCooldown", player.AddScopedCooldownDurationMultiplier(d => d == charge, 1f / 6f));
        Set(tutorial, "thrustCooldown", player.AddScopedCooldownDurationMultiplier(d => d == thrust, 1f / 3f));
        var cooldown = Get<AbilityCooldownController>(player, "cooldownController");
        Check(Mathf.Approximately(cooldown.GetFinalCooldownSeconds(charge), 1f) &&
              Mathf.Approximately(cooldown.GetFinalCooldownSeconds(thrust), 1f), "Both skill cooldowns become one second");
        Call(tutorial, "Advance");
        Check(tutorial.Stage == 4 && gates[3].activeSelf && !gates[0].activeSelf, "Skill completion still requires chest confirmation before boss travel");
        tutorial.CompleteChestTutorial();
        Check(tutorial.Stage == 5 && !gates[3].activeSelf, "Chest confirmation must open the final boss portal route");
        Check(Get<IDisposable>(tutorial, "chargeCooldown") == null, "Completion releases cooldown policy");
        Check(Mathf.Approximately(cooldown.GetFinalCooldownSeconds(charge), 6f) &&
              Mathf.Approximately(cooldown.GetFinalCooldownSeconds(thrust), 3f), "Original skill cooldowns return after completion");
        Call(tutorial, "OnDisable");
        Call(tutorial, "ResetTarget", 0);
        damage.Apply(Spec(basic), targets[0].gameObject);
        Check(Hp(0) == 90f, "Disabling the tutorial must remove its global damage filter");
    }

    private static void DashIntroLifecycle()
    {
        var host = new GameObject("Dash intro test");
        host.SetActive(false);
        var tutorial = host.AddComponent<PrototypeTutorialUpgrade>();
        var playerObject = new GameObject("Dash test player");
        var player = playerObject.AddComponent<AbilitySystem>();
        Call(player, "Awake");
        var bullet = new GameObject("Authored bullet").transform;
        bullet.position = Vector3.up * 4f;
        var bottom = new GameObject("Bottom").transform;
        bottom.position = Vector3.down * 10f;
        var top = new GameObject("Top").transform;
        top.position = Vector3.up * 10f;
        var dash = ScriptableObject.CreateInstance<AbilityDefinition>();
        var damage = ScriptableObject.CreateInstance<GE_Damage_Spec>();
        // The real TagSystem only accepts tags registered from Resources/Tags.
        System.IO.Directory.CreateDirectory(Application.dataPath + "/Resources/Tags");
        AssetDatabase.Refresh();
        const string tagPath = "Assets/Resources/Tags/TutorialTestInvulnerable.asset";
        damage.invulnerableTag = AssetDatabase.LoadAssetAtPath<GameplayTag>(tagPath);
        if (damage.invulnerableTag == null)
        {
            damage.invulnerableTag = ScriptableObject.CreateInstance<GameplayTag>();
            damage.invulnerableTag.name = "State.Invulnerable";
            Set(damage.invulnerableTag, "id", 1);
            AssetDatabase.CreateAsset(damage.invulnerableTag, tagPath);
        }
        typeof(TagRegistry).GetField("_initialized", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, false);
        Set(tutorial, "skillTargets", Array.Empty<Transform>());
        Set(tutorial, "bullets", new[] { bullet });
        Set(tutorial, "bulletTop", top);
        Set(tutorial, "bulletBottom", bottom);
        Set(tutorial, "dash", dash);
        Set(tutorial, "damageEffect", damage);
        Call(tutorial, "Awake");
        Set(tutorial, "player", player);
        Set(tutorial, "stage", 1);
        var pause = new PauseFixture();
        var camera = new CameraFixture();
        var input = new InputFixture { BackendComponent = player.transform, Held = true };
        var ui = new UiBlockFixture();
        TimeScalePausePlayback.RegisterBackend(pause);
        GameplayCameraFocusPlayback.RegisterBackend(camera);
        InputActionQuery.RegisterBackend(input);
        UiStackPlayback.RegisterBackend(ui);
        try
        {
            Call(tutorial, "TickDashIntro");
            Check(!pause.IsPaused, "Distant bullet must not freeze gameplay");
            bullet.position = Vector3.up * 2.8f;
            Call(tutorial, "TickDashIntro");
            Check(!tutorial.IsDashPromptVisible, "Prompt must wait until the bullet is within safe immediate-dash reach");
            bullet.position = Vector3.up * 2.7f;
            Call(tutorial, "TickDashIntro");
            Check(pause.IsHeldBy(tutorial) && Mathf.Approximately(Time.timeScale, .6f) && tutorial.IsDashPromptVisible,
                "Approach must start slow motion and show the prompt before full stop");
            Check(InputActionQuery.IsPressBlocked(InputActionId.Dash), "Normal combat input cannot consume the guided dash");
            Set(tutorial, "introElapsed", .2f);
            bullet.position = Vector3.up * 2.4f;
            Call(tutorial, "TickDashIntro");
            Check(Time.timeScale > 0f && Time.timeScale < .6f && camera.CurrentOrthographicSize < 10f &&
                  camera.CurrentOrthographicSize > 6.5f, "World speed and lens must change progressively");
            Call(tutorial, "TickDashIntro");
            Check(pause.SlowAcquisitions == 0 && tutorial.IsDashPromptVisible, "Held key must not skip the fresh confirmation");
            input.Held = false;
            Call(tutorial, "TickDashIntro");
            input.Held = true;
            Call(tutorial, "TickDashIntro");
            Check(pause.SlowAcquisitions == 1 && !pause.IsPaused && tutorial.IsDashPromptVisible,
                "Mapped action must reach GAS during slowdown; rejected activation preserves the ramp");
            Check(ui.Releases == 1 && ui.Blocked, "Confirm must release UI control lock before GAS and reacquire it on rejection");
            Set(tutorial, "introElapsed", 1.2f);
            bullet.position = Vector3.up * 1.8f;
            Call(tutorial, "TickDashIntro");
            Check(!pause.IsPaused && Time.timeScale > .2f && Time.timeScale < .4f && tutorial.IsDashPromptVisible,
                "Midpoint must remain visibly in slow motion with the cue available");
            float distanceScale = Time.timeScale;
            Set(tutorial, "introElapsed", 20f);
            Call(tutorial, "TickDashIntro");
            Check(Mathf.Approximately(Time.timeScale, distanceScale), "Elapsed time alone must not stop a distant projectile");
            Set(tutorial, "introElapsed", 2.5f);
            bullet.position = Vector3.up * .95f;
            Call(tutorial, "TickDashIntro");
            Check(pause.IsPaused && tutorial.IsDashPromptVisible && Mathf.Approximately(camera.CurrentOrthographicSize, 6.5f),
                "Unanswered slowdown must settle at complete stop with the prompt retained");
            input.Held = false;
            Call(tutorial, "TickDashIntro");
            input.Held = true;
            Call(tutorial, "TickDashIntro");
            Check(pause.SlowAcquisitions == 2 && pause.IsPaused, "Rejected input after full stop must retain its pause");

            // Exercise the same real invulnerability check and relative sweep used during a guided dash.
            pause.Release(tutorial);
            pause.AcquireCombatSlowMotion(tutorial);
            Type phase = tutorial.GetType().GetField("dashIntro", Private).FieldType;
            Set(tutorial, "dashIntro", Enum.Parse(phase, "Dashing"));
            Set(tutorial, "dashToken", new AbilityCancellationToken());
            playerObject.AddComponent<TagSystem>().AddTag(damage.invulnerableTag, 1);
            Check(CombatInvulnerabilityUtil.IsDamageSuppressed(playerObject, damage), "Fixture must register actual invulnerability");
            player.transform.position = Vector3.up * 2f;
            bullet.position = Vector3.up * 1.2f;
            Vector3 frozenPosition = bullet.position;
            Call(tutorial, "TickBullets");
            Check(bullet.position == frozenPosition && Get<bool>(tutorial, "evadedBullet"),
                "A real invulnerable dash segment must evade a stationary projectile");
            Check(!Get<bool[]>(tutorial, "bulletConsumed")[0] && bullet.gameObject.activeSelf,
                "Dodging must leave the projectile active to pass through the player");

            pause.Acquire(playerObject); // Independent owner must survive tutorial cancellation.
            Call(tutorial, "OnDisable");
            Check(!pause.IsHeldBy(tutorial) && pause.IsPaused, "Cleanup cannot release another owner's pause");
            Check(camera.Restored && camera.CurrentOrthographicSize == 10f && !tutorial.IsDashPromptVisible,
                "Cancellation restores lens and hides prompt");
            Check(!InputActionQuery.IsPressBlocked(InputActionId.Dash) && !InputActionQuery.IsPressBlocked(InputActionId.SwapWeapon),
                "Cancellation releases both input blocks");
            pause.Release(playerObject);
            Set(tutorial, "player", player);
            Call(tutorial, "TickDashIntro");
            Check(!pause.IsPaused, "The scene-entry introduction must not repeat");
        }
        finally
        {
            Call(tutorial, "CleanupDashIntro");
            TimeScalePausePlayback.RegisterBackend(null);
            GameplayCameraFocusPlayback.RegisterBackend(null);
            InputActionQuery.UnregisterBackend(input);
            UiStackPlayback.RegisterBackend(null);
            Time.timeScale = 1f;
        }
    }

    private static void SharedTimeScaleOwnership()
    {
        var rampOwner = new GameObject("Real ramp owner");
        var menuOwner = new GameObject("Other pause owner");
        // Supply the existing runner slot without invoking play-only DDOL bootstrap in this native EditMode harness.
        var runnerObject = new GameObject("Native time runner");
        Type runnerType = typeof(TimeScalePauseService).GetNestedType("TimeScalePauseServiceRunner", BindingFlags.NonPublic);
        FieldInfo runnerField = typeof(TimeScalePauseService).GetField("runner", BindingFlags.Static | BindingFlags.NonPublic);
        runnerField.SetValue(null, runnerObject.AddComponent(runnerType));
        Time.timeScale = .8f;
        try
        {
            Check(TimeScalePauseService.SetOwnedTimeScale(rampOwner, .35f), "Real service accepts ramp owner");
            TimeScalePauseService.SetOwnedTimeScale(rampOwner, .1f);
            Check(Mathf.Approximately(Time.timeScale, .1f), "Existing owner can update its scale atomically");
            Check(!TimeScalePauseService.Acquire(rampOwner), "Legacy acquire remains non-overwriting");
            TimeScalePauseService.Acquire(menuOwner);
            TimeScalePauseService.SetOwnedTimeScale(rampOwner, .2f);
            Check(Time.timeScale == 0f, "An independent full pause wins over ramp updates");
            TimeScalePauseService.Release(menuOwner);
            Check(Mathf.Approximately(Time.timeScale, .2f), "Closing other pause resumes the latest ramp scale");
            TimeScalePauseService.SetOwnedTimeScale(rampOwner, 0f);
            Check(TimeScalePauseService.IsPaused, "Ramp reaches a genuine shared full pause");
            TimeScalePauseService.SetOwnedTimeScale(rampOwner, .15f);
            Check(!TimeScalePauseService.IsPaused && Mathf.Approximately(Time.timeScale, .15f), "Same owner transitions from stop to dash slow motion");
            Check(!TimeScalePauseService.SetOwnedTimeScale(rampOwner, float.NaN), "Nonfinite scale must be rejected");
            TimeScalePauseService.Release(rampOwner);
            Check(Mathf.Approximately(Time.timeScale, .8f), "Ramp updates must retain the original restore scale");
        }
        finally
        {
            TimeScalePauseService.Release(rampOwner);
            TimeScalePauseService.Release(menuOwner);
            Time.timeScale = 1f;
            UnityEngine.Object.DestroyImmediate(rampOwner);
            UnityEngine.Object.DestroyImmediate(menuOwner);
            runnerField.SetValue(null, null);
            UnityEngine.Object.DestroyImmediate(runnerObject);
        }
    }

    private sealed class PauseFixture : ITimeScalePauseBackend, ICombatSlowMotionBackend, ITimeScaleRampBackend
    {
        private readonly Dictionary<UnityEngine.Object, float> owners = new();
        public int SlowAcquisitions;
        public bool IsPaused => owners.ContainsValue(0f);
        public bool IsCombatSlowMotion => owners.ContainsValue(.15f);
        public bool IsHeldBy(UnityEngine.Object owner) => owners.ContainsKey(owner);
        public bool Acquire(UnityEngine.Object owner) => Add(owner, 0f);
        public bool AcquireCombatSlowMotion(UnityEngine.Object owner) { SlowAcquisitions++; return Add(owner, .15f); }
        public bool SetOwnedTimeScale(UnityEngine.Object owner, float scale)
        {
            if (Mathf.Approximately(scale, .15f)) SlowAcquisitions++;
            owners[owner] = scale;
            Apply();
            return true;
        }
        private bool Add(UnityEngine.Object owner, float scale)
        {
            if (owners.ContainsKey(owner)) return false;
            owners.Add(owner, scale);
            Apply();
            return true;
        }
        public bool Release(UnityEngine.Object owner) { bool removed = owners.Remove(owner); Apply(); return removed; }
        private void Apply() { float scale = 1f; foreach (float value in owners.Values) scale = Mathf.Min(scale, value); Time.timeScale = scale; }
    }

    private sealed class CameraFixture : IGameplayCameraFocusBackend, IGameplayCameraFocusSession
    {
        public bool Restored;
        public Transform CachedFollow => null;
        public bool HasOrthographicSize => true;
        public float CachedOrthographicSize => 10f;
        public float CurrentOrthographicSize { get; private set; } = 10f;
        public Vector3 CurrentCenter => Vector3.zero;
        public IGameplayCameraFocusSession Capture(Component owner) => this;
        public void SetTarget(Transform target) { }
        public void SnapToTarget(Transform target) { }
        public void SetOrthographicSize(float size) => CurrentOrthographicSize = size;
        public System.Collections.IEnumerator WaitForSettle(Transform target) { yield break; }
        public void Restore(Transform target) { Restored = true; CurrentOrthographicSize = 10f; }
    }

    private sealed class InputFixture : IInputActionQueryBackend
    {
        public Component BackendComponent { get; set; }
        public bool Held;
        public bool PrimaryHeld;
        public Sprite GetBindingIcon(InputActionId action) => null;
        public bool IsPressed(InputActionId action) => action == InputActionId.Dash ? Held : action == InputActionId.PrimaryAttack && PrimaryHeld;
        public bool WasPressedThisFrame(InputActionId action) => false;
        public bool WasReleasedThisFrame(InputActionId action) => false;
        public bool IsKeyPressed(KeyCode key) => false;
        public bool WasKeyPressedThisFrame(KeyCode key) => false;
        public bool WasKeyReleasedThisFrame(KeyCode key) => false;
        public Vector2 GetMoveVectorRaw() => Vector2.up;
        public Vector2 GetMoveVectorNormalized() => Vector2.up;
        public Vector3 GetPointerWorldPosition(Camera camera, float z = 0f) => Vector3.up;
    }

    private sealed class UiBlockFixture : IUiStackBackend
    {
        public bool Blocked;
        public int Releases;
        public bool SetExternalUiInputBlocked(UnityEngine.Object owner, bool blocked)
        {
            Blocked = blocked;
            if (!blocked) Releases++;
            return true;
        }
        public bool CanOpenUIForExternalBlockOwner(UnityEngine.Object owner, IStackableUI ui) => false;
        public bool TryPushUIForExternalBlockOwner(UnityEngine.Object owner, IStackableUI ui) => false;
    }
}
