// Run in an isolated Unity Editor project with freshly built production DLLs and the authored Buffy prefab.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityGAS;

public static class BuffyWorkoutPresentationRegression
{
    private const string PrefabPath = "Assets/_Project/Prefabs/Map/Procedural/Events/BuffyHealthTime/BuffyHealthTimeEventModule.prefab";
    private const string Pending = "BuffyWorkoutPresentationRegression.Pending";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static IEnumerator cases;
    private static double deadline;
    private static MemorySave profile;
    private static PopupProbe popup;

    public static void Run()
    {
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (SessionState.GetBool(Pending, false)) EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (cases == null)
            {
                deadline = EditorApplication.timeSinceStartup + 90;
                cases = Cases();
            }
            Check(EditorApplication.timeSinceStartup < deadline, "Regression timed out");
            if (cases.MoveNext()) return;
            Debug.Log("BUFFY_REGRESSION_PASS: all three rewards, popup text/colors, intro guidance, dust completion, 70% RGB/alpha, reentry, repeat rejection, failure and disable cleanup.");
            Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); Exit(1); }
    }

    private static void Exit(int code)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Pending, false);
        EditorApplication.Exit(code);
    }

    private static IEnumerator Cases()
    {
        profile = new MemorySave();
        GameDataStore.RegisterBackend(profile);
        var session = GamePlayDataManager.EnsureInstance();
        RunSessionStore.RegisterBackend(session);
        var data = session.Data;
        data.isRunActive = true;
        popup = new PopupProbe();
        DamagePopupPlayback.RegisterBackend(popup);

        foreach (BuffyWorkoutType choice in Enum.GetValues(typeof(BuffyWorkoutType)))
        {
            data.completedRunMapEventIds = new List<string>();
            data.levelProgression = new LevelProgressionState();
            profile.Data.completedNpcRoomIntroductions = new List<string>();
            popup.Requests.Clear();
            GameObject root = CreateFixture(out var group);
            var selected = group.Single(e => e.WorkoutType == choice);
            var attack = Get<AttributeDefinition>(selected, "attackBaseAttribute");
            var speed = Get<AttributeDefinition>(selected, "moveSpeedMultiplierAttribute");
            var playerObject = new GameObject("Buffy test player");
            playerObject.SetActive(false);
            var player = playerObject.AddComponent<BuffyWorkoutProbePlayer>();
            var attributes = playerObject.AddComponent<AttributeSet>();
            var catalog = ScriptableObject.CreateInstance<AttributeCatalogSO>();
            Set(catalog, "attributes", new[] { attack, speed });
            Set(attributes, "attributeCatalog", catalog);
            playerObject.SetActive(true);
            float attackBefore = attributes.GetBaseValue(attack);
            float speedBefore = attributes.GetBaseValue(speed);
            int levelBefore = data.levelProgression.level;
            yield return null;
            foreach (var equipment in group)
                Check(!Arrow(equipment).gameObject.activeSelf, "Guidance must wait for introduction completion");
            profile.Data.completedNpcRoomIntroductions.Add(Get<DialogueTrigger>(selected, "introductionSource").IntroductionKey);
            yield return null;
            foreach (var equipment in group)
            {
                Check(Arrow(equipment).gameObject.activeSelf, "Completed introduction must show all arrows");
                equipment.OnUnHighlight();
                var block = new MaterialPropertyBlock();
                Body(equipment).GetPropertyBlock(block);
                Check(block.GetFloat("_OutlineEnabled") == 1f && block.GetColor("_OutlineColor") == Color.white,
                    "Proximity unhighlight must preserve white guidance outline");
            }

            selected.OnPlayerInteract(player);
            Check(RunMapEventProgress.IsEventCompleted(data, "buffy_health_time"), "Successful grant must complete event");
            Check(popup.Requests.Count == 1, "One successful grant must create exactly one popup");
            string expected = choice == BuffyWorkoutType.Strength ? "<color=#FF9933>공격력 +10</color>"
                : choice == BuffyWorkoutType.Wheel ? "<color=#A6DFFF>이동속도 +15%</color>"
                : "<color=#B2FF99>레벨업 !</color>";
            Check(popup.Requests[0].TextOverride == expected, "Reward text/color must match choice");
            Check(popup.Requests[0].Kind == DamagePopupKind.Text, "Must use existing damage text path");
            Check(Vector3.Distance(popup.Requests[0].WorldPosition, player.transform.position + Vector3.up) < .001f, "Popup must appear above player");
            Check(Mathf.Approximately(attributes.GetBaseValue(attack), attackBefore + (choice == BuffyWorkoutType.Strength ? 10 : 0)), "Attack must increase only for Strength");
            Check(Mathf.Approximately(attributes.GetBaseValue(speed), speedBefore + (choice == BuffyWorkoutType.Wheel ? .15f : 0)), "Speed must increase only for Wheel");
            Check(data.levelProgression.level == levelBefore + (choice == BuffyWorkoutType.Log ? 1 : 0), "Experience choice must level up");
            Color dimmed = Body(selected).color;
            Check(Mathf.Abs(dimmed.r - .7f) < .001f && Mathf.Abs(dimmed.g - .7f) < .001f &&
                Mathf.Abs(dimmed.b - .7f) < .001f && Mathf.Abs(dimmed.a - .8f) < .001f, "Selected RGB must be 70%, alpha preserved");
            foreach (var equipment in group)
            {
                Check(!Arrow(equipment).gameObject.activeSelf, "Selection must clear every arrow");
                if (equipment == selected) continue;
                Check(!Body(equipment).enabled && !equipment.CanInteract(player), "Unselected body and interaction must stop immediately");
                Check(Get<ParticleSystem>(equipment, "dustParticle").isPlaying, "Unselected dust must play");
            }
            selected.OnPlayerInteract(player);
            Check(popup.Requests.Count == 1, "Repeat use must not grant another reward");
            float until = Time.realtimeSinceStartup + 8f;
            while (group.Any(e => e != selected && e.gameObject.activeSelf) && Time.realtimeSinceStartup < until) yield return null;
            Check(group.Count(e => e.gameObject.activeSelf) == 1, "Only selected equipment must remain after dust finishes");

            GameObject restored = CreateFixture(out var restoredGroup);
            yield return null;
            Check(restoredGroup.Count(e => e.gameObject.activeSelf) == 1 && restoredGroup.Single(e => e.gameObject.activeSelf).WorkoutType == choice,
                "Reentry must restore the selected equipment without replaying rewards");
            Check(restoredGroup.All(e => !Get<ParticleSystem>(e, "dustParticle").isPlaying), "Reentry must not replay dust");
            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(restored);
            UnityEngine.Object.Destroy(playerObject);
            UnityEngine.Object.Destroy(catalog);
            yield return null;
        }

        data.completedRunMapEventIds.Clear();
        popup.Requests.Clear();
        GameObject failureRoot = CreateFixture(out var failureGroup);
        var failurePlayer = new GameObject("Failure player").AddComponent<BuffyWorkoutProbePlayer>();
        var strength = failureGroup.Single(e => e.WorkoutType == BuffyWorkoutType.Strength);
        Set(strength, "attackBaseAttribute", null);
        yield return null;
        strength.OnPlayerInteract(failurePlayer);
        var log = failureGroup.Single(e => e.WorkoutType == BuffyWorkoutType.Log);
        data.levelProgression.level = Get<LevelProgressionConfigSO>(log, "levelProgressionConfig").MaxLevel;
        log.OnPlayerInteract(failurePlayer);
        Check(!RunMapEventProgress.IsEventCompleted(data, "buffy_health_time") && popup.Requests.Count == 0,
            "Missing attributes and max level must not complete or show success");
        Check(failureGroup.All(e => Body(e).enabled && !Get<ParticleSystem>(e, "dustParticle").isPlaying), "Failed rewards must leave all equipment visible");

        data.levelProgression = new LevelProgressionState();
        log.OnPlayerInteract(failurePlayer);
        failureRoot.SetActive(false);
        failureRoot.SetActive(true);
        yield return null;
        Check(failureGroup.Where(e => e != log).All(e => !e.gameObject.activeSelf && !Get<ParticleSystem>(e, "dustParticle").isPlaying),
            "Disabling during disappearance must clear particles and keep discarded equipment hidden");
        UnityEngine.Object.Destroy(failureRoot);
        UnityEngine.Object.Destroy(failurePlayer.gameObject);
    }

    private static GameObject CreateFixture(out BuffyHealthTimeInteractable[] group)
    {
        var host = new GameObject("Buffy fixture");
        host.SetActive(false);
        var module = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), host.transform);
        foreach (var component in module.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(component is BuffyHealthTimeInteractable)) component.enabled = false;
        // NPC nameplate layout is outside this fixture; its UI services/fonts are not bootstrapped here.
        foreach (var text in module.GetComponentsInChildren<TMPro.TMP_Text>(true)) text.gameObject.SetActive(false);
        group = module.GetComponentsInChildren<BuffyHealthTimeInteractable>(true);
        Check(group.Length == 3, "Prefab must contain three equipment choices");
        foreach (var equipment in group)
        {
            Set(equipment, "npcSpeechBubble", null);
            Body(equipment).color = new Color(1, 1, 1, .8f);
            Check(Get<BuffyHealthTimeInteractable[]>(equipment, "equipmentGroup").Length == 3, "Group must be wired");
            Check(Get<ParticleSystem>(equipment, "dustParticle") != null && Arrow(equipment) != null, "Authored presentation must be wired");
        }
        host.SetActive(true);
        return host;
    }

    private static SpriteRenderer Body(BuffyHealthTimeInteractable equipment) => Get<SpriteRenderer[]>(equipment, "highlightedRenderers")[0];
    private static Transform Arrow(BuffyHealthTimeInteractable equipment) => Get<Transform>(equipment, "guidanceArrow");
    private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, Hidden).GetValue(owner);
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Hidden).SetValue(owner, value);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    private sealed class PopupProbe : IDamagePopupBackend
    {
        public readonly List<DamagePopupRequest> Requests = new();
        public void Show(DamagePopupRequest request) => Requests.Add(request);
    }

    private sealed class MemorySave : IGameDataStoreBackend
    {
        public GameData Data { get; } = new GameData();
        public int ActiveSlotIndex => 0;
        public event Action<GameData, int> OnDataLoaded { add { } remove { } }
        public GameData EnsureData() => Data;
        public void SaveData() { }
        public void RequestImmediateSave(UnityEngine.Object requester) { }
        public void RequestDeferredSave(UnityEngine.Object requester) { }
        public void FlushSave(UnityEngine.Object requester) { }
    }
}

public sealed class BuffyWorkoutProbePlayer : MonoBehaviour, IPlayerInteractor
{
    public Transform Transform => transform;
    public InteractState CurrentState { get; private set; } = InteractState.Idle;
    public void SetInteractState(InteractState state) => CurrentState = state;
}
