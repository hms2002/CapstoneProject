// Run in an isolated Unity Editor project with freshly built project assemblies.
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityGAS;

public static class StatusTelegraphRecoveryRegression
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    public static void Run()
    {
        try
        {
            Geometry();
            Hud();
            Debug.Log("STATUS_TELEGRAPH_PASS: world geometry and borders invariant under scale/reflection/shear; wall endpoint correct; progress projection/count/percent/reset and slot reuse passed.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void Geometry()
    {
        var parent = new GameObject("Scaled caster");
        var nested = new GameObject("Rotated service"); nested.transform.SetParent(parent.transform, false);
        var host = new GameObject("Warning"); host.transform.SetParent(nested.transform, false);
        var view = host.AddComponent<AttackTelegraphView>();
        var wall = new GameObject("Wall"); wall.transform.position = new Vector3(5, 0, 0);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(1, 30);
        Physics2D.SyncTransforms();
        var specs = new[] {
            AttackTelegraphSpec.CreateLine(Vector3.zero, Vector3.right * 8, 1, 1).WithWallClipping(1, 12, 0.03f),
            AttackTelegraphSpec.CreateRectangle(new Vector3(1,2,0), new Vector2(4,2), 25, 1),
            AttackTelegraphSpec.CreateCircle(Vector3.zero, 4, 1),
            AttackTelegraphSpec.CreateRing(Vector3.zero, 8, 2, 1).WithWallClipping(1, 12, 0.03f),
            AttackTelegraphSpec.CreateSector(Vector3.zero, 8, 90, 0, 1).WithWallClipping(1, 12, 0.03f)
        };
        foreach (var spec in specs)
        {
            parent.transform.localScale = Vector3.one;
            nested.transform.localRotation = Quaternion.identity;
            view.Show(spec);
            Vector3[] expected = WorldVertices(host);
            Vector3[][] expectedBorders = Borders(host);
            if (spec.shape == AttackTelegraphShape.Line)
            {
                float maxX = float.MinValue;
                foreach (var v in expected) maxX = Mathf.Max(maxX, v.x);
                Check(Mathf.Abs(maxX - 4.47f) < 0.005f, "Line failed to clip to the near wall face");
            }
            foreach (var scale in new[] {new Vector3(2,2,1), new Vector3(-2,0.5f,1), new Vector3(0.7f,3,1)})
            {
                parent.transform.localScale = scale;
                nested.transform.localRotation = Quaternion.Euler(0,0,33);
                view.UpdateGeometry(spec);
                Equal(expected, WorldVertices(host), spec.shape + " mesh");
                var borders = Borders(host);
                Check(borders.Length == expectedBorders.Length, "Border count changed");
                for (int i=0;i<borders.Length;i++) Equal(expectedBorders[i], borders[i], spec.shape + " border");
                // Style animation rebuilds use the same geometry conversion path.
                host.GetComponent<AttackTelegraphWallClippedMeshView>().ApplyStyle(null, 0.5f);
                Equal(expected, WorldVertices(host), spec.shape + " animated mesh");
            }
            view.HideImmediate();
            Check(!host.GetComponent<MeshRenderer>().enabled, "Hide left mesh enabled");
        }
        UnityEngine.Object.DestroyImmediate(parent);
        UnityEngine.Object.DestroyImmediate(wall);
    }

    static Vector3[] WorldVertices(GameObject host)
    {
        var points = host.GetComponent<MeshFilter>().sharedMesh.vertices;
        for (int i=0;i<points.Length;i++) points[i] = host.transform.TransformPoint(points[i]);
        return points;
    }
    static Vector3[][] Borders(GameObject host)
    {
        var result = new System.Collections.Generic.List<Vector3[]>();
        foreach (var line in host.GetComponentsInChildren<LineRenderer>())
        {
            if (!line.enabled) continue;
            var points = new Vector3[line.positionCount]; line.GetPositions(points);
            if (!line.useWorldSpace) for (int i=0;i<points.Length;i++) points[i] = line.transform.TransformPoint(points[i]);
            result.Add(points);
        }
        return result.ToArray();
    }
    static void Equal(Vector3[] expected, Vector3[] actual, string label)
    {
        Check(expected.Length == actual.Length, label + " vertex count");
        for (int i=0;i<expected.Length;i++) Check(Vector3.Distance(expected[i],actual[i]) < 0.001f, label + " scaled point " + i);
    }

    static void Hud()
    {
        var definition = ScriptableObject.CreateInstance<StatusHudDefinition>();
        var active = new ActiveStatusEntry(1, new StatusApplyRequest(definition, "curse", progressText: "0/10"));
        Check(active.ToHudEntry().ProgressText == "0/10", "Zero progress lost");
        var host = new GameObject("Authored slot fixture", typeof(RectTransform)); host.SetActive(false);
        var view = host.AddComponent<StatusHudEntryView>();
        Set(view, "backgroundImage", host.AddComponent<Image>());
        foreach (var field in new[] {"iconImage", "durationFillImage"})
        {
            var child = new GameObject(field, typeof(RectTransform), typeof(Image)); child.transform.SetParent(host.transform, false);
            Set(view, field, child.GetComponent<Image>());
        }
        TMP_Text stack = null;
        foreach (var field in new[] {"stackText", "durationText"})
        {
            var child = new GameObject(field, typeof(RectTransform), typeof(TextMeshProUGUI)); child.transform.SetParent(host.transform, false);
            var text = child.GetComponent<TMP_Text>(); text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 13; text.alignment = TextAlignmentOptions.TopRight;
            Set(view, field, text); if (field == "stackText") stack = text;
        }
        var originalAnchor = stack.rectTransform.anchorMin;
        var originalOutline = stack.outlineWidth;
        view.Bind(active.ToHudEntry());
        Check(stack.text == "0/10" && stack.alignment == TextAlignmentOptions.Center, "Count not centered");
        Check(stack.outlineWidth > 0 && ((Color)stack.outlineColor).r < 0.01f, "Missing black outline");
        active.Apply(new StatusApplyRequest(definition, "curse", progressText: "45%"));
        view.Bind(active.ToHudEntry()); Check(stack.text == "45%", "Percentage lost");
        active.Apply(new StatusApplyRequest(definition, "buff", stackCount: 3, showStacksOverride: true));
        view.Bind(active.ToHudEntry());
        Check(stack.text == "3" && stack.alignment == TextAlignmentOptions.TopRight, "Reused slot retained progress");
        Check(stack.rectTransform.anchorMin == originalAnchor && Mathf.Approximately(stack.outlineWidth,originalOutline), "Original layout/style not restored");
        active.Apply(new StatusApplyRequest(definition, "completed", showStacksOverride: false));
        view.Bind(active.ToHudEntry()); Check(stack.text == "", "Completed curse retained text");
        UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(definition);
    }
}
