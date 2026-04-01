using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

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

    private Action onCloseCallback;

    public bool IsActive => panelRoot != null && panelRoot.activeSelf;
    public bool CanCloseOnEscape => true;
    public UIOpenGroup OpenGroup => UIOpenGroup.ExclusiveModal;
    public UIOpenGroup BlockedOpenGroups => UIOpenGroup.ExclusiveModal;
    public UIGameplayLockProfile GameplayLockProfile => UIGameplayLockProfile.FreezeAndBlockControl;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        GlobalUIRoot.AdoptToCanvas(GlobalCanvasLayer.Reward, transform);

        if (panelRoot != null)
            panelRoot.SetActive(false);

        RewardDisplayService.Instance?.RegisterView(this);
    }

    private void OnDestroy()
    {
        RewardDisplayService.Instance?.UnregisterView(this);

        if (Instance == this)
            Instance = null;
    }

    public void OpenUI()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    public void CloseUI()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        onCloseCallback?.Invoke();
        onCloseCallback = null;
        RewardDisplayService.Instance?.NotifyClosed(this);
    }

    public void ShowReward(List<UpgradeEffectSO> upgradeEffects = null, List<AffectionEffect> affectionEffects = null, Action callback = null)
    {
        onCloseCallback = callback;

        if (slotParent != null)
        {
            foreach (Transform child in slotParent)
                Destroy(child.gameObject);
        }

        string summary = string.Empty;

        if (upgradeEffects != null && upgradeEffects.Count > 0)
        {
            if (titleText != null)
                titleText.text = "업그레이드 완료!";

            foreach (UpgradeEffectSO effect in upgradeEffects)
                ProcessUpgrade(effect, ref summary);
        }
        else if (affectionEffects != null && affectionEffects.Count > 0)
        {
            if (titleText != null)
                titleText.text = "호감도 보상!";

            foreach (AffectionEffect effect in affectionEffects)
                ProcessAffection(effect, ref summary);
        }

        if (contextText != null)
            contextText.text = summary.TrimEnd();

        if (UIManager.Instance != null)
            UIManager.Instance.TryPushUI(this);
        else if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    public void Close()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.PopUI(this);
        else
            CloseUI();
    }

    private void ProcessUpgrade(UpgradeEffectSO effect, ref string summary)
    {
        if (effect == null)
            return;

        if (effect is ItemUnlockUpgradeEffectSO unlockEffect)
        {
            if (unlockEffect.Weapons != null)
            {
                foreach (var weapon in unlockEffect.Weapons)
                    CreateUnlockSlot(weapon);
            }

            if (unlockEffect.Relics != null)
            {
                foreach (var relic in unlockEffect.Relics)
                    CreateUnlockSlot(relic);
            }
        }
        else if (effect.rewardIcon != null)
        {
            CreateEffectSlot(effect.rewardIcon);
        }

        if (!string.IsNullOrEmpty(effect.rewardText))
            summary += $"- {effect.rewardText}\n";
    }

    private void ProcessAffection(AffectionEffect effect, ref string summary)
    {
        if (effect == null)
            return;

        if (effect is UnlockItemAffectionEffect unlockEffect)
        {
            foreach (var weapon in unlockEffect.weapons)
                CreateUnlockSlot(weapon);

            foreach (var relic in unlockEffect.relics)
                CreateUnlockSlot(relic);
        }
        else if (effect.rewardIcon != null)
        {
            CreateEffectSlot(effect.rewardIcon);
        }

        if (!string.IsNullOrEmpty(effect.rewardText))
            summary += $"- {effect.rewardText}\n";
    }

    private void CreateUnlockSlot(ScriptableObject definition)
    {
        if (unlockSlotPrefab == null || slotParent == null)
            return;

        Instantiate(unlockSlotPrefab, slotParent).GetComponent<UnlockSlotUI>().Setup(definition);
    }

    private void CreateEffectSlot(Sprite icon)
    {
        if (effectSlotPrefab == null || slotParent == null)
            return;

        Instantiate(effectSlotPrefab, slotParent).GetComponent<RewardEffectSlotUI>().Setup(icon);
    }
}
