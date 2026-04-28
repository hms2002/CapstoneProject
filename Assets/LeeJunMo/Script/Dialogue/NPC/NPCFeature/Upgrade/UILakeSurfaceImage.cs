using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UILakeSurfaceImage : Image
{
    [NonSerialized] private Material previewMaterialOverride;

    public Material PreviewMaterialOverride
    {
        get => previewMaterialOverride;
        set
        {
            if (previewMaterialOverride == value)
                return;

            previewMaterialOverride = value;
            SetMaterialDirty();
        }
    }

    public override Material materialForRendering =>
        previewMaterialOverride != null ? previewMaterialOverride : base.materialForRendering;

    protected override void OnDisable()
    {
        previewMaterialOverride = null;
        base.OnDisable();
    }
}
