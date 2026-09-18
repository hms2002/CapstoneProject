using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class PrototypeTutorialWorldGuideRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    public static void Run()
    {
        try
        {
            var owner = new GameObject("Tutorial owner"); owner.SetActive(false);
            var tutorial = owner.AddComponent<PrototypeTutorialUpgrade>();
            Set(tutorial, "skillTargets", Array.Empty<Transform>());
            Set(tutorial, "gates", Array.Empty<GameObject>());
            owner.SetActive(true);
            // The production stage gate checks an enabled scene owner.
            Check(tutorial.isActiveAndEnabled, "Tutorial fixture disabled");
            var host = new GameObject("Guide owner"); host.SetActive(false);
            var guide = host.AddComponent<PrototypeTutorialEntranceGuide>();
            var content = new GameObject("Authored content"); content.transform.SetParent(host.transform);
            var sprite = content.AddComponent<SpriteRenderer>();
            Set(guide, "tutorial", tutorial); Set(guide, "visibleStage", 3);
            Set(guide, "presentationRoot", content.transform); Set(guide, "animate", true);
            Call(guide, "Awake");
            Set(tutorial, "stage", 2); Call(guide, "TickPresentation", .2f);
            Check(!content.activeSelf, "Skill guide visible during basic attack");
            Set(tutorial, "stage", 3); Call(guide, "TickPresentation", 1f);
            Check(content.activeSelf && sprite.color.a == 0f && Mathf.Approximately(content.transform.localPosition.y, -.45f),
                "A long transition frame must not consume the entrance animation");
            Call(guide, "TickPresentation", .06f);
            Check(content.activeSelf && sprite.color.a > 0f && sprite.color.a < 1f && content.transform.localPosition.y < 0f,
                "Skill entrance must fade and slide upward");
            Call(guide, "TickPresentation", .25f);
            Check(Mathf.Approximately(sprite.color.a, 1f) && content.transform.localPosition == Vector3.zero, "Entrance did not settle");
            Set(tutorial, "stage", 4); Call(guide, "TickPresentation", .04f);
            Call(guide, "TickPresentation", .05f);
            Check(content.activeSelf && sprite.color.a < 1f, "Stage exit must animate");
            Call(guide, "TickPresentation", .15f);
            Check(!content.activeSelf, "Skill guide remained after its stage");
            Call(guide, "OnDisable");

            var chestObject = new GameObject("Chest"); chestObject.SetActive(false);
            var chest = chestObject.AddComponent<ChestInteractable>();
            var chestSprite = chestObject.AddComponent<SpriteRenderer>();
            Set(chest, "spriteRenderer", chestSprite); Call(chest, "Awake");
            chestObject.SetActive(true);
            var navObject = new GameObject("Chest navigation"); navObject.SetActive(false);
            var nav = navObject.AddComponent<PrototypeChestNavigation>();
            var arrow = new GameObject("Authored arrow");
            var shield = new GameObject("Shield", typeof(RectTransform));
            Set(nav, "worldChest", chest); Set(nav, "worldArrow", arrow.transform);
            Set(nav, "tutorial", tutorial); Set(nav, "inputShield", shield.transform);
            Call(nav, "OnEnable");
            Call(nav, "LateUpdate");
            var block = new MaterialPropertyBlock(); chestSprite.GetPropertyBlock(block);
            Check(arrow.activeSelf && block.GetFloat("_OutlineEnabled") == 1f && arrow.transform.position.y >= 1.2f,
                "Chest stage must show outline and arrow");
            Set(tutorial, "stage", 5); Call(nav, "LateUpdate");
            chestSprite.GetPropertyBlock(block);
            Check(!arrow.activeSelf && block.GetFloat("_OutlineEnabled") == 0f, "Chest highlight leaked into portal stage");
            Set(tutorial, "stage", 4); Call(nav, "LateUpdate"); Call(nav, "OnDisable");
            chestSprite.GetPropertyBlock(block);
            Check(!arrow.activeSelf && block.GetFloat("_OutlineEnabled") == 0f, "Disable must release owned guidance");
            Debug.Log("TUTORIAL_WORLD_GUIDES_PASS: stage gating, fade/slide entrance and exit, chest outline/arrow and cleanup.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
