using UnityEngine;

public sealed class WitchRuntimeData
{
    // 이 클래스의 책임:
    // 마녀 보스 패턴이 공유하는 전용 런타임 상태를 보관한다.

    public StrangeCandlestick SelectedCandle { get; private set; }
    public Vector3 SelectedCenter { get; private set; }
    public bool HasActiveExtinguishSelection => SelectedCandle != null;

    public void SetExtinguishSelection(StrangeCandlestick candle, Vector3 center)
    {
        SelectedCandle = candle;
        SelectedCenter = center;
    }

    public void ClearExtinguishSelection()
    {
        SelectedCandle = null;
        SelectedCenter = Vector3.zero;
    }
}
