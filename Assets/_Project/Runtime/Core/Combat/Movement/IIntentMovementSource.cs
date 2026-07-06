using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임: 현재 프레임의 "의도 이동값"을 MovementMotor2D에 제공한다.
    /// - 입력, AI, 자동이동 등이 구현 가능
    /// - Rigidbody2D를 직접 만지지 않는다
    /// - 최종 이동 결과는 MovementMotor2D가 결정한다
    /// </summary>
    public interface IIntentMovementSource2D
    {
        IntentMovementData GetIntent();
    }

    /// <summary>
    /// 책임: Ability 방향 결정에 사용할 현재 이동 입력을 구체 플레이어 입력 컴포넌트 없이 제공한다.
    /// </summary>
    public interface IAbilityMoveInputSource2D
    {
        Vector2 AbilityMoveInput { get; }
    }
}
