using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Story = Ink.Runtime.Story;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Runs production dialogue routing/state against an in-memory profile, never a user's save slot.</summary>
public static class NpcAffectionDialogueValidation
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Output = "outputs/npc-affection-2026-11-01";
    private static readonly List<string> Checks = new();
    private static readonly List<Object> Owned = new();

    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    public static void Run()
    {
        Checks.Clear();
        Directory.CreateDirectory(Output);
        var save = new MemorySave();
        GameDataStore.RegisterBackend(save);
        var session = Singleton<GamePlayDataManager>();
        RunSessionStore.RegisterBackend(session);
        var affection = Singleton<AffectionManager>();
        var rewards = new RewardView(session);
        RewardDisplayPlayback.RegisterBackend(rewards);
        var playback = new Playback();
        DialoguePlayback.RegisterBackend(playback);
        try
        {
            var npc = AssetDatabase.LoadAssetAtPath<NPCData>(NpcAffectionDialogueAuthoring.MerchantPath);
            VerifyContent(npc);
            var trigger = Encounter();
            var player = new Player(Own(new GameObject("Validation player")).transform);
            Check(!NpcAffectionDialogue.CanOffer(npc), "Hub has no affection conversation");
            Check(!affection.AddAffection(npc, 1), "Opted-in merchant cannot gain through hub dialogue");
            trigger.OnPlayerInteract(player);
            Check(npc.greetingPaths.Contains(playback.Path), "Hub routes to a choice-free greeting");
            playback.End(true);

            session.StartRun();
            Check(trigger.CanOfferAffectionDialogue, "First active-run encounter is available");
            Check(trigger.IntroductionKey == null, "Run dialogue does not start through automatic room introduction");
            trigger.OnPlayerInteract(player);
            Check(npc.affectionDialogues.Any(e => e.affectionLevel == 0 && e.startPath == playback.Path), "First encounter selects a level-zero conversation");
            playback.End(false);
            Check(trigger.CanOfferAffectionDialogue, "Interrupted conversation without a gain keeps its opportunity");
            trigger.OnPlayerInteract(player);
            playback.End(true); // Failure branch: no add_aff request.
            Check(!trigger.CanOfferAffectionDialogue && affection.GetAffection(npc.id) == 0, "Completed failure consumes the opportunity without gain");
            Check(!affection.AddAffection(npc, 1), "Consumed failure cannot be bypassed with a later gain request");
            var repeated = Encounter();
            Check(!repeated.CanOfferAffectionDialogue, "New stage / new merchant object shares NPC-ID consumption");
            repeated.OnPlayerInteract(player);
            Check(npc.greetingPaths.Contains(playback.Path), "Repeated encounter routes to a greeting");
            playback.End(true);
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(session.Data), session.Data);
            Check(!repeated.CanOfferAffectionDialogue, "Session DTO round-trip preserves consumption");

            session.EndRun(RunEndReason.Defeat);
            Check(session.Data.affectionGainNpcIds.Count == 0 && !repeated.CanOfferAffectionDialogue, "Defeat resets consumption; outside a run remains unavailable");
            session.StartRun();
            repeated.OnPlayerInteract(player);
            Action<bool> lateEnd = playback.TakeEnd();
            session.EndRun(RunEndReason.Defeat);
            session.StartRun();
            lateEnd(true);
            Check(repeated.CanOfferAffectionDialogue, "Old run's late completion cannot consume a new run's opportunity");

            for (int level = 0; level < 4; level++)
            {
                if (level > 0) session.StartRun();
                repeated.OnPlayerInteract(player);
                Check(npc.affectionDialogues.Any(e => e.affectionLevel == level && e.startPath == playback.Path), "Random candidate matches current affection " + level);
                int continuations = 0;
                Check(affection.AddAffection(npc, 1, () => continuations++), "Successful choice grants level " + (level + 1));
                Check(continuations == 1 && rewards.Count == level + 1, "Exactly one reward and continuation at level " + (level + 1));
                playback.End(false); // A granted reward must remain consumed even if presentation is interrupted.
                Check(!NpcAffectionDialogue.CanOffer(npc) && !affection.AddAffection(npc, 1), "Gain cannot repeat after interrupted presentation at level " + (level + 1));
                CommitProgress(session.Data, save.Data);
                session.EndRun(level == 3 ? RunEndReason.Victory : RunEndReason.Defeat);
                Check(save.Data.affectionData.affectionRecords.Single(r => r.npcId == npc.id).amount == level + 1,
                    "Existing commit policy persists permanent affection " + (level + 1));
            }
            session.StartRun();
            Check(!repeated.CanOfferAffectionDialogue, "Maximum affection suppresses further conversations");
            repeated.OnPlayerInteract(player);
            Check(npc.greetingPaths.Contains(playback.Path), "Maximum affection still permits greetings");
            playback.End(true);
            var cappedBubble = repeated.GetComponentInChildren<NpcAffectionBubble>(true);
            Call(cappedBubble, "LateUpdate");
            Check(!cappedBubble.GetComponentInChildren<SpriteRenderer>(true).enabled, "Maximum affection also hides the actual indicator renderer");

            var backpack = player.Transform.gameObject.AddComponent<PlayerBackpackInventory>();
            Call(backpack, "Awake");
            Check(!NpcAffectionDialogue.IsBackpackFull(backpack), "An empty backpack is not full");
            var item = Own(ScriptableObject.CreateInstance<RelicDefinition>());
            for (int i = 0; i < backpack.Capacity; i++) backpack.Set(i, item);
            Check(NpcAffectionDialogue.IsBackpackFull(backpack), "Actual backpack slots determine fullness");
            repeated.OnPlayerInteract(player);
            Check(npc.fullBackpackGreetingPaths.Contains(playback.Path), "Full-backpack reaction takes precedence over ordinary greetings");
            playback.End(true);

            var other = Own(Object.Instantiate(npc));
            other.id = 1002;
            Check(NpcAffectionDialogue.CanOffer(other), "Allowance remains independent for another NPC ID");
            var boss = Own(Object.Instantiate(npc));
            boss.id = 3001; boss.isBoss = true;
            session.EndRun(RunEndReason.Defeat);
            Check(!boss.UsesRunAffectionDialogue && !NpcAffectionDialogue.CanOffer(boss), "Bosses ignore the ordinary-NPC content fields");
            Check(affection.AddAffection(boss, 1), "Existing boss positive-gain behavior is preserved outside an active run");

            var npcManager = Singleton<NPCManager>();
            var database = Own(ScriptableObject.CreateInstance<NPCDatabase>());
            database.npcList.Add(npc);
            Set(npcManager, "database", database);
            var modifiers = Singleton<RunModifierService>();
            modifiers.RebuildFromPurchasedUpgrades();
            Check(Mathf.Approximately(modifiers.ShopModifiers.affectionDiscountRate, 0.05f), "Fourth merchant reward grants a five-percent global discount");
            modifiers.RebuildFromPurchasedUpgrades();
            Check(Mathf.Approximately(modifiers.ShopModifiers.affectionDiscountRate, 0.05f), "Modifier rebuild does not compound the discount");

            // Real imported prefab, renderer and source frames; no UI hierarchy is constructed at runtime.
            affection.SetAffection(npc.id, 0);
            session.StartRun();
            VerifyBubble(repeated);
            session.EndRun(RunEndReason.Defeat);

            File.WriteAllLines(Output + "/validation.txt", Checks.Concat(new[] {
                $"PASS: {Checks.Count} checks; Unity Editor native execution with production assemblies and in-memory save.",
                "Full manual playthrough and Player build were not executed." }));
            Debug.Log($"NPC_AFFECTION_PASS: {Checks.Count} checks.");
        }
        finally
        {
            for (int i = Owned.Count - 1; i >= 0; i--) if (Owned[i] != null) Object.DestroyImmediate(Owned[i]);
            Owned.Clear();
            GameDataStore.UnregisterBackend(save);
        }
    }

    private static void VerifyContent(NPCData npc)
    {
        string source = File.ReadAllText(NpcAffectionDialogueAuthoring.InkPath);
        string json = File.ReadAllText(Path.ChangeExtension(NpcAffectionDialogueAuthoring.InkPath, ".json"));
        Check(new Ink.Compiler(source).Compile().ToJson() == json, "Committed merchant JSON matches compiled Ink source");
        Check(npc.affectionRewards.Count == 4 && npc.affectionRewards.Select(r => r.effect).Distinct().Count() == 4, "Four authored merchant rewards have distinct effects");
        foreach (var entry in npc.affectionDialogues)
            for (int choice = 0; choice < 2; choice++)
            {
                var story = new Story(json);
                story.ChoosePathString(entry.startPath);
                while (story.canContinue) story.Continue();
                Check(story.currentChoices.Count == 2, entry.startPath + " presents two choices");
                story.ChooseChoiceIndex(choice);
                var tags = new List<string>();
                while (story.canContinue) { story.Continue(); tags.AddRange(story.currentTags); }
                Check(choice == 0 ? tags.Contains("add_aff: 1") : tags.Contains("choice_fail") && !tags.Any(t => t.StartsWith("add_aff")),
                    entry.startPath + " choice " + choice + " has the correct affection outcome");
            }
        foreach (string path in npc.greetingPaths.Concat(npc.fullBackpackGreetingPaths))
        {
            var story = new Story(json); story.ChoosePathString(path);
            int lines = 0; var tags = new List<string>();
            while (story.canContinue) { story.Continue(); lines++; tags.AddRange(story.currentTags); }
            Check(lines <= 2 && story.currentChoices.Count == 0 && !tags.Any(t => t.StartsWith("add_aff")), path + " is short, choice-free and grants no affection");
        }
        var frames = AssetDatabase.LoadAllAssetsAtPath(NpcAffectionDialogueAuthoring.SheetPath).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        Check(frames.Length == 3 && frames.All(s => s.rect.size == new Vector2(32, 32)) && frames.Select(s => s.rect.x).SequenceEqual(new[] { 0f, 32f, 64f }), "Provided sheet is sliced left-to-right into three 32x32 frames");
        var collection = LocalizationEditorSettings.GetStringTableCollection(GameText.TableName);
        string[] codes = { "ko", "en", "ja", "zh-Hans", "zh-Hant" };
        using (var reader = File.OpenText("DataSheets/Localization/GameText.csv"))
            foreach (var row in LocalizationSheetSetup.ReadRows(reader).Skip(1).Where(row => row[0].StartsWith("dialogue.merchant.run.", StringComparison.Ordinal)))
                for (int i = 0; i < codes.Length; i++)
                    Check(collection.StringTables.Single(table => table.LocaleIdentifier.Code == codes[i]).GetEntry(row[0])?.Value == row[i + 1],
                        "Localized dialogue matches CSV: " + row[0] + "/" + codes[i]);
    }

    private static void VerifyBubble(DialogueTrigger trigger)
    {
        var bubble = trigger.GetComponentInChildren<NpcAffectionBubble>(true);
        var renderer = bubble.GetComponentInChildren<SpriteRenderer>(true);
        Call(bubble, "LateUpdate");
        Check(renderer.enabled && bubble.Frames.Length == 3, "Authored bubble shows when an affection conversation is available");
        trigger.transform.localScale = new Vector3(-2, 2, 1);
        Call(bubble, "LateUpdate");
        Check(renderer.transform.lossyScale.x > 0 && Mathf.Approximately(renderer.transform.lossyScale.x, bubble.WorldSize), "Bubble stays upright and fixed-size under actor mirroring/scaling");
        Check(renderer.transform.position == bubble.Anchor.position, "Bubble follows the independently authored position anchor");
        trigger.transform.localScale = Vector3.one;
        Call(bubble, "LateUpdate");
        Capture(trigger, renderer, bubble);
        RunSessionStore.TryConsumeAffectionGain(trigger.NpcData.id);
        Call(bubble, "LateUpdate");
        Check(!renderer.enabled, "Consumed opportunity hides the authored bubble");
    }

    private static void Capture(DialogueTrigger trigger, SpriteRenderer renderer, NpcAffectionBubble bubble)
    {
        var cameraObject = Own(new GameObject("Bubble preview camera"));
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 1.7f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.23f, 0.24f, 0.27f);
        camera.transform.position = new Vector3(bubble.Anchor.position.x + 0.4f, bubble.Anchor.position.y - 0.8f, -10);
        var target = new RenderTexture(512, 512, 24);
        var texture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
        var previous = RenderTexture.active;
        try
        {
            // Preview only the actor and new indicator; nameplate font presentation needs the live UI service.
            foreach (var instance in Owned.OfType<GameObject>())
                if (instance != trigger.transform.root.gameObject && instance.GetComponentInChildren<DialogueTrigger>(true) != null)
                    instance.SetActive(false);
            foreach (var canvas in trigger.transform.root.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
            camera.targetTexture = target;
            for (int i = 0; i < bubble.Frames.Length; i++)
            {
                renderer.sprite = bubble.Frames[i];
                camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); texture.Apply();
                File.WriteAllBytes(Output + $"/merchant-bubble-{i}.png", texture.EncodeToPNG());
            }
        }
        finally { RenderTexture.active = previous; camera.targetTexture = null; Object.DestroyImmediate(texture); Object.DestroyImmediate(target); }
    }

    private static DialogueTrigger Encounter()
    {
        var root = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NpcAffectionDialogueAuthoring.PrefabPath)));
        var trigger = root.GetComponentInChildren<DialogueTrigger>(true);
        Call(trigger, "Awake"); Call(trigger, "OnDisable"); Call(trigger, "OnEnable");
        return trigger;
    }
    private static T Own<T>(T item) where T : Object { Owned.Add(item); return item; }
    private static void CommitProgress(GamePlayData run, GameData profile)
    {
        Assembly assembly = typeof(GamePlayDataManager).Assembly;
        Type request = assembly.GetType("RunSessionProgressCommitRequest");
        assembly.GetType("RunSessionProgressCommitPolicy").GetMethod("Commit").Invoke(null,
            new[] { Activator.CreateInstance(request, run, profile, 0) });
    }
    private static T Singleton<T>() where T : Component
    {
        T component = Own(new GameObject(typeof(T).Name)).AddComponent<T>();
        typeof(T).GetProperty("Instance").SetValue(null, component);
        return component;
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Checks.Add("PASS " + message); }
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Hidden).SetValue(owner, value);
    private static void Call(object owner, string method) => owner.GetType().GetMethod(method, Hidden).Invoke(owner, null);

    private sealed class Player : IPlayerInteractor
    {
        public Transform Transform { get; }
        public InteractState CurrentState => InteractState.Idle;
        public Player(Transform transform) { Transform = transform; }
        public void SetInteractState(InteractState state) { }
    }
    private sealed class MemorySave : IGameDataStoreBackend
    {
        public GameData Data { get; } = new GameData();
        public int ActiveSlotIndex => 0;
        public event Action<GameData, int> OnDataLoaded { add { } remove { } }
        public GameData EnsureData() => Data;
        public void SaveData() { }
        public void RequestImmediateSave(Object requester) { }
        public void RequestDeferredSave(Object requester) { }
        public void FlushSave(Object requester) { }
    }
    private sealed class RewardView : IRewardDisplayBackend
    {
        public int Count;
        public Component BackendComponent { get; }
        public RewardView(Component host) { BackendComponent = host; }
        public void ShowUpgradeReward(UpgradeNodeSO node, Action done) => done?.Invoke();
        public void ShowFlowOwnedReward(List<UpgradeEffectSO> upgrades, List<AffectionEffect> affection, Action done)
        { Count++; done?.Invoke(); }
    }
    private sealed class Playback : IDialoguePlaybackBackend
    {
        public bool IsPlaying { get; private set; }
        public bool HasActiveController => true;
        public string Path;
        private Action<bool> ended;
        public void End(bool completed) { IsPlaying = false; var callback = ended; ended = null; callback?.Invoke(completed); }
        public Action<bool> TakeEnd() { IsPlaying = false; var callback = ended; ended = null; return callback; }
        public void SetUpperPanelHiddenForCameraDialogue(bool hidden, Action done = null) => done?.Invoke();
        public void SetPortraitsHiddenForCameraDialogue(bool hidden) { }
        public void SetLowerPanelRetainedBetweenDialogues(bool retained) { }
        public void AcquireNonDialogueUiSuppression(object owner, float fadeSeconds = -1f) { }
        public void ReleaseNonDialogueUiSuppression(object owner, float fadeSeconds = -1f) { }
        public void ReleaseNonDialogueUiSuppressionWithoutRestore(object owner) { }
        public bool TryStartDialogue(TextAsset ink, List<NPCData> npcs, NPCFeatureController feature = null) => false;
        public bool TryStartDialogue(TextAsset ink, List<NPCData> npcs, string path, NPCFeatureController feature = null) => false;
        public bool TryStartDialogue(TextAsset ink, List<NPCData> npcs, NPCFeatureController feature, string path) => false;
        public bool TryStartDialogueSequence(IReadOnlyList<DialogueStorySegment> stories, List<NPCData> npcs, NPCFeatureController feature = null) => false;
        public bool TryStartDialogueSequence(IReadOnlyList<DialogueStorySegment> stories, List<NPCData> npcs, DialoguePresentationOptions options) => false;
        public bool TryStartDialogueSequence(IReadOnlyList<DialogueStorySegment> stories, List<NPCData> npcs, NPCFeatureController feature, DialoguePresentationOptions options)
        { Path = stories[0].StartPath; ended = options.OnEnded; IsPlaying = true; return true; }
    }
}
