using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : Flowering Bloom gameplay 흐름이 concrete presentation 구현 없이 컷인과 활성화 연출을 요청하는 계약을 제공한다.
/// </summary>
public interface IFloweringBloomPresentation
{
    void Initialize(GameObject ownerObject, FloweringBloomData bloomData);
    IEnumerator PlayCutIn(AbilitySystem system, AbilitySpec spec, FloweringBloomData bloomData);
    IEnumerator PlayBloomEndTransition(AbilitySpec spec, FloweringBloomData bloomData);
    void BeginActiveBloom(FloweringBloomData bloomData);
    void Release();
}

/// <summary>
/// 책임 : Flowering Bloom presentation 구현을 gameplay 소유 오브젝트에 준비하는 backend 계약을 제공한다.
/// </summary>
public interface IFloweringBloomPresentationBackend
{
    IFloweringBloomPresentation GetOrAdd(GameObject ownerObject, FloweringBloomData bloomData);
}

/// <summary>
/// 책임 : Flowering gameplay 코드가 concrete presentation 컴포넌트를 직접 참조하지 않고 현재 등록된 backend를 호출하게 한다.
/// </summary>
public static class FloweringBloomPresentationPlayback
{
    private static IFloweringBloomPresentationBackend backend;

    public static void RegisterBackend(IFloweringBloomPresentationBackend presentationBackend)
    {
        backend = presentationBackend;
    }

    public static IFloweringBloomPresentation GetOrAdd(GameObject ownerObject, FloweringBloomData bloomData)
    {
        return backend != null ? backend.GetOrAdd(ownerObject, bloomData) : null;
    }
}
