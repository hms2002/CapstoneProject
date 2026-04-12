using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[DefaultExecutionOrder(-850)]
[DisallowMultipleComponent]
public sealed class CameraShakeService : MonoBehaviour
{
    public static CameraShakeService Instance { get; private set; }

    private static bool s_isQuitting;

    private readonly Dictionary<int, float> lastEmitTimeBySource = new();
    private float lastGlobalEmitTime = -999f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        EnsureInstance();
    }

    public static CameraShakeService EnsureInstance()
    {
        if (Instance != null)
            return Instance;

#if UNITY_2023_1_OR_NEWER
        CameraShakeService existing = FindAnyObjectByType<CameraShakeService>();
#else
        CameraShakeService existing = FindObjectOfType<CameraShakeService>();
#endif
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject root = new GameObject(nameof(CameraShakeService));
        return root.AddComponent<CameraShakeService>();
    }

    public static bool Play(in CameraShakeRequest request)
    {
        CameraShakeService service = EnsureInstance();
        return service != null && service.TryPlay(request);
    }

    public static bool Play(
        float amplitude,
        Vector3 direction,
        GameObject source = null,
        float minIntervalSeconds = 0f,
        string debugReason = null,
        bool ignoreScreenShakeSetting = false)
    {
        return Play(new CameraShakeRequest(
            amplitude,
            direction,
            source,
            minIntervalSeconds,
            debugReason,
            ignoreScreenShakeSetting));
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    public bool TryPlay(in CameraShakeRequest request)
    {
        if (request.Amplitude <= 0f)
            return false;

        if (!request.IgnoreScreenShakeSetting && !GameSettingsService.IsScreenShakeEnabled())
            return false;

        float now = Time.unscaledTime;
        if (!CanEmit(request, now))
            return false;

        Camera camera = CameraBootstrap.GetMainCamera();
        if (camera == null)
            camera = Camera.main;
        if (camera == null)
            return false;

        CinemachineImpulseSource impulseSource = CameraBootstrap.EnsureImpulseSource(camera.gameObject);
        if (impulseSource == null)
            return false;

        Vector3 direction = request.Direction;
        direction.z = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.up;
        else
            direction.Normalize();

        impulseSource.GenerateImpulse(direction * request.Amplitude);
        RecordEmit(request, now);
        return true;
    }

    private bool CanEmit(in CameraShakeRequest request, float now)
    {
        if (request.MinIntervalSeconds <= 0f)
            return true;

        if (request.Source != null)
        {
            int key = request.Source.GetInstanceID();
            return !lastEmitTimeBySource.TryGetValue(key, out float lastEmitTime)
                || now - lastEmitTime >= request.MinIntervalSeconds;
        }

        return now - lastGlobalEmitTime >= request.MinIntervalSeconds;
    }

    private void RecordEmit(in CameraShakeRequest request, float now)
    {
        if (request.Source != null)
        {
            lastEmitTimeBySource[request.Source.GetInstanceID()] = now;
            return;
        }

        lastGlobalEmitTime = now;
    }
}
