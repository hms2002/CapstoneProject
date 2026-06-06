using UnityEngine;
using System.Text;

/// <summary>
/// 책임:
/// - 전투 객체가 경로 차단 여부를 물을 때 어떤 맥락의 검사인지 구분한다.
/// - 문, 벽 대체 오브젝트, 기믹 장애물이 구체 타입을 노출하지 않고 동일한 질문에 답하게 한다.
/// </summary>
public enum CombatPathBlockerQuery
{
    Sight,
    Charge
}

/// <summary>
/// 책임:
/// - collider를 가진 월드 오브젝트가 전투 시야/돌진 경로를 막는지 공통 계약으로 제공한다.
/// - Enemy/Rook 같은 전투 객체가 DoorObject 같은 구체 구현을 직접 알지 않게 한다.
/// </summary>
public interface ICombatPathBlocker2D
{
    bool BlocksCombatPath(Collider2D queriedCollider, GameObject requester, CombatPathBlockerQuery query);
}

/// <summary>
/// 책임:
/// - collider 계층에서 ICombatPathBlocker2D 구현체를 찾아 전투 경로 차단 여부를 질의한다.
/// - 호출자가 DoorObject/기믹 오브젝트 같은 구체 타입 탐색을 반복하지 않게 한다.
/// </summary>
public static class CombatPathBlocker2DUtility
{
    public static bool BlocksCombatPath(Collider2D collider, GameObject requester, CombatPathBlockerQuery query)
    {
        if (collider == null)
            return false;

        MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>(includeInactive: false);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ICombatPathBlocker2D blocker &&
                blocker.BlocksCombatPath(collider, requester, query))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 책임:
    /// - 경로 차단 판정에 참여한 ICombatPathBlocker2D 구현체와 결과를 디버그 문자열로 풀어낸다.
    /// - 문 trigger/obstacle collider 혼동처럼 "누가 왜 막았는지"가 중요한 테스트 상황에서만 사용한다.
    /// </summary>
    public static string DescribeBlockerDecision(Collider2D collider, GameObject requester, CombatPathBlockerQuery query)
    {
        if (collider == null)
            return "no collider";

        MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>(includeInactive: false);
        if (behaviours == null || behaviours.Length == 0)
            return "no parent behaviours";

        StringBuilder builder = new StringBuilder();
        bool foundBlocker = false;
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (!(behaviour is ICombatPathBlocker2D blocker))
                continue;

            foundBlocker = true;
            bool blocks = blocker.BlocksCombatPath(collider, requester, query);
            if (builder.Length > 0)
                builder.Append("; ");

            builder
                .Append(behaviour.GetType().Name)
                .Append("(")
                .Append(behaviour.name)
                .Append(")=")
                .Append(blocks);

            AppendDoorDebugInfo(builder, behaviour, collider);
        }

        return foundBlocker ? builder.ToString() : "no ICombatPathBlocker2D";
    }

    /// <summary>DoorObject 차단 판정에서 queried collider가 실제 obstacle인지 확인할 수 있는 부가 정보를 붙입니다.</summary>
    private static void AppendDoorDebugInfo(StringBuilder builder, MonoBehaviour behaviour, Collider2D queriedCollider)
    {
        if (!(behaviour is DoorObject door))
            return;

        Collider2D obstacle = door.obstacleCollider;
        builder
            .Append(" [doorOpen=")
            .Append(door.IsOpen)
            .Append(", queried=")
            .Append(queriedCollider != null ? queriedCollider.name : "null")
            .Append(", obstacle=")
            .Append(obstacle != null ? obstacle.name : "null")
            .Append(", queriedIsObstacle=")
            .Append(obstacle != null && queriedCollider == obstacle)
            .Append("]");
    }
}
