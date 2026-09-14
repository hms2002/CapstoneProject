#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using CapstoneAudio;
using CapstonePresentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>
/// Verifies that the real Witch tile renderer cannot move warning, damage or presentation geometry.
/// Owns test actors/assets and restores the temporarily intercepted presentation backend.
/// </summary>
public sealed class WitchNormalAttack1GeometryPlayModeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string TilePath = "Assets/_Project/Prefabs/Bosses/ShadowBoss/WitchNormalAttack1Tile.prefab";
    private const string StylePath = "Assets/_Project/Data/Abilities/TelegraphStyles/Bosses/ShadowBoss/AttackTelegraphStyle_WitchNormalAttack1Hit.asset";
    private static readonly Vector3 Center = new(100f, 80f, 0f);
    private static readonly Vector2 Size = new(5.1f, 10.2f);
    private readonly List<Object> owned = new();
    private IWorldPresentationBackend previousBackend;
    private RecordingPresentation presentation;

    [SetUp]
    public void SetUp()
    {
        previousBackend = (IWorldPresentationBackend)typeof(WorldPresentationPlayback)
            .GetField("backend", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        presentation = new RecordingPresentation();
        WorldPresentationPlayback.RegisterBackend(presentation);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null)
                Object.DestroyImmediate(owned[i]);
        owned.Clear();
        WorldPresentationPlayback.RegisterBackend(previousBackend);
    }

    [TestCase(0f)]
    [TestCase(90f)]
    [TestCase(180f)]
    [TestCase(-90f)]
    [TestCase(37f)]
    [TestCase(173f)]
    public void WarningHitAndPresentation_KeepTheAuthoredWorldRectangle(float angle)
    {
        WitchNormalAttack1Tile tile = CreateTile();
        StartWarning(tile, Center, angle);
        AssertMeshRectangle(tile, Center, angle);
        Assert.That(Vector3.Distance(tile.transform.position, Center), Is.GreaterThan(1f),
            "The fixture must exercise the mesh renderer's shifted root, not a mock view.");
        Vector3[] warningBorder = WorldBorder(tile);

        Invoke(tile, "ShowHit");
        AssertMeshRectangle(tile, Center, angle);
        Vector3[] hitBorder = WorldBorder(tile);
        Assert.AreEqual(warningBorder.Length, hitBorder.Length);
        for (int i = 0; i < hitBorder.Length; i++)
            Assert.That(Vector3.Distance(warningBorder[i], hitBorder[i]), Is.LessThan(0.001f));
        Assert.AreEqual(1, presentation.Contexts.Count);
        Assert.That(Vector3.Distance(presentation.Contexts[0].Position, Center), Is.LessThan(0.001f));
        Assert.That(Quaternion.Angle(presentation.Contexts[0].Rotation, Quaternion.Euler(0f, 0f, angle)), Is.LessThan(0.001f));
    }

    [TestCase(0f, 0.45f, true)]
    [TestCase(0f, 0f, true)]
    [TestCase(0f, -0.45f, true)]
    [TestCase(0f, 0.6f, false)]
    [TestCase(0f, -0.6f, false)]
    [TestCase(90f, 0.45f, true)]
    [TestCase(90f, -0.6f, false)]
    [TestCase(173f, 0.45f, true)]
    [TestCase(173f, -0.6f, false)]
    public void Damage_UsesTheWarnedBox_NotTheRendererRoot(float angle, float forwardFraction, bool expectedHit)
    {
        var source = Own(new GameObject("WitchTileSourceTest"));
        source.SetActive(false);
        source.AddComponent<AttributeSet>();
        source.AddComponent<TagSystem>();
        var runner = source.AddComponent<GameplayEffectRunner>();
        var system = source.AddComponent<AbilitySystem>();
        Invoke(system, "CacheRequiredComponents");
        Invoke(runner, "Awake");

        var target = Own(new GameObject("WitchTileTargetTest"));
        target.transform.position = Center + Quaternion.Euler(0f, 0f, angle) * new Vector3(Size.x * forwardFraction, 0f, 0f);
        var attributes = target.AddComponent<AttributeSet>();
        var health = Own(ScriptableObject.CreateInstance<AttributeDefinition>());
        Invoke(attributes, "EnsureAttributeExists", health);
        Assert.IsTrue(attributes.TrySetBaseValue(health, 10f, null));
        target.AddComponent<GameplayEffectRunner>();
        target.AddComponent<BoxCollider2D>().size = Vector2.one * 0.1f;
        target.AddComponent<CombatHurtbox2D>();
        var damage = Own(ScriptableObject.CreateInstance<GE_Damage_Spec>());
        damage.healthAttribute = health;
        damage.fallbackDamage = 1f;
        damage.fallbackStunSeconds = 0f;
        damage.fallbackCameraShake = 0f;
        var payload = new CombatHitPayload
        {
            sourceSystem = system, damageEffect = damage, finalHpDamage = 1f,
            causer = source, hasResolvedElementBuildUps = true
        };
        WitchNormalAttack1Tile tile = CreateTile();
        StartWarning(tile, Center, angle, target, payload);
        Invoke(tile, "ShowHit");
        Physics2D.SyncTransforms();
        Invoke(tile, "TryHit");
        Assert.That(attributes.GetAttributeValue(health), Is.EqualTo(expectedHit ? 9f : 10f));
    }

    [Test]
    public void NewPlay_CapturesANewCenter_WithoutCarryingTheOldMeshOrigin()
    {
        WitchNormalAttack1Tile tile = CreateTile();
        StartWarning(tile, Center, 0f);
        Invoke(tile, "ShowHit");
        Vector3 newCenter = Center + new Vector3(30f, 20f, 0f);
        StartWarning(tile, newCenter, 73f);
        AssertMeshRectangle(tile, newCenter, 73f);
        Invoke(tile, "ShowHit");
        AssertMeshRectangle(tile, newCenter, 73f);
        Assert.That(Vector3.Distance(presentation.Contexts[1].Position, newCenter), Is.LessThan(0.001f));
    }

    private WitchNormalAttack1Tile CreateTile()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TilePath);
        Assert.IsNotNull(prefab);
        return Own(Object.Instantiate(prefab)).GetComponent<WitchNormalAttack1Tile>();
    }

    private static void StartWarning(WitchNormalAttack1Tile tile, Vector3 center, float angle,
        GameObject target = null, CombatHitPayload payload = null)
    {
        tile.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, 0f, angle));
        var style = AssetDatabase.LoadAssetAtPath<AttackTelegraphStyle>(StylePath);
        tile.Play(target, payload, Size, angle, 0f, 60f, style, style);
        tile.StopAllCoroutines();
    }

    private static Vector3[] WorldBorder(WitchNormalAttack1Tile tile)
    {
        var border = tile.GetComponent<LineRenderer>();
        Assert.IsNotNull(border);
        var points = new Vector3[border.positionCount];
        border.GetPositions(points);
        if (!border.useWorldSpace)
            for (int i = 0; i < points.Length; i++)
                points[i] = border.transform.TransformPoint(points[i]);
        return points;
    }

    private static void AssertMeshRectangle(WitchNormalAttack1Tile tile, Vector3 center, float angle)
    {
        var filter = tile.GetComponent<MeshFilter>();
        Assert.IsNotNull(filter);
        Assert.IsTrue(tile.GetComponent<MeshRenderer>().enabled);
        var local = new Bounds();
        bool first = true;
        Quaternion inverse = Quaternion.Inverse(Quaternion.Euler(0f, 0f, angle));
        foreach (Vector3 vertex in filter.sharedMesh.vertices)
        {
            Vector3 point = inverse * (filter.transform.TransformPoint(vertex) - center);
            if (first) { local = new Bounds(point, Vector3.zero); first = false; }
            else local.Encapsulate(point);
        }
        Assert.That(local.center.magnitude, Is.LessThan(0.001f), "The rendered rectangle moved away from the damage center.");
        Assert.That(local.size.x, Is.EqualTo(Size.x).Within(0.001f));
        Assert.That(local.size.y, Is.EqualTo(Size.y).Within(0.001f));
    }

    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private static object Invoke(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Private).Invoke(target, args);

    /// <summary>Records world effect requests without emitting audio, shake or pooled VFX during geometry tests.</summary>
    private sealed class RecordingPresentation : IWorldPresentationBackend
    {
        public readonly List<WorldPresentationContext> Contexts = new();
        public Coroutine PlayDeferredAsync(in WorldPresentationHook hook, in WorldPresentationContext context) { Contexts.Add(context); return null; }
        public void Play(in WorldPresentationHook hook, in WorldPresentationContext context) { }
        public void PlaySignalOnly(in WorldPresentationHook hook, in WorldPresentationContext context) { }
        public void PlayMerged(in WorldPresentationHook hook, SoundRef sound, CameraShakeHook shake, in WorldPresentationContext context) { }
        public GameObject SpawnOneShot(in SpawnedPresentationHook hook, in WorldPresentationContext context) => null;
        public Coroutine SpawnOneShotDeferredAsync(in SpawnedPresentationHook hook, in WorldPresentationContext context) => null;
        public GameObject SpawnPersistent(in SpawnedPresentationHook hook, in WorldPresentationContext context) => null;
        public void InitializeSpawnedPresentation(GameObject instance, bool useUnscaledTime) { }
        public void Release(GameObject instance) { }
    }
}
#endif
