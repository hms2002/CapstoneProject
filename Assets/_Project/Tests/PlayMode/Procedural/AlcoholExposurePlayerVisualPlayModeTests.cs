#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>Checks authored exposure projection, reuse, drain visibility and head anchoring.</summary>
public sealed class AlcoholExposurePlayerVisualPlayModeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Type ViewType => Type.GetType("UnityGAS.AlcoholExposurePlayerVisual, Presentation", true);
    private GameObject player;
    private GameObject puddleObject;
    private PlayerAlcoholExposure exposure;
    private AlcoholPuddleArea puddle;
    private MonoBehaviour view;
    private Transform visualRoot;
    private SpriteRenderer mug;
    private SpriteRenderer[] drops;

    [SetUp]
    public void Setup()
    {
        player = new GameObject("AlcoholHeadVisualTest");
        player.SetActive(false);
        exposure = player.AddComponent<PlayerAlcoholExposure>();
        visualRoot = new GameObject("AuthoredAlcoholVisual").transform;
        visualRoot.SetParent(player.transform, false);
        visualRoot.localPosition = new Vector3(0f, 1.8f, 0f);
        visualRoot.localScale = new Vector3(0.8f, 0.8f, 1f);
        mug = AddRenderer("Mug", new Vector3(0f, -0.36f, 0f));
        drops = new[]
        {
            AddRenderer("LeftDrop", new Vector3(-0.19f, 0.06f, 0f)),
            AddRenderer("RightDrop", new Vector3(0.17f, 0.1f, 0f)),
            AddRenderer("RightLowerDrop", new Vector3(0.22f, 0.02f, 0f))
        };
        view = (MonoBehaviour)player.AddComponent(ViewType);
        ViewType.GetField("exposure", Private).SetValue(view, exposure);
        ViewType.GetField("visualRoot", Private).SetValue(view, visualRoot);
        ViewType.GetField("mug", Private).SetValue(view, mug);
        ViewType.GetField("overflowDroplets", Private).SetValue(view, drops);
        player.SetActive(true);
        puddleObject = new GameObject("AlcoholContactTest");
        puddle = puddleObject.AddComponent<AlcoholPuddleArea>();
    }

    [TearDown]
    public void Cleanup()
    {
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(puddleObject);
    }

    private SpriteRenderer AddRenderer(string name, Vector3 position)
    {
        var child = new GameObject(name);
        child.transform.SetParent(visualRoot, false);
        child.transform.localPosition = position;
        return child.AddComponent<SpriteRenderer>();
    }

    private void TickExposure(float seconds)
        => typeof(PlayerAlcoholExposure).GetMethod("Tick", Private).Invoke(exposure, new object[] { seconds });
    private void TickView() => ViewType.GetMethod("LateUpdate", Private).Invoke(view, null);
    private float DisplayedFill()
    {
        var block = new MaterialPropertyBlock();
        mug.GetPropertyBlock(block);
        return block.GetFloat("_FillAmount");
    }

    [Test]
    public void ActualPlayerPrefabReferencesAuthoredMugAndDroplets()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PF Player.prefab");
        Component authored = prefab.GetComponent(ViewType);
        Assert.That(authored, Is.Not.Null);
        var authoredExposure = (PlayerAlcoholExposure)ViewType.GetField("exposure", Private).GetValue(authored);
        var root = (Transform)ViewType.GetField("visualRoot", Private).GetValue(authored);
        var renderer = (SpriteRenderer)ViewType.GetField("mug", Private).GetValue(authored);
        var authoredDrops = (SpriteRenderer[])ViewType.GetField("overflowDroplets", Private).GetValue(authored);
        Assert.That(authoredExposure, Is.EqualTo(prefab.GetComponent<PlayerAlcoholExposure>()));
        Assert.That(root.parent, Is.EqualTo(prefab.transform));
        Assert.That(renderer.transform.IsChildOf(root), Is.True);
        Assert.That(renderer.sprite, Is.Not.Null);
        Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Custom/AlcoholMugFill"));
        Assert.That(authoredDrops.Length, Is.EqualTo(3));
        foreach (SpriteRenderer drop in authoredDrops)
        {
            Assert.That(drop.transform.IsChildOf(root), Is.True);
            Assert.That(drop.sharedMaterial.GetFloat("_DropletMode"), Is.EqualTo(1f));
        }
    }

    [Test]
    public void FillOverflowAndDrainReuseAuthoredRenderers()
    {
        SpriteRenderer[] authored = player.GetComponentsInChildren<SpriteRenderer>(true);
        TickView();
        Assert.That(mug.enabled, Is.False);
        exposure.EnterPuddle(puddle);
        TickExposure(1.5f);
        TickView();
        Assert.That(mug.enabled, Is.True);
        Assert.That(DisplayedFill(), Is.EqualTo(0.5f).Within(0.0001f));
        foreach (SpriteRenderer drop in drops) Assert.That(drop.enabled, Is.False);

        TickExposure(1.51f);
        TickView();
        Assert.That(DisplayedFill(), Is.EqualTo(1f));
        foreach (SpriteRenderer drop in drops) Assert.That(drop.enabled, Is.True);

        exposure.ExitPuddle(puddle);
        TickExposure(0.75f);
        TickView();
        Assert.That(mug.enabled, Is.True);
        Assert.That(DisplayedFill(), Is.EqualTo(0.5f).Within(0.0001f));
        foreach (SpriteRenderer drop in drops) Assert.That(drop.enabled, Is.False);
        exposure.EnterPuddle(puddle);
        TickExposure(0.3f);
        TickView();
        Assert.That(DisplayedFill(), Is.EqualTo(0.6f).Within(0.0001f));
        foreach (SpriteRenderer drop in drops) Assert.That(drop.enabled, Is.False);
        exposure.ExitPuddle(puddle);
        TickExposure(1.5f);
        TickView();
        Assert.That(mug.enabled, Is.False);
        Assert.That(player.GetComponentsInChildren<SpriteRenderer>(true), Is.EqualTo(authored));
    }

    [Test]
    public void HeadAnchorIgnoresOwnerMirroringAndDisableRestoresAuthoredPose()
    {
        Vector3 originalPosition = visualRoot.localPosition;
        Vector3 originalScale = visualRoot.localScale;
        Vector3[] originalDrops = Array.ConvertAll(drops, drop => drop.transform.localPosition);
        exposure.EnterPuddle(puddle);
        TickExposure(3.1f);
        player.transform.position = new Vector3(4f, 5f, 0f);
        player.transform.localScale = new Vector3(-2f, 3f, 1f);
        TickView();
        Assert.That(visualRoot.position, Is.EqualTo(player.transform.position + originalPosition));
        Assert.That(Mathf.Abs(visualRoot.lossyScale.x), Is.EqualTo(0.8f).Within(0.0001f));
        Assert.That(Mathf.Abs(visualRoot.lossyScale.y), Is.EqualTo(0.8f).Within(0.0001f));
        Assert.That(visualRoot.TransformVector(Vector3.right).x, Is.GreaterThan(0f));
        Assert.That(visualRoot.TransformVector(Vector3.up).y, Is.GreaterThan(0f));
        view.enabled = false;
        Assert.That(mug.enabled, Is.False);
        Assert.That(visualRoot.localPosition, Is.EqualTo(originalPosition));
        Assert.That(visualRoot.localScale, Is.EqualTo(originalScale));
        Assert.That(visualRoot.localRotation, Is.EqualTo(Quaternion.identity));
        for (int i = 0; i < drops.Length; i++)
        {
            Assert.That(drops[i].enabled, Is.False);
            Assert.That(drops[i].transform.localPosition, Is.EqualTo(originalDrops[i]));
            Assert.That(drops[i].color, Is.EqualTo(Color.white));
        }
    }

    [Test]
    public void DestroyedAuthoredRootKeepsTickAndDisableSafe()
    {
        exposure.EnterPuddle(puddle);
        TickExposure(3.1f);
        TickView();
        Object.DestroyImmediate(visualRoot.gameObject);
        Assert.DoesNotThrow(TickView);
        Assert.DoesNotThrow(() => view.enabled = false);
        Assert.That(player.GetComponentsInChildren<SpriteRenderer>(true), Is.Empty,
            "A missing authored hierarchy never creates replacement renderers.");
        Assert.That(exposure.IsOverflowing, Is.True, "Presentation cleanup does not mutate exposure.");
    }

    [UnityTest]
    public IEnumerator OverflowAnimatesWithScaledTimePausesAndStopsOnDrain()
    {
        float previousScale = Time.timeScale;
        try
        {
            Time.timeScale = 1f;
            exposure.EnterPuddle(puddle);
            TickExposure(3.1f);
            yield return null;
            yield return null;
            Quaternion firstRotation = mug.transform.localRotation;
            Vector3 firstDrop = drops[0].transform.localPosition;
            bool sawSway = false;
            bool sawDropTravel = false;
            for (int sample = 0; sample < 3; sample++)
            {
                yield return new WaitForSecondsRealtime(0.06f);
                sawSway |= Quaternion.Angle(firstRotation, mug.transform.localRotation) > 0.2f;
                sawDropTravel |= Vector3.Distance(firstDrop, drops[0].transform.localPosition) > 0.0001f;
            }
            Assert.That(sawSway, Is.True, "The overflow mug sways across native frames.");
            Assert.That(sawDropTravel, Is.True, "The authored overflow drops travel across native frames.");
            foreach (SpriteRenderer drop in drops) Assert.That(drop.enabled, Is.True);

            Time.timeScale = 0f;
            yield return null;
            yield return null;
            Quaternion pausedRotation = mug.transform.localRotation;
            Vector3 pausedDrop = drops[0].transform.localPosition;
            Color pausedColor = drops[0].color;
            yield return new WaitForSecondsRealtime(0.08f);
            Assert.That(Quaternion.Angle(pausedRotation, mug.transform.localRotation), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(pausedDrop, drops[0].transform.localPosition), Is.LessThan(0.0001f));
            Assert.That(drops[0].color, Is.EqualTo(pausedColor), "Pause freezes the drop fade too.");

            exposure.ExitPuddle(puddle);
            TickExposure(0.2f);
            TickView();
            Assert.That(mug.enabled, Is.True, "Drain remains visible while its fill is nonzero.");
            Assert.That(mug.transform.localRotation, Is.EqualTo(Quaternion.identity));
            foreach (SpriteRenderer drop in drops) Assert.That(drop.enabled, Is.False);
        }
        finally { Time.timeScale = previousScale; }
    }
}
#endif
