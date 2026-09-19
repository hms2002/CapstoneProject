using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Isolated native Unity fixture; does not run the project's bootstrap or touch the open scene.
public static class TutorialOpeningRegression
{
    public static void Run()
    {
        SessionState.SetBool("TutorialOpeningRegression", true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Subscribe()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("TutorialOpeningRegression", false)) return;
            SessionState.SetBool("TutorialOpeningRegression", false);
            new GameObject("Opening test driver").AddComponent<TutorialOpeningRegressionDriver>();
        };
    }
}

public sealed class TutorialOpeningRegressionDriver : MonoBehaviour
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Set(object instance, string name, object value) =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);

    private IEnumerator Start()
    {
        IEnumerator test = Exercise();
        while (true)
        {
            object current;
            try { if (!test.MoveNext()) break; current = test.Current; }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); yield break; }
            yield return current;
        }
        Debug.Log("TUTORIAL_OPENING_PASS: landing/contact wake, owner cancellation, portal recovery, physics/pose restore, normalized-start input handoff, external camera ownership.");
        EditorApplication.Exit(0);
    }

    private IEnumerator Exercise()
    {
        var player = new GameObject("Arrival fixture");
        player.transform.position = new Vector3(8f, -6f, 0f);
        var body = player.AddComponent<Rigidbody2D>(); body.gravityScale = 0f;
        var collider = player.AddComponent<BoxCollider2D>();
        player.AddComponent<SpriteRenderer>();
        var arrival = player.AddComponent<PlayerHubSpawnPresentation2D>();
        Set(arrival, "fallDuration", .05f);
        Set(arrival, "autoWakeWithoutInput", true);
        Set(arrival, "autoWakeDelaySeconds", 0f);
        Set(arrival, "landingLockSeconds", 0f);
        int hubCompletions = 0;
        arrival.PresentationCompleted += _ => hubCompletions++;
        object owner = new object();
        bool landed = false, wake = false;
        Check(arrival.TryPlayScriptedArrival(owner, () => landed = true, () => wake), "Arrival did not start");
        float deadline = Time.realtimeSinceStartup + 3f;
        while (!landed && Time.realtimeSinceStartup < deadline) yield return null;
        Check(landed, "Never landed");
        yield return new WaitForSeconds(.15f);
        Check(arrival.IsPlaying && Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.z, 90f)) < .1f,
            "Auto wake escaped the scripted hold");
        Check(Vector3.Distance(player.transform.position, new Vector3(8, -6, 0)) < .001f, "Landing position drifted");
        Check(body.simulated && collider.enabled, "Landing did not restore physics");
        arrival.CancelPortalArrival(new object());
        Check(arrival.IsPlaying, "A different owner cancelled arrival");
        // Only contact with this shot's designated hurtbox may wake the player.
        player.AddComponent<UnityGAS.CombatHurtbox2D>();
        var projectileHost = new GameObject("Cinematic projectile");
        projectileHost.AddComponent<CircleCollider2D>().isTrigger = true;
        var projectile = projectileHost.AddComponent<UnityGAS.LightBeadProjectile2D>();
        int contacts = 0;
        projectile.BindPresentationImpact(player, () => { contacts++; wake = true; });
        var contactMethod = typeof(UnityGAS.LightBeadProjectile2D).GetMethod("TryPresentationImpact", BindingFlags.Instance | BindingFlags.NonPublic);
        var other = new GameObject("Unrelated hurtbox");
        var otherCollider = other.AddComponent<BoxCollider2D>();
        other.AddComponent<UnityGAS.CombatHurtbox2D>();
        Check(!(bool)contactMethod.Invoke(projectile, new object[] { otherCollider }) && !wake,
            "An unrelated collision woke the player");
        Check((bool)contactMethod.Invoke(projectile, new object[] { collider }) && wake, "Designated contact failed to wake");
        contactMethod.Invoke(projectile, new object[] { collider });
        Check(contacts == 1, "Multiple colliders repeated the wake notification");
        Destroy(other);
        yield return null; yield return null;
        Check(!arrival.IsPlaying && Quaternion.Angle(player.transform.rotation, Quaternion.identity) < .1f, "Explicit wake failed");
        Check(hubCompletions == 0, "Scripted arrival emitted Hub completion");

        Check(arrival.TryPlayPortalArrival(owner, new Vector3(8, 2, 0), null), "Normal portal failed");
        deadline = Time.realtimeSinceStartup + 3f;
        while (arrival.IsPlaying && Time.realtimeSinceStartup < deadline) yield return null;
        Check(!arrival.IsPlaying, "Normal portal lost auto wake");
        Check(arrival.TryPlayScriptedArrival(owner, null, () => false), "Second scripted arrival failed");
        arrival.CancelPortalArrival(owner);
        Check(!arrival.IsPlaying && body.simulated && collider.enabled, "Cancel leaked physics state");
        Check(Quaternion.Angle(player.transform.rotation, Quaternion.identity) < .1f, "Cancel leaked lying pose");
        Check(arrival.TryPlayScriptedArrival(owner, null, () => false), "Third scripted arrival failed");
        arrival.enabled = false;
        Check(!arrival.IsPlaying && body.simulated && collider.enabled, "Disable leaked arrival state");
        // Reproduce the direct-start normalizer releasing protection during letterbox startup.
        arrival.enabled = true;
        var sensor = new GameObject("Authored interaction sensor");
        sensor.transform.SetParent(player.transform, false);
        sensor.AddComponent<BoxCollider2D>().isTrigger = true;
        sensor.AddComponent<PlayerInteractionSensor2D>();
        var interactor = player.AddComponent<PlayerInteractor2D>();
        var intent = player.AddComponent<PlayerIntentInput2D>();
        var protection = player.AddComponent<PlayerCinematicProtection>();
        var opening = new PrototypeTutorialOpeningSequence();
        Set(opening, "enabled", true);
        Set(opening, "protection", protection);
        Set(opening, "playerTransform", player.transform);
        protection.Acquire(opening);
        protection.ForceReleaseAll();
        Check(intent.enabled, "Normalizer did not restore the initial input snapshot");
        // The corrected sequence acquires AFTER yielding letterbox startup, not before it.
        protection.Acquire(opening);
        bool wakeForControl = false, landedForControl = false;
        var cameraBackend = new OpeningCameraProbe();
        GameplayCameraFocusPlayback.RegisterBackend(cameraBackend);
        Check(arrival.TryPlayScriptedArrival(opening, () => landedForControl = true,
            () => wakeForControl, useExternalCamera: true), "Externally framed arrival failed");
        deadline = Time.realtimeSinceStartup + 3f;
        while (!landedForControl && Time.realtimeSinceStartup < deadline) yield return null;
        Check(landedForControl, "Control fixture never landed");
        wakeForControl = true;
        while (arrival.IsPlaying && Time.realtimeSinceStartup < deadline) yield return null;
        Check(!intent.enabled, "Early wake released movement before cinematic handoff");
        Check(cameraBackend.Captures == 0, "Arrival stole externally authored camera framing");
        interactor.SetInteractState(InteractState.None);
        var release = typeof(PrototypeTutorialOpeningSequence).GetMethod("ReleaseCinematicControl", BindingFlags.Instance | BindingFlags.NonPublic);
        release.Invoke(opening, null);
        Check(intent.enabled && interactor.CurrentState == InteractState.Idle && !opening.IsPending,
            "Cinematic handoff did not restore movement and interaction after normalization");
        opening.Cancel();
        Check(intent.enabled, "Post-escape cleanup reapplied a stale input snapshot");
        GameplayCameraFocusPlayback.RegisterBackend(null);
        Destroy(player);
    }

    private sealed class OpeningCameraProbe : IGameplayCameraFocusBackend
    {
        public int Captures;
        public IGameplayCameraFocusSession Capture(Component owner) { Captures++; return null; }
    }
}
