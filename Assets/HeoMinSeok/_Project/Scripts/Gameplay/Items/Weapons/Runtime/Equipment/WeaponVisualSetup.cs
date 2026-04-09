using System;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 무기 프리팹 안에서 실제 비주얼 pivot과 좌/우 손별 로컬 포즈를 정의한다.
/// - WeaponEquipController가 장착 시 비주얼만 안전하게 회전/오프셋 보정할 수 있도록 기준점을 제공한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WeaponVisualSetup : MonoBehaviour
{
    [Serializable]
    public struct HandVisualPose
    {
        /// <summary>
        /// 책임 :
        /// - 특정 손 소켓에 장착되었을 때 visual pivot이 가져야 하는 로컬 위치/회전/스케일 한 세트를 담는다.
        /// - 무기 variant마다 좌/우 손 pose를 안전하게 override할 수 있는 최소 데이터 단위다.
        /// </summary>
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale;
    }

    [Header("Refs")]
    [SerializeField] private Transform visualPivot;

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

    private void Reset()
    {
        if (visualPivot == null)
            visualPivot = transform;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (visualPivot == null)
            visualPivot = transform;
    }
#endif

    /// <summary>
    /// 책임 :
    /// - 현재 장착 손 기준에 맞는 로컬 포즈를 비주얼 pivot에 적용한다.
    /// - 루트 판정/이펙트 구조는 건드리지 않고 비주얼 계층만 보정한다.
    /// </summary>
    public void ApplyPose(bool isLeftHand)
    {
        var pivot = VisualPivot;
        var pose = isLeftHand ? leftHandPose : rightHandPose;

        pivot.localPosition = pose.localPosition;
        pivot.localRotation = Quaternion.Euler(pose.localEulerAngles);
        pivot.localScale = pose.localScale == Vector3.zero ? Vector3.one : pose.localScale;
    }
}
