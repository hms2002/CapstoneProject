using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.CSV;
using UnityEditor.Localization.Plugins.CSV.Columns;
using UnityEngine;
using UnityGAS;

/// <summary>제공된 세 원본 이미지로 등불 유물과 전용 프리팹을 재작성하는 제작 진입점이다.</summary>
public static class ChloeLanternAuthoring
{
    public const string RelicId = "RD_ChloeLantern";
    private const string Art = "Assets/_Project/Art/Sprites/";
    private const string Prefabs = "Assets/_Project/Prefabs/VFX/Relics/";
    private const string Animation = "Assets/_Project/Art/Animations/VFX/Common/ChloeLanternProjectile";
    private const string Definition = "Assets/_Project/Data/Items/Relics/Definitions/RD_ChloeLantern.asset";
    private const string Strategy = "Assets/_Project/Data/Items/Relics/Strategies/RL_ChloeLantern.asset";

    [MenuItem("Tools/Content/Relics/Author Chloe Lantern")]
    public static void Apply()
    {
        Sprite icon = ImportSprite(Art + "Items/RelicIcon/Icon_ChloeLantern.png", 100f, false).Single();
        Sprite summonSprite = ImportSprite(Art + "VFX/Common/ChloeLanternSummon.png", 100f, false).Single();
        Sprite[] frames = ImportSprite(Art + "VFX/Common/ChloeLanternProjectile.png", 32f, true);
        GameObject sunPrefab = Load<GameObject>("Assets/_Project/Prefabs/VFX/Relics/Fire/PF_SunFragment.prefab");
        var sunLogic = new SerializedObject(Load<RelicLogic_SunFragment_Managed>(
            "Assets/_Project/Data/Items/Relics/Strategies/Relic Logic_Sun Fragment.asset"));
        var gunner = Load<AbilityLogic_GoblinGunnerShot>(
            "Assets/_Project/Data/Abilities/Strategies/Monsters/CommonCorridor/AL_GoblinGunnerShot.asset");
        var gunnerData = new SerializedObject(gunner);
        GameObject shotReference = gunnerData.FindProperty("projectilePrefab").objectReferenceValue as GameObject;
        SpriteRenderer shotRenderer = shotReference.GetComponent<SpriteRenderer>();
        CircleCollider2D shotCollider = shotReference.GetComponent<CircleCollider2D>();
        float shotWidth = shotRenderer.sprite.bounds.size.x * Mathf.Abs(shotReference.transform.localScale.x);

        GameObject summonObject = new("PF_ChloeLanternSummon");
        GameObject shotObject = new("PF_ChloeLanternProjectile");
        GameObject summonPrefab;
        GameObject projectilePrefab;
        try
        {
            SpriteRenderer summonRenderer = summonObject.AddComponent<SpriteRenderer>();
            summonRenderer.sprite = summonSprite;
            summonRenderer.sortingLayerName = "Projectile";
            summonRenderer.sortingOrder = 4;
            SpriteRenderer sunRenderer = sunPrefab.GetComponent<SpriteRenderer>();
            float summonWidth = sunRenderer.sprite.bounds.size.x * sunPrefab.transform.localScale.x *
                sunLogic.FindProperty("fragmentSize").floatValue;
            summonObject.transform.localScale = Vector3.one * (summonWidth / summonSprite.bounds.size.x);
            summonPrefab = PrefabUtility.SaveAsPrefabAsset(summonObject, Prefabs + "PF_ChloeLanternSummon.prefab");

            shotObject.layer = shotReference.layer;
            SpriteRenderer renderer = shotObject.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.sortingLayerName = "Projectile";
            renderer.sortingOrder = 4;
            float shotScale = shotWidth / frames.Max(frame => frame.bounds.size.x);
            shotObject.transform.localScale = new Vector3(shotScale, shotScale, 1f);
            Rigidbody2D body = shotObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            CircleCollider2D collider = shotObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = shotCollider.radius * Mathf.Abs(shotReference.transform.localScale.x) / shotScale;
            shotObject.AddComponent<ChloeLanternProjectile2D>();
            Animator animator = shotObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = AuthorAnimation(frames);
            projectilePrefab = PrefabUtility.SaveAsPrefabAsset(shotObject, Prefabs + "PF_ChloeLanternProjectile.prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(summonObject);
            UnityEngine.Object.DestroyImmediate(shotObject);
        }

        RelicLogic_ChloeLantern logic = LoadOrCreate<RelicLogic_ChloeLantern>(Strategy);
        logic.summonPrefab = summonPrefab;
        logic.projectilePrefab = projectilePrefab.GetComponent<ChloeLanternProjectile2D>();
        logic.damageEffect = Load<GameplayEffect>("Assets/_Project/Data/Abilities/Effects/GE_Damage_Spec.asset");
        logic.targetLayers = sunLogic.FindProperty("targetLayers").intValue;
        logic.wallLayers = gunnerData.FindProperty("wallLayers").intValue;
        logic.fireInterval = 2f;
        logic.orbitRadius = sunLogic.FindProperty("orbitRadius").floatValue;
        logic.orbitCenterLocalOffset = sunLogic.FindProperty("orbitCenterLocalOffset").vector2Value;
        logic.angularSpeedDegPerSec = sunLogic.FindProperty("angularSpeedDegPerSec").floatValue;
        logic.projectileSpeedMultiplier = gunnerData.FindProperty("projectileSpeedMultiplier").floatValue;
        logic.projectileLifetime = gunnerData.FindProperty("projectileLifetime").floatValue;
        logic.homingTurnRate = 1080f;
        EditorUtility.SetDirty(logic);
        RelicDefinition definition = LoadOrCreate<RelicDefinition>(Definition);
        definition.relicId = RelicId;
        definition.displayName = "클로에의 등불";
        definition.description = "주위를 공전하는 소환수가 적에게 강한 유도탄을 발사합니다.";
        definition.icon = icon;
        definition.rarity = ItemRarity.Epic; // Existing UI displays Epic as 전설.
        definition.maxLevel = 1;
        definition.dropLevel = 1;
        definition.logic = logic;
        EditorUtility.SetDirty(definition);
        ItemDatabase database = Load<ItemDatabase>("Assets/_Project/Data/Items/ItemDatabase.asset");
        database.allRelics ??= new List<RelicDefinition>();
        if (!database.allRelics.Contains(definition)) database.allRelics.Add(definition);
        EditorUtility.SetDirty(database); // Deliberately leave default unlocks and affection rewards alone.
        AssetDatabase.SaveAssets();
        Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        Debug.Log("[ChloeLantern] Authored " + RelicId + "; definition GUID=" + AssetDatabase.AssetPathToGUID(Definition) +
            "; summon width=" + summonPrefab.GetComponent<SpriteRenderer>().bounds.size.x + "; projectile width=" + shotWidth);
    }

    private static Sprite[] ImportSprite(string path, float pixelsPerUnit, bool sheet)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new FileNotFoundException(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        if (sheet)
        {
#pragma warning disable CS0618 // Compatibility API avoids adding a new sprite-editor assembly dependency.
            importer.spritesheet = Enumerable.Range(0, 8).Select(index => new SpriteMetaData
            {
                name = "ChloeLanternProjectile_" + index,
                rect = new Rect(index * 32, 0, 32, 32),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            }).ToArray();
#pragma warning restore CS0618
        }
        importer.SaveAndReimport();
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
        if (sprites.Length != (sheet ? 8 : 1)) throw new InvalidOperationException("Unexpected sprite count: " + path);
        return sprites;
    }

    private static AnimatorController AuthorAnimation(Sprite[] frames)
    {
        AnimationClip clip = LoadOrCreate<AnimationClip>(Animation + ".anim");
        clip.frameRate = 12f;
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"),
            Enumerable.Range(0, frames.Length + 1).Select(index => new ObjectReferenceKeyframe
            { time = index / clip.frameRate, value = frames[index % frames.Length] }).ToArray());
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Animation + ".controller");
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(Animation + ".controller");
            controller.AddMotion(clip);
        }
        return controller;
    }

    // Import only this relic's prepared subset. Existing locale entries are preserved.
    public static void Localize()
    {
        var collection = Load<StringTableCollection>("Assets/_Project/Data/Localization/GameText.asset");
        var mappings = new List<CsvColumns>
        {
            new KeyIdColumns { KeyFieldName = "key", IncludeId = false, IncludeSharedComments = false }
        };
        foreach (string code in new[] { "ko", "en", "ja", "zh-Hans", "zh-Hant" })
            mappings.Add(new LocaleColumns { LocaleIdentifier = code, FieldName = code, IncludeComments = false });
        using (var reader = File.OpenText("Library/CodexChloeLantern/content.csv"))
            Csv.ImportInto(reader, collection, mappings, true, removeMissingEntries: false);
        EditorUtility.SetDirty(collection.SharedData);
        foreach (var table in collection.StringTables) EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
        Debug.Log("[ChloeLantern] Imported three localization keys in five locales.");
    }

    private static T Load<T>(string path) where T : UnityEngine.Object
        => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new FileNotFoundException(path);

    private static T LoadOrCreate<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = typeof(ScriptableObject).IsAssignableFrom(typeof(T))
            ? (T)(UnityEngine.Object)ScriptableObject.CreateInstance(typeof(T))
            : (T)Activator.CreateInstance(typeof(T));
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
