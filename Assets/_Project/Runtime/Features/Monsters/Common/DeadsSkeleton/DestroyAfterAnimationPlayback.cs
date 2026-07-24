using UnityEngine;

/// <summary>
/// 이 클래스의 책임:
/// 원샷 비주얼 프리팹이 애니메이션 재생을 마치면 자동으로 제거되도록 수명을 관리한다.
/// </summary>
public sealed class DestroyAfterAnimationPlayback : MonoBehaviour
{
    [Tooltip("자동 제거에 사용할 Animator입니다. 비어 있으면 자식까지 포함해 자동 탐색합니다.")]
    [SerializeField] private Animator targetAnimator;

    [Tooltip("Animator에서 길이를 찾지 못했을 때 사용할 기본 수명입니다.")]
    [SerializeField] private float fallbackLifetime = 1f;

    [Tooltip("애니메이션 종료 뒤 살짝 여유를 두고 제거하고 싶을 때 추가합니다.")]
    [SerializeField] private float destroyDelay = 0f;

    private void Awake()
    {
        if (targetAnimator == null)
            targetAnimator = GetComponentInChildren<Animator>(true);
    }

    private void OnEnable()
    {
        float lifetime = ResolveLifetime();
        Destroy(gameObject, lifetime + Mathf.Max(0f, destroyDelay));
    }

    /// <summary>
    /// 책임 :
    /// - 현재 프리팹이 재생할 애니메이션 길이를 추정해 자동 제거 시간을 계산한다.
    /// - Animator 정보가 부족한 경우에도 fallbackLifetime으로 안전하게 정리되게 보장한다.
    /// </summary>
    private float ResolveLifetime()
    {
        if (targetAnimator == null || targetAnimator.runtimeAnimatorController == null)
            return Mathf.Max(0.01f, fallbackLifetime);

        AnimationClip[] clips = targetAnimator.runtimeAnimatorController.animationClips;
        if (clips == null || clips.Length == 0)
            return Mathf.Max(0.01f, fallbackLifetime);

        float longest = 0f;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null)
                continue;

            if (clip.length > longest)
                longest = clip.length;
        }

        return longest > 0f
            ? longest
            : Mathf.Max(0.01f, fallbackLifetime);
    }
}
