using UnityEngine;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(-90)]
[DisallowMultipleComponent]
public sealed class LightRevealSource : MonoBehaviour
{
    [SerializeField] private Light2D sourceLight;
    [SerializeField, Min(0.01f)] private float revealRadius = 3f;
    [SerializeField] private Transform animatedArea;
    [SerializeField] private Sprite areaSprite;
    [SerializeField, Min(0.001f)] private float localAreaRadius = 0.32f;
    [SerializeField] private CandlestickSeal seal;
    [SerializeField] private SpriteMask[] legacyMasks = System.Array.Empty<SpriteMask>();

    private SceneLightVisionPresentation owner;
    private bool[] maskStates;
    private bool lightWasEnabled;

    private void OnEnable()
    {
        owner = SceneLightVisionPresentation.FindForScene(gameObject.scene);
        if (owner == null || sourceLight == null)
            return;

        lightWasEnabled = sourceLight.enabled;
        maskStates = new bool[legacyMasks.Length];
        for (int i = 0; i < legacyMasks.Length; i++)
        {
            if (legacyMasks[i] == null) continue;
            maskStates[i] = legacyMasks[i].enabled;
            legacyMasks[i].enabled = false;
        }
        owner.Register(this);
        if (seal != null)
            seal.SealChanged += OnSealChanged;
        SyncLight();
    }

    private void LateUpdate() => SyncLight();
    private void OnSealChanged(bool _) => SyncLight();

    private bool IsEmitting => owner != null && owner.isActiveAndEnabled && isActiveAndEnabled &&
        sourceLight != null && sourceLight.gameObject.activeInHierarchy &&
        (seal == null || !seal.IsSealed) &&
        (animatedArea == null || animatedArea.gameObject.activeInHierarchy);

    private void SyncLight()
    {
        if (owner == null || sourceLight == null)
            return;
        sourceLight.enabled = IsEmitting;
        if (animatedArea != null)
        {
            sourceLight.transform.position = animatedArea.position;
            sourceLight.pointLightOuterRadius = CurrentRadius;
            sourceLight.pointLightInnerRadius = CurrentRadius * 0.75f;
        }
    }

    private float CurrentRadius => animatedArea == null ? revealRadius :
        (areaSprite != null ? areaSprite.bounds.extents.x : localAreaRadius) * Mathf.Abs(animatedArea.lossyScale.x);

    public bool TryGetArea(out Vector4 area)
    {
        Vector3 position = animatedArea != null ? animatedArea.position :
            sourceLight != null ? sourceLight.transform.position : transform.position;
        area = new Vector4(position.x, position.y, CurrentRadius, 0f);
        return IsEmitting && sourceLight.enabled && sourceLight.intensity > 0f && area.z > 0f;
    }

    private void OnDisable()
    {
        if (maskStates == null)
            return;
        if (owner != null)
            owner.Unregister(this);
        if (seal != null)
            seal.SealChanged -= OnSealChanged;
        if (sourceLight != null)
            sourceLight.enabled = lightWasEnabled;
        if (maskStates != null)
            for (int i = 0; i < legacyMasks.Length; i++)
                if (legacyMasks[i] != null) legacyMasks[i].enabled = maskStates[i];
        owner = null;
        maskStates = null;
    }
}
