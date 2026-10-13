using UnityEngine;

namespace UnityGAS
{
    /// <summary>Projects player alcohol exposure into the authored head mug and overflow droplets.</summary>
    [DisallowMultipleComponent]
    public sealed class AlcoholExposurePlayerVisual : MonoBehaviour
    {
        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");

        [SerializeField] private PlayerAlcoholExposure exposure;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer mug;
        [SerializeField] private SpriteRenderer[] overflowDroplets;
        [SerializeField, Min(0f)] private float overflowShakeDegrees = 9f;
        [SerializeField, Min(0f)] private float overflowShakeCyclesPerSecond = 3f;
        [SerializeField, Min(0.01f)] private float overflowDropSeconds = 0.6f;
        [SerializeField, Min(0f)] private float overflowDropDistance = 0.25f;

        private MaterialPropertyBlock mugProperties;
        private Vector3 rootPosition;
        private Vector3 rootScale;
        private Quaternion rootRotation;
        private Quaternion mugRotation;
        private Vector3[] dropPositions;
        private Color[] dropColors;
        private bool cached;
        private bool overflowing;
        private float overflowTime;

        private void Awake() => CacheAuthoredPose();

        private void LateUpdate()
        {
            if (!cached) CacheAuthoredPose();
            if (!cached || visualRoot == null || mug == null || exposure == null || !exposure.isActiveAndEnabled)
            {
                Hide();
                return;
            }

            float fill = Mathf.Clamp01(exposure.FillAmount);
            if (!exposure.HasAlcoholContact && fill <= 0f)
            {
                Hide();
                return;
            }

            bool showOverflow = exposure.IsOverflowing && !exposure.IsDraining && fill >= 0.999f;
            if (!showOverflow || !overflowing) overflowTime = 0f;
            overflowing = showOverflow;
            if (showOverflow) overflowTime += Time.deltaTime;

            // The root remains a prefab child, but its head offset and size do not mirror or scale with the body.
            visualRoot.position = transform.position + rootPosition;
            float angle = showOverflow
                ? Mathf.Sin(overflowTime * overflowShakeCyclesPerSecond * Mathf.PI * 2f) * overflowShakeDegrees
                : 0f;
            visualRoot.rotation = rootRotation;
            Vector3 parentScale = visualRoot.parent != null ? visualRoot.parent.lossyScale : Vector3.one;
            visualRoot.localScale = new Vector3(
                CompensateScale(rootScale.x, parentScale.x),
                CompensateScale(rootScale.y, parentScale.y),
                CompensateScale(rootScale.z, parentScale.z));
            mug.transform.localRotation = mugRotation * Quaternion.Euler(0f, 0f, angle);

            mug.GetPropertyBlock(mugProperties);
            mugProperties.SetFloat(FillAmountId, fill);
            mug.SetPropertyBlock(mugProperties);
            mug.enabled = true;
            UpdateDroplets(showOverflow);
        }

        private void CacheAuthoredPose()
        {
            if (cached || visualRoot == null || mug == null) return;
            rootPosition = visualRoot.localPosition;
            rootScale = visualRoot.localScale;
            rootRotation = visualRoot.localRotation;
            mugRotation = mug.transform.localRotation;
            mugProperties = new MaterialPropertyBlock();
            int count = overflowDroplets != null ? overflowDroplets.Length : 0;
            dropPositions = new Vector3[count];
            dropColors = new Color[count];
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer drop = overflowDroplets[i];
                if (drop == null) continue;
                dropPositions[i] = drop.transform.localPosition;
                dropColors[i] = drop.color;
            }
            cached = true;
            Hide();
        }

        private void UpdateDroplets(bool show)
        {
            if (overflowDroplets == null) return;
            for (int i = 0; i < overflowDroplets.Length; i++)
            {
                SpriteRenderer drop = overflowDroplets[i];
                if (drop == null) continue;
                drop.enabled = show;
                if (!show) continue;
                float t = Mathf.Repeat(overflowTime / Mathf.Max(0.01f, overflowDropSeconds)
                    + (float)i / overflowDroplets.Length, 1f);
                float side = dropPositions[i].x < 0f ? -1f : 1f;
                drop.transform.localPosition = dropPositions[i]
                    + new Vector3(side * overflowDropDistance * 0.3f * t, -overflowDropDistance * t * t, 0f);
                Color color = dropColors[i];
                color.a *= 1f - t;
                drop.color = color;
            }
        }

        private static float CompensateScale(float authored, float inherited)
            => Mathf.Abs(inherited) > 0.0001f ? authored / inherited : authored;

        private void Hide()
        {
            overflowing = false;
            overflowTime = 0f;
            if (mug != null) mug.enabled = false;
            if (!cached) return;
            if (visualRoot != null)
            {
                visualRoot.localPosition = rootPosition;
                visualRoot.localScale = rootScale;
                visualRoot.localRotation = rootRotation;
            }
            if (mug != null)
            {
                mug.transform.localRotation = mugRotation;
                mug.GetPropertyBlock(mugProperties);
                mugProperties.SetFloat(FillAmountId, 0f);
                mug.SetPropertyBlock(mugProperties);
            }
            if (overflowDroplets == null) return;
            for (int i = 0; i < overflowDroplets.Length; i++)
            {
                SpriteRenderer drop = overflowDroplets[i];
                if (drop == null) continue;
                drop.enabled = false;
                drop.transform.localPosition = dropPositions[i];
                drop.color = dropColors[i];
            }
        }

        private void OnDisable() => Hide();
    }
}
