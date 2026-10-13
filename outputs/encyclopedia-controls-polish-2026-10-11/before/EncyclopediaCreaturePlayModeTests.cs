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
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private Fixture fixture;
    private float originalTimeScale;

    [SetUp]
    public void SetUp() => originalTimeScale = Time.timeScale;

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = originalTimeScale;
        fixture?.Dispose();
        fixture = null;
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
        Click(Field<Component>(fixture.Tab, "nextPageButton"));
        Assert.That(ActiveSlots(fixture.Tab, "characterSlots"), Is.EqualTo(1));
        Assert.That(Text(Field<Component>(fixture.Tab, "pageText")), Is.EqualTo("2 / 2"));
        Assert.That(Text(Field<Component>(fixture.Detail, "characterTitleText")), Is.EqualTo(LoadCatalog().NpcEntries[4].DisplayName));
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
        Assert.That(CurrentPortrait(fixture.Detail), Is.SameAs(melta.portraits[0].sprite));
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
        public readonly Component Tab, Detail, Screen, Book;
        private readonly GameObject staging;
        private readonly Camera camera;
        private readonly Canvas canvas;

        public Fixture(bool keepScreen = false, bool keepBook = false)
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
            Assert.That(Tab, Is.Not.Null, "Author the approved native creature pages before running these tests.");
            Assert.That(Detail, Is.Not.Null);
            if (keepScreen)
                foreach (string reference in new[] { "bookPresentation", "revealPresentation", "rootSlideFadePresentation", "itemTab" })
                    if (!keepBook || reference != "bookPresentation")
                        SetField(Screen, reference, null);
            foreach (MonoBehaviour behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                string type = behaviour.GetType().Name;
                string assembly = behaviour.GetType().Assembly.GetName().Name;
                bool allowed = assembly == "UnityEngine.UI" || assembly == "Unity.TextMeshPro" ||
                    type == "EncyclopediaCreatureTab" || type == "EncyclopediaCreatureDetailView" ||
                    type == "EncyclopediaCharacterEntryView" || type == "EncyclopediaAffectionRewardView" ||
                    type == "EncyclopediaGradientGraphic" || (keepScreen && type == "EncyclopediaScreen") ||
                    (keepBook && type == "EncyclopediaBookPresentation");
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
                if (node.name == "LeftPage" || node.name == "ItemDetailPanel" || node.name == "DimPanel")
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

        public void Capture(string path)
        {
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
        }
    }
}
#endif
