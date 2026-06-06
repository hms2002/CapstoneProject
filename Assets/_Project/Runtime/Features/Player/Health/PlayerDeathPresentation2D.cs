using System.Collections;
using UnityEngine;
using CapstoneAudio;

[DisallowMultipleComponent]
/// <summary>
/// 책임 :
/// - 플레이어 사망 시 애니메이션, 틴트, 사망 사운드 1회 재생을 함께 수행한다.
/// - 사망 연출 지속 시간을 단일 컴포넌트에서 제공해 상위 시퀀스가 기다릴 수 있게 한다.
/// </summary>
public sealed class PlayerDeathPresentation2D : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string deathTrigger = "Death";

    [Header("Visual")]
    [SerializeField] private SpriteRenderer[] spriteRenderers;
    [SerializeField] private Color deadTint = new Color(0.45f, 0.45f, 0.45f, 1f);

    [Header("Audio")]
    [SerializeField] private SoundRef deathSound;

    [Header("Timing")]
    [SerializeField] private float presentationDuration = 1.25f;

    public float PresentationDuration => Mathf.Max(0f, presentationDuration);

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (spriteRenderers == null || spriteRenderers.Length == 0)
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
    }

    public IEnumerator Play()
    {
        PlayAnimation();
        ApplyTint();
        PlayDeathSound();

        float waitSeconds = PresentationDuration;
        if (waitSeconds > 0f)
            yield return new WaitForSeconds(waitSeconds);
    }

    private void PlayAnimation()
    {
        if (animator == null || string.IsNullOrWhiteSpace(deathTrigger))
            return;

        animator.SetTrigger(deathTrigger);
    }

    private void ApplyTint()
    {
        if (spriteRenderers == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
                continue;

            spriteRenderers[i].color = deadTint;
        }
    }

    /// <summary>
    /// 책임 :
    /// - 플레이어 사망 연출 시작 시 전용 사운드를 1회 재생한다.
    /// - SoundManager 문맥에 플레이어 자신을 실어 추후 앵커 정책과 카탈로그 규칙을 재사용한다.
    /// </summary>
    private void PlayDeathSound()
    {
        if (!deathSound.IsSet)
            return;

        SoundManager.EnsureInstance().Play(deathSound, new SoundPlaybackContext
        {
            Instigator = gameObject,
            Causer = gameObject,
            Target = gameObject,
            Position = transform.position,
            SourceObject = this
        });
    }
}
