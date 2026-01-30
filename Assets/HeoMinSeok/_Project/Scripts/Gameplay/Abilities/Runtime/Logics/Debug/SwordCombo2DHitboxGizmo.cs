using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    [ExecuteAlways]
    public class SwordCombo2DHitboxGizmo : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private AbilitySystem system;
        [Tooltip("검 일반공격 AbilityDefinition (sourceObject가 SwordCombo2DData여야 함)")]
        [SerializeField] private AbilityDefinition attackAbility;

        [Header("Preview")]
        public bool drawOnlyWhenSelected = true;
        public bool showAllCombos = true; // 0~2 모두
        [Tooltip("Play Mode에서 입력(AimDirection) 사용")]
        public bool useInputAimInPlayMode = true;

        [Tooltip("Edit Mode에서 사용할 방향(0이면 transform.right)")]
        public Vector2 previewDirection = Vector2.right;

        private void Reset()
        {
            if (system == null) system = GetComponent<AbilitySystem>();
        }

        private void OnDrawGizmos()
        {
            if (drawOnlyWhenSelected) return;
            DrawInternal();
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawOnlyWhenSelected) return;
            DrawInternal();
        }

        private void DrawInternal()
        {
            if (system == null) system = GetComponent<AbilitySystem>();
            if (system == null) return;
            if (attackAbility == null) return;

            var data = attackAbility.sourceObject as SwordCombo2DData;
            if (data == null) return;

            Vector2 dir = ResolveAimDirection();
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir.Normalize();

            int start = 0, end = 2;
            if (!showAllCombos) start = end = 0;

            for (int comboIndex = start; comboIndex <= end; comboIndex++)
            {
                Vector2 center = ComputeCenter(data, comboIndex, dir);

                // 보기 좋게 콤보별 색 분리(선택)
                Gizmos.color = (comboIndex == 0) ? Color.green : (comboIndex == 1 ? Color.yellow : Color.cyan);

                Gizmos.DrawLine(system.transform.position, center);
                Gizmos.DrawWireCube(center, data.hitboxSize);
                Gizmos.DrawSphere(center, 0.03f);
            }
        }

        private Vector2 ResolveAimDirection()
        {
            if (Application.isPlaying && useInputAimInPlayMode)
            {
                var input = system.GetComponent<PlayerCombatInput2D>();
                if (input != null) return input.AimDirection;
            }

            if (!Application.isPlaying && previewDirection.sqrMagnitude > 0.0001f)
                return previewDirection.normalized;

            return (Vector2)system.transform.right;
        }

        private Vector2 ComputeCenter(SwordCombo2DData data, int comboIndex, Vector2 dir)
        {
            Vector2 perp = new Vector2(-dir.y, dir.x);
            int sideSign = GetArraySafe(data.sideSigns, comboIndex, 0);

            // ✅ AbilityLogic_SwordCombo2D.DoHit()의 center 계산과 동일 :contentReference[oaicite:1]{index=1}
            return (Vector2)system.transform.position
                   + dir * data.forwardOffset
                   + perp * (data.sideOffset * sideSign);
        }

        private static T GetArraySafe<T>(T[] arr, int index, T fallback)
        {
            if (arr == null || arr.Length == 0) return fallback;
            index = Mathf.Clamp(index, 0, arr.Length - 1);
            return arr[index];
        }
    }
}
