// Isolated Unity Editor harness with freshly built Core/Gameplay dependencies.
// -batchmode -nographics -executeMethod TutorialBossCombatRegression.Run
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityGAS;

public static class TutorialBossCombatRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object o, string n, object v) => o.GetType().GetField(n, Private).SetValue(o, v);
    private static T Get<T>(object o, string n) => (T)o.GetType().GetField(n, Private).GetValue(o);
    private static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, Private).Invoke(o, a);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static GameObject Host(string name) { var go = new GameObject(name); go.SetActive(false); return go; }

    public static void Run()
    {
        try
        {
            Defeat();
            Victory();
            Cancellation();
            CinematicHandoff();
            HeavySlashApproachWall();
            Debug.Log("TUTORIAL_BOSS_COMBAT_PASS: real HP subscription; active boss combat; player unlock; lethal defeat cancellation; single outcome; victory handoff; normal death routing restoration; cancellation cleanup.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private sealed class Fixture
    {
        public readonly TutorialBossEncounterSequence Sequence;
        public readonly DemonKingController Boss;
        public readonly AttributeSet Attributes;
        public readonly AttributeDefinition Hp;
        public readonly PlayerDeathReturnToHub2D Death;
        public readonly StackProbe Input = new();
        public Fixture()
        {
            UiStackPlayback.RegisterBackend(Input);
            Hp = ScriptableObject.CreateInstance<AttributeDefinition>();
            var max = ScriptableObject.CreateInstance<AttributeDefinition>();
            var player = Host("Combat player");
            Attributes = player.AddComponent<AttributeSet>();
            Set(Attributes, "maxLinks", new List<AttributeSet.MaxLink> { new AttributeSet.MaxLink { value = Hp, max = max, fillToMaxOnInitialize = true } });
            Set(Attributes, "_initialized", false);
            Attributes.InitializeFromInitialData();
            Death = player.AddComponent<PlayerDeathReturnToHub2D>();
            Set(Death, "body", player.AddComponent<Rigidbody2D>());
            Set(Death, "attributeSet", Attributes);
            Set(Death, "hpDef", Hp);
            var boss = Host("Combat boss");
            Boss = boss.AddComponent<DemonKingController>();
            var host = Host("Tutorial sequence");
            Sequence = host.AddComponent<TutorialBossEncounterSequence>();
            Set(Sequence, "combatBoss", Boss);
            Set(Sequence, "combatPlayerHealth", Hp);
            Set(Sequence, "playerTransform", player.transform);
            // The fixture tests routing and ownership without invoking the game's full cinematic/tag bootstrap.
            Set(Sequence, "lockPlayerControls", false);
            Set(Sequence, "blockPlayerTargetability", false);
            Set(Sequence, "pauseRunTimer", false);
        }
        public IEnumerator Start()
        {
            IEnumerator run = (IEnumerator)Call(Sequence, "PlayCombatRoutine");
            Check(run.MoveNext(), "Combat should wait for an outcome");
            Check(Boss.IsCombatActive, "Boss did not start fighting");
            Check(CombatIncomingDamageModifiers.Apply(new CombatIncomingDamageContext(Boss.gameObject, null, 100f)) == 100f,
                "Boss must remain killable during combat");
            Check(!Death.enabled, "Normal game-over route must be suspended before damage");
            Set(Sequence, "lockPlayerControls", true);
            Check(!(bool)Call(Sequence, "ShouldMaintainPlayerLock"), "Cinematic LateUpdate must not relock combat");
            Set(Sequence, "lockPlayerControls", false);
            return run;
        }
        public void Cancel() => Sequence.CancelSequence();
    }

    private static void Defeat()
    {
        var f = new Fixture();
        var run = f.Start();
        f.Attributes.TrySetCurrentValue(f.Hp, 20f, null);
        Check(!Get<bool>(f.Sequence, "combatLost") && f.Boss.IsCombatActive, "Nonlethal hit ended combat");
        f.Attributes.TrySetCurrentValue(f.Hp, 0f, null);
        Check(Get<bool>(f.Sequence, "combatLost") && !f.Boss.IsCombatActive && f.Input.Blocked,
            "Lethal damage must stop combat and lock follow-up dialogue synchronously");
        Check(!Get<Rigidbody2D>(f.Death, "body").simulated, "Tutorial defeat did not apply the real death physics state");
        Check(CombatIncomingDamageModifiers.Apply(new CombatIncomingDamageContext(f.Boss.gameObject, null, 100f)) == 0f,
            "A lingering attack could launch the boss-owned ending after player defeat");
        Check(CombatIncomingDamageModifiers.Apply(new CombatIncomingDamageContext(f.Sequence.gameObject, null, 100f)) == 100f,
            "Defeat damage guard escaped its boss scope");
        Call(f.Sequence, "HandleCombatBossDeath", f.Boss);
        Check(!Get<bool>(f.Sequence, "combatWon"), "A later boss death changed a defeat into victory");
        Check(!run.MoveNext(), "Defeat did not release combat wait");
        Check(!f.Death.enabled && !f.Death.IsDeathSequenceRunning, "Normal game over raced tutorial defeat");
        Check(!Get<bool>(f.Sequence, "hasStartedPlayerDeathPresentation"), "Scripted collapse was played");
        f.Cancel();
        Check(!f.Input.Blocked, "Defeat cancellation leaked input ownership");
        Check(CombatIncomingDamageModifiers.Apply(new CombatIncomingDamageContext(f.Boss.gameObject, null, 100f)) == 100f,
            "Cancellation leaked the terminal damage guard");
    }

    private static void Victory()
    {
        var f = new Fixture();
        var run = f.Start();
        Call(f.Sequence, "HandleCombatBossDeath", f.Boss);
        Call(f.Sequence, "HandleCombatPlayerHealth", f.Hp, 20f, 0f);
        Check(Get<bool>(f.Sequence, "combatWon") && !Get<bool>(f.Sequence, "combatLost"), "Victory must win once handed to the boss ending");
        Check(!run.MoveNext(), "Victory did not release combat wait");
        Call(f.Sequence, "ReleaseCombatPlayer");
        Check(f.Death.enabled, "Victory did not restore normal death routing");
        Set(f.Sequence, "lockPlayerControls", true);
        Check(!(bool)Call(f.Sequence, "ShouldMaintainPlayerLock"), "Tutorial lock interferes with boss-owned ending");
        f.Cancel();
    }

    private static void Cancellation()
    {
        var f = new Fixture();
        f.Start();
        f.Cancel();
        Check(!f.Boss.IsCombatActive && f.Death.enabled && !f.Input.Blocked, "Cancellation did not restore live player/boss state");
        f.Attributes.TrySetCurrentValue(f.Hp, 0f, null);
        Check(!Get<bool>(f.Sequence, "combatLost"), "Canceled sequence still subscribed to health");
    }

    private static void CinematicHandoff()
    {
        var f = new Fixture();
        var hud = new DialogueProbe();
        DialoguePlayback.RegisterBackend(hud);
        var player = Get<Transform>(f.Sequence, "playerTransform").gameObject;
        var interactor = player.AddComponent<PlayerInteractor2D>();
        var input = player.AddComponent<PlayerIntentInput2D>();
        Set(f.Sequence, "lockPlayerControls", true);
        Call(f.Sequence, "AcquirePlayerProtection");
        Check(!input.enabled && interactor.CurrentState == InteractState.None, "Intro did not lock real input");
        player.GetComponent<PlayerCinematicProtection>().ForceReleaseAll();
        Call(f.Sequence, "MaintainPlayerLock");
        Check(interactor.CurrentState == InteractState.None, "Normalization regression was not reproduced");
        Call(f.Sequence, "HideDefaultHudRoots");
        Call(f.Sequence, "HideDefaultHudRoots");
        Check(hud.Acquires == 1, "Intro must own HUD suppression exactly once");
        Call(f.Sequence, "OnEnable");
        Check((bool)typeof(BossControllerBase).GetField("encounterIntroFinished", Private).GetValue(f.Boss),
            "Boss FSM can start a competing intro");
        f.Start();
        Check(input.enabled && interactor.CurrentState == InteractState.Idle, "Combat left real player input/state locked");
        Check(hud.Releases == 1 && hud.FadeSeconds > 0f, "Combat must fade HUD back through dialogue ownership");
        Call(f.Sequence, "OnDisable");
        Check(hud.Releases == 1, "Cleanup released HUD twice");
        DialoguePlayback.RegisterBackend(null);
    }

    private static void HeavySlashApproachWall()
    {
        var f = new Fixture();
        var probeObject = new GameObject("Approach body probe");
        probeObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        var probe = probeObject.AddComponent<BoxCollider2D>();
        probe.size = Vector2.one;
        Set(f.Boss, "wallRushCollisionProbe", probe);
        Set(f.Boss, "wallMask", (LayerMask)(1 << 30));
        var wall = new GameObject("Approach wall");
        wall.layer = 30;
        wall.transform.position = new Vector3(3f, 0f, 0f);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(1f, 8f);
        Physics2D.SyncTransforms();
        var logic = ScriptableObject.CreateInstance<AbilityLogic_DemonKingHeavySlash>();
        var resolve = typeof(AbilityLogic_DemonKingHeavySlash).GetMethod("ResolveWallSafeApproachTarget", BindingFlags.Static | BindingFlags.NonPublic);
        Vector2 safe = (Vector2)resolve.Invoke(null, new object[] { f.Boss, Vector2.zero, new Vector2(7f, 0f) });
        Check(safe.x > 1.9f && safe.x < 2f && Mathf.Abs(safe.y) < 0.001f, $"Approach destination crossed the wall or ignored body size: {safe}");
        var motion = f.Boss.GetComponent<AbilityMotionController2D>();
        if (motion == null) motion = f.Boss.gameObject.AddComponent<AbilityMotionController2D>();
        var run = (IEnumerator)Call(logic, "RunSlashApproachMove", f.Boss, motion, Vector2.zero, Vector2.right, 7f, 0f, new Vector2(7f, 0f), null);
        while (run.MoveNext()) { }
        Check(f.Boss.transform.position == Vector3.zero, "Approach completion snapped the boss through the wall");
        Check(!motion.HasActiveMotion, "Completed approach leaked movement");
        wall.transform.position = new Vector3(30f, 0f, 0f);
        Physics2D.SyncTransforms();
        safe = (Vector2)resolve.Invoke(null, new object[] { f.Boss, Vector2.zero, new Vector2(7f, 0f) });
        Check(Mathf.Abs(safe.x - 7f) < 0.001f, "Clear approach was shortened");
        UnityEngine.Object.DestroyImmediate(wall);
        UnityEngine.Object.DestroyImmediate(probeObject);
        UnityEngine.Object.DestroyImmediate(logic);
        f.Cancel();
    }

    private sealed class DialogueProbe : IDialoguePlaybackBackend
    {
        public int Acquires, Releases;
        public float FadeSeconds;
        public bool IsPlaying => false;
        public bool HasActiveController => true;
        public void AcquireNonDialogueUiSuppression(object owner, float fadeSeconds = -1f) => Acquires++;
        public void ReleaseNonDialogueUiSuppression(object owner, float fadeSeconds = -1f) { Releases++; FadeSeconds = fadeSeconds; }
        public void ReleaseNonDialogueUiSuppressionWithoutRestore(object owner) { }
        public void SetUpperPanelHiddenForCameraDialogue(bool hidden, Action onComplete = null) => onComplete?.Invoke();
        public void SetPortraitsHiddenForCameraDialogue(bool hidden) { }
        public void SetLowerPanelRetainedBetweenDialogues(bool retained) { }
        public bool TryStartDialogue(TextAsset ink, List<NPCData> people, NPCFeatureController feature = null) => true;
        public bool TryStartDialogue(TextAsset ink, List<NPCData> people, string path, NPCFeatureController feature = null) => true;
        public bool TryStartDialogue(TextAsset ink, List<NPCData> people, NPCFeatureController feature, string path) => true;
        public bool TryStartDialogueSequence(IReadOnlyList<DialogueStorySegment> stories, List<NPCData> people, NPCFeatureController feature = null) => true;
        public bool TryStartDialogueSequence(IReadOnlyList<DialogueStorySegment> stories, List<NPCData> people, DialoguePresentationOptions options) => true;
        public bool TryStartDialogueSequence(IReadOnlyList<DialogueStorySegment> stories, List<NPCData> people, NPCFeatureController feature, DialoguePresentationOptions options) => true;
    }

    private sealed class StackProbe : IUiStackBackend
    {
        public bool Blocked;
        public bool SetExternalUiInputBlocked(UnityEngine.Object owner, bool value) { Blocked = value; return true; }
        public bool CanOpenUIForExternalBlockOwner(UnityEngine.Object owner, IStackableUI ui) => !Blocked;
        public bool TryPushUIForExternalBlockOwner(UnityEngine.Object owner, IStackableUI ui) => !Blocked;
    }
}
