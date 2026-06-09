using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS.Sample
{
    [ExecuteAlways]
    public class RealtimeHitboxGizmo2D : MonoBehaviour
    {
        [System.Serializable]
        private struct Box
        {
            public Vector2 center;
            public Vector2 size;
            public float angleDeg;
            public float expireTime;
            public Color color;
        }

        [Header("Gizmo")]
        public bool drawOnlyWhenSelected = false;
        public float defaultDuration = 0.15f;
        public int maxBoxes = 12;

        private readonly List<Box> boxes = new();

        public void RecordBox(Vector2 center, Vector2 size, float angleDeg, float duration, Color color)
        {
            // Play 모드에서만 시간 기반으로 지우는 게 자연스럽지만,
            // ExecuteAlways라 Edit에서도 호출되면 동작은 함.
            float now = Application.isPlaying ? Time.time : 0f;

            boxes.Add(new Box
            {
                center = center,
                size = size,
                angleDeg = angleDeg,
                expireTime = now + Mathf.Max(0.01f, duration),
                color = color
            });

            if (boxes.Count > maxBoxes)
                boxes.RemoveAt(0);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            float now = Time.time;
            for (int i = boxes.Count - 1; i >= 0; i--)
            {
                if (now > boxes[i].expireTime)
                    boxes.RemoveAt(i);
            }
        }

        private void OnDrawGizmos()
        {
            if (drawOnlyWhenSelected) return;
            Draw();
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawOnlyWhenSelected) return;
            Draw();
        }

        private void Draw()
        {
            if (boxes.Count == 0) return;

            var old = Gizmos.matrix;

            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i];
                Gizmos.color = b.color;

                // 회전 박스까지 지원(지금은 angle=0이지만 확장성)
                Gizmos.matrix = Matrix4x4.TRS(
                    new Vector3(b.center.x, b.center.y, 0f),
                    Quaternion.Euler(0f, 0f, b.angleDeg),
                    Vector3.one
                );

                Gizmos.DrawWireCube(Vector3.zero, new Vector3(b.size.x, b.size.y, 0f));
            }

            Gizmos.matrix = old;
        }
    }
}
