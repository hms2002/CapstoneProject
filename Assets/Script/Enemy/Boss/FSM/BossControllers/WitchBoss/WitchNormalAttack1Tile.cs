using System.Collections;
using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(AttackTelegraphView))]
public class WitchNormalAttack1Tile : MonoBehaviour
{
    private const float HitTime = 0.12f;

    private AttackTelegraphView telegraphView;
    private AttackTelegraphStyle warningStyle;
    private AttackTelegraphStyle hitStyle;
    private GameObject targetObject;
    private CombatHitPayload hitPayload;
    private Vector2 tileSize;
    private float angleDeg;

    private void Awake()
    {
        telegraphView = GetComponent<AttackTelegraphView>();
        warningStyle = MakeWarningStyle();
        hitStyle = MakeHitStyle();
    }

    private void OnDestroy()
    {
        if (warningStyle != null) Destroy(warningStyle);
        if (hitStyle != null) Destroy(hitStyle);
    }

    /// <summary>장판 경고와 타격 순서를 시작합니다.</summary>
    public void Play(GameObject target, CombatHitPayload payload, Vector2 size, float angle, float showDelay, float hitDelay)
    {
        targetObject = target;
        hitPayload = payload;
        tileSize = size;
        angleDeg = angle;

        StopAllCoroutines();
        StartCoroutine(Run(showDelay, hitDelay));
    }

    private IEnumerator Run(float showDelay, float hitDelay)
    {
        float safeShowDelay = Mathf.Max(0f, showDelay);
        float safeHitDelay = Mathf.Max(safeShowDelay, hitDelay);
        float warningTime = safeHitDelay - safeShowDelay;

        if (safeShowDelay > 0f)
            yield return new WaitForSeconds(safeShowDelay);

        if (warningTime > 0f)
        {
            ShowWarning(warningTime);
            yield return new WaitForSeconds(warningTime);
        }

        ShowHit();
        TryHit();
        yield return new WaitForSeconds(HitTime);
        Destroy(gameObject);
    }

    /// <summary>빨간 경고 장판을 표시합니다.</summary>
    private void ShowWarning(float duration)
    {
        if (telegraphView == null) return;

        telegraphView.Show(MakeSpec(duration, warningStyle));
    }

    /// <summary>타격 장판을 표시합니다.</summary>
    private void ShowHit()
    {
        if (telegraphView == null) return;

        telegraphView.Show(MakeSpec(HitTime, hitStyle));
    }

    /// <summary>장판 안의 플레이어를 공격합니다.</summary>
    private void TryHit()
    {
        if (targetObject == null || hitPayload == null || !hitPayload.IsValid()) return;

        Collider2D[] hits = Physics2D.OverlapBoxAll(transform.position, tileSize, angleDeg);
        for (int i = 0; i < hits.Length; i++)
        {
            GameObject hitObject = GetHitObject(hits[i]);
            if (hitObject != targetObject) continue;

            CombatHitPayloadApplier.Apply(hitObject, hitPayload, transform.position);
            return;
        }
    }

    /// <summary>사각형 장판 정보를 만듭니다.</summary>
    private AttackTelegraphSpec MakeSpec(float duration, AttackTelegraphStyle style)
    {
        return AttackTelegraphSpec.CreateRectangle(
            transform.position,
            tileSize,
            angleDeg,
            duration,
            style);
    }

    /// <summary>충돌한 대상의 본체를 찾습니다.</summary>
    private GameObject GetHitObject(Collider2D hitCollider)
    {
        if (hitCollider == null) return null;

        if (hitCollider.attachedRigidbody != null) return hitCollider.attachedRigidbody.gameObject;

        return hitCollider.transform.root.gameObject;
    }

    /// <summary>경고 색상 스타일을 만듭니다.</summary>
    private AttackTelegraphStyle MakeWarningStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        style.fillColorStart = new Color(1f, 0f, 0f, 0.2f);
        style.fillColorEnd = new Color(1f, 0f, 0f, 0.2f);
        style.borderColorStart = new Color(1f, 0f, 0f, 1f);
        style.borderColorEnd = new Color(1f, 0f, 0f, 1f);
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 1f;
        style.blinkFrequency = 0f;
        style.blinkAlphaMin = 1f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }

    /// <summary>타격 색상 스타일을 만듭니다.</summary>
    private AttackTelegraphStyle MakeHitStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        style.fillColorStart = new Color(0.95f, 0f, 1f, 0.8f);
        style.fillColorEnd = new Color(0.95f, 0f, 1f, 0.8f);
        style.borderColorStart = new Color(0.95f, 0f, 1f, 1f);
        style.borderColorEnd = new Color(0.95f, 0f, 1f, 1f);
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 1f;
        style.blinkFrequency = 0f;
        style.blinkAlphaMin = 1f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }
}
