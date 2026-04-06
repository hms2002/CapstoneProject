using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameUiScaleController : MonoBehaviour
{
    private static readonly GlobalCanvasLayer[] UiScaleLayers =
    {
        GlobalCanvasLayer.GameplayHUD,
        GlobalCanvasLayer.Dialogue,
        GlobalCanvasLayer.Popup,
        GlobalCanvasLayer.Hover,
        GlobalCanvasLayer.Prompt,
        GlobalCanvasLayer.Reward,
        GlobalCanvasLayer.DamagePopup,
        GlobalCanvasLayer.BossHUD,
    };

    private readonly Dictionary<CanvasScaler, Vector2> baseReferenceResolutions = new();
    private readonly Dictionary<CanvasScaler, float> baseScaleFactors = new();

    public void Apply(UiScalePreset preset)
    {
        float multiplier = GetUiScaleMultiplier(preset);

        for (int i = 0; i < UiScaleLayers.Length; i++)
        {
            Canvas canvas = GlobalUIRoot.GetCanvas(UiScaleLayers[i]);
            if (canvas == null)
                continue;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
                continue;

            if (!baseReferenceResolutions.ContainsKey(scaler))
                baseReferenceResolutions[scaler] = scaler.referenceResolution;

            if (!baseScaleFactors.ContainsKey(scaler))
                baseScaleFactors[scaler] = scaler.scaleFactor;

            if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                Vector2 baseReferenceResolution = baseReferenceResolutions[scaler];
                scaler.referenceResolution = baseReferenceResolution / multiplier;
            }
            else if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
            {
                float baseScaleFactor = baseScaleFactors[scaler];
                scaler.scaleFactor = baseScaleFactor * multiplier;
            }
        }
    }

    private static float GetUiScaleMultiplier(UiScalePreset preset)
    {
        return preset switch
        {
            UiScalePreset.Small => 0.9f,
            UiScalePreset.Large => 1.1f,
            _ => 1f,
        };
    }
}
