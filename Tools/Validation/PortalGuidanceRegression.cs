// Run in an isolated Unity Editor project with the freshly compiled production assemblies.
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class PortalGuidanceRegression
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run()
    {
        try
        {
            var save = new MemorySave();
            GameDataStore.RegisterBackend(save);
            var player = new GameObject("Player").AddComponent<PlayerInteractor2D>();
            var inventory = player.gameObject.AddComponent<WeaponInventory2D>();
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            InteractableBase target = new GameObject("Hub portal").AddComponent<SceneTravelInteractable>();
            var marker = new GameObject("Guidance").AddComponent<PlayerPortalGuidanceView>();
            var icon = new GameObject("Icon").AddComponent<SpriteRenderer>();
            var arrow = new GameObject("Arrow").AddComponent<SpriteRenderer>();
            icon.transform.SetParent(marker.transform);
            arrow.transform.SetParent(marker.transform);
            Set(marker, "target", target.transform);
            Set(marker, "arrow", arrow);
            Set(marker, "portalIcon", icon);
            Set(marker, "requireEquippedWeapon", true);
            Call(marker, "Awake");
            Call(marker, "BindPlayer", player);
            Check(!arrow.enabled && !icon.enabled, "Must start hidden");
            Call(marker, "LateUpdate");
            Check(!arrow.enabled, "Unarmed Hub must hide guidance");

            Set(inventory, "slots", new[] { weapon, null });
            Set(inventory, "activeIndex", 0);
            foreach (var direction in new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left, new Vector2(1, 1).normalized })
            {
                player.transform.position += new Vector3(2, -1, 0);
                var origin = player.transform.position + new Vector3(0, 0.3f, 0);
                target.transform.position = origin + (Vector3)(direction * 8);
                Call(marker, "LateUpdate");
                Check(arrow.enabled && icon.enabled, "Equipped Hub must show both renderers");
                Check(Vector3.Distance(marker.transform.position, origin + (Vector3)(direction * 1.2f)) < 0.001f, "Marker must follow player at chest radius");
                Check(Vector3.Distance(arrow.transform.position, origin + (Vector3)(direction * 1.55f)) < 0.001f, "Arrow must sit outside icon");
                Check(Vector3.Dot(arrow.transform.rotation * Vector3.left, direction) > 0.999f, "Arrow must point toward target");
                Check(Quaternion.Angle(icon.transform.rotation, Quaternion.identity) < 0.001f, "Portal icon must stay upright");
            }
            Set(inventory, "activeIndex", -1);
            Call(marker, "LateUpdate");
            Check(!arrow.enabled, "Losing the weapon must hide Hub guidance again");

            var scribe = new GameObject("Scribe").AddComponent<GrandHallScribeSequence>();
            target = new GameObject("Officer portal").AddComponent<ScenePortal>();
            Set(marker, "target", target.transform);
            Call(marker, "Awake");
            Set(scribe, "slot", save.Data);
            Set(marker, "requireEquippedWeapon", false);
            Set(marker, "scribeSequence", scribe);
            Set(scribe, "busy", false);
            Call(marker, "LateUpdate");
            Check(!arrow.enabled, "Grand Hall requires completed introduction");
            TutorialProgressStore.MarkCompleted("grandhall_scribe_intro_seen", false);
            Set(scribe, "busy", true);
            Call(marker, "LateUpdate");
            Check(!arrow.enabled, "Saved completion must not bypass camera restoration");
            Set(scribe, "busy", false);
            Call(marker, "LateUpdate");
            Check(arrow.enabled, "Completed Grand Hall entry must show without a Hub weapon gate");
            Time.timeScale = 0;
            Call(marker, "LateUpdate");
            Check(!arrow.enabled, "Pause must hide markers");
            Time.timeScale = 1;
            Set(player, "<CurrentState>k__BackingField", InteractState.Talking);
            Call(marker, "LateUpdate");
            Check(!arrow.enabled, "Dialogue interaction must hide markers");
            Set(player, "<CurrentState>k__BackingField", InteractState.Idle);
            target.enabled = false;
            Call(marker, "LateUpdate");
            Check(!arrow.enabled, "Disabled portal must hide marker");
            target.enabled = true;
            Call(marker, "LateUpdate");
            Check(arrow.enabled, "Marker must recover after temporary suppression");
            Call(marker, "OnDisable");
            Check(!arrow.enabled && !icon.enabled, "Disable must hide both renderers");
            Call(marker, "LateUpdate");
            Check(!arrow.enabled, "Disable must release player references");
            Call(marker, "BindPlayer", player);
            Call(marker, "LateUpdate");
            Check(arrow.enabled, "Rebinding a player must restore eligible guidance");
            save.Data = new GameData();
            Call(marker, "LateUpdate");
            Check(!arrow.enabled, "Changing slots must invalidate old sequence readiness");
            Check(save.SaveCalls == 0, "Guidance must never write save state");
            Debug.Log("PORTAL_GUIDANCE_PASS: Hub equipment gate; Grand Hall completion/camera gate; five directions; upright icon; player following; pause, interaction, portal disable, cleanup, rebind and slot replacement.");
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    private static void Set(object owner, string field, object value) =>
        owner.GetType().GetField(field, Hidden).SetValue(owner, value);
    private static void Call(object owner, string method, params object[] args) =>
        owner.GetType().GetMethod(method, Hidden).Invoke(owner, args);
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    private sealed class MemorySave : IGameDataStoreBackend
    {
        public GameData Data { get; set; } = new GameData();
        public int ActiveSlotIndex => 0;
        public int SaveCalls;
        public event Action<GameData, int> OnDataLoaded { add { } remove { } }
        public GameData EnsureData() => Data;
        public void SaveData() => SaveCalls++;
        public void RequestImmediateSave(UnityEngine.Object requester) => SaveCalls++;
        public void RequestDeferredSave(UnityEngine.Object requester) => SaveCalls++;
        public void FlushSave(UnityEngine.Object requester) => SaveCalls++;
    }
}
