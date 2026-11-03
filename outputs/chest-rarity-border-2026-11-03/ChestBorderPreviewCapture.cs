// Image production only: run in an isolated Edit Mode batch Editor, never Play Mode.
// A temporary copy under Editor is removed after capture. No assets/scenes are saved.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ChestBorderPreviewCapture
{
    const string Folder = "outputs/chest-rarity-border-2026-11-03";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);

    public static void CaptureBatch()
    {
        EditorApplication.delayCall += () =>
        {
            int code = 0;
            try { Capture(); }
            catch (Exception error) { Debug.LogException(error); code = 1; }
            EditorApplication.Exit(code);
        };
    }

    static void Capture()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Capture requires Edit Mode.");
        Directory.CreateDirectory(Folder);
        LocalizationSettings.InitializationOperation.WaitForCompletion();
        var originalLocale = LocalizationSettings.SelectedLocale;
        var korean = LocalizationSettings.AvailableLocales.GetLocale("ko");
        LocalizationSettings.SelectedLocale = korean;
        LocalizationSettings.StringDatabase.GetTableAsync(GameText.TableName, korean).WaitForCompletion();
        LocalizationSettings.AssetDatabase.GetTableAsync("GameFonts", korean).WaitForCompletion();
        Scene preview = EditorSceneManager.NewPreviewScene();
        ChestContainerAdapter adapter = null;
        try
        {
            GameObject host = new("Chest image canvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(host, preview);
            host.SetActive(false);
            Canvas canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)host.transform).sizeDelta = new Vector2(1920, 1080);
            host.transform.localScale = Vector3.one * 0.01f;
            GameObject cameraObject = new("Chest image camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.scene = preview;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.071f, 0.09f, 1f);
            canvas.worldCamera = camera;

            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab")
                .GetComponentInChildren<ChestScreen>(true);
            GameObject root = Object.Instantiate(source.gameObject, host.transform, false);
            ChestScreen screen = root.GetComponent<ChestScreen>();
            screen.enabled = false;
            foreach (UISlideFadePresentation slide in root.GetComponentsInChildren<UISlideFadePresentation>(true))
                slide.enabled = false;
            RectTransform chest = Get<RectTransform>(screen, "chestPanelRect");
            RectTransform inventory = Get<RectTransform>(screen, "inventoryPanelRect");
            if (inventory != null) inventory.gameObject.SetActive(false);
            string[] samples = { "RD_CurrentShard", "RD_ConductorRing", "RD_BurningFeather", "RD_OddIronMagazine",
                "RD_CombatGloves", "RD_CurrentJar", "RD_BrokenCrown", "RD_ApprenticeChargeLink" };
            var stock = new ChestInventory(samples.Length);
            for (int i = 0; i < samples.Length; i++)
            {
                var relic = AssetDatabase.LoadAssetAtPath<RelicDefinition>(
                    "Assets/_Project/Data/Items/Relics/Definitions/" + samples[i] + ".asset");
                if (relic == null) throw new InvalidOperationException("Missing image sample: " + samples[i]);
                stock.SetRelicWithLevel(i, relic, 1);
            }
            adapter = new ChestContainerAdapter(stock, true);
            Set(screen, "chestContainer", adapter);
            typeof(ChestScreen).GetMethod("BuildChestSlots", Private).Invoke(screen, null);
            root.SetActive(true);
            host.SetActive(true);
            foreach (CanvasGroup group in root.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1;
            var reveal = root.GetComponentInChildren<ChestFirstOpenRevealPresentation>(true);
            reveal.SnapOpen();
            reveal.enabled = false;
            foreach (LocalizeTmpFontEvent binding in root.GetComponentsInChildren<LocalizeTmpFontEvent>(true))
            {
                TMP_FontAsset font = binding.AssetReference.LoadAssetAsync().WaitForCompletion();
                if (font != null) { binding.ApplyLocalizedFont(font); binding.FlushPendingLayout(); }
            }
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true)) label.ForceMeshUpdate(true);
            // Supply display-only sample data through the actual formatter, without a live chest owner.
            Get<TMP_Text>(screen, "rerollCountLabel").text = (string)typeof(ChestScreen)
                .GetMethod("FormatRerollCount", Private).Invoke(screen, new object[] { 3, 3, 0 });
            Rebuild(root);
            reveal.SnapOpen();
            Rebuild(root);

            var grid = (RectTransform)Get<Transform>(screen, "chestGridRoot");
            var selected = Get<RectTransform>(screen, "selectedItemsRoot");
            var confirm = Get<Button>(screen, "confirmSelectionButton");
            Bounds full = BoundsOf(chest);
            if (selected != null && selected.gameObject.activeInHierarchy) full.Encapsulate(BoundsOf(selected));
            if (confirm != null && confirm.gameObject.activeInHierarchy) full.Encapsulate(BoundsOf((RectTransform)confirm.transform));
            Render(camera, full, 1200, 1200, "chest-ui.png", 0.32f);
            Render(camera, BoundsOf(grid), 1600, 800, "chest-slots-closeup.png", 0.10f);
            File.WriteAllText(Path.Combine(Folder, "capture-details.txt"),
                "Edit Mode rendering of the current GlobalUIRoot ChestScreen subtree.\n" +
                "No Play Mode, tests, gameplay acquisition, save writes, or AssetDatabase.SaveAssets.\n" +
                "Player inventory is hidden in the temporary preview; the camera frames the chest.\n" +
                "Remaining rerolls are display-only sample data (3), formatted by ChestScreen.\n" +
                "Rows show Common / Rare / Epic / weapon-exclusive authored relics.\n" +
                string.Join("\n", samples));
            Debug.Log("CHEST_BORDER_IMAGES_WRITTEN: " + Path.GetFullPath(Folder));
        }
        finally
        {
            adapter?.Dispose();
            EditorSceneManager.ClosePreviewScene(preview);
            LocalizationSettings.SelectedLocale = originalLocale;
        }
    }

    static void Rebuild(GameObject root)
    {
        for (int pass = 0; pass < 3; pass++)
        {
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true).Reverse())
                if (rect.gameObject.activeInHierarchy) LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            Canvas.ForceUpdateCanvases();
        }
    }

    static Bounds BoundsOf(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Bounds result = new(corners[0], Vector3.zero);
        foreach (var corner in corners) result.Encapsulate(corner);
        return result;
    }

    static void Render(Camera camera, Bounds bounds, int width, int height, string name, float padding)
    {
        var target = new RenderTexture(width, height, 24);
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10);
            camera.aspect = (float)width / height;
            camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) + padding;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(Folder, name), pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(pixels);
        }
    }
}
