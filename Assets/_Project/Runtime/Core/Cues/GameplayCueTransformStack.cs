using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 여러 Cue가 동시에 Transform을 건드려도 안전하게 합성/해제할 수 있는 레이어 스택.
    /// - localPosition: base + sum(addPos)
    /// - localRotation: baseRot * Euler(sum(addEuler))
    /// - localScale   : baseScale * product(mulScale)
    /// </summary>
    public sealed class GameplayCueTransformStack : MonoBehaviour
    {
        public struct Contribution
        {
            public Vector3 AddLocalPos;
            public Vector3 AddLocalEuler;
            public Vector3 MulLocalScale;   // (1,1,1)이 기본
        }

        private bool _baseCaptured;
        private Vector3 _baseLocalPos;
        private Quaternion _baseLocalRot;
        private Vector3 _baseLocalScale;

        private readonly Dictionary<int, Contribution> _layers = new();

        public bool HasAnyLayer => _layers.Count > 0;

        private void CaptureBaseIfNeeded()
        {
            if (_baseCaptured) return;
            _baseCaptured = true;
            _baseLocalPos = transform.localPosition;
            _baseLocalRot = transform.localRotation;
            _baseLocalScale = transform.localScale;
        }

        public void AddOrUpdate(int key, Contribution c)
        {
            CaptureBaseIfNeeded();
            _layers[key] = c;
            RecomputeAndApply();
        }

        public void Remove(int key)
        {
            if (!_layers.Remove(key)) return;

            if (_layers.Count == 0)
            {
                // 전부 제거되면 원복
                if (_baseCaptured)
                {
                    transform.localPosition = _baseLocalPos;
                    transform.localRotation = _baseLocalRot;
                    transform.localScale = _baseLocalScale;
                }
                return;
            }

            RecomputeAndApply();
        }

        private void RecomputeAndApply()
        {
            Vector3 sumPos = Vector3.zero;
            Vector3 sumEuler = Vector3.zero;
            Vector3 mulScale = Vector3.one;

            foreach (var kv in _layers)
            {
                var c = kv.Value;
                sumPos += c.AddLocalPos;
                sumEuler += c.AddLocalEuler;
                mulScale = Vector3.Scale(mulScale, c.MulLocalScale);
            }

            transform.localPosition = _baseLocalPos + sumPos;
            transform.localRotation = _baseLocalRot * Quaternion.Euler(sumEuler);
            transform.localScale = Vector3.Scale(_baseLocalScale, mulScale);
        }
    }
}
