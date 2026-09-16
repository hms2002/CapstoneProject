using System.Diagnostics;
using UnityEngine;

namespace CapstoneDiagnostics
{
    /// <summary>Emits project informational and warning diagnostics only in the editor, excluding argument evaluation from player builds.</summary>
    public static class EditorOnlyLog
    {
        [Conditional("UNITY_EDITOR")]
        public static void Log(object message) => UnityEngine.Debug.Log(message);

        [Conditional("UNITY_EDITOR")]
        public static void Log(object message, Object context) => UnityEngine.Debug.Log(message, context);

        [Conditional("UNITY_EDITOR")]
        public static void LogWarning(object message) => UnityEngine.Debug.LogWarning(message);

        [Conditional("UNITY_EDITOR")]
        public static void LogWarning(object message, Object context) => UnityEngine.Debug.LogWarning(message, context);

        [Conditional("UNITY_EDITOR")]
        public static void LogWithoutStacktrace(Object context, string format, params object[] args)
            => UnityEngine.Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, context, format, args);
    }
}
