using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.Cinemachine;

public static class CameraFocusQueryRegression
{
    public static void Run()
    {
        try
        {
            var host = new GameObject("Camera query fixture"); host.SetActive(false);
            var bootstrap = host.AddComponent<CameraBootstrap>();
            typeof(CameraBootstrap).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, bootstrap);
            var mainObject = new GameObject("Fixture main camera");
            var main = mainObject.AddComponent<Camera>();
            var brain = mainObject.AddComponent<CinemachineBrain>();
            var rig = new GameObject("Fixture virtual camera").AddComponent<CinemachineCamera>();
            var anchor = new GameObject("Opening point").transform;
            anchor.position = new Vector3(-12, 14, 0);
            Set(bootstrap, "runtimeMainCamera", main);
            Set(bootstrap, "runtimeBrain", brain);
            Set(bootstrap, "runtimePlayerCam", rig);
            rig.Follow = anchor;
            rig.LookAt = anchor;
            rig.Priority = 4321;
            for (int i = 0; i < 5; i++)
            {
                Check(CameraBootstrap.GetMainCamera() == main, "Main camera identity changed");
                Check(CameraBootstrap.GetPlayerCamera() == rig, "Player rig identity changed");
                Check(CameraBootstrap.GetBrain() == brain, "Brain identity changed");
                Check(CameraBootstrap.GetLegacyFollow() == null, "Optional legacy follow unexpectedly created");
                Check(rig.Follow == anchor && rig.LookAt == anchor, "Read queries replaced cinematic target");
                Check(rig.Priority == 4321, "Read queries reset cinematic priority");
            }
            Debug.Log("CAMERA_FOCUS_QUERY_PASS: repeated camera/brain/legacy queries preserve cinematic target and priority, including absent optional legacy follower.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private static void Set(object instance, string field, object value) =>
        instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
