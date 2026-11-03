using System;
using System.Collections;
using UnityEngine;

/// <summary>Positions the authored field-item shadow using editor-baked visible contours.</summary>
[DisallowMultipleComponent]
public sealed class WorldItemShadowPresenter2D : MonoBehaviour, IWorldItemDropLandingVisual
{
    [Serializable]
    private struct SpriteContact
    {
        public Sprite sprite;
        public float centerFromTop;
    }

    [SerializeField] private SpriteRenderer sourceRenderer;
    [SerializeField] private SpriteRenderer shadowRenderer;
    [SerializeField] private SpriteContact[] contacts = Array.Empty<SpriteContact>();

    private Sprite lastSprite;
    private Vector3 lastPosition;
    private Vector3 lastScale;
    private bool hasContact;
    private bool travelling;

    private void LateUpdate() => RefreshShadow();

    private void RefreshShadow()
    {
        if (shadowRenderer == null)
            return;

        if (travelling || sourceRenderer == null || !sourceRenderer.enabled || sourceRenderer.sprite == null)
        {
            shadowRenderer.enabled = false;
            lastSprite = null;
            return;
        }

        Transform sourceTransform = sourceRenderer.transform;
        Sprite sprite = sourceRenderer.sprite;
        Vector3 position = sourceTransform.localPosition;
        Vector3 scale = sourceTransform.localScale;
        if (lastSprite != sprite || lastPosition != position || lastScale != scale)
        {
            lastSprite = sprite;
            lastPosition = position;
            lastScale = scale;
            hasContact = false;
            foreach (SpriteContact contact in contacts)
            {
                if (contact.sprite != sprite)
                    continue;

                Bounds bounds = sprite.bounds;
                float displaySize = Mathf.Max(bounds.size.x * scale.x, bounds.size.y * scale.y);
                Bounds shadowBounds = shadowRenderer.sprite.bounds;
                Vector3 shadowScale = new(displaySize * 0.75f / shadowBounds.size.x,
                    displaySize * 0.25f / shadowBounds.size.y, 1f);
                Vector3 center = sourceTransform.TransformPoint(new Vector3(bounds.center.x, bounds.max.y, 0f));
                center = shadowRenderer.transform.parent.InverseTransformPoint(center);
                center.y -= contact.centerFromTop * displaySize;
                shadowRenderer.transform.localScale = shadowScale;
                shadowRenderer.transform.localPosition = center - Vector3.Scale(shadowBounds.center, shadowScale);
                hasContact = true;
                break;
            }
        }

        shadowRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        shadowRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
        shadowRenderer.enabled = hasContact;
    }

    public void OnDropTravelStarted()
    {
        travelling = true;
        if (shadowRenderer != null)
            shadowRenderer.enabled = false;
    }

    public IEnumerator PlayDropLandingRoutine()
    {
        travelling = false;
        RefreshShadow();
        yield break;
    }

    private void OnDisable()
    {
        travelling = false;
        lastSprite = null;
        if (shadowRenderer != null)
            shadowRenderer.enabled = false;
    }
}
