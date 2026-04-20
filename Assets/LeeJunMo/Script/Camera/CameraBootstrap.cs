using System;
using System.Collections.Generic;
using System.Linq;
using Cainos.PixelArtTopDown_Basic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class CameraBootstrap : MonoBehaviour
{
    private const string TitleSceneName = "TitleScene";

    // 이 클래스의 책임:
    // 런타임 카메라 리그를 DDOL로 유지하고, 씬 전환 중에도 메인 카메라/플레이어 카메라/추적 바인딩을 일관되게 보장한다.

    public const int DefaultImpulseChannel = 1;

    public static CameraBootstrap Instance { get; private set; }

    [Header("Runtime Rig")]
    [SerializeField] private Camera runtimeMainCamera;
    [SerializeField] private CameraFollow runtimeLegacyFollow;
    [SerializeField] private CinemachineBrain runtimeBrain;
    [SerializeField] private CinemachineCamera runtimePlayerCam;
    [SerializeField] private int persistentPlayerPriority = 100;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapBeforeFirstSceneLoad()
    {
        if (IsTitleScene(SceneManager.GetActiveScene()))
            return;

        EnsureInstance();
    }

    public static CameraBootstrap EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        CameraBootstrap existing = FindFirstObjectByType<CameraBootstrap>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject bootstrapObject = new GameObject("CameraBootstrap");
        Instance = bootstrapObject.AddComponent<CameraBootstrap>();
        return Instance;
    }

    public static CameraBootstrap EnsureRuntimeRigForCurrentScene()
    {
        CameraBootstrap bootstrap = EnsureInstance();
        bootstrap.EnsureRuntimeRig();
        return bootstrap;
    }

    public static Camera GetMainCamera()
    {
        return EnsureRuntimeRigForCurrentScene().runtimeMainCamera;
    }

    public static CameraFollow GetLegacyFollow()
    {
        return EnsureRuntimeRigForCurrentScene().runtimeLegacyFollow;
    }

    public static CinemachineBrain GetBrain()
    {
        return EnsureRuntimeRigForCurrentScene().runtimeBrain;
    }

    public static CinemachineCamera GetPlayerCamera()
    {
        return EnsureRuntimeRigForCurrentScene().runtimePlayerCam;
    }

    public static CinemachineImpulseSource EnsureImpulseSource(GameObject owner)
    {
        if (owner == null)
            return null;

        CinemachineImpulseSource source = owner.GetComponent<CinemachineImpulseSource>();
        bool created = false;
        if (source == null)
        {
            source = owner.AddComponent<CinemachineImpulseSource>();
            created = true;
        }

        ConfigureImpulseSource(source, created);
        return source;
    }

    public static void ConfigureImpulseSource(CinemachineImpulseSource source, bool applyFullDefaults = false)
    {
        if (source == null)
            return;

        source.ImpulseDefinition ??= new CinemachineImpulseDefinition();
        if (applyFullDefaults)
        {
            source.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            source.ImpulseDefinition.ImpulseDuration = 0.2f;
            source.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            source.ImpulseDefinition.DissipationDistance = 100f;
            source.ImpulseDefinition.DissipationRate = 0.25f;
            source.ImpulseDefinition.PropagationSpeed = 343f;
            source.DefaultVelocity = Vector3.down;
        }

        source.ImpulseDefinition.ImpulseChannel = DefaultImpulseChannel;
        source.ImpulseDefinition.OnValidate();
    }

    public static CinemachineImpulseListener EnsureImpulseListener(GameObject owner)
    {
        if (owner == null)
            return null;

        CinemachineImpulseListener listener = owner.GetComponent<CinemachineImpulseListener>();
        if (listener == null)
            listener = owner.AddComponent<CinemachineImpulseListener>();

        ConfigureImpulseListener(listener);
        return listener;
    }

    public static void ConfigureImpulseListener(CinemachineImpulseListener listener)
    {
        if (listener == null)
            return;

        listener.ApplyAfter = CinemachineCore.Stage.Noise;
        listener.ChannelMask = -1;
        listener.Gain = 1f;
        listener.Use2DDistance = true;
        listener.UseCameraSpace = true;
        listener.SignalCombinationMode = CinemachineImpulseListener.SignalCombinationModes.Additive;

        var reaction = listener.ReactionSettings;
        if (reaction.AmplitudeGain <= 0f)
            reaction.AmplitudeGain = 1f;
        if (reaction.FrequencyGain <= 0f)
            reaction.FrequencyGain = 1f;
        if (reaction.Duration <= 0f)
            reaction.Duration = 1f;
        listener.ReactionSettings = reaction;
    }

    public static CinemachineCamera FindSceneBossCamera(Scene scene)
    {
        CinemachineCamera[] cameras = FindSceneComponents<CinemachineCamera>(scene, includeInactive: true);
        return cameras.FirstOrDefault(camera =>
            camera != null &&
            string.Equals(camera.name, "BossCam", StringComparison.Ordinal));
    }

    public static bool IsBossScene(Scene scene)
    {
        if (!scene.IsValid())
            return false;

        return FindSceneComponents<BossEncounterDirector>(scene, includeInactive: true).Length > 0
            || FindSceneComponents<BossTalkManager>(scene, includeInactive: true).Length > 0;
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
        EnsureRuntimeRig();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered += HandlePlayerUnregistered;
    }

    private void OnDisable()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered -= HandlePlayerUnregistered;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureRuntimeRig();
    }

    private void HandlePlayerRegistered(PlayerInteractor2D player)
    {
        if (player == null)
            return;

        EnsureRuntimeRig();
        runtimeLegacyFollow?.BindTarget(player.transform, true);
    }

    private void HandlePlayerUnregistered(PlayerInteractor2D player)
    {
        if (player == null || runtimePlayerCam == null)
            return;

        if (runtimePlayerCam.Follow == player.transform)
            runtimePlayerCam.Follow = null;

        if (runtimePlayerCam.LookAt == player.transform)
            runtimePlayerCam.LookAt = null;
    }

    private void EnsureRuntimeRig()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (IsTitleScene(activeScene))
        {
            ReleaseRuntimeRigForTitleScene(activeScene);
            return;
        }

        runtimeMainCamera = ResolveOrCreateMainCamera(activeScene);
        runtimeBrain = runtimeMainCamera != null ? runtimeMainCamera.GetComponent<CinemachineBrain>() : null;
        runtimeLegacyFollow = runtimeMainCamera != null ? runtimeMainCamera.GetComponent<CameraFollow>() : null;

        runtimePlayerCam = ResolveOrCreatePlayerCamera(activeScene);
        if (runtimePlayerCam != null)
            runtimePlayerCam.Priority = persistentPlayerPriority;

        if (runtimeLegacyFollow != null && runtimePlayerCam != null)
            runtimeLegacyFollow.SetControlledCamera(runtimePlayerCam, rebindCurrentTarget: true);

        Transform playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        if (playerTransform != null)
            runtimeLegacyFollow?.BindTarget(playerTransform, true);

        DisableDuplicateScenePlayerCams(activeScene);
        DisableDuplicateSceneMainCameras(activeScene);
    }

    private Camera ResolveOrCreateMainCamera(Scene scene)
    {
        if (runtimeMainCamera != null)
        {
            if (!runtimeMainCamera.gameObject.activeSelf)
                runtimeMainCamera.gameObject.SetActive(true);

            return runtimeMainCamera;
        }

        Camera candidate = Camera.main;
        if (candidate == null || candidate.GetComponent<CinemachineBrain>() == null)
        {
            candidate = FindSceneComponents<Camera>(scene, includeInactive: true)
                .FirstOrDefault(camera => camera != null &&
                                          (camera.GetComponent<CinemachineBrain>() != null ||
                                           string.Equals(camera.name, "Main Camera", StringComparison.Ordinal)));
        }

        if (candidate == null)
            candidate = CreateFallbackMainCamera();

        AdoptRuntimeObject(candidate.transform);
        EnsureMainCameraComponents(candidate.gameObject);
        return candidate;
    }

    private CinemachineCamera ResolveOrCreatePlayerCamera(Scene scene)
    {
        if (runtimePlayerCam != null)
        {
            if (!runtimePlayerCam.gameObject.activeSelf)
                runtimePlayerCam.gameObject.SetActive(true);

            return runtimePlayerCam;
        }

        CinemachineCamera candidate = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(camera =>
                camera != null &&
                string.Equals(camera.name, "PlayerCam", StringComparison.Ordinal) &&
                camera != FindSceneBossCamera(scene));

        if (candidate == null)
            candidate = CreateFallbackPlayerCamera();

        AdoptRuntimeObject(candidate.transform);
        EnsurePlayerCameraComponents(candidate.gameObject);
        return candidate;
    }

    private Camera CreateFallbackMainCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        Camera cameraComponent = cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);

        cameraComponent.clearFlags = CameraClearFlags.SolidColor;
        cameraComponent.backgroundColor = new Color(0.09411765f, 0.078431375f, 0.14509805f, 1f);
        cameraComponent.orthographic = true;
        cameraComponent.orthographicSize = 10f;
        cameraComponent.nearClipPlane = 0.1f;
        cameraComponent.farClipPlane = 5000f;

        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<CinemachineBrain>();
        cameraObject.AddComponent<CameraFollow>();
        EnsureImpulseSource(cameraObject);
        return cameraComponent;
    }

    private static CinemachineCamera CreateFallbackPlayerCamera()
    {
        GameObject playerCameraObject = new GameObject("PlayerCam");
        CinemachineCamera camera = playerCameraObject.AddComponent<CinemachineCamera>();
        CinemachineFollow follow = playerCameraObject.AddComponent<CinemachineFollow>();

        camera.Priority = 100;
        camera.Lens.FieldOfView = 40f;
        camera.Lens.OrthographicSize = 6f;
        camera.Lens.NearClipPlane = 0.1f;
        camera.Lens.FarClipPlane = 5000f;

        follow.FollowOffset = new Vector3(0f, 0f, -10f);
        var trackerSettings = follow.TrackerSettings;
        trackerSettings.PositionDamping = Vector3.one;
        follow.TrackerSettings = trackerSettings;

        return camera;
    }

    private void EnsureMainCameraComponents(GameObject cameraObject)
    {
        if (cameraObject == null)
            return;

        if (!cameraObject.CompareTag("MainCamera"))
            cameraObject.tag = "MainCamera";

        if (cameraObject.GetComponent<AudioListener>() == null)
            cameraObject.AddComponent<AudioListener>();

        if (cameraObject.GetComponent<CinemachineBrain>() == null)
            cameraObject.AddComponent<CinemachineBrain>();

        if (cameraObject.GetComponent<CameraFollow>() == null)
            cameraObject.AddComponent<CameraFollow>();

        EnsureImpulseSource(cameraObject);
    }

    private static void EnsurePlayerCameraComponents(GameObject cameraObject)
    {
        if (cameraObject == null)
            return;

        if (cameraObject.GetComponent<CinemachineFollow>() == null)
            cameraObject.AddComponent<CinemachineFollow>();

        EnsureImpulseListener(cameraObject);
    }

    private void AdoptRuntimeObject(Transform target)
    {
        if (target == null)
            return;

        if (target.parent == transform)
            return;

        target.SetParent(transform, true);
    }

    private void DisableDuplicateScenePlayerCams(Scene scene)
    {
        CinemachineCamera[] cameras = FindSceneComponents<CinemachineCamera>(scene, includeInactive: true);
        foreach (CinemachineCamera camera in cameras)
        {
            if (camera == null || camera == runtimePlayerCam)
                continue;

            if (!string.Equals(camera.name, "PlayerCam", StringComparison.Ordinal))
                continue;

            camera.gameObject.SetActive(false);
        }
    }

    private void DisableDuplicateSceneMainCameras(Scene scene)
    {
        Camera[] cameras = FindSceneComponents<Camera>(scene, includeInactive: true);
        foreach (Camera camera in cameras)
        {
            if (camera == null || camera == runtimeMainCamera)
                continue;

            if (camera.GetComponent<CinemachineBrain>() == null &&
                !string.Equals(camera.name, "Main Camera", StringComparison.Ordinal))
            {
                continue;
            }

            camera.gameObject.SetActive(false);
        }
    }

    private void ReleaseRuntimeRigForTitleScene(Scene scene)
    {
        if (runtimePlayerCam != null)
            runtimePlayerCam.gameObject.SetActive(false);

        if (runtimeMainCamera != null)
            runtimeMainCamera.gameObject.SetActive(false);

        Camera[] sceneCameras = FindSceneComponents<Camera>(scene, includeInactive: true);
        for (int i = 0; i < sceneCameras.Length; i++)
        {
            Camera sceneCamera = sceneCameras[i];
            if (sceneCamera == null)
                continue;

            sceneCamera.gameObject.SetActive(true);
        }
    }

    private static bool IsTitleScene(Scene scene)
    {
        return scene.IsValid() &&
               string.Equals(scene.name, TitleSceneName, StringComparison.OrdinalIgnoreCase);
    }

    private static T[] FindSceneComponents<T>(Scene scene, bool includeInactive) where T : Component
    {
        if (!scene.IsValid())
            return Array.Empty<T>();

        List<T> results = new List<T>();
        GameObject[] rootObjects = scene.GetRootGameObjects();

        for (int i = 0; i < rootObjects.Length; i++)
        {
            if (rootObjects[i] == null)
                continue;

            results.AddRange(rootObjects[i].GetComponentsInChildren<T>(includeInactive));
        }

        return results.Where(component => component != null && component.gameObject.scene == scene).ToArray();
    }
}
