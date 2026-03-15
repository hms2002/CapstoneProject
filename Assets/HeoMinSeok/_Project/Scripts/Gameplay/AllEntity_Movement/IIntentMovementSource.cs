using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 현재 프레임의 "의도 이동값"을 제공하는 인터페이스.
    /// - 입력, AI, 자동이동 등이 구현 가능
    /// - Rigidbody2D를 직접 만지지 않는다
    /// - 최종 이동 결과는 MovementMotor2D가 결정한다
    /// </summary>
    public interface IIntentMovementSource2D
    {
        IntentMovementData GetIntent();
    }
}