internal readonly struct UpgradePurchaseRequest
{
    public readonly int UpgradeId;
    public readonly UpgradeProgressService ProgressService;
    public readonly CurrencyManager CurrencyManager;

    public UpgradePurchaseRequest(
        int upgradeId,
        UpgradeProgressService progressService,
        CurrencyManager currencyManager)
    {
        UpgradeId = upgradeId;
        ProgressService = progressService;
        CurrencyManager = currencyManager;
    }
}

internal readonly struct UpgradePurchaseResult
{
    public readonly bool Succeeded;
    public readonly UpgradeNodeSO Node;
    public readonly UpgradePurchaseFailureReason FailureReason;

    public UpgradePurchaseResult(
        bool succeeded,
        UpgradeNodeSO node,
        UpgradePurchaseFailureReason failureReason)
    {
        Succeeded = succeeded;
        Node = node;
        FailureReason = failureReason;
    }
}

internal enum UpgradePurchaseFailureReason
{
    None,
    MissingProgressService,
    MissingNode,
    NotUnlocked,
    MissingCurrencyManager,
    NotEnoughMagicStone,
    PurchaseRejected,
    CurrencySpendFailed
}

internal static class UpgradePurchaseService
{
    public static UpgradePurchaseResult TryPurchase(UpgradePurchaseRequest request)
    {
        if (request.ProgressService == null)
            return Failure(UpgradePurchaseFailureReason.MissingProgressService);

        UpgradeNodeSO node = request.ProgressService.GetUpgradeByID(request.UpgradeId);
        if (node == null)
            return Failure(UpgradePurchaseFailureReason.MissingNode);

        if (request.ProgressService.GetNodeStatus(request.UpgradeId) != LockType.UnLocked)
            return Failure(UpgradePurchaseFailureReason.NotUnlocked, node);

        if (request.CurrencyManager == null)
            return Failure(UpgradePurchaseFailureReason.MissingCurrencyManager, node);

        if (request.CurrencyManager.GetMagicStone() < node.price)
            return Failure(UpgradePurchaseFailureReason.NotEnoughMagicStone, node);

        if (!request.ProgressService.TryPurchase(request.UpgradeId, out node))
            return Failure(UpgradePurchaseFailureReason.PurchaseRejected, node);

        if (!request.CurrencyManager.SpendMagicStone(node.price))
        {
            request.ProgressService.RevertPurchase(request.UpgradeId);
            return Failure(UpgradePurchaseFailureReason.CurrencySpendFailed, node);
        }

        return new UpgradePurchaseResult(true, node, UpgradePurchaseFailureReason.None);
    }

    private static UpgradePurchaseResult Failure(
        UpgradePurchaseFailureReason failureReason,
        UpgradeNodeSO node = null)
    {
        return new UpgradePurchaseResult(false, node, failureReason);
    }
}
