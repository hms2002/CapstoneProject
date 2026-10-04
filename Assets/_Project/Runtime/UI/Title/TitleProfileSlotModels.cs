using System;
using UnityEngine;

[Serializable]
public struct TitleProfileSlotSummary
{
    [SerializeField] private int slotIndex;
    [SerializeField] private bool hasProfile;
    [SerializeField] private bool hasActiveRun;
    [SerializeField] private string slotLabel;
    [SerializeField] private string playTimeLabel;
    [SerializeField] private string upgradeProgressLabel;
    [SerializeField] private string magicStoneLabel;
    [SerializeField] private string clearCountLabel;

    public TitleProfileSlotSummary(
        int slotIndex,
        bool hasProfile,
        bool hasActiveRun,
        string slotLabel,
        string playTimeLabel,
        string upgradeProgressLabel,
        string magicStoneLabel,
        string clearCountLabel)
    {
        this.slotIndex = slotIndex;
        this.hasProfile = hasProfile;
        this.hasActiveRun = hasActiveRun;
        this.slotLabel = slotLabel;
        this.playTimeLabel = playTimeLabel;
        this.upgradeProgressLabel = upgradeProgressLabel;
        this.magicStoneLabel = magicStoneLabel;
        this.clearCountLabel = clearCountLabel;
    }

    public int SlotIndex => slotIndex;
    public bool HasProfile => hasProfile;
    public bool HasActiveRun => hasActiveRun;
    public string SlotLabel => slotLabel;
    public string PlayTimeLabel => playTimeLabel;
    public string UpgradeProgressLabel => upgradeProgressLabel;
    public string MagicStoneLabel => magicStoneLabel;
    public string ClearCountLabel => clearCountLabel;
}

[Serializable]
public sealed class TitleProfileSlotDebugState
{
    [SerializeField] private bool hasProfile;
    [SerializeField] private bool hasActiveRun;
    [SerializeField] private string slotLabelOverride = string.Empty;
    [SerializeField] private string playTimeLabel = "--\uC2DC\uAC04 --\uBD84";
    [SerializeField] private string upgradeProgressLabel = "--%";
    [SerializeField] private string magicStoneLabel = "--\uAC1C";
    [SerializeField] private string clearCountLabel = "--\uD68C";

    public TitleProfileSlotSummary BuildSummary(int slotIndex)
    {
        string resolvedSlotLabel = string.IsNullOrWhiteSpace(slotLabelOverride)
            ? GameText.Get("code.titleprofileslotmodels.d2f7319b25", "슬롯 ") + (slotIndex + 1)
            : slotLabelOverride;

        string resolvedPlayTimeLabel = hasProfile
            ? NormalizeLabel(playTimeLabel, GameText.Get("code.titleprofileslotmodels.2f4e954267", "--시간 --분"))
            : GameText.Get("code.titleprofileslotmodels.2f4e954267", "--시간 --분");

        string resolvedUpgradeProgressLabel = hasProfile
            ? NormalizeLabel(upgradeProgressLabel, "--%")
            : "--%";

        string resolvedMagicStoneLabel = hasProfile
            ? NormalizeLabel(magicStoneLabel, GameText.Get("code.titleprofileslotmodels.0eeffea837", "--개"))
            : GameText.Get("code.titleprofileslotmodels.0eeffea837", "--개");

        string resolvedClearCountLabel = hasProfile
            ? NormalizeLabel(clearCountLabel, GameText.Get("code.titleprofileslotmodels.10690e9888", "--회"))
            : GameText.Get("code.titleprofileslotmodels.10690e9888", "--회");

        return new TitleProfileSlotSummary(
            slotIndex,
            hasProfile,
            hasActiveRun,
            resolvedSlotLabel,
            resolvedPlayTimeLabel,
            resolvedUpgradeProgressLabel,
            resolvedMagicStoneLabel,
            resolvedClearCountLabel);
    }

    private static string NormalizeLabel(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
