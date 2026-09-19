// Run in an isolated Unity Editor with the project's freshly built assemblies.
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityGAS;

public static class EgoSwordRecallRegression
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object o, string field, object value) => o.GetType().GetField(field, Flags).SetValue(o, value);
    private static object Call(object o, string method) => o.GetType().GetMethod(method, Flags).Invoke(o, null);
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private sealed class Handle : IAttackTelegraphHandle
    {
        public bool IsVisible => true;
        public int Releases;
        public AttackTelegraphSpec Geometry;
        public void Show(AttackTelegraphSpec spec) { }
        public void UpdateGeometry(AttackTelegraphSpec spec) { Geometry = spec; }
        public void HideImmediate() { }
        public void Release() { Releases++; }
    }
    public static void Run()
    {
        GameObject host = null;
        try
        {
            host = new GameObject("Recall fixture");
            host.SetActive(false);
            var sword = host.AddComponent<EgoSwordActor>();
            Check(Mathf.Abs((float)typeof(EgoSwordActor).GetField("recallLiftHoldSeconds", Flags).GetValue(sword) - .84f) < .001f, "Preparation default");
            host.transform.position = new Vector3(2, 3);
            Set(sword, "recallWarningStart", new Vector2(2, 5.2f));
            Set(sword, "recallTargetLocalOffset", new Vector3(8, 2.2f));
            Set(sword, "recallWarningDuration", 3f);
            var path = new Handle(); var arrival = new Handle();
            Set(sword, "recallPathWarning", path); Set(sword, "recallArrivalWarning", arrival);
            Call(sword, "UpdateRecallWarnings");
            Check(Vector2.Distance(path.Geometry.center, new Vector2(6, 5.2f)) < .001f, "Warning must start at lifted position");
            Check(Mathf.Abs(path.Geometry.size.x - 8.9f) < .001f && Mathf.Abs(path.Geometry.size.y - .9f) < .001f, "Contact radius coverage");
            Check(Mathf.Abs(arrival.Geometry.size.x - 3.2f) < .001f && arrival.Geometry.size.x == arrival.Geometry.size.y, "Arrival must use actual circle damage radius");
            Set(sword, "recallMovementActive", true);
            host.transform.position = new Vector3(4, 5.2f);
            Set(sword, "recallTargetLocalOffset", new Vector3(0, 6));
            Call(sword, "UpdateRecallWarnings");
            Check(Mathf.Abs(path.Geometry.rotationDeg - 90f) < .001f && Mathf.Abs(path.Geometry.size.x - 6.9f) < .001f, "Moving path and target update");
            Check(sword.EstimateRecallTimeoutSeconds(100) >= 2.09f, "Timeout must cover longer preparation and return");
            Call(sword, "ResetRecallReadiness"); Call(sword, "ResetRecallReadiness");
            Check(path.Releases == 1 && arrival.Releases == 1, "Cleanup must release each handle exactly once");
            path = new Handle(); arrival = new Handle();
            Set(sword, "recallPathWarning", path); Set(sword, "recallArrivalWarning", arrival);
            Call(sword, "OnDisable");
            Check(path.Releases == 1 && arrival.Releases == 1, "Disable leaked warnings");
            Debug.Log("EGO_SWORD_RECALL_PASS: preparation, lifted and moving geometry, contact/arrival radii, timeout, idempotent cleanup and disable.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        finally { if (host != null) UnityEngine.Object.DestroyImmediate(host); }
    }
}
