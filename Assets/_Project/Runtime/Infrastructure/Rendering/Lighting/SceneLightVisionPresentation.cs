using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class SceneLightVisionPresentation : MonoBehaviour, IGlobalVisionPresentation
{
    [SerializeField] private Light2D globalLight;
    [SerializeField, Min(0f)] private float defaultIntensity = 0.2f;
    [SerializeField, Min(0f)] private float fogIntensity;
    [SerializeField] private LightRevealSource playerLight;
    [SerializeField] private Vector3 playerOffset = new(0f, 0.5f, 0f);

    private readonly List<LightRevealSource> sources = new();
    private Transform player;

    public static SceneLightVisionPresentation FindForScene(Scene scene)
    {
        foreach (var candidate in FindObjectsByType<SceneLightVisionPresentation>(FindObjectsSortMode.None))
            if (candidate.gameObject.scene == scene && candidate.isActiveAndEnabled)
                return candidate;
        return null;
    }

    public void Register(LightRevealSource source)
    {
        if (!sources.Contains(source))
            sources.Add(source);
    }

    public void Unregister(LightRevealSource source) => sources.Remove(source);

    public void BindPlayer(Transform target)
    {
        player = target;
        SyncPlayer();
    }

    public void SetFogWeight(float weight)
    {
        if (globalLight != null)
            globalLight.intensity = Mathf.Lerp(defaultIntensity, fogIntensity, Mathf.Clamp01(weight));
    }

    private void OnEnable() => SyncPlayer();
    private void LateUpdate() => SyncPlayer();

    private void SyncPlayer()
    {
        if (playerLight == null)
            return;
        if (player != null)
            playerLight.transform.position = player.position + playerOffset;
        playerLight.gameObject.SetActive(player != null && isActiveAndEnabled);
    }

    private void OnDisable()
    {
        player = null;
        SyncPlayer();
        SetFogWeight(0f);
    }

    public int CopyAreas(Bounds bounds, Vector4[] areas)
    {
        if (!isActiveAndEnabled)
            return 0;

        int count = 0;
        foreach (var source in sources)
        {
            if (source == null || !source.TryGetArea(out Vector4 area))
                continue;
            Vector3 closest = bounds.ClosestPoint(new Vector3(area.x, area.y, bounds.center.z));
            Vector2 delta = new(closest.x - area.x, closest.y - area.y);
            if (delta.sqrMagnitude > area.z * area.z)
                continue;
            if (count == areas.Length)
            {
                Debug.LogError("Too many overlapping reveal lights for one monster. Extra lights are excluded from both sprite and gauge visibility.", this);
                break;
            }
            areas[count++] = area;
        }
        return count;
    }

    public static bool Contains(Vector4 area, Vector3 point)
    {
        float x = point.x - area.x;
        float y = point.y - area.y;
        return area.z > 0f && x * x + y * y <= area.z * area.z;
    }
}
