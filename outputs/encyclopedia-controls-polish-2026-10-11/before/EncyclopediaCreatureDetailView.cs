using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Shows authored creature details and portrait controls without owning gameplay state.</summary>
[DisallowMultipleComponent]
public sealed class EncyclopediaCreatureDetailView : MonoBehaviour
{
    [Header("Authored Sections")]
    [SerializeField] private GameObject monsterRoot;
    [SerializeField] private GameObject characterRoot;
    [SerializeField] private GameObject emptyRoot;

    [Header("Monster")]
    [SerializeField] private UnityEngine.UI.Image monsterImage;
    [SerializeField] private TMP_Text monsterTitleText;
    [SerializeField] private TMP_Text killCountText;
    [SerializeField] private TMP_Text monsterDescriptionText;
    [SerializeField] private TMP_Text monsterDescriptionTitleText;

    [Header("Character")]
    [SerializeField] private UnityEngine.UI.Image characterImage;
    [SerializeField] private TMP_Text characterTitleText;
    [SerializeField] private TMP_Text portraitMissingText;
    [SerializeField] private TMP_Text affectionValueText;
    [SerializeField] private TMP_Text affectionLabelText;
    [SerializeField] private UnityEngine.UI.Image[] affectionFillImages = Array.Empty<UnityEngine.UI.Image>();
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private TMP_Text rewardTitleText;
    [SerializeField] private EncyclopediaAffectionRewardView[] rewardSlots = Array.Empty<EncyclopediaAffectionRewardView>();
    [SerializeField] private TMP_Text storyText;
    [SerializeField] private TMP_Text storyTitleText;
    [SerializeField] private UnityEngine.UI.ScrollRect characterScrollRect;

    [Header("Portrait Controls")]
    [SerializeField] private UnityEngine.UI.Button previousPortraitButton;
    [SerializeField] private UnityEngine.UI.Button nextPortraitButton;
    [SerializeField] private UnityEngine.UI.Button fullViewButton;
    [SerializeField] private UnityEngine.UI.Button[] portraitDotButtons = Array.Empty<UnityEngine.UI.Button>();
    [SerializeField] private GameObject[] portraitDotSelectedMarkers = Array.Empty<GameObject>();
    [SerializeField] private float portraitDotSpacing = 24f;

    [Header("Authored Full View")]
    [SerializeField] private GameObject fullViewRoot;
    [SerializeField] private UnityEngine.UI.Image fullViewImage;
    [SerializeField] private UnityEngine.UI.Button fullViewCloseButton;

    private readonly List<EncyclopediaPortrait> portraits = new();
    private UnityAction[] dotListeners;
    private int portraitIndex = -1;
    private Sprite[] previewFrames = Array.Empty<Sprite>();
    private float previewFrameRate;
    private float previewClock;
    private int previewIndex;
    private bool showingMonster;
    private bool interactionEnabled = true;
    private bool fullViewOpen;

    public event Action<Sprite> PortraitChanged;
    public bool IsFullViewOpen => fullViewOpen;
    public Sprite CurrentPortrait => portraitIndex >= 0 && portraitIndex < portraits.Count
        ? portraits[portraitIndex].sprite : null;

    private void Awake()
    {
        if (previousPortraitButton != null)
            previousPortraitButton.onClick.AddListener(ShowPreviousPortrait);
        if (nextPortraitButton != null)
            nextPortraitButton.onClick.AddListener(ShowNextPortrait);
        if (fullViewButton != null)
            fullViewButton.onClick.AddListener(OpenFullView);
        if (fullViewCloseButton != null)
            fullViewCloseButton.onClick.AddListener(CloseFullView);

        dotListeners = new UnityAction[portraitDotButtons.Length];
        for (int i = 0; i < portraitDotButtons.Length; i++)
        {
            int slot = i;
            dotListeners[i] = () => SelectDot(slot);
            if (portraitDotButtons[i] != null)
                portraitDotButtons[i].onClick.AddListener(dotListeners[i]);
        }
        CloseFullView();
    }

    private void OnDisable()
    {
        CloseFullView();
        previewClock = 0f;
    }

    private void OnDestroy()
    {
        if (previousPortraitButton != null)
            previousPortraitButton.onClick.RemoveListener(ShowPreviousPortrait);
        if (nextPortraitButton != null)
            nextPortraitButton.onClick.RemoveListener(ShowNextPortrait);
        if (fullViewButton != null)
            fullViewButton.onClick.RemoveListener(OpenFullView);
        if (fullViewCloseButton != null)
            fullViewCloseButton.onClick.RemoveListener(CloseFullView);
        if (dotListeners != null)
            for (int i = 0; i < portraitDotButtons.Length; i++)
                if (portraitDotButtons[i] != null)
                    portraitDotButtons[i].onClick.RemoveListener(dotListeners[i]);
    }

