using UnityEngine;

namespace UnityGAS
{
    /// <summary>Projects player alcohol exposure into the authored head mug and overflow droplets.</summary>
    [DisallowMultipleComponent]
    public sealed class AlcoholExposurePlayerVisual : MonoBehaviour
    {
        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
        private static readonly int StatusActiveId = Shader.PropertyToID("_StatusActive");
        private const float MugPixelsPerUnit = 32f;
        private enum MugState { Charging, Confused, Buffed }

        [SerializeField] private PlayerAlcoholExposure exposure;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer mug;
        [SerializeField] private Sprite confusedMug;
        [SerializeField] private Sprite buffedMug;
        [SerializeField] private SpriteRenderer[] overflowDroplets;
        [SerializeField, Min(0f)] private float overflowShakeDegrees = 9f;
        [SerializeField, Min(0f)] private float overflowShakeCyclesPerSecond = 3f;
        [SerializeField, Min(0.01f)] private float overflowDropSeconds = 0.6f;
        [SerializeField, Min(0f)] private float overflowDropDistance = 0.25f;

        private MaterialPropertyBlock mugProperties;
        private Vector3 rootPosition;
        private Vector3 rootScale;
        private Quaternion rootRotation;
        private Vector3 mugPosition;
        private Sprite chargingMug;
        private MugState displayedState;
        private float stateTime;
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
            MugState state = MugState.Charging;
            if (exposure.HasActiveConfusion) state = MugState.Confused;
            else if (exposure.HasActiveSpeedBuff) state = MugState.Buffed;
            if (!exposure.HasAlcoholContact && fill <= 0f && state == MugState.Charging)
            {
                if (mug.enabled) Hide();
                return;
            }

            if (state != displayedState)
            {
                displayedState = state;
                stateTime = 0f;
                mug.sprite = state switch
                {
                    MugState.Confused => confusedMug,
                    MugState.Buffed => buffedMug,
                    _ => chargingMug
                };
            }
            else stateTime += Time.deltaTime;

            bool showOverflow = exposure.IsOverflowing && !exposure.IsDraining && fill >= 0.999f;
            if (!showOverflow || !overflowing) overflowTime = 0f;
            overflowing = showOverflow;
            if (showOverflow) overflowTime += Time.deltaTime;

            // The root remains a prefab child, but its head offset and size do not mirror or scale with the body.
            visualRoot.position = transform.position + rootPosition;
            visualRoot.rotation = rootRotation;
            Vector3 parentScale = visualRoot.parent != null ? visualRoot.parent.lossyScale : Vector3.one;
            visualRoot.localScale = new Vector3(
                CompensateScale(rootScale.x, parentScale.x),
                CompensateScale(rootScale.y, parentScale.y),
                CompensateScale(rootScale.z, parentScale.z));
            // Project the authored tilt onto horizontal travel at half the mug height.
            // Whole sprite pixels keep the outline intact through overflow and confusion drain.
            float swayPixels = 0f;
            if (state == MugState.Confused || (state == MugState.Charging && showOverflow))
            {
                float swayTime = state == MugState.Confused ? stateTime : overflowTime;
                float phase = swayTime * overflowShakeCyclesPerSecond * Mathf.PI * 2f;
                float tilt = Mathf.Sin(phase) * overflowShakeDegrees * Mathf.Deg2Rad;
                swayPixels = Mathf.Round(0.5f * MugPixelsPerUnit * Mathf.Sin(tilt));
            }
            float liftPixels = state == MugState.Charging ? 0f : Mathf.Round(2f * Mathf.Clamp01(1f - stateTime / 0.18f));
            mug.transform.localPosition = mugPosition + new Vector3(swayPixels, liftPixels, 0f) / MugPixelsPerUnit;

            mug.GetPropertyBlock(mugProperties);
            mugProperties.SetFloat(FillAmountId, fill);
            mugProperties.SetFloat(StatusActiveId, state == MugState.Charging ? 0f : 1f);
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
            mugPosition = mug.transform.localPosition;
            chargingMug = mug.sprite;
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
            displayedState = MugState.Charging;
            stateTime = 0f;
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
                mug.sprite = chargingMug;
                mug.transform.localPosition = mugPosition;
                mug.GetPropertyBlock(mugProperties);
                mugProperties.SetFloat(FillAmountId, 0f);
                mugProperties.SetFloat(StatusActiveId, 0f);
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
