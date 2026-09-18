using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
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
            Check(door.IsOpen && !obstacle.enabled, "Chest open must open door and release collision before item confirmation");
            Check(tutorial.Stage == 0, "Opening door must not advance tutorial stage");
            Call(tutorial, "OnDisable"); host.SetActive(false);
            Check(typeof(TreasureChest).GetField("FirstOpenedUi", Flags).GetValue(chest) == null, "Chest subscription leaked on disable");
            door.ForceClose(immediate: true); host.SetActive(true); Call(tutorial, "OnEnable");
            Check(door.IsOpen && !obstacle.enabled, "Already-open chest must restore the open door");
            Call(tutorial, "OnDisable"); host.SetActive(false);
            Debug.Log("TUTORIAL_PORTAL_DOOR_PASS: chest event, obstacle release, stage preservation, unsubscribe, reenable.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
