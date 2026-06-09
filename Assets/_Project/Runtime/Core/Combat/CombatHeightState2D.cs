using System;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 2D 전투 객체의 가짜 높이 상태를 보관하고 변경 이벤트를 발행한다.
    /// - 실제 물리 좌표는 바닥 기준으로 유지하고, 장판/공중 판정과 프레젠테이션이 읽을 공통 상태만 제공한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatHeightState2D : MonoBehaviour
    {
        public enum HeightMode
        {
            Grounded,
            Airborne
        }

        [SerializeField] private HeightMode mode = HeightMode.Grounded;
        [SerializeField, Min(0f)] private float visualHeight;
        [SerializeField, Min(0f)] private float zHeight = 1f;

        public event Action<CombatHeightState2D> Changed;

        public HeightMode Mode => mode;
        public float VisualHeight => visualHeight;
        public float ZMin => mode == HeightMode.Airborne ? visualHeight : 0f;
        public float ZMax => ZMin + Mathf.Max(0f, zHeight);
        public bool IsGrounded => mode == HeightMode.Grounded;
        public bool IsAirborne => mode == HeightMode.Airborne;

        private void OnValidate()
        {
            visualHeight = Mathf.Max(0f, visualHeight);
            zHeight = Mathf.Max(0f, zHeight);
        }

        /// <summary>전투 객체를 지상 상태로 전환하고, visual 높이를 0으로 되돌린다.</summary>
        public void SetGrounded()
        {
            SetHeight(HeightMode.Grounded, 0f, zHeight);
        }

        /// <summary>전투 객체를 공중 상태로 전환하고, 판정/연출에서 사용할 높이 값을 갱신한다.</summary>
        public void SetAirborne(float height, float bodyZHeight = -1f)
        {
            SetHeight(HeightMode.Airborne, Mathf.Max(0f, height), bodyZHeight >= 0f ? bodyZHeight : zHeight);
        }

        private void SetHeight(HeightMode nextMode, float nextVisualHeight, float nextZHeight)
        {
            nextVisualHeight = Mathf.Max(0f, nextVisualHeight);
            nextZHeight = Mathf.Max(0f, nextZHeight);

            if (mode == nextMode &&
                Mathf.Approximately(visualHeight, nextVisualHeight) &&
                Mathf.Approximately(zHeight, nextZHeight))
            {
                return;
            }

            mode = nextMode;
            visualHeight = nextVisualHeight;
            zHeight = nextZHeight;
            Changed?.Invoke(this);
        }
    }
}
