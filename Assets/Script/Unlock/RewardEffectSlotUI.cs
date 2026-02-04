using UnityEngine;
using UnityEngine.UI;

public class RewardEffectSlotUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;

    // 상호작용이 필요 없으므로 ScriptableObject 참조도 제거하고 아이콘만 받습니다.
    public void Setup(Sprite icon)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }
    }
}