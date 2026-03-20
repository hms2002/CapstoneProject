using UnityEngine;
using UnityEngine.Pool;

public class SpeechBubbleComponent : MonoBehaviour
{
    [Header("Bubble Settings")]
    [SerializeField] private SpeechBubble bubblePrefab;
    [SerializeField] private Vector3 bubbleOffset = new Vector3(0, 2f, 0);

    [Header("Typing Settings")]
    [SerializeField] private bool defaultUseTyping = true;
    [SerializeField] private float defaultTypingSpeed = 0.05f;

    private IObjectPool<SpeechBubble> bubblePool;

    private void Awake()
    {
        bubblePool = new ObjectPool<SpeechBubble>(
            createFunc: () => Instantiate(bubblePrefab),
            actionOnGet: (bubble) => bubble.gameObject.SetActive(true),
            actionOnRelease: (bubble) => bubble.gameObject.SetActive(false),
            actionOnDestroy: (bubble) => Destroy(bubble.gameObject),
            defaultCapacity: 1,
            maxSize: 3
        );
    }

    public void Speak(string text, float duration = 2.5f)
    {
        SpeechBubble bubble = bubblePool.Get();
        bubble.SetupAndShow(transform, bubbleOffset, text, duration, defaultUseTyping, defaultTypingSpeed, (b) => bubblePool.Release(b));
    }
}