using System;
using UnityEngine;

namespace UnityGAS
{
    public class GameplayTag : ScriptableObject
    {
        [TextArea] public string developerNote;

        // "노드 이름" (마지막 구간). 예: Fire
        [SerializeField] private string tagName;

        // 계층 구조
        [SerializeField] private GameplayTag parent;

        // 런타임용 안정 ID (에디터에서 자동 할당 권장)
        [SerializeField] private int id;

        // 에디터/디버그/검색 최적화용 캐시
        [SerializeField, HideInInspector] private string cachedPath;
        [SerializeField, HideInInspector] private string cachedLowerPath;

        public string Name => string.IsNullOrEmpty(tagName) ? FallbackNameFromAssetName() : tagName;
        public GameplayTag Parent => parent;

        /// <summary>
        /// 태그 전체 경로. (Parent.Path + "." + Name)
        /// 기존 데이터(점으로 된 asset.name)도 깨지지 않도록 fallback 제공.
        /// </summary>
        public string Path
        {
            get
            {
                if (!string.IsNullOrEmpty(cachedPath)) return cachedPath;

                // 신 구조: tagName 기반
                if (!string.IsNullOrEmpty(tagName))
                {
                    cachedPath = parent != null ? $"{parent.Path}.{tagName}" : tagName;
                    return cachedPath;
                }

                // 구 구조 fallback: asset.name 자체가 "A.B.C"일 수 있음
                cachedPath = name;
                return cachedPath;
            }
        }

        public string LowerPath
        {
            get
            {
                if (!string.IsNullOrEmpty(cachedLowerPath)) return cachedLowerPath;
                cachedLowerPath = Path.ToLowerInvariant();
                return cachedLowerPath;
            }
        }

        public int Id => id;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // tagName에 '.' 들어가면 계층과 충돌하니 정리
            if (!string.IsNullOrEmpty(tagName) && tagName.Contains("."))
            {
                tagName = tagName.Replace(".", "_");
            }

            // 캐시 갱신
            cachedPath = null;
            cachedLowerPath = null;

            // 유효하지 않은 id(음수)는 막아둠
            if (id < 0) id = 0;
        }

        public void Editor_SetName(string newName)
        {
            tagName = newName;
            cachedPath = null;
            cachedLowerPath = null;
        }

        public void Editor_SetParent(GameplayTag newParent)
        {
            parent = newParent;
            cachedPath = null;
            cachedLowerPath = null;
        }

        public void Editor_SetId(int newId)
        {
            id = newId;
        }
#endif

        private string FallbackNameFromAssetName()
        {
            // "Element.Fire" 같은 구 구조도 마지막만 뽑아 Name으로 사용
            var n = name;
            int lastDot = n.LastIndexOf('.');
            return lastDot >= 0 ? n.Substring(lastDot + 1) : n;
        }

    }
}
