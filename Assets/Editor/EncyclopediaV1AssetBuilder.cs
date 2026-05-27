#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class EncyclopediaV1AssetBuilder
{
    private const string CatalogPath = "Assets/LeeJunMo/Datas/Encyclopedia/EncyclopediaCatalog.asset";
    private const string ScreenPrefabPath = "Assets/LeeJunMo/Prefab/UI/PopupUI/Encyclopedia/EncyclopediaScreen.prefab";
    private const string EntrySlotPrefabPath = "Assets/LeeJunMo/Prefab/UI/PopupUI/Encyclopedia/EncyclopediaEntrySlot.prefab";
    private const string AbilityBlockPrefabPath = "Assets/LeeJunMo/Prefab/UI/PopupUI/Encyclopedia/Panel_AbilityBlock_Encyclopedia.prefab";
    private const string StandPrefabPath = "Assets/LeeJunMo/Prefab/Interactables/EncyclopediaStand.prefab";
    private const string ItemDatabasePath = "Assets/LeeJunMo/Datas/Looting/ItemDatabase.asset";
    private const string PaperBookSpriteRoot = "Assets/Sprites/UI/Encyclopedia/Updated_Paper_Book/Sprites";
    private const string ContentRoot = PaperBookSpriteRoot + "/Content";
    private const string InventoryBookRoot = PaperBookSpriteRoot + "/Inventory Book";
    private const string EarthTomeRoot = "Assets/Sprites/UI/Encyclopedia/EarthTome";
    private const string AnimationRoot = "Assets/LeeJunMo/Animations/Encyclopedia";
    private const string UiBookAnimationRoot = AnimationRoot + "/UIBook";
    private const string EarthTomeAnimationRoot = AnimationRoot + "/EarthTome";
    private const string EntrySlotAnimationRoot = AnimationRoot + "/EntrySlot";
    private const string UiBookControllerPath = UiBookAnimationRoot + "/AC_Encyclopedia_UIBook.controller";
    private const string PageCoverControllerPath = UiBookAnimationRoot + "/AC_Encyclopedia_PageCover.controller";
    private const string EarthTomeControllerPath = EarthTomeAnimationRoot + "/AC_Encyclopedia_EarthTome.controller";
    private const string EntrySlotControllerPath = EntrySlotAnimationRoot + "/AC_Encyclopedia_EntrySlot.controller";
    private const string GalmuriFontPath = "Assets/Font/Galmuri9 SDF.asset";

    [MenuItem("Tools/Encyclopedia/Rebuild V1 Assets")]
    public static void RebuildV1Assets()
    {
        if (!EditorUtility.DisplayDialog(
                "Rebuild Generated Encyclopedia Assets",
                "This rebuilds generated shell assets and is not the safe path for the current authored GlobalUIRoot layout. Use Tools/Encyclopedia/Wire Existing GlobalUIRoot Encyclopedia for the current layout.",
                "Rebuild Generated Assets",
                "Cancel"))
        {
            return;
        }

        EnsureParentDirectory(CatalogPath);
        EnsureParentDirectory(ScreenPrefabPath);
        EnsureParentDirectory(EntrySlotPrefabPath);
        EnsureParentDirectory(StandPrefabPath);
        EnsureParentDirectory(UiBookControllerPath);
        EnsureParentDirectory(EarthTomeControllerPath);
        EnsureParentDirectory(EntrySlotControllerPath);

        ItemDatabase itemDatabase = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
        EncyclopediaCatalogSO catalog = BuildCatalog();
        EncyclopediaEntryButton entrySlotPrefab = BuildEntrySlotPrefab();
        BuildScreenPrefab(itemDatabase, entrySlotPrefab);
        BuildStandPrefab(catalog, itemDatabase);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[EncyclopediaV1AssetBuilder] Rebuilt catalog, screen prefab, and stand prefab.");
    }

    private static EncyclopediaCatalogSO BuildCatalog()
    {
        EncyclopediaCatalogSO catalog = AssetDatabase.LoadAssetAtPath<EncyclopediaCatalogSO>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<EncyclopediaCatalogSO>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.SetEntries(BuildWeaponEntries(), BuildMonsterEntries(), BuildBossEntries());
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static List<EncyclopediaWeaponEntry> BuildWeaponEntries()
    {
        var entries = new List<EncyclopediaWeaponEntry>();
        ItemDatabase database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
        if (database == null || database.allWeapons == null)
            return entries;

        for (int i = 0; i < database.allWeapons.Count; i++)
        {
            WeaponDefinition weapon = database.allWeapons[i];
            if (weapon == null)
                continue;

            entries.Add(new EncyclopediaWeaponEntry
            {
                weapon = weapon,
                stageText = "무기 데이터베이스"
            });
        }

        return entries;
    }

    private static List<EncyclopediaMonsterEntry> BuildMonsterEntries()
    {
        var entries = new List<EncyclopediaMonsterEntry>();
        MonsterSeed[] seeds =
        {
            new("monster.beer", "Beer Monster", "주점 몬스터", "근접 추격", "드래곤 루트", "술기운에 반응하는 하급 몬스터입니다.", "Assets/Prefabs/Enemies/Mobs/BeerMonster.prefab"),
            new("monster.treasure", "Treasure Monster", "보물 몬스터", "접근 압박", "공용", "보물처럼 보이지만 전투를 유도하는 몬스터입니다.", "Assets/Prefabs/Enemies/Mobs/TreasureMonster.prefab"),
            new("monster.frog", "Frog", "일반 몬스터", "도약 공격", "초원/습지 계열", "빠르게 거리를 좁히는 개구리형 몬스터입니다.", "Assets/Prefabs/Enemies/Mobs/Frog.prefab"),
            new("monster.frog_elite", "Frog BOSS", "정예 몬스터", "강화 도약 공격", "초원/습지 계열", "일반 개구리보다 강한 정예 개체입니다.", "Assets/Prefabs/Enemies/Mobs/Frog_BOSS.prefab"),
            new("monster.slime_pawn", "Pawn", "슬라임 병사", "직선 압박", "슬라임 복도", "앞을 향해 전진하는 슬라임 체스 병사입니다.", "Assets/Prefabs/Enemies/Mobs/SlimeCorridor/Pawn.prefab"),
            new("monster.slime_rook", "Rook", "슬라임 병사", "직선 돌진", "슬라임 복도", "묵직하게 전열을 밀어내는 슬라임입니다.", "Assets/Prefabs/Enemies/Mobs/SlimeCorridor/Rook.prefab"),
            new("monster.slime_knight", "Knight", "슬라임 병사", "변칙 이동", "슬라임 복도", "예측하기 어려운 이동으로 접근하는 슬라임입니다.", "Assets/Prefabs/Enemies/Mobs/SlimeCorridor/Knight.prefab"),
            new("monster.slime_bishop", "Bishop", "슬라임 병사", "사선 압박", "슬라임 복도", "사선 위협을 담당하는 슬라임입니다.", "Assets/Prefabs/Enemies/Mobs/SlimeCorridor/Bishop.prefab"),
            new("monster.slime_wizard", "Wizard", "슬라임 병사", "원거리 시전", "슬라임 복도", "마법 공격을 사용하는 슬라임입니다.", "Assets/Prefabs/Enemies/Mobs/SlimeCorridor/Wizard.prefab"),
            new("monster.candlestick_corridor", "Corridor Candlestick", "그림자 몬스터", "고정형 위협", "그림자 복도", "어둠 속 시야와 위치를 압박하는 촛대 몬스터입니다.", "Assets/Prefabs/Enemies/Mobs/ShadowCorridor/CorridorCandlestickMonster.prefab"),
            new("monster.deads_skeleton", "Dead's Skeleton", "그림자 몬스터", "근접 공격", "그림자 복도", "죽은 자의 흔적을 품은 몬스터입니다.", "Assets/Prefabs/Enemies/Mobs/ShadowCorridor/Dead'sSkeleton.prefab"),
            new("monster.shadow", "Shadow Monster", "그림자 몬스터", "기습 공격", "그림자 복도", "어둠과 함께 움직이는 그림자 몬스터입니다.", "Assets/Prefabs/Enemies/Mobs/ShadowCorridor/ShadowMonster.prefab"),
            new("monster.shadow_servant", "Shadow Servant", "그림자 하수인", "시야 교란", "그림자 복도", "날개와 검은 외장을 지닌 그림자 하수인입니다.", "Assets/Prefabs/Enemies/Mobs/ShadowCorridor/ShadowServant/ShadowServant.prefab"),
            new("monster.strange_candlestick", "Strange Candlestick", "그림자 오브젝트 몬스터", "봉인/보조 패턴", "그림자 복도", "전투 흐름과 연결되는 기묘한 촛대입니다.", "Assets/Prefabs/Enemies/Mobs/ShadowCorridor/StrangeCandlestick/StrangeCandlestick.prefab")
        };

        for (int i = 0; i < seeds.Length; i++)
        {
            MonsterSeed seed = seeds[i];
            GameObject prefab = LoadAsset<GameObject>(seed.PrefabPath);
            entries.Add(new EncyclopediaMonsterEntry
            {
                id = seed.Id,
                displayName = ResolveEnemyName(prefab, seed.DisplayName),
                image = ResolvePreviewSprite(prefab),
                type = seed.Type,
                attackStyle = seed.AttackStyle,
                stageText = seed.StageText,
                storyText = seed.StoryText,
                sourcePrefab = prefab
            });
        }

        return entries;
    }

    private static List<EncyclopediaBossEntry> BuildBossEntries()
    {
        var entries = new List<EncyclopediaBossEntry>();
        BossSeed[] seeds =
        {
            new("boss.slime_queen", "Slime Queen", "보스", "점프/낙성 패턴/소환", "슬라임의 방", "슬라임 루트의 여왕 보스입니다.", "Assets/Prefabs/Enemies/Bosses/SlimeQueen/SlimeQueen.prefab", null),
            new("boss.witch_chloe", "클로에", "보스", "투사체/시야 교란/광역 마법", "클로에의 방", "그림자 복도 중심에 있는 마녀 보스입니다.", "Assets/Prefabs/Enemies/Bosses/Witch/Witch.prefab", "Assets/LeeJunMo/Datas/Dialogue/NPC/MSBossNpc.asset"),
            new("boss.dragon", "Dragon", "보스", "돌진/화염/광역 사격", "드래곤의 방", "술기운을 머금은 드래곤 보스입니다.", "Assets/Prefabs/Enemies/Bosses/DragonBoss/DragonBoss.prefab", "Assets/LeeJunMo/Datas/Dialogue/NPC/DragonBossNpc.asset"),
            new("boss.demon_king", "마왕", "보스", "검격/사선/광역 패턴", "마왕의 방", "최종 루트의 마왕 보스입니다.", null, "Assets/LeeJunMo/Datas/Dialogue/NPC/DarkLordNpcData.asset")
        };

        for (int i = 0; i < seeds.Length; i++)
        {
            BossSeed seed = seeds[i];
            GameObject prefab = LoadAsset<GameObject>(seed.PrefabPath);
            NPCData npcData = LoadAsset<NPCData>(seed.NpcDataPath);
            entries.Add(new EncyclopediaBossEntry
            {
                id = seed.Id,
                displayName = npcData != null && !string.IsNullOrWhiteSpace(npcData.npcName) ? npcData.npcName : seed.DisplayName,
                image = ResolvePreviewSprite(prefab),
                type = seed.Type,
                attackStyle = seed.AttackStyle,
                stageText = seed.StageText,
                storyText = seed.StoryText,
                sourcePrefab = prefab,
                npcData = npcData
            });
        }

        return entries;
    }

    private static void BuildScreenPrefab(ItemDatabase itemDatabase, EncyclopediaEntryButton entrySlotPrefab)
    {
        GameObject root = CreateUIObject("EncyclopediaScreen", null);
        root.SetActive(false);
        var canvasGroup = root.AddComponent<CanvasGroup>();
        Image dimImage = root.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.56f);
        var slideFade = root.AddComponent<UISlideFadePresentation>();
        var screen = root.AddComponent<EncyclopediaScreen>();
        var itemTab = root.AddComponent<EncyclopediaItemTab>();
        SetStretch(root.GetComponent<RectTransform>());

        Sprite bookIdleSprite = LoadSprite($"{InventoryBookRoot}/Book Idle/1.png");
        Sprite[] bookOpenFrames = LoadSequentialSprites($"{InventoryBookRoot}/Book Open and Close/Style 1/Open", 5);
        Sprite[] bookCloseFrames = LoadSequentialSprites($"{InventoryBookRoot}/Book Open and Close/Style 1/Close", 5);
        Sprite[] contentAppearFrames = LoadSequentialSprites($"{InventoryBookRoot}/Book Content Appear/Style 1", 36);
        Sprite bookClosedSprite = FirstFrame(bookOpenFrames) ?? bookIdleSprite;
        Sprite bookOpenedSprite = bookIdleSprite != null ? bookIdleSprite : LastFrame(bookOpenFrames);
        AnimationClip bookClosedClip = BuildImageSpriteClip(
            $"{UiBookAnimationRoot}/ENC_UIBook_Closed.anim",
            "ENC_UIBook_Closed",
            SingleFrame(bookClosedSprite),
            1f / 60f,
            loop: false);
        AnimationClip bookOpenedClip = BuildImageSpriteClip(
            $"{UiBookAnimationRoot}/ENC_UIBook_Opened.anim",
            "ENC_UIBook_Opened",
            SingleFrame(bookOpenedSprite),
            1f / 60f,
            loop: false);
        AnimationClip bookOpenClip = BuildImageSpriteClip(
            $"{UiBookAnimationRoot}/ENC_UIBook_Open.anim",
            "ENC_UIBook_Open",
            bookOpenFrames,
            0.16f,
            loop: false);
        AnimationClip bookCloseClip = BuildImageSpriteClip(
            $"{UiBookAnimationRoot}/ENC_UIBook_Close.anim",
            "ENC_UIBook_Close",
            bookCloseFrames,
            0.12f,
            loop: false);
        AnimationClip pageContentAppearClip = BuildImageSpriteClip(
            $"{UiBookAnimationRoot}/ENC_UIBook_ContentAppear.anim",
            "ENC_UIBook_ContentAppear",
            contentAppearFrames,
            0.18f,
            loop: false);
        AnimatorController bookController = BuildAnimatorController(
            UiBookControllerPath,
            new[]
            {
                new AnimatorStateSpec("Closed", bookClosedClip, isDefault: true),
                new AnimatorStateSpec("Opened", bookOpenedClip, isDefault: false),
                new AnimatorStateSpec("Open", bookOpenClip, isDefault: false),
                new AnimatorStateSpec("Close", bookCloseClip, isDefault: false)
            });
        AnimatorController pageCoverController = BuildAnimatorController(
            PageCoverControllerPath,
            new[]
            {
                new AnimatorStateSpec("ContentAppear", pageContentAppearClip, isDefault: true)
            });
        Sprite tabSprite = LoadSprite($"{InventoryBookRoot}/Book Side Tabs/Tabs/Without icons/1.png")
            ?? LoadSprite($"{ContentRoot}/8 Side tabs/1.png");

        Image bookFrameImage = CreateImage("BookFrameImage", root.transform, Color.white);
        bookFrameImage.raycastTarget = false;
        bookFrameImage.sprite = bookClosedSprite;
        SetCentered(bookFrameImage.rectTransform, new Vector2(1040f, 836f));
        Animator bookAnimator = bookFrameImage.gameObject.AddComponent<Animator>();
        bookAnimator.runtimeAnimatorController = bookController;
        bookAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        var bookPresentation = bookFrameImage.gameObject.AddComponent<EncyclopediaBookPresentation>();

        RectTransform book = CreateUIObject("BookInterior", root.transform).GetComponent<RectTransform>();
        SetCentered(book, new Vector2(1040f, 620f));
        CanvasGroup pageContentGroup = book.gameObject.AddComponent<CanvasGroup>();

        RectTransform left = CreateUIObject("LeftPage", book).GetComponent<RectTransform>();
        SetAnchor(left, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(24f, 24f), new Vector2(340f, -24f));

        RectTransform detail = CreateUIObject("RightPage", book).GetComponent<RectTransform>();
        SetAnchor(detail, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(364f, 24f), new Vector2(-24f, -24f));

        Button closeButton = CreateButton("CloseButton", book, "X", new Vector2(0.5f, 0.5f));
        SetAnchor(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-58f, -50f), new Vector2(-18f, -10f));

        Button weaponTab = CreateButton("WeaponTab", left, "무기", new Vector2(0.5f, 0.5f), tabSprite);
        Button relicTab = CreateButton("RelicTab", left, "유물", new Vector2(0.5f, 0.5f), tabSprite);
        Button consumableTab = CreateButton("ConsumableTab", left, "소모품", new Vector2(0.5f, 0.5f), tabSprite);
        SetAnchor(weaponTab.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -46f), new Vector2(104f, -8f));
        SetAnchor(relicTab.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(112f, -46f), new Vector2(206f, -8f));
        SetAnchor(consumableTab.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(214f, -46f), new Vector2(308f, -8f));

        GameObject weaponMarker = CreateMarker("SelectedMarker", weaponTab.transform);
        GameObject relicMarker = CreateMarker("SelectedMarker", relicTab.transform);
        GameObject consumableMarker = CreateMarker("SelectedMarker", consumableTab.transform);
        var itemLeftPage = left.gameObject.AddComponent<EncyclopediaItemLeftPage>();

        RectTransform titleGroup = CreateUIObject("TitleGroup", left).GetComponent<RectTransform>();
        SetAnchor(titleGroup, new Vector2(0f, 1f), new Vector2(0.62f, 1f), new Vector2(12f, -82f), new Vector2(-8f, -52f));
        Image leftTitleIcon = CreateImage("TitleIcon", titleGroup, Color.white);
        leftTitleIcon.raycastTarget = false;
        leftTitleIcon.enabled = false;
        SetAnchor(leftTitleIcon.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 2f), new Vector2(26f, -2f));
        TMP_Text leftTitleText = CreateText("TitleText", titleGroup, "무기", 22f, TextAlignmentOptions.Left);
        SetAnchor(leftTitleText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(32f, 0f), Vector2.zero);

        TMP_Text entryCountText = CreateText("EntryCountText", left, "0/0", 18f, TextAlignmentOptions.Right);
        SetAnchor(entryCountText.rectTransform, new Vector2(0.62f, 1f), new Vector2(1f, 1f), new Vector2(8f, -78f), new Vector2(-12f, -52f));

        Button previousStepButton = CreateButton("PreviousStepButton", left, "<", new Vector2(0.5f, 0.5f), LoadSprite($"{ContentRoot}/4 Buttons/1.png"));
        Button nextStepButton = CreateButton("NextStepButton", left, ">", new Vector2(0.5f, 0.5f), LoadSprite($"{ContentRoot}/4 Buttons/1.png"));
        TMP_Text pageText = CreateText("PageText", left, "0/0", 16f, TextAlignmentOptions.Center);
        SetAnchor(previousStepButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 12f), new Vector2(58f, 46f));
        SetAnchor(nextStepButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-58f, 12f), new Vector2(-16f, 46f));
        SetAnchor(pageText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(70f, 12f), new Vector2(-70f, 46f));

        TMP_Text listNoticeText = CreateText("ListNoticeText", left, string.Empty, 17f, TextAlignmentOptions.Center);
        SetAnchor(listNoticeText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 48f), new Vector2(-12f, 78f));

        RectTransform listViewport = CreateTransparentRaycastPanel("EntryListViewport", left);
        SetAnchor(listViewport, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 84f), new Vector2(-10f, -84f));
        listViewport.gameObject.AddComponent<RectMask2D>();

        RectTransform listContent = CreateUIObject("EntryGridRoot", listViewport).GetComponent<RectTransform>();
        listContent.anchorMin = new Vector2(0.5f, 0.5f);
        listContent.anchorMax = new Vector2(0.5f, 0.5f);
        listContent.pivot = new Vector2(0.5f, 0.5f);
        listContent.anchoredPosition = Vector2.zero;
        listContent.sizeDelta = new Vector2(244f, 244f);
        var layout = listContent.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(56f, 56f);
        layout.spacing = new Vector2(4f, 4f);
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 4;
        var entryGridView = listContent.gameObject.AddComponent<EncyclopediaEntryGridView>();

        Image detailImage = CreateImage("DetailImage", detail, new Color(1f, 1f, 1f, 1f));
        SetAnchor(detailImage.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -186f), new Vector2(194f, -22f));

        TMP_Text titleText = CreateText("TitleText", detail, "Title", 32f, TextAlignmentOptions.Left);
        SetAnchor(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(214f, -60f), new Vector2(-20f, -18f));
        TMP_Text categoryText = CreateText("CategoryText", detail, "Category", 18f, TextAlignmentOptions.Left);
        SetAnchor(categoryText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(214f, -90f), new Vector2(-20f, -62f));
        TMP_Text typeText = CreateText("TypeText", detail, string.Empty, 18f, TextAlignmentOptions.Left);
        SetAnchor(typeText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(214f, -124f), new Vector2(-20f, -94f));
        TMP_Text attackStyleText = CreateText("AttackStyleText", detail, string.Empty, 18f, TextAlignmentOptions.Left);
        SetAnchor(attackStyleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(214f, -154f), new Vector2(-20f, -124f));
        TMP_Text stageText = CreateText("StageText", detail, string.Empty, 18f, TextAlignmentOptions.Left);
        SetAnchor(stageText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(214f, -184f), new Vector2(-20f, -154f));

        TMP_Text storyText = CreateText("StoryText", detail, string.Empty, 20f, TextAlignmentOptions.TopLeft);
        SetAnchor(storyText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(22f, 250f), new Vector2(-22f, -206f));

        RectTransform weaponRoot = CreatePanel("WeaponDetailRoot", detail, new Color(0f, 0f, 0f, 0f));
        SetAnchor(weaponRoot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(22f, 24f), new Vector2(-22f, 238f));
        TMP_Text weaponStatsText = CreateText("WeaponStatsText", weaponRoot, string.Empty, 18f, TextAlignmentOptions.TopLeft);
        SetAnchor(weaponStatsText.rectTransform, new Vector2(0f, 0f), new Vector2(0.46f, 1f), Vector2.zero, new Vector2(-8f, 0f));
        RectTransform abilityContainer = CreateUIObject("AbilityContainer", weaponRoot).GetComponent<RectTransform>();
        SetAnchor(abilityContainer, new Vector2(0.46f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), Vector2.zero);
        var abilityLayout = abilityContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        abilityLayout.childControlWidth = true;
        abilityLayout.childControlHeight = true;
        abilityLayout.childForceExpandWidth = true;
        abilityLayout.childForceExpandHeight = false;
        abilityLayout.spacing = 8f;
        WeaponAbilityBlockView abilityBlockPrefab = LoadAbilityBlockPrefab();

        RectTransform bossRoot = CreatePanel("BossAffectionRoot", detail, new Color(0f, 0f, 0f, 0f));
        SetAnchor(bossRoot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(22f, 24f), new Vector2(-22f, 168f));
        TMP_Text bossAffectionText = CreateText("BossAffectionText", bossRoot, string.Empty, 18f, TextAlignmentOptions.TopLeft);
        SetAnchor(bossAffectionText.rectTransform, new Vector2(0f, 0.68f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        TMP_Text bossRewardText = CreateText("BossRewardText", bossRoot, string.Empty, 17f, TextAlignmentOptions.TopLeft);
        SetAnchor(bossRewardText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.68f), Vector2.zero, Vector2.zero);
        var itemRightPage = detail.gameObject.AddComponent<EncyclopediaItemRightPage>();

        Image pageCover = CreateImage("PageContentAppearOverlay", root.transform, Color.white);
        pageCover.raycastTarget = false;
        pageCover.sprite = FirstFrame(contentAppearFrames);
        SetCentered(pageCover.rectTransform, new Vector2(1040f, 836f));
        pageCover.enabled = false;
        Animator pageCoverAnimator = pageCover.gameObject.AddComponent<Animator>();
        pageCoverAnimator.runtimeAnimatorController = pageCoverController;
        pageCoverAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;

        Image overlay = CreateImage("RevealOverlay", book, new Color(0.04f, 0.035f, 0.08f, 1f));
        SetStretch(overlay.rectTransform);
        var reveal = overlay.gameObject.AddComponent<BookPixelRevealPresentation>();

        SerializedObject screenSo = new SerializedObject(screen);
        SetObject(screenSo, "screenActiveRoot", root);
        SetObject(screenSo, "canvasGroup", canvasGroup);
        SetObject(screenSo, "closeButton", closeButton);
        SetObject(screenSo, "itemTab", itemTab);
        SetObject(screenSo, "revealPresentation", reveal);
        SetObject(screenSo, "bookPresentation", bookPresentation);
        SetObject(screenSo, "rootSlideFadePresentation", slideFade);
        SetBool(screenSo, "closeOnRuntimeAwake", true);
        screenSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject itemTabSo = new SerializedObject(itemTab);
        SetObject(itemTabSo, "itemDatabase", itemDatabase);
        SetObject(itemTabSo, "leftPage", itemLeftPage);
        SetObject(itemTabSo, "rightPage", itemRightPage);
        SetObject(itemTabSo, "bookPresentation", bookPresentation);
        itemTabSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject itemLeftSo = new SerializedObject(itemLeftPage);
        SetObject(itemLeftSo, "titleText", leftTitleText);
        SetObject(itemLeftSo, "titleIcon", leftTitleIcon);
        SetObject(itemLeftSo, "weaponButton", weaponTab);
        SetObject(itemLeftSo, "relicButton", relicTab);
        SetObject(itemLeftSo, "consumableButton", consumableTab);
        SetObject(itemLeftSo, "weaponSelectedMarker", weaponMarker);
        SetObject(itemLeftSo, "relicSelectedMarker", relicMarker);
        SetObject(itemLeftSo, "consumableSelectedMarker", consumableMarker);
        SetObject(itemLeftSo, "entryGridView", entryGridView);
        SetObject(itemLeftSo, "previousPageButton", previousStepButton);
        SetObject(itemLeftSo, "nextPageButton", nextStepButton);
        SetObject(itemLeftSo, "pageText", pageText);
        SetObject(itemLeftSo, "entryCountText", entryCountText);
        SetObject(itemLeftSo, "noticeText", listNoticeText);
        itemLeftSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject gridSo = new SerializedObject(entryGridView);
        SetObject(gridSo, "entryGridRoot", listContent);
        SetObject(gridSo, "entrySlotPrefab", entrySlotPrefab);
        SetInt(gridSo, "slotsPerPage", 16);
        gridSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject slideSo = new SerializedObject(slideFade);
        SetObject(slideSo, "targetRoot", root.GetComponent<RectTransform>());
        SetObject(slideSo, "canvasGroup", canvasGroup);
        slideSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject bookSo = new SerializedObject(bookPresentation);
        SetObject(bookSo, "bookFrameImage", bookFrameImage);
        SetObject(bookSo, "pageCoverImage", pageCover);
        SetObject(bookSo, "pageContentGroup", pageContentGroup);
        SetObject(bookSo, "bookAnimator", bookAnimator);
        SetObject(bookSo, "pageCoverAnimator", pageCoverAnimator);
        SetObject(bookSo, "openedClip", bookOpenedClip);
        SetObject(bookSo, "closedClip", bookClosedClip);
        SetObject(bookSo, "bookOpenClip", bookOpenClip);
        SetObject(bookSo, "bookCloseClip", bookCloseClip);
        SetObject(bookSo, "contentAppearClip", pageContentAppearClip);
        bookSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject itemRightSo = new SerializedObject(itemRightPage);
        SetObject(itemRightSo, "contentRoot", detail.gameObject);
        SetObject(itemRightSo, "iconImage", detailImage);
        SetObject(itemRightSo, "titleText", titleText);
        SetObject(itemRightSo, "subtitleText", categoryText);
        SetObject(itemRightSo, "storyText", storyText);
        SetObject(itemRightSo, "weaponStatsRoot", weaponRoot.gameObject);
        SetObject(itemRightSo, "weaponStatsText", weaponStatsText);
        SetObject(itemRightSo, "weaponAbilityRoot", weaponRoot.gameObject);
        SetObject(itemRightSo, "abilityContainer", abilityContainer);
        SetObject(itemRightSo, "abilityBlockPrefab", abilityBlockPrefab);
        itemRightSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject revealSo = new SerializedObject(reveal);
        SetObject(revealSo, "overlayGraphic", overlay);
        SetObject(revealSo, "interactionGate", canvasGroup);
        revealSo.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, ScreenPrefabPath);
        Object.DestroyImmediate(root);
    }

    private static EncyclopediaEntryButton BuildEntrySlotPrefab()
    {
        Sprite holderSprite = LoadSprite($"{ContentRoot}/5 Holders/1.png");
        Sprite selectedSprite = LoadSprite($"{ContentRoot}/6 Highlighter/1.png");
        Sprite hoverSprite = LoadSprite($"{ContentRoot}/6 Highlighter/2.png") ?? selectedSprite;
        AnimatorController slotController = BuildEntrySlotAnimatorController();

        GameObject row = CreateUIObject("EncyclopediaEntrySlot", null);
        var rowRect = row.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(56f, 56f);
        var layoutElement = row.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 56f;
        layoutElement.preferredHeight = 56f;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;

        Image background = row.AddComponent<Image>();
        background.sprite = holderSprite;
        background.color = holderSprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        background.preserveAspect = true;
        Button button = row.AddComponent<Button>();
        button.targetGraphic = background;
        Animator animator = row.AddComponent<Animator>();
        animator.runtimeAnimatorController = slotController;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        var entryButton = row.AddComponent<EncyclopediaEntryButton>();

        GameObject hoverMarker = CreateUIObject("HoverMarker", row.transform);
        Image hoverImage = hoverMarker.AddComponent<Image>();
        hoverImage.sprite = hoverSprite;
        hoverImage.color = hoverSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.32f);
        hoverImage.raycastTarget = false;
        hoverMarker.AddComponent<CanvasGroup>();
        SetStretch(hoverMarker.GetComponent<RectTransform>());
        hoverMarker.SetActive(false);

        GameObject selectedMarker = CreateUIObject("SelectedMarker", row.transform);
        Image selectedImage = selectedMarker.AddComponent<Image>();
        selectedImage.sprite = selectedSprite;
        selectedImage.color = selectedSprite != null ? Color.white : new Color(0.95f, 0.76f, 0.34f, 0.75f);
        selectedImage.raycastTarget = false;
        selectedMarker.AddComponent<CanvasGroup>();
        SetStretch(selectedMarker.GetComponent<RectTransform>());
        selectedMarker.SetActive(false);

        Image icon = CreateImage("Icon", row.transform, Color.white);
        icon.raycastTarget = false;
        SetAnchor(icon.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));

        GameObject lockedMarker = CreateUIObject("LockedMarker", row.transform);
        Image lockedImage = lockedMarker.AddComponent<Image>();
        lockedImage.color = new Color(0f, 0f, 0f, 0.55f);
        lockedImage.raycastTarget = false;
        lockedMarker.AddComponent<CanvasGroup>();
        SetStretch(lockedMarker.GetComponent<RectTransform>());
        lockedMarker.SetActive(false);

        SerializedObject so = new SerializedObject(entryButton);
        SetObject(so, "button", button);
        SetObject(so, "indexText", null);
        SetObject(so, "titleText", null);
        SetObject(so, "iconImage", icon);
        SetObject(so, "selectedMarker", selectedMarker);
        SetObject(so, "hoverMarker", hoverMarker);
        SetObject(so, "lockedMarker", lockedMarker);
        SetObject(so, "animator", animator);
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(row, EntrySlotPrefabPath);
        Object.DestroyImmediate(row);
        return savedPrefab != null ? savedPrefab.GetComponent<EncyclopediaEntryButton>() : null;
    }

    private static void BuildStandPrefab(EncyclopediaCatalogSO catalog, ItemDatabase itemDatabase)
    {
        GameObject root = new GameObject("EncyclopediaStand");
        var collider = root.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(1.2f, 1.2f);

        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        Sprite[] closedIdleFrames = LoadSpriteSheetFrames($"{EarthTomeRoot}/EarthTome_16x16_Idle_Closed.png");
        Sprite[] openedIdleFrames = LoadSpriteSheetFrames($"{EarthTomeRoot}/EarthTome_16x16_Idle_Opened.png");
        Sprite[] openFrames = LoadSpriteSheetFrames($"{EarthTomeRoot}/EarthTome_16x16_OpenBook.png");
        Sprite[] closeFrames = ReverseFrames(openFrames);
        AnimationClip closedIdleClip = BuildSpriteRendererClip(
            $"{EarthTomeAnimationRoot}/ENC_EarthTome_ClosedIdle.anim",
            "ENC_EarthTome_ClosedIdle",
            closedIdleFrames,
            Mathf.Max(1, closedIdleFrames.Length) * 0.08f,
            loop: true);
        AnimationClip openedIdleClip = BuildSpriteRendererClip(
            $"{EarthTomeAnimationRoot}/ENC_EarthTome_OpenedIdle.anim",
            "ENC_EarthTome_OpenedIdle",
            openedIdleFrames,
            Mathf.Max(1, openedIdleFrames.Length) * 0.08f,
            loop: true);
        AnimationClip openClip = BuildSpriteRendererClip(
            $"{EarthTomeAnimationRoot}/ENC_EarthTome_Open.anim",
            "ENC_EarthTome_Open",
            openFrames,
            0.18f,
            loop: false);
        AnimationClip closeClip = BuildSpriteRendererClip(
            $"{EarthTomeAnimationRoot}/ENC_EarthTome_Close.anim",
            "ENC_EarthTome_Close",
            closeFrames,
            0.14f,
            loop: false);
        AnimatorController earthTomeController = BuildAnimatorController(
            EarthTomeControllerPath,
            new[]
            {
                new AnimatorStateSpec("ClosedIdle", closedIdleClip, isDefault: true),
                new AnimatorStateSpec("OpenedIdle", openedIdleClip, isDefault: false),
                new AnimatorStateSpec("Open", openClip, isDefault: false),
                new AnimatorStateSpec("Close", closeClip, isDefault: false)
            });
        renderer.sprite = FirstFrame(closedIdleFrames) ?? AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        renderer.color = new Color(0.48f, 0.38f, 0.24f, 1f);

        Animator animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = earthTomeController;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        var bookPresentation = root.AddComponent<BookWorldSpriteSequencePresentation>();
        var interactable = root.AddComponent<EncyclopediaInteractable>();
        Transform promptAnchor = new GameObject("PromptAnchor").transform;
        promptAnchor.SetParent(root.transform, false);
        promptAnchor.localPosition = new Vector3(0f, 0.9f, 0f);

        SerializedObject bookSo = new SerializedObject(bookPresentation);
        SetObject(bookSo, "targetRenderer", renderer);
        SetObject(bookSo, "animator", animator);
        SetObject(bookSo, "closedIdleClip", closedIdleClip);
        SetObject(bookSo, "openedIdleClip", openedIdleClip);
        SetObject(bookSo, "openClip", openClip);
        SetObject(bookSo, "closeClip", closeClip);
        bookSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject so = new SerializedObject(interactable);
        SetObject(so, "itemDatabase", itemDatabase);
        SetObject(so, "catalog", catalog);
        SetObject(so, "promptAnchor", promptAnchor);
        SetObject(so, "spriteRenderer", renderer);
        SetObject(so, "bookPresentation", bookPresentation);
        SetBool(so, "resolveSceneScreenIfMissing", true);
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, StandPrefabPath);
        Object.DestroyImmediate(root);
    }

    private static AnimatorController BuildEntrySlotAnimatorController()
    {
        EnsureParentDirectory(EntrySlotControllerPath);
        AnimationClip idleClip = BuildEntrySlotStateClip(
            $"{EntrySlotAnimationRoot}/ENC_EntrySlot_Idle.anim",
            "ENC_EntrySlot_Idle",
            scale: 1f,
            hoverAlpha: 0f,
            selectedAlpha: 0f,
            lockedAlpha: 0f);
        AnimationClip hoveredClip = BuildEntrySlotStateClip(
            $"{EntrySlotAnimationRoot}/ENC_EntrySlot_Hovered.anim",
            "ENC_EntrySlot_Hovered",
            scale: 1.04f,
            hoverAlpha: 0.78f,
            selectedAlpha: 0f,
            lockedAlpha: 0f);
        AnimationClip pressedClip = BuildEntrySlotStateClip(
            $"{EntrySlotAnimationRoot}/ENC_EntrySlot_Pressed.anim",
            "ENC_EntrySlot_Pressed",
            scale: 0.94f,
            hoverAlpha: 0.9f,
            selectedAlpha: 0f,
            lockedAlpha: 0f);
        AnimationClip selectedClip = BuildEntrySlotStateClip(
            $"{EntrySlotAnimationRoot}/ENC_EntrySlot_Selected.anim",
            "ENC_EntrySlot_Selected",
            scale: 1.06f,
            hoverAlpha: 0f,
            selectedAlpha: 1f,
            lockedAlpha: 0f);
        AnimationClip lockedClip = BuildEntrySlotStateClip(
            $"{EntrySlotAnimationRoot}/ENC_EntrySlot_Locked.anim",
            "ENC_EntrySlot_Locked",
            scale: 1f,
            hoverAlpha: 0f,
            selectedAlpha: 0f,
            lockedAlpha: 1f);

        AnimatorController controller = BuildAnimatorController(
            EntrySlotControllerPath,
            new[]
            {
                new AnimatorStateSpec("Idle", idleClip, isDefault: true),
                new AnimatorStateSpec("Hovered", hoveredClip, isDefault: false),
                new AnimatorStateSpec("Pressed", pressedClip, isDefault: false),
                new AnimatorStateSpec("Selected", selectedClip, isDefault: false),
                new AnimatorStateSpec("Locked", lockedClip, isDefault: false)
            });

        EnsureAnimatorBool(controller, "Hovered");
        EnsureAnimatorBool(controller, "Pressed");
        EnsureAnimatorBool(controller, "Selected");
        EnsureAnimatorBool(controller, "Locked");
        return controller;
    }

    private static AnimationClip BuildEntrySlotStateClip(
        string clipPath,
        string clipName,
        float scale,
        float hoverAlpha,
        float selectedAlpha,
        float lockedAlpha)
    {
        EnsureParentDirectory(clipPath);
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        clip.name = clipName;
        clip.frameRate = 60f;
        clip.ClearCurves();
        SetConstantCurve(clip, string.Empty, typeof(Transform), "m_LocalScale.x", scale);
        SetConstantCurve(clip, string.Empty, typeof(Transform), "m_LocalScale.y", scale);
        SetConstantCurve(clip, string.Empty, typeof(Transform), "m_LocalScale.z", 1f);
        SetConstantCurve(clip, "HoverMarker", typeof(CanvasGroup), "m_Alpha", hoverAlpha);
        SetConstantCurve(clip, "SelectedMarker", typeof(CanvasGroup), "m_Alpha", selectedAlpha);
        SetConstantCurve(clip, "LockedMarker", typeof(CanvasGroup), "m_Alpha", lockedAlpha);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void SetConstantCurve(AnimationClip clip, string path, Type componentType, string propertyName, float value)
    {
        var binding = new EditorCurveBinding
        {
            path = path,
            type = componentType,
            propertyName = propertyName
        };

        AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, 1f / 60f, value));
    }

    private static AnimationClip BuildImageSpriteClip(string clipPath, string clipName, Sprite[] frames, float duration, bool loop)
    {
        return BuildSpriteClip(clipPath, clipName, typeof(Image), frames, duration, loop);
    }

    private static AnimationClip BuildSpriteRendererClip(string clipPath, string clipName, Sprite[] frames, float duration, bool loop)
    {
        return BuildSpriteClip(clipPath, clipName, typeof(SpriteRenderer), frames, duration, loop);
    }

    private static AnimationClip BuildSpriteClip(string clipPath, string clipName, Type componentType, Sprite[] frames, float duration, bool loop)
    {
        EnsureParentDirectory(clipPath);
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        clip.name = clipName;
        clip.frameRate = ResolveClipSampleRate(frames, duration);
        clip.ClearCurves();

        var binding = new EditorCurveBinding
        {
            path = string.Empty,
            type = componentType,
            propertyName = "m_Sprite"
        };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, BuildSpriteKeyframes(frames, duration));
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static ObjectReferenceKeyframe[] BuildSpriteKeyframes(IReadOnlyList<Sprite> frames, float duration)
    {
        if (frames == null || frames.Count == 0)
            return Array.Empty<ObjectReferenceKeyframe>();

        duration = Mathf.Max(duration, 1f / 60f);
        float interval = duration / frames.Count;
        var keyframes = new List<ObjectReferenceKeyframe>(frames.Count + 1);
        for (int i = 0; i < frames.Count; i++)
        {
            keyframes.Add(new ObjectReferenceKeyframe
            {
                time = i * interval,
                value = frames[i]
            });
        }

        keyframes.Add(new ObjectReferenceKeyframe
        {
            time = duration,
            value = frames[frames.Count - 1]
        });
        return keyframes.ToArray();
    }

    private static float ResolveClipSampleRate(IReadOnlyList<Sprite> frames, float duration)
    {
        if (frames == null || frames.Count <= 1 || duration <= 0f)
            return 60f;

        return Mathf.Max(1f, frames.Count / duration);
    }

    private static AnimatorController BuildAnimatorController(string controllerPath, IReadOnlyList<AnimatorStateSpec> states)
    {
        EnsureParentDirectory(controllerPath);
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        AnimatorControllerLayer[] layers = controller.layers;
        if (layers == null || layers.Length == 0)
        {
            var stateMachine = new AnimatorStateMachine { name = "Base Layer" };
            AssetDatabase.AddObjectToAsset(stateMachine, controller);
            layers = new[]
            {
                new AnimatorControllerLayer
                {
                    name = "Base Layer",
                    stateMachine = stateMachine,
                    defaultWeight = 1f
                }
            };
            controller.layers = layers;
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        ChildAnimatorState[] existingStates = machine.states;
        for (int i = 0; i < existingStates.Length; i++)
            machine.RemoveState(existingStates[i].state);

        AnimatorState defaultState = null;
        for (int i = 0; i < states.Count; i++)
        {
            AnimatorStateSpec spec = states[i];
            AnimatorState state = machine.AddState(spec.Name, new Vector3(260f, 80f + i * 70f, 0f));
            state.motion = spec.Clip;
            state.writeDefaultValues = true;

            if (spec.IsDefault || defaultState == null)
                defaultState = state;
        }

        machine.defaultState = defaultState;
        EditorUtility.SetDirty(machine);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void EnsureAnimatorBool(AnimatorController controller, string parameterName)
    {
        if (controller == null || string.IsNullOrWhiteSpace(parameterName))
            return;

        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == parameterName)
                return;
        }

        controller.AddParameter(parameterName, AnimatorControllerParameterType.Bool);
        EditorUtility.SetDirty(controller);
    }

    private static Sprite[] SingleFrame(Sprite sprite)
    {
        return sprite != null ? new[] { sprite } : Array.Empty<Sprite>();
    }

    private static Sprite LoadSprite(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite[] LoadSequentialSprites(string folderPath, int count)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || count <= 0)
            return System.Array.Empty<Sprite>();

        var sprites = new List<Sprite>(count);
        for (int i = 1; i <= count; i++)
        {
            Sprite sprite = LoadSprite($"{folderPath}/{i}.png");
            if (sprite != null)
                sprites.Add(sprite);
        }

        return sprites.ToArray();
    }

    private static Sprite[] LoadSpriteSheetFrames(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return System.Array.Empty<Sprite>();

        Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
        var sprites = new List<Sprite>();
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
                sprites.Add(sprite);
        }

        sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return sprites.ToArray();
    }

    private static Sprite[] ReverseFrames(IReadOnlyList<Sprite> frames)
    {
        if (frames == null || frames.Count == 0)
            return Array.Empty<Sprite>();

        var reversed = new Sprite[frames.Count];
        for (int i = 0; i < frames.Count; i++)
            reversed[i] = frames[frames.Count - 1 - i];

        return reversed;
    }

    private static Sprite FirstFrame(IReadOnlyList<Sprite> frames)
    {
        if (frames == null)
            return null;

        for (int i = 0; i < frames.Count; i++)
        {
            if (frames[i] != null)
                return frames[i];
        }

        return null;
    }

    private static Sprite LastFrame(IReadOnlyList<Sprite> frames)
    {
        if (frames == null)
            return null;

        for (int i = frames.Count - 1; i >= 0; i--)
        {
            if (frames[i] != null)
                return frames[i];
        }

        return null;
    }

    private static string ResolveEnemyName(GameObject prefab, string fallback)
    {
        if (prefab != null)
        {
            Enemy enemy = prefab.GetComponent<Enemy>();
            if (enemy != null && !string.IsNullOrWhiteSpace(enemy.EnemyName))
                return enemy.EnemyName;
        }

        return fallback;
    }

    private static Sprite ResolvePreviewSprite(GameObject prefab)
    {
        if (prefab == null)
            return null;

        SpriteRenderer[] renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer best = null;
        float bestArea = -1f;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null)
                continue;

            Vector2 size = renderer.sprite.rect.size;
            float area = size.x * size.y;
            if (area > bestArea)
            {
                best = renderer;
                bestArea = area;
            }
        }

        return best != null ? best.sprite : null;
    }

    private static T LoadAsset<T>(string path) where T : Object
    {
        return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static WeaponAbilityBlockView LoadAbilityBlockPrefab()
    {
        GameObject prefab = LoadAsset<GameObject>(AbilityBlockPrefabPath);
        return prefab != null ? prefab.GetComponent<WeaponAbilityBlockView>() : null;
    }

    private static RectTransform CreatePanel(string name, Transform parent, Color color)
    {
        Image image = CreateImage(name, parent, color);
        return image.rectTransform;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = CreateUIObject(name, parent);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.preserveAspect = true;
        return image;
    }

    private static RectTransform CreateTransparentRaycastPanel(string name, Transform parent)
    {
        Image image = CreateImage(name, parent, new Color(1f, 1f, 1f, 0f));
        image.preserveAspect = false;
        image.raycastTarget = true;
        return image.rectTransform;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 pivot, Sprite backgroundSprite = null)
    {
        GameObject go = CreateUIObject(name, parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.pivot = pivot;
        Image image = go.AddComponent<Image>();
        image.sprite = backgroundSprite;
        image.color = backgroundSprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        image.preserveAspect = true;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        TMP_Text text = CreateText("Label", go.transform, label, 18f, TextAlignmentOptions.Center);
        SetStretch(text.rectTransform);
        return button;
    }

    private static TMP_Text CreateText(string name, Transform parent, string value, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = CreateUIObject(name, parent);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        TMP_FontAsset fontAsset = LoadAsset<TMP_FontAsset>(GalmuriFontPath);
        text.font = fontAsset;
        if (fontAsset != null)
        {
            text.fontSharedMaterial = fontAsset.material;
        }

        text.fontSize = fontSize;
        text.color = new Color(0.94f, 0.9f, 0.82f, 1f);
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private static GameObject CreateMarker(string name, Transform parent)
    {
        GameObject marker = CreateUIObject(name, parent);
        Image image = marker.AddComponent<Image>();
        image.color = new Color(0.95f, 0.76f, 0.34f, 1f);
        SetAnchor(marker.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(8f, 0f), new Vector2(-8f, 4f));
        return marker;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.localPosition = Vector3.zero;
        if (parent != null)
            rect.SetParent(parent, false);
        return go;
    }

    private static void SetCentered(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    private static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetAnchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetObject(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetBool(SerializedObject so, string propertyName, bool value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetInt(SerializedObject so, string propertyName, int value)
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
    }

    private static void SetObjectArray<T>(SerializedObject so, string propertyName, IReadOnlyList<T> values) where T : Object
    {
        SerializedProperty property = so.FindProperty(propertyName);
        if (property == null || !property.isArray)
            return;

        int count = values != null ? values.Count : 0;
        property.arraySize = count;
        for (int i = 0; i < count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void EnsureParentDirectory(string assetPath)
    {
        string directory = Path.GetDirectoryName(assetPath);
        if (string.IsNullOrWhiteSpace(directory))
            return;

        string[] parts = directory.Replace("\\", "/").Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }

    private readonly struct AnimatorStateSpec
    {
        public readonly string Name;
        public readonly AnimationClip Clip;
        public readonly bool IsDefault;

        public AnimatorStateSpec(string name, AnimationClip clip, bool isDefault)
        {
            Name = name;
            Clip = clip;
            IsDefault = isDefault;
        }
    }

    private readonly struct MonsterSeed
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Type;
        public readonly string AttackStyle;
        public readonly string StageText;
        public readonly string StoryText;
        public readonly string PrefabPath;

        public MonsterSeed(string id, string displayName, string type, string attackStyle, string stageText, string storyText, string prefabPath)
        {
            Id = id;
            DisplayName = displayName;
            Type = type;
            AttackStyle = attackStyle;
            StageText = stageText;
            StoryText = storyText;
            PrefabPath = prefabPath;
        }
    }

    private readonly struct BossSeed
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Type;
        public readonly string AttackStyle;
        public readonly string StageText;
        public readonly string StoryText;
        public readonly string PrefabPath;
        public readonly string NpcDataPath;

        public BossSeed(string id, string displayName, string type, string attackStyle, string stageText, string storyText, string prefabPath, string npcDataPath)
        {
            Id = id;
            DisplayName = displayName;
            Type = type;
            AttackStyle = attackStyle;
            StageText = stageText;
            StoryText = storyText;
            PrefabPath = prefabPath;
            NpcDataPath = npcDataPath;
        }
    }
}
#endif
