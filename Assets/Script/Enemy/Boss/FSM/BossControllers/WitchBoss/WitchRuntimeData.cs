using System.Collections.Generic;
using UnityEngine;

public sealed class WitchRuntimeData
{
    // 이 클래스의 책임:
    // 마녀 보스 패턴이 공유하는 전용 런타임 상태를 보관한다.

    public Candlestick SelectedCandle { get; private set; }
    public Vector3 SelectedCenter { get; private set; }
    public bool HasActiveExtinguishSelection => SelectedCandle != null;
    private readonly List<WitchNormalAttack1Tile> normal1Tiles = new();

    public void SetExtinguishSelection(Candlestick candle, Vector3 center)
    {
        SelectedCandle = candle;
        SelectedCenter = center;
    }

    public void ClearExtinguishSelection()
    {
        SelectedCandle = null;
        SelectedCenter = Vector3.zero;
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
