using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TitleIntroAuthoringTool
{
    private const string IntroRootFolder = "Assets/LeeJunMo/Datas/Intro";
    private const string IntroImagesFolder = IntroRootFolder + "/Images";
    private const string DefaultSequencePath = IntroRootFolder + "/IntroSequence_Default.asset";
    private const string IntroOverlayName = "IntroOverlay";
    private const string SlideImageName = "SlideImage";
    private const string ScriptTextName = "ScriptText";
    private const string SkipPromptName = "SkipPrompt";
    private const string TitleFadeServiceRootName = "TitleSceneFadeTransitionService";
    private const string TitleFadeCanvasName = "TitleSceneFadeCanvas";
    private const string TitleFadeImageName = "FadeImage";

    private static readonly string[] DefaultSlideTexts =
    {
        "오래전. 인간과 이종족의 갈등이 점차 격해져 마침내 최고조에 달했다.",
        "당장이라도 큰 싸움이 벌어질 듯했던 그때. 홀연히 나타난 '마왕'이 이종족을 규합해 거대한 세력을 세우니, 사람들은 이를 마왕군이라 불렀다.",
        "불안에 빠진 인간들은 당신의 소꿉친구를 용사로 발탁했다. 하지만 마왕군의 위협을 조기에 막고자 홀로 마왕성으로 향했던 친구는, 그곳에서 감감무소식으로 실종되고 말았다.",
        "그리고 현재. 사라진 소꿉친구의 흔적을 쫓아, 당신 또한 마왕성의 입구에 도달하며 이야기는 시작된다."
    };

    [MenuItem("Tools/Title Intro/Import Intro Zip And Create Default Sequence")]
    public static void ImportIntroZipAndCreateDefaultSequence()
    {
        string zipPath = EditorUtility.OpenFilePanel("Select Intro.zip", string.Empty, "zip");
        if (string.IsNullOrWhiteSpace(zipPath))
            return;

        EnsureIntroFolders();

        List<string> importedImagePaths = ExtractZipImages(zipPath);
        AssetDatabase.Refresh();

        ConfigureTexturesAsSprites(importedImagePaths);
        CreateOrRefreshDefaultSequence(importedImagePaths);
    }

    [MenuItem("Tools/Title Intro/Create Default Sequence From Images Folder")]
    public static void CreateDefaultSequenceFromImagesFolder()
    {
        EnsureIntroFolders();

        string[] imageGuids = AssetDatabase.FindAssets(string.Empty, new[] { IntroImagesFolder });
        List<string> imagePaths = imageGuids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(IsSupportedImageAsset)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ConfigureTexturesAsSprites(imagePaths);
        CreateOrRefreshDefaultSequence(imagePaths);
    }

    [MenuItem("Tools/Title Intro/Wire Active TitleScene Intro Overlay")]
    public static void WireActiveTitleSceneIntroOverlay()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            Debug.LogError("[TitleIntroAuthoring] No active scene is loaded.");
            return;
        }

        TitleMenuController titleMenu = FindSceneComponent<TitleMenuController>(activeScene);
        if (titleMenu == null)
        {
            Debug.LogError("[TitleIntroAuthoring] Active scene does not contain TitleMenuController.");
            return;
        }

        Canvas canvas = ResolveSceneCanvas(activeScene, titleMenu);
        if (canvas == null)
        {
            Debug.LogError("[TitleIntroAuthoring] Active scene does not contain a Canvas for the intro overlay.");
            return;
        }

        TitleIntroView introView = CreateOrRepairIntroOverlay(canvas.transform);
        TitleIntroPlayer introPlayer = titleMenu.GetComponent<TitleIntroPlayer>();
        if (introPlayer == null)
            introPlayer = Undo.AddComponent<TitleIntroPlayer>(titleMenu.gameObject);

        TitleIntroSequenceSO defaultSequence =
            AssetDatabase.LoadAssetAtPath<TitleIntroSequenceSO>(DefaultSequencePath);

        SerializedObject playerObject = new SerializedObject(introPlayer);
        playerObject.FindProperty("sequence").objectReferenceValue = defaultSequence;
        playerObject.FindProperty("view").objectReferenceValue = introView;
        playerObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(introPlayer);

        SerializedObject titleMenuObject = new SerializedObject(titleMenu);
        titleMenuObject.FindProperty("introPlayer").objectReferenceValue = introPlayer;
        titleMenuObject.FindProperty("playIntroForNewProfile").boolValue = true;
        titleMenuObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(titleMenu);

        EditorSceneManager.MarkSceneDirty(activeScene);
        Debug.Log(
            defaultSequence != null
                ? "[TitleIntroAuthoring] Wired TitleScene intro overlay and default sequence."
                : "[TitleIntroAuthoring] Wired TitleScene intro overlay. Create IntroSequence_Default.asset before play validation.");
    }

    [MenuItem("Tools/Title Intro/Wire Active TitleScene Fade Service")]
    public static void WireActiveTitleSceneFadeService()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            Debug.LogError("[TitleIntroAuthoring] No active scene is loaded.");
            return;
        }

        SceneFadeTransitionService fadeService = CreateOrRepairTitleFadeService(activeScene);
        if (fadeService == null)
            return;

        EditorSceneManager.MarkSceneDirty(activeScene);
        Debug.Log("[TitleIntroAuthoring] Wired a scene-root TitleScene fade service. Review fade durations in the Inspector.");
    }

    private static List<string> ExtractZipImages(string zipPath)
    {
        string destinationRoot = Path.GetFullPath(ToFullPath(IntroImagesFolder));
        string destinationRootWithSeparator =
            destinationRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        Directory.CreateDirectory(destinationRoot);

        List<string> extractedAssetPaths = new List<string>();
        using (FileStream stream = File.OpenRead(zipPath))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Name))
                    continue;

                if (entry.FullName.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!IsSupportedImageFile(entry.Name))
                    continue;

                string safeFileName = Path.GetFileName(entry.Name);
                string destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, safeFileName));
                if (!destinationPath.StartsWith(destinationRootWithSeparator, StringComparison.OrdinalIgnoreCase))
                    continue;

                using (Stream input = entry.Open())
                using (FileStream output = File.Create(destinationPath))
                {
                    input.CopyTo(output);
                }

                string assetPath = ToAssetPath(destinationPath);
                if (!string.IsNullOrWhiteSpace(assetPath))
                    extractedAssetPaths.Add(assetPath);
            }
        }

        if (extractedAssetPaths.Count == 0)
            Debug.LogWarning("[TitleIntroAuthoring] Intro zip did not contain supported image files.");

        return extractedAssetPaths;
    }

    private static void ConfigureTexturesAsSprites(IReadOnlyList<string> imagePaths)
    {
        if (imagePaths == null)
            return;

        for (int i = 0; i < imagePaths.Count; i++)
        {
            string path = imagePaths[i];
            if (string.IsNullOrWhiteSpace(path))
                continue;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }
    }

    private static void CreateOrRefreshDefaultSequence(IReadOnlyList<string> imagePaths)
    {
        EnsureIntroFolders();

        Sprite[] sprites = ResolveSprites(imagePaths);
        TitleIntroSequenceSO sequence =
            AssetDatabase.LoadAssetAtPath<TitleIntroSequenceSO>(DefaultSequencePath);

        if (sequence == null)
        {
            sequence = ScriptableObject.CreateInstance<TitleIntroSequenceSO>();
            AssetDatabase.CreateAsset(sequence, DefaultSequencePath);
        }

        SerializedObject sequenceObject = new SerializedObject(sequence);
        SerializedProperty slides = sequenceObject.FindProperty("slides");
        slides.arraySize = DefaultSlideTexts.Length;

        for (int i = 0; i < DefaultSlideTexts.Length; i++)
        {
            SerializedProperty slide = slides.GetArrayElementAtIndex(i);
            slide.FindPropertyRelative("image").objectReferenceValue = i < sprites.Length ? sprites[i] : null;
            slide.FindPropertyRelative("text").stringValue = DefaultSlideTexts[i];
        }

        sequenceObject.FindProperty("secondsPerCharacter").floatValue = 0.05f;
        sequenceObject.FindProperty("introStartFadeDuration").floatValue = 1.2f;
        sequenceObject.FindProperty("oneLineWaitSeconds").floatValue = 1.5f;
        sequenceObject.FindProperty("multiLineWaitSeconds").floatValue = 2f;
        sequenceObject.FindProperty("imageFadeDuration").floatValue = 0.5f;
        sequenceObject.FindProperty("initialImageFadeDuration").floatValue = 1.2f;
        sequenceObject.FindProperty("skipHoldSeconds").floatValue = 2.5f;
        sequenceObject.FindProperty("skipFillColor").colorValue = new Color32(0xF3, 0x3F, 0x48, 0xFF);
        sequenceObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(sequence);
        AssetDatabase.SaveAssets();
        Debug.Log($"[TitleIntroAuthoring] Created/refreshed {DefaultSequencePath} with {sprites.Length} sprite(s).");
    }

    private static Sprite[] ResolveSprites(IReadOnlyList<string> imagePaths)
    {
        if (imagePaths == null)
            return Array.Empty<Sprite>();

        List<Sprite> sprites = new List<Sprite>();
        for (int i = 0; i < imagePaths.Count && sprites.Count < DefaultSlideTexts.Length; i++)
        {
            string path = imagePaths[i];
            if (string.IsNullOrWhiteSpace(path))
                continue;

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                sprites.Add(sprite);
        }

        return sprites.ToArray();
    }

    private static TitleIntroView CreateOrRepairIntroOverlay(Transform canvasTransform)
    {
        Transform existing = FindDirectChild(canvasTransform, IntroOverlayName);
        GameObject root = existing != null
            ? existing.gameObject
            : CreateUiObject(IntroOverlayName, canvasTransform, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));

        RectTransform rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);

        CanvasGroup rootGroup = root.GetComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = true;

        Image rootImage = root.GetComponent<Image>();
        rootImage.color = Color.black;
        rootImage.raycastTarget = true;

        Image slideImage = EnsureChildImage(root.transform, SlideImageName);
        Stretch(slideImage.rectTransform);
        slideImage.color = Color.white;
        slideImage.preserveAspect = true;
        slideImage.raycastTarget = false;

        TextMeshProUGUI scriptText = EnsureChildText(root.transform, ScriptTextName);
        RectTransform textRect = scriptText.rectTransform;
        textRect.anchorMin = new Vector2(0.12f, 0.08f);
        textRect.anchorMax = new Vector2(0.88f, 0.28f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        scriptText.alignment = TextAlignmentOptions.Center;
        scriptText.fontSize = 34f;
        scriptText.textWrappingMode = TextWrappingModes.Normal;
        scriptText.color = Color.white;
        scriptText.raycastTarget = false;

        Transform skipPrompt = FindDirectChild(root.transform, SkipPromptName);
        GameObject skipRoot = skipPrompt != null
            ? skipPrompt.gameObject
            : CreateUiObject(SkipPromptName, root.transform, typeof(RectTransform));

        RectTransform skipRect = skipRoot.GetComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(1f, 0f);
        skipRect.anchorMax = new Vector2(1f, 0f);
        skipRect.pivot = new Vector2(1f, 0f);
        skipRect.anchoredPosition = new Vector2(-72f, 54f);
        skipRect.sizeDelta = new Vector2(360f, 54f);

        CanvasGroup skipGroup = skipRoot.GetComponent<CanvasGroup>();
        if (skipGroup == null)
            skipGroup = Undo.AddComponent<CanvasGroup>(skipRoot);
        skipGroup.alpha = 0f;
        skipGroup.interactable = false;
        skipGroup.blocksRaycasts = false;

        Image keyBackground = EnsureChildImage(skipRoot.transform, "SpaceKeyBackground");
        RectTransform keyRect = keyBackground.rectTransform;
        keyRect.anchorMin = new Vector2(0f, 0.5f);
        keyRect.anchorMax = new Vector2(0f, 0.5f);
        keyRect.pivot = new Vector2(0f, 0.5f);
        keyRect.anchoredPosition = Vector2.zero;
        keyRect.sizeDelta = new Vector2(120f, 42f);
        keyBackground.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        keyBackground.type = Image.Type.Sliced;
        keyBackground.color = new Color(0f, 0f, 0f, 0.7f);
        keyBackground.raycastTarget = false;

        Image fillImage = EnsureChildImage(keyBackground.transform, "SpaceHoldFill");
        Stretch(fillImage.rectTransform);
        fillImage.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.fillAmount = 0f;
        fillImage.color = new Color32(0xF3, 0x3F, 0x48, 0xFF);
        fillImage.raycastTarget = false;

        TextMeshProUGUI keyLabel = EnsureChildText(keyBackground.transform, "SpaceKeyLabel");
        Stretch(keyLabel.rectTransform);
        keyLabel.text = "Space";
        keyLabel.alignment = TextAlignmentOptions.Center;
        keyLabel.fontSize = 20f;
        keyLabel.color = Color.white;
        keyLabel.raycastTarget = false;

        Image keyIcon = EnsureChildImage(keyBackground.transform, "SpaceKeyIcon");
        Stretch(keyIcon.rectTransform);
        keyIcon.enabled = false;
        keyIcon.preserveAspect = true;
        keyIcon.raycastTarget = false;

        TextMeshProUGUI guideText = EnsureChildText(skipRoot.transform, "SkipGuideText");
        RectTransform guideRect = guideText.rectTransform;
        guideRect.anchorMin = new Vector2(0f, 0f);
        guideRect.anchorMax = new Vector2(1f, 1f);
        guideRect.offsetMin = new Vector2(132f, 0f);
        guideRect.offsetMax = Vector2.zero;
        guideText.text = "를 길게 눌러 스킵";
        guideText.alignment = TextAlignmentOptions.MidlineLeft;
        guideText.fontSize = 21f;
        guideText.color = Color.white;
        guideText.raycastTarget = false;

        TitleIntroView introView = root.GetComponent<TitleIntroView>();
        if (introView == null)
            introView = Undo.AddComponent<TitleIntroView>(root);

        SerializedObject viewObject = new SerializedObject(introView);
        viewObject.FindProperty("root").objectReferenceValue = root;
        viewObject.FindProperty("rootGroup").objectReferenceValue = rootGroup;
        viewObject.FindProperty("slideImage").objectReferenceValue = slideImage;
        viewObject.FindProperty("scriptText").objectReferenceValue = scriptText;
        viewObject.FindProperty("skipPromptRoot").objectReferenceValue = skipRoot;
        viewObject.FindProperty("skipPromptGroup").objectReferenceValue = skipGroup;
        viewObject.FindProperty("skipKeyIconImage").objectReferenceValue = keyIcon;
        viewObject.FindProperty("skipKeyLabel").objectReferenceValue = keyLabel;
        viewObject.FindProperty("skipHoldFillImage").objectReferenceValue = fillImage;
        viewObject.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(false);
        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(introView);
        return introView;
    }

    private static Image EnsureChildImage(Transform parent, string name)
    {
        Transform child = FindDirectChild(parent, name);
        GameObject gameObject = child != null
            ? child.gameObject
            : CreateUiObject(name, parent, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        return gameObject.GetComponent<Image>();
    }

    private static TextMeshProUGUI EnsureChildText(Transform parent, string name)
    {
        Transform child = FindDirectChild(parent, name);
        GameObject gameObject = child != null
            ? child.gameObject
            : CreateUiObject(name, parent, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));

        return gameObject.GetComponent<TextMeshProUGUI>();
    }

    private static GameObject CreateUiObject(string name, Transform parent, params Type[] components)
    {
        GameObject gameObject = new GameObject(name, components);
        Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static SceneFadeTransitionService CreateOrRepairTitleFadeService(Scene scene)
    {
        GameObject root = FindRootObject(scene, TitleFadeServiceRootName);
        if (root == null)
        {
            root = new GameObject(TitleFadeServiceRootName);
            Undo.RegisterCreatedObjectUndo(root, $"Create {TitleFadeServiceRootName}");
            SceneManager.MoveGameObjectToScene(root, scene);
        }

        SceneFadeTransitionService fadeService = root.GetComponent<SceneFadeTransitionService>();
        if (fadeService == null)
            fadeService = Undo.AddComponent<SceneFadeTransitionService>(root);

        Canvas canvas = EnsureChildCanvas(root.transform, TitleFadeCanvasName);
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        Stretch(canvasRect);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        canvas.pixelPerfect = false;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        raycaster.ignoreReversedGraphics = true;

        Image fadeImage = EnsureChildImage(canvas.transform, TitleFadeImageName);
        Stretch(fadeImage.rectTransform);
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = true;

        CanvasGroup fadeGroup = fadeImage.GetComponent<CanvasGroup>();
        if (fadeGroup == null)
            fadeGroup = Undo.AddComponent<CanvasGroup>(fadeImage.gameObject);

        fadeGroup.alpha = 0f;
        fadeGroup.interactable = false;
        fadeGroup.blocksRaycasts = true;
        fadeImage.gameObject.SetActive(false);

        SerializedObject fadeObject = new SerializedObject(fadeService);
        fadeObject.FindProperty("overlayRoot").objectReferenceValue = fadeImage.gameObject;
        fadeObject.FindProperty("overlayCanvasGroup").objectReferenceValue = fadeGroup;
        fadeObject.FindProperty("overlayImage").objectReferenceValue = fadeImage;
        fadeObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(canvas);
        EditorUtility.SetDirty(fadeImage);
        EditorUtility.SetDirty(fadeService);
        return fadeService;
    }

    private static Canvas EnsureChildCanvas(Transform parent, string name)
    {
        Transform child = FindDirectChild(parent, name);
        GameObject gameObject = child != null
            ? child.gameObject
            : CreateUiObject(name, parent, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = Undo.AddComponent<Canvas>(gameObject);

        if (gameObject.GetComponent<CanvasScaler>() == null)
            Undo.AddComponent<CanvasScaler>(gameObject);

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            Undo.AddComponent<GraphicRaycaster>(gameObject);

        return canvas;
    }

    private static GameObject FindRootObject(Scene scene, string rootName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root != null && root.name == rootName)
                return root;
        }

        return null;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private static Canvas ResolveSceneCanvas(Scene scene, Component nearComponent)
    {
        Canvas parentCanvas = nearComponent != null ? nearComponent.GetComponentInParent<Canvas>() : null;
        if (parentCanvas != null && parentCanvas.gameObject.scene == scene)
            return parentCanvas;

        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.gameObject.scene == scene)
                return canvas;
        }

        return null;
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        T[] candidates = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include);

        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (candidate != null && candidate.gameObject.scene == scene)
                return candidate;
        }

        return null;
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && child.name == childName)
                return child;
        }

        return null;
    }

    private static void EnsureIntroFolders()
    {
        EnsureAssetFolder("Assets/LeeJunMo/Datas", "Intro");
        EnsureAssetFolder(IntroRootFolder, "Images");
    }

    private static void EnsureAssetFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (AssetDatabase.IsValidFolder(path))
            return;

        AssetDatabase.CreateFolder(parent, child);
    }

    private static bool IsSupportedImageAsset(string assetPath)
    {
        return IsSupportedImageFile(assetPath);
    }

    private static bool IsSupportedImageFile(string path)
    {
        string extension = Path.GetExtension(path);
        return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToFullPath(string assetPath)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static string ToAssetPath(string fullPath)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
            .Replace('\\', '/')
            .TrimEnd('/');
        string normalized = Path.GetFullPath(fullPath).Replace('\\', '/');

        if (!normalized.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
            return null;

        return normalized.Substring(projectRoot.Length + 1);
    }
}
