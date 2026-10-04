using System.Collections.Generic;
using UnityEngine.Localization.Settings;

// Table lookup only. Locale state remains owned by Unity Localization.
public static class GameText
{
    public const string TableName = "GameText";

    public static string Asset(UnityEngine.Object asset, string field, string authoredFallback)
    {
        string key = AssetKey(asset, field);
        return key != null ? Get(key, authoredFallback) : authoredFallback;
    }

    // Capture stable display identity before a damage source is destroyed; never store translated names as keys.
    public static string AssetKey(UnityEngine.Object asset, string field)
    {
        if (asset == null) return null;
        string name = asset.name;
        if (asset is UnityEngine.Component && name.EndsWith("(Clone)", System.StringComparison.Ordinal))
            name = name.Substring(0, name.Length - "(Clone)".Length);
        string identity = asset.GetType().FullName + "/" + name + "/" + field;
        return GameTextAssetKeys.Keys.TryGetValue(identity, out string key) ? key : null;
    }

    public static string Format(string key, string authoredFormat, params object[] values) =>
        string.Format(Get(key, authoredFormat), values);

    public static string Get(string key, string authoredFallback)
    {
        if (string.IsNullOrEmpty(key) || !LocalizationSettings.InitializationOperation.IsDone)
            return authoredFallback;
        var table = LocalizationSettings.StringDatabase.GetTable(TableName);
        var entry = table != null ? table.GetEntry(key) : null;
        return entry != null && !string.IsNullOrEmpty(entry.Value) ? entry.Value : authoredFallback;
    }

    public static string FromInkTags(IList<string> tags, string authoredFallback)
    {
        if (tags != null)
            foreach (string tag in tags)
                if (tag != null && tag.Trim().StartsWith("loc:", System.StringComparison.Ordinal))
                    return Get(tag.Trim().Substring(4).Trim(), authoredFallback);
        return authoredFallback;
    }

    public static string WeaponStory(string weaponId, string authoredFallback) =>
        weaponId == "Weapon.OddIron" ? Get("weapon.odd_iron.description", authoredFallback) : authoredFallback;
}
