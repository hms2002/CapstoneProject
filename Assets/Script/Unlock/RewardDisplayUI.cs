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
    [SerializeField] private GameObject unlockSlotPrefab;
    [SerializeField] private GameObject effectSlotPrefab;
    [SerializeField] private Transform slotParent;

    private void Awake() { Instance = this; panelRoot.SetActive(false); }

    private void OnEnable() => UIManager.Instance?.RegisterUI("RewardDisplay");
    private void OnDisable() => UIManager.Instance?.UnregisterUI("RewardDisplay");

    public void ShowReward(List<UpgradeEffectSO> uEffects = null, List<AffectionEffect> aEffects = null)
    {
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
        panelRoot.SetActive(true);
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
        panelRoot.SetActive(false);

        // [핵심 수정] 업그레이드 UI가 열려있다면 대화를 재개하지 않음 (업그레이드 UI가 닫힐 때 재개할 것이므로)
        if (UIManager.Instance.IsUIOpen("UpgradeTree")) return;

        // 그 외의 경우(호감도 이벤트 등)에는 대화 재개
        if (DialogueManager.GetInstance() != null && DialogueManager.GetInstance().dialogueIsPlaying)
        {
            DialogueManager.GetInstance().ResumeDialogueAfterUI();
        }
    }
}