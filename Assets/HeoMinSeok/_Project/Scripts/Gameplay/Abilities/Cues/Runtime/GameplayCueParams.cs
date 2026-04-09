using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Lightweight runtime payload for gameplay cue execution.
    /// </summary>
    public struct GameplayCueParams
    {
        public GameObject Instigator;
        public GameObject Causer;
        public GameObject Target;

        public Vector3 Position;
        public bool HasExplicitPosition;
        public Vector3 Normal;

        public Object SourceObject;
        public float Magnitude;

        public static GameplayCueParams FromTarget(GameObject target)
        {
            return new GameplayCueParams
            {
                Target = target,
                Position = target != null ? target.transform.position : Vector3.zero,
                HasExplicitPosition = false,
                Normal = Vector3.up
            };
        }
    }
}
