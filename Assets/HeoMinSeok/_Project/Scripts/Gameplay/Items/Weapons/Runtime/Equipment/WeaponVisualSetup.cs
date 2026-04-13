using System;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 무기 프리팹 안에서 공격 방향 반전을 담당하는 visual pivot과 실제 렌더 포즈를 담당하는 render pivot을 분리한다.
/// - WeaponEquipController가 손별 포즈 보정과 공격 단계별 좌우 반전을 서로 다른 계층에 안전하게 적용할 수 있도록 기준점을 제공한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WeaponVisualSetup : MonoBehaviour
{
    [Serializable]
    public struct HandVisualPose
    {
        /// <summary>
        /// 책임 :
        /// - 특정 손 소켓에 장착되었을 때 weapon render pivot이 가져야 하는 로컬 위치/회전/스케일 한 세트를 담는다.
        /// - 무기 variant마다 좌/우 손 pose를 안전하게 override할 수 있는 최소 데이터 단위다.
        /// </summary>
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale;
    }

    [Header("Refs")]
    [SerializeField] private Transform visualPivot;
    [SerializeField] private Transform weaponRenderPivot;

    [Header("Hand Poses")]
    [SerializeField] private HandVisualPose rightHandPose = new HandVisualPose
    {
        localPosition = Vector3.zero,
        localEulerAngles = Vector3.zero,
        localScale = Vector3.one
    };

    [SerializeField] private HandVisualPose leftHandPose = new HandVisualPose
    {
        localPosition = Vector3.zero,
        localEulerAngles = new Vector3(0f, 180f, 0f),
        localScale = Vector3.one
    };

    public Transform VisualPivot => visualPivot != null ? visualPivot : transform;
    public Transform WeaponRenderPivot => weaponRenderPivot != null ? weaponRenderPivot : VisualPivot;

    private void Reset()
    {
        if (visualPivot == null)
            visualPivot = transform;

        if (weaponRenderPivot == null)
            weaponRenderPivot = visualPivot != null ? visualPivot : transform;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (visualPivot == null)
            visualPivot = transform;

        if (weaponRenderPivot == null)
            weaponRenderPivot = visualPivot != null ? visualPivot : transform;
    }
#endif

    /// <summary>
    /// 책임 :
    /// - 현재 장착 손 기준에 맞는 로컬 포즈를 weapon render pivot에 적용한다.
    /// - 공격 방향 반전을 담당하는 visual pivot과 손별 오프셋/회전을 담당하는 render pivot의 책임을 분리한다.
    /// </summary>
    public void ApplyPose(bool isLeftHand)
    {
        var pivot = WeaponRenderPivot;
        var pose = isLeftHand ? leftHandPose : rightHandPose;

        pivot.localPosition = pose.localPosition;
        pivot.localRotation = Quaternion.Euler(pose.localEulerAngles);
        pivot.localScale = pose.localScale == Vector3.zero ? Vector3.one : pose.localScale;
    }

    /// <summary>
    /// 책임 :
    /// - 공격 단계의 sideSign에 따라 visual pivot의 Y축 회전을 뒤집는다.
    /// - 렌더 포즈 보정은 유지한 채 무기 전체 표현 계층만 좌우 반전해 대각선 스프라이트의 위치 틀어짐을 줄인다.
    /// </summary>
    public void ApplyAttackSideSign(int sideSign)
    {
        Transform pivot = VisualPivot;
        Vector3 euler = pivot.localEulerAngles;
        euler.y = sideSign < 0 ? 180f : 0f;
        pivot.localEulerAngles = euler;
    }
}
