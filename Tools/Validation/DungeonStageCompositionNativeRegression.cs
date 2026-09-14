// Isolated Unity probe using current Core/Gameplay DLLs and stage-probe.json exported
// from production room layouts/profiles (no tile or prefab instantiation).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class DungeonStageCompositionNativeRegression
{
    [Serializable] public class Input { public Profile[] profiles; }
    [Serializable] public class Profile
    {
        public string theme, policyJson; public Room[] rooms; public string[] guaranteed;
        public int seed, count, attempts, minimum, variation; public float ratio;
    }
    [Serializable] public class Room
    {
        public string name, guid, shape; public int w, h, type, tier, size, reward;
        public float weight; public Socket[] sockets; public RoomTopologyPlacementData placement;
    }
    [Serializable] public class Socket { public string id; public int x, y, direction, width; }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Set(object obj, string name, object value) =>
        obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
    public static void Run()
    {
        try
        {
            Check(DungeonStageComposition.Allocate(10, 1, 4).SequenceEqual(new[] { 10, 0, 0 }), "stage1 ten");
            Check(DungeonStageComposition.Allocate(10, 2, 4).SequenceEqual(new[] { 5, 5, 0 }), "stage2 ten");
            Check(DungeonStageComposition.Allocate(10, 3, 4).SequenceEqual(new[] { 2, 4, 4 }), "stage3 ten");
            Check(DungeonStageComposition.Allocate(8, 3, 4).SequenceEqual(new[] { 2, 3, 3 }), "stage3 rounding");
            var winners = new HashSet<int>();
            for (int seed = 0; seed < 64; seed++)
            {
                var counts = DungeonStageComposition.Allocate(7, 2, seed);
                Check(counts.Sum() == 7 && counts[2] == 0, "sum and future tier");
                Check(counts.SequenceEqual(DungeonStageComposition.Allocate(7, 2, seed)), "rounding seed");
                winners.Add(counts[0]);
            }
            Check(winners.Count == 2, "rounding tie must not always favor same stage");
            int cases = 0;
            foreach (var p in JsonUtility.FromJson<Input>(File.ReadAllText("Assets/stage-probe.json")).profiles)
            {
                var library = ScriptableObject.CreateInstance<RoomThemeLibrarySO>(); Set(library, "themeId", p.theme);
                var rooms = new Dictionary<string, RoomTemplateSO>(); var shapes = new Dictionary<string, RoomShapeTagSO>();
                foreach (var dto in p.rooms)
                {
                    var room = ScriptableObject.CreateInstance<RoomTemplateSO>(); room.name = dto.name;
                    RoomShapeTagSO shape = null;
                    if (!string.IsNullOrEmpty(dto.shape))
                    { if (!shapes.TryGetValue(dto.shape, out shape)) shapes.Add(dto.shape, shape = ScriptableObject.CreateInstance<RoomShapeTagSO>()); }
                    var data = new RoomLayoutData { roomId = dto.name, roomType = (RoomType)dto.type,
                        size = new Vector2Int(dto.w, dto.h), localBounds = new RectInt(0, 0, dto.w, dto.h),
                        difficultyTier = dto.tier, selectionWeight = dto.weight, shapeTag = shape,
                        combatMetadata = new RoomCombatMetadata { sizeTag = (RoomCombatSizeTag)dto.size, killLockRewardTag = (RoomKillLockRewardTag)dto.reward },
                        topologyPlacement = dto.placement,
                        sockets = dto.sockets.Select(s => new RoomSocketData { socketId = s.id, localCell = new Vector2Int(s.x, s.y), direction = (RoomSocketDirection)s.direction, width = s.width }).ToList() };
                    Set(room, "layoutData", data); rooms.Add(dto.guid, room);
                }
                Set(library, "rooms", rooms.Values.ToList());
                var policy = ScriptableObject.CreateInstance<DungeonLayoutPolicySO>(); JsonUtility.FromJsonOverwrite(p.policyJson, policy);
                var guaranteed = p.guaranteed.Select(g => rooms[g]).ToArray();
                for (int stage = 1; stage <= 3; stage++)
                {
                    for (int n = 0; n < 3; n++)
                    {
                        int seed = unchecked(p.seed + n * 997);
                        var clock = System.Diagnostics.Stopwatch.StartNew();
                        DungeonLayoutResult Build() => new DungeonGraphLayoutAssembler().Assemble(library, policy,
                            seed, p.count, p.attempts, p.minimum, p.ratio, p.variation, guaranteed, generationStage: stage);
                        var result = Build(); Validate(result, stage, seed, p.theme);
                        foreach (var t in guaranteed) Check(result.Rooms.Count(r => r.Template == t) == 1, "guaranteed room");
                        if (n == 0)
                        {
                            var repeat = Build(); Validate(repeat, stage, seed, p.theme);
                            Check(result.Rooms.Select(r => (r.Template.name, r.Origin)).SequenceEqual(repeat.Rooms.Select(r => (r.Template.name, r.Origin))), "deterministic layout");
                        }
                        Debug.Log($"STAGE_CASE_PASS {p.theme} stage={stage} seed={seed} ms={clock.ElapsedMilliseconds} {result.TemplateSelection.Description}"); cases++;
                    }
                    var legacy = new DungeonLayoutAssembler().Assemble(library, p.seed, 8, true, 1024, p.minimum, p.ratio, p.variation, stage);
                    Validate(legacy, stage, p.seed, "legacy " + p.theme); cases++;
                    var eventRoom = rooms.Values.FirstOrDefault(t => t.LayoutData.roomType == RoomType.Event && !guaranteed.Contains(t));
                    if (eventRoom != null)
                    {
                        var withEvent = guaranteed.Concat(new[] { eventRoom }).ToArray();
                        var eventLayout = new DungeonGraphLayoutAssembler().Assemble(library, policy, p.seed, p.count,
                            p.attempts, p.minimum, p.ratio, p.variation, withEvent, generationStage: stage);
                        Validate(eventLayout, stage, p.seed, "event " + p.theme);
                        Check(eventLayout.Rooms.Count(r => r.Template == eventRoom) == 1, "injected event preserved");
                        Debug.Log($"STAGE_EVENT_PASS {p.theme} stage={stage} event={eventRoom.name}"); cases++;
                    }
                }
                var missing = ScriptableObject.CreateInstance<RoomThemeLibrarySO>();
                Set(missing, "rooms", rooms.Values.Where(t => !(t.LayoutData.roomType == RoomType.Combat &&
                    RoomTemplateCombatMetadataUtility.ResolveSizeTag(t) == RoomCombatSizeTag.Large && DungeonStageComposition.Tier(t) == 3)).ToList());
                var rejected = new DungeonGraphLayoutAssembler().Assemble(missing, policy, p.seed, p.count,
                    p.attempts, p.minimum, p.ratio, p.variation, guaranteed, generationStage: 3);
                Check(!rejected.IsComplete && rejected.FailureReason.Contains("Stage 3"), "missing current Large must fail explicitly");
                Debug.Log("STAGE_MISSING_LARGE_PASS " + p.theme);
            }
            Debug.Log("STAGE_COMPOSITION_REGRESSION_PASS cases=" + cases + "; exact counts, Large tier, seed replay, legacy, missing tier.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    private static void Validate(DungeonLayoutResult result, int stage, int seed, string context)
    {
        Check(result.IsComplete, context + " stage=" + stage + " " + result.FailureReason);
        var combat = result.Rooms.Where(r => r.Template.LayoutData.roomType == RoomType.Combat).Select(r => r.Template).ToArray();
        var large = combat.Where(t => RoomTemplateCombatMetadataUtility.ResolveSizeTag(t) == RoomCombatSizeTag.Large).ToArray();
        Check(large.Length == 1 && DungeonStageComposition.Tier(large[0]) == stage, context + " Large tier/count");
        var normal = combat.Where(t => RoomTemplateCombatMetadataUtility.ResolveSizeTag(t) == RoomCombatSizeTag.Normal).ToArray();
        var counts = DungeonStageComposition.Allocate(normal.Length, stage, seed);
        for (int tier = 1; tier <= 3; tier++)
            Check(normal.Count(t => DungeonStageComposition.Tier(t) == tier) == counts[tier - 1], context + " normal quotas");
        Check(normal.All(t => DungeonStageComposition.Tier(t) <= stage), "no future tiers");
        for (int i = 0; i < result.Rooms.Count; i++)
            for (int j = i + 1; j < result.Rooms.Count; j++)
                Check(!result.Rooms[i].WorldBounds.Overlaps(result.Rooms[j].WorldBounds), "room overlap");
    }
}
