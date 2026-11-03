#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

/// <summary>Native TMP geometry/RNG parity and authored menu placement checks without a UI assembly dependency.</summary>
public sealed class UiTextMeshRefreshRegressionPlayModeTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<GameObject> owned = new();
    private Type textType, animatorType, menuType;
    private Object authoredFont;

    [SetUp]
    public void SetUp()
    {
        textType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro", true);
        animatorType = Type.GetType("SimpleTextAnimator, UI", true);
        menuType = Type.GetType("MenuButtonHighlightPresentation, UI", true);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab");
        var authored = prefab.GetComponentInChildren(animatorType, true);
        var text = authored.GetComponent(textType);
        authoredFont = (Object)textType.GetProperty("font").GetValue(text);
        Assert.That(authoredFont, Is.Not.Null);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }

    private GameObject Own(GameObject go) { owned.Add(go); return go; }

    private Component NewText(string content, int visible = int.MaxValue)
    {
        var canvas = Own(new GameObject("TMP regression canvas", typeof(RectTransform), typeof(Canvas)));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var root = Own(new GameObject("TMP regression text", typeof(RectTransform)));
        root.transform.SetParent(canvas.transform, false);
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(1000, 300);
        var text = root.AddComponent(textType);
        SetProperty(text, "font", authoredFont);
        SetProperty(text, "fontSize", 32f);
        SetProperty(text, "enableAutoSizing", false);
        SetProperty(text, "text", content);
        SetProperty(text, "maxVisibleCharacters", visible);
        Force(text);
        return text;
    }

    private static void SetProperty(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static object Field(object target, string name) => target.GetType().GetField(name, Fields).GetValue(target);
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, Fields).Invoke(target, null);
    private static void Force(Component text) => text.GetType().GetMethod("ForceMeshUpdate").Invoke(text, new object[] { false, false });
    private static int Counter(object target, string name) => (int)Property(target, name);

    private static Vector3[][] Geometry(Component text)
    {
        object info = Property(text, "textInfo");
        var meshes = (Array)Field(info, "meshInfo");
        var vertices = new Vector3[meshes.Length][];
        for (int i = 0; i < meshes.Length; i++)
            vertices[i] = (Vector3[])((Vector3[])Field(meshes.GetValue(i), "vertices")).Clone();
        return vertices;
    }

    private static void AssertGeometry(Vector3[][] expected, Vector3[][] actual)
    {
        Assert.AreEqual(expected.Length, actual.Length);
        for (int material = 0; material < expected.Length; material++)
        {
            Assert.AreEqual(expected[material].Length, actual[material].Length);
            for (int vertex = 0; vertex < expected[material].Length; vertex++)
                Assert.That(Vector3.Distance(expected[material][vertex], actual[material][vertex]), Is.LessThan(0.00001f),
                    $"material {material}, vertex {vertex}");
        }
    }

    [Test]
    public void PlainStaticText_DoesNotForceUploadOrOverwriteExternalGeometry()
    {
        var text = NewText("Plain <color=#ff0000>text</color>");
        var animator = text.gameObject.AddComponent(animatorType);
        object info = Property(text, "textInfo");
        var meshes = (Array)Field(info, "meshInfo");
        Vector3[] vertices = (Vector3[])Field(meshes.GetValue(0), "vertices");
        vertices[0] += new Vector3(3f, 2f, 0f); // Another effect's geometry remains its own.
        Vector3[][] before = Geometry(text);
        Random.State state = Random.state;
        for (int i = 0; i < 30; i++) Invoke(animator, "Update");
        Assert.AreEqual(0, Counter(animator, "DiagnosticForceMeshRefreshCount"));
        Assert.AreEqual(0, Counter(animator, "DiagnosticGeometryUploadCount"));
        Assert.AreEqual(state, Random.state);
        AssertGeometry(before, Geometry(text));
    }

    [TestCase("Plain text", 0)]
    [TestCase("Plain text", 2)]
    [TestCase("Plain text", int.MaxValue)]
    [TestCase("<link=wave>Wave</link>", 0)]
    [TestCase("<link=wave>Wave</link>", 2)]
    [TestCase("<link=wave>Wave</link>", int.MaxValue)]
    [TestCase("<link=\"wave\">W a v e</link>", 0)]
    [TestCase("<link=\"wave\">W a v e</link>", 2)]
    [TestCase("<link=\"wave\">W a v e</link>", int.MaxValue)]
    [TestCase("<link=\"shake\">Sh a ke</link>", 0)]
    [TestCase("<link=\"shake\">Sh a ke</link>", 2)]
    [TestCase("<link=\"shake\">Sh a ke</link>", int.MaxValue)]
    [TestCase("<link=other>Other</link>", 0)]
    [TestCase("<link=other>Other</link>", 2)]
    [TestCase("<link=other>Other</link>", int.MaxValue)]
    [TestCase("<link=wave>A</link>+<link=shake>B C</link>", 0)]
    [TestCase("<link=wave>A</link>+<link=shake>B C</link>", 2)]
    [TestCase("<link=wave>A</link>+<link=shake>B C</link>", int.MaxValue)]
    [TestCase("<link=shake>X</link><link=wave>Y</link><link=other>Z</link>", 0)]
    [TestCase("<link=shake>X</link><link=wave>Y</link><link=other>Z</link>", 2)]
    [TestCase("<link=shake>X</link><link=wave>Y</link><link=other>Z</link>", int.MaxValue)]
    public void OriginalAndCurrentEffectGeometryAndRngRemainEquivalent(string content, int visible)
    {
        Component original = NewText(content, visible);
        Component current = NewText(content, visible);
        Component animator = current.gameObject.AddComponent(animatorType);
        Random.State saved = Random.state;
        try
        {
            for (int frame = 0; frame < 3; frame++)
            {
                Random.InitState(841 + frame);
                Random.State input = Random.state;
                OriginalUpdate(original, 10f, 5f, 3f);
                Random.State expectedState = Random.state;
                Vector3[][] expected = Geometry(original);
                Random.state = input;
                Invoke(animator, "Update");
                Assert.AreEqual(expectedState, Random.state, "Random calls retain link/character traversal order.");
                AssertGeometry(expected, Geometry(current));
            }
        }
        finally { Random.state = saved; }
    }

    [Test]
    public void SwitchingTextAndFontProperties_ReclassifiesEffectsAndClearsOldOffsets()
    {
        Component original = NewText("Plain text");
        Component current = NewText("Plain text");
        Component animator = current.gameObject.AddComponent(animatorType);
        Random.State saved = Random.state;
        try
        {
            foreach (string content in new[] { "<link=wave>Changed wave</link>", "Ordinary text", "<link=shake>Changed shake</link>" })
            {
                SetProperty(original, "text", content); SetProperty(current, "text", content);
                SetProperty(original, "fontSize", 40f); SetProperty(current, "fontSize", 40f);
                Random.InitState(1953);
                Random.State input = Random.state;
                OriginalUpdate(original, 10f, 5f, 3f);
                Random.State expected = Random.state;
                Random.state = input;
                Invoke(animator, "Update");
                Assert.AreEqual(expected, Random.state);
                // Normal TMP rendering completes a plain-text input change itself.
                if (content == "Ordinary text") Force(current);
                AssertGeometry(Geometry(original), Geometry(current));
            }
        }
        finally { Random.state = saved; }
    }

    // Exact previous algorithm: regenerate first, preserve link order and visible-character RNG calls.
    private static void OriginalUpdate(Component text, float speed, float height, float shake)
    {
        object info = Property(text, "textInfo");
        if ((int)Field(info, "characterCount") == 0) return;
        Force(text);
        info = Property(text, "textInfo");
        var links = (Array)Field(info, "linkInfo");
        var characters = (Array)Field(info, "characterInfo");
        var meshes = (Array)Field(info, "meshInfo");
        for (int i = 0; i < (int)Field(info, "linkCount"); i++)
        {
            object link = links.GetValue(i);
            string id = (string)link.GetType().GetMethod("GetLinkID").Invoke(link, null);
            int first = (int)Field(link, "linkTextfirstCharacterIndex");
            int count = (int)Field(link, "linkTextLength");
            for (int j = first; j < first + count; j++)
            {
                object character = characters.GetValue(j);
                if (!(bool)Field(character, "isVisible")) continue;
                int material = (int)Field(character, "materialReferenceIndex");
                int index = (int)Field(character, "vertexIndex");
                var vertices = (Vector3[])Field(meshes.GetValue(material), "vertices");
                Vector3 offset = Vector3.zero;
                if (id == "wave") offset = new Vector3(0, Mathf.Sin(Time.time * speed + j) * height, 0);
                else if (id == "shake") offset = new Vector3(Random.Range(-shake, shake), Random.Range(-shake, shake), 0);
                for (int corner = 0; corner < 4; corner++) vertices[index + corner] += offset;
            }
        }
        for (int i = 0; i < meshes.Length; i++)
        {
            Mesh mesh = (Mesh)Field(meshes.GetValue(i), "mesh");
            mesh.vertices = (Vector3[])Field(meshes.GetValue(i), "vertices");
            text.GetType().GetMethod("UpdateGeometry").Invoke(text, new object[] { mesh, i });
        }
    }

    [Test]
    public void AuthoredMenuHighlight_ReusesBoundsAndKeepsPlacementDuringTransformLayoutChanges()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab");
        var authored = prefab.GetComponentInChildren(menuType, true);
        RectTransform authoredSword = (RectTransform)Field(authored, "swordRoot");
        Assert.That(authoredSword, Is.Not.Null);
        // Sword visuals can be authored as menu siblings. Clone the common menu
        // subtree so Unity remaps that serialized reference into this fixture.
        Transform logicalRoot = authored.transform;
        while (logicalRoot != null && !authoredSword.IsChildOf(logicalRoot)) logicalRoot = logicalRoot.parent;
        Assert.That(logicalRoot, Is.Not.Null);
        string buttonPath = RelativePath(logicalRoot, authored.transform);
        string swordPath = RelativePath(logicalRoot, authoredSword);
        var canvas = Own(new GameObject("Menu regression canvas", typeof(RectTransform), typeof(Canvas)));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var staging = Own(new GameObject("Menu regression staging"));
        staging.SetActive(false);
        var menuObject = Own(Object.Instantiate(logicalRoot.gameObject, staging.transform));
        var buttonObject = string.IsNullOrEmpty(buttonPath) ? menuObject : menuObject.transform.Find(buttonPath).gameObject;
        Component menu = buttonObject.GetComponent(menuType);
        foreach (MonoBehaviour behaviour in menuObject.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour != menu && behaviour is not UnityEngine.EventSystems.UIBehaviour &&
                behaviour.GetType().Name != "LocalizeTmpFontEvent")
                Object.DestroyImmediate(behaviour);
        RectTransform sword = (RectTransform)Field(menu, "swordRoot");
        Assert.That(sword, Is.Not.Null);
        // Verify remapping while inactive: Awake/OnEnable can move and hide the
        // sword, so a bad fixture must fail before it can touch a prefab asset.
        Assert.IsTrue(sword.IsChildOf(menuObject.transform), "The authored fixture must own its sword reference.");
        Assert.AreSame(menuObject.transform.Find(swordPath), sword, "The sibling reference must be remapped into the cloned menu subtree.");
        Assert.AreNotSame(authoredSword, sword, "The test must not mutate a prefab-asset reference.");
        menuObject.transform.SetParent(canvas.transform, false);
        Component label = (Component)Field(menu, "labelText");
        Assert.That(label, Is.Not.Null);
        Canvas.ForceUpdateCanvases();
        MethodInfo refresh = menuType.GetMethod("RefreshSwordVisiblePosition", Fields);
        refresh.Invoke(menu, null);
        int baseline = Counter(menu, "DiagnosticLabelMeshRefreshCount");
        for (int i = 0; i < 30; i++) refresh.Invoke(menu, null);
        Assert.AreEqual(baseline, Counter(menu, "DiagnosticLabelMeshRefreshCount"));

        RectTransform labelRect = (RectTransform)label.transform;
        labelRect.anchoredPosition += new Vector2(65, 25);
        buttonObject.transform.localScale = new Vector3(1.3f, 0.8f, 1f);
        refresh.Invoke(menu, null);
        Vector2 cachedPosition = (Vector2)Field(menu, "swordVisiblePosition");
        Force(label); // Read the independent native bounds used by the previous placement algorithm.
        Assert.That(Vector2.Distance(cachedPosition, OriginalSwordPosition(label, sword)), Is.LessThan(0.001f));
        Assert.AreEqual(baseline, Counter(menu, "DiagnosticLabelMeshRefreshCount"), "Transform following does not regenerate label geometry.");

        SetProperty(label, "text", "Longer localized menu label");
        labelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 350f);
        SetProperty(label, "fontSize", 25f);
        refresh.Invoke(menu, null);
        Assert.Greater(Counter(menu, "DiagnosticLabelMeshRefreshCount"), baseline);
        cachedPosition = (Vector2)Field(menu, "swordVisiblePosition");
        Force(label);
        Assert.That(Vector2.Distance(cachedPosition, OriginalSwordPosition(label, sword)), Is.LessThan(0.001f));
        int changed = Counter(menu, "DiagnosticLabelMeshRefreshCount");
        refresh.Invoke(menu, null); // Consume the native refresh notification from the independent check.
        changed = Counter(menu, "DiagnosticLabelMeshRefreshCount");
        refresh.Invoke(menu, null);
        Assert.AreEqual(changed, Counter(menu, "DiagnosticLabelMeshRefreshCount"));
    }

    private static string RelativePath(Transform ancestor, Transform descendant)
    {
        var parts = new List<string>();
        for (Transform current = descendant; current != ancestor; current = current.parent)
        {
            if (current == null) throw new InvalidOperationException("The authored menu reference is outside its logical subtree.");
            parts.Insert(0, current.name);
        }
        return string.Join("/", parts);
    }

    private static Vector2 OriginalSwordPosition(Component label, RectTransform sword)
    {
        var parent = (RectTransform)sword.parent;
        Bounds bounds = (Bounds)Property(label, "textBounds");
        Vector3 left = parent.InverseTransformPoint(label.transform.TransformPoint(new Vector3(bounds.min.x, bounds.center.y, 0)));
        var corners = new Vector3[4]; sword.GetWorldCorners(corners);
        float right = float.NegativeInfinity, bottom = float.PositiveInfinity, top = float.NegativeInfinity;
        foreach (Vector3 corner in corners)
        {
            Vector3 point = parent.InverseTransformPoint(corner);
            right = Mathf.Max(right, point.x); bottom = Mathf.Min(bottom, point.y); top = Mathf.Max(top, point.y);
        }
        return sword.anchoredPosition + new Vector2(left.x - 12f - right, left.y - (bottom + top) * 0.5f);
    }
}
#endif

