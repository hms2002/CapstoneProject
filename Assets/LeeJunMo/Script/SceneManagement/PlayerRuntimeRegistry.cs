using System;
using UnityEngine;

public static class PlayerRuntimeRegistry
{
    public static SampleTopDownPlayer CurrentPlayer { get; private set; }

    public static event Action<SampleTopDownPlayer> PlayerRegistered;
    public static event Action<SampleTopDownPlayer> PlayerUnregistered;

    public static void Register(SampleTopDownPlayer player)
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

    public static void Unregister(SampleTopDownPlayer player)
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

        return SampleTopDownPlayer.Instance != null
            ? SampleTopDownPlayer.Instance.transform
            : null;
    }

    public static T GetPlayerComponent<T>() where T : Component
    {
        var player = CurrentPlayer != null ? CurrentPlayer : SampleTopDownPlayer.Instance;
        return player != null ? player.GetComponent<T>() : null;
    }
}
