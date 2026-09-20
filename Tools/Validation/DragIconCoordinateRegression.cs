// Execute in an isolated Unity Editor project with freshly built UI/dependency assemblies.
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DragIconCoordinateRegression
{
    private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Fields).SetValue(owner, value);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    public static void Run()
    {
        try
        {
            Exercise();
            Debug.Log("DRAG_ICON_COORDINATE_PASS: overlay, camera canvas, offset viewport, scaled parent, runtime render-mode switch, visible tracking, hide/cancel.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static void Exercise()
    {
        var cameraHost = new GameObject("Coordinate camera", typeof(Camera));
        var camera = cameraHost.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.transform.position = new Vector3(23f, -11f, -10f);
        camera.rect = new Rect(.1f, .15f, .8f, .7f);
        var canvasHost = new GameObject("Coordinate canvas", typeof(RectTransform), typeof(Canvas));
        var canvas = canvasHost.GetComponent<Canvas>();
        var parent = new GameObject("Scaled parent", typeof(RectTransform)).GetComponent<RectTransform>();
        parent.SetParent(canvas.transform, false);
        parent.localScale = new Vector3(.75f, 1.25f, 1f);
        parent.anchoredPosition = new Vector2(35f, -25f);
        var host = new GameObject("Drag", typeof(RectTransform), typeof(CanvasGroup));
        host.SetActive(false);
        host.transform.SetParent(parent, false);
        var image = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(host.transform, false);
        var drag = host.AddComponent<DragIcon>();
        var rect = (RectTransform)host.transform;
        var group = host.GetComponent<CanvasGroup>();
        Set(drag, "rectTransform", rect); Set(drag, "image", image); Set(drag, "canvasGroup", group);
        typeof(DragIcon).GetMethod("Awake", Fields).Invoke(drag, null);
        host.SetActive(true);
        var texture = new Texture2D(4, 4);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
        try
        {
            // The same initialized view must also follow a runtime canvas mode change.
            foreach (var mode in new[] { RenderMode.ScreenSpaceOverlay, RenderMode.ScreenSpaceCamera, RenderMode.ScreenSpaceOverlay })
            {
                canvas.renderMode = mode;
                canvas.worldCamera = mode == RenderMode.ScreenSpaceOverlay ? null : camera;
                canvas.planeDistance = 2f;
                Canvas.ForceUpdateCanvases();
                drag.Show(sprite);
                foreach (var pointer in new[] { new Vector2(120, 140), new Vector2(320, 240), new Vector2(500, 360) })
                {
                    drag.Follow(pointer);
                    Vector2 actual = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.position);
                    Check(Vector2.Distance(actual, pointer) < .1f, $"{mode}: projected icon {actual} does not follow {pointer}");
                    Check(image.enabled && image.sprite == sprite && group.alpha == 1f, "Tracking hid the icon");
                    if (mode == RenderMode.ScreenSpaceCamera)
                    {
                        Vector2 oldPosition = RectTransformUtility.WorldToScreenPoint(camera, (Vector3)pointer);
                        Check(Vector2.Distance(oldPosition, pointer) > 5f, "Fixture does not reproduce the old screen/world mismatch");
                    }
                }
                drag.Hide();
                Check(group.alpha == 0f && !image.enabled, "Hide left icon visible");
                drag.Show(sprite);
                ItemDragContext.CancelActiveDragSession();
                Check(group.alpha == 0f && !image.enabled && !ItemDragContext.Active, "Cancel left drag presentation active");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(canvasHost);
            UnityEngine.Object.DestroyImmediate(cameraHost);
            UnityEngine.Object.DestroyImmediate(sprite);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
