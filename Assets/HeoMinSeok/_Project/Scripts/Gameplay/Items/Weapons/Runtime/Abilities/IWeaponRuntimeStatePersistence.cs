/// <summary>
/// 책임 :
/// - 저장이 필요한 무기 런타임 상태가 자신의 직렬화 포맷과 복원 진입점을 직접 정의하게 만든다.
/// - 모든 무기 상태에 저장을 강제하지 않고, 필요한 무기만 opt-in 하도록 경계를 분리한다.
/// </summary>
public interface IWeaponRuntimeStatePersistence
{
    string StateType { get; }

    string CaptureStateJson();

    void RestoreStateJson(string json);
}
