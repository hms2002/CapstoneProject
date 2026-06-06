using UnityEngine;

public abstract class BossPatternCondition : ScriptableObject
{
    // 이 클래스의 책임:
    // 패턴 선택의 단일 조건을 평가하고 다단계 결과를 반환한다.

    public abstract BossPatternEvalResult Evaluate(BossPatternEvalContext context, BossPatternEntry ownerPattern);
}
