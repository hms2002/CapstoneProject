#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>Exercises the actual authored creature prefab through UI reflection without extending test asmdefs.</summary>
public sealed class EncyclopediaCreaturePlayModeTests
{
    private const string PrefabPath = "Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab";
    private const string CatalogPath = "Assets/_Project/Data/Encyclopedia/EncyclopediaCatalog.asset";
    private const string EvidencePath = "outputs/encyclopedia-implementation-2026-10-11";
    private const string RevealEvidencePath = "outputs/encyclopedia-tab-visibility-reveal-2026-10-12";
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type LocalizationType = Type.GetType("UnityEngine.Localization.Settings.LocalizationSettings, Unity.Localization", true);
    private Fixture fixture;
    private float originalTimeScale;
    private object previousLocale;
    private bool restoreLocale;

    [SetUp]
    public void SetUp() => originalTimeScale = Time.timeScale;

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = originalTimeScale;
        fixture?.Dispose();
        fixture = null;
    }

    [UnityTearDown]
    public IEnumerator RestoreSelectedLocale()
    {
        if (!restoreLocale) yield break;
        LocalizationType.GetProperty("SelectedLocale").SetValue(null, previousLocale);
        yield return WaitForLocalization();
        restoreLocale = false;
    }

    [TestCase(0, 0, new int[] { }, new int[] { })]
    [TestCase(1, 0, new[] { 0 }, new[] { 0 })]
    [TestCase(2, 0, new[] { 0, 1 }, new[] { -1, 0 })]
    [TestCase(2, 1, new[] { 0, 1 }, new[] { 0, 1 })]
    [TestCase(3, 0, new[] { 0, 1, 2 }, new[] { -1, 0, 1 })]
    [TestCase(3, 1, new[] { 0, 1, 2 }, new[] { -1, 0, 1 })]
    [TestCase(3, 2, new[] { 0, 1, 2 }, new[] { -1, 0, 1 })]
    [TestCase(7, 0, new[] { 0, 1, 2 }, new[] { -1, 0, 1 })]
    [TestCase(7, 3, new[] { 2, 3, 4 }, new[] { -1, 0, 1 })]
    [TestCase(7, 6, new[] { 4, 5, 6 }, new[] { -1, 0, 1 })]
    public void DotWindow_PreservesRequestedBoundaryPosition(int count, int selected, int[] indices, int[] offsets)
    {
        Type helper = UiType("EncyclopediaPortraitUtility");
        Assert.That(StaticCall(helper, "GetDotCount", count), Is.EqualTo(indices.Length));
        for (int slot = 0; slot < indices.Length; slot++)
        {
            Assert.That(StaticCall(helper, "GetDotPortraitIndex", count, selected, slot), Is.EqualTo(indices[slot]));
            Assert.That(StaticCall(helper, "GetDotOffset", count, selected, slot), Is.EqualTo(offsets[slot]));
        }
        Assert.That(StaticCall(helper, "GetDotPortraitIndex", count, selected, indices.Length), Is.EqualTo(-1));
    }

    [Test]
    public void DefaultPortrait_UsesActualIdleInsteadOfFirstRegisteredSpiriPose()
    {
        EncyclopediaCatalogSO catalog = LoadCatalog();
        EncyclopediaBossEntry spiri = catalog.BossEntries.Single(entry => entry.npcData.id == 2001);
        int index = (int)StaticCall(UiType("EncyclopediaPortraitUtility"), "GetDefaultIndex", spiri.portraits);
        Assert.That(spiri.portraits[index].label, Is.EqualTo("Spiri_Idle"));
        Assert.That(UnityEditor.AssetDatabase.GetAssetPath(spiri.portraits[index].sprite),
            Does.EndWith("/SpiriNotion/Spiri_Idle.png"));
    }

    [Test]
    public void AuthoredCharacterCards_UseCurrentPortraitsFilledHeartsAndNoCardDots()
    {
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        Component tab = ComponentOf(prefab, "EncyclopediaCreatureTab");
        Component detail = ComponentOf(prefab, "EncyclopediaCreatureDetailView");
        Assert.That(Field<Component>(tab, "entryCountText").gameObject.activeSelf, Is.False,
            "The authored creature pages must keep the species-count label hidden.");
        Assert.That(Field<Array>(tab, "monsterSlots").Length, Is.EqualTo(12));
        Array cards = Field<Array>(tab, "characterSlots");
        Assert.That(cards.Length, Is.EqualTo(4));
        foreach (Component card in cards)
        {
            Assert.That(card.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Dot", StringComparison.Ordinal)), Is.False);
            Array hearts = Field<Array>(card, "affectionFillImages");
            Assert.That(hearts.Length, Is.EqualTo(5));
            foreach (Component heart in hearts)
            {
                Assert.That(Property(heart, "type").ToString(), Is.EqualTo("Filled"));
                Assert.That(((Sprite)Property(heart, "sprite")).name, Is.EqualTo("Spritesheet_126"));
            }
            Component image = Field<Component>(card, "portraitImage");
            Assert.That(((RectTransform)image.transform).pivot, Is.EqualTo(new Vector2(.5f, 0f)));
            Assert.That(Property(image.GetComponent(UiEngineType("AspectRatioFitter")), "aspectMode").ToString(), Is.EqualTo("EnvelopeParent"));
        }
        Component hero = Field<Component>(detail, "characterImage");
        Transform topFade = hero.transform.parent.Find("TopFade");
        Assert.That(topFade, Is.Not.Null);
        Component gradient = topFade.GetComponent(UiType("EncyclopediaGradientGraphic"));
        Assert.That(Field<Color>(gradient, "topColor").a, Is.GreaterThan(.5f));
        Assert.That(Field<Color>(gradient, "bottomColor").a, Is.Zero);
        EncyclopediaCatalogSO catalog = LoadCatalog();
        Assert.That(catalog.BossEntries.Count, Is.EqualTo(4));
        Assert.That(catalog.NpcEntries.Count, Is.EqualTo(5));
        foreach (EncyclopediaBossEntry boss in catalog.BossEntries)
        {
            object library = Field<object>(boss.npcData, "spriteLibraryAsset");
            foreach (EncyclopediaPortrait portrait in boss.portraits)
                Assert.That(library.GetType().GetMethod("GetSprite", new[] { typeof(string), typeof(string) })
                    .Invoke(library, new object[] { "Face", portrait.label }), Is.SameAs(portrait.sprite));
        }
    }

    [UnityTest]
    public IEnumerator NativePaging_BindsFourBossesAndBothNpcPagesWithoutChangingProgress()
    {
        fixture = new Fixture();
        string progress = ProgressSnapshot();
        Call(fixture.Tab, "Show", EncyclopediaMainTab.Boss);
        yield return null;
        Assert.That(ActiveSlots(fixture.Tab, "characterSlots"), Is.EqualTo(4));
        Assert.That(Field<GameObject>(fixture.Tab, "leftContentRoot").activeInHierarchy, Is.True);
        Assert.That(Field<GameObject>(fixture.Tab, "rightContentRoot").activeInHierarchy, Is.True);
        Assert.That((bool)Property(Field<Component>(fixture.Tab, "nextPageButton"), "interactable"), Is.False);
        Call(fixture.Tab, "Show", EncyclopediaMainTab.NPC);
        Assert.That(ActiveSlots(fixture.Tab, "characterSlots"), Is.EqualTo(4));
        Assert.That(Text(Field<Component>(fixture.Tab, "pageText")), Is.EqualTo("1 / 2"));
        Assert.That(Field<Component>(fixture.Detail, "previousPortraitButton").gameObject.activeSelf, Is.False);
        Assert.That(Field<Component>(fixture.Detail, "nextPortraitButton").gameObject.activeSelf, Is.False,
            "A one-portrait NPC must hide both expression arrows.");
        Click(Field<Component>(fixture.Tab, "nextPageButton"));
        Assert.That(ActiveSlots(fixture.Tab, "characterSlots"), Is.EqualTo(1));
        Assert.That(Text(Field<Component>(fixture.Tab, "pageText")), Is.EqualTo("2 / 2"));
        Assert.That(Text(Field<Component>(fixture.Detail, "characterTitleText")), Is.EqualTo(LoadCatalog().NpcEntries[4].DisplayName));
        Assert.That(Field<Component>(fixture.Detail, "previousPortraitButton").gameObject.activeSelf, Is.False);
        Assert.That(Field<Component>(fixture.Detail, "nextPortraitButton").gameObject.activeSelf, Is.False);
        Assert.That(ProgressSnapshot(), Is.EqualTo(progress));
    }

    [UnityTest]
    public IEnumerator SevenPortraits_BindRealImagesSyncCardDotsAndCloseFullViewBeforeBook()
    {
        fixture = new Fixture(true);
        string progress = ProgressSnapshot();
        Call(fixture.Screen, "OpenBossTab");
        yield return null;
        EncyclopediaCatalogSO catalog = LoadCatalog();
        int meltaIndex = Enumerable.Range(0, catalog.BossEntries.Count).Single(i => catalog.BossEntries[i].npcData.id == 3001);
        Call(fixture.Tab, "SelectEntry", meltaIndex);
        EncyclopediaBossEntry melta = catalog.BossEntries[meltaIndex];
        Assert.That(melta.portraits.Count, Is.EqualTo(7));
        Array dots = Field<Array>(fixture.Detail, "portraitDotButtons");
        Array markers = Field<Array>(fixture.Detail, "portraitDotSelectedMarkers");
        Component previousArrow = Field<Component>(fixture.Detail, "previousPortraitButton");
        Component nextArrow = Field<Component>(fixture.Detail, "nextPortraitButton");
        Assert.That(CurrentPortrait(fixture.Detail), Is.SameAs(melta.portraits[0].sprite));
        Assert.That(previousArrow.gameObject.activeSelf, Is.False, "The first portrait must hide the unavailable previous arrow.");
        Assert.That(nextArrow.gameObject.activeSelf, Is.True);
        Call(fixture.Tab, "SetInteractionEnabled", false);
        Assert.That(previousArrow.gameObject.activeSelf, Is.False);
        Assert.That(nextArrow.gameObject.activeSelf, Is.False, "Input locking must hide both expression arrows.");
        Assert.That((bool)Property(nextArrow, "interactable"), Is.False);
        Click(nextArrow);
        Assert.That(CurrentPortrait(fixture.Detail), Is.SameAs(melta.portraits[0].sprite), "Locked arrow input must not change the portrait.");
        Call(fixture.Tab, "SetInteractionEnabled", true);
        Assert.That(previousArrow.gameObject.activeSelf, Is.False);
        Assert.That(nextArrow.gameObject.activeSelf, Is.True, "Unlocking must restore only the direction available at the current portrait.");
        Assert.That((bool)Property(nextArrow, "interactable"), Is.True);
        Assert.That(((GameObject)markers.GetValue(0)).activeSelf, Is.True);
        Assert.That(((GameObject)markers.GetValue(1)).activeSelf, Is.False);
        float spacing = Field<float>(fixture.Detail, "portraitDotSpacing");
        Assert.That(((RectTransform)((Component)dots.GetValue(1)).transform).anchoredPosition.x, Is.Zero);
        for (int i = 1; i < melta.portraits.Count; i++)
        {
            Click(Field<Component>(fixture.Detail, "nextPortraitButton"));
            Assert.That(CurrentPortrait(fixture.Detail), Is.SameAs(melta.portraits[i].sprite));
        }
        Assert.That(((GameObject)markers.GetValue(2)).activeSelf, Is.True);
        Assert.That(((RectTransform)((Component)dots.GetValue(2)).transform).anchoredPosition.x, Is.EqualTo(spacing));
        Assert.That(previousArrow.gameObject.activeSelf, Is.True);
        Assert.That(nextArrow.gameObject.activeSelf, Is.False, "The final portrait must hide the unavailable next arrow.");
        Assert.That((bool)Property(Field<Component>(fixture.Detail, "nextPortraitButton"), "interactable"), Is.False);
        Component selectedCard = (Component)Field<Array>(fixture.Tab, "characterSlots").GetValue(meltaIndex);
        Assert.That(Property(Field<Component>(selectedCard, "portraitImage"), "sprite"), Is.SameAs(CurrentPortrait(fixture.Detail)));
        Click(Field<Component>(fixture.Detail, "fullViewButton"));
        Assert.That(Field<GameObject>(fixture.Detail, "fullViewRoot").activeInHierarchy, Is.True);
        Assert.That(Property(Field<Component>(fixture.Detail, "fullViewImage"), "sprite"), Is.SameAs(CurrentPortrait(fixture.Detail)));
        Assert.That(Call(fixture.Screen, "TryHandleCloseRequest"), Is.EqualTo(true));
        Assert.That(Field<GameObject>(fixture.Detail, "fullViewRoot").activeSelf, Is.False);
        Assert.That(Field<GameObject>(fixture.Tab, "rightContentRoot").activeInHierarchy, Is.True);
        Click(Field<Component>(fixture.Detail, "previousPortraitButton"));
        Assert.That(CurrentPortrait(fixture.Detail), Is.SameAs(melta.portraits[5].sprite));
        Assert.That(nextArrow.gameObject.activeSelf, Is.True, "Moving back from the final portrait must restore the next arrow.");
        Assert.That((bool)Property(nextArrow, "interactable"), Is.True);
        Assert.That(ProgressSnapshot(), Is.EqualTo(progress));
    }

    [UnityTest]
    public IEnumerator PausedMonsterPreview_AdvancesAuthoredFramesWithoutSpawningEnemies()
    {
        fixture = new Fixture();
        EncyclopediaCatalogSO catalog = LoadCatalog();
        int animatedIndex = -1;
        for (int i = 0; i < catalog.MonsterEntries.Count; i++)
            if (catalog.MonsterEntries[i].previewFrames.Length > 1) { animatedIndex = i; break; }
        Assert.That(animatedIndex, Is.GreaterThanOrEqualTo(0));
        int[] enemiesBefore = Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(enemy => enemy.GetInstanceID()).OrderBy(id => id).ToArray();
        Time.timeScale = 0f;
        Call(fixture.Tab, "Show", EncyclopediaMainTab.Monster);
        Call(fixture.Tab, "SelectEntry", animatedIndex);
        Assert.That(catalog.TryGetMonster(animatedIndex, out EncyclopediaMonsterEntry monster), Is.True);
        int frameBefore = Field<int>(fixture.Detail, "previewIndex");
        bool advanced = false;
        float deadline = Time.realtimeSinceStartup + 1f;
        while (!advanced && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            advanced = Field<int>(fixture.Detail, "previewIndex") != frameBefore;
        }
        Assert.That(advanced, Is.True, "Idle preview must use unscaled time while the book freezes gameplay.");
        Assert.That(monster.previewFrames, Does.Contain(Property(Field<Component>(fixture.Detail, "monsterImage"), "sprite")));
        CollectionAssert.AreEqual(enemiesBefore, Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(enemy => enemy.GetInstanceID()).OrderBy(id => id).ToArray());
        Assert.That(Text(Field<Component>(fixture.Detail, "killCountText")),
            Is.EqualTo(GameText.Format("encyclopedia.monster_kill_count", "누적 처치 수 {0}회", MonsterEncyclopediaProgress.GetKillCount(monster.id))));
        Call(fixture.Tab, "Hide");
        Assert.That(Field<bool>(fixture.Detail, "showingMonster"), Is.False);
    }

    [Test]
    public void MissingCharacterData_UsesAuthoredEmptyStateAndZeroHearts()
    {
        fixture = new Fixture();
        Call(fixture.Tab, "Show", EncyclopediaMainTab.Boss);
        Call(fixture.Detail, "ShowCharacter", "", "", null, null, 0, true);
        Assert.That(CurrentPortrait(fixture.Detail), Is.Null);
        Assert.That(Field<Component>(fixture.Detail, "previousPortraitButton").gameObject.activeSelf, Is.False);
        Assert.That(Field<Component>(fixture.Detail, "nextPortraitButton").gameObject.activeSelf, Is.False,
            "Missing portrait data must hide both expression arrows.");
        Assert.That(Field<Component>(fixture.Detail, "portraitMissingText").gameObject.activeSelf, Is.True);
        foreach (Component dot in Field<Array>(fixture.Detail, "portraitDotButtons"))
            Assert.That(dot.gameObject.activeSelf, Is.False);
        foreach (Component heart in Field<Array>(fixture.Detail, "affectionFillImages"))
            Assert.That(Property(heart, "fillAmount"), Is.EqualTo(0f));
        Assert.That(Text(Field<Component>(fixture.Detail, "storyText")),
            Is.EqualTo(GameText.Get("code.encyclopediadetailpanel.c6793d9ba0", "설명 준비 중")));
        Assert.That(Text(Field<Component>(fixture.Detail, "rewardText")),
            Is.EqualTo(GameText.Get("code.encyclopediadetailpanel.5df09cdf01", "등록된 호감도 보상 없음")));
    }

    [UnityTest]
    public IEnumerator ActualCreaturePages_RenderAt1920x1080()
    {
        fixture = new Fixture();
        Directory.CreateDirectory(EvidencePath);
        foreach (EncyclopediaMainTab tab in new[] { EncyclopediaMainTab.Boss, EncyclopediaMainTab.Monster, EncyclopediaMainTab.NPC })
        {
            Call(fixture.Tab, "Show", tab);
            yield return null;
            Canvas.ForceUpdateCanvases();
            fixture.Capture(EvidencePath + "/actual-" + tab.ToString().ToLowerInvariant() + "-1920x1080.png");
        }
    }

    [UnityTest]
    public IEnumerator ScreenRouting_ActivatesNativeTabsAndPreservesMainBookmarkRail()
    {
        fixture = new Fixture(true);
        foreach (string method in new[] { "OpenMonsterTab", "OpenBossTab", "OpenNpcTab" })
        {
            Call(fixture.Screen, method);
            yield return null;
            Assert.That(Field<GameObject>(fixture.Tab, "leftContentRoot").activeInHierarchy, Is.True);
            Assert.That(Field<GameObject>(fixture.Tab, "rightContentRoot").activeInHierarchy, Is.True);
            foreach (string button in new[] { "monsterMainTabButton", "bossMainTabButton", "npcMainTabButton" })
                Assert.That(Field<Component>(fixture.Screen, button).gameObject.activeInHierarchy, Is.True);
        }
        Call(fixture.Screen, "CloseUI");
        Assert.That(Field<GameObject>(fixture.Tab, "leftContentRoot").activeSelf, Is.False);
        Assert.That(Field<GameObject>(fixture.Tab, "rightContentRoot").activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator AnimatedBookPageTurns_LeaveOnlyCurrentTabContentVisible()
    {
        fixture = new Fixture(true, true);
        Time.timeScale = 0f;
        string progress = ProgressSnapshot();
        Assert.That(fixture.Book, Is.Not.Null);
        Assert.That(Property(fixture.Book, "CanPlayRightPageTurn"), Is.EqualTo(true),
            "This regression must execute the authored animation rather than the immediate fallback.");
        Assert.That(Field<bool>(fixture.Book, "setContentRootsActive"), Is.False);
        Array groups = Field<Array>(fixture.Book, "additionalContentGroups");
        Assert.That(groups.Length, Is.GreaterThanOrEqualTo(2));
        foreach (string method in new[] { "OpenBossTab", "OpenMonsterTab", "OpenNpcTab" })
        {
            Call(fixture.Screen, method);
            Assert.That(Property(fixture.Book, "IsPlaying"), Is.EqualTo(true));
            Assert.That(Field<CanvasGroup>(fixture.Screen, "canvasGroup").interactable, Is.False);
            float deadline = Time.realtimeSinceStartup + 8f;
            while ((bool)Property(fixture.Book, "IsPlaying") && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(Property(fixture.Book, "IsPlaying"), Is.EqualTo(false), "Unscaled page turn did not complete within eight seconds.");
            Assert.That(Field<CanvasGroup>(fixture.Screen, "canvasGroup").interactable, Is.True);
            Assert.That(Field<GameObject>(fixture.Tab, "leftContentRoot").activeInHierarchy, Is.True);
            Assert.That(Field<GameObject>(fixture.Tab, "rightContentRoot").activeInHierarchy, Is.True);
            foreach (CanvasGroup group in groups)
                Assert.That(group.alpha, Is.EqualTo(1f).Within(.001f), "Visible creature pages must be restored after the shared book animation.");
            Transform book = fixture.Tab.transform;
            Assert.That(book.Find("LeftPage").gameObject.activeSelf, Is.False,
                "Shared page animation must not reactivate the hidden Item left page.");
            GameObject itemDetails = book.GetComponentsInChildren<Transform>(true).Single(t => t.name == "ItemDetailPanel").gameObject;
            Assert.That(itemDetails.activeSelf, Is.False, "Shared page animation must not reactivate Item details.");
            Assert.That(Field<GameObject>(fixture.Detail, "monsterRoot").activeSelf, Is.EqualTo(method == "OpenMonsterTab"));
            Assert.That(Field<GameObject>(fixture.Detail, "characterRoot").activeSelf, Is.EqualTo(method != "OpenMonsterTab"));
        }
        Assert.That(ProgressSnapshot(), Is.EqualTo(progress));
    }

    [UnityTest]
    public IEnumerator LiveItemPresenters_AnimatedCreatureRoundTripsHideCompleteDetailsAndRestoreSelection()
    {
        fixture = new Fixture(true, true, true);
        Time.timeScale = 0f;
        string progress = ProgressSnapshot();
        Call(fixture.Book, "SnapOpened");
        Call(fixture.Screen, "OpenUI");
        ItemDatabase database = Field<ItemDatabase>(fixture.ItemTab, "itemDatabase");
        Assert.That(database.allWeapons.Count(weapon => weapon != null), Is.GreaterThan(1));
        Call(fixture.ItemTab, "SelectEntry", EncyclopediaCategory.Weapon, 1);
        ScriptableObject selected = Field<ScriptableObject>(fixture.ItemTab, "selectedItem");
        string title = Text(Field<Component>(fixture.ItemRightPage, "titleText"));
        string story = Text(Field<Component>(fixture.ItemRightPage, "storyText"));
        Assert.That(selected, Is.Not.Null);
        Assert.That(title, Is.Not.Empty);
        Assert.That(story, Is.Not.Empty);
        AssertLiveItemVisibility(true);
        Assert.That(Property(fixture.Book, "CanPlayContentReveal"), Is.EqualTo(true));

        foreach (string method in new[] { "OpenBossTab", "OpenMonsterTab", "OpenNpcTab" })
        {
            Call(fixture.Screen, method);
            bool sawCreatureReveal = false;
            float deadline = Time.realtimeSinceStartup + 8f;
            while ((bool)Property(fixture.Book, "IsPlaying") && Time.realtimeSinceStartup < deadline)
            {
                if (Field<EncyclopediaMainTab>(fixture.Screen, "currentMainTab") != EncyclopediaMainTab.Item)
                {
                    AssertLiveItemVisibility(false);
                    sawCreatureReveal |= Field<Component>(fixture.Book, "pageCoverImage").gameObject.activeInHierarchy;
                }
                yield return null;
            }
            Assert.That(Property(fixture.Book, "IsPlaying"), Is.EqualTo(false));
            Assert.That(sawCreatureReveal, Is.True, "Exercise the authored reveal after the real Item-to-Creature swap.");
            AssertLiveItemVisibility(false);
            Assert.That(Field<GameObject>(fixture.Tab, "leftContentRoot").activeInHierarchy, Is.True);
            Assert.That(Field<GameObject>(fixture.Tab, "rightContentRoot").activeInHierarchy, Is.True);
            Call(fixture.Screen, method); // The same-tab refresh must not restore the old Item page.
            AssertLiveItemVisibility(false);

            Call(fixture.Screen, "OpenItemTab");
            yield return WaitForBookPresentation(fixture.Book);
            AssertLiveItemVisibility(true);
            Assert.That(Field<GameObject>(fixture.Tab, "leftContentRoot").activeInHierarchy, Is.False);
            Assert.That(Field<GameObject>(fixture.Tab, "rightContentRoot").activeInHierarchy, Is.False);
            Assert.That(Field<int>(fixture.ItemTab, "selectedIndex"), Is.EqualTo(1));
            Assert.That(Field<ScriptableObject>(fixture.ItemTab, "selectedItem"), Is.SameAs(selected));
            Assert.That(Text(Field<Component>(fixture.ItemRightPage, "titleText")), Is.EqualTo(title));
            Assert.That(Text(Field<Component>(fixture.ItemRightPage, "storyText")), Is.EqualTo(story));
        }
        Assert.That(ProgressSnapshot(), Is.EqualTo(progress));
    }

    [UnityTest]
    public IEnumerator LiveItemPresenters_CloseDuringCreatureRevealReopensWithoutStaleDetails()
    {
        fixture = new Fixture(true, true, true);
        Time.timeScale = 0f;
        string progress = ProgressSnapshot();
        Call(fixture.Book, "SnapOpened");
        Call(fixture.Screen, "OpenUI");
        AssertLiveItemVisibility(true);
        Call(fixture.Screen, "OpenBossTab");
        float deadline = Time.realtimeSinceStartup + 8f;
        while (Field<EncyclopediaMainTab>(fixture.Screen, "currentMainTab") != EncyclopediaMainTab.Boss &&
               (bool)Property(fixture.Book, "IsPlaying") && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(Field<EncyclopediaMainTab>(fixture.Screen, "currentMainTab"), Is.EqualTo(EncyclopediaMainTab.Boss));
        Assert.That(Property(fixture.Book, "IsPlaying"), Is.EqualTo(true), "Close while the target page reveal is still running.");
        AssertLiveItemVisibility(false);
        Call(fixture.Screen, "CloseUI");
        Assert.That(Field<GameObject>(fixture.ItemRightPage, "pageContentRoot").activeSelf, Is.False);
        Assert.That(Property(fixture.Book, "IsPlaying"), Is.EqualTo(false));
        Assert.That(Field<Component>(fixture.Book, "pageCoverImage").gameObject.activeSelf, Is.False);
        yield return null;

        Call(fixture.Screen, "OpenUI");
        Assert.That(Field<EncyclopediaMainTab>(fixture.Screen, "currentMainTab"), Is.EqualTo(EncyclopediaMainTab.Item));
        AssertLiveItemVisibility(true);
        Call(fixture.Screen, "OpenBossTab");
        yield return WaitForBookPresentation(fixture.Book);
        AssertLiveItemVisibility(false);
        Assert.That(ProgressSnapshot(), Is.EqualTo(progress));
    }

    [UnityTest]
    public IEnumerator LiveItemPresenters_NativeBossRevealCapturesCoverAboveCreaturePages()
    {
        yield return WaitForLocalization();
        fixture = new Fixture(true, true, true);
        Time.timeScale = 0f;
        string progress = ProgressSnapshot();
        Call(fixture.Book, "SnapOpened");
        Call(fixture.Screen, "OpenUI");
        yield return fixture.WaitForNativeFonts();
        AssertLiveItemVisibility(true);
        Call(fixture.Screen, "OpenBossTab");
        Component cover = Field<Component>(fixture.Book, "pageCoverImage");
        float deadline = Time.realtimeSinceStartup + 8f;
        while ((Field<EncyclopediaMainTab>(fixture.Screen, "currentMainTab") != EncyclopediaMainTab.Boss ||
                !cover.gameObject.activeInHierarchy) && (bool)Property(fixture.Book, "IsPlaying") &&
               Time.realtimeSinceStartup < deadline)
            yield return null;
        yield return fixture.WaitForNativeFonts();
        Assert.That(Field<EncyclopediaMainTab>(fixture.Screen, "currentMainTab"), Is.EqualTo(EncyclopediaMainTab.Boss));
        Assert.That(Property(fixture.Book, "IsPlaying"), Is.EqualTo(true));
        Assert.That(cover.gameObject.activeInHierarchy, Is.True);
        Assert.That(Property(cover, "enabled"), Is.EqualTo(true));
        GameObject left = Field<GameObject>(fixture.Tab, "leftContentRoot");
        GameObject right = Field<GameObject>(fixture.Tab, "rightContentRoot");
        GameObject fullView = Field<GameObject>(fixture.Detail, "fullViewRoot");
        Assert.That(cover.transform.parent, Is.SameAs(left.transform.parent));
        Assert.That(cover.transform.parent, Is.SameAs(right.transform.parent));
        Assert.That(cover.transform.parent, Is.SameAs(fullView.transform.parent));
        Assert.That(cover.transform.GetSiblingIndex(), Is.GreaterThan(left.transform.GetSiblingIndex()));
        Assert.That(cover.transform.GetSiblingIndex(), Is.GreaterThan(right.transform.GetSiblingIndex()));
        Assert.That(fullView.transform.GetSiblingIndex(), Is.GreaterThan(cover.transform.GetSiblingIndex()));
        AnimationClip clip = Field<AnimationClip>(fixture.Book, "contentAppearClip");
        Assert.That(clip, Is.Not.Null);
        Assert.That(clip.length, Is.GreaterThan(0f));
        Directory.CreateDirectory(RevealEvidencePath);
        var evidence = new List<string>();
        string[] phases = { "early", "mid", "final" };
        float[] times = { 0f, clip.length * .5f, clip.length };
        for (int i = 0; i < phases.Length; i++)
        {
            // Capture immediately after sampling; yielding would let the live coroutine overwrite this frame.
            Call(fixture.Book, "SamplePageCover", times[i]);
            AssertLiveItemVisibility(false);
            Component frame = Field<Component>(fixture.Book, "bookFrameImage");
            Assert.That(frame.gameObject.activeInHierarchy, Is.True);
            Assert.That(Property(frame, "enabled"), Is.EqualTo(true));
            Assert.That(left.activeInHierarchy && right.activeInHierarchy, Is.True);
            fixture.Capture(RevealEvidencePath + "/actual-boss-reveal-" + phases[i] + "-1920x1080.png");
            evidence.Add(JsonUtility.ToJson(new NativeRevealSnapshot
            {
                phase = phases[i], sampleTime = times[i], bookFrameVisible = frame.gameObject.activeInHierarchy,
                coverVisible = cover.gameObject.activeInHierarchy && (bool)Property(cover, "enabled"),
                itemRootVisible = Field<GameObject>(fixture.ItemRightPage, "pageContentRoot").activeInHierarchy,
                creaturePagesVisible = left.activeInHierarchy && right.activeInHierarchy,
                coverIndex = cover.transform.GetSiblingIndex(), creatureLeftIndex = left.transform.GetSiblingIndex(),
                creatureRightIndex = right.transform.GetSiblingIndex(), fullViewIndex = fullView.transform.GetSiblingIndex()
            }, true));
        }
        yield return WaitForBookPresentation(fixture.Book);
        AssertLiveItemVisibility(false);
        Assert.That(cover.gameObject.activeSelf, Is.False);
        Assert.That(Property(cover, "enabled"), Is.EqualTo(false));
        fixture.Capture(RevealEvidencePath + "/native-boss-post-reveal-1920x1080.png");
        evidence.Add(JsonUtility.ToJson(new NativeRevealSnapshot
        {
            phase = "post-reveal", sampleTime = clip.length,
            coverVisible = cover.gameObject.activeInHierarchy && (bool)Property(cover, "enabled"),
            bookFrameVisible = Field<Component>(fixture.Book, "bookFrameImage").gameObject.activeInHierarchy,
            itemRootVisible = Field<GameObject>(fixture.ItemRightPage, "pageContentRoot").activeInHierarchy,
            creaturePagesVisible = left.activeInHierarchy && right.activeInHierarchy,
            coverIndex = cover.transform.GetSiblingIndex(), creatureLeftIndex = left.transform.GetSiblingIndex(),
            creatureRightIndex = right.transform.GetSiblingIndex(), fullViewIndex = fullView.transform.GetSiblingIndex()
        }, true));
        File.WriteAllText(RevealEvidencePath + "/native-reveal-states.json", "[\n" + string.Join(",\n", evidence) + "\n]\n");
        Assert.That(ProgressSnapshot(), Is.EqualTo(progress));
    }

    [UnityTest]
    public IEnumerator LiveItemPresenters_HiddenPageIgnoresShortcutsLocaleAndScrollReset()
    {
        fixture = new Fixture(true, false, true);
        Time.timeScale = 0f;
        string progress = ProgressSnapshot();
        Call(fixture.Screen, "OpenUI");
        AssertLiveItemVisibility(true);
        using (var keyboard = new ShortcutKeyboard())
        {
            keyboard.Set(InputContextShortcutId.TooltipDescriptionToggle, true);
            Call(fixture.ItemRightPage, "Update");
            Assert.That(Field<bool>(fixture.ItemRightPage, "showDetailedDescription"), Is.True,
                "The injected shortcut must work while the actual Item page is visible.");
            keyboard.Set(InputContextShortcutId.TooltipDescriptionToggle, false);
            Call(fixture.Screen, "SetInteractionEnabled", false);
            keyboard.Set(InputContextShortcutId.TooltipDescriptionToggle, true);
            Call(fixture.ItemRightPage, "Update");
            Assert.That(Field<bool>(fixture.ItemRightPage, "showDetailedDescription"), Is.True,
                "The Screen interaction lock used by Reveal must also block visible Item shortcuts.");
            keyboard.Set(InputContextShortcutId.TooltipDescriptionToggle, false);
            Call(fixture.Screen, "SetInteractionEnabled", true);
            Call(fixture.ItemTab, "SetContentVisible", false);
            bool hiddenDetailMode = Field<bool>(fixture.ItemRightPage, "showDetailedDescription");
            keyboard.Set(InputContextShortcutId.TooltipDescriptionToggle, true);
            Call(fixture.ItemRightPage, "Update");
            Assert.That(Field<bool>(fixture.ItemRightPage, "showDetailedDescription"), Is.EqualTo(hiddenDetailMode));
            keyboard.Set(InputContextShortcutId.TooltipDescriptionToggle, false);

            Component story = Field<Component>(fixture.ItemRightPage, "storyText");
            story.GetType().GetProperty("text").SetValue(story, "Hidden projection sentinel");
            Call(fixture.ItemRightPage, "HandleLocaleChanged", new object[] { null });
            Assert.That(Text(story), Is.EqualTo("Hidden projection sentinel"));
            Component scroll = Field<Component>(fixture.ItemRightPage, "detailScrollRect");
            RectTransform content = (RectTransform)Property(scroll, "content");
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 73f);
            Vector2 hiddenScrollPosition = content.anchoredPosition;
            Call(fixture.ItemRightPage, "QueueScrollReset");
            Assert.That(content.anchoredPosition, Is.EqualTo(hiddenScrollPosition));
            Assert.That(Field<Coroutine>(fixture.ItemRightPage, "pendingScrollReset"), Is.Null);
            AssertLiveItemVisibility(false);

            var relic = ScriptableObject.CreateInstance<RelicDefinition>();
            try
            {
                relic.maxLevel = 3;
                Call(fixture.ItemTab, "SetContentVisible", true);
                Assert.That(Text(story), Is.Not.EqualTo("Hidden projection sentinel"),
                    "A deferred locale refresh must be consumed when the Item page becomes visible again.");
                Call(fixture.ItemRightPage, "ShowRelic", relic);
                keyboard.Set(InputContextShortcutId.RelicPreviewNext, true);
                Call(fixture.ItemRightPage, "Update");
                Assert.That(Field<int>(fixture.ItemRightPage, "relicPreviewLevel"), Is.EqualTo(2));
                keyboard.Set(InputContextShortcutId.RelicPreviewNext, false);
                Call(fixture.Screen, "SetInteractionEnabled", false);
                keyboard.Set(InputContextShortcutId.RelicPreviewNext, true);
                Call(fixture.ItemRightPage, "Update");
                Assert.That(Field<int>(fixture.ItemRightPage, "relicPreviewLevel"), Is.EqualTo(2));
                keyboard.Set(InputContextShortcutId.RelicPreviewNext, false);
                Call(fixture.Screen, "SetInteractionEnabled", true);
                Call(fixture.ItemTab, "SetContentVisible", false);
                foreach (InputContextShortcutId shortcut in new[] { InputContextShortcutId.RelicPreviewNext, InputContextShortcutId.RelicPreviewPrevious })
                {
                    keyboard.Set(shortcut, true);
                    Call(fixture.ItemRightPage, "Update");
                    Assert.That(Field<int>(fixture.ItemRightPage, "relicPreviewLevel"), Is.EqualTo(2));
                    keyboard.Set(shortcut, false);
                }
                Assert.That(Field<Coroutine>(fixture.ItemRightPage, "pendingScrollReset"), Is.Null);
                yield return null;
                AssertLiveItemVisibility(false);
            }
            finally { Object.DestroyImmediate(relic); }
        }
        Assert.That(ProgressSnapshot(), Is.EqualTo(progress));
    }

    [UnityTest]
    public IEnumerator LiveItemPresenters_LocaleChangedOnCreatureTabRefreshesRestoredItemSelection()
    {
        Type settingsType = UiType("GameSettingsService");
        for (int frame = 0; frame < 180; frame++)
        {
            object settings = settingsType.GetProperty("Instance").GetValue(null);
            if (settings == null || (Field<bool>(settings, "initialized") && Field<object>(settings, "localeInitialization") == null))
                break;
            if (frame == 179) Assert.Fail("Saved-locale bootstrap did not finish.");
            yield return null;
        }
        yield return WaitForLocalization();
        previousLocale = LocalizationType.GetProperty("SelectedLocale").GetValue(null);
        restoreLocale = true;
        Type localeType = Type.GetType("UnityEngine.Localization.Locale, Unity.Localization", true);
        Object korean = UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/_Project/Data/Localization/Locale_ko.asset", localeType);
        Object english = UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/_Project/Data/Localization/Locale_en.asset", localeType);
        Assert.That(korean, Is.Not.Null);
        Assert.That(english, Is.Not.Null);
        LocalizationType.GetProperty("SelectedLocale").SetValue(null, korean);
        yield return WaitForLocalization();
        yield return null;

        fixture = new Fixture(true, false, true);
        Time.timeScale = 0f;
        string progress = ProgressSnapshot();
        Call(fixture.Screen, "OpenUI");
        Call(fixture.ItemTab, "SelectEntry", EncyclopediaCategory.Weapon, 1);
        WeaponDefinition selected = Field<WeaponDefinition>(fixture.ItemTab, "selectedItem");
        Assert.That(selected, Is.Not.Null);
        string koreanTitle = Text(Field<Component>(fixture.ItemRightPage, "titleText"));
        string koreanStory = Text(Field<Component>(fixture.ItemRightPage, "storyText"));
        AssertLiveItemVisibility(true);
        Call(fixture.Screen, "OpenBossTab");
        AssertLiveItemVisibility(false);
        LocalizationType.GetProperty("SelectedLocale").SetValue(null, english);
        yield return WaitForLocalization();
        yield return null;
        yield return null;
        AssertLiveItemVisibility(false);
        Assert.That(Text(Field<Component>(fixture.ItemRightPage, "titleText")), Is.EqualTo(koreanTitle));
        string englishTitle = selected.DisplayName;
        string englishStory = (string)Call(fixture.ItemRightPage, "FormatText", GameText.Asset(selected, "storyText", selected.storyText));
        Assert.That(englishTitle, Is.Not.EqualTo(koreanTitle), "The selected actual weapon must have distinct translations.");
        Assert.That(englishStory, Is.Not.EqualTo(koreanStory));

        Call(fixture.Screen, "OpenItemTab");
        AssertLiveItemVisibility(true);
        Assert.That(Field<int>(fixture.ItemTab, "selectedIndex"), Is.EqualTo(1));
        Assert.That(Field<ScriptableObject>(fixture.ItemTab, "selectedItem"), Is.SameAs(selected));
        Assert.That(Text(Field<Component>(fixture.ItemRightPage, "titleText")), Is.EqualTo(englishTitle));
        Assert.That(Text(Field<Component>(fixture.ItemRightPage, "storyText")), Is.EqualTo(englishStory));
        Assert.That(ProgressSnapshot(), Is.EqualTo(progress));
    }

    [UnityTest]
    public IEnumerator CharacterStoryScrollbar_AutoHidesAfterShrinkWithoutChangingViewportWidth()
    {
        fixture = new Fixture();
        Time.timeScale = 0f;
        string progress = ProgressSnapshot();
        Call(fixture.Tab, "Show", EncyclopediaMainTab.Boss);
        yield return null;
        Component scroll = Field<Component>(fixture.Detail, "characterScrollRect");
        Component scrollbar = (Component)Property(scroll, "verticalScrollbar");
        Assert.That(scrollbar, Is.Not.Null, "Character details must use the authored right scrollbar.");
        Assert.That(Property(scroll, "verticalScrollbarVisibility").ToString(), Is.EqualTo("AutoHide"));
        RectTransform viewport = (RectTransform)Property(scroll, "viewport");
        RectTransform content = (RectTransform)Property(scroll, "content");
        Assert.That(Property(viewport.GetComponent(UiEngineType("Image")), "raycastTarget"), Is.EqualTo(true),
            "The viewport must remain a pointer surface for native wheel and drag events.");
        // Isolate the fit/overflow boundary from the book's normal short viewport and section headers.
        ((RectTransform)scroll.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 520f);
        EncyclopediaBossEntry entry = LoadCatalog().BossEntries[0];
        Call(fixture.Detail, "ShowCharacter", entry.DisplayName, "Short story.", null, entry.portraits, 0, true);
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;
        float viewportWidth = viewport.rect.width;
        Assert.That(content.rect.height, Is.LessThan(viewport.rect.height - 1f));
        Assert.That(scrollbar.gameObject.activeSelf, Is.False, "Short content must hide the scrollbar.");

        string longStory = string.Join("\n", Enumerable.Range(1, 36).Select(i => "Story line " + i + ": enough lines to overflow the authored viewport."));
        Call(fixture.Detail, "ShowCharacter", entry.DisplayName, longStory, null, entry.portraits, 0, true);
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;
        Assert.That(content.rect.height, Is.GreaterThan(viewport.rect.height + 1f));
        Assert.That(scrollbar.gameObject.activeInHierarchy, Is.True, "Overflowing story must reveal the scrollbar.");
        Assert.That(viewport.rect.width, Is.EqualTo(viewportWidth).Within(.1f), "AutoHide must reserve fixed scrollbar space rather than expand the viewport.");
        Assert.That((float)Property(scroll, "verticalNormalizedPosition"), Is.EqualTo(1f).Within(.01f));

        Type pointerType = Type.GetType("UnityEngine.EventSystems.PointerEventData, UnityEngine.UI", true);
        object pointer = Activator.CreateInstance(pointerType, new object[] { null });
        pointerType.GetProperty("scrollDelta").SetValue(pointer, new Vector2(0f, -40f));
        Vector2 beforeWheel = content.anchoredPosition;
        Call(scroll, "OnScroll", pointer);
        yield return null;
        Assert.That(content.anchoredPosition.y, Is.GreaterThan(beforeWheel.y), "The native ScrollRect wheel handler must move overflow content.");
        Assert.That((float)Property(scroll, "verticalNormalizedPosition"), Is.LessThan(.999f));
        scrollbar.GetType().GetProperty("value").SetValue(scrollbar, .25f);
        yield return null;
        Assert.That((float)Property(scroll, "verticalNormalizedPosition"), Is.EqualTo(.25f).Within(.02f),
            "The authored scrollbar must drive the ScrollRect normalized position.");

        Call(fixture.Detail, "ShowCharacter", entry.DisplayName, "Short story again.", null, entry.portraits, 0, true);
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;
        Assert.That(content.rect.height, Is.LessThan(viewport.rect.height - 1f));
        Assert.That(scrollbar.gameObject.activeSelf, Is.False, "Replacing a long story must hide the scrollbar again.");
        Assert.That(viewport.rect.width, Is.EqualTo(viewportWidth).Within(.1f));
        Vector2 fittedPosition = content.anchoredPosition;
        Call(scroll, "OnScroll", pointer);
        yield return null;
        Assert.That(content.anchoredPosition.y, Is.EqualTo(fittedPosition.y).Within(.1f), "Fitting content must stop accepting vertical movement.");
        Assert.That(ProgressSnapshot(), Is.EqualTo(progress));
    }

    [UnityTest]
    public IEnumerator LongAffectionReward_ExpandsRowAndShrinksBackWithoutClippingDescription()
    {
        fixture = new Fixture();
        Time.timeScale = 0f;
        string progress = ProgressSnapshot();
        Call(fixture.Tab, "Show", EncyclopediaMainTab.Boss);
        yield return null;
        Component scroll = Field<Component>(fixture.Detail, "characterScrollRect");
        Component scrollbar = (Component)Property(scroll, "verticalScrollbar");
        Assert.That(scrollbar, Is.Not.Null);
        ((RectTransform)scroll.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 520f);
        RectTransform viewport = (RectTransform)Property(scroll, "viewport");
        RectTransform content = (RectTransform)Property(scroll, "content");
        Component row = (Component)Field<Array>(fixture.Detail, "rewardSlots").GetValue(0);
        Component description = Field<Component>(row, "descriptionText");
        RectTransform descriptionRect = (RectTransform)description.transform;
        RectTransform rowRect = (RectTransform)row.transform;
        Component rowLayout = row.GetComponent(UiEngineType("LayoutElement"));
        Assert.That((float)Property(rowLayout, "minHeight"), Is.EqualTo(104f));
        Assert.That((float)Property(rowLayout, "preferredHeight"), Is.EqualTo(-1f), "A fixed preferred height must not override the reward presenter.");
        var npc = ScriptableObject.CreateInstance<NPCData>();
        var effect = ScriptableObject.CreateInstance<InfoOnlyAffectionEffect>();
        try
        {
            npc.name = "EncyclopediaRewardScrollbarTestNpc";
            effect.name = "EncyclopediaRewardScrollbarTestEffect";
            npc.affectionRewards = new List<AffectionReward> { new AffectionReward { targetLevel = 5, effect = effect } };
            EncyclopediaBossEntry entry = LoadCatalog().BossEntries[0];
            effect.rewardText = "Short reward.";
            Call(fixture.Detail, "ShowCharacter", entry.DisplayName, "Short story.", npc, entry.portraits, 0, true);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            float shortRowHeight = rowRect.rect.height;
            float viewportWidth = viewport.rect.width;
            Assert.That(shortRowHeight, Is.GreaterThanOrEqualTo(104f - .1f));
            Assert.That(content.rect.height, Is.LessThan(viewport.rect.height - 1f));
            Assert.That(scrollbar.gameObject.activeSelf, Is.False);

            effect.rewardText = string.Join("\n", Enumerable.Range(1, 28).Select(i => "Reward description line " + i + "."));
            Call(fixture.Detail, "ShowCharacter", entry.DisplayName, "Short story.", npc, entry.portraits, 0, true);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            float preferredTextHeight = (float)Property(description, "preferredHeight");
            float verticalPadding = descriptionRect.offsetMin.y - descriptionRect.offsetMax.y;
            Assert.That(rowRect.rect.height, Is.GreaterThan(shortRowHeight + 100f));
            Assert.That(descriptionRect.rect.height, Is.GreaterThanOrEqualTo(preferredTextHeight - 1f), "The reward's actual text box must fit its preferred height.");
            Assert.That(rowRect.rect.height, Is.GreaterThanOrEqualTo(preferredTextHeight + verticalPadding - 1f));
            Assert.That(content.rect.height, Is.GreaterThan(viewport.rect.height + 1f));
            Assert.That(scrollbar.gameObject.activeInHierarchy, Is.True);
            Assert.That(viewport.rect.width, Is.EqualTo(viewportWidth).Within(.1f));
            scrollbar.GetType().GetProperty("value").SetValue(scrollbar, 0f);
            yield return null;
            Assert.That((float)Property(scroll, "verticalNormalizedPosition"), Is.EqualTo(0f).Within(.02f), "The last reward lines must be reachable with the native scrollbar.");

            effect.rewardText = "Short reward again.";
            Call(fixture.Detail, "ShowCharacter", entry.DisplayName, "Short story.", npc, entry.portraits, 0, true);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(rowRect.rect.height, Is.EqualTo(shortRowHeight).Within(1f));
            Assert.That(rowRect.rect.height, Is.GreaterThanOrEqualTo(104f - .1f));
            Assert.That(content.rect.height, Is.LessThan(viewport.rect.height - 1f));
            Assert.That(scrollbar.gameObject.activeSelf, Is.False, "Shrinking a reward must clear the old overflow state.");
            Assert.That(viewport.rect.width, Is.EqualTo(viewportWidth).Within(.1f));
            Assert.That(ProgressSnapshot(), Is.EqualTo(progress));
        }
        finally
        {
            Object.DestroyImmediate(npc);
            Object.DestroyImmediate(effect);
        }
    }

    [Serializable]
    private sealed class NativeRevealSnapshot
    {
        public string phase;
        public float sampleTime;
        public bool bookFrameVisible, itemRootVisible, creaturePagesVisible, coverVisible;
        public int coverIndex, creatureLeftIndex, creatureRightIndex, fullViewIndex;
    }

    private void AssertLiveItemVisibility(bool visible)
    {
        Assert.That(fixture.ItemTab, Is.Not.Null, "This regression must keep the actual Item presenter.");
        Assert.That(Field<GameObject>(fixture.ItemRightPage, "pageContentRoot").activeInHierarchy, Is.EqualTo(visible),
            "Hide the complete authored Item detail panel, including its background and header.");
        Assert.That(Field<GameObject>(fixture.ItemRightPage, "contentRoot").activeInHierarchy, Is.EqualTo(visible));
        Assert.That(Field<Component>(fixture.ItemRightPage, "titleText").gameObject.activeInHierarchy, Is.EqualTo(visible));
        Assert.That(Field<Component>(fixture.ItemRightPage, "storyText").gameObject.activeInHierarchy, Is.EqualTo(visible));
        object hint = Field<object>(fixture.ItemRightPage, "weaponDescriptionHint");
        if (!visible)
            Assert.That(Field<GameObject>(hint, "root").activeInHierarchy, Is.False);
        Component leftPage = Field<Component>(fixture.ItemTab, "leftPage");
        Assert.That(Field<GameObject>(leftPage, "contentRoot").activeInHierarchy, Is.EqualTo(visible));
        Assert.That(fixture.ItemRightPage.gameObject.activeInHierarchy, Is.True,
            "RightPage also owns the main-tab bookmarks and must remain active.");
        foreach (string button in new[] { "itemMainTabButton", "monsterMainTabButton", "bossMainTabButton", "npcMainTabButton" })
            Assert.That(Field<Component>(fixture.Screen, button).gameObject.activeInHierarchy, Is.True);
    }

    private static IEnumerator WaitForBookPresentation(Component book)
    {
        float deadline = Time.realtimeSinceStartup + 8f;
        while ((bool)Property(book, "IsPlaying") && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(Property(book, "IsPlaying"), Is.EqualTo(false), "The authored unscaled book transition timed out.");
    }

    private static IEnumerator WaitForLocalization()
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 30d;
        while (Time.realtimeSinceStartupAsDouble < deadline)
        {
            object operation = LocalizationType.GetProperty("InitializationOperation").GetValue(null);
            if ((bool)Property(operation, "IsDone"))
            {
                Assert.That(Property(operation, "Status").ToString(), Is.EqualTo("Succeeded"));
                yield break;
            }
            yield return null;
        }
        Assert.Fail("Localization initialization did not finish.");
    }

    // Use the installed Input System through reflection so the existing test assembly references stay unchanged.
    private sealed class ShortcutKeyboard : IDisposable
    {
        private readonly Type inputSystem = Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem", true);
        private readonly Type stateType = Type.GetType("UnityEngine.InputSystem.LowLevel.KeyboardState, Unity.InputSystem", true);
        private readonly object keyboard;
        private readonly object previousKeyboard;
        private readonly Object previousInputSettings;
        private readonly Object temporaryInputSettings;

        public ShortcutKeyboard()
        {
            Type keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem", true);
            previousKeyboard = keyboardType.GetProperty("current").GetValue(null);
            previousInputSettings = (Object)inputSystem.GetProperty("settings").GetValue(null);
            temporaryInputSettings = Object.Instantiate(previousInputSettings);
            temporaryInputSettings.hideFlags = HideFlags.HideAndDontSave;
            inputSystem.GetProperty("settings").SetValue(null, temporaryInputSettings);
            // Batch tests do not focus GameView. Keep synthetic events in player state without changing the asset.
            SetTemporaryInputSetting("backgroundBehavior", "IgnoreFocus");
            SetTemporaryInputSetting("editorInputBehaviorInPlayMode", "AllDeviceInputAlwaysGoesToGameView");
            SetTemporaryInputSetting("updateMode", "ProcessEventsManually");
            keyboard = inputSystem.GetMethod("AddDevice", new[] { typeof(string), typeof(string), typeof(string) })
                .Invoke(null, new object[] { "Keyboard", "EncyclopediaShortcutTestKeyboard", null });
            keyboard.GetType().GetMethod("MakeCurrent").Invoke(keyboard, null);
        }

        public void Set(InputContextShortcutId shortcut, bool pressed)
        {
            object[] controlArgs = { InputContextShortcutDefaultsSO.LoadOrCreate().GetKey(shortcut), null };
            Assert.That((bool)typeof(InputKeyCompatibility).GetMethod("TryGetButtonControl").Invoke(null, controlArgs), Is.True);
            object key = Property(controlArgs[1], "keyCode");
            Array keys = Array.CreateInstance(key.GetType(), pressed ? 1 : 0);
            if (pressed) keys.SetValue(key, 0);
            object state = Activator.CreateInstance(stateType, new object[] { keys });
            inputSystem.GetMethods().Single(method => method.Name == "QueueStateEvent" && method.IsGenericMethodDefinition)
                .MakeGenericMethod(stateType).Invoke(null, new[] { keyboard, state, (object)(-1d) });
            inputSystem.GetMethod("Update", Type.EmptyTypes).Invoke(null, null);
            Assert.That(Property(controlArgs[1], "isPressed"), Is.EqualTo(pressed),
                "The synthetic keyboard event must reach player input state in the headless Editor.");
            if (pressed)
                Assert.That(InputBindingService.EnsureInstance().WasPressedThisFrame(shortcut), Is.True,
                    "The regression must inject a real pressed shortcut rather than test an idle keyboard.");
        }

        private void SetTemporaryInputSetting(string name, string value)
        {
            PropertyInfo property = temporaryInputSettings.GetType().GetProperty(name);
            property.SetValue(temporaryInputSettings, Enum.Parse(property.PropertyType, value));
        }

        public void Dispose()
        {
            inputSystem.GetMethods().Single(method => method.Name == "RemoveDevice" && method.GetParameters().Length == 1)
                .Invoke(null, new[] { keyboard });
            if (previousKeyboard != null)
                previousKeyboard.GetType().GetMethod("MakeCurrent").Invoke(previousKeyboard, null);
            inputSystem.GetProperty("settings").SetValue(null, previousInputSettings);
            Object.DestroyImmediate(temporaryInputSettings);
        }
    }

    private static Type UiType(string name) => Type.GetType(name + ", UI", true);
    private static Type UiEngineType(string name) => Type.GetType("UnityEngine.UI." + name + ", UnityEngine.UI", true);
    private static Component ComponentOf(GameObject root, string name) => root.GetComponentInChildren(UiType(name), true);
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, InstanceFields).GetValue(owner);
    private static void SetField(object owner, string name, object value) => owner.GetType().GetField(name, InstanceFields).SetValue(owner, value);
    private static object Property(object owner, string name) => owner.GetType().GetProperty(name).GetValue(owner);
    private static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, InstanceFields).Invoke(owner, args);
    private static object StaticCall(Type type, string name, params object[] args) => type.GetMethod(name, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
    private static string Text(Component text) => (string)Property(text, "text");
    private static Sprite CurrentPortrait(Component detail) => (Sprite)Property(detail, "CurrentPortrait");
    private static void Click(Component button) => ((UnityEvent)Property(button, "onClick")).Invoke();
    private static int ActiveSlots(Component tab, string name) => Field<Array>(tab, name).Cast<Component>().Count(slot => slot.gameObject.activeSelf);
    private static EncyclopediaCatalogSO LoadCatalog() => UnityEditor.AssetDatabase.LoadAssetAtPath<EncyclopediaCatalogSO>(CatalogPath);
    private static string ProgressSnapshot() => JsonUtility.ToJson(GameDataStore.Data?.affectionData) + "|" + JsonUtility.ToJson(GameDataStore.Data?.monsterEncyclopediaData);

    private sealed class Fixture : IDisposable
    {
        public readonly Component Tab, Detail, Screen, Book, ItemTab, ItemRightPage;
        private readonly GameObject staging;
        private readonly Camera camera;
        private readonly Canvas canvas;
        private readonly InputBindingService[] inputServicesBefore = Object.FindObjectsByType<InputBindingService>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        public Fixture(bool keepScreen = false, bool keepBook = false, bool keepItemPresenters = false)
        {
            staging = new GameObject("EncyclopediaCreatureTestStaging");
            staging.SetActive(false);
            var canvasObject = new GameObject("ReviewCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(staging.transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            var cameraObject = new GameObject("ReviewCamera", typeof(Camera));
            cameraObject.transform.SetParent(staging.transform, false);
            camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.16f, .18f, .23f, 1f);
            camera.cullingMask = 1 << 31;
            camera.orthographic = true;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 50f;
            canvas.worldCamera = camera;
            canvas.planeDistance = 2f;
            Component scaler = canvasObject.AddComponent(UiEngineType("CanvasScaler"));
            PropertyInfo scaleMode = scaler.GetType().GetProperty("uiScaleMode");
            scaleMode.SetValue(scaler, Enum.Parse(scaleMode.PropertyType, "ScaleWithScreenSize"));
            scaler.GetType().GetProperty("referenceResolution").SetValue(scaler, new Vector2(2400f, 1350f));
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            GameObject clone = Object.Instantiate(prefab, canvas.transform, false);
            Tab = ComponentOf(clone, "EncyclopediaCreatureTab");
            Detail = ComponentOf(clone, "EncyclopediaCreatureDetailView");
            Screen = keepScreen ? ComponentOf(clone, "EncyclopediaScreen") : null;
            Book = keepBook ? ComponentOf(clone, "EncyclopediaBookPresentation") : null;
            ItemTab = keepItemPresenters ? ComponentOf(clone, "EncyclopediaItemTab") : null;
            ItemRightPage = keepItemPresenters ? ComponentOf(clone, "EncyclopediaItemRightPage") : null;
            Assert.That(Tab, Is.Not.Null, "Author the approved native creature pages before running these tests.");
            Assert.That(Detail, Is.Not.Null);
            if (keepScreen)
                foreach (string reference in new[] { "bookPresentation", "revealPresentation", "rootSlideFadePresentation", "itemTab" })
                    if (!(keepBook && reference == "bookPresentation") &&
                        !(keepItemPresenters && reference == "itemTab"))
                        SetField(Screen, reference, null);
            foreach (MonoBehaviour behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                string type = behaviour.GetType().Name;
                string assembly = behaviour.GetType().Assembly.GetName().Name;
                bool allowed = assembly == "UnityEngine.UI" || assembly == "Unity.TextMeshPro" ||
                    type == "EncyclopediaCreatureTab" || type == "EncyclopediaCreatureDetailView" ||
                    type == "EncyclopediaCharacterEntryView" || type == "EncyclopediaAffectionRewardView" ||
                    type == "EncyclopediaGradientGraphic" || (keepScreen && type == "EncyclopediaScreen") ||
                    (keepBook && type == "EncyclopediaBookPresentation") ||
                    (keepItemPresenters && (type == "EncyclopediaItemTab" || type == "EncyclopediaItemLeftPage" ||
                        type == "EncyclopediaItemRightPage" || type == "EncyclopediaEntryGridView" ||
                        type == "EncyclopediaEntryButton" || type == "WeaponAbilityBlockView" ||
                        type == "LocalizeTmpFontEvent"));
                if (!allowed) Object.DestroyImmediate(behaviour);
            }
            foreach (Animator animator in clone.GetComponentsInChildren<Animator>(true))
                animator.enabled = keepBook;
            if (keepScreen)
            {
                SetField(Screen, "closeOnRuntimeAwake", false);
                SetField(Screen, "playRootSlideFadePresentation", false);
                SetField(Screen, "playBookOpenOnOpen", false);
                SetField(Screen, "playBookCloseOnClose", false);
            }
            foreach (Transform node in clone.GetComponentsInChildren<Transform>(true))
            {
                node.gameObject.layer = 31;
                if (node.name == "DimPanel" || (!keepItemPresenters &&
                    (node.name == "LeftPage" || node.name == "ItemDetailPanel")))
                    node.gameObject.SetActive(false);
            }
            canvasObject.layer = 31;
            clone.SetActive(true);
            Tab.gameObject.SetActive(true);
            Tab.transform.localScale = Vector3.one;
            if (Tab.GetComponent<CanvasGroup>() is CanvasGroup group) group.alpha = 1f;
            staging.SetActive(true);
            Call(Tab, "SetCatalog", LoadCatalog());
            Call(Tab, "SetInteractionEnabled", true);
        }

        public IEnumerator WaitForNativeFonts()
        {
            Type fontBindingType = UiType("LocalizeTmpFontEvent");
            Type textType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro", true);
            double deadline = Time.realtimeSinceStartupAsDouble + 30d;
            int stableFrames = 0;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                bool ready = true;
                int activeBindings = 0;
                foreach (Component binding in staging.GetComponentsInChildren(fontBindingType, true))
                {
                    if (!((Behaviour)binding).isActiveAndEnabled) continue;
                    activeBindings++;
                    object reference = Property(binding, "AssetReference");
                    object handle = Property(reference, "CurrentLoadingOperationHandle");
                    bool valid = (bool)Call(handle, "IsValid");
                    bool finished = valid && (bool)Property(handle, "IsDone");
                    if (!finished) { ready = false; continue; }
                    Assert.That(Property(handle, "Status").ToString(), Is.EqualTo("Succeeded"));
                    ready &= ReferenceEquals(Property(handle, "Result"), Property(binding.GetComponent(textType), "font"));
                }
                Assert.That(activeBindings, Is.GreaterThan(0), "Native evidence must retain the authored font localizers.");
                stableFrames = ready ? stableFrames + 1 : 0;
                if (stableFrames >= 2) yield break;
                yield return null;
            }
            Assert.Fail("The authored native font callbacks did not settle within thirty seconds.");
        }

        public void Capture(string path)
        {
            // Item binding can instantiate authored entries and ability blocks after Fixture construction.
            foreach (Transform node in staging.GetComponentsInChildren<Transform>(true))
                node.gameObject.layer = 31;
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                pixels.Apply();
                Color32[] colors = pixels.GetPixels32();
                var samples = new HashSet<int>();
                for (int i = 0; i < colors.Length; i += 211)
                    samples.Add((colors[i].r << 16) | (colors[i].g << 8) | colors[i].b);
                Assert.That(samples.Count, Is.GreaterThan(20), "Capture must contain rendered book art and native UI, not only the camera clear color.");
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(pixels);
            }
        }

        public void Dispose()
        {
            if (staging != null) Object.DestroyImmediate(staging);
            InputBindingService currentInput = InputBindingService.Instance;
            if (currentInput != null && !inputServicesBefore.Contains(currentInput))
                Object.DestroyImmediate(currentInput.gameObject);
        }
    }
}
#endif
