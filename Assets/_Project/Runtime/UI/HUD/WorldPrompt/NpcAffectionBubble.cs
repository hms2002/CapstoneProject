using UnityEngine;

/// <summary>Animates an authored indicator. The dialogue/run owners decide its availability.</summary>
public sealed class NpcAffectionBubble : MonoBehaviour
{
    [SerializeField] private DialogueTrigger dialogue;
    [SerializeField] private Transform anchor;
    [SerializeField] private SpriteRenderer bubbleRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(0.1f)] private float framesPerSecond = 4f;
    [SerializeField, Min(0.1f)] private float worldSize = 0.8f;

    public Transform Anchor => anchor;
    public Sprite[] Frames => frames;
    public float FramesPerSecond => framesPerSecond;
    public float WorldSize => worldSize;

    private void LateUpdate()
    {
        if (bubbleRenderer == null) return;
        bubbleRenderer.enabled = dialogue != null && dialogue.isActiveAndEnabled && dialogue.CanOfferAffectionDialogue &&
            !DialoguePlayback.IsPlaying && !CinematicLetterboxOverlay.IsAnyVisible;
        if (anchor != null) bubbleRenderer.transform.position = anchor.position;
        bubbleRenderer.transform.rotation = Quaternion.identity;
        Vector3 parent = bubbleRenderer.transform.parent != null ? bubbleRenderer.transform.parent.lossyScale : Vector3.one;
        bubbleRenderer.transform.localScale = new Vector3(Scale(parent.x), Scale(parent.y), Scale(parent.z));
        if (frames != null && frames.Length > 0)
            bubbleRenderer.sprite = frames[(int)(Time.unscaledTime * framesPerSecond) % frames.Length];
    }

    private float Scale(float parent) => Mathf.Abs(parent) > 0.0001f ? worldSize / parent : worldSize;

    private void OnDisable()
    {
        if (bubbleRenderer != null) bubbleRenderer.enabled = false;
    }
}
