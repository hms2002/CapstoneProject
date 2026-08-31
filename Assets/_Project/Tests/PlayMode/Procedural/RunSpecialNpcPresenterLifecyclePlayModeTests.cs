using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 책임 : 선택지 UI 수명 주기 테스트에서 표시 호출과 선택 확정 계약을 기록하는 대역을 제공한다.
/// </summary>
internal sealed class RunSpecialNpcChoicePresenterLifecycleStub :
    MonoBehaviour,
    IRunSpecialNpcChoicePresenter
{
    public bool CanAcceptInput => true;
    public bool AllowGlobalLookup => true;
    public Component PresenterComponent => this;

    public void Show(
        IReadOnlyList<string> labels,
        Action<int> onChoiceSelected,
        float inputGuardSeconds)
    {
    }

    public void Hide()
    {
    }

    public bool ConfirmChoiceAt(int index) => true;
}

/// <summary>
/// 책임 : 씬 전환 중 교체된 전역 특수 NPC 선택지 UI를 NPC가 파괴 참조 대신 다시 탐색하는지 회귀 검증한다.
/// </summary>
public sealed class RunSpecialNpcPresenterLifecyclePlayModeTests
{
    [UnityTest]
    public IEnumerator Interactor_ReplacesDestroyedCachedGlobalPresenter()
    {
        GameObject stalePresenterObject = new("StaleChoicePresenter");
        GameObject interactorObject = new("RunSpecialNpcInteractor");
        GameObject replacementPresenterObject = null;
        try
        {
            RunSpecialNpcChoicePresenterLifecycleStub stalePresenter =
                stalePresenterObject.AddComponent<RunSpecialNpcChoicePresenterLifecycleStub>();
            RunSpecialNpcInteractor interactor =
                interactorObject.AddComponent<RunSpecialNpcInteractor>();
            SetPrivateField(interactor, "choicePresenter", stalePresenter);
            SetPrivateField(interactor, "resolvedChoicePresenter", stalePresenter);
            Assert.That(
                GetResolvedPresenter(interactor),
                Is.SameAs(stalePresenter),
                "The setup must reproduce the cached scene-local GlobalUIRoot presenter.");

            UnityEngine.Object.Destroy(stalePresenterObject);
            yield return null;

            replacementPresenterObject = new GameObject("ReplacementChoicePresenter");
            RunSpecialNpcChoicePresenterLifecycleStub replacementPresenter =
                replacementPresenterObject.AddComponent<RunSpecialNpcChoicePresenterLifecycleStub>();

            InvokeResolveChoicePresenter(interactor);

            Assert.That(
                GetResolvedPresenter(interactor),
                Is.SameAs(replacementPresenter),
                "A destroyed serialized interface reference must not be selected again.");
        }
        finally
        {
            if (replacementPresenterObject != null)
                UnityEngine.Object.DestroyImmediate(replacementPresenterObject);
            if (interactorObject != null)
                UnityEngine.Object.DestroyImmediate(interactorObject);
            if (stalePresenterObject != null)
                UnityEngine.Object.DestroyImmediate(stalePresenterObject);
        }
    }

    private static object GetResolvedPresenter(RunSpecialNpcInteractor interactor)
    {
        FieldInfo field = typeof(RunSpecialNpcInteractor).GetField(
            "resolvedChoicePresenter",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return field.GetValue(interactor);
    }

    private static void InvokeResolveChoicePresenter(RunSpecialNpcInteractor interactor)
    {
        MethodInfo method = typeof(RunSpecialNpcInteractor).GetMethod(
            "ResolveChoicePresenter",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(interactor, null);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
}
