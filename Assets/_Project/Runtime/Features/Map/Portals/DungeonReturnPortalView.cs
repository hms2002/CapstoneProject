using UnityEngine;

/// <summary>Owns four authored portal visuals and optional Open/Idle/Close animator states, without owning travel rules.</summary>
[DisallowMultipleComponent]
public sealed class DungeonReturnPortalView : MonoBehaviour
{
    [SerializeField] private Transform up;
    [SerializeField] private Transform right;
    [SerializeField] private Transform down;
    [SerializeField] private Transform left;
    [SerializeField] private string openState = "Open";
    [SerializeField] private string idleState = "Idle";
    [SerializeField] private string closeState = "Close";
    [SerializeField, Min(0f)] private float openSeconds = 0.25f;
    [SerializeField, Min(0f)] private float closeSeconds = 0.25f;
    private Transform selected;
    private Vector3 fullScale;
    private float progress;
    private bool closing;
    private bool visible;
    private bool animated;

    public bool IsOpening => visible && !closing && progress < 1f;
    public float OpenSeconds => Mathf.Max(0f, openSeconds);
    public float CloseSeconds => Mathf.Max(0f, closeSeconds);

    public void SelectDirection(RoomSocketDirection wallDirection)
    {
        HideImmediate();
        selected = wallDirection switch
        {
            RoomSocketDirection.Up => up, RoomSocketDirection.Right => right,
            RoomSocketDirection.Down => down, _ => left
        };
        if (selected != null) fullScale = selected.localScale;
    }

    public void Open(bool immediate = false)
    {
        if (selected == null) return;
        visible = true;
        closing = false;
        progress = immediate ? 1f : 0f;
        selected.gameObject.SetActive(true);
        animated = Play(immediate ? idleState : openState);
        selected.localScale = animated || immediate ? fullScale : Vector3.zero;
    }

    public void Close()
    {
        if (!visible || selected == null) return;
        closing = true;
        progress = 0f;
        animated = Play(closeState);
    }

    public void HideImmediate()
    {
        if (selected != null) selected.localScale = fullScale;
        if (up != null) up.gameObject.SetActive(false);
        if (right != null) right.gameObject.SetActive(false);
        if (down != null) down.gameObject.SetActive(false);
        if (left != null) left.gameObject.SetActive(false);
        visible = false;
    }

    private void Update()
    {
        if (!visible || selected == null || progress >= 1f) return;
        float seconds = closing ? CloseSeconds : OpenSeconds;
        progress = seconds <= 0f ? 1f : Mathf.Min(1f, progress + Time.unscaledDeltaTime / seconds);
        if (!animated) selected.localScale = fullScale * (closing ? 1f - progress : progress);
        if (progress < 1f) return;
        if (closing) HideImmediate();
        else Play(idleState);
    }

    private bool Play(string state)
    {
        Animator animator = selected != null ? selected.GetComponent<Animator>() : null;
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(state)) return false;
        int hash = Animator.StringToHash(state);
        if (!animator.HasState(0, hash)) return false;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.Play(hash, 0, 0f);
        return true;
    }

    private void OnDisable() => HideImmediate();

#if UNITY_EDITOR
    public void EditorConfigure(Transform upRoot, Transform rightRoot, Transform downRoot, Transform leftRoot)
    {
        up = upRoot; right = rightRoot; down = downRoot; left = leftRoot;
    }
#endif
}
