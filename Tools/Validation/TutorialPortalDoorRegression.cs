using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityGAS;
public static class TutorialPortalDoorRegression
{
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
    static void Call(object o, string n) => o.GetType().GetMethod(n, Flags).Invoke(o, null);
    static void Check(bool c, string m) { if (!c) throw new Exception(m); }
    public static void Run()
    {
        try
        {
            OpeningWound();
            var doorHost = new GameObject("Door"); doorHost.SetActive(false);
            var obstacle = doorHost.AddComponent<BoxCollider2D>();
            var door = doorHost.AddComponent<DoorObject>();
            door.doorID = "TutorialDoorTest"; door.isPermanent = false; door.obstacleCollider = obstacle;
            doorHost.SetActive(true); Call(door, "Awake");
            var chestHost = new GameObject("Chest"); chestHost.SetActive(false);
            var chest = chestHost.AddComponent<TreasureChest>();
            var host = new GameObject("Tutorial"); host.SetActive(false);
            var tutorial = host.AddComponent<PrototypeTutorialUpgrade>();
            Set(tutorial, "skillTargets", Array.Empty<Transform>()); Set(tutorial, "gates", Array.Empty<GameObject>());
            Set(tutorial, "portalDoorChest", chest); Set(tutorial, "bossPortalDoor", door);
            host.SetActive(true); Call(tutorial, "Awake"); Call(tutorial, "OnEnable");
            Check(!door.IsOpen && obstacle.enabled, "Door must block before opening chest");
            Set(chest, "isOpened", true); Call(chest, "RaiseOpenedUiEvents");
            Check(!door.IsOpen && obstacle.enabled, "Opening chest must not unlock portal");
            Set(tutorial, "stage", 4); tutorial.CompleteChestTutorial();
            Check(tutorial.Stage == 5 && !door.IsOpen, "Chest confirmation must wait for inventory inspection");
            tutorial.CompleteInventoryTutorial();
            Check(tutorial.Stage == 6 && !door.IsOpen && obstacle.enabled, "Inventory return must wait for potion use");
            tutorial.CompletePotionTutorial();
            Check(tutorial.Stage == 7 && door.IsOpen && !obstacle.enabled, "Potion completion must open door");
            tutorial.CompleteInventoryTutorial();
            tutorial.CompletePotionTutorial();
            Check(tutorial.Stage == 7, "Completion must be idempotent");
            Call(tutorial, "OnDisable"); host.SetActive(false);
            Check(typeof(TreasureChest).GetField("FirstOpenedUi", Flags).GetValue(chest) == null, "Chest subscription leaked on disable");
            door.ForceClose(immediate: true); host.SetActive(true); Call(tutorial, "OnEnable");
            Check(!door.IsOpen && obstacle.enabled, "Restarted tutorial must not use old chest-open state to unlock door");
            Call(tutorial, "OnDisable"); host.SetActive(false);
            Debug.Log("TUTORIAL_PORTAL_DOOR_PASS: chest/inventory do not unlock; potion completion opens obstacle; idempotence; restart.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private static void OpeningWound()
    {
        var host = new GameObject("Opening wound"); host.SetActive(false);
        var player = host.AddComponent<AbilitySystem>();
        var health = ScriptableObject.CreateInstance<AttributeDefinition>(); health.defaultBaseValue = health.maxValue = 10;
        var maximum = ScriptableObject.CreateInstance<AttributeDefinition>(); maximum.defaultBaseValue = maximum.maxValue = 10;
        var catalog = ScriptableObject.CreateInstance<AttributeCatalogSO>(); Set(catalog, "attributes", new[] { health, maximum });
        var attributes = host.AddComponent<AttributeSet>(); Set(attributes, "attributeCatalog", catalog);
        attributes.InitializeFromInitialData();
        var recovery = host.AddComponent<TutorialPlayerHealthAutoRecover>();
        Set(recovery, "attributeSet", attributes); Set(recovery, "healthAttribute", health); Set(recovery, "maxHealthAttribute", maximum);
        Call(recovery, "SubscribeAttributeSet");
        var damage = ScriptableObject.CreateInstance<GE_Damage_Spec>(); damage.healthAttribute = health;
        var tutorial = host.AddComponent<PrototypeTutorialUpgrade>();
        Set(tutorial, "skillTargets", Array.Empty<Transform>()); Call(tutorial, "Awake");
        Set(tutorial, "player", player); Set(tutorial, "damageEffect", damage); Set(tutorial, "healthRecovery", recovery);
        Set(tutorial, "gates", Array.Empty<GameObject>());
        tutorial.ApplyOpeningShotDamage(); tutorial.ApplyOpeningShotDamage();
        Check(attributes.GetCurrentValue(health) == 9, "Opening shot must deal exactly one HP once");
        attributes.TrySetCurrentValue(health, 3, tutorial); recovery.RestoreNow();
        Check(attributes.GetCurrentValue(health) == 9, "Automatic recovery must preserve the potion wound");
        attributes.TrySetCurrentValue(health, 10, tutorial);
        Check(attributes.GetCurrentValue(health) == 9, "Other healing must not erase the lesson wound");
        Set(tutorial, "stage", 5); tutorial.CompleteInventoryTutorial();
        var potion = ScriptableObject.CreateInstance<ConsumableDefinition>(); Set(potion, "targetAttribute", health);
        Check(potion.TryUse(host) && attributes.GetCurrentValue(health) == 10, "Potion must heal after wound reservation is released");
        Call(recovery, "UnsubscribeAttributeSet"); Call(tutorial, "OnDisable");
        UnityEngine.Object.DestroyImmediate(host);
        Debug.Log("TUTORIAL_OPENING_WOUND_PASS: one HP once; recovery preserves deficit; potion heals after inventory.");
    }
}
