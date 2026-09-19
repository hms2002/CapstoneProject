// Run in an isolated Unity Editor project with the layout helper source.
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public sealed class OverheadPromptTestOwner : MonoBehaviour { }

public static class OverheadPromptLayoutRegression
{
    private static readonly Type Layout = AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType("PlayerOverheadPromptLayout")).First(t => t != null);
    private static void Call(string name, params object[] args) => Layout.GetMethod(name,
        BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static float Edge(RectTransform rect, bool top)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        return top ? corners.Max(c => c.y) : corners.Min(c => c.y);
    }

    public static void Run()
    {
        try
        {
            int[][] orders = { new[] {0,1,2}, new[] {0,2,1}, new[] {1,0,2},
                new[] {1,2,0}, new[] {2,0,1}, new[] {2,1,0} };
            foreach (int[] order in orders)
            for (int mask = 0; mask < 8; mask++)
            {
                Call("Reset");
                var canvases = new Canvas[3];
                var rects = new RectTransform[3];
                var owners = new OverheadPromptTestOwner[3];
                for (int i = 0; i < 3; i++)
                {
                    canvases[i] = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
                    canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                    owners[i] = canvases[i].gameObject.AddComponent<OverheadPromptTestOwner>();
                    rects[i] = new GameObject("Prompt", typeof(RectTransform)).GetComponent<RectTransform>();
                    rects[i].SetParent(canvases[i].transform, false);
                    rects[i].sizeDelta = new Vector2(240, i == 0 ? 92 : 50);
                    rects[i].localScale = Vector3.one * (i == 0 ? 1.5f : 1f);
                    rects[i].pivot = new Vector2(.5f, i == 2 ? 0f : .5f);
                }
                Canvas.ForceUpdateCanvases();
                foreach (int i in order)
                {
                    rects[i].gameObject.SetActive((mask & (1 << i)) != 0);
                    rects[i].position = new Vector3(400, 300, 0);
                    if (rects[i].gameObject.activeSelf) Call("Place", owners[i], rects[i], canvases[i], i);
                }
                float previousTop = float.NegativeInfinity;
                for (int i = 0; i < 3; i++)
                {
                    if (!rects[i].gameObject.activeSelf) continue;
                    Check(Edge(rects[i], false) >= previousTop + 11.9f, "Priority overlap or reversed order");
                    previousTop = Edge(rects[i], true);
                }
                // Removal must collapse the remaining rows immediately, without cumulative drift.
                Call("Remove", owners[0]); Call("Remove", owners[1]);
                if ((mask & 4) != 0)
                    Check(Mathf.Abs(rects[2].position.y - 300f) < .01f, "Hidden rows retained a gap");
                foreach (var canvas in canvases) UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                Call("Remove", owners[2]);
            }
            Call("Reset");
            Debug.Log("OVERHEAD_LAYOUT_PASS: 48 visibility/order combinations, scaled heights, mixed pivots, immediate gap collapse and destroyed-owner cleanup.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
