using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 공격 예고 연출의 공통 위험 색상 규칙을 코드 생성 스타일에도 일관되게 적용한다.
    /// - 개별 패턴은 점멸/진행도만 조정하고, 색상 정책은 이 유틸을 통해 공유한다.
    /// </summary>
    public static class AttackTelegraphStyleUtility
    {
        private static readonly Color DangerColor = Color.red;

        public static void ApplyDangerAreaColors(AttackTelegraphStyle style)
        {
            if (style == null)
                return;

            style.fillColorStart = WithAlpha(DangerColor, 0.3f);
            style.fillColorEnd = WithAlpha(DangerColor, 0.3f);
            style.borderColorStart = WithAlpha(DangerColor, 0.9f);
            style.borderColorEnd = WithAlpha(DangerColor, 0.9f);
        }

        public static void ApplyDangerLineColors(AttackTelegraphStyle style)
        {
            if (style == null)
                return;

            style.fillColorStart = WithAlpha(DangerColor, 0f);
            style.fillColorEnd = WithAlpha(DangerColor, 0f);
            style.borderColorStart = WithAlpha(DangerColor, 0.9f);
            style.borderColorEnd = WithAlpha(DangerColor, 0.9f);
        }

        public static void ApplyDangerSolidLineColors(AttackTelegraphStyle style)
        {
            if (style == null)
                return;

            style.fillColorStart = WithAlpha(DangerColor, 0.9f);
            style.fillColorEnd = WithAlpha(DangerColor, 0.9f);
            style.borderColorStart = WithAlpha(DangerColor, 0.9f);
            style.borderColorEnd = WithAlpha(DangerColor, 0.9f);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
