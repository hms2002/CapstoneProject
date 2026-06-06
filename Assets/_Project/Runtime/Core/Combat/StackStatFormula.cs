using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 후위 표기식(RPN) 기반의 복합 스탯 공식을 정의한다.
    /// - 다중 스탯 참조와 사칙연산을 지원하며, 런타임에는 스택 기반으로 평가한다.
    /// - 에디터 검증과 디버그 문자열을 제공해 authoring 오류를 줄인다.
    /// </summary>
    [CreateAssetMenu(fileName = "SF_NewStackFormula", menuName = "GAS/Formula/Stack Stat Formula")]
    public sealed class StackStatFormula : ScriptableObject
    {
        public enum OpCode
        {
            PushStat = 0,
            PushConst = 1,
            Add = 2,
            Sub = 3,
            Mul = 4,
            Div = 5
        }

        /// <summary>
        /// 책임 :
        /// - StackStatFormula 한 줄 명령을 표현한다.
        /// - Push 계열은 피연산자 데이터를 보관하고, 연산 계열은 opcode만 사용한다.
        /// </summary>
        [Serializable]
        public struct Instruction
        {
            public OpCode opCode;

            [Header("PushStat")]
            [Tooltip("If true, this instruction queries the stat provider by StatId (recommended). If false, it reads the AttributeDefinition directly (legacy).")]
            public bool useStatId;

            [Tooltip("StatId queried from IStatProvider when useStatId is enabled.")]
            public StatId statId;

            [Tooltip("Legacy source attribute read from AttributeSet when useStatId is disabled.")]
            public AttributeDefinition sourceAttribute;

            [Header("PushConst")]
            [Tooltip("Constant value used by PushConst.")]
            public float constantValue;
        }

        [SerializeField] private List<Instruction> instructions = new List<Instruction>();
        [SerializeField, HideInInspector] private string lastValidationMessage = string.Empty;

        private static readonly Stack<float> s_evalStack = new Stack<float>(16);
        private static readonly Stack<string> s_debugStack = new Stack<string>(16);

        public IReadOnlyList<Instruction> Instructions => instructions;
        public string LastValidationMessage => lastValidationMessage;

        private void OnValidate()
        {
            if (TryValidate(out string message))
            {
                lastValidationMessage = string.Empty;
                return;
            }

            lastValidationMessage = message;
            Debug.LogWarning($"[StackStatFormula] '{name}' expression is invalid: {message}", this);
        }

        public float Evaluate(AttributeSet source, IStatProvider provider, float defaultIfEmpty = 0f)
        {
            if (instructions == null || instructions.Count == 0)
                return defaultIfEmpty;

            if (!TryValidate(out _))
                return defaultIfEmpty;

            s_evalStack.Clear();

            for (int i = 0; i < instructions.Count; i++)
            {
                var inst = instructions[i];
                switch (inst.opCode)
                {
                    case OpCode.PushStat:
                    {
                        float value = 0f;
                        if (inst.useStatId)
                        {
                            if (provider != null && inst.statId != StatId.None)
                                value = provider.Get(inst.statId);
                            else if (source != null && inst.sourceAttribute != null)
                                value = source.GetAttributeValue(inst.sourceAttribute);
                        }
                        else if (source != null && inst.sourceAttribute != null)
                        {
                            value = source.GetAttributeValue(inst.sourceAttribute);
                        }

                        s_evalStack.Push(value);
                        break;
                    }

                    case OpCode.PushConst:
                        s_evalStack.Push(inst.constantValue);
                        break;

                    case OpCode.Add:
                    {
                        float rhs = s_evalStack.Pop();
                        float lhs = s_evalStack.Pop();
                        s_evalStack.Push(lhs + rhs);
                        break;
                    }

                    case OpCode.Sub:
                    {
                        float rhs = s_evalStack.Pop();
                        float lhs = s_evalStack.Pop();
                        s_evalStack.Push(lhs - rhs);
                        break;
                    }

                    case OpCode.Mul:
                    {
                        float rhs = s_evalStack.Pop();
                        float lhs = s_evalStack.Pop();
                        s_evalStack.Push(lhs * rhs);
                        break;
                    }

                    case OpCode.Div:
                    {
                        float rhs = s_evalStack.Pop();
                        float lhs = s_evalStack.Pop();
                        s_evalStack.Push(Mathf.Approximately(rhs, 0f) ? 0f : lhs / rhs);
                        break;
                    }

                    default:
                        return defaultIfEmpty;
                }
            }

            return s_evalStack.Count == 1 ? s_evalStack.Pop() : defaultIfEmpty;
        }

        public string BuildDebugString()
        {
            if (instructions == null || instructions.Count == 0)
                return "(empty)";

            if (!TryValidate(out string message))
                return $"(invalid: {message})";

            s_debugStack.Clear();

            for (int i = 0; i < instructions.Count; i++)
            {
                var inst = instructions[i];
                switch (inst.opCode)
                {
                    case OpCode.PushStat:
                        s_debugStack.Push(BuildPushStatLabel(inst));
                        break;

                    case OpCode.PushConst:
                        s_debugStack.Push(inst.constantValue.ToString("0.###"));
                        break;

                    case OpCode.Add:
                        PushDebugBinary("+");
                        break;

                    case OpCode.Sub:
                        PushDebugBinary("-");
                        break;

                    case OpCode.Mul:
                        PushDebugBinary("*");
                        break;

                    case OpCode.Div:
                        PushDebugBinary("/");
                        break;
                }
            }

            return s_debugStack.Count == 1 ? s_debugStack.Pop() : "(invalid)";
        }

        public bool TryValidate(out string message)
        {
            if (instructions == null || instructions.Count == 0)
            {
                message = "Instruction list is empty.";
                return false;
            }

            int stackDepth = 0;
            for (int i = 0; i < instructions.Count; i++)
            {
                var inst = instructions[i];
                switch (inst.opCode)
                {
                    case OpCode.PushStat:
                        if (inst.useStatId)
                        {
                            if (inst.statId == StatId.None && inst.sourceAttribute == null)
                            {
                                message = $"Instruction {i} PushStat has no StatId fallback.";
                                return false;
                            }
                        }
                        else if (inst.sourceAttribute == null)
                        {
                            message = $"Instruction {i} PushStat requires sourceAttribute.";
                            return false;
                        }

                        stackDepth += 1;
                        break;

                    case OpCode.PushConst:
                        stackDepth += 1;
                        break;

                    case OpCode.Add:
                    case OpCode.Sub:
                    case OpCode.Mul:
                    case OpCode.Div:
                        if (stackDepth < 2)
                        {
                            message = $"Instruction {i} causes stack underflow.";
                            return false;
                        }

                        stackDepth -= 1;
                        break;

                    default:
                        message = $"Instruction {i} uses unsupported opcode.";
                        return false;
                }
            }

            if (stackDepth != 1)
            {
                message = $"Expression must end with exactly one result, but stack depth is {stackDepth}.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static string BuildPushStatLabel(in Instruction inst)
        {
            if (inst.useStatId && inst.statId != StatId.None)
                return inst.statId.ToString();

            if (inst.sourceAttribute != null)
                return inst.sourceAttribute.name;

            return "0";
        }

        private static void PushDebugBinary(string op)
        {
            string rhs = s_debugStack.Pop();
            string lhs = s_debugStack.Pop();
            s_debugStack.Push($"({lhs} {op} {rhs})");
        }
    }
}
