#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityGAS;

namespace CapstonePresentation.EditorTools
{
    [CreateAssetMenu(fileName = "PresentationWorkbenchProfile", menuName = "Presentation/Editor/Workbench Profile")]
    internal sealed class PresentationWorkbenchProfileSO : ScriptableObject
    {
        [SerializeField] private List<AbilityDefinition> abilityDefinitions = new();
        [SerializeField] private List<AbilityLogic> abilityLogics = new();

        public IReadOnlyList<AbilityDefinition> AbilityDefinitions => abilityDefinitions;
        public IReadOnlyList<AbilityLogic> AbilityLogics => abilityLogics;

        public bool AddTarget(Object target)
        {
            switch (target)
            {
                case AbilityDefinition definition:
                    if (abilityDefinitions.Contains(definition))
                        return false;

                    abilityDefinitions.Add(definition);
                    MarkDirty();
                    return true;

                case AbilityLogic logic:
                    if (abilityLogics.Contains(logic))
                        return false;

                    abilityLogics.Add(logic);
                    MarkDirty();
                    return true;

                default:
                    return false;
            }
        }

        public void AddDefinitions(IEnumerable<AbilityDefinition> definitions)
        {
            if (definitions == null)
                return;

            bool changed = false;
            foreach (AbilityDefinition definition in definitions)
            {
                if (definition == null || abilityDefinitions.Contains(definition))
                    continue;

                abilityDefinitions.Add(definition);
                changed = true;
            }

            if (changed)
                MarkDirty();
        }

        public void AddLogics(IEnumerable<AbilityLogic> logics)
        {
            if (logics == null)
                return;

            bool changed = false;
            foreach (AbilityLogic logic in logics)
            {
                if (logic == null || abilityLogics.Contains(logic))
                    continue;

                abilityLogics.Add(logic);
                changed = true;
            }

            if (changed)
                MarkDirty();
        }

        private void MarkDirty()
        {
            EditorUtility.SetDirty(this);
        }
    }
}
#endif
