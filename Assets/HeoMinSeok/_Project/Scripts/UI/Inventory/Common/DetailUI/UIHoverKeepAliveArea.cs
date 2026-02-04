using UnityEngine;

public class UIHoverKeepAliveArea : MonoBehaviour
{
    [SerializeField] private RectTransform rect;

    private RectTransform Rect => rect != null ? rect : transform as RectTransform;

    private void OnEnable()
    {
        UIHoverManager.Instance?.RegisterKeepAlive(Rect);
    }

    private void OnDisable()
    {
        UIHoverManager.Instance?.UnregisterKeepAlive(Rect);
    }
}
