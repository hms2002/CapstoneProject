using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityGAS;

public static class TutorialDodgeDoorRegression
{
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
    static void Call(object o, string n) => o.GetType().GetMethod(n, Flags).Invoke(o, null);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static GameObject Inactive(string name) { var go = new GameObject(name); go.SetActive(false); return go; }
    public static void Run()
    {
        try
        {
            var host = Inactive("Dodge tutorial");
            var tutorial = host.AddComponent<PrototypeTutorialUpgrade>();
            var doorHost = new GameObject("Entrance door");
            var door = doorHost.AddComponent<DoorObject>();
            door.doorID = "DodgeTest"; door.isPermanent = false;
            door.obstacleCollider = doorHost.AddComponent<BoxCollider2D>();
            Call(door, "Awake");
            Set(tutorial, "dodgeEntranceDoor", door);
            Set(tutorial, "skillTargets", Array.Empty<Transform>());
            Set(tutorial, "gates", Array.Empty<GameObject>());
            var gunner = Inactive("Gunner").AddComponent<GoblinGunner>();
            Set(tutorial, "tutorialGunner", gunner);
            var bullets = new[] { Inactive("Bullet 1").transform, Inactive("Bullet 2").transform };
            Set(tutorial, "bullets", bullets);
            var marker = new GameObject("Entry marker").transform;
            marker.position = new Vector3(0, .5f, 0);
            Set(tutorial, "bulletFireStart", marker);
            Call(tutorial, "Awake"); Call(tutorial, "OnEnable");
            Check(door.IsOpen && !door.obstacleCollider.enabled, "Entry must start open");
            var player = Inactive("Player").AddComponent<AbilitySystem>();
            Set(tutorial, "player", player); Set(tutorial, "stage", 1);
            player.transform.position = Vector3.zero;
            Call(tutorial, "TickGunner");
            Check(door.IsOpen && !bullets[0].gameObject.activeSelf, "Must not fire or close before entry");
            player.transform.position = new Vector3(1, 1, 0);
            Call(tutorial, "TickGunner");
            Check(door.IsOpen, "Side approach must not close door");
            player.transform.position = new Vector3(0, .5f, 0);
            Call(tutorial, "TickGunner");
            Check(!door.IsOpen && door.obstacleCollider.enabled, "Entered door must close and block");
            Check(bullets[0].gameObject.activeSelf && !bullets[1].gameObject.activeSelf, "Must launch exactly one bullet");
            bullets[0].gameObject.SetActive(false);
            Set(tutorial, "bulletConsumed", new[] { true, true });
            for (int i = 0; i < 10; i++) Call(tutorial, "TickGunner");
            Check(!bullets[0].gameObject.activeSelf && !bullets[1].gameObject.activeSelf, "Consumed bullet must not respawn");
            Set(tutorial, "player", null);
            Call(tutorial, "OnDisable"); Call(tutorial, "OnEnable");
            Check(door.IsOpen && !door.obstacleCollider.enabled, "Restart must reopen entrance");
            Set(tutorial, "player", player); Set(tutorial, "stage", 1);
            Call(tutorial, "TickGunner");
            Check(bullets[0].gameObject.activeSelf && !door.IsOpen, "Restart must allow one new shot");
            Debug.Log("TUTORIAL_DODGE_PASS: entry/side gate, close collision, one shot/no recycle, restart reset.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
