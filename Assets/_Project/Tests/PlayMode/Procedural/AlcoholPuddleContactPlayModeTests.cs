#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>Checks real hurtbox contact routing and the authored reward/status integration.</summary>
public sealed class AlcoholPuddleContactPlayModeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    private const string Effects = "Assets/_Project/Data/Abilities/Effects/";
    private readonly List<Object> owned = new();
    private PlayerAlcoholExposure exposure;
    private GameplayEffectRunner runner;
    private CombatHeightState2D height;
    private AlcoholPuddleArea puddle;
    private Collider2D firstHurtbox;
    private Collider2D secondHurtbox;
    private PlayerInteractor2D previousInteractor;
    private PuddleManager previousPuddleManager;

    [SetUp]
    public void Setup()
    {
        previousInteractor = PlayerInteractor2D.Instance;
        previousPuddleManager = (PuddleManager)typeof(PuddleManager).GetField("cachedSceneInstance", StaticPrivate).GetValue(null);
        if (Object.FindAnyObjectByType<PuddleManager>() == null)
            Own(new GameObject("Alcohol contact manager fixture")).AddComponent<PuddleManager>();
        var player = Own(new GameObject("Alcohol contact recipient"));
        player.SetActive(false);
        player.transform.position = new Vector3(10000f, 10000f, 0f);
        var body = player.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        var bodyCollider = player.AddComponent<CapsuleCollider2D>();
        bodyCollider.size = new Vector2(.2f, .3f);
        // The real prefab authors a concrete collider before its interaction sensor.
        // Reproduce that setup instead of requiring Unity to add abstract Collider2D.
        var sensorObject = new GameObject("InteractionSensor");
        sensorObject.transform.SetParent(player.transform, false);
        sensorObject.AddComponent<BoxCollider2D>().isTrigger = true;
        sensorObject.AddComponent<PlayerInteractionSensor2D>();
        player.AddComponent<AttributeSet>();
        player.AddComponent<TagSystem>();
        runner = player.AddComponent<GameplayEffectRunner>();
        player.AddComponent<AbilitySystem>();
        height = player.AddComponent<CombatHeightState2D>();
        player.AddComponent<PlayerInteractor2D>();
        exposure = player.AddComponent<PlayerAlcoholExposure>();
        Set(exposure, "alcoholBuffDefinition", Load<CombatBuffDebuffApplicationDefinition>(Effects + "CBD_Puddle_AlcoholBuff.asset"));
        Set(exposure, "confusionDefinition", Load<CombatBuffDebuffApplicationDefinition>(Effects + "CBD_Puddle_Confusion.asset"));
        firstHurtbox = Hurtbox(player, "Body hurtbox A");
        secondHurtbox = Hurtbox(player, "Body hurtbox B");
        // Interaction is present for native target classification; only exposure ticks in this fixture.
        foreach (MonoBehaviour behaviour in player.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour != exposure) behaviour.enabled = false;
        player.SetActive(true);
        var source = Own(new GameObject("Alcohol contact puddle"));
        source.transform.position = player.transform.position + Vector3.right * 10f;
        puddle = source.AddComponent<AlcoholPuddleArea>();
    }

    [TearDown]
    public void Cleanup()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        typeof(PlayerInteractor2D).GetField("<Instance>k__BackingField", StaticPrivate).SetValue(null, previousInteractor);
        typeof(PuddleManager).GetField("cachedSceneInstance", StaticPrivate).SetValue(null, previousPuddleManager);
    }

    [Test]
    public void OneOfTwoHurtboxExitsKeepsContactUntilTheLastExit()
    {
        Contact("OnTriggerEnter2D", firstHurtbox);
        Contact("OnTriggerEnter2D", secondHurtbox);
        Tick(1.5f);
        Contact("OnTriggerExit2D", firstHurtbox);
        Assert.That(exposure.HasAlcoholContact, Is.True);
        Assert.That(exposure.IsDraining, Is.False);
        Tick(.3f);
        Assert.That(exposure.FillAmount, Is.EqualTo(.6f).Within(.0001f));
        Contact("OnTriggerExit2D", secondHurtbox);
        Assert.That(exposure.HasAlcoholContact, Is.False);
        Assert.That(exposure.IsDraining, Is.True);
        Tick(.75f);
        Assert.That(exposure.FillAmount, Is.EqualTo(.3f).Within(.0001f));
    }

    [Test]
    public void LandingInsideThePuddleRegistersFromStayWithoutAnotherEnter()
    {
        height.SetAirborne(1f);
        Contact("OnTriggerEnter2D", firstHurtbox);
        Tick(.5f);
        Assert.That(exposure.HasAlcoholContact, Is.False);
        Assert.That(exposure.FillAmount, Is.Zero);
        height.SetGrounded();
        Contact("OnTriggerStay2D", firstHurtbox);
        Assert.That(exposure.HasAlcoholContact, Is.True);
        Tick(1.5f);
        Assert.That(exposure.FillAmount, Is.EqualTo(.5f).Within(.0001f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SourceDisableOrIgnitionReleasesTheRecipientImmediately(bool ignite)
    {
        Contact("OnTriggerEnter2D", firstHurtbox);
        Tick(1.5f);
        if (ignite) puddle.BeginIgnitionPresentation();
        else puddle.gameObject.SetActive(false);
        Assert.That(exposure.HasAlcoholContact, Is.False);
        Assert.That(exposure.IsDraining, Is.True);
        Tick(1.5f);
        Assert.That(exposure.FillAmount, Is.Zero);
        Assert.That(exposure.IsDraining, Is.False);
        Contact("OnTriggerStay2D", firstHurtbox);
        Assert.That(exposure.HasAlcoholContact, Is.False);
    }

    [Test]
    public void DisabledRecipientCannotBeRegrantedFromAnExistingTriggerStay()
    {
        Contact("OnTriggerEnter2D", firstHurtbox);
        Tick(1.5f);
        exposure.enabled = false;
        Contact("OnTriggerStay2D", firstHurtbox);
        Tick(3.1f);
        Assert.That(exposure.HasAlcoholContact, Is.False);
        Assert.That(exposure.FillAmount, Is.Zero);
        Assert.That(runner.ActiveEffects, Is.Empty);
        exposure.enabled = true;
        Contact("OnTriggerStay2D", firstHurtbox);
        Assert.That(exposure.HasAlcoholContact, Is.True);
        Tick(.3f);
        Assert.That(exposure.FillAmount, Is.EqualTo(.1f).Within(.0001f));
    }

    [Test]
    public void PlayerAndLevelOneRewardReferenceTheAuthoredConfusionAndExistingBuff()
    {
        var player = Load<GameObject>("Assets/_Project/Prefabs/Player/PF Player.prefab");
        var tag = Load<GameplayTag>("Assets/_Project/Resources/Tags/State.Confusion.asset");
        var confusion = Load<CombatBuffDebuffApplicationDefinition>(Effects + "CBD_Puddle_Confusion.asset");
        var buff = Load<CombatBuffDebuffApplicationDefinition>(Effects + "CBD_Puddle_AlcoholBuff.asset");
        Assert.That(tag.Path, Is.EqualTo("State.Confusion"));
        Assert.That(tag.Id, Is.GreaterThan(0));
        Assert.That(Get(player.GetComponent<PlayerIntentInput2D>(), "confusionTag"), Is.SameAs(tag));
        var authoredExposure = player.GetComponent<PlayerAlcoholExposure>();
        Assert.That(authoredExposure, Is.Not.Null);
        Assert.That(Get(authoredExposure, "alcoholBuffDefinition"), Is.SameAs(buff));
        Assert.That(Get(authoredExposure, "confusionDefinition"), Is.SameAs(confusion));
        Assert.That(confusion.GameplayEffect, Is.TypeOf<GE_StatusOnlyDuration>());
        Assert.That(confusion.GameplayEffect.duration, Is.EqualTo(5f));
        Assert.That(confusion.GameplayEffect.canStack, Is.False);
        Assert.That(confusion.GameplayEffect.grantedTags, Is.EquivalentTo(new[] { tag }));
        Assert.That(confusion.LifetimePolicy, Is.EqualTo(BuffDebuffLifetimePolicy.IndependentDuration));
        Assert.That(confusion.StatusHudDefinition.Group, Is.EqualTo(StatusHudGroup.Debuff));
        Assert.That(confusion.StatusHudDefinition.ShowDurationByDefault, Is.False);
        Assert.That(confusion.StatusHudDefinition.Icon, Is.Not.Null);
        Assert.That(buff.GameplayEffect.duration, Is.EqualTo(5f));
        var modifiers = new SerializedObject(buff.GameplayEffect).FindProperty("modifiers");
        Assert.That(modifiers.arraySize, Is.EqualTo(2));
        var move = modifiers.GetArrayElementAtIndex(0);
        var attack = modifiers.GetArrayElementAtIndex(1);
        Assert.That(AssetDatabase.GetAssetPath(move.FindPropertyRelative("attribute").objectReferenceValue), Does.EndWith("MoveSpeedMulAttribute.asset"));
        Assert.That(move.FindPropertyRelative("type").intValue, Is.EqualTo((int)ModifierType.Percent));
        Assert.That(move.FindPropertyRelative("value").floatValue, Is.EqualTo(.25f));
        Assert.That(AssetDatabase.GetAssetPath(attack.FindPropertyRelative("attribute").objectReferenceValue), Does.EndWith("AttackSpeedAddAttribute.asset"));
        Assert.That(attack.FindPropertyRelative("type").intValue, Is.EqualTo((int)ModifierType.Flat));
        Assert.That(attack.FindPropertyRelative("value").floatValue, Is.EqualTo(.4f));
        var npc = Load<NPCData>("Assets/_Project/Data/Dialogue/NPC/DragonBossNpc.asset");
        var reward = Load<InfoOnlyAffectionEffect>("Assets/_Project/Data/Dialogue/Affection/SpiriAlcoholPuddleReward.asset");
        Assert.That(npc.id, Is.EqualTo(2001));
        Assert.That(npc.affectionRewards.Count(value => value.targetLevel == 1 && value.effect == reward), Is.EqualTo(1));
        Assert.That(reward.rewardIcon, Is.SameAs(confusion.StatusHudDefinition.Icon));
    }

    [Test]
    public void NewRewardAndConfusionTextsHaveRuntimeKeysAndFiveLocaleValues()
    {
        var hud = Load<StatusHudDefinition>(Effects + "SHD_Puddle_Confusion.asset");
        var reward = Load<InfoOnlyAffectionEffect>("Assets/_Project/Data/Dialogue/Affection/SpiriAlcoholPuddleReward.asset");
        var fields = new[] { (asset: (Object)hud, field: "nameText"), (asset: (Object)hud, field: "storyText"),
            (asset: (Object)hud, field: "effectText"), (asset: (Object)reward, field: "rewardText") };
        foreach (var field in fields)
        {
            string key = GameText.AssetKey(field.asset, field.field);
            Assert.That(key, Is.EqualTo("asset." + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(field.asset)) + "." + field.field));
            foreach (string code in new[] { "ko", "en", "ja", "zh-Hans", "zh-Hant" })
            {
                Object table = Load<Object>("Assets/_Project/Data/Localization/GameText_" + code + ".asset");
                object entry = table.GetType().GetMethod("GetEntry", new[] { typeof(string) }).Invoke(table, new object[] { key });
                Assert.That(entry, Is.Not.Null, code + "/" + key);
                Assert.That((string)entry.GetType().GetProperty("Value").GetValue(entry), Is.Not.Empty, code + "/" + key);
            }
        }
    }

    [UnityTest]
    public IEnumerator AuthoredMugRendersChargeOverflowAndDrainToNativePngGrid()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            Assert.Ignore("A native graphics device is required for the representative mug capture.");
        Type viewType = Type.GetType("UnityGAS.AlcoholExposurePlayerVisual, Presentation", true);
        var prefab = Load<GameObject>("Assets/_Project/Prefabs/Player/PF Player.prefab");
        Transform authored = prefab.transform.Find("AlcoholExposureVisual");
        Assert.That(authored, Is.Not.Null);
        // Use the exact authored hierarchy/materials without running the unrelated full player bootstrap.
        var visual = Own(Object.Instantiate(authored.gameObject, exposure.transform));
        Transform visualRoot = visual.transform;
        foreach (Transform child in visual.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
        var view = (MonoBehaviour)exposure.gameObject.AddComponent(viewType);
        Set(view, "exposure", exposure);
        Set(view, "visualRoot", visualRoot);
        Set(view, "mug", visualRoot.Find("Mug").GetComponent<SpriteRenderer>());
        Set(view, "overflowDroplets", visual.GetComponentsInChildren<SpriteRenderer>().Where(value => value.name != "Mug").ToArray());
        var camera = Own(new GameObject("Alcohol mug capture camera")).AddComponent<Camera>();
        camera.transform.position = exposure.transform.position + new Vector3(0f, 1.8f, -10f);
        camera.orthographic = true;
        camera.orthographicSize = .5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.11f, .12f, .13f, 1f);
        camera.cullingMask = 1 << 31;
        var target = Own(new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32));
        target.Create();
        camera.targetTexture = target;
        var readback = Own(new Texture2D(256, 256, TextureFormat.RGBA32, false));
        var grid = Own(new Texture2D(1536, 256, TextureFormat.RGBA32, false));
        float previousScale = Time.timeScale;
        var beerPixels = new List<int>();
        try
        {
            Time.timeScale = 0f;
            Contact("OnTriggerEnter2D", firstHurtbox);
            for (int frame = 0; frame < 6; frame++)
            {
                if (frame == 1 || frame == 2) Tick(1.5f);
                if (frame == 3) Tick(.01f);
                if (frame == 4) { Contact("OnTriggerExit2D", firstHurtbox); Tick(.75f); }
                if (frame == 5) Tick(.75f);
                viewType.GetMethod("LateUpdate", Private).Invoke(view, null);
                yield return null;
                yield return null;
                Read(target, readback);
                Color32[] pixels = readback.GetPixels32();
                beerPixels.Add(pixels.Count(pixel => pixel.r > 150 && pixel.g > 80 && pixel.g < 225 && pixel.b < 90));
                grid.SetPixels32(frame * 256, 0, 256, 256, pixels);
            }
            Assert.That(beerPixels[1], Is.GreaterThan(beerPixels[0]), "Half-full mug must contain visible beer pixels.");
            Assert.That(beerPixels[2], Is.GreaterThan(beerPixels[1]), "Full mug must contain more beer pixels than half-full.");
            Assert.That(beerPixels[5], Is.Zero, "The empty drained mug must be hidden.");
            grid.Apply();
            string output = Path.GetFullPath("outputs/spiri-alcohol-exposure-2026-10-12");
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, "native-mug-states.png"), grid.EncodeToPNG());
            File.WriteAllText(Path.Combine(output, "native-mug-states.txt"), "Left to right: contact/empty, half charge, full before overflow, overflow, half drain, drained/hidden.\nNative URP camera capture of the actual authored player mug hierarchy/materials; no gameplay scene or profile changes.\n");
        }
        finally { Time.timeScale = previousScale; camera.targetTexture = null; target.Release(); }
    }

    private Collider2D Hurtbox(GameObject player, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(player.transform, false);
        var collider = child.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(.2f, .2f);
        var hurtbox = child.AddComponent<CombatHurtbox2D>();
        Set(hurtbox, "targetRoot", player);
        Set(hurtbox, "ownedColliders", new Collider2D[] { collider });
        return collider;
    }

    private void Contact(string method, Collider2D collider) => typeof(AlcoholPuddleArea).GetMethod(method, Private).Invoke(puddle, new object[] { collider });
    private void Tick(float seconds) => typeof(PlayerAlcoholExposure).GetMethod("Tick", Private).Invoke(exposure, new object[] { seconds });
    private static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.That(asset, Is.Not.Null, path);
        return asset;
    }
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private static void Read(RenderTexture target, Texture2D readback)
    {
        RenderTexture previous = RenderTexture.active;
        try { RenderTexture.active = target; readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); readback.Apply(); }
        finally { RenderTexture.active = previous; }
    }
}
#endif
