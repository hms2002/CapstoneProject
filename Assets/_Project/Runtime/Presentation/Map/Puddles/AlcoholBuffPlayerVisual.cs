using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Displays one reusable set of green rising arrows on the buff recipient while its alcohol GameplayEffect is active.
    /// Observes effect lifetime without changing buffs, puddles or HUD ownership, and hides visuals on disable/expiration.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AlcoholBuffPlayerVisual : MonoBehaviour
    {
        [SerializeField] private GameplayEffect alcoholBuff;
        [SerializeField] private Sprite arrowSprite;
        [SerializeField, Range(1, 8)] private int arrowCount = 3;
        [SerializeField, Min(0.01f)] private float arrowWidth = 0.16f;
        [SerializeField, Min(0.1f)] private float cycleSeconds = 0.85f;
        [SerializeField, Min(0f)] private float riseHeight = 0.8f;
        [SerializeField, Min(0f)] private float horizontalSpread = 0.35f;
        [SerializeField] private float baseHeight = 0.15f;
        [SerializeField] private Color arrowColor = new Color(0.45f, 1f, 0.55f, 0.8f);
        [SerializeField] private string sortingLayerName = "Entity";
        [SerializeField] private int sortingOrder = 20;

        private GameplayEffectRunner runner;
        private SpriteRenderer[] arrows;
        private float elapsed;
        private bool showing;

        private void Awake() => runner = GetComponent<GameplayEffectRunner>();

        private void LateUpdate()
        {
            if (runner == null) runner = GetComponent<GameplayEffectRunner>();
            ActiveGameplayEffect effect = runner != null && alcoholBuff != null
                ? runner.FindActiveEffect(alcoholBuff, gameObject) : null;
            if (effect == null || effect.TimeRemaining <= 0f || arrowSprite == null)
            {
                Hide();
                return;
            }

            EnsureArrows();
            if (!showing) elapsed = 0f;
            showing = true;
            float duration = Mathf.Max(0.1f, cycleSeconds);
            elapsed = (elapsed + Time.deltaTime) % duration;
            for (int i = 0; i < arrows.Length; i++)
            {
                float t = Mathf.Repeat(elapsed / duration + (float)i / arrows.Length, 1f);
                SpriteRenderer arrow = arrows[i];
                float x = arrows.Length == 1 ? 0f : Mathf.Lerp(-horizontalSpread, horizontalSpread, (float)i / (arrows.Length - 1));
                arrow.transform.localPosition = new Vector3(x, baseHeight + riseHeight * t, 0f);
                float scale = arrowWidth / Mathf.Max(0.001f, arrowSprite.bounds.size.x);
                arrow.transform.localScale = Vector3.one * scale * Mathf.Lerp(1f, 0.7f, t);
                Color color = arrowColor;
                color.a *= Mathf.Min(1f, t / 0.12f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, t)));
                arrow.color = color;
                arrow.enabled = true;
            }
        }

        private void EnsureArrows()
        {
            if (arrows != null) return;
            arrows = new SpriteRenderer[Mathf.Clamp(arrowCount, 1, 8)];
            for (int i = 0; i < arrows.Length; i++)
            {
                var child = new GameObject("AlcoholBuffPlayerArrow");
                child.layer = gameObject.layer;
                child.transform.SetParent(transform, false);
                SpriteRenderer arrow = child.AddComponent<SpriteRenderer>();
                arrow.sprite = arrowSprite;
                arrow.flipY = true;
                arrow.sortingLayerName = sortingLayerName;
                arrow.sortingOrder = sortingOrder;
                arrow.enabled = false;
                arrows[i] = arrow;
            }
        }

        private void Hide()
        {
            showing = false;
            if (arrows == null) return;
            foreach (SpriteRenderer arrow in arrows)
                if (arrow != null) arrow.enabled = false;
        }

        private void OnDisable() => Hide();

        private void OnDestroy()
        {
            if (arrows == null) return;
            foreach (SpriteRenderer arrow in arrows)
                if (arrow != null) Destroy(arrow.gameObject);
        }
    }
}
