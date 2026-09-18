using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class TutorialBossHudRegression
{
    public static void Run()
    {
        try
        {
            var host = new GameObject("Suppressed boss HUD", typeof(RectTransform));
            host.SetActive(false);
            var hud = host.AddComponent<BossHudController>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var initialized = typeof(BossHudController).GetField("_hasAppliedInitialSlideState", flags);
            initialized.SetValue(hud, true);
            typeof(BossHudController).GetMethod("ApplySlidePresentation", flags).Invoke(hud, new object[] { true });
            if ((bool)initialized.GetValue(hud)) throw new Exception("Inactive HUD consumed its entrance request");
            initialized.SetValue(hud, true);
            typeof(BossHudController).GetMethod("OnDisable", flags).Invoke(hud, null);
            if ((bool)initialized.GetValue(hud)) throw new Exception("Interrupted entrance was not invalidated");
            UnityEngine.Object.DestroyImmediate(host);
            Debug.Log("TUTORIAL_BOSS_HUD_PASS: deferred inactive entrance and reset on disable.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
