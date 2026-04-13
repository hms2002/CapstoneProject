using System;
using UnityEngine;
using UnityEngine.Pool;

public class SpeechBubbleComponent : MonoBehaviour
{
    [Header("Bubble Settings")]
    [SerializeField] private SpeechBubble bubblePrefab;
    [SerializeField] private Vector3 bubbleOffset = new Vector3(0, 2f, 0);
    [SerializeField] private bool showBubbleOffsetGizmo = true;
    [SerializeField] private Color bubbleOffsetGizmoColor = new Color(1f, 0.92f, 0.16f, 0.9f);
    [SerializeField] private float bubbleOffsetGizmoRadius = 0.12f;

    [Header("Typing Settings")]
    [SerializeField] private bool defaultUseTyping = true;
    [SerializeField] private float defaultTypingSpeed = 0.05f;

    private IObjectPool<SpeechBubble> bubblePool;
    private SpeechBubble activeBubble;

    private void Awake()
    {
        bubblePool = new ObjectPool<SpeechBubble>(
            createFunc: () => Instantiate(bubblePrefab),
            actionOnGet: (bubble) => bubble.gameObject.SetActive(true),
            actionOnRelease: (bubble) => bubble.gameObject.SetActive(false),
            actionOnDestroy: (bubble) => Destroy(bubble.gameObject),
            defaultCapacity: 1,
            maxSize: 1
        );
    }

    public void Speak(string text, float duration = 2.5f)
    {
        Speak(text, duration, null);
    }

    public void Speak(string text, float duration, SpeechBubbleThemeSettings theme)
    {
        Speak(text, duration, theme, null);
    }

    public void Speak(string text, float duration, SpeechBubbleThemeSettings theme, Action onHidden)
    {
        if (bubblePrefab == null || string.IsNullOrWhiteSpace(text))
            return;

        SpeechBubble bubble = activeBubble;
        if (bubble == null)
        {
            bubble = bubblePool.Get();
            activeBubble = bubble;
        }

        bubble.SetupAndShow(
            transform,
            bubbleOffset,
            text,
            duration,
            defaultUseTyping,
            defaultTypingSpeed,
            theme,
            onHidden,
            HandleBubbleReleased);
    }

    private void HandleBubbleReleased(SpeechBubble bubble)
    {
        if (activeBubble == bubble)
            activeBubble = null;

        bubblePool.Release(bubble);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showBubbleOffsetGizmo)
            return;

        Vector3 origin = transform.position;
        Vector3 targetPosition = origin + bubbleOffset;
        float radius = Mathf.Max(0.01f, bubbleOffsetGizmoRadius);

        Gizmos.color = bubbleOffsetGizmoColor;
        Gizmos.DrawLine(origin, targetPosition);
        Gizmos.DrawWireSphere(targetPosition, radius);
        Gizmos.DrawSphere(targetPosition, radius * 0.35f);
    }
}