    private void Update()
    {
        if (!showingMonster || previewFrames.Length < 2 || previewFrameRate <= 0f)
            return;

        previewClock += Time.unscaledDeltaTime * previewFrameRate;
        int advance = Mathf.FloorToInt(previewClock);
        if (advance <= 0)
            return;
        previewClock -= advance;
        previewIndex = (previewIndex + advance) % previewFrames.Length;
        EncyclopediaPortraitUtility.SetSprite(monsterImage, previewFrames[previewIndex]);
    }

    public void ShowMonster(EncyclopediaMonsterEntry entry, int killCount, bool resetPreview = true)
    {
        if (entry == null)
        {
            Clear();
            return;
        }
        CloseFullView();
        showingMonster = true;
        SetActive(monsterRoot, true);
        SetActive(characterRoot, false);
        SetActive(emptyRoot, false);
        portraits.Clear();
        portraitIndex = -1;
        previewFrames = entry.previewFrames ?? Array.Empty<Sprite>();
        previewFrameRate = Mathf.Max(0f, entry.previewFrameRate);
        if (resetPreview)
        {
            previewClock = 0f;
            previewIndex = 0;
        }
        previewIndex = previewFrames.Length > 0 ? Mathf.Clamp(previewIndex, 0, previewFrames.Length - 1) : 0;
        Sprite image = previewFrames.Length > 0 ? previewFrames[previewIndex] : entry.image;
        EncyclopediaPortraitUtility.SetSprite(monsterImage, image);
        SetText(monsterTitleText, entry.DisplayName);
        SetText(killCountText, GameText.Format("encyclopedia.monster_kill_count", "누적 처치 수 {0}회", Mathf.Max(0, killCount)));
        SetText(monsterDescriptionTitleText, GameText.Get("encyclopedia.description", "설명"));
        SetText(monsterDescriptionText, DescriptionOrPending(entry.StoryText));
    }

    public void ShowCharacter(string title, string story, NPCData npc, IReadOnlyList<EncyclopediaPortrait> entries,
        int affection, bool resetPortrait = true)
    {
        showingMonster = false;
        previewFrames = Array.Empty<Sprite>();
        SetActive(monsterRoot, false);
        SetActive(characterRoot, true);
        SetActive(emptyRoot, false);
        int previousIndex = portraitIndex;
        portraits.Clear();
        if (entries != null)
            for (int i = 0; i < entries.Count; i++)
                if (entries[i]?.sprite != null)
                    portraits.Add(entries[i]);
        portraitIndex = resetPortrait ? EncyclopediaPortraitUtility.GetDefaultIndex(portraits)
            : portraits.Count > 0 ? Mathf.Clamp(previousIndex, 0, portraits.Count - 1) : -1;
        if (resetPortrait)
            CloseFullView();
        SetText(characterTitleText, title);
        SetText(portraitMissingText, GameText.Get("encyclopedia.portrait_pending", "일러스트 준비 중"));
        SetActive(portraitMissingText != null ? portraitMissingText.gameObject : null, portraits.Count == 0);
        SetText(affectionLabelText, GameText.Get("encyclopedia.current_affection", "현재 호감도"));
        SetText(affectionValueText, affection.ToString());
        for (int i = 0; i < affectionFillImages.Length; i++)
            if (affectionFillImages[i] != null)
                affectionFillImages[i].fillAmount = i < Mathf.Max(0, affection) ? 1f : 0f;
        SetText(rewardTitleText, GameText.Get("encyclopedia.affection_rewards", "호감도 보상"));
        BindRewards(npc, affection);
        SetText(storyTitleText, GameText.Get("encyclopedia.character_story", "설명 · 스토리"));
        SetText(storyText, DescriptionOrPending(story));
        RefreshPortrait();
        SettleCharacterLayout(resetPortrait);
    }

    public void Clear()
    {
        showingMonster = false;
        previewFrames = Array.Empty<Sprite>();
        previewClock = 0f;
        portraits.Clear();
        portraitIndex = -1;
        CloseFullView();
        SetActive(monsterRoot, false);
        SetActive(characterRoot, false);
        SetActive(emptyRoot, true);
        EncyclopediaPortraitUtility.SetSprite(monsterImage, null);
        EncyclopediaPortraitUtility.SetSprite(characterImage, null);
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        RefreshPortraitControls();
        if (fullViewCloseButton != null)
            fullViewCloseButton.interactable = enabled;
    }

