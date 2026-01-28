using UnityEngine;

public class AffectionShortcut : PermanentShortcut
{
    [Header("호감도 설정")]
    public int targetBossID;        // 감시할 보스 ID
    public int requiredAffection;   // 목표 수치

    // =========================================================
    // 1. 초기화 및 이벤트 구독 (Update 대신 사용)
    // =========================================================
    protected override void Start()
    {
        base.Start(); // 부모의 Start(저장된 상태 확인) 실행

        // 이미 열려있다면 구독할 필요 없음
        if (targetDoor != null && targetDoor.IsOpen) return;

        // (A) 시작하자마자 조건 만족하는지 1회 체크 (이미 높은 상태로 맵에 진입했을 경우)
        CheckAndAutoOpen(AffectionManager.Instance.GetAffection(targetBossID));

        // (B) 앞으로 호감도가 변할 때마다 연락해달라고 등록
        if (AffectionManager.Instance != null)
        {
            AffectionManager.Instance.OnAffectionChanged += HandleAffectionChange;
        }
    }

    private void OnDestroy()
    {
        // (중요) 오브젝트가 사라질 때 구독 해제 (메모리 누수 방지)
        if (AffectionManager.Instance != null)
        {
            AffectionManager.Instance.OnAffectionChanged -= HandleAffectionChange;
        }
    }

    // =========================================================
    // 2. 자동 개방 로직 (이벤트 핸들러)
    // =========================================================
    private void HandleAffectionChange(int npcId, int currentAffection)
    {
        // 내 담당 보스의 호감도가 변했는지 확인
        if (npcId == targetBossID)
        {
            CheckAndAutoOpen(currentAffection);
        }
    }

    private void CheckAndAutoOpen(int currentAffection)
    {
        // 문이 닫혀있고 && 조건 만족하면 -> 자동 개방!
        if (targetDoor != null && !targetDoor.IsOpen && currentAffection >= requiredAffection)
        {
            // PermanentShortcut의 OnSuccess 호출 (문 열기 + 저장)
            OnSuccess();

            // 문이 열렸으니 더 이상 감시할 필요 없음 -> 구독 해제
            AffectionManager.Instance.OnAffectionChanged -= HandleAffectionChange;
        }
    }

    // =========================================================
    // 3. 플레이어 직접 상호작용 (F키) - 실패 알림용
    // =========================================================

    // 조건 체크 로직은 이제 '자동'으로 처리되므로,
    // 플레이어가 눌렀다는 건 '아직 안 열렸다(조건 불만족)'는 뜻입니다.
    protected override bool CheckCondition(TempPlayer player)
    {
        // 상호작용을 했다는 것 자체가 이미 조건 실패임.
        // 하지만 혹시 모르니 현재 수치를 가져와서 로그에 보여줌.
        int current = AffectionManager.Instance.GetAffection(targetBossID);

        // 실패 메시지 출력
        Debug.Log($"[안내] {targetBossID}번 보스의 호감도가 부족합니다. (현재:{current} / 필요:{requiredAffection})");

        // 무조건 false 반환 -> 부모 클래스(ShortcutBase)가 OnFail()을 호출하여 문을 덜컹거리게 함
        return false;
    }

    public override string GetInteractDescription()
    {
        // 문이 잠겨있을 때만 이 텍스트가 뜸
        return "살펴보기";
    }
}