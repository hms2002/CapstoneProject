using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class FogSightLock : MonoBehaviour
{
    private readonly List<Light2D> globalLights = new();
    private readonly List<float> lightValues = new();

    private float endTime;
    private bool isDark;

    private void Update()
    {
        if (!isDark) return;

        if (Time.time < endTime) return;

        RestoreLight();
    }

    private void OnDestroy()
    {
        RestoreLight();
    }

    /// <summary>시야 제한 시간을 적용합니다.</summary>
    public void ApplyFog(float duration)
    {
        CacheLights();
        endTime = Time.time + Mathf.Max(0f, duration);

        if (isDark) return;

        SetDark();
    }

    /// <summary>글로벌 라이트를 찾아 저장합니다.</summary>
    private void CacheLights()
    {
        if (globalLights.Count > 0) return;

        Light2D[] lights = FindObjectsByType<Light2D>(FindObjectsSortMode.None);

        for (int i = 0; i < lights.Length; i++)
        {
            Light2D light = lights[i];
            if (light == null || light.lightType != Light2D.LightType.Global) continue;

            globalLights.Add(light);
            lightValues.Add(light.intensity);
        }
    }

    /// <summary>글로벌 라이트를 꺼서 시야를 좁힙니다.</summary>
    private void SetDark()
    {
        for (int i = 0; i < globalLights.Count; i++)
        {
            Light2D light = globalLights[i];
            if (light == null) continue;

            light.intensity = 0f;
        }

        isDark = true;
    }

    /// <summary>글로벌 라이트 밝기를 되돌립니다.</summary>
    private void RestoreLight()
    {
        if (!isDark) return;

        for (int i = 0; i < globalLights.Count; i++)
        {
            Light2D light = globalLights[i];
            if (light == null) continue;

            light.intensity = lightValues[i];
        }

        isDark = false;
    }
}
