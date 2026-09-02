using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임 : 현재 로드된 씬의 활성 SceneTravelEndpoint를 씬 handle과 endpoint Id로 조회할 수 있게 등록하고 중복을 차단한다.
/// </summary>
public static class SceneTravelEndpointRegistry
{
    /// <summary>
    /// 책임 : 로드된 씬의 raw handle과 endpoint Id 조합을 registry dictionary의 안정 키로 표현한다.
    /// </summary>
    private readonly struct EndpointKey : IEquatable<EndpointKey>
    {
        public ulong SceneHandle { get; }
        public string EndpointId { get; }

        public EndpointKey(ulong sceneHandle, string endpointId)
        {
            SceneHandle = sceneHandle;
            EndpointId = endpointId ?? string.Empty;
        }

        public bool Equals(EndpointKey other)
        {
            return SceneHandle == other.SceneHandle &&
                   string.Equals(EndpointId, other.EndpointId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is EndpointKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int sceneHash = (int)(SceneHandle ^ (SceneHandle >> 32));
                return (sceneHash * 397) ^ StringComparer.Ordinal.GetHashCode(EndpointId);
            }
        }
    }

    private static readonly Dictionary<EndpointKey, SceneTravelEndpoint> Endpoints = new();

    public static bool Register(SceneTravelEndpoint endpoint)
    {
        if (!TryCreateKey(endpoint, out EndpointKey key))
            return false;

        if (Endpoints.TryGetValue(key, out SceneTravelEndpoint existing) && existing != null && existing != endpoint)
        {
            Debug.LogError(
                $"[SceneTravelEndpointRegistry] Duplicate endpoint Id '{endpoint.EndpointId}' in scene '{endpoint.gameObject.scene.name}'.",
                endpoint);
            return false;
        }

        Endpoints[key] = endpoint;
        return true;
    }

    public static void Unregister(SceneTravelEndpoint endpoint)
    {
        if (!TryCreateKey(endpoint, out EndpointKey key))
            return;

        if (Endpoints.TryGetValue(key, out SceneTravelEndpoint existing) && existing == endpoint)
            Endpoints.Remove(key);
    }

    public static bool TryGet(Scene scene, string endpointId, out SceneTravelEndpoint endpoint)
    {
        endpoint = null;
        if (!scene.IsValid() || string.IsNullOrWhiteSpace(endpointId))
            return false;

        EndpointKey key = new(scene.handle.GetRawData(), endpointId);
        if (!Endpoints.TryGetValue(key, out endpoint) || endpoint == null)
        {
            Endpoints.Remove(key);
            endpoint = null;
            return false;
        }

        return true;
    }

    public static bool TryGetActiveScene(string endpointId, out SceneTravelEndpoint endpoint)
    {
        return TryGet(SceneManager.GetActiveScene(), endpointId, out endpoint);
    }

    private static bool TryCreateKey(SceneTravelEndpoint endpoint, out EndpointKey key)
    {
        key = default;
        if (endpoint == null || string.IsNullOrWhiteSpace(endpoint.EndpointId))
            return false;

        Scene scene = endpoint.gameObject.scene;
        if (!scene.IsValid())
            return false;

        key = new EndpointKey(scene.handle.GetRawData(), endpoint.EndpointId);
        return true;
    }
}
