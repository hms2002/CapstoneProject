using UnityEngine;

/// <summary>
/// 책임 :
/// - 상자 UI의 열기/닫기 수명주기를 관리하고, 열림/닫힘에 따라 플레이어 상호작용 상태를 정리한다.
/// - ChestScreen이 닫힐 때 상자 상호작용 상태를 안전하게 복구한다.
/// </summary>
public class ChestUIManager : MonoBehaviour
{
    public static ChestUIManager Instance { get; private set; }

    [SerializeField] private ChestScreen chestScreen;

    private TreasureChest openedChest;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveChestScreenReference();
        // 씬 시작 시 상자 UI가 꺼져 있도록 보장
        if (chestScreen != null) chestScreen.gameObject.SetActive(false);
    }

    public bool OpenChest(TreasureChest chest)
    {
        if (chest == null)
            return false;

        ResolveChestScreenReference();
        if (chestScreen == null)
        {
            Debug.LogError("[ChestUIManager] ChestScreen reference is missing.");
            return false;
        }

        openedChest = chest;

        // 1. 데이터 바인딩
        chestScreen.Bind(chest.GetInventory());

        // 2. [핵심] 직접 켜지 않고 UIManager의 스택 명부에 정식 등록 요청! (이제 ESC가 먹힙니다)
        bool opened = true;
        if (UIManager.Instance != null) opened = UIManager.Instance.TryPushUI(chestScreen);
        else chestScreen.OpenUI(); // UIManager가 없을 때를 대비한 방어 코드

        if (!opened && PlayerInteractor2D.Instance != null)
            PlayerInteractor2D.Instance.SetInteractState(InteractState.Idle);

        return opened;
    }

    public void HandleChestClosed()
    {
        if (PlayerInteractor2D.Instance != null)
        {
            PlayerInteractor2D.Instance.SetInteractState(InteractState.Idle);
        }

        openedChest = null;
    }
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void ResolveChestScreenReference()
    {
        if (chestScreen != null)
            return;

        chestScreen = GetComponentInChildren<ChestScreen>(true);
    }
}
