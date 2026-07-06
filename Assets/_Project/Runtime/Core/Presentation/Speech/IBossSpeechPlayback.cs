using System;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 보스 gameplay 코드가 상황 키 기반 말풍선 출력을 요청하는 최소 계약을 제공한다.
/// - 실제 말풍선 UI 컴포넌트와 pool/rendering 구현은 UI 계층에 남기고, 호출 방향만 Core 계약으로 낮춘다.
/// </summary>
public interface IBossSpeechPlayback
{
    bool TrySpeakSituation(BossSpeechSituationEnum situation, float duration = 2f);
    bool TrySpeakSituation(BossSpeechSituationEnum situation, float duration, Action onBubbleHidden);
    bool TrySpeakSituationParallelAt(BossSpeechSituationEnum situation, float duration, Transform anchor, Vector3 offsetDelta);
    bool TrySpeakSituationParallelAt(BossSpeechSituationEnum situation, float duration, Action onBubbleHidden, Transform anchor, Vector3 offsetDelta);
    bool TrySpeakSituationParallelAt(BossSpeechSituationEnum situation, float duration, Func<Vector3> anchorPositionResolver, Vector3 offsetDelta);
    bool TrySpeakSituationParallelAt(BossSpeechSituationEnum situation, float duration, Action onBubbleHidden, Func<Vector3> anchorPositionResolver, Vector3 offsetDelta);
    bool TrySpeakSituationParallelAt(BossSpeechSituationEnum situation, float duration, Func<Vector3> anchorPositionResolver, Func<Quaternion> anchorRotationResolver, Vector3 offsetDelta);
    bool TrySpeakSituationParallelAt(BossSpeechSituationEnum situation, float duration, Action onBubbleHidden, Func<Vector3> anchorPositionResolver, Func<Quaternion> anchorRotationResolver, Vector3 offsetDelta);
    bool TrySpeakSituationAnimated(BossSpeechSituationEnum situation, float duration, DialogueAnimType animType, Action onBubbleHidden);
    bool TrySpeakSituationAnimated(BossSpeechSituationEnum situation, float duration, DialogueAnimType animType, Action onBubbleHidden, Func<string, string> lineFormatter);
}
