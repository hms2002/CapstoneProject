using UnityEngine;
namespace CapstonePresentation {
public interface IPresentationPrewarmTraceBackend
    {
        void RecordSpawn(GameObject prefab, bool coldSpawn);
    }
public static class PresentationPrewarmTracePlayback
    {
        private static IPresentationPrewarmTraceBackend backend;

        public static void RegisterBackend(IPresentationPrewarmTraceBackend traceBackend)
        {
            backend = traceBackend;
        }

        public static void UnregisterBackend(IPresentationPrewarmTraceBackend traceBackend)
        {
            if (ReferenceEquals(backend, traceBackend))
                backend = null;
        }

        public static void RecordSpawn(GameObject prefab, bool coldSpawn)
        {
            if (prefab != null)
                backend?.RecordSpawn(prefab, coldSpawn);
        }
    }
}
