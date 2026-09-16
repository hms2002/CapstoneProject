// GPU probe: run in an isolated project with current Gameplay/Core DLLs and URP 17.4.
// Copy the referenced project settings, sprites and three current vision prefabs.
// Assets/Before contains their pre-fix copies (from HEAD); keep their original mask flags.
// Run -batchmode -executeMethod ApprenticeChargeRenderProbe.Run WITHOUT -nographics.
// Main-project assets and open Editor state are never edited by this probe.
using System;
using System.Collections;
using System.Reflection;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityGAS;
[InitializeOnLoad]
public static class ApprenticeChargeRenderProbe
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    const string Pending = "ChargeRenderProbe";
    static ApprenticeChargeRenderProbe() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void Run()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/_Project/Settings/UniversalRP.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        new GameObject("ProbeRunner").AddComponent<AbilitySystem>().StartCoroutine(Guard(Exercise()));
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        while (true)
        {
            object current;
            try { if (!routine.MoveNext()) break; current = routine.Current; }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); yield break; }
            yield return current;
        }
        EditorApplication.Exit(0);
    }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, Private).SetValue(o,v); }
    static IEnumerator Exercise()
    {
        Directory.CreateDirectory("Results");
        Debug.Log("GPU=" + SystemInfo.graphicsDeviceName + " API=" + SystemInfo.graphicsDeviceType);
        var data = ScriptableObject.CreateInstance<ApprenticeHeroSwordChargeSpinData>();
        Sprite charge = null, full = null;
        foreach(var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Sprites/Items/Weapon/BasicWeapon/BasicWeaponSkill1ChargeAura.png"))
            if (asset is Sprite sprite) { Debug.Log("SPRITE="+sprite.name); if(sprite.name.EndsWith("_0")) charge=sprite; else if(sprite.name.EndsWith("_1")) full=sprite; }
        if(charge==null) throw new Exception("Missing authored charge sprite");
        Set(data,"chargeRevealSprite",charge); Set(data,"fullChargeRevealSprite",full ?? charge);
        Set(data,"chargeRevealColor",Color.white); Set(data,"fullChargeRevealColor",Color.white);
        Set(data,"chargeRevealLocalPosition",new Vector3(-.5f,.5f,0));
        Set(data,"chargeRevealLocalScale",Vector3.one*1.2f);
        var source = new GameObject("Weapon",typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        source.sprite=charge; source.sortingLayerName="Entity"; source.enabled=false;
        source.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        var type=typeof(AbilityLogic_ApprenticeHeroSwordChargeSpin).GetNestedType("ApprenticeHeroSwordChargePresentationRuntime",BindingFlags.NonPublic);
        object[] args={source,data,charge,null,null,null};
        type.GetMethod("CreateReveal",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
        var reveal=(GameObject)args[3];
        var runtime=type.GetConstructors(Private)[0].Invoke(new object[]{data,null,reveal,args[4],args[5],charge,null});
        var vision=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Map/Gimmicks/Witch/PlayerVisionMask.prefab"));
        vision.transform.position=Vector3.zero;
        var cam=new GameObject("Capture",typeof(Camera)).GetComponent<Camera>();
        cam.transform.position=new Vector3(0,0,-10); cam.orthographic=true; cam.orthographicSize=3;
        cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=Color.clear;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        var rt=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32);rt.Create();cam.targetTexture=rt;
        var texture=new Texture2D(256,256,TextureFormat.RGBA32,false);
        var before = new GameObject("UnboundedBefore");
        var after = new GameObject("ScopedAfter");
        foreach(var path in new[]{
            "Assets/_Project/Prefabs/Monsters/ShadowCorridor/StrangeCandlestick/Candlestick.prefab",
            "Assets/_Project/Prefabs/Monsters/ShadowCorridor/StrangeCandlestick/LightBead.prefab",
            "Assets/_Project/Prefabs/Monsters/ShadowCorridor/Dead'sSkeleton.prefab"})
        {
            CloneMasks("Assets/Before/"+Path.GetFileName(path), before.transform);
            CloneMasks(path, after.transform);
        }
        foreach(var layer in new[]{"Entity","WeaponChargeEffect"})
        {
            reveal.GetComponent<SortingGroup>().sortingLayerName=layer;
            foreach(var pose in new[]{new Vector2(0,1),new Vector2(90,1),new Vector2(135,-1)})
            {
                source.transform.rotation=Quaternion.Euler(0,0,pose.x);
                source.transform.localScale=new Vector3(1,pose.y,1);
                foreach(float ratio in new[]{.25f,.65f,1f})
                {
                    type.GetMethod("Update").Invoke(runtime,new object[]{ratio});
                    int baseline=-1;
                    foreach(var mode in new[]{"None","Player","Unbounded","Fixed"})
                    {
                        vision.SetActive(mode!="None"); before.SetActive(mode=="Unbounded"); after.SetActive(mode=="Fixed");
                        yield return null; yield return null; yield return null;
                        RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,256,256),0,0);texture.Apply();RenderTexture.active=null;
                        int count=0;foreach(var p in texture.GetPixels32()) if(p.a>32) count++;
                        string id=layer+"_"+pose.x+"_"+pose.y+"_"+ratio.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+"_"+mode;
                        Debug.Log("CHARGE_PIXELS "+id+"="+count);
                        if(mode=="None") { baseline=count; if(ratio > .5f && count==0) throw new Exception("Charge failed to render"); }
                        if(layer=="WeaponChargeEffect" && (mode=="Player"||mode=="Fixed") && count!=baseline)
                            throw new Exception("External mask changed charge coverage: "+id+" baseline="+baseline);
                        if(pose.x==0) File.WriteAllBytes("Results/"+id+".png",texture.EncodeToPNG());
                    }
                }
            }
        }
        reveal.SetActive(false); vision.SetActive(false);
        // Preserve the intended world effect of all four formerly unbounded masks.
        var solidTex=new Texture2D(2,2);solidTex.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white});solidTex.Apply();
        var solid=Sprite.Create(solidTex,new Rect(0,0,2,2),Vector2.one*.5f,2);
        source.transform.rotation=Quaternion.identity;source.transform.localScale=Vector3.one*6;
        source.sprite=solid;source.maskInteraction=SpriteMaskInteraction.VisibleInsideMask;source.enabled=true;
        var dark=new GameObject("Dark",typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        dark.sprite=solid;dark.sharedMaterial=source.sharedMaterial;dark.transform.localScale=Vector3.one*6;
        dark.sortingLayerName="MaskRender";dark.color=new Color(0,0,0,.8f);dark.maskInteraction=SpriteMaskInteraction.VisibleOutsideMask;
        foreach(var m in before.GetComponentsInChildren<SpriteMask>(true)) m.transform.localScale=Vector3.one*2;
        foreach(var m in after.GetComponentsInChildren<SpriteMask>(true)) m.transform.localScale=Vector3.one*2;
        Color32[] beforePixels=null;
        foreach(bool fixedMasks in new[]{false,true})
        {
            before.SetActive(!fixedMasks);after.SetActive(fixedMasks);
            yield return null;yield return null;yield return null;
            RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,256,256),0,0);texture.Apply();RenderTexture.active=null;
            var pixels=texture.GetPixels32();
            if(!fixedMasks) beforePixels=pixels;
            else { for(int i=0;i<pixels.Length;i++) if(!pixels[i].Equals(beforePixels[i])) throw new Exception("World light/entity coverage changed at pixel "+i); }
            File.WriteAllBytes("Results/world_"+fixedMasks+".png",texture.EncodeToPNG());
        }
        CheckSkillAim(typeof(AbilityLogic_ApprenticeHeroSwordChargeSpin));
        CheckSkillAim(typeof(AbilityLogic_ApprenticeHeroSwordDashStab));
        Debug.Log("CHARGE_RENDER_AND_AIM_PASS: progressive mask coverage, rotations/mirroring, world light/entity parity, skill aim and cancellation.");
        Debug.Log("CHARGE_RENDER_PROBE_COMPLETE");
    }
    static void CloneMasks(string path, Transform parent)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(asset==null) throw new Exception("Missing "+path);
        foreach(var m in asset.GetComponentsInChildren<SpriteMask>(true))
        {
            var mask=new GameObject(m.name,typeof(SpriteMask)).GetComponent<SpriteMask>();
            mask.transform.SetParent(parent,false);mask.transform.localScale=Vector3.one*20;
            mask.sprite=m.sprite;mask.alphaCutoff=m.alphaCutoff;mask.renderingLayerMask=m.renderingLayerMask;
            mask.isCustomRangeActive=m.isCustomRangeActive;
            mask.frontSortingLayerID=m.frontSortingLayerID;mask.backSortingLayerID=m.backSortingLayerID;
            mask.frontSortingOrder=m.frontSortingOrder;mask.backSortingOrder=m.backSortingOrder;
        }
    }
    static void CheckSkillAim(Type logicType)
    {
        var owner=new GameObject("AimOwner",typeof(AbilitySystem),typeof(PlayerAim2D));
        var system=owner.GetComponent<AbilitySystem>();var aim=owner.GetComponent<PlayerAim2D>();aim.enabled=false;
        var rigGo=new GameObject("Rig");rigGo.transform.SetParent(owner.transform,false);
        var aimRoot=new GameObject("AimRoot").transform;aimRoot.SetParent(rigGo.transform,false);
        var rig=rigGo.AddComponent<WeaponPresentationRig2D>();Set(rig,"aimRoot",aimRoot);Set(rig,"sideOffsetRoot",rigGo.transform);
        aim.SetAimDirectionForPresentation(Vector2.one);rig.RefreshNow();
        var logic=(AbilityLogic)ScriptableObject.CreateInstance(logicType);
        var spec=new AbilitySpec(ScriptableObject.CreateInstance<AbilityDefinition>());
        logicType.GetMethod("BeginSkillAimLock",Private).Invoke(logic,new object[]{system,spec,aim.AimDirection,10f});
        aim.SetAimDirectionForPresentation(Vector2.left);rig.RefreshNow();
        if(Mathf.Abs(Mathf.DeltaAngle(aimRoot.eulerAngles.z,45))>.01f||rig.CurrentSideSign!=1) throw new Exception("Aim not locked "+logicType.Name);
        logic.CleanupForSceneTransition(system,spec,null);rig.RefreshNow();
        if(Mathf.Abs(Mathf.DeltaAngle(aimRoot.eulerAngles.z,180))>.01f||rig.CurrentSideSign!=-1) throw new Exception("Aim cleanup failed "+logicType.Name);
        int oldToken=rig.BeginAimPresentationOverride(WeaponAimPresentationMode.LockedAtCast,Vector2.up,10f);
        rig.BeginAimPresentationOverride(WeaponAimPresentationMode.LockedAtCast,Vector2.down,10f);
        rig.CancelAimPresentationOverride(oldToken);rig.RefreshNow();
        if(Mathf.Abs(Mathf.DeltaAngle(aimRoot.eulerAngles.z,270))>.01f) throw new Exception("Stale token cleared current skill");
        UnityEngine.Object.DestroyImmediate(owner);
        Debug.Log("AIM_LOCK_PASS "+logicType.Name);
    }
}
