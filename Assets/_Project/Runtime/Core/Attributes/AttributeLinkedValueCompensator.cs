using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 : Attribute max-current 연결 보상 적용 상황을 구분한다.
    /// 복원/재적용 경로와 실제 획득/해제 경로가 같은 보정 규칙을 공유하지 않도록 한다.
    /// </summary>
    public enum AttributeLinkedValueCompensationContext
    {
        None = 0,
        Purchase,
        RelicEquip,
        RelicUnequip,
        RelicLevelChange,
        Restore,
        Reapply,
        Initialize
    }

    /// <summary>
    /// 책임 : max Attribute 변경 전후의 delta를 current Attribute에 안전하게 반영한다.
    /// 변경 전 current 값을 기준으로 target을 계산해 MaxLink clamp와 중복 차감되는 문제를 피한다.
    /// </summary>
    public static class AttributeLinkedValueCompensator
    {
        public readonly struct Snapshot
        {
            public bool IsValid { get; }
            public AttributeDefinition MaxAttribute { get; }
            public AttributeDefinition CurrentAttribute { get; }
            public float OldMaxValue { get; }
            public float OldCurrentValue { get; }
            public float MinimumCurrentValue { get; }

            public Snapshot(
                AttributeDefinition maxAttribute,
                AttributeDefinition currentAttribute,
                float oldMaxValue,
                float oldCurrentValue,
                float minimumCurrentValue)
            {
                IsValid = maxAttribute != null && currentAttribute != null;
                MaxAttribute = maxAttribute;
                CurrentAttribute = currentAttribute;
                OldMaxValue = oldMaxValue;
                OldCurrentValue = oldCurrentValue;
                MinimumCurrentValue = minimumCurrentValue;
            }
        }

        public static bool TryCapture(
            AttributeSet attributeSet,
            AttributeDefinition maxAttribute,
            AttributeLinkedValueCompensationContext context,
            out Snapshot snapshot)
        {
            snapshot = default;

            if (attributeSet == null || maxAttribute == null)
                return false;

            if (!TryResolvePolicy(attributeSet, maxAttribute, context, out AttributeDefinition currentAttribute, out float minimumCurrent))
                return false;

            snapshot = new Snapshot(
                maxAttribute,
                currentAttribute,
                attributeSet.GetCurrentValue(maxAttribute),
                attributeSet.GetCurrentValue(currentAttribute),
                minimumCurrent);
            return snapshot.IsValid;
        }

        public static void CaptureAll(
            AttributeSet attributeSet,
            AttributeLinkedValueCompensationContext context,
            List<Snapshot> snapshots)
        {
            if (snapshots == null)
                throw new ArgumentNullException(nameof(snapshots));

            snapshots.Clear();

            if (attributeSet == null)
                return;

            foreach (AttributeSet.MaxLink link in attributeSet.EnumerateMaxLinks())
            {
                if (link.max == null || link.value == null)
                    continue;

                if (TryCapture(attributeSet, link.max, context, out Snapshot snapshot))
                    snapshots.Add(snapshot);
            }
        }

        public static bool WouldDropBelowMinimum(Snapshot snapshot, float projectedMaxValue)
        {
            if (!snapshot.IsValid)
                return false;

            float projectedCurrent = snapshot.OldCurrentValue + (projectedMaxValue - snapshot.OldMaxValue);
            return projectedCurrent < snapshot.MinimumCurrentValue - 0.0001f;
        }

        public static bool Complete(AttributeSet attributeSet, Snapshot snapshot, UnityEngine.Object source)
        {
            if (attributeSet == null || !snapshot.IsValid)
                return false;

            float newMaxValue = attributeSet.GetCurrentValue(snapshot.MaxAttribute);
            float delta = newMaxValue - snapshot.OldMaxValue;
            if (Mathf.Abs(delta) < 0.0001f)
                return false;

            float targetCurrent = snapshot.OldCurrentValue + delta;
            return attributeSet.TrySetCurrentValue(snapshot.CurrentAttribute, targetCurrent, source);
        }

        public static void CompleteAll(AttributeSet attributeSet, IReadOnlyList<Snapshot> snapshots, UnityEngine.Object source)
        {
            if (attributeSet == null || snapshots == null)
                return;

            for (int i = 0; i < snapshots.Count; i++)
                Complete(attributeSet, snapshots[i], source);
        }

        private static bool TryResolvePolicy(
            AttributeSet attributeSet,
            AttributeDefinition maxAttribute,
            AttributeLinkedValueCompensationContext context,
            out AttributeDefinition currentAttribute,
            out float minimumCurrent)
        {
            currentAttribute = null;
            minimumCurrent = 1f;

            // AttributeSet.MaxLink is the single source of truth for max-current relationships.
            // Keeping resolution local avoids a first-use Resources scan during relic acquisition.
            return IsRuntimeCompensationContext(context) &&
                   attributeSet.TryGetLinkedValueForMax(maxAttribute, out currentAttribute) &&
                   currentAttribute != null;
        }

        private static bool IsRuntimeCompensationContext(AttributeLinkedValueCompensationContext context)
        {
            return context == AttributeLinkedValueCompensationContext.Purchase ||
                   context == AttributeLinkedValueCompensationContext.RelicEquip ||
                   context == AttributeLinkedValueCompensationContext.RelicUnequip ||
                   context == AttributeLinkedValueCompensationContext.RelicLevelChange;
        }
    }
}
