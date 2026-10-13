#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Creates only approved non-Item encyclopedia authoring through Unity APIs.</summary>
public static class EncyclopediaCreatureUIAuthoring
{
    public const string PrefabPath = "Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab";
    public const string CatalogPath = "Assets/_Project/Data/Encyclopedia/EncyclopediaCatalog.asset";
    private const string Evidence = "outputs/encyclopedia-implementation-2026-10-11";
    private static TMP_Text textTemplate;
    private static Sprite heartFull, heartEmpty;
    private static readonly Color Ink = new Color(.23f, .15f, .1f);
    private static readonly Color Paper = new Color(.94f, .75f, .54f);

    [MenuItem("Tools/Encyclopedia/Author Monster Boss NPC Tabs")]
    public static void Build()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<EncyclopediaCatalogSO>(CatalogPath);
        if (catalog == null) throw new InvalidOperationException("Encyclopedia catalog missing.");
        EncyclopediaCreatureLocalizationAuthoring.Populate();
        EncyclopediaCreatureDataAuthoring.Populate(catalog);
        EncyclopediaCreatureDataAuthoring.WireMonsterIdentities();
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var screen = root.GetComponentInChildren<EncyclopediaScreen>(true);
            var book = screen.transform as RectTransform;
            var left = Find(book, "LeftPage") as RectTransform;
            var right = Find(book, "RightPage") as RectTransform;
            textTemplate = left.GetComponentsInChildren<TMP_Text>(true).First(t => t.font != null);
            heartFull = SpriteAt("Assets/_Project/Art/Sprites/UI/HeartUIAsset/Free Version 1.1/SpriteSheet/Spritesheet.png", "Spritesheet_126");
            heartEmpty = SpriteAt("Assets/_Project/Art/Sprites/UI/HeartUIAsset/EmptyHeartImage.png", "EmptyHeartImage_0");
            if (heartFull == null || heartEmpty == null) throw new InvalidOperationException("Authored affection hearts missing.");

            var presentation = root.GetComponentInChildren<EncyclopediaBookPresentation>(true);
            if (Find(book, "CreatureLeftPage") != null) throw new InvalidOperationException("Creature pages already authored; use Inspector for subsequent layout changes.");

            var leftPage = CopyRect("CreatureLeftPage", book, left);
            var rightPage = CopyRect("CreatureRightPage", book, right);
            leftPage.gameObject.SetActive(false); rightPage.gameObject.SetActive(false);
            var tab = book.gameObject.AddComponent<EncyclopediaCreatureTab>();
            var detail = rightPage.gameObject.AddComponent<EncyclopediaCreatureDetailView>();
            var leftGroup = leftPage.gameObject.AddComponent<CanvasGroup>();
            var rightGroup = rightPage.gameObject.AddComponent<CanvasGroup>();
            var tabSo = new SerializedObject(tab);
            Ref(tabSo, "catalog", catalog); Ref(tabSo, "leftContentRoot", leftPage.gameObject); Ref(tabSo, "rightContentRoot", rightPage.gameObject);
            Ref(tabSo, "detailView", detail); Ref(tabSo, "interactionCanvasGroup", leftGroup);
            Ref(tabSo, "titleText", Text("Title", leftPage, "몬스터", 42, new Vector2(.5f, 1), new Vector2(0, -25), new Vector2(530, 62), TextAlignmentOptions.Center));
            var monsters = Rect("MonsterGrid", leftPage, new Vector2(.5f, 1), new Vector2(0, -100), new Vector2(550, 560), new Vector2(.5f, 1));
            var characters = Rect("CharacterGrid", leftPage, new Vector2(.5f, 1), new Vector2(0, -100), new Vector2(550, 560), new Vector2(.5f, 1));
            Ref(tabSo, "monsterGridRoot", monsters.gameObject); Ref(tabSo, "characterGridRoot", characters.gameObject);
            Refs(tabSo, "monsterSlots", MakeGrid(monsters, 12, 4, false));
            Refs(tabSo, "characterSlots", MakeGrid(characters, 4, 2, true));
            Ref(tabSo, "previousPageButton", Button("PreviousPage", leftPage, "‹", new Vector2(.5f, 0), new Vector2(-130, 30), new Vector2(60, 50)));
            Ref(tabSo, "nextPageButton", Button("NextPage", leftPage, "›", new Vector2(.5f, 0), new Vector2(130, 30), new Vector2(60, 50)));
            Ref(tabSo, "pageText", Text("Page", leftPage, "1 / 1", 28, new Vector2(.5f, 0), new Vector2(0, 30), new Vector2(180, 50), TextAlignmentOptions.Center));
            Ref(tabSo, "entryCountText", Text("Count", leftPage, "", 20, new Vector2(0, 0), new Vector2(24, 62), new Vector2(180, 35)));
            Ref(tabSo, "emptyText", Text("Empty", leftPage, "", 28, new Vector2(.5f, .5f), Vector2.zero, new Vector2(520, 130), TextAlignmentOptions.Center));
            tabSo.ApplyModifiedPropertiesWithoutUndo();
            BuildDetail(detail, rightPage, book);

            var screenSo = new SerializedObject(screen);
            var bossButton = screenSo.FindProperty("bossMainTabButton").objectReferenceValue as UnityEngine.UI.Button;
            var npcObject = UnityEngine.Object.Instantiate(bossButton.gameObject, bossButton.transform.parent);
            npcObject.name = "NpcTab";
            var npcButton = npcObject.GetComponent<UnityEngine.UI.Button>(); npcButton.onClick.RemoveAllListeners();
            // New tab extends the bookmark rail; old bookmark geometry remains untouched.
            var npcRect = (RectTransform)npcObject.transform;
            npcRect.anchoredPosition = ((RectTransform)bossButton.transform).anchoredPosition + new Vector2(0, -100);
            foreach (var icon in npcObject.GetComponentsInChildren<Image>(true).Where(i => i.transform != npcObject.transform)) icon.gameObject.SetActive(false);
            Text("NpcLabel", npcRect, "NPC", 22, new Vector2(.5f, .5f), Vector2.zero, new Vector2(85, 45), TextAlignmentOptions.Center);
            Ref(screenSo, "creatureTab", tab); Ref(screenSo, "npcMainTabButton", npcButton);
            screenSo.FindProperty("enableMonsterCategory").boolValue = true;
            screenSo.FindProperty("enableBossCategory").boolValue = true;
            screenSo.FindProperty("enableNpcCategory").boolValue = true;
            screenSo.ApplyModifiedPropertiesWithoutUndo();
            var presentationSo = new SerializedObject(presentation);
            presentationSo.FindProperty("setContentRootsActive").boolValue = false;
            Refs(presentationSo, "additionalContentGroups", new[] { left.gameObject.AddComponent<CanvasGroup>(), right.gameObject.AddComponent<CanvasGroup>(), leftGroup, rightGroup });
            presentationSo.ApplyModifiedPropertiesWithoutUndo();
            leftPage.gameObject.SetActive(false); rightPage.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        RestoreItemImportSideEffects();
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Evidence + "/authoring-report.txt", EncyclopediaCreatureDataAuthoring.Report(catalog));
        Validate();
    }

    // Native prefab loading previews the selected locale and drives a Scrollbar handle.
    // Restore only these four verified import side effects on the persistent asset.
    public static void RestoreItemImportSideEffects()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath("5cb2a964a4d75134d94a70d8c8aa34ee"));
        var material = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(font)).OfType<Material>()
            .First(m => {AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m,out string _,out long id);return id==1826925326976027445;});
        foreach(var component in prefab.GetComponentsInChildren<Component>(true))
        {
            if(component == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(component,out string _,out long id))continue;
            if((id==4427339292197942672 || id==4338773769007280345 || id==6729888636979836795) && component is TMP_Text text)
            {
                var s=new SerializedObject(text);s.FindProperty("m_fontAsset").objectReferenceValue=font;s.FindProperty("m_sharedMaterial").objectReferenceValue=material;s.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(text);
            }
            if(id==3633219522933028530 && component is RectTransform rect)
            {var s=new SerializedObject(rect);s.FindProperty("m_AnchorMax").vector2Value=Vector2.one;s.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(rect);}
        }
        AssetDatabase.SaveAssetIfDirty(prefab);
    }

    private static void BuildDetail(EncyclopediaCreatureDetailView view, RectTransform right, RectTransform book)
    {
        var so = new SerializedObject(view);
        var monster = Stretch("MonsterDetail", right); var character = Stretch("CharacterDetail", right);
        Ref(so, "monsterRoot", monster.gameObject); Ref(so, "characterRoot", character.gameObject);
        Ref(so, "monsterTitleText", Text("MonsterName", monster, "", 38, new Vector2(.5f, 1), new Vector2(0, -18), new Vector2(550, 58), TextAlignmentOptions.Center));
        var monsterFrame = Rect("IdleFrame", monster, new Vector2(.5f, 1), new Vector2(0, -95), new Vector2(540, 250), new Vector2(.5f, 1));
        Ref(so, "monsterImage", Image("Idle", monsterFrame, Color.white, true));
        Ref(so, "killCountText", Text("Kills", monster, "", 30, new Vector2(.5f, 1), new Vector2(0, -355), new Vector2(530, 62), TextAlignmentOptions.Center));
        Ref(so, "monsterDescriptionTitleText", Text("DescriptionTitle", monster, "설명", 27, new Vector2(.5f, 1), new Vector2(0, -427), new Vector2(530, 44)));
        var monsterScroll = Scroll("MonsterDescription", monster, new Vector2(15, 22), new Vector2(-15, -485));
        Ref(so, "monsterDescriptionText", FlowText("Description", monsterScroll.content, "", 26));
        var hero = Rect("Illustration", character, new Vector2(.5f, 1), new Vector2(0, -6), new Vector2(570, 410), new Vector2(.5f, 1));
        hero.gameObject.AddComponent<RectMask2D>();
        Ref(so, "characterImage", CoverImage("Portrait", hero));
        Gradient("BottomFade", hero, new Vector2(0, 0), new Vector2(1, .3f), Color.clear, new Color(0,0,0,.92f));
        Gradient("TopFade", hero, new Vector2(0, .72f), Vector2.one, new Color(0,0,0,.8f), Color.clear);
        var title = Text("Name", hero, "", 38, new Vector2(.5f, 1), new Vector2(0, -16), new Vector2(510, 66), TextAlignmentOptions.Center); title.color = Color.white;
        Ref(so, "characterTitleText", title);
        Ref(so, "previousPortraitButton", Button("PreviousExpression", hero, "‹", new Vector2(0, .5f), new Vector2(30, 0), new Vector2(48, 65)));
        Ref(so, "nextPortraitButton", Button("NextExpression", hero, "›", new Vector2(1, .5f), new Vector2(-30, 0), new Vector2(48, 65)));
        Ref(so, "fullViewButton", Button("FullView", hero, "전체 보기", new Vector2(1, 0), new Vector2(-87, 72), new Vector2(160, 42), 22));
        var dotButtons = new List<UnityEngine.UI.Button>(); var dotMarkers = new List<GameObject>();
        // Dots are authored native uGUI graphics; circles use the built-in UI knob.
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        for (int i=0;i<3;i++)
        {
            var button = Button("Dot" + i, hero, "", new Vector2(.5f, 0), new Vector2((i-1)*30, 26), new Vector2(26,26));
            button.image.color = Color.clear;
            var idle = Image("EmptyDot", button.transform, new Color(1,1,1,.45f), false); idle.sprite = circle; ((RectTransform)idle.transform).sizeDelta = new Vector2(-12,-12);
            var selected = Image("SelectedDot", button.transform, Color.white, false); selected.sprite = circle; ((RectTransform)selected.transform).sizeDelta = new Vector2(-12,-12);
            dotButtons.Add(button); dotMarkers.Add(selected.gameObject);
        }
        Refs(so,"portraitDotButtons",dotButtons); Refs(so,"portraitDotSelectedMarkers",dotMarkers); so.FindProperty("portraitDotSpacing").floatValue=30;
        var heartRow = Rect("Affection", character, new Vector2(.5f, 1), new Vector2(0, -447), new Vector2(550, 60), new Vector2(.5f,.5f));
        Ref(so, "affectionLabelText", Text("Label",heartRow,"현재 호감도",26,new Vector2(0,.5f),new Vector2(110,0),new Vector2(210,50)));
        Refs(so, "affectionFillImages", Hearts(heartRow, new Vector2(1,.5f), new Vector2(-165,0), 42, 7));
        var scroll = Scroll("RewardsAndStory",character,new Vector2(15,12),new Vector2(-15,-495));
        Ref(so,"characterScrollRect",scroll);
        Ref(so,"rewardTitleText",FlowText("RewardTitle",scroll.content,"호감도 보상",28));
        Ref(so,"rewardText",FlowText("NoRewards",scroll.content,"",25));
        var rewards=new List<EncyclopediaAffectionRewardView>();
        for(int i=0;i<4;i++)
        {
            var row=Stretch("Reward"+i,scroll.content); var layout=row.gameObject.AddComponent<LayoutElement>();layout.preferredHeight=160;
            var slot=row.gameObject.AddComponent<EncyclopediaAffectionRewardView>();var rs=new SerializedObject(slot);
            var icon=Image("Icon",row,Color.white,true); Anchor((RectTransform)icon.transform,new Vector2(0,1),new Vector2(45,-50),new Vector2(70,70),new Vector2(.5f,.5f));
            Ref(rs,"iconImage",icon);Ref(rs,"levelText",Text("Level",row,"",24,new Vector2(0,1),new Vector2(150,-22),new Vector2(170,40)));
            Ref(rs,"statusText",Text("Status",row,"",22,new Vector2(1,1),new Vector2(-110,-22),new Vector2(180,40),TextAlignmentOptions.Right));
            Ref(rs,"descriptionText",Text("RewardDescription",row,"",24,new Vector2(.5f,1),new Vector2(42,-85),new Vector2(440,115)));
            rs.ApplyModifiedPropertiesWithoutUndo();rewards.Add(slot);
        }
        Refs(so,"rewardSlots",rewards);
        Ref(so,"storyTitleText",FlowText("StoryTitle",scroll.content,"설명 · 스토리",28));
        Ref(so,"storyText",FlowText("Story",scroll.content,"",26));
        Ref(so,"portraitMissingText",Text("MissingPortrait",hero,"",24,new Vector2(.5f,.5f),Vector2.zero,new Vector2(450,60),TextAlignmentOptions.Center));
        var full=Stretch("CreatureFullIllustration",book); full.anchorMin=new Vector2(.13f,.15f); full.anchorMax=new Vector2(.87f,.86f);
        var backdrop=full.gameObject.AddComponent<Image>();backdrop.color=new Color(.06f,.04f,.04f,.96f);backdrop.raycastTarget=true;
        Ref(so,"fullViewRoot",full.gameObject);
        var fullFrame=Stretch("ImageFrame",full);fullFrame.offsetMin=new Vector2(90,35);fullFrame.offsetMax=new Vector2(-90,-85);
        Ref(so,"fullViewImage",Image("Portrait",fullFrame,Color.white,true));
        Ref(so,"fullViewCloseButton",Button("CloseFullView",full,"닫기",new Vector2(1,1),new Vector2(-80,-40),new Vector2(120,55),26));
        full.gameObject.SetActive(false);so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void Polish()
    {
        var root=PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var tab=root.GetComponentInChildren<EncyclopediaCreatureTab>(true);
            var detail=root.GetComponentInChildren<EncyclopediaCreatureDetailView>(true);
            var ts=new SerializedObject(tab);var ds=new SerializedObject(detail);
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath("5cb2a964a4d75134d94a70d8c8aa34ee"));
            var title=ds.FindProperty("characterTitleText").objectReferenceValue as TMP_Text;
            title.rectTransform.anchoredPosition=new Vector2(0,-43);
            var hero=(RectTransform)((Image)ds.FindProperty("characterImage").objectReferenceValue).transform.parent;
            hero.sizeDelta=new Vector2(570,390);
            var character=(Transform)hero.parent;Find(character,"Affection").GetComponent<RectTransform>().anchoredPosition=new Vector2(0,-426);
            var scroll=(ScrollRect)ds.FindProperty("characterScrollRect").objectReferenceValue;
            ((RectTransform)scroll.transform).offsetMax=new Vector2(-15,-465);
            scroll.content.GetComponent<VerticalLayoutGroup>().spacing=12;
            var count=ts.FindProperty("entryCountText").objectReferenceValue as TMP_Text;
            count.rectTransform.anchoredPosition=new Vector2(120,58);
            foreach(var reward in root.GetComponentsInChildren<EncyclopediaAffectionRewardView>(true))
            {
                reward.GetComponent<LayoutElement>().preferredHeight=104;
                var rs=new SerializedObject(reward);
                var level=(TMP_Text)rs.FindProperty("levelText").objectReferenceValue;
                level.rectTransform.anchoredPosition=new Vector2(210,-20);level.rectTransform.sizeDelta=new Vector2(250,40);
                var body=(TMP_Text)rs.FindProperty("descriptionText").objectReferenceValue;
                body.rectTransform.pivot=new Vector2(.5f,1);body.rectTransform.anchoredPosition=new Vector2(45,-52);body.rectTransform.sizeDelta=new Vector2(430,52);
            }
            foreach(var card in root.GetComponentsInChildren<EncyclopediaCharacterEntryView>(true))
            {
                var cs=new SerializedObject(card);var marker=(GameObject)cs.FindProperty("selectedMarker").objectReferenceValue;
                marker.GetComponent<Image>().color=Color.clear;
                if(marker.transform.Find("TopEdge")!=null)continue;
                foreach(var edge in new[]{("TopEdge",new Vector2(0,1),new Vector2(1,1),new Vector2(0,3)),("BottomEdge",Vector2.zero,new Vector2(1,0),new Vector2(0,3)),("LeftEdge",Vector2.zero,new Vector2(0,1),new Vector2(3,0)),("RightEdge",new Vector2(1,0),Vector2.one,new Vector2(3,0))})
                {var image=Image(edge.Item1,marker.transform,new Color(1,.8f,.4f,1),false);var r=image.rectTransform;r.anchorMin=edge.Item2;r.anchorMax=edge.Item3;r.sizeDelta=edge.Item4;r.anchoredPosition=Vector2.zero;}
            }
            var screen=root.GetComponentInChildren<EncyclopediaScreen>(true);var ss=new SerializedObject(screen);
            foreach(var item in new[]{("monsterMainTabButton","몬스터","code.encyclopediadetailpanel.fa2390684b"),("bossMainTabButton","보스","code.encyclopediadetailpanel.792e7e43e5")})
            {
                var button=(UnityEngine.UI.Button)ss.FindProperty(item.Item1).objectReferenceValue;
                if(button.transform.Find("CreatureTabLabel")!=null)continue;
                textTemplate=font!=null?((TMP_Text)ts.FindProperty("titleText").objectReferenceValue):null;
                var label=Text("CreatureTabLabel",button.transform,item.Item2,20,new Vector2(.5f,.5f),Vector2.zero,new Vector2(90,44),TextAlignmentOptions.Center);
                var localized=label.gameObject.AddComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();localized.StringReference=new UnityEngine.Localization.LocalizedString(GameText.TableName,item.Item3);
                var setter=(UnityEngine.Events.UnityAction<string>)Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction<string>),label,"set_text");UnityEventTools.AddPersistentListener(localized.OnUpdateString,setter);localized.OnUpdateString.SetPersistentListenerState(0,UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
            }
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.transform.IsChildOf(((GameObject)ts.FindProperty("leftContentRoot").objectReferenceValue).transform)||t.transform.IsChildOf(((GameObject)ts.FindProperty("rightContentRoot").objectReferenceValue).transform)))
            {text.font=font;text.fontSharedMaterial=font.material;var l=text.GetComponent<LocalizeTmpFontEvent>();if(l!=null)l.SetAuthoredMaterial(font.material);}
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        Validate();
    }

    private static EncyclopediaCharacterEntryView[] MakeGrid(RectTransform parent,int count,int columns,bool character)
    {
        var layout=parent.gameObject.AddComponent<GridLayoutGroup>(); layout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=columns;
        layout.spacing=new Vector2(12,12);layout.cellSize=character?new Vector2(269,274):new Vector2(128,174);layout.childAlignment=TextAnchor.UpperCenter;
        var cards=new List<EncyclopediaCharacterEntryView>();
        for(int i=0;i<count;i++)
        {
            var card=Rect("Entry"+i,parent,Vector2.zero,Vector2.zero,layout.cellSize,new Vector2(.5f,.5f));card.gameObject.AddComponent<RectMask2D>();
            var bg=card.gameObject.AddComponent<Image>();bg.color=Paper;
            var button=card.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=bg;
            var entry=card.gameObject.AddComponent<EncyclopediaCharacterEntryView>();var so=new SerializedObject(entry);Ref(so,"button",button);
            Ref(so,"portraitImage",CoverImage("Portrait",card));
            Gradient("Fade",card,Vector2.zero,new Vector2(1,character?.43f:.45f),Color.clear,new Color(0,0,0,.95f));
            var title=Text("Name",card,"",character?26:18,new Vector2(.5f,0),new Vector2(0,character?74:35),new Vector2(layout.cellSize.x-14,50),TextAlignmentOptions.Center);title.color=Color.white;Ref(so,"titleText",title);
            var hearts=Rect("Hearts",card,new Vector2(.5f,0),new Vector2(0,34),new Vector2(190,28),new Vector2(.5f,.5f));Ref(so,"affectionRoot",hearts.gameObject);Refs(so,"affectionFillImages",Hearts(hearts,new Vector2(.5f,.5f),Vector2.zero,28,5));
            var marker=Stretch("Selected",card);var mark=marker.gameObject.AddComponent<Image>();mark.color=new Color(1,.8f,.4f,.17f);mark.raycastTarget=false;Ref(so,"selectedMarker",marker.gameObject);marker.gameObject.SetActive(false);
            so.ApplyModifiedPropertiesWithoutUndo();cards.Add(entry);
        }
        return cards.ToArray();
    }

    private static Image[] Hearts(Transform parent,Vector2 anchor,Vector2 pos,float size,float gap)
    {
        var row=Rect("HeartRow",parent,anchor,pos,new Vector2(5*size+4*gap,size),new Vector2(.5f,.5f));var fills=new List<Image>();
        for(int i=0;i<5;i++) {var r=Rect("Heart"+i,row,new Vector2(0,.5f),new Vector2(i*(size+gap),0),new Vector2(size,size),new Vector2(0,.5f));var bg=Image("Empty",r,Color.white,true);bg.sprite=heartEmpty;var fill=Image("Fill",r,Color.white,true);fill.sprite=heartFull;fill.type=UnityEngine.UI.Image.Type.Filled;fill.fillMethod=UnityEngine.UI.Image.FillMethod.Vertical;fill.fillOrigin=0;fills.Add(fill);}
        return fills.ToArray();
    }
    private static ScrollRect Scroll(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var root=Stretch(name,parent);root.offsetMin=min;root.offsetMax=max;var scroll=root.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
        var viewport=Stretch("Viewport",root);viewport.gameObject.AddComponent<RectMask2D>();var ray=viewport.gameObject.AddComponent<Image>();ray.color=Color.clear;
        var content=Stretch("Content",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);
        var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=18;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
        var fitter=content.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=viewport;scroll.content=content;scroll.scrollSensitivity=36;return scroll;
    }
    private static TMP_Text FlowText(string name,Transform parent,string value,float size) {var t=Text(name,parent,value,size,new Vector2(.5f,1),Vector2.zero,new Vector2(530,65));t.gameObject.AddComponent<LayoutElement>().minHeight=42;return t;}
    private static Image CoverImage(string name,Transform parent) {var image=Image(name,parent,Color.white,false);image.rectTransform.pivot=new Vector2(.5f,0);var f=image.gameObject.AddComponent<AspectRatioFitter>();f.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;f.aspectRatio=1;return image;}
    private static Image Image(string name,Transform parent,Color color,bool preserve) {var r=Stretch(name,parent);var i=r.gameObject.AddComponent<Image>();i.color=color;i.preserveAspect=preserve;i.raycastTarget=false;return i;}
    private static void Gradient(string name,Transform parent,Vector2 min,Vector2 max,Color top,Color bottom) {var r=Stretch(name,parent);r.anchorMin=min;r.anchorMax=max;r.gameObject.AddComponent<EncyclopediaGradientGraphic>().Configure(top,bottom);}
    private static TMP_Text Text(string name,Transform parent,string value,float size,Vector2 anchor,Vector2 pos,Vector2 dimensions,TextAlignmentOptions align=TextAlignmentOptions.TopLeft)
    {
        var r=Rect(name,parent,anchor,pos,dimensions,new Vector2(.5f,.5f));var text=r.gameObject.AddComponent<TextMeshProUGUI>();text.font=textTemplate.font;text.fontSharedMaterial=textTemplate.fontSharedMaterial;text.color=Ink;text.fontSize=size;text.text=value;text.alignment=align;text.raycastTarget=false;text.overflowMode=TextOverflowModes.Overflow;text.textWrappingMode=TextWrappingModes.Normal;
        var source=textTemplate.GetComponent<LocalizeTmpFontEvent>();
        if(source!=null) {var l=r.gameObject.AddComponent<LocalizeTmpFontEvent>();l.AssetReference=new LocalizedTmpFont{TableReference=source.AssetReference.TableReference,TableEntryReference=source.AssetReference.TableEntryReference};l.SetAuthoredMaterial(text.fontSharedMaterial);UnityEventTools.AddPersistentListener(l.OnUpdateAsset,l.ApplyLocalizedFont);l.OnUpdateAsset.SetPersistentListenerState(0,UnityEngine.Events.UnityEventCallState.EditorAndRuntime);}
        string key=value=="전체 보기"?"encyclopedia.full_view":value=="닫기"?"encyclopedia.close_full_view":value=="NPC"?"encyclopedia.tab_npc":null;
        if(key!=null) {var localized=r.gameObject.AddComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();localized.StringReference=new UnityEngine.Localization.LocalizedString("GameText",key);var setter=(UnityEngine.Events.UnityAction<string>)Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction<string>),text,"set_text");UnityEventTools.AddPersistentListener(localized.OnUpdateString,setter);localized.OnUpdateString.SetPersistentListenerState(0,UnityEngine.Events.UnityEventCallState.EditorAndRuntime);}
        return text;
    }
    private static UnityEngine.UI.Button Button(string name,Transform parent,string label,Vector2 anchor,Vector2 pos,Vector2 size,float fontSize=32)
    {
        var r=Rect(name,parent,anchor,pos,size,new Vector2(.5f,.5f));var img=r.gameObject.AddComponent<Image>();img.color=Paper;var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=img;
        if(label.Length>0)Text("Label",r,label,fontSize,new Vector2(.5f,.5f),Vector2.zero,size,TextAlignmentOptions.Center);return b;
    }
    private static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size,Vector2 pivot) {var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.gameObject.layer=5;r.SetParent(parent,false);Anchor(r,anchor,position,size,pivot);return r;}
    private static RectTransform Stretch(string name,Transform parent) {var r=Rect(name,parent,Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(.5f,.5f));r.anchorMax=Vector2.one;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;return r;}
    private static RectTransform CopyRect(string name,Transform parent,RectTransform source) {var r=Rect(name,parent,source.anchorMin,source.anchoredPosition,source.sizeDelta,source.pivot);r.anchorMax=source.anchorMax;r.localScale=source.localScale;return r;}
    private static void Anchor(RectTransform r,Vector2 a,Vector2 p,Vector2 size,Vector2 pivot) {r.anchorMin=a;r.anchorMax=a;r.pivot=pivot;r.sizeDelta=size;r.anchoredPosition=p;}
    private static Transform Find(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
    private static Sprite SpriteAt(string path,string name)=>AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault(s=>s.name==name);
    private static void Ref(SerializedObject so,string name,UnityEngine.Object obj) {var p=so.FindProperty(name);if(p==null)throw new InvalidOperationException(so.targetObject.GetType().Name+" missing "+name);p.objectReferenceValue=obj;}
    private static void Refs<T>(SerializedObject so,string name,IEnumerable<T> objects) where T:UnityEngine.Object {var list=objects.ToArray();var p=so.FindProperty(name);if(p==null)throw new InvalidOperationException("Missing "+name);p.arraySize=list.Length;for(int i=0;i<list.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=list[i];}

    public static void Validate()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var tab=prefab.GetComponentInChildren<EncyclopediaCreatureTab>(true);
        var so=new SerializedObject(tab);
        foreach(string field in new[]{"catalog","leftContentRoot","rightContentRoot","detailView","titleText","previousPageButton","nextPageButton"})if(so.FindProperty(field).objectReferenceValue==null)throw new InvalidOperationException("Missing tab reference "+field);
        if(so.FindProperty("characterSlots").arraySize!=4||so.FindProperty("monsterSlots").arraySize!=12)throw new InvalidOperationException("Wrong authored grid capacity.");
        if(prefab.GetComponentsInChildren<Transform>(true).Any(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0))throw new InvalidOperationException("Missing script in encyclopedia prefab.");
        Directory.CreateDirectory(Evidence);File.WriteAllText(Evidence+"/ui-authoring-validation.txt","Authored Monster12 / Character4 slots, references and missing-script checks passed.\n");
        Debug.Log("[EncyclopediaCreatureUIAuthoring] Authoring and reference validation complete.");
    }
}
#endif
