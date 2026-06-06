using UnityEngine;

/// <summary>
/// 책임:
/// - 대화 시작 전 현재 런타임/저장 상태를 기준으로 Ink 시작 knot을 선택하는 공용 계약을 제공한다.
/// - BossDialogueRunner가 특정 보스의 조건 분기 구현을 직접 알지 않도록 분리한다.
/// </summary>
public interface IDialogueStartKnotSelector
{
    string SelectStartKnot(NPCData npcData, TextAsset inkJSON);
}
