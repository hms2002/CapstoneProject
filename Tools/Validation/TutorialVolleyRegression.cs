using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class TutorialVolleyRegression
{
    public static void Run() { SessionState.SetBool("VolleyTest", true); EditorApplication.EnterPlaymode(); }
    [InitializeOnLoadMethod] private static void Register() => EditorApplication.playModeStateChanged += state =>
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("VolleyTest", false)) return;
        SessionState.SetBool("VolleyTest", false);
        new GameObject("Volley test").AddComponent<TutorialVolleyRegressionDriver>();
    };
}
public sealed class TutorialVolleyRegressionDriver : MonoBehaviour
{
    private static void Set(object o, string key, object value) => o.GetType().GetField(key, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, value);
    private static object Call(object o, string key, params object[] args) => o.GetType().GetMethod(key, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, args);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
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
        Debug.Log("TUTORIAL_VOLLEY_PASS: continuous camera travel, exact arrival, faster return, cancellation restore and prompt release.");
        EditorApplication.Exit(0);
    }
    private IEnumerator Exercise()
    {
        var host = new GameObject("Inactive director"); host.SetActive(false);
        var director = host.AddComponent<PrototypeTutorialUpgrade>();
        var anchor = new GameObject("Authored camera point").transform;
        var target = new GameObject("Gunner").transform; target.position = new Vector3(0,21,0);
        var opening = new PrototypeTutorialOpeningSequence(); Set(opening, "openingCameraPoint", anchor);
        Set(director, "opening", opening);
        var camera = new CameraSession(); Set(director, "volleyCamera", camera);
        Set(director, "volleyCinematic", true); Set(director, "volleyAnchorPosition", new Vector3(8,-6,0));
        Set(director, "bulletConsumed", new bool[] { false });
        var pan = (IEnumerator)Call(director, "PanVolleyCamera", target, .65f, false);
        float started = Time.time;
        while (pan.MoveNext()) yield return pan.Current;
        float outbound = Time.time - started;
        Check(camera.Samples > 2 && camera.SawIntermediate, "Camera teleported instead of travelling");
        Check(Vector3.Distance(camera.CurrentCenter, target.position) < .001f, "Did not reach gunner");
        target.position = new Vector3(0,1,0);
        pan = (IEnumerator)Call(director, "PanVolleyCamera", target, .25f, false);
        started = Time.time;
        while (pan.MoveNext()) yield return pan.Current;
        Check(Time.time - started < outbound, "Return was not faster");
        Check(Vector3.Distance(camera.CurrentCenter, target.position) < .001f, "Did not return to player");
        Call(director, "CleanupVolleyCinematic");
        Check(camera.Restored && !director.IsOpeningCinematic, "Camera or prompt remained captured");
        Check(anchor.position == new Vector3(8,-6,0), "Authored point was not restored");
        Call(director, "CleanupVolleyCinematic");
    }
    private sealed class CameraSession : IGameplayCameraFocusSession
    {
        public int Samples; public bool SawIntermediate, Restored;
        public Transform CachedFollow => null;
        public bool HasOrthographicSize => true;
        public float CachedOrthographicSize => 6;
        public float CurrentOrthographicSize => 6;
        public Vector3 CurrentCenter { get; private set; }
        public void SetTarget(Transform target) { }
        public void SnapToTarget(Transform target) { CurrentCenter = target.position; Samples++; if (CurrentCenter.y > 0 && CurrentCenter.y < 21) SawIntermediate = true; }
        public void SetOrthographicSize(float size) { }
        public IEnumerator WaitForSettle(Transform target) { yield break; }
        public void Restore(Transform target) { Restored = true; }
    }
}
