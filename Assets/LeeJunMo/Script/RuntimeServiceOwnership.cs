using UnityEngine;

namespace CapstoneRuntime
{
    public static class RuntimeServiceOwnership
    {
        private const string RuntimeServicesRootName = "[RuntimeServices]";
        private static Transform cachedRoot;

        public static GameObject CreateServiceHost(string serviceName)
        {
            Transform root = EnsureRoot();
            GameObject host = new GameObject(serviceName);
            host.transform.SetParent(root, false);
            return host;
        }

        public static void Adopt(Component service)
        {
            if (service == null)
                return;

            Transform root = EnsureRoot();
            if (service.transform.parent != root)
                service.transform.SetParent(root, false);
        }

        public static T FindExistingService<T>() where T : Component
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindAnyObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }

        private static Transform EnsureRoot()
        {
            if (cachedRoot != null)
                return cachedRoot;

            GameObject existing = GameObject.Find(RuntimeServicesRootName);
            if (existing != null)
            {
                cachedRoot = existing.transform;
                Object.DontDestroyOnLoad(existing);
                return cachedRoot;
            }

            GameObject root = new GameObject(RuntimeServicesRootName);
            Object.DontDestroyOnLoad(root);
            cachedRoot = root.transform;
            return cachedRoot;
        }
    }
}
