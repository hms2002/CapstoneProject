using UnityEngine;
using UnityGAS;

public class WitchBoss : Boss
{
    //[Header("Abilities")]
    //[SerializeField] private AbilityDefinition 파동;
    //[SerializeField] private AbilityDefinition 뭐시기Ability;
    //[SerializeField] private AbilityDefinition unseenPassiveBuff; // 시야 밖 무적/실루엣 효과 GE

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer sprite;
    [SerializeField] private Sprite         silhouetteSprite;
    [SerializeField] private Sprite         normalSprite;

    // Variables
    private float separationDistance = 8.0f; // 플레이어와의 적정 거리

    // -----------------------------------------------------------
    // BT Action Node가 호출할 행동 함수

    /// <summary> BT: "시야 밖이야, 실루엣 모드로 바꿔" </summary>
    public void SetUnseenMode(bool enable)
    {
        if (enable)
        {
            sprite.sprite = silhouetteSprite;
            sprite.color = Color.black;
            // 시야 밖 무적/버프 효과 적용
            // abilitySystem.TryActivateAbility(unseenPassiveBuff); 
        }
        else
        {
            sprite.sprite = normalSprite;
            sprite.color = Color.white;
            // 무적 버프 해제 로직 등...
        }
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