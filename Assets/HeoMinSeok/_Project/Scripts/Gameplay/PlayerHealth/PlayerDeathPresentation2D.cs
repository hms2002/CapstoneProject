using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerDeathPresentation2D : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string deathTrigger = "Death";

    [Header("Visual")]
    [SerializeField] private SpriteRenderer[] spriteRenderers;
    [SerializeField] private Color deadTint = new Color(0.45f, 0.45f, 0.45f, 1f);

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
}
