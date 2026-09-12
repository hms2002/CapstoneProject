#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>Verifies terminal monster death cues and terrain-independent thin telegraphs using authored assets.</summary>
public sealed class CombatPresentationRegressionPlayModeTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string ControllerPath = "Assets/_Project/Art/Sprites/Monsters/CommonMonster/AC_GoblinWarrior.controller";
    private const string TelegraphPath = "Assets/_Project/Prefabs/VFX/Telegraphs/AttackTelegraphView.prefab";
    private readonly List<Object> owned = new();
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }

    [SetUp]
    public void SetUp()
    {
        Type.GetType("PrewarmTraceRuntime, Editor")?.GetMethod("ResetSession", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }

    [Test]
    public void GoblinController_PrioritizesDeathOverActionTransitions()
    {
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ControllerPath);
        Assert.That(controller.layers[0].stateMachine.anyStateTransitions[0].destinationState.name, Is.EqualTo("Die"));
    }

    [TestCase("Idle")]
    [TestCase("Walk")]
    [TestCase("AttackReady")]
    [TestCase("Attack")]
    [TestCase("Recover")]
    public void GoblinDeath_ClearsPendingAttackCues_AndRemainsTerminal(string startState)
    {
        var actor = Own(new GameObject("GoblinAnimationTest", typeof(SpriteRenderer), typeof(Animator)));
        var animator = actor.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        var bridge = actor.AddComponent<CommonMonsterAnimatorBridge>();
        bridge.Configure(animator, "GoblinWarrior_AttackReady", "GoblinWarrior_Attack", "GoblinWarrior_Recover", "GoblinWarrior_Die", null, null);
        animator.Play("Base Layer." + startState, 0, 0.1f);
        animator.Update(0f);
        bridge.TriggerAttackReady();
        bridge.TriggerAttack();
        bridge.TriggerRecover();
        bridge.TriggerDie();
        animator.Update(0.02f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Die"), Is.True, startState);

        // Late runner callbacks must not reopen an attack after death has consumed its trigger.
        for (int frame = 0; frame < 40; frame++)
        {
            bridge.TriggerAttackReady();
            bridge.TriggerAttack();
            bridge.TriggerRecover();
            bridge.TriggerDie();
            animator.Update(0.02f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Die"), Is.True, $"{startState}, frame={frame}");
        }
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThanOrEqualTo(1f));
    }

    [UnityTest]
    public IEnumerator GoblinDeath_DuringAttackAndHitPause_RemovesBeforeFiveSecondFallback()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Monsters/CommonCorridor/GoblinWarrior.prefab");
        var actor = Own(Object.Instantiate(prefab));
        var enemy = actor.GetComponent<GoblinWarrior>();
        enemy.SuppressMonsterLootDrop();
        enemy.enabled = false; // Suppress AI/target acquisition, not the active object's death coroutine.
        var animator = actor.GetComponentInChildren<Animator>();
        animator.Play("Base Layer.Attack", 0, 0.1f);
        animator.Update(0f);
        var bridge = actor.GetComponent<CommonMonsterAnimatorBridge>();
        bridge.TriggerAttack();
        CombatHitPause2D.Apply(actor, 0.08f);
        enemy.RequestDeath();
        Assert.That(enemy.IsDead, Is.True);
        float deadline = Time.time + 2f;
        while (actor != null && Time.time < deadline) yield return null;
        Assert.That(actor == null, Is.True, "Death should finish its 0.67s clip, not wait for the 5s fail-safe.");
    }

    [TestCase(AttackTelegraphShape.Rectangle, false)]
    [TestCase(AttackTelegraphShape.Rectangle, true)]
    [TestCase(AttackTelegraphShape.Circle, false)]
    [TestCase(AttackTelegraphShape.Circle, true)]
    public void ThinTelegraph_PreservesMeshStyleAndFullGeometry_AcrossWallsAndUpdates(AttackTelegraphShape shape, bool requestClipping)
    {
        Type viewType = Type.GetType("UnityGAS.AttackTelegraphView, Presentation", true);
        var actor = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphPath)));
        Component view = actor.GetComponent(viewType);
        var style = Own(ScriptableObject.CreateInstance<AttackTelegraphStyle>());
        style.scaleFillWithProgress = true;
        style.fillScaleStart = 0.1f;
        style.fillScaleEnd = 1f;
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkFrequency = 0f;
        style.fillColorEnd = new Color(0.8f, 0.2f, 0.1f, 0.4f);
        var spec = shape == AttackTelegraphShape.Rectangle
            ? AttackTelegraphSpec.CreateRectangle(Vector3.right * 2f, new Vector2(4f, 2f), 0f, 2f, style)
            : AttackTelegraphSpec.CreateCircle(Vector3.zero, 4f, 2f, style);
        if (requestClipping) spec = spec.WithWallClipping(1, 48);
        var show = viewType.GetMethod("Show", new[] { typeof(AttackTelegraphSpec), typeof(AttackTelegraphStyle) });
        show.Invoke(view, new object[] { spec, null });
        var meshFilter = actor.GetComponent<MeshFilter>();
        Assert.That(meshFilter, Is.Not.Null, "Thin mesh rendering must not fall back to the thick sprite border.");
        Component meshView = actor.GetComponent(Type.GetType("UnityGAS.AttackTelegraphWallClippedMeshView, Presentation", true));
        var applyStyle = meshView.GetType().GetMethod("ApplyStyle");
        float startWidth = meshFilter.sharedMesh.bounds.size.x;
        applyStyle.Invoke(meshView, new object[] { style, 1f });
        var baseline = meshFilter.sharedMesh.vertices;
        Assert.That(meshFilter.sharedMesh.bounds.size.x, Is.GreaterThan(startWidth));
        Assert.That(actor.GetComponent<LineRenderer>().widthMultiplier, Is.EqualTo(0.045f).Within(0.0001f));
        Color actualColor = actor.GetComponent<MeshRenderer>().sharedMaterial.color;
        Assert.That(Vector4.Distance(actualColor, style.fillColorEnd), Is.LessThan(0.0001f));
        Assert.That((bool)viewType.GetField("activeUseWallClipping", PrivateInstance).GetValue(view), Is.False);

        var wall = Own(new GameObject("TelegraphWall", typeof(BoxCollider2D)));
        wall.transform.position = Vector3.right;
        wall.GetComponent<BoxCollider2D>().size = new Vector2(0.5f, 10f);
        Physics2D.SyncTransforms();
        show.Invoke(view, new object[] { spec, null });
        applyStyle.Invoke(meshView, new object[] { style, 1f });
        CollectionAssert.AreEqual(baseline, meshFilter.sharedMesh.vertices);
        // Explicit and inherited geometry updates must keep the same no-terrain policy.
        viewType.GetMethod("UpdateGeometry").Invoke(view, new object[] { spec.WithWallClipping(1, 48) });
        applyStyle.Invoke(meshView, new object[] { style, 1f });
        CollectionAssert.AreEqual(baseline, meshFilter.sharedMesh.vertices);
        spec.useWallClipping = false;
        spec.useMeshOutline = false;
        viewType.GetMethod("UpdateGeometry").Invoke(view, new object[] { spec });
        applyStyle.Invoke(meshView, new object[] { style, 1f });
        CollectionAssert.AreEqual(baseline, meshFilter.sharedMesh.vertices);
    }
}
#endif
