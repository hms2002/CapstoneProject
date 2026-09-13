#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Verifies the boss candle's authored light binding and reversible seal/relight lifecycle.
/// Owns and destroys only the candle instances created by these tests.
/// </summary>
public sealed class CandlestickSealLightPlayModeTests
{
    private const string CandlePath = "Assets/_Project/Prefabs/Monsters/ShadowCorridor/StrangeCandlestick/Candlestick.prefab";
    private const string WitchPath = "Assets/_Project/Prefabs/Bosses/ShadowBoss/Witch.prefab";
    private readonly List<GameObject> candles = new();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject candle in candles)
            if (candle != null)
                Object.DestroyImmediate(candle);
        candles.Clear();
    }

    [Test]
    public void BossCandle_BindsTheActiveVisionMask_NotTheDisabledLegacyMask()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandlePath);
        var witch = AssetDatabase.LoadAssetAtPath<GameObject>(WitchPath);
        Assert.IsNotNull(prefab);
        Assert.IsNotNull(witch);
        var witchData = new SerializedObject(witch.GetComponent<Witch>());
        Assert.AreSame(prefab, witchData.FindProperty("candlestickPrefab").objectReferenceValue);

        var sealData = new SerializedObject(prefab.GetComponent<CandlestickSeal>());
        var mask = sealData.FindProperty("sightMask").objectReferenceValue as SpriteMask;
        Assert.IsNotNull(mask);
        Assert.AreEqual("PlayerVisionMask(Clone)", mask.name);
        Assert.IsTrue(mask.gameObject.activeSelf);
        Assert.IsFalse(prefab.transform.Find("SightMask").gameObject.activeSelf);
    }

    [UnityTest]
    public IEnumerator SealAndThreeHits_ToggleTheSameLightObjects_WithoutDestroyingThem()
    {
        GameObject candle = CreateCandle();
        var seal = candle.GetComponent<CandlestickSeal>();
        GameObject visibleMask = candle.transform.Find("PlayerVisionMask(Clone)").gameObject;
        GameObject legacyMask = candle.transform.Find("SightMask").gameObject;
        GameObject lightZone = candle.GetComponentInChildren<CandlestickLightZone>(true).gameObject;
        int maskId = visibleMask.GetInstanceID();
        yield return null;

        for (int cycle = 0; cycle < 2; cycle++)
        {
            Assert.IsTrue(candle.GetComponent<Candlestick>().Seal());
            yield return null;
            Assert.IsTrue(candle.activeInHierarchy, "The candle must remain available for unsealing hits.");
            Assert.AreEqual(3, seal.CurrentHitsLeft);
            AssertAllMasksInactive(candle);
            Assert.IsFalse(lightZone.activeInHierarchy);

            for (int hit = 0; hit < 2; hit++)
            {
                Assert.IsTrue(seal.UseHit());
                Assert.IsTrue(seal.IsSealed);
                AssertAllMasksInactive(candle);
            }

            Assert.IsTrue(seal.UseHit());
            yield return null;
            Assert.IsFalse(seal.IsSealed);
            Assert.IsTrue(visibleMask.activeInHierarchy);
            Assert.AreEqual(maskId, visibleMask.GetInstanceID());
            Assert.IsTrue(lightZone.activeInHierarchy);
            Assert.IsFalse(legacyMask.activeSelf, "Unsealing must not revive the obsolete mask.");
            Assert.AreEqual(2, candle.GetComponentsInChildren<SpriteMask>(true).Length);
        }
    }

    [UnityTest]
    public IEnumerator SealedCandle_StaysDarkAfterDisableAndReenable()
    {
        GameObject candle = CreateCandle();
        candle.GetComponent<Candlestick>().Seal();
        candle.SetActive(false);
        yield return null;
        candle.SetActive(true);
        yield return null;
        Assert.IsTrue(candle.GetComponent<CandlestickSeal>().IsSealed);
        AssertAllMasksInactive(candle);
        Assert.IsFalse(candle.GetComponentInChildren<CandlestickLightZone>(true).gameObject.activeInHierarchy);
    }

    private GameObject CreateCandle()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandlePath);
        Assert.IsNotNull(prefab);
        GameObject candle = Object.Instantiate(prefab);
        candles.Add(candle);
        return candle;
    }

    private static void AssertAllMasksInactive(GameObject candle)
    {
        foreach (SpriteMask mask in candle.GetComponentsInChildren<SpriteMask>(true))
            Assert.IsFalse(mask.gameObject.activeInHierarchy, mask.name + " remained active while sealed.");
    }
}
#endif
