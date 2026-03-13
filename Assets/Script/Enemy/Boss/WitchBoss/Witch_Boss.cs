using UnityEngine;
using UnityGAS;

public class WitchBoss : Boss
{
    // Variables
    private float separationDistance = 8.0f; // 플레이어와의 적정 거리


    private void Update()
    {
        // 스프라이트 반전
        if      (transform.position.x > target.position.x) sprite.flipX = true;
        else if (transform.position.x < target.position.x) sprite.flipX = false;
    }

    // 실루엣모드
    public void SetUnseenMode()
    {
        // 은신 모드 애니메이션 트리거
        animator.SetBool("isUnseen", true);
    }

    // -----------------------------------------------------------
    // BT Condition Node가 참고할 판단용 데이터 제공

    /// <summary> BT Condition: "플레이어가 시야에 있어?" </summary>
    public bool IsTargetInSight()
    {
        if (target == null) return false;

        // 기획: "시야 범위에 조금이라도 들어온다면"
        // 여기서 Raycast나 거리 체크 로직 수행
        Vector2 dirToTarget = target.position - transform.position;
        float   distance    = dirToTarget.magnitude;

        // 예시: 거리가 10 이내이고, 벽에 가려지지 않았을 때
        if (distance > separationDistance) return false;

        // Physics2D.Raycast로 벽 체크
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dirToTarget, distance, LayerMask.GetMask("Wall"));
        return hit.collider == null;
    }
}