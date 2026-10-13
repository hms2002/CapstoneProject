#if UNITY_EDITOR
// [UNITY-SKILL:SPRITEATLAS]
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

// Scoped, idempotent prebuild authoring for ordinary card art; no runtime loader.
public sealed class UiBatchingAtlasPrebuild : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;
    public void OnPreprocessBuild(BuildReport report)
    {
        UiBatchingAssetAuthoring.EnsureCardAtlas();
        UiBatchingAssetAuthoring.EnsureMenuAtlas();
    }
}

public static class UiBatchingAssetAuthoring
{
    public const string CardAtlasPath = "Assets/_Project/Art/SpriteAtlases/UI_LevelRewardCards.spriteatlasv2";
    public const string MenuAtlasPath = "Assets/_Project/Art/SpriteAtlases/UI_MenuChrome.spriteatlasv2";
    public const string ProgressStylePath = "Assets/_Project/Art/Font/UI_StatusProgress_BlackOutline.mat";
    const string EntryPath = "Assets/_Project/Prefabs/UI/status_HUD_EntryView.prefab";
    const string CardFolder = "Assets/_Project/Art/Sprites/UI/LevelRewardCards/";
    static readonly string[] CardNames = { "Base", "Description", "Title_1", "Outline_1" };

    public static void Author()
    {
        EnsureCardAtlas();
        EnsureMenuAtlas();
        AuthorProgressStyle();
        AssetDatabase.SaveAssets();
        Debug.Log("UI batching assets authored through Unity APIs; Sprite/SpriteMeta references were preserved.");
    }

    public static void EnsureCardAtlas() => EnsureAtlas(CardAtlasPath,
        CardNames.Select(name => CardFolder + name + ".png").ToArray());

    public static void EnsureMenuAtlas() => EnsureAtlas(MenuAtlasPath, new[] {
        "Assets/_Project/Art/Sprites/UI/ButtonUI.png",
        "Assets/_Project/Art/Sprites/UI/UIPanel.png",
        "Assets/_Project/Art/Sprites/UI/UITitle.png" });

    static void EnsureAtlas(string atlasPath, string[] texturePaths)
    {
        if (EditorSettings.spritePackerMode == SpritePackerMode.Disabled)
            throw new BuildFailedException("Sprite packing is disabled; do not silently change project settings.");
        EnsureFolder(Path.GetDirectoryName(atlasPath).Replace('\\', '/'));
        UnityEngine.Object[] expected = texturePaths.SelectMany(path =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()).Cast<UnityEngine.Object>().ToArray();
        if (expected.Length != texturePaths.Length) throw new BuildFailedException("Each whitelisted UI texture must contain one authored Sprite.");

        bool created = !File.Exists(atlasPath);
        var source = created ? new SpriteAtlasAsset() : SpriteAtlasAsset.Load(atlasPath);
        var runtimeView = created ? null : AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
        UnityEngine.Object[] existing = runtimeView != null ? runtimeView.GetPackables() : Array.Empty<UnityEngine.Object>();
        bool packablesChanged = existing.Length != expected.Length || expected.Any(item => !existing.Contains(item));
        if (packablesChanged)
        {
            if (existing.Length > 0) source.Remove(existing);
            source.Add(expected);
            SpriteAtlasAsset.Save(source, atlasPath);
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceSynchronousImport);
        }

        var importer = AssetImporter.GetAtPath(atlasPath) as SpriteAtlasImporter;
        if (importer == null) throw new BuildFailedException("SpriteAtlas V2 importer missing.");
        bool changed = false;
        if (!importer.includeInBuild) { importer.includeInBuild = true; changed = true; }
        var packing = importer.packingSettings;
        if (packing.enableRotation || packing.enableTightPacking || packing.padding != 4)
        {
            packing.enableRotation = false; packing.enableTightPacking = false; packing.padding = 4;
            importer.packingSettings = packing; changed = true;
        }
        var texture = importer.textureSettings;
        if (texture.filterMode != FilterMode.Point || texture.generateMipMaps || !texture.sRGB)
        {
            texture.filterMode = FilterMode.Point; texture.generateMipMaps = false; texture.sRGB = true;
            importer.textureSettings = texture; changed = true;
        }
        var platform = importer.GetPlatformSettings("DefaultTexturePlatform");
        if (platform.maxTextureSize != 512 || platform.format != TextureImporterFormat.RGBA32 || platform.crunchedCompression)
        {
            platform.maxTextureSize = 512; platform.format = TextureImporterFormat.RGBA32;
            platform.textureCompression = TextureImporterCompression.Uncompressed; platform.crunchedCompression = false;
            importer.SetPlatformSettings(platform); changed = true;
        }
        if (changed) importer.SaveAndReimport();
        Debug.Log($"UI atlas {atlasPath}: {EditorSettings.spritePackerMode}, {expected.Length} explicit sprites, included in build.");
    }

    static void AuthorProgressStyle()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(EntryPath);
        try
        {
            var entry = contents.GetComponent<StatusHudEntryView>();
            var entryData = new SerializedObject(entry);
            var text = entryData.FindProperty("stackText").objectReferenceValue as TMP_Text;
            if (text == null || text.font == null) throw new InvalidOperationException("Authored status stack text/font missing.");
            var binding = text.GetComponent<LocalizeTmpFontEvent>();
            Material authored = binding != null ? binding.AuthoredMaterial : null;
            authored = authored != null ? authored : text.fontSharedMaterial != null ? text.fontSharedMaterial : text.font.material;
            Material style = AssetDatabase.LoadAssetAtPath<Material>(ProgressStylePath);
            if (style == null)
            {
                style = new Material(authored) { name = "UI_StatusProgress_BlackOutline" };
                style.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                style.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
                AssetDatabase.CreateAsset(style, ProgressStylePath);
            }
            if (entryData.FindProperty("progressTextMaterial").objectReferenceValue != style)
            {
                entryData.FindProperty("progressTextMaterial").objectReferenceValue = style;
                entryData.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, EntryPath);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
#endif
