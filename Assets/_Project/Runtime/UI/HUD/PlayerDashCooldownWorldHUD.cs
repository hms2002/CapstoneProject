using UnityEngine;
using UnityGAS;

/// <summary>Projects the existing dash cooldown onto an authored player-foot gauge.</summary>
[DisallowMultipleComponent]
public sealed class PlayerDashCooldownWorldHUD : MonoBehaviour
{
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private AbilityDefinition dash;
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private RectTransform fill;

    private float fullWidth;
    private float observedDuration;
    private float previousRemaining;

    private void Awake()
    {
        if (fill != null)
            fullWidth = fill.rect.width;
    }

    private void OnEnable() => Hide();
    private void OnDisable() => Hide();

    private void LateUpdate()
    {
        if (worldCanvas == null || fill == null)
            return;

        float remaining = abilitySystem != null && dash != null
            ? abilitySystem.GetCooldownRemaining(dash)
            : 0f;
        if (remaining <= 0f)
        {
            Hide();
            return;
        }

        // Capture each cooldown's actual duration, including modifiers. No separate timer.
        // A new dash can begin between rendered frames, without a visible zero frame.
        if (previousRemaining <= 0f || remaining > previousRemaining)
            observedDuration = remaining;

        previousRemaining = remaining;
        transform.rotation = Quaternion.identity;
        fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
            fullWidth * Mathf.Clamp01(remaining / observedDuration));
        worldCanvas.enabled = true;
    }

    private void Hide()
    {
        if (worldCanvas != null)
            worldCanvas.enabled = false;
        observedDuration = 0f;
        previousRemaining = 0f;
    }
}
