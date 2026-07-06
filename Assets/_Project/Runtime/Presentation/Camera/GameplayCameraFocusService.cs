using System.Collections;
using Cainos.PixelArtTopDown_Basic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 책임 : Core의 임시 카메라 포커스 계약을 현재 Cinemachine gameplay camera rig 조작으로 수행한다.
/// </summary>
public sealed class GameplayCameraFocusService :
    IGameplayCameraFocusBackend,
    IGameplayCameraViewBackend,
    IGameplayCameraMapZoomBackend
{
    private static readonly GameplayCameraFocusService Instance = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterBackend()
    {
        GameplayCameraFocusPlayback.RegisterBackend(Instance);
        GameplayCameraViewQuery.RegisterBackend(Instance);
        GameplayCameraMapZoomPlayback.RegisterBackend(Instance);
    }

    public IGameplayCameraFocusSession Capture(Component owner)
    {
        return new GameplayCameraFocusSession(owner);
    }

    public Camera GetMainCamera()
    {
        return CameraBootstrap.GetMainCamera();
    }

    public IGameplayCameraMapZoomSession Capture()
    {
        return new GameplayCameraMapZoomSession();
    }

    /// <summary>
    /// 책임 : 한 컷씬 동안 gameplay camera의 Follow/LookAt/priority/lens/legacy-follow/brain 상태를 보관하고 복구한다.
    /// </summary>
    private sealed class GameplayCameraFocusSession : IGameplayCameraFocusSession
    {
        private readonly Component owner;
        private readonly CinemachineCamera gameplayCamera;
        private readonly CinemachineBrain cameraBrain;
        private readonly CameraFollow legacyFollowCamera;
        private readonly Transform cachedCameraLookAt;
        private readonly int cachedCameraPriority;
        private readonly float cachedCameraOrthographicSize;
        private readonly bool cachedLegacyFollowEnabled;
        private readonly bool cachedBrainIgnoreTimeScale;

        public GameplayCameraFocusSession(Component owner)
        {
            this.owner = owner;

            CameraBootstrap.EnsureRuntimeRigForCurrentScene();
            gameplayCamera = CameraBootstrap.GetPlayerCamera();
            cameraBrain = CameraBootstrap.GetBrain();
            legacyFollowCamera = CameraBootstrap.GetLegacyFollow();

            if (gameplayCamera != null)
            {
                CachedFollow = gameplayCamera.Follow;
                cachedCameraLookAt = gameplayCamera.LookAt;
                cachedCameraPriority = gameplayCamera.Priority;
                cachedCameraOrthographicSize = GetCameraOrthographicSize(gameplayCamera, 6f);
                HasOrthographicSize = true;
            }

            if (legacyFollowCamera != null)
                cachedLegacyFollowEnabled = legacyFollowCamera.enabled;

            if (cameraBrain != null)
                cachedBrainIgnoreTimeScale = cameraBrain.IgnoreTimeScale;
        }

        public Transform CachedFollow { get; }

        public bool HasOrthographicSize { get; }

        public float CachedOrthographicSize => cachedCameraOrthographicSize;

        public float CurrentOrthographicSize =>
            GetCameraOrthographicSize(gameplayCamera, cachedCameraOrthographicSize);

        public Vector3 CurrentCenter
        {
            get
            {
                Camera mainCamera = CameraBootstrap.GetMainCamera();
                if (mainCamera != null)
                    return new Vector3(mainCamera.transform.position.x, mainCamera.transform.position.y, 0f);

                if (gameplayCamera != null)
                    return new Vector3(gameplayCamera.transform.position.x, gameplayCamera.transform.position.y, 0f);

                if (CachedFollow != null)
                    return CachedFollow.position;

                return owner != null ? owner.transform.position : Vector3.zero;
            }
        }

        public void SetTarget(Transform target)
        {
            if (target == null)
                return;

            if (cameraBrain != null)
                cameraBrain.IgnoreTimeScale = true;

            if (legacyFollowCamera != null)
                legacyFollowCamera.enabled = false;

            if (gameplayCamera == null)
                return;

            gameplayCamera.Follow = target;
            gameplayCamera.LookAt = target;
        }

        public void SnapToTarget(Transform target)
        {
            if (target == null)
                return;

            CameraBootstrap.CenterGameplayCameraOn(target);
        }

        public void SetOrthographicSize(float orthographicSize)
        {
            SetCameraOrthographicSize(gameplayCamera, orthographicSize);
        }

        public IEnumerator WaitForSettle(Transform target)
        {
            yield return CameraCinematicWaitUtility.WaitForCameraSettle(cameraBrain, null, target);
        }

        public void Restore(Transform preferredTarget)
        {
            Transform restoreFollow = preferredTarget != null ? preferredTarget : CachedFollow;
            Transform restoreLookAt = preferredTarget != null ? preferredTarget : cachedCameraLookAt;

            if (gameplayCamera != null)
            {
                gameplayCamera.Follow = restoreFollow;
                gameplayCamera.LookAt = restoreLookAt;
                gameplayCamera.Priority = cachedCameraPriority;

                if (HasOrthographicSize)
                    SetCameraOrthographicSize(gameplayCamera, cachedCameraOrthographicSize);
            }

            if (legacyFollowCamera != null)
            {
                if (restoreFollow != null)
                    legacyFollowCamera.BindTarget(restoreFollow, snap: false);

                legacyFollowCamera.enabled = cachedLegacyFollowEnabled;
            }

            if (cameraBrain != null)
                cameraBrain.IgnoreTimeScale = cachedBrainIgnoreTimeScale;
        }

        private static float GetCameraOrthographicSize(CinemachineCamera camera, float fallbackOrthographicSize)
        {
            if (camera == null)
                return Mathf.Max(0.01f, fallbackOrthographicSize);

            LensSettings lens = camera.Lens;
            return Mathf.Max(0.01f, lens.OrthographicSize);
        }

        private static void SetCameraOrthographicSize(CinemachineCamera camera, float orthographicSize)
        {
            if (camera == null)
                return;

            LensSettings lens = camera.Lens;
            lens.OrthographicSize = Mathf.Max(0.01f, orthographicSize);
            camera.Lens = lens;
        }
    }

    /// <summary>
    /// 책임 : 시연 지도 줌 동안 gameplay camera의 Follow/LookAt/priority/lens/위치 상태를 보관하고 복구한다.
    /// </summary>
    private sealed class GameplayCameraMapZoomSession : IGameplayCameraMapZoomSession
    {
        private readonly CinemachineCamera gameplayCamera;
        private readonly Camera mainCamera;
        private readonly Transform cachedFollow;
        private readonly Transform cachedLookAt;
        private readonly int cachedPriority;
        private readonly float cachedOrthographicSize;
        private readonly Vector3 cachedPosition;
        private readonly Quaternion cachedRotation;

        public GameplayCameraMapZoomSession()
        {
            CameraBootstrap.EnsureRuntimeRigForCurrentScene();

            gameplayCamera = CameraBootstrap.GetPlayerCamera();
            mainCamera = CameraBootstrap.GetMainCamera();

            if (gameplayCamera == null)
                return;

            cachedFollow = gameplayCamera.Follow;
            cachedLookAt = gameplayCamera.LookAt;
            cachedPriority = gameplayCamera.Priority;
            cachedOrthographicSize = GetCameraOrthographicSize(gameplayCamera, 6f);
            cachedPosition = gameplayCamera.transform.position;
            cachedRotation = gameplayCamera.transform.rotation;
        }

        public bool IsValid => gameplayCamera != null && gameplayCamera.gameObject.activeInHierarchy;

        public float Aspect
        {
            get
            {
                if (mainCamera != null && mainCamera.aspect > 0f)
                    return mainCamera.aspect;

                if (Screen.height > 0)
                    return Mathf.Max(0.01f, (float)Screen.width / Screen.height);

                return 16f / 9f;
            }
        }

        public float CurrentOrthographicSize =>
            GetCameraOrthographicSize(gameplayCamera, cachedOrthographicSize);

        public float CachedOrthographicSize => cachedOrthographicSize;

        public Vector2 CurrentCenter
        {
            get
            {
                if (mainCamera != null)
                    return mainCamera.transform.position;

                return gameplayCamera != null
                    ? new Vector2(gameplayCamera.transform.position.x, gameplayCamera.transform.position.y)
                    : Vector2.zero;
            }
        }

        public void Begin(int minimumPriority)
        {
            if (gameplayCamera == null)
                return;

            gameplayCamera.Follow = null;
            gameplayCamera.LookAt = null;
            gameplayCamera.Priority = Mathf.Max(gameplayCamera.Priority, minimumPriority);
        }

        public void Apply(Vector2 center, float orthographicSize)
        {
            if (gameplayCamera == null)
                return;

            SetCameraOrthographicSize(gameplayCamera, orthographicSize);

            Vector3 position = gameplayCamera.transform.position;
            position.x = center.x;
            position.y = center.y;
            gameplayCamera.ForceCameraPosition(position, gameplayCamera.transform.rotation);
        }

        public Vector2 ResolveRestoreCenter()
        {
            if (cachedFollow != null)
                return cachedFollow.position;

            if (cachedLookAt != null)
                return cachedLookAt.position;

            return new Vector2(cachedPosition.x, cachedPosition.y);
        }

        public void Restore()
        {
            if (gameplayCamera == null)
                return;

            gameplayCamera.Priority = cachedPriority;
            gameplayCamera.Follow = cachedFollow;
            gameplayCamera.LookAt = cachedLookAt;
            SetCameraOrthographicSize(gameplayCamera, cachedOrthographicSize);

            Vector2 restoreCenter = ResolveRestoreCenter();
            Vector3 restorePosition = cachedPosition;
            restorePosition.x = restoreCenter.x;
            restorePosition.y = restoreCenter.y;
            gameplayCamera.ForceCameraPosition(restorePosition, cachedRotation);
        }

        private static float GetCameraOrthographicSize(CinemachineCamera camera, float fallbackOrthographicSize)
        {
            if (camera == null)
                return Mathf.Max(0.01f, fallbackOrthographicSize);

            LensSettings lens = camera.Lens;
            return Mathf.Max(0.01f, lens.OrthographicSize);
        }

        private static void SetCameraOrthographicSize(CinemachineCamera camera, float orthographicSize)
        {
            if (camera == null)
                return;

            LensSettings lens = camera.Lens;
            lens.OrthographicSize = Mathf.Max(0.01f, orthographicSize);
            camera.Lens = lens;
        }
    }
}
