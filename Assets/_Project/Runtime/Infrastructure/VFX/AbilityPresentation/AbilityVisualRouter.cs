using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - Ability 로직이 전용 지속 비주얼을 안전하게 생성, 재사용, 정리할 수 있는 공통 진입점을 제공한다.
    /// - Rush 잔상처럼 spec 단위 수명 관리가 필요한 runtime visual을 AbilitySystem 외부에 흩뿌리지 않고 한 곳에 모은다.
    /// </summary>
    public sealed class AbilityVisualRouter
    {
        private readonly GameObject ownerObject;
        private readonly Dictionary<AbilitySpec, List<Object>> runtimeVisuals = new();

        public AbilityVisualRouter(GameObject ownerObject)
        {
            this.ownerObject = ownerObject;
        }

        /// <summary>
        /// 책임 :
        /// - ownerObject에 붙는 능력 전용 visual component를 spec 단위로 1개만 생성/재사용한다.
        /// - 같은 spec이 반복 호출돼도 중복 component를 만들지 않게 해준다.
        /// </summary>
        public T GetOrAddOwnedComponent<T>(AbilitySpec spec) where T : Component
        {
            if (spec == null || ownerObject == null)
                return null;

            T existing = GetOwnedComponent<T>(spec);
            if (existing != null)
                return existing;

            T created = ownerObject.AddComponent<T>();
            Register(spec, created);
            return created;
        }

        /// <summary>
        /// 책임 :
        /// - spec에 연결된 ownerObject component 중 원하는 타입을 조회한다.
        /// - 개별 AbilityLogic이 자기 비주얼 상태를 다시 찾을 수 있는 최소 조회 API를 제공한다.
        /// </summary>
        public T GetOwnedComponent<T>(AbilitySpec spec) where T : Component
        {
            if (spec == null || !runtimeVisuals.TryGetValue(spec, out List<Object> visuals))
                return null;

            for (int i = 0; i < visuals.Count; i++)
            {
                if (visuals[i] is T typed)
                    return typed;
            }

            return null;
        }

        /// <summary>
        /// 책임 :
        /// - AbilityLogic이 직접 만든 visual object/component를 spec 수명에 묶어 정리 목록에 등록한다.
        /// - ownerObject 외부에 생성된 프리팹 인스턴스도 같은 해제 경로로 회수할 수 있게 한다.
        /// </summary>
        public void Register(AbilitySpec spec, Object runtimeVisual)
        {
            if (spec == null || runtimeVisual == null)
                return;

            if (!runtimeVisuals.TryGetValue(spec, out List<Object> visuals))
            {
                visuals = new List<Object>();
                runtimeVisuals[spec] = visuals;
            }

            if (!visuals.Contains(runtimeVisual))
                visuals.Add(runtimeVisual);
        }

        /// <summary>
        /// 책임 :
        /// - 특정 spec이 생성한 runtime visual만 정리한다.
        /// - 정상 종료/강제 취소 어느 경로에서도 AbilityLogic이 같은 API로 정리할 수 있게 한다.
        /// </summary>
        public void Release(AbilitySpec spec)
        {
            if (spec == null || !runtimeVisuals.TryGetValue(spec, out List<Object> visuals))
                return;

            DestroyAll(visuals);
            runtimeVisuals.Remove(spec);
        }

        /// <summary>
        /// 책임 :
        /// - AbilitySystem 리셋/씬 이동 같은 전역 정리 경로에서 모든 runtime visual을 한 번에 제거한다.
        /// - AbilityLogic cleanup 누락이 있더라도 비주얼이 씬에 남지 않도록 하는 마지막 안전장치다.
        /// </summary>
        public void ReleaseAll()
        {
            foreach (KeyValuePair<AbilitySpec, List<Object>> pair in runtimeVisuals)
            {
                DestroyAll(pair.Value);
            }

            runtimeVisuals.Clear();
        }

        private static void DestroyAll(List<Object> visuals)
        {
            if (visuals == null)
                return;

            for (int i = visuals.Count - 1; i >= 0; i--)
            {
                Object visual = visuals[i];
                if (visual == null)
                    continue;

                if (visual is GameObject go)
                    Object.Destroy(go);
                else if (visual is Component component)
                    Object.Destroy(component);
            }

            visuals.Clear();
        }
    }
}
