using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 책임 : Core/Gameplay 코드가 TextMeshPro 같은 concrete text 구현을 직접 참조하지 않고 serialized text component에 문자열을 투영하게 한다.
/// </summary>
public interface ITextValueSink
{
    void SetText(string value);
}

/// <summary>
/// 책임 : concrete text component 또는 ITextValueSink 구현에 문자열 표시 요청을 전달하는 좁은 presentation binding helper이다.
/// </summary>
public static class TextPresentationBinding
{
    private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;

    public static bool TrySetText(Component target, string value)
    {
        if (target == null)
            return false;

        if (target is ITextValueSink textSink)
        {
            textSink.SetText(value);
            return true;
        }

        PropertyInfo textProperty = ResolveWritableStringTextProperty(target);
        if (textProperty == null)
            return false;

        textProperty.SetValue(target, value);
        return true;
    }

    public static bool TryGetText(Component target, out string value)
    {
        value = null;
        if (target == null)
            return false;

        PropertyInfo textProperty = ResolveReadableStringTextProperty(target);
        if (textProperty == null)
            return false;

        value = textProperty.GetValue(target) as string;
        return true;
    }

    public static bool TryGetPreferredWidth(Component target, out float preferredWidth)
    {
        preferredWidth = 0f;
        if (target == null)
            return false;

        PropertyInfo preferredWidthProperty = target.GetType().GetProperty("preferredWidth", PublicInstance);
        if (preferredWidthProperty == null || !preferredWidthProperty.CanRead)
            return false;

        object rawValue = preferredWidthProperty.GetValue(target);
        if (!(rawValue is float width))
            return false;

        preferredWidth = width;
        return true;
    }

    public static bool TryForceMeshUpdate(Component target)
    {
        if (target == null)
            return false;

        MethodInfo forceMeshUpdate = ResolveForceMeshUpdateMethod(target);
        if (forceMeshUpdate == null)
            return false;

        ParameterInfo[] parameters = forceMeshUpdate.GetParameters();
        object[] arguments = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
            arguments[i] = ResolveDefaultArgument(parameters[i]);

        forceMeshUpdate.Invoke(target, arguments);
        return true;
    }

    public static bool TryResolveInChildren(Transform root, out Component target)
    {
        target = null;
        if (root == null)
            return false;

        Component[] candidates = root.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            Component candidate = candidates[i];
            if (candidate == null || candidate.transform == root)
                continue;

            if (!CanSetText(candidate))
                continue;

            target = candidate;
            return true;
        }

        return false;
    }

    private static bool CanSetText(Component target)
    {
        return target is ITextValueSink || ResolveWritableStringTextProperty(target) != null;
    }

    private static PropertyInfo ResolveReadableStringTextProperty(Component target)
    {
        if (target == null)
            return null;

        PropertyInfo textProperty = target.GetType().GetProperty("text", PublicInstance);
        return textProperty != null && textProperty.CanRead && textProperty.PropertyType == typeof(string)
            ? textProperty
            : null;
    }

    private static PropertyInfo ResolveWritableStringTextProperty(Component target)
    {
        if (target == null)
            return null;

        PropertyInfo textProperty = target.GetType().GetProperty("text", PublicInstance);
        return textProperty != null && textProperty.CanWrite && textProperty.PropertyType == typeof(string)
            ? textProperty
            : null;
    }

    private static object ResolveDefaultArgument(ParameterInfo parameter)
    {
        if (parameter.HasDefaultValue)
            return parameter.DefaultValue;

        return parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null;
    }

    private static MethodInfo ResolveForceMeshUpdateMethod(Component target)
    {
        MethodInfo[] methods = target.GetType().GetMethods(PublicInstance);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (method.Name == "ForceMeshUpdate")
                return method;
        }

        return null;
    }
}
