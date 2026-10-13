#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Explicit authoring only: catalog display data and identities on registered monster prefabs.
public static class EncyclopediaCreatureDataAuthoring
{
    private const string CatalogPath = "Assets/_Project/Data/Encyclopedia/EncyclopediaCatalog.asset";
    private const string NpcFolder = "Assets/_Project/Data/Dialogue/NPC/";
    private const string ShadowServantIdlePath = "Assets/_Project/Art/Sprites/Monsters/ShadowServant/ShadowServant_Idle.anim";
    private static readonly string[] NpcPaths =
    {
        "MSShopNpc.asset", "MSUpgradeNpc.asset", "ScribeNpc.asset", "ParcelEventNpc.asset", "BuffyEventNpc.asset"
    };
    private static readonly Dictionary<string, string> MonsterPaths = new()
    {
        { "monster.beer", "Assets/_Project/Prefabs/Monsters/BeerMonster.prefab" },
        { "monster.treasure", "Assets/_Project/Prefabs/Monsters/TreasureMonster.prefab" },
        { "monster.frog", "Assets/_Project/Prefabs/Monsters/Frog.prefab" },
        { "monster.frog_elite", "Assets/_Project/Prefabs/Monsters/Frog_BOSS.prefab" },
        { "monster.slime_pawn", "Assets/_Project/Prefabs/Monsters/SlimeCorridor/Pawn.prefab" },
        { "monster.slime_rook", "Assets/_Project/Prefabs/Monsters/SlimeCorridor/Rook.prefab" },
        { "monster.slime_knight", "Assets/_Project/Prefabs/Monsters/SlimeCorridor/Knight.prefab" },
        { "monster.slime_bishop", "Assets/_Project/Prefabs/Monsters/SlimeCorridor/Bishop.prefab" },
        { "monster.slime_wizard", "Assets/_Project/Prefabs/Monsters/SlimeCorridor/Wizard.prefab" },
        { "monster.candlestick_corridor", "Assets/_Project/Prefabs/Monsters/ShadowCorridor/CorridorCandlestickMonster.prefab" },
        { "monster.deads_skeleton", "Assets/_Project/Prefabs/Monsters/ShadowCorridor/Dead'sSkeleton.prefab" },
        { "monster.shadow", "Assets/_Project/Prefabs/Monsters/ShadowCorridor/ShadowMonster.prefab" },
        { "monster.shadow_servant", "Assets/_Project/Prefabs/Monsters/ShadowCorridor/ShadowServant/ShadowServant.prefab" },
        { "monster.strange_candlestick", "Assets/_Project/Prefabs/Monsters/ShadowCorridor/StrangeCandlestick/StrangeCandlestick.prefab" }
    };

    private sealed class Preview
    {
        public GameObject source;
        public Sprite[] frames;
        public float frameRate;
    }

    public static void Populate(EncyclopediaCatalogSO catalog)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        var serialized = new SerializedObject(catalog);
        var monsters = Required(serialized, "monsterEntries");
        var bosses = Required(serialized, "bossEntries");
        var npcs = Required(serialized, "npcEntries");
        var previews = new List<Preview>();
        var bossNpcs = new List<NPCData>();
        var bossPortraits = new List<List<EncyclopediaPortrait>>();
        var npcData = NpcPaths.Select(path => Load<NPCData>(NpcFolder + path)).ToArray();
        var npcPortraits = npcData.Select(ReadPortraits).ToArray();
        var npcStories = new Dictionary<int, string>();

        // Resolve every source before applying serialized changes. Item arrays are never touched.
        for (int i = 0; i < monsters.arraySize; i++)
        {
            var entry = monsters.GetArrayElementAtIndex(i);
            string id = entry.FindPropertyRelative("id").stringValue;
            var source = entry.FindPropertyRelative("sourcePrefab").objectReferenceValue as GameObject;
            if (source == null && MonsterPaths.TryGetValue(id, out string path)) source = Load<GameObject>(path);
            if (source == null) throw new InvalidOperationException("Monster source prefab missing: " + id);
            var clip = FindIdleClip(source, id);
            previews.Add(ReadPreview(source, clip, id));
        }
        for (int i = 0; i < bosses.arraySize; i++)
        {
            var entry = bosses.GetArrayElementAtIndex(i);
            var npc = entry.FindPropertyRelative("npcData").objectReferenceValue as NPCData;
            if (npc == null && entry.FindPropertyRelative("id").stringValue == "boss.slime_queen")
                npc = Load<NPCData>(NpcFolder + "SlimeQueenBossNpc.asset");
            if (npc == null) throw new InvalidOperationException("Boss NPCData missing: " + i);
            bossNpcs.Add(npc);
            bossPortraits.Add(ReadPortraits(npc));
        }
        for (int i = 0; i < npcs.arraySize; i++)
        {
            var entry = npcs.GetArrayElementAtIndex(i);
            var npc = entry.FindPropertyRelative("npcData").objectReferenceValue as NPCData;
            if (npc != null) npcStories[npc.id] = entry.FindPropertyRelative("storyText").stringValue;
        }

        Undo.RecordObject(catalog, "Populate encyclopedia creature display data");
        for (int i = 0; i < monsters.arraySize; i++)
        {
            var entry = monsters.GetArrayElementAtIndex(i);
            var preview = previews[i];
            entry.FindPropertyRelative("sourcePrefab").objectReferenceValue = preview.source;
            var frames = entry.FindPropertyRelative("previewFrames");
            frames.arraySize = preview.frames.Length;
            for (int j = 0; j < preview.frames.Length; j++)
                frames.GetArrayElementAtIndex(j).objectReferenceValue = preview.frames[j];
            entry.FindPropertyRelative("previewFrameRate").floatValue = preview.frameRate;
        }
        for (int i = 0; i < bosses.arraySize; i++)
        {
            var entry = bosses.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("npcData").objectReferenceValue = bossNpcs[i];
            WritePortraits(entry.FindPropertyRelative("portraits"), bossPortraits[i]);
        }
        npcs.arraySize = npcData.Length;
        for (int i = 0; i < npcData.Length; i++)
        {
            var entry = npcs.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("npcData").objectReferenceValue = npcData[i];
            entry.FindPropertyRelative("storyText").stringValue =
                npcStories.TryGetValue(npcData[i].id, out string story) ? story : string.Empty;
            WritePortraits(entry.FindPropertyRelative("portraits"), npcPortraits[i]);
        }
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(catalog);
    }

    public static void WireMonsterIdentities()
    {
        var catalog = Load<EncyclopediaCatalogSO>(CatalogPath);
        foreach (var entry in catalog.MonsterEntries)
        {
            if (entry == null || entry.sourcePrefab == null || string.IsNullOrWhiteSpace(entry.id))
                throw new InvalidOperationException("Populate the monster catalog before authoring identities.");
            string path = AssetDatabase.GetAssetPath(entry.sourcePrefab);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var identity = contents.GetComponent<MonsterEncyclopediaIdentity>();
                bool added = identity == null;
                if (added) identity = contents.AddComponent<MonsterEncyclopediaIdentity>();
                var serialized = new SerializedObject(identity);
                var id = Required(serialized, "monsterId");
                if (!added && id.stringValue == entry.id) continue;
                id.stringValue = entry.id;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
    }

    public static string Report(EncyclopediaCatalogSO catalog)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        var report = new StringBuilder();
        report.AppendLine($"Encyclopedia creatures: {catalog.MonsterEntries.Count} monsters, {catalog.BossEntries.Count} bosses, {catalog.NpcEntries.Count} NPCs.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in catalog.MonsterEntries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.id) || !ids.Add(entry.id) || entry.sourcePrefab == null ||
                entry.previewFrames == null || entry.previewFrames.Length == 0 || entry.previewFrames.Any(sprite => sprite == null) ||
                entry.previewFrameRate <= 0f)
                throw new InvalidOperationException("Invalid monster catalog source/ID/preview.");
            var identity = entry.sourcePrefab.GetComponent<MonsterEncyclopediaIdentity>();
            if (identity == null || Required(new SerializedObject(identity), "monsterId").stringValue != entry.id)
                throw new InvalidOperationException("Monster identity does not match catalog: " + entry.id);
            report.AppendLine($"{entry.id}: {entry.previewFrames.Length} frames @ {entry.previewFrameRate:0.###} FPS, {AssetDatabase.GetAssetPath(entry.sourcePrefab)}");
        }
        foreach (var entry in catalog.BossEntries)
        {
            ValidatePortraits(entry.npcData, entry.portraits);
            report.AppendLine($"Boss {entry.npcData.npcName} ({entry.npcData.id}): {entry.portraits.Count} portraits.");
        }
        foreach (var entry in catalog.NpcEntries)
        {
            ValidatePortraits(entry.npcData, entry.portraits);
            report.AppendLine($"NPC {entry.npcData.npcName} ({entry.npcData.id}): {entry.portraits.Count} portraits; story {(string.IsNullOrEmpty(entry.storyText) ? "unregistered" : "preserved")}.");
        }
        Debug.Log(report.ToString());
        return report.ToString();
    }

    private static List<EncyclopediaPortrait> ReadPortraits(NPCData npc)
    {
        if (npc.spriteLibraryAsset == null) throw new InvalidOperationException("NPC portrait library missing: " + npc.name);
        var labels = npc.spriteLibraryAsset.GetCategoryLabelNames("Face");
        // The registered Dragon library also retains old portraits. This tab uses the current six Spiri illustrations.
        if (npc.id == 2001) labels = labels.Where(label => label.StartsWith("Spiri_", StringComparison.Ordinal));
        var portraits = new List<EncyclopediaPortrait>();
        var sprites = new HashSet<Sprite>();
        foreach (string label in labels.OrderBy(PortraitPriority))
        {
            var sprite = npc.spriteLibraryAsset.GetSprite("Face", label);
            if (sprite != null && sprites.Add(sprite)) portraits.Add(new EncyclopediaPortrait { label = label, sprite = sprite });
        }
        ValidatePortraits(npc, portraits);
        return portraits;
    }

    private static int PortraitPriority(string label)
    {
        if (label.Equals("Idle", StringComparison.OrdinalIgnoreCase) || label.Equals("Spiri_Idle", StringComparison.Ordinal)) return 0;
        if (label.Equals("Normal", StringComparison.OrdinalIgnoreCase)) return 1;
        if (label.Equals("default", StringComparison.OrdinalIgnoreCase)) return 2;
        return 3;
    }

    private static void ValidatePortraits(NPCData npc, IReadOnlyList<EncyclopediaPortrait> portraits)
    {
        if (npc == null || portraits == null || portraits.Count == 0 || portraits.Any(portrait => portrait == null || portrait.sprite == null))
            throw new InvalidOperationException("NPC portrait catalog contains missing data.");
        if (portraits.Select(portrait => portrait.sprite).Distinct().Count() != portraits.Count)
            throw new InvalidOperationException("NPC portrait aliases were not deduplicated: " + npc.name);
    }

    private static void WritePortraits(SerializedProperty target, IReadOnlyList<EncyclopediaPortrait> portraits)
    {
        target.arraySize = portraits.Count;
        for (int i = 0; i < portraits.Count; i++)
        {
            var portrait = target.GetArrayElementAtIndex(i);
            portrait.FindPropertyRelative("label").stringValue = portraits[i].label;
            portrait.FindPropertyRelative("sprite").objectReferenceValue = portraits[i].sprite;
        }
    }

    private static AnimationClip FindIdleClip(GameObject source, string id)
    {
        if (id == "monster.shadow_servant") return Load<AnimationClip>(ShadowServantIdlePath);
        var animator = source.GetComponent<Animator>();
        if (animator == null) animator = source.GetComponentInChildren<Animator>(true);
        var runtime = animator != null ? animator.runtimeAnimatorController : null;
        if (runtime == null) throw new InvalidOperationException("Monster Animator controller missing: " + id);
        var idle = runtime.animationClips.Where(clip => clip != null && clip.name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(clip => clip.name, StringComparer.Ordinal).FirstOrDefault();
        if (idle != null) return idle;
        // DeadsSkeleton's authored default resting animation is named Float rather than Idle.
        var controller = runtime as AnimatorController;
        var defaultClip = controller != null && controller.layers.Length > 0
            ? controller.layers[0].stateMachine.defaultState?.motion as AnimationClip : null;
        if (defaultClip == null) throw new InvalidOperationException("Monster resting animation missing: " + id);
        return defaultClip;
    }

    private static Preview ReadPreview(GameObject source, AnimationClip clip, string id)
    {
        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip)
            .Where(binding => binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Sprite")
            .OrderBy(binding => binding.path, StringComparer.Ordinal).ToArray();
        if (bindings.Length == 0) throw new InvalidOperationException("No sprite curve in monster resting clip: " + id);
        var keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
        if (keys == null || keys.Length == 0 || keys.Any(key => !(key.value is Sprite)))
            throw new InvalidOperationException("Monster resting sprite curve is incomplete: " + id);
        float interval = keys.Length > 1 ? keys.Zip(keys.Skip(1), (a, b) => b.time - a.time).Min() : 1f / clip.frameRate;
        if (interval <= 0f) throw new InvalidOperationException("Monster sprite key timing is invalid: " + id);
        float rate = Mathf.Round(1000f / interval) / 1000f;
        int count = Mathf.Max(1, Mathf.CeilToInt(clip.length * rate - 0.001f));
        var frames = new Sprite[count];
        int keyIndex = 0;
        for (int frame = 0; frame < count; frame++)
        {
            float time = frame / rate;
            while (keyIndex + 1 < keys.Length && keys[keyIndex + 1].time <= time + 0.0001f) keyIndex++;
            frames[frame] = (Sprite)keys[keyIndex].value;
        }
        return new Preview { source = source, frames = frames, frameRate = rate };
    }

    private static SerializedProperty Required(SerializedObject owner, string name) => owner.FindProperty(name) ??
        throw new InvalidOperationException("Required encyclopedia authoring field missing: " + name);

    private static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path) ??
        throw new InvalidOperationException("Required encyclopedia source asset missing: " + path);
}
#endif
