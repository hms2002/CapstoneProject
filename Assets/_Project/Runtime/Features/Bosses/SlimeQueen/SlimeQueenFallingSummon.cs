using CapstonePresentation;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 슬라임 여왕이 소환한 중형 슬라임의 낙하 연출, 착지 사운드, 실제 몬스터 생성을 관리한다.
/// </summary>
public sealed class SlimeQueenFallingSummon : MonoBehaviour
{
    private enum FallState
    {
        Falling,
        LandingWait,
        Finished
    }

    private SlimeQueen owner;
    private AbilitySpec sourceSpec;
    private GameObject summonPrefab;
    private Transform damageTarget;
    private Vector3 landingPosition;
    private float fallSpeed;
    private float postLandingWaitSeconds;
    private float contactRadius;
    private float landingWaitElapsed;
    private bool hasAppliedContactDamage;
    private FallState state;
    private WorldPresentationHook landingPresentation;
    private Object presentationSourceObject;

    public bool IsFinished => state == FallState.Finished;

    /// <summary>낙하 소환 연출 오브젝트를 만들고 프리팹 렌더링 정보를 복사합니다.</summary>
    public static SlimeQueenFallingSummon Create(
        SlimeQueen owner,
        AbilitySpec sourceSpec,
        GameObject summonPrefab,
        SpriteRenderer sourceRenderer,
        Vector3 startPosition,
        Vector3 landingPosition,
        float fallSpeed,
        float postLandingWaitSeconds,
        float contactRadius,
        Transform damageTarget,
        WorldPresentationHook landingPresentation = default,
        Object presentationSourceObject = null)
    {
        if (owner == null || summonPrefab == null)
            return null;

        GameObject actorObject = new GameObject($"{summonPrefab.name}_FallingSummon");
        actorObject.transform.position = startPosition;

        SpriteRenderer actorRenderer = actorObject.AddComponent<SpriteRenderer>();
        CopyRendererSettings(sourceRenderer, actorRenderer);

        SlimeQueenFallingSummon fallingSummon = actorObject.AddComponent<SlimeQueenFallingSummon>();
        fallingSummon.owner = owner;
        fallingSummon.sourceSpec = sourceSpec;
        fallingSummon.summonPrefab = summonPrefab;
        fallingSummon.damageTarget = damageTarget;
        fallingSummon.landingPosition = landingPosition;
        fallingSummon.fallSpeed = Mathf.Max(0.1f, fallSpeed);
        fallingSummon.postLandingWaitSeconds = Mathf.Max(0f, postLandingWaitSeconds);
        fallingSummon.contactRadius = Mathf.Max(0.1f, contactRadius);
        fallingSummon.landingPresentation = landingPresentation;
        fallingSummon.presentationSourceObject = presentationSourceObject;
        fallingSummon.state = FallState.Falling;
        return fallingSummon;
    }

    private void Update()
    {
        if (state == FallState.Falling)
        {
            AdvanceFall();
            return;
        }

        if (state == FallState.LandingWait)
            AdvanceLandingWait();
    }

    /// <summary>낙하 중인 소환체를 착지 위치까지 이동시킵니다.</summary>
    private void AdvanceFall()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            landingPosition,
            fallSpeed * Time.deltaTime);

        if ((transform.position - landingPosition).sqrMagnitude > 0.0001f)
            return;

        transform.position = landingPosition;
        PlayLandingPresentation();
        state = FallState.LandingWait;
        landingWaitElapsed = 0f;
    }

    /// <summary>AL에서 주입된 착지 연출 hook을 기존 프레젠테이션 인프라로 실행합니다.</summary>
    private void PlayLandingPresentation()
    {
        SlimeQueenPresentationAudioUtility.PlayPresentation(
            landingPresentation,
            owner != null ? owner.gameObject : gameObject,
            landingPosition,
            presentationSourceObject != null ? presentationSourceObject : this,
            damageTarget != null ? damageTarget.gameObject : null,
            gameObject);
    }

    /// <summary>착지 후 대기 시간을 진행하고 끝나면 실제 중형 슬라임을 생성합니다.</summary>
    private void AdvanceLandingWait()
    {
        landingWaitElapsed += Time.deltaTime;
        TryApplyContactDamage();

        if (landingWaitElapsed < postLandingWaitSeconds)
            return;

        SpawnActualSummon();
        Finish();
    }

    /// <summary>낙하체와 플레이어가 접촉 거리 안에 들어오면 한 번만 피해를 적용합니다.</summary>
    private void TryApplyContactDamage()
    {
        if (hasAppliedContactDamage || owner == null || damageTarget == null)
            return;

        if (!TopDownEllipseHitUtility2D.ContainsPoint(transform.position, contactRadius * 2f, damageTarget.position))
            return;

        hasAppliedContactDamage = true;
        owner.ApplyFallingSummonDamage(sourceSpec, damageTarget.gameObject, transform.position);
    }

    /// <summary>착지 대기가 끝난 위치에 실제 Knight 또는 Wizard 프리팹을 생성합니다.</summary>
    private void SpawnActualSummon()
    {
        if (summonPrefab == null)
            return;

        GameObject summonedSlime = Instantiate(summonPrefab, landingPosition, Quaternion.identity);
        if (summonedSlime != null && summonedSlime.TryGetComponent(out Mob mob))
            mob.SuppressMonsterLootDrop();
        if (summonedSlime != null && summonedSlime.TryGetComponent(out ExperienceRewardSource experienceReward))
            experienceReward.SetGrantExperience(false);
    }

    /// <summary>프리팹의 SpriteRenderer 설정을 낙하 연출용 렌더러에 복사합니다.</summary>
    private static void CopyRendererSettings(SpriteRenderer sourceRenderer, SpriteRenderer targetRenderer)
    {
        if (targetRenderer == null || sourceRenderer == null)
            return;

        targetRenderer.sprite = sourceRenderer.sprite;
        targetRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
        targetRenderer.color = sourceRenderer.color;
        targetRenderer.flipX = sourceRenderer.flipX;
        targetRenderer.flipY = sourceRenderer.flipY;
        targetRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        targetRenderer.sortingOrder = sourceRenderer.sortingOrder;
        targetRenderer.maskInteraction = sourceRenderer.maskInteraction;
        targetRenderer.transform.localScale = sourceRenderer.transform.lossyScale;
    }

    /// <summary>패턴이 취소되었을 때 낙하 연출을 중단하고 제거합니다.</summary>
    public void CancelFall()
    {
        Finish();
    }

    /// <summary>낙하 소환 연출 오브젝트를 종료 상태로 전환하고 제거합니다.</summary>
    private void Finish()
    {
        state = FallState.Finished;
        Destroy(gameObject);
    }
}
