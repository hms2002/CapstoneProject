using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 보스 FSM state가 AbilitySystem과 TagSystem의 구체 구현을 직접 몰라도 능력 실행 상태를 조회하고 제어할 수 있게 한다.
/// - ASC/태그 시스템과 FSM 사이의 최소 소통 계약을 제공해 state 코드의 결합도를 낮춘다.
/// </summary>
public interface IBossAbilityStateBridge : IAIAbilityBridge
{
}
