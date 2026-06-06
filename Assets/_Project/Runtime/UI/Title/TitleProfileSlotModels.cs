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
            ? "\uC2AC\uB86F " + (slotIndex + 1)
            : slotLabelOverride;

        string resolvedPlayTimeLabel = hasProfile
            ? NormalizeLabel(playTimeLabel, "--\uC2DC\uAC04 --\uBD84")
            : "--\uC2DC\uAC04 --\uBD84";

        string resolvedUpgradeProgressLabel = hasProfile
            ? NormalizeLabel(upgradeProgressLabel, "--%")
            : "--%";

        string resolvedMagicStoneLabel = hasProfile
            ? NormalizeLabel(magicStoneLabel, "--\uAC1C")
            : "--\uAC1C";

        string resolvedClearCountLabel = hasProfile
            ? NormalizeLabel(clearCountLabel, "--\uD68C")
            : "--\uD68C";

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
