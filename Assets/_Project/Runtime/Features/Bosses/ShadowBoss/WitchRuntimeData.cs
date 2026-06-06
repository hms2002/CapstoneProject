using System.Collections.Generic;
using UnityEngine;

public sealed class WitchRuntimeData
{
    // 이 클래스의 책임:
    // 마녀 보스 패턴이 공유하는 전용 런타임 상태를 보관한다.

    private readonly List<Candlestick> selectedCandles = new();
    private readonly List<Vector3> selectedCenters = new();
    private readonly List<WitchNormalAttack1Tile> normal1Tiles = new();

    public IReadOnlyList<Candlestick> SelectedCandles => selectedCandles;
    public IReadOnlyList<Vector3> SelectedCenters => selectedCenters;
    public bool HasActiveExtinguishSelection => selectedCandles.Count > 0;

    /// <summary>촛불 끄기 패턴에서 선택된 촛대들과 중심점을 저장합니다.</summary>
    public void SetExtinguishSelections(IReadOnlyList<Candlestick> candles, IReadOnlyList<Vector3> centers)
    {
        selectedCandles.Clear();
        selectedCenters.Clear();

        if (candles != null)
        {
            for (int i = 0; i < candles.Count; i++)
            {
                Candlestick candle = candles[i];
                if (candle != null)
                    selectedCandles.Add(candle);
            }
        }

        if (centers != null)
        {
            for (int i = 0; i < centers.Count; i++)
                selectedCenters.Add(centers[i]);
        }
    }

    /// <summary>촛불 끄기 패턴에서 저장한 선택 정보를 비웁니다.</summary>
    public void ClearExtinguishSelection()
    {
        selectedCandles.Clear();
        selectedCenters.Clear();
    }

    /// <summary>평타1 장판을 등록합니다.</summary>
    public void AddNormal1Tile(WitchNormalAttack1Tile tile)
    {
        if (tile == null) return;

        normal1Tiles.Add(tile);
    }

    /// <summary>남아 있는 평타1 장판을 모두 지웁니다.</summary>
    public void ClearNormal1Tiles()
    {
        for (int i = 0; i < normal1Tiles.Count; i++)
        {
            WitchNormalAttack1Tile tile = normal1Tiles[i];
            if (tile == null) continue;

            Object.Destroy(tile.gameObject);
        }

        normal1Tiles.Clear();
    }
}
