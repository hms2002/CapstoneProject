using UnityEngine;

/// <summary>
/// 책임 : 엔딩 아웃트로 재생 로직이 구체 UI view 없이 슬라이드, 문구, 스킵 프롬프트를 제어하게 하는 계약이다.
/// </summary>
public interface IEndingOutroView
{
    bool IsReady { get; }
    float SlideAlpha { get; }
    float RootAlpha { get; }
    void Show(KeyCode skipKey);
    void HideImmediate();
    void SetRootAlpha(float alpha);
    void SetSlideSprite(Sprite sprite);
    void SetSlideAlpha(float alpha);
    void SetText(string text);
    void SetSkipPromptAlpha(float alpha);
    void SetSkipFill(float normalized);
    bool ContainsSkipPromptScreenPoint(Vector2 screenPosition);
    void ApplySkipFillColor(Color color);
}
