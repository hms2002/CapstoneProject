// Run in an isolated Unity Editor project with current Gameplay/Core DLLs.
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class WeaponSwapPoseRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Folder = "Assets/WeaponSwapPoseProbe";

    public static void Run()
    {
        try
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "WeaponSwapPoseProbe");
            var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/Weapon.controller");
            var idle = new AnimationClip { name = "EmptyIdle" };
            AssetDatabase.CreateAsset(idle, Folder + "/Idle.anim");
            controller.AddMotion(idle);
            var swing = new AnimationClip { name = "Swing" };
            swing.SetCurve(WeaponVisualRig2D.MotionRootPath, typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0, 0, 1, 4));
            AssetDatabase.CreateAsset(swing, Folder + "/Swing.anim");
            controller.AddMotion(swing);
            var a = CreatePrefab("A", controller, new Vector3(.3f, -.2f, 0), 25f, new Vector3(1.2f, .8f, 1));
            var b = CreatePrefab("B", controller, new Vector3(-.5f, .7f, 0), -40f, new Vector3(.7f, 1.4f, 1));
            var owner = new GameObject("EquipOwner");
            var equip = owner.AddComponent<WeaponEquipController>();
            GameObject Current() => (GameObject)typeof(WeaponEquipController).GetField("currentWeaponGO", Private).GetValue(equip);
            equip.Equip(a);
            var cachedA = Current();
            for (int i = 0; i < 3; i++)
            {
                swing.SampleAnimation(Current(), .2f + i * .3f);
                var motion = Current().GetComponent<WeaponVisualRig2D>().MotionRoot;
                motion.localRotation = Quaternion.Euler(0, 0, 137);
                motion.localScale = Vector3.one * 3;
                equip.Equip(b);
                CheckPose(Current(), new Vector3(-.5f, .7f, 0), -40, new Vector3(.7f, 1.4f, 1));
                CheckPose(cachedA, new Vector3(.3f, -.2f, 0), 25, new Vector3(1.2f, .8f, 1));
                swing.SampleAnimation(Current(), .5f);
                equip.Equip(a);
                if (Current() != cachedA) throw new Exception("Expected cached instance reuse");
                CheckPose(Current(), new Vector3(.3f, -.2f, 0), 25, new Vector3(1.2f, .8f, 1));
            }
            swing.SampleAnimation(Current(), .6f);
            equip.Equip(a); // Same-prefab activation must also restore before Rebind.
            CheckPose(Current(), new Vector3(.3f, -.2f, 0), 25, new Vector3(1.2f, .8f, 1));
            equip.Clear();
            CheckPose(cachedA, new Vector3(.3f, -.2f, 0), 25, new Vector3(1.2f, .8f, 1));
            UnityEngine.Object.DestroyImmediate(owner);
            Debug.Log("WEAPON_SWAP_POSE_PASS: animated pose, repeated cached swaps, nonzero authored offsets/rotation/scale, same-prefab reactivation and clear.");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    private static GameObject CreatePrefab(string name, RuntimeAnimatorController controller, Vector3 position, float angle, Vector3 scale)
    {
        var root = new GameObject(name);
        root.SetActive(false);
        Transform Child(string childName, Transform parent)
        {
            var t = new GameObject(childName).transform;
            t.SetParent(parent, false);
            return t;
        }
        var visual = Child("WeaponVisualRoot", root.transform);
        var mirror = Child("MirrorRoot", visual);
        var motion = Child("MotionRoot", mirror);
        Child("RenderRoot", motion);
        motion.localPosition = position;
        motion.localRotation = Quaternion.Euler(0, 0, angle);
        motion.localScale = scale;
        root.AddComponent<WeaponVisualRig2D>();
        root.AddComponent<Animator>().runtimeAnimatorController = controller;
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static void CheckPose(GameObject weapon, Vector3 position, float angle, Vector3 scale)
    {
        var t = weapon.GetComponent<WeaponVisualRig2D>().MotionRoot;
        if (Vector3.Distance(t.localPosition, position) > .0001f ||
            Quaternion.Angle(t.localRotation, Quaternion.Euler(0, 0, angle)) > .01f ||
            Vector3.Distance(t.localScale, scale) > .0001f)
            throw new Exception("Pose mismatch: " + weapon.name + " position=" + t.localPosition + " rotation=" + t.localEulerAngles + " scale=" + t.localScale);
    }
}
