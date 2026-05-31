using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 진행도 기반으로 커지는 공격 예고 fill이 어느 기준점에서 확장될지 표현한다.
    /// </summary>
    public enum AttackTelegraphFillAnchor
    {
        StartAnchored = 0,
        CenterOut = 1
    }

    /// <summary>
    /// 책임 :
    /// - 공격 예고 연출의 색상, 진행도, 점멸 규칙 같은 시각 스타일을 데이터로 보관한다.
    /// </summary>
    [CreateAssetMenu(fileName = "AttackTelegraphStyle", menuName = "Gameplay/Combat/Attack Telegraph Style")]
    public sealed class AttackTelegraphStyle : ScriptableObject
    {
        [Header("Fill")]
        public Color fillColorStart = new Color(1f, 0f, 0f, 0.3f);
        public Color fillColorEnd = new Color(1f, 0f, 0f, 0.3f);

        [Header("Border")]
        public Color borderColorStart = new Color(1f, 0f, 0f, 0.9f);
        public Color borderColorEnd = new Color(1f, 0f, 0f, 0.9f);

        [Header("Timing")]
        public AnimationCurve progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Range(0f, 1f)] public float blinkStartNormalized = 0.8f;
        [Min(0f)] public float blinkFrequency = 10f;
        [Range(0f, 1f)] public float blinkAlphaMin = 0.35f;

        [Header("Scale")]
        public bool scaleFillWithProgress = false;
        [Range(0f, 1f)] public float fillScaleStart = 0f;
        [Range(0f, 1f)] public float fillScaleEnd = 1f;
        public AttackTelegraphFillAnchor fillAnchor = AttackTelegraphFillAnchor.StartAnchored;
    }
}
