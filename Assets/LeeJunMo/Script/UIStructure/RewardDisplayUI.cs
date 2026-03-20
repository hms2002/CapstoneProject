using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

// [수정] IStackableUI를 상속받아 UIManager의 스택 관리를 받습니다!
public class RewardDisplayUI : MonoBehaviour, IStackableUI
{
    public static RewardDisplayUI Instance { get; private set; }

    [Header("UI Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI contextText;

    [Header("Slot Prefabs")]
    [SerializeField] private GameObject unlockSlotPrefab;
    [SerializeField] private GameObject effectSlotPrefab;
    [SerializeField] private Transform slotParent;

    private System.Action onCloseCallback;

    private void Awake()
    {
        Instance = this;
        panelRoot.SetActive(false);
    }

    // [수정] RegisterUI / UnregisterUI 삭제 (Push/Pop으로 대체됨)

    // =========================================================
    // IStackableUI 규약
    // =========================================================
    public bool IsActive => panelRoot.activeSelf;
    public bool CanCloseOnEscape => true;

    public void OpenUI()
    {
        panelRoot.SetActive(true);
    }

    public void CloseUI()
    {
        panelRoot.SetActive(false);
        onCloseCallback?.Invoke();
        onCloseCallback = null;
    }
    // =========================================================

    public void ShowReward(List<UpgradeEffectSO> uEffects = null, List<AffectionEffect> aEffects = null, System.Action callback = null)
    {
        this.onCloseCallback = callback;

        foreach (Transform child in slotParent) Destroy(child.gameObject);
        string summary = "";

        if (uEffects != null && uEffects.Count > 0)
        {
            titleText.text = "업그레이드 완료!";
            foreach (var e in uEffects) ProcessUpgrade(e, ref summary);
        }
        else if (aEffects != null && aEffects.Count > 0)
        {
            titleText.text = "호감도 보상!";
            foreach (var e in aEffects) ProcessAffection(e, ref summary);
        }

        contextText.text = summary.TrimEnd();

        // [수정] 직접 켜지 않고 UIManager에게 켜달라고(Push) 요청!
        if (UIManager.Instance != null) UIManager.Instance.PushUI(this);
        else panelRoot.SetActive(true); // 혹시 UIManager가 없을 때를 대비한 방어코드
    }

    private void ProcessUpgrade(UpgradeEffectSO e, ref string s)
    {
        if (e is UnlockItemUpgradeEffect uie)
        {
            foreach (var w in uie.weapons) CreateUnlockSlot(w);
            foreach (var r in uie.relics) CreateUnlockSlot(r);
        }
        else if (e.rewardIcon != null) CreateEffectSlot(e.rewardIcon);
        if (!string.IsNullOrEmpty(e.rewardText)) s += $"- {e.rewardText}\n";
    }

    private void ProcessAffection(AffectionEffect e, ref string s)
    {
        if (e is UnlockItemAffectionEffect aie)
        {
            foreach (var w in aie.weapons) CreateUnlockSlot(w);
            foreach (var r in aie.relics) CreateUnlockSlot(r);
        }
        else if (e.rewardIcon != null) CreateEffectSlot(e.rewardIcon);
        if (!string.IsNullOrEmpty(e.rewardText)) s += $"- {e.rewardText}\n";
    }

    private void CreateUnlockSlot(ScriptableObject d) => Instantiate(unlockSlotPrefab, slotParent).GetComponent<UnlockSlotUI>().Setup(d);
    private void CreateEffectSlot(Sprite i) => Instantiate(effectSlotPrefab, slotParent).GetComponent<RewardEffectSlotUI>().Setup(i);

    public void Close()
    {
        // [수정] 직접 끄지 않고 UIManager에게 꺼달라고(Pop) 요청!
        if (UIManager.Instance != null) UIManager.Instance.PopUI(this);
        else CloseUI();
    }
}