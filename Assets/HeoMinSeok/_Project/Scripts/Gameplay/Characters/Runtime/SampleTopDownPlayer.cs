using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SampleTopDownPlayer : MonoBehaviour, IPlayerInteractor
{
    public static SampleTopDownPlayer Instance { get; private set; }

    public Transform Transform => transform;
    public InteractState CurrentState { get; private set; } = InteractState.Idle;

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    [Header("Speech System")]
    [SerializeField] private SpeechBubbleComponent speechBubble;
    [SerializeField] private PlayerSpeechData speechData;

    private readonly List<IInteractable> nearbyObjects = new();
    private IInteractable currentTarget;

    private void Awake()
    {
        Instance = this;
    }

    public void SetInteractState(InteractState state)
    {
        CurrentState = state;

        if (state == InteractState.Talking && currentTarget != null)
        {
            currentTarget.OnUnHighlight();
            currentTarget = null;
        }
    }

    private void Update()
    {
        if (CurrentState == InteractState.Talking)
            return;

        HandleInteractSearch();

        if (Input.GetKeyDown(interactKey) &&
            currentTarget != null &&
            currentTarget.CanInteract(this))
        {
            currentTarget.OnPlayerInteract(this);
        }
    }

    // 상황에 맞는 대사를 띄워주는 핵심 헬퍼 함수
    public void SpeakSituation(PlayerSpeechSituationEnum situation, float duration = 2f)
    {
        if (speechData == null || speechBubble == null)
        {
            Debug.LogWarning("[Player] SpeechData 또는 SpeechBubbleComponent가 연결되지 않았습니다!");
            return;
        }

        string line = speechData.GetLine(situation);
        if (!string.IsNullOrEmpty(line))
        {
            speechBubble.Speak(line, duration);
        }
    }

    private void HandleInteractSearch()
    {
        IInteractable nearest = GetClosestInteractable();

        if (nearest != currentTarget)
        {
            if (currentTarget != null)
                currentTarget.OnUnHighlight();

            currentTarget = nearest;

            if (currentTarget != null)
                currentTarget.OnHighlight();
        }
    }

    private IInteractable GetClosestInteractable()
    {
        if (nearbyObjects.Count == 0)
            return null;

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

            var mbObj = (MonoBehaviour)obj;
            float dist = Vector2.Distance(transform.position, mbObj.transform.position);

            if (dist < closestDist && obj.CanInteract(this))
            {
                closestDist = dist;
                closestObj = obj;
            }
        }

        return closestObj;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out IInteractable interactable))
        {
            if (!nearbyObjects.Contains(interactable))
            {
                nearbyObjects.Add(interactable);
                interactable.OnPlayerNearby();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent(out IInteractable interactable))
        {
            if (nearbyObjects.Contains(interactable))
            {
                interactable.OnPlayerLeave();

                if (currentTarget == interactable)
                {
                    currentTarget.OnUnHighlight();
                    currentTarget = null;
                }

                nearbyObjects.Remove(interactable);
            }
        }
    }
}