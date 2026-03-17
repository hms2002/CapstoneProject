using UnityEngine;

public class ChestUIManager : MonoBehaviour
{
    public static ChestUIManager Instance { get; private set; }

    [SerializeField] private ChestScreen chestScreen;
    [SerializeField] private MonoBehaviour[] playerControlScriptsToDisable; // SampleTopDownPlayer 등

    private TreasureChest openedChest;
    private float prevTimeScale = 1f;

    private void Awake()
    {
        Instance = this;
        // 씬 시작 시 상자 UI가 꺼져 있도록 보장
        if (chestScreen != null) chestScreen.gameObject.SetActive(false);
    }

    public void OpenChest(TreasureChest chest)
    {
        if (chest == null) return;

        openedChest = chest;

        // 1. 게임 정지 및 플레이어 조작 잠금
        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (playerControlScriptsToDisable != null)
        {
            foreach (var s in playerControlScriptsToDisable)
            {
                if (s != null) s.enabled = false;
            }
        }

        // 2. 데이터 바인딩
        chestScreen.Bind(chest.GetInventory());

        // 3. [핵심] 직접 켜지 않고 UIManager의 스택 명부에 정식 등록 요청! (이제 ESC가 먹힙니다)
        if (UIManager.Instance != null) UIManager.Instance.PushUI(chestScreen);
        else chestScreen.OpenUI(); // UIManager가 없을 때를 대비한 방어 코드
    }

    // [핵심] ChestScreen이 UIManager에 의해 닫혔을 때(ESC나 X버튼) 호출될 뒷수습(콜백) 함수
    public void HandleChestClosed()
    {
        // 상자 닫으면 플레이어 스크립트 복구
        if (playerControlScriptsToDisable != null)
        {
            foreach (var s in playerControlScriptsToDisable)
            {
                if (s != null) s.enabled = true;
            }
        }

        if (SampleTopDownPlayer.Instance != null)
        {
            SampleTopDownPlayer.Instance.SetInteractState(InteractState.Idle);
        }

        // 시간 복구 및 상태 초기화
        Time.timeScale = prevTimeScale;
        openedChest = null;
    }
}