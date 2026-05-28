using UnityEngine;

/// <summary>
/// 책임:
/// - 취룡 보스의 시작 미연시에서 어떤 Ink knot으로 진입할지 결정한다.
/// - 조우 횟수, 호감도, 남은 시간, 이전 전투 결과 같은 취룡 전용 조건을 공용 BossDialogueRunner 밖에 격리한다.
/// </summary>
public sealed class DragonDialogueStartKnotSelector : MonoBehaviour, IDialogueStartKnotSelector
{
    [SerializeField] private string defaultStartKnot = "DRAGON_01";

    public string SelectStartKnot(NPCData npcData, TextAsset inkJSON)
    {
        return string.IsNullOrWhiteSpace(defaultStartKnot)
            ? null
            : defaultStartKnot.Trim();
    }
}