    public bool TryCloseFullView()
    {
        if (!fullViewOpen)
            return false;
        CloseFullView();
        return true;
    }

    public void StepPortrait(int direction) => SelectPortrait(portraitIndex + Math.Sign(direction));

    private void BindRewards(NPCData npc, int affection)
    {
        IReadOnlyList<AffectionReward> rewards = npc != null ? npc.affectionRewards : null;
        int count = rewards != null ? rewards.Count : 0;
        for (int i = 0; i < rewardSlots.Length; i++)
        {
            if (rewardSlots[i] == null)
                continue;
            if (i < count)
                rewardSlots[i].Bind(rewards[i], affection);
            else
                rewardSlots[i].Clear();
        }
        SetActive(rewardText != null ? rewardText.gameObject : null, count == 0);
        SetText(rewardText, GameText.Get("code.encyclopediadetailpanel.5df09cdf01", "등록된 호감도 보상 없음"));
    }

    private void ShowPreviousPortrait() => SelectPortrait(portraitIndex - 1);
    private void ShowNextPortrait() => SelectPortrait(portraitIndex + 1);

    private void SelectDot(int slot)
    {
        int index = EncyclopediaPortraitUtility.GetDotPortraitIndex(portraits.Count, portraitIndex, slot);
        if (index >= 0)
            SelectPortrait(index);
    }

    private void SelectPortrait(int index)
    {
        if (!interactionEnabled || index < 0 || index >= portraits.Count || index == portraitIndex)
            return;
        portraitIndex = index;
        RefreshPortrait();
    }

    private void RefreshPortrait()
    {
        EncyclopediaPortraitUtility.SetSprite(characterImage, CurrentPortrait);
        if (fullViewOpen)
            EncyclopediaPortraitUtility.SetSprite(fullViewImage, CurrentPortrait);
        RefreshPortraitControls();
        PortraitChanged?.Invoke(CurrentPortrait);
    }

    private void RefreshPortraitControls()
    {
        if (previousPortraitButton != null)
            previousPortraitButton.interactable = interactionEnabled && portraitIndex > 0;
        if (nextPortraitButton != null)
            nextPortraitButton.interactable = interactionEnabled && portraitIndex >= 0 && portraitIndex < portraits.Count - 1;
        if (fullViewButton != null)
            fullViewButton.interactable = interactionEnabled && CurrentPortrait != null;
        int dotCount = EncyclopediaPortraitUtility.GetDotCount(portraits.Count);
        for (int i = 0; i < portraitDotButtons.Length; i++)
        {
            UnityEngine.UI.Button dot = portraitDotButtons[i];
            if (dot == null)
                continue;
            dot.gameObject.SetActive(i < dotCount);
            if (i >= dotCount)
                continue;
            int index = EncyclopediaPortraitUtility.GetDotPortraitIndex(portraits.Count, portraitIndex, i);
            dot.interactable = interactionEnabled && index != portraitIndex;
            if (dot.transform is RectTransform rect)
            {
                Vector2 position = rect.anchoredPosition;
                position.x = EncyclopediaPortraitUtility.GetDotOffset(portraits.Count, portraitIndex, i) * portraitDotSpacing;
                rect.anchoredPosition = position;
            }
            if (i < portraitDotSelectedMarkers.Length)
                SetActive(portraitDotSelectedMarkers[i], index == portraitIndex);
        }
    }

    private void OpenFullView()
    {
        if (!interactionEnabled || CurrentPortrait == null || fullViewRoot == null)
            return;
        fullViewOpen = true;
        EncyclopediaPortraitUtility.SetSprite(fullViewImage, CurrentPortrait);
        fullViewRoot.SetActive(true);
    }

    private void CloseFullView()
    {
        fullViewOpen = false;
        SetActive(fullViewRoot, false);
        EncyclopediaPortraitUtility.SetSprite(fullViewImage, null);
    }

    private void SettleCharacterLayout(bool resetScroll)
    {
        if (characterScrollRect == null)
            return;
        Canvas.ForceUpdateCanvases();
        if (characterScrollRect.content != null)
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(characterScrollRect.content);
        if (resetScroll)
        {
            characterScrollRect.StopMovement();
            characterScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private static string DescriptionOrPending(string description) => !string.IsNullOrWhiteSpace(description)
        ? description : GameText.Get("code.encyclopediadetailpanel.c6793d9ba0", "설명 준비 중");

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value ?? string.Empty;
    }

    private static void SetActive(GameObject root, bool active)
    {
        if (root != null)
            root.SetActive(active);
    }
}
