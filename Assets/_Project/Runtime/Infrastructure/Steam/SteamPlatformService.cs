using System;
using System.IO;
using UnityEngine;
using CapstoneRuntime;
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX) && !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>App-scoped owner of the Steam API; unavailable Steam never blocks gameplay.</summary>
[DisallowMultipleComponent]
public sealed class SteamPlatformService : MonoBehaviour
{
    private static SteamPlatformService instance;
    private static bool stopping;
    private bool attempted;
    private bool initialized;

    public static bool IsInitialized => instance != null && instance.initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        stopping = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() => EnsureReady();

    private static SteamPlatformService EnsureReady()
    {
        if (stopping || !Application.isPlaying) return null;
        if (instance == null)
        {
            instance = RuntimeServiceOwnership.FindExistingService<SteamPlatformService>();
            if (instance == null)
                instance = RuntimeServiceOwnership.CreateServiceHost(nameof(SteamPlatformService))
                    .AddComponent<SteamPlatformService>();
            else
                RuntimeServiceOwnership.Adopt(instance);
        }
        instance.InitializeOnce();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
        RuntimeServiceOwnership.Adopt(this);
        InitializeOnce();
    }

    private void InitializeOnce()
    {
        if (attempted || stopping) return;
        attempted = true;
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX) && !DISABLESTEAMWORKS
        // An editor session must explicitly provide its own development App ID.
        // Steam-launched players receive the identity from Steam instead.
        if (Application.isEditor && !HasDevelopmentAppId()) return;
        try
        {
            if (!Packsize.Test() || !DllCheck.Test())
            {
                Debug.LogWarning("[Steam] Native library compatibility check failed. Continuing without Steam.");
                return;
            }
            initialized = SteamAPI.Init();
            if (!initialized) Debug.Log("[Steam] Initialization unavailable. Continuing without Steam.");
        }
        catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException || e is BadImageFormatException)
        {
            Debug.LogWarning("[Steam] Native library unavailable: " + e.Message);
        }
#endif
    }

    private static bool HasDevelopmentAppId()
    {
        try
        {
            const string path = "steam_appid.txt";
            return File.Exists(path) && uint.TryParse(File.ReadAllText(path).Trim(), out uint id) && id > 0;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    public static bool TryGetGameLanguage(out string language)
    {
        language = null;
        var service = EnsureReady();
        if (service == null || !service.initialized) return false;
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX) && !DISABLESTEAMWORKS
        language = SteamApps.GetCurrentGameLanguage();
        return !string.IsNullOrEmpty(language);
#else
        return false;
#endif
    }

    private void Update()
    {
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX) && !DISABLESTEAMWORKS
        if (initialized && !stopping) SteamAPI.RunCallbacks();
#endif
    }

    private void OnApplicationQuit() => stopping = true;

    private void OnDestroy()
    {
        if (instance != this) return;
        stopping = true;
        instance = null;
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX) && !DISABLESTEAMWORKS
        if (initialized)
        {
            initialized = false;
            SteamAPI.Shutdown();
        }
#endif
    }
}
