// 3. ✨ 새롭게 개편된 호버 전용 인터페이스
using UnityEngine;

public interface IHoverView : IUIView
{
    // HoverUIController가 위치를 계산하기 위해 뷰의 Rect 크기를 알아야 함
    RectTransform Rect { get; }

    // 데이터와 컨텍스트를 받아 화면에 그림
    void ShowHover(object data, object context = null);
    void HideHover();
}

public interface IHoverPositionOffsetProvider
{
    Vector2 HoverPositionOffset { get; }
}
