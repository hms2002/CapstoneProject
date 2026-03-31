using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 책임 :
/// - 플레이어가 장착한 유물들의 개별 런타임 상태를 token 기준으로 보관하는 공용 허브다.
/// - 저장/복원/실시간 동기화가 필요한 유물 로직이 공통으로 사용할 단일 상태 저장소 역할을 한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RelicRuntimeStateHub : MonoBehaviour
{
    private readonly Dictionary<Object, string> _jsonByToken = new();
    private readonly Dictionary<Object, Action<string>> _listenersByToken = new();

    /// <summary>
    /// 책임 : 특정 token의 상태 변경 알림을 구독하고, 저장된 값이 있으면 즉시 전달한다.
    /// </summary>
    public void Bind(Object token, Action<string> listener)
    {
        if (token == null || listener == null)
            return;

        if (_listenersByToken.TryGetValue(token, out var existing))
            _listenersByToken[token] = existing + listener;
        else
            _listenersByToken[token] = listener;

        if (_jsonByToken.TryGetValue(token, out var json))
            listener.Invoke(json);
    }

    /// <summary>
    /// 책임 : 특정 token의 상태 변경 구독을 해제한다.
    /// </summary>
    public void Unbind(Object token, Action<string> listener)
    {
        if (token == null || listener == null)
            return;

        if (!_listenersByToken.TryGetValue(token, out var existing))
            return;

        existing -= listener;
        if (existing == null)
            _listenersByToken.Remove(token);
        else
            _listenersByToken[token] = existing;
    }

    /// <summary>
    /// 책임 : token의 현재 런타임 상태 JSON을 갱신한다.
    /// </summary>
    public void SetJson(Object token, string json)
    {
        if (token == null)
            return;

        if (string.IsNullOrWhiteSpace(json))
        {
            _jsonByToken.Remove(token);
            return;
        }

        _jsonByToken[token] = json;
    }

    /// <summary>
    /// 책임 : token에 저장된 현재 런타임 상태 JSON을 조회한다.
    /// </summary>
    public bool TryGetJson(Object token, out string json)
    {
        json = null;

        if (token == null)
            return false;

        return _jsonByToken.TryGetValue(token, out json) &&
               !string.IsNullOrWhiteSpace(json);
    }

    /// <summary>
    /// 책임 : 저장된 런타임 상태 JSON을 token에 복원하고, 바인딩된 로직이 있으면 즉시 전달한다.
    /// </summary>
    public void RestoreJson(Object token, string json)
    {
        if (token == null)
            return;

        if (string.IsNullOrWhiteSpace(json))
        {
            _jsonByToken.Remove(token);
            return;
        }

        _jsonByToken[token] = json;

        if (_listenersByToken.TryGetValue(token, out var listener))
            listener?.Invoke(json);
    }

    /// <summary>
    /// 책임 : token 기준으로 보관 중인 상태와 리스너 정보를 함께 정리한다.
    /// </summary>
    public void Clear(Object token)
    {
        if (token == null)
            return;

        _jsonByToken.Remove(token);
        _listenersByToken.Remove(token);
    }
}

/// <summary>
/// 책임 :
/// - 저장이 필요한 유물 로직이 자신의 런타임 상태를 캡처/복원하는 규약을 정의한다.
/// - bridge는 이 인터페이스만 통해 유물별 저장/복원 로직에 접근한다.
/// </summary>
public interface IRelicRuntimeStateSerializer
{
    bool TryCaptureRuntimeState(
        RelicContext ctx,
        RelicRuntimeStateHub hub,
        int slotIndex,
        out RelicRuntimeState state);

    void RestoreRuntimeState(
        RelicContext ctx,
        RelicRuntimeState state,
        RelicRuntimeStateHub hub);
}
