using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임 :
/// - 보스 HUD 체력바 슬롯이 사용할 프레임 스프라이트 같은 시각 테마 데이터를 제공한다.
/// - 보스별 HUD 외형 차이를 코드 분기 없이 에셋 참조로 요청할 수 있게 한다.
/// </summary>
[CreateAssetMenu(fileName = "BossHudHealthBarTheme", menuName = "UnityGAS/UI/Boss HUD Health Bar Theme")]
public sealed class BossHudHealthBarTheme : ScriptableObject
{
    [Tooltip("보스 체력바 프레임 Image에 적용할 스프라이트입니다. 비워두면 프리팹 기본 프레임을 유지합니다.")]
    [SerializeField] private Sprite frameSprite;

    [Tooltip("프레임 Image의 렌더 타입입니다. 9-slice 프레임은 Sliced를 사용합니다.")]
    [SerializeField] private Image.Type frameImageType = Image.Type.Sliced;

    [Tooltip("Sliced/Tiled 이미지의 픽셀 밀도 보정값입니다.")]
    [SerializeField, Min(0.01f)] private float pixelsPerUnitMultiplier = 1f;

    public Sprite FrameSprite => frameSprite;
    public Image.Type FrameImageType => frameImageType;
    public float PixelsPerUnitMultiplier => Mathf.Max(0.01f, pixelsPerUnitMultiplier);
}
