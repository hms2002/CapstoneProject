using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class RewardDisplayUI : MonoBehaviour
{
    public static RewardDisplayUI Instance { get; private set; }

    [Header("UI Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI contextText;

    [Header("Slot Prefabs")]
    [SerializeField] private GameObject unlockSlotPrefab; // 큰 슬롯 (툴팁 O)
    [SerializeField] private GameObject effectSlotPrefab; // 작은 슬롯 (툴팁 X)

    [Header("Container")]
    [SerializeField] private Transform slotParent;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        panelRoot.SetActive(false);
    }

    private void Update()
    {
        if (panelRoot.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    public void ShowReward(List<UpgradeEffectSO> upgradeEffects = null, List<AffectionEffect> affectionEffects = null)
    {
        foreach (Transform child in slotParent) Destroy(child.gameObject);
        string summary = "";

        // 1. 타입에 따른 타이틀 자동 결정
        if (upgradeEffects != null && upgradeEffects.Count > 0)
        {
            titleText.text = "업그레이드 완료!";
            foreach (var e in upgradeEffects) ProcessUpgradeEffect(e, ref summary);
        }
        else if (affectionEffects != null && affectionEffects.Count > 0)
        {
            titleText.text = "호감도 보상!";
            foreach (var e in affectionEffects) ProcessAffectionEffect(e, ref summary);
        }

        contextText.text = summary.TrimEnd();
        panelRoot.SetActive(true);
    }

    private void ProcessUpgradeEffect(UpgradeEffectSO e, ref string summary)
    {
        if (e == null) return;
        if (e is UnlockItemUpgradeEffect uie)
        {
            foreach (var w in uie.weapons) CreateUnlockSlot(w);
            foreach (var r in uie.relics) CreateUnlockSlot(r);
        }
        else if (e.rewardIcon != null) CreateEffectSlot(e.rewardIcon);

        if (!string.IsNullOrEmpty(e.rewardText)) summary += $"- {e.rewardText}\n";
    }

    private void ProcessAffectionEffect(AffectionEffect e, ref string summary)
    {
        if (e == null) return;
        if (e is UnlockItemAffectionEffect aie)
        {
            foreach (var w in aie.weapons) CreateUnlockSlot(w);
            foreach (var r in aie.relics) CreateUnlockSlot(r);
        }
        else if (e.rewardIcon != null) CreateEffectSlot(e.rewardIcon);

        if (!string.IsNullOrEmpty(e.rewardText)) summary += $"- {e.rewardText}\n";
    }

    private void CreateUnlockSlot(ScriptableObject def)
    {
        GameObject go = Instantiate(unlockSlotPrefab, slotParent);
        go.GetComponent<UnlockSlotUI>().Setup(def);
    }

    private void CreateEffectSlot(Sprite icon)
    {
        GameObject go = Instantiate(effectSlotPrefab, slotParent);
        go.GetComponent<RewardEffectSlotUI>().Setup(icon);
    }

    public void Close()
    {
        panelRoot.SetActive(false);
        if (UIHoverManager.Instance != null) UIHoverManager.Instance.HideImmediate();
    }
}