public enum UpgradeCinematicType
{
    ShopActivated
}

public readonly struct UpgradeCinematicRequest
{
    public UpgradeCinematicRequest(UpgradeCinematicType type, int upgradeNodeId)
    {
        Type = type;
        UpgradeNodeId = upgradeNodeId;
    }

    public UpgradeCinematicType Type { get; }
    public int UpgradeNodeId { get; }
}
