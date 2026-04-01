using System;
using UnityEngine;

public static class PlayerRuntimeRegistry
{
    public static PlayerInteractor2D CurrentPlayer { get; private set; }

    public static event Action<PlayerInteractor2D> PlayerRegistered;
    public static event Action<PlayerInteractor2D> PlayerUnregistered;

    public static void Register(PlayerInteractor2D player)
    {
        if (player == null)
            return;

        if (CurrentPlayer == player)
            return;

        if (CurrentPlayer != null)
        {
            var previous = CurrentPlayer;
            CurrentPlayer = null;
            PlayerUnregistered?.Invoke(previous);
        }

        CurrentPlayer = player;
        PlayerRegistered?.Invoke(player);
    }

    public static void Unregister(PlayerInteractor2D player)
    {
        if (player == null)
            return;

        if (CurrentPlayer != player)
            return;

        CurrentPlayer = null;
        PlayerUnregistered?.Invoke(player);
    }

    public static Transform GetPlayerTransform()
    {
        if (CurrentPlayer != null)
            return CurrentPlayer.transform;

        return PlayerInteractor2D.Instance != null
            ? PlayerInteractor2D.Instance.transform
            : null;
    }

    public static T GetPlayerComponent<T>() where T : Component
    {
        var player = CurrentPlayer != null ? CurrentPlayer : PlayerInteractor2D.Instance;
        return player != null ? player.GetComponent<T>() : null;
    }
}
