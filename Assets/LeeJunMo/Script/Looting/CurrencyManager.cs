using System;
using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    public event Action<int> OnMagicStoneChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance != null)
            return;

        var go = new GameObject(nameof(CurrencyManager));
        go.AddComponent<CurrencyManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        OnMagicStoneChanged?.Invoke(GetMagicStone());
    }

    public int GetMagicStone()
    {
        if (!TryGetData(out GameData data))
            return 0;

        int amount = data.magicStone;
        if (IsRunActive())
            amount += GamePlayDataManager.Instance.GetPendingRunMagicStoneDelta();

        return amount;
    }

    public void AddMagicStone(int amount)
    {
        if (!TryGetData(out GameData data))
            return;

        if (amount == 0)
            return;

        if (IsRunActive())
        {
            GamePlayDataManager.Instance.AddPendingRunMagicStoneDelta(amount);
            OnMagicStoneChanged?.Invoke(GetMagicStone());
            return;
        }

        data.magicStone += amount;
        OnMagicStoneChanged?.Invoke(data.magicStone);
        GameDataSaveCoordinator.RequestImmediateSave(this);
    }

    public bool SpendMagicStone(int amount)
    {
        if (!TryGetData(out GameData data))
            return false;

        if (GetMagicStone() < amount)
        {
            Debug.Log("[CurrencyManager] Not enough magic stone.");
            return false;
        }

        if (IsRunActive())
        {
            GamePlayDataManager.Instance.AddPendingRunMagicStoneDelta(-amount);
            OnMagicStoneChanged?.Invoke(GetMagicStone());
            return true;
        }

        data.magicStone -= amount;
        OnMagicStoneChanged?.Invoke(data.magicStone);
        GameDataSaveCoordinator.RequestImmediateSave(this);
        return true;
    }

    private static bool TryGetData(out GameData data)
    {
        data = GameDataManager.Instance != null ? GameDataManager.Instance.Data : null;
        return data != null;
    }

    private static bool IsRunActive()
    {
        return GamePlayDataManager.Instance != null
            && GamePlayDataManager.Instance.Data != null
            && GamePlayDataManager.Instance.Data.isRunActive;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
