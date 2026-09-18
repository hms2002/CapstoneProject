#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Responsibility: keep the non-combat speed HUD and upgrade icons distinct from relic artwork.</summary>
public sealed class OutOfCombatSpeedIconTests
{
    [Test]
    public void SpeedUpgradeUsesDedicatedSpriteAtAllDisplaySites()
    {
        const string iconPath = "Assets/_Project/Art/Sprites/UI/StatusIcons/OutOfCombatSpeed.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        Assert.That(sprite, Is.Not.Null);
        var importer = (TextureImporter)AssetImporter.GetAtPath(iconPath);
        Assert.That(importer.maxTextureSize, Is.EqualTo(128));
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.DoesSourceTextureHaveAlpha(), Is.True);
        Assert.That(sprite.texture.width, Is.LessThanOrEqualTo(128));
        Check("Assets/_Project/Data/Progression/Upgrades/Effect/SHD_OutOfCombatMoveSpeed.asset", "icon", sprite);
        Check("Assets/_Project/Data/Progression/Upgrades/Effect/Effect_OutOfCombatMoveSpeed.asset", "rewardIcon", sprite);
        Check("Assets/_Project/Resources/Upgrades/Nodes/Node_OutOfCombatMoveSpeed.asset", "icon", sprite);
        var relic = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/_Project/Art/Sprites/Items/RelicIcon/Icon_SpeedBoots.png");
        Assert.That(relic, Is.Not.Null);
        Assert.That(sprite, Is.Not.EqualTo(relic));
    }

    private static void Check(string path, string field, Sprite expected)
    {
        var asset = AssetDatabase.LoadMainAssetAtPath(path);
        Assert.That(asset, Is.Not.Null, path);
        var data = new SerializedObject(asset);
        Assert.That(data.FindProperty(field).objectReferenceValue, Is.EqualTo(expected), path);
    }
}
#endif
