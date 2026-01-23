using UnityEngine;

// 에디터 우클릭 -> Create -> Upgrade -> Nodes -> Test Dummy 선택
[CreateAssetMenu(menuName = "Upgrade/Nodes/Test Dummy")]
public class Upgrade_TestDummy : UpgradeNodeSO
{
    [Header("테스트 메시지 설정")]
    public string messageToLog = "테스트 업그레이드 적용됨!";

    public override void ApplyEffect(TempPlayer player)
    {
        // 플레이어 스크립트가 없어도 에러 안 나고 로그만 찍힘
        Debug.Log($"[Dummy Upgrade] 효과 발동! ID: {nodeID}, 메시지: {messageToLog}");

        if (player != null)
        {
            Debug.Log($" -> 플레이어 오브젝트({player.name}) 감지됨.");
        }
        else
        {
            Debug.LogWarning(" -> 플레이어를 찾을 수 없지만, 업그레이드 로직은 정상 작동함.");
        }
    }
}