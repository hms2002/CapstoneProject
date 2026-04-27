using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 촛대/화염 공격/불 장판처럼 술 장판을 점화할 수 있는 2D trigger 표식을 제공한다.
    /// - 실제 연쇄 변환과 지연 처리는 PuddleConversionService에 위임한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PuddleIgnitionSource2D : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            TryIgnite(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryIgnite(other);
        }

        private static void TryIgnite(Collider2D other)
        {
            if (other == null)
                return;

            AlcoholPuddleArea alcohol = other.GetComponentInParent<AlcoholPuddleArea>();
            if (alcohol == null)
                return;

            alcohol.RequestIgnite();
        }
    }
}
