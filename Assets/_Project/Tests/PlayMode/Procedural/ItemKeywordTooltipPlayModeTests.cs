#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class ItemKeywordTooltipPlayModeTests
{
    const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Type ViewType = Type.GetType("ItemKeywordTooltipView, UI", true);
    static readonly Type PanelType = Type.GetType("ItemDetailPanel, UI", true);
    static readonly Type RootType = Type.GetType("GlobalUIRoot, UI", true);
    static readonly Type TextType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro", true);
    static readonly Type UiBehaviour = Type.GetType("UnityEngine.EventSystems.UIBehaviour, UnityEngine.UI", true);
    static readonly Type Localization = Type.GetType("UnityEngine.Localization.Settings.LocalizationSettings, Unity.Localization", true);
    static readonly Type LocaleType = Type.GetType("UnityEngine.Localization.Locale, Unity.Localization", true);
    static readonly Type AppSettings = Type.GetType("GameSettingsService, UI", true);
    object previousLocale;
    bool restoreLocale;

    [UnityTearDown]
    public IEnumerator RestoreLocale()
    {
        if (!restoreLocale) yield break;
        yield return WaitForLocalization();
        Localization.GetProperty("SelectedLocale").SetValue(null, previousLocale);
        yield return WaitForLocalization();
        restoreLocale = false;
    }

    [Test]
    public void VisibleDescriptions_SelectZeroOneOrBothCardsWithoutDuplicatesOrStaleSources()
    {
        using var fixture = new AuthoredView();
        fixture.SetSource("Ordinary item");
        fixture.Refresh();
        fixture.AssertCards(false, false);
        fixture.SetSource("전기 피해 전기 축적");
        fixture.Refresh();
        fixture.AssertCards(false, false);
        fixture.SetSource("감전");
        fixture.Refresh();
        fixture.AssertCards(true, false);
        fixture.SetSource("<link=\"glossary:화상 피해\">Burn damage</link>");
        fixture.Refresh();
        fixture.AssertCards(false, true);
        fixture.SetSource("감전 감전 화상 화상");
        fixture.Refresh();
        fixture.AssertCards(true, true);
        Assert.That(fixture.Stack.childCount, Is.EqualTo(2), "Repeated terms must use the same authored cards.");

        Component hidden = fixture.CloneSource();
        SetText(hidden, "감전 화상");
        hidden.gameObject.SetActive(false);
        SetText(fixture.Body("electrocutedBody"), "감전 화상");
        SetText(fixture.Body("burnBody"), "감전 화상");
        fixture.SetSource("Ordinary item");
        fixture.Refresh();
        fixture.AssertCards(false, false);
    }

    [UnityTest]
    public IEnumerator NativeTextChanges_RefreshAutomaticallyAndDisabledComponentStaysCleared()
    {
        using var fixture = new AuthoredView();
        fixture.SetSource("Ordinary item");
        fixture.Refresh();
        fixture.SetSource("<link=\"glossary:전기 속성\">Electric element</link>");
        ForceMesh(fixture.Source);
        yield return null;
        yield return null;
        fixture.AssertCards(true, false);

        ((Behaviour)fixture.View).enabled = false;
        fixture.AssertCards(false, false);
        fixture.SetSource("화상");
        ForceMesh(fixture.Source);
        yield return null;
        yield return null;
        fixture.AssertCards(false, false);
        ((Behaviour)fixture.View).enabled = true;
        yield return null;
        yield return null;
        fixture.AssertCards(false, true);
    }

    [Test]
    public void AuthoredStack_ExtendsHoverBoundsToTheRightWithoutResizingThePrimaryPanel()
    {
        using var fixture = new AuthoredView();
        fixture.SetSource("Ordinary item");
        fixture.Refresh();
        Rect primary = fixture.Owner.rect;
        Assert.That(fixture.Bounds(), Is.EqualTo(primary));
        fixture.SetSource("감전 화상");
        fixture.Refresh();
        Assert.That(fixture.Owner.rect, Is.EqualTo(primary));
        Rect combined = fixture.Bounds();
        Assert.That(combined.xMax, Is.GreaterThan(primary.xMax));
        var corners = new Vector3[4];
        fixture.Stack.GetWorldCorners(corners);
        foreach (Vector3 corner in corners)
        {
            Vector3 local = fixture.Owner.InverseTransformPoint(corner);
            Assert.That(local.x, Is.InRange(combined.xMin - .01f, combined.xMax + .01f));
            Assert.That(local.y, Is.InRange(combined.yMin - .01f, combined.yMax + .01f));
        }
        Invoke(fixture.View, "Clear");
        Assert.That(fixture.Bounds(), Is.EqualTo(primary));
    }

    [UnityTest]
    public IEnumerator FiveLocales_KeepStableGlossaryIdentityAndRenderTranslatedDescriptions()
    {
        for (int frame = 0; frame < 180; frame++)
        {
            object settings = AppSettings.GetProperty("Instance").GetValue(null);
            if (settings == null || ((bool)Field(settings, "initialized") && Field(settings, "localeInitialization") == null))
                break;
            if (frame == 179) Assert.Fail("Saved-locale bootstrap did not finish.");
            yield return null;
        }
        yield return WaitForLocalization();
        previousLocale = Localization.GetProperty("SelectedLocale").GetValue(null);
        restoreLocale = true;
        using var fixture = new AuthoredView();
        string[] localeCodes = { "ko", "en", "ja", "zh-Hans", "zh-Hant" };
        string[] baseChainPhrases = { "기본 최대 3명", "base maximum of 3 targets", "基本で最大3体", "基础最多命中3个目标", "基礎最多命中3個目標" };
        string[] shockDurationPhrases = { "4초", "4 seconds", "4秒", "4秒", "4秒" };
        string[] gaugeTerms = { "게이지", "gauge", "ゲージ", "积累槽", "累積槽" };
        string[] healthCapClauses = { "칸까지 적용", "Counts up to", "まで計算", "最多计算", "最多計算" };
        string[] enemyCapClauses = { "명까지 적용", "Counts up to", "体まで", "最多计算", "最多計算" };
        string[] movementSkillPhrases = { "이동 스킬", "movement skill", "移動スキル", "移动技能", "移動技能" };
        string[] allStackPhrases = { "모든 중첩", "all stack", "すべての重複", "所有层", "所有層" };
        string[] forbiddenGameTerms = { "현재 씬|발현", @"(?i)\bscene\b|\bactivation\b", "シーン|発動", "场景|触发", "場景|觸發" };
        string[] currentElectricTemplates = { "scene", "health", "dash", "extension", "targets", "paralysis", "expiry", "echo", "conductor" };
        foreach (string code in localeCodes)
        {
            int localeIndex = Array.IndexOf(localeCodes, code);
            Object locale = AssetDatabase.LoadAssetAtPath("Assets/_Project/Data/Localization/Locale_" + code + ".asset", LocaleType);
            Assert.That(locale, Is.Not.Null, code);
            Localization.GetProperty("SelectedLocale").SetValue(null, locale);
            yield return WaitForLocalization();
            fixture.SetSource($"<link=\"glossary:전기 속성\">{Text("term.감전")}</link> " +
                $"<link=\"glossary:화상\">{Text("term.화상")}</link>");
            ForceMesh(fixture.Source);
            yield return null;
            yield return null;
            fixture.AssertCards(true, true);
            foreach (string status in new[] { "electrocuted", "burn" })
            {
                string expected = Text("ui.item_keyword." + status + ".description");
                Assert.That(expected, Does.Not.StartWith("MISSING:"), code + " translation must exist.");
                Assert.That(ReadText(fixture.Body(status + "Body")), Is.EqualTo(expected), code + "/" + status);
                if (status == "electrocuted")
                {
                    Assert.That(expected, Does.Contain(baseChainPhrases[localeIndex]), code + " must explain the base three-target count.");
                    Assert.That(expected, Does.Contain(shockDurationPhrases[localeIndex]), code + " must explain four-second electrocution.");
                    Assert.That(expected, Does.Not.Match(@"[()（）]"), code + " must omit the parenthetical clause.");
                    Assert.That(expected, Does.Not.Contain(gaugeTerms[localeIndex]), code + " must omit gauge details.");
                    Assert.That(expected, Does.Not.Match(@"[+＋%％]"), code + " must omit damage formulas and coefficients.");
                    Assert.That(expected, Does.Not.Match(forbiddenGameTerms[localeIndex]), code + "/status explanation uses game terms");
                }
            }
            foreach (string effect in currentElectricTemplates)
            {
                string template = Text("relic.electric." + effect + ".template");
                Assert.That(template, Does.Not.StartWith("MISSING:"), code + "/electric effect template translation");
                Assert.That(template, Does.Not.Match(forbiddenGameTerms[localeIndex]), code + "/electric template uses game terms");
            }
            string[][] electricNames =
            {
                new[] { "천둥의 심장", "생명의 전류", "대시 코일", "뇌명의 매듭", "공명의 종", "저릿한 족쇄", "전류 항아리", "메아리 코일", "도체 고리" },
                new[] { "Thunder Heart", "Life Current", "Dash Coil", "Thunder Knot", "Resonance Bell", "Tingling Shackles", "Current Jar", "Echo Coil", "Conductor Ring" },
                new[] { "雷鳴の心臓", "生命の電流", "ダッシュコイル", "雷鳴の結び目", "共鳴の鐘", "痺れの足枷", "電流の壺", "エコーコイル", "導体の輪" },
                new[] { "雷鸣之心", "生命电流", "冲刺线圈", "雷鸣之结", "共鸣之钟", "麻痹镣铐", "电流罐", "回声线圈", "导体环" },
                new[] { "雷鳴之心", "生命電流", "衝刺線圈", "雷鳴之結", "共鳴之鐘", "麻痺鐐銬", "電流罐", "回聲線圈", "導體環" }
            };
            string[] electricFiles = { "ThunderHeart", "StormStep", "DashCoil", "ThunderKnot", "ResonanceBell", "TinglingShackles", "CurrentJar", "EchoCoil", "ConductorRing" };
            string[] maximumBonusTokens = { "+3", "+3", "+2", "1.5", "+3", "0.1", "40%", "50%", "20%" };
            for (int index = 0; index < electricFiles.Length; index++)
            {
                var relic = AssetDatabase.LoadAssetAtPath<RelicDefinition>(
                    "Assets/_Project/Data/Items/Relics/Definitions/RD_" + electricFiles[index] + ".asset");
                Assert.That(relic, Is.Not.Null);
                Assert.That(relic.DisplayName, Is.EqualTo(electricNames[localeIndex][index]), code + "/electric relic name");
                Assert.That(GameText.Asset(relic, "description", relic.description), Does.Not.Match(forbiddenGameTerms[localeIndex]),
                    code + "/electric relic description uses game terms");
                var tooltip = relic.logic.BuildTooltip(relic, relic.maxLevel, default).effectText;
                Assert.That(tooltip, Does.Not.Match(@"\{[a-z_]+\}"), code + "/tooltip must resolve named tokens");
                Assert.That(tooltip, Does.Not.Contain("MISSING:"), code + "/tooltip translation");
                Assert.That(tooltip, Does.Not.Match(forbiddenGameTerms[localeIndex]), code + "/electric tooltip uses game terms");
                Assert.That(tooltip, Does.Contain(maximumBonusTokens[index]), code + "/maximum-level bonus");
                if (index == 0)
                {
                    Assert.That(relic.maxLevel, Is.EqualTo(5), "Thunder Heart has five upgrade tiers.");
                    string template = Text("relic.electric.scene.template");
                    Assert.That(template, Does.Not.Contain("{cap}"), code + "/shocked enemy count has no cap token");
                    Assert.That(tooltip, Does.Not.Contain(enemyCapClauses[localeIndex]), code + "/shocked enemy count has no cap clause");
                    Assert.That(tooltip, Is.EqualTo(template.Replace("{value}", RelicTooltipFormatter.FormatSignedValueToken(3f, false))),
                        code + "/uncapped Thunder Heart tooltip");
                }
                if (index == 1)
                {
                    string template = Text("relic.electric.health.template");
                    Assert.That(template, Does.Not.Contain("{cap}"), code + "/health scaling has no cell cap token");
                    Assert.That(tooltip, Does.Not.Contain(healthCapClauses[localeIndex]), code + "/health scaling has no cell cap clause");
                    Assert.That(tooltip, Is.EqualTo(template
                        .Replace("{health_step}", RelicTooltipFormatter.FormatUnsignedValueToken(1f, false))
                        .Replace("{value}", RelicTooltipFormatter.FormatSignedValueToken(3f, false))), code + "/uncapped health tooltip");
                }
                if (index == 2)
                {
                    Assert.That(relic.maxLevel, Is.EqualTo(3));
                    Assert.That(tooltip, Is.EqualTo(Text("relic.electric.dash.template")
                        .Replace("{duration}", RelicTooltipFormatter.FormatSeconds(3f))
                        .Replace("{value}", RelicTooltipFormatter.FormatSignedValueToken(2f, false))), code + "/movement stack tooltip");
                    Assert.That(tooltip, Does.Contain(movementSkillPhrases[localeIndex]));
                    Assert.That(tooltip, Does.Contain(allStackPhrases[localeIndex]));
                    Assert.That(tooltip, Does.Contain("4"), "The movement bonus has four stacks.");
                }
                if (index == 4)
                    Assert.That(tooltip, Is.EqualTo(Text("relic.electric.targets.template")
                        .Replace("{value}", RelicTooltipFormatter.FormatSignedValueToken(3f, false))), code + "/target-count tooltip");
                if (index == 5)
                {
                    Assert.That(relic.maxLevel, Is.EqualTo(1), "Paralysis duration is fixed and has no upgrade levels.");
                    Assert.That(tooltip, Is.EqualTo(Text("relic.electric.paralysis.template")
                        .Replace("{duration}", RelicTooltipFormatter.FormatSeconds(0.1f))), code + "/paralysis tooltip");
                }
                if (index >= 6)
                {
                    Assert.That(relic.maxLevel, Is.EqualTo(1), "The new electric effects have fixed values and no upgrade levels.");
                    string expectedTooltip = index switch
                    {
                        6 => Text("relic.electric.expiry.template").Replace("{electric_ratio}", RelicTooltipFormatter.FormatUnsignedValueToken(0.4f, true)),
                        7 => Text("relic.electric.echo.template").Replace("{damage_ratio}", RelicTooltipFormatter.FormatUnsignedValueToken(0.5f, true)),
                        _ => Text("relic.electric.conductor.template")
                            .Replace("{step_bonus}", RelicTooltipFormatter.FormatUnsignedValueToken(0.2f, true))
                            .Replace("{max_bonus}", RelicTooltipFormatter.FormatUnsignedValueToken(0.6f, true))
                    };
                    Assert.That(tooltip, Is.EqualTo(expectedTooltip), code + "/new electric tooltip must match its translated template and values");
                    if (index == 8) Assert.That(tooltip, Does.Contain("60%"), code + "/conductor bonus cap");
                }
            }
            var currentShard = AssetDatabase.LoadAssetAtPath<RelicDefinition>("Assets/_Project/Data/Items/Relics/Definitions/RD_CurrentShard.asset");
            Assert.That(currentShard, Is.Not.Null);
            Assert.That(currentShard.maxLevel, Is.EqualTo(6), "Current Shard has six upgrade tiers.");
            Assert.That(GameText.Asset(currentShard, "description", currentShard.description), Does.Not.Match(forbiddenGameTerms[localeIndex]),
                code + "/existing Current Shard description uses game terms");
            string currentShardTooltip = currentShard.logic.BuildTooltip(currentShard, currentShard.maxLevel, default).effectText;
            Assert.That(currentShardTooltip, Does.Contain("+6"), code + "/existing Current Shard maximum-level bonus");
            Assert.That(currentShardTooltip, Does.Not.Match(forbiddenGameTerms[localeIndex]), code + "/existing Current Shard tooltip uses game terms");
            Assert.That(ReadText(fixture.Body("electrocutedTitle")), Is.EqualTo(Text("term.감전")));
            Assert.That(ReadText(fixture.Body("burnTitle")), Is.EqualTo(Text("term.화상")));

            fixture.SetSource(Text("term.전기 속성") + " " + Text("term.화상"));
            ForceMesh(fixture.Source);
            yield return null;
            yield return null;
            fixture.AssertCards(true, true);
            if (code == "en")
            {
                fixture.SetSource("Auburn Burning");
                ForceMesh(fixture.Source);
                yield return null;
                yield return null;
                fixture.AssertCards(false, false);
            }
        }
    }

    sealed class AuthoredView : IDisposable
    {
        readonly GameObject staging, canvas;
        readonly object previousRoot = RootType.GetProperty("Instance").GetValue(null);
        readonly object previousPanel = PanelType.GetProperty("Instance").GetValue(null);
        public readonly Component View, Source;
        public readonly RectTransform Owner, Stack;

        public AuthoredView()
        {
            staging = new GameObject("Inactive keyword test staging");
            staging.SetActive(false);
            canvas = new GameObject("Keyword test canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab");
                Component authored = asset.GetComponentInChildren(ViewType, true);
                Assert.That(authored, Is.Not.Null);
                GameObject clone = Object.Instantiate(authored.gameObject, staging.transform);
                View = clone.GetComponent(ViewType);
                Owner = (RectTransform)clone.transform;
                Stack = (RectTransform)Field(View, "stackRoot");
                Component panel = clone.GetComponent(PanelType);
                Assert.That(Field(panel, "keywordTooltips"), Is.SameAs(View), "Panel must reference its authored companion view.");
                Source = (Component)Field(panel, "titleText");
                foreach (string field in new[] { "stackRoot", "electrocutedCard", "burnCard", "electrocutedTitle", "electrocutedBody", "burnTitle", "burnBody" })
                {
                    Component reference = (Component)Field(View, field);
                    Assert.That(reference, Is.Not.Null, field);
                    Assert.That(reference.transform.IsChildOf(Owner), Is.True, field + " must remap into the clone.");
                    Assert.That(EditorUtility.IsPersistent(reference), Is.False, "Tests must never mutate prefab assets.");
                }
                foreach (MonoBehaviour behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour != View && !UiBehaviour.IsInstanceOfType(behaviour)) Object.DestroyImmediate(behaviour);
                foreach (Component text in clone.GetComponentsInChildren(TextType, true))
                    if (!text.transform.IsChildOf(Stack)) SetText(text, string.Empty);
                for (Transform current = Source.transform; current != Owner; current = current.parent) current.gameObject.SetActive(true);
                clone.SetActive(true);
                clone.transform.SetParent(canvas.transform, false);
            }
            catch { Dispose(); throw; }
        }

        public Component Body(string field) => (Component)Field(View, field);
        public void SetSource(string content) => SetText(Source, content);
        public void Refresh() => Invoke(View, "Refresh");
        public Rect Bounds() => (Rect)Invoke(View, "GetLocalBounds");
        public Component CloneSource() => Object.Instantiate(Source.gameObject, Owner).GetComponent(TextType);
        public void AssertCards(bool electric, bool burn)
        {
            Assert.That(((RectTransform)Field(View, "electrocutedCard")).gameObject.activeSelf, Is.EqualTo(electric));
            Assert.That(((RectTransform)Field(View, "burnCard")).gameObject.activeSelf, Is.EqualTo(burn));
            Assert.That(Stack.gameObject.activeSelf, Is.EqualTo(electric || burn));
        }
        public void Dispose()
        {
            Object.DestroyImmediate(canvas);
            Object.DestroyImmediate(staging);
            Assert.That(RootType.GetProperty("Instance").GetValue(null), Is.SameAs(previousRoot));
            Assert.That(PanelType.GetProperty("Instance").GetValue(null), Is.SameAs(previousPanel));
        }
    }

    static IEnumerator WaitForLocalization()
    {
        // Addressables completes asynchronously; headless frames are not a wall-clock timeout.
        double deadline = Time.realtimeSinceStartupAsDouble + 30d;
        while (Time.realtimeSinceStartupAsDouble < deadline)
        {
            object operation = Localization.GetProperty("InitializationOperation").GetValue(null);
            if ((bool)operation.GetType().GetProperty("IsDone").GetValue(operation))
            {
                Assert.That(operation.GetType().GetProperty("Status").GetValue(operation).ToString(), Is.EqualTo("Succeeded"));
                yield break;
            }
            yield return null;
        }
        Assert.Fail("Localization initialization did not finish.");
    }
    static object Field(object target, string name) => target.GetType().GetField(name, Members).GetValue(target);
    static object Invoke(Component target, string name) => target.GetType().GetMethod(name, Members).Invoke(target, null);
    static void SetText(Component text, string value) => TextType.GetProperty("text").SetValue(text, value);
    static string ReadText(Component text) => (string)TextType.GetProperty("text").GetValue(text);
    static void ForceMesh(Component text) => TextType.GetMethod("ForceMeshUpdate").Invoke(text, new object[] { false, false });
    static string Text(string key) => GameText.Get(key, "MISSING:" + key);
}

/// <summary>Opt-in native test dispatch for the live Editor; the marker is consumed once.</summary>
[InitializeOnLoad]
static class ItemKeywordTooltipLiveTestRunner
{
    const string Marker = "Temp/ItemKeywordTooltipTests.runonce";
    const string Output = "Library/CodexItemKeywords";
    const string ActiveKey = "Codex.ItemKeywordTests.Active";
    const string FailuresKey = "Codex.ItemKeywordTests.Failures";
    const string AnyRunKey = "Codex.ItemKeywordTests.AnyRun";

    static ItemKeywordTooltipLiveTestRunner()
    {
        TestRunnerApi.RegisterTestCallback(new Callbacks());
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (!File.Exists(Marker) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(ActiveKey, false) ||
            SessionState.GetBool(AnyRunKey, false)) return;
        File.Delete(Marker);
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (!scene.isDirty) continue;
            WriteStatus("REFUSED: unsaved scene changes: " + scene.name + " (" + scene.path + ")");
            return;
        }
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetString(FailuresKey, string.Empty);
        WriteStatus("RUNNING: ItemKeywordTooltipPlayModeTests");
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        try
        {
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.PlayMode,
                testNames = new[] { "ItemKeywordTooltipPlayModeTests" }
            }));
        }
        catch (Exception error)
        {
            SessionState.SetBool(ActiveKey, false);
            WriteStatus("DISPATCH FAILED: " + error);
        }
        finally { Object.DestroyImmediate(api); }
    }

    static void WriteStatus(string message)
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/playmode-status.txt", message);
    }

    sealed class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) => SessionState.SetBool(AnyRunKey, true);
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (!SessionState.GetBool(ActiveKey, false) || result.Test.IsSuite ||
                !result.ResultState.StartsWith("Failed", StringComparison.Ordinal)) return;
            SessionState.SetString(FailuresKey, SessionState.GetString(FailuresKey, string.Empty) +
                "\n" + result.FullName + ": " + result.ResultState + "\n" + result.Message);
        }
        public void RunFinished(ITestResultAdaptor result)
        {
            SessionState.SetBool(AnyRunKey, false);
            if (!SessionState.GetBool(ActiveKey, false)) return;
            try
            {
                Directory.CreateDirectory(Output);
                TestRunnerApi.SaveResultToFile(result, Output + "/playmode-results.xml");
                WriteStatus($"{result.ResultState}: passed={result.PassCount}; failed={result.FailCount}; " +
                    $"skipped={result.SkipCount}; inconclusive={result.InconclusiveCount}; duration={result.Duration:F2}s\n" +
                    result.Message + SessionState.GetString(FailuresKey, string.Empty));
            }
            finally { SessionState.SetBool(ActiveKey, false); }
        }
    }
}
#endif
