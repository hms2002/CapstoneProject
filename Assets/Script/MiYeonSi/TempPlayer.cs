using System.Collections.Generic;
using UnityEngine;

public class TempPlayer : MonoBehaviour, IPlayerInteractor
{
    public static TempPlayer Instance { get; private set; }

    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 5f;
     
    [Header("상호작용 설정")]
    private readonly List<IInteractable> nearbyObjects = new();
    private IInteractable currentTarget; // 현재 가장 가까운 타겟

    public InteractState CurrentState { get; private set; } = InteractState.Idle;

    public Transform Transform => transform;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Update()
    {
        if (CurrentState == InteractState.Talking) return;

        HandleMovement();
        HandleInteractSearch();

        // 상호작용 실행
        if (Input.GetKeyDown(KeyCode.F) && currentTarget != null)
        {
            currentTarget.OnPlayerInteract(this);
        }
    }

    private void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 moveDir = new Vector3(h, v, 0).normalized;
        transform.position += moveDir * moveSpeed * Time.deltaTime;
    }

    private void HandleInteractSearch()
    {
        IInteractable nearest = GetClosestInteractable();

        // 최단 거리 타겟이 변경되었을 때 (하이라이트 관리)
        if (nearest != currentTarget)
        {
            if (currentTarget != null)
            {
                currentTarget.OnUnHighlight();
            }

            currentTarget = nearest;

            if (currentTarget != null)
            {
                currentTarget.OnHighlight();
                // 기존 프로젝트의 GameEvents가 있다면 여기서 호출 가능
                // GameEvents.OnShowInteractKey?.Invoke(((MonoBehaviour)currentTarget).transform, currentTarget.GetInteractDescription());
            }
        }
    }

    private IInteractable GetClosestInteractable()
    {
        if (nearbyObjects.Count == 0) return null;

        float closestDist = float.MaxValue;
        IInteractable closestObj = null;

        for (int i = nearbyObjects.Count - 1; i >= 0; i--)
        {
            var obj = nearbyObjects[i];
            if (obj == null || (obj is MonoBehaviour mb && mb == null))
            {
                nearbyObjects.RemoveAt(i);
                continue;
            }

            float dist = Vector2.Distance(transform.position, ((MonoBehaviour)obj).transform.position);
            if (dist < closestDist)
            {
                if (obj.CanInteract(this))
                {
                    closestDist = dist;
                    closestObj = obj;
                }
            }
        }
        return closestObj;
    }

    #region 트리거 기반 리스트 관리 및 Nearby/Leave 호출

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("주변 오브젝트 확인");
        if (other.TryGetComponent(out IInteractable interactable))
        {
            if (!nearbyObjects.Contains(interactable))
            {
                nearbyObjects.Add(interactable);
                interactable.OnPlayerNearby(); // [복구] 시각적 가이드 활성화
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent(out IInteractable interactable))
        {
            if (nearbyObjects.Contains(interactable))
            {
                interactable.OnPlayerLeave(); // [복구] 시각적 가이드 비활성화

                if (currentTarget == interactable)
                {
                    currentTarget.OnUnHighlight();
                    currentTarget = null;
                }
                nearbyObjects.Remove(interactable);
            }
        }
    }

    #endregion

    public void SetInteractState(InteractState state)
    {
        CurrentState = state;

        // 대화 시작 시 타겟 하이라이트 해제 (visualCue는 유지할지 여부에 따라 OnPlayerLeave 호출 가능)
        if (state == InteractState.Talking && currentTarget != null)
        {
            currentTarget.OnUnHighlight();
            currentTarget = null;
        }
    }
}