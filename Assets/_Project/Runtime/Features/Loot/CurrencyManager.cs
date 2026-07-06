using System;
using UnityEngine;

// 책임: 매직스톤 재화 값을 저장 데이터와 동기화하고 변경 이벤트를 발행한다.
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
        if (RunSessionStore.IsRunActive)
            amount += RunSessionStore.GetPendingRunMagicStoneDelta();

        return amount;
    }

    public void AddMagicStone(int amount)
    {
        if (!TryGetData(out GameData data))
            return;

        if (amount == 0)
            return;

        if (RunSessionStore.IsRunActive)
        {
            RunSessionStore.AddPendingRunMagicStoneDelta(amount);
            OnMagicStoneChanged?.Invoke(GetMagicStone());
            return;
        }

        data.magicStone += amount;
        OnMagicStoneChanged?.Invoke(data.magicStone);
        GameDataStore.RequestImmediateSave(this);
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

        if (RunSessionStore.IsRunActive)
        {
            RunSessionStore.AddPendingRunMagicStoneDelta(-amount);
            OnMagicStoneChanged?.Invoke(GetMagicStone());
            return true;
        }

        data.magicStone -= amount;
        OnMagicStoneChanged?.Invoke(data.magicStone);
        GameDataStore.RequestImmediateSave(this);
        return true;
    }

    private static bool TryGetData(out GameData data)
    {
        data = GameDataStore.Data;
        return data != null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
