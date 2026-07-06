using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : RunSpecialNpc gameplay 흐름이 concrete 선택지 UI 구현 없이 선택지 표시와 입력 확정을 요청하는 계약을 제공한다.
/// </summary>
public interface IRunSpecialNpcChoicePresenter
{
    bool CanAcceptInput { get; }
    bool AllowGlobalLookup { get; }
    Component PresenterComponent { get; }

    void Show(IReadOnlyList<string> labels, Action<int> onChoiceSelected, float inputGuardSeconds);
    void Hide();
    bool ConfirmChoiceAt(int index);
}

/// <summary>
/// 책임 : RunSpecialNpc 선택지 UI가 대상 Transform을 따라가도록 gameplay 흐름에서 요청하는 계약을 제공한다.
/// </summary>
public interface IRunSpecialNpcChoiceAnchorFollower
{
    void SetFollowTarget(Transform target);
    void ClearFollowTarget();
}
