using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGAS;

[DisallowMultipleComponent]
public sealed class PlayerDeathReturnToHub2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private AttributeSet attributeSet;
    [SerializeField] private AttributeDefinition hpDef;
    [SerializeField] private SampleTopDownPlayer player;
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private PlayerIntentInput2D intentInput;
    [SerializeField] private PlayerCombatInput2D combatInput;
    [SerializeField] private PlayerAim2D aimInput;
    [SerializeField] private PlayerHitFeedback2D hitFeedback;
    [SerializeField] private PlayerDeathPresentation2D deathPresentation;

    [Header("Transition")]
    [SerializeField] private string hubSceneName = "ProtoTypeHub";
    [SerializeField] private float fallbackDelaySeconds = 1.25f;

    [Header("Optional Extra Blockers")]
    [SerializeField] private Behaviour[] additionalBehavioursToDisable;
    [SerializeField] private Collider2D[] collidersToDisable;

    private bool isDeathSequenceRunning;

    private void Awake()
    {
        if (attributeSet == null) attributeSet = GetComponent<AttributeSet>();
        if (player == null) player = GetComponent<SampleTopDownPlayer>();
        if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();
        if (body == null) body = GetComponent<Rigidbody2D>();
        if (intentInput == null) intentInput = GetComponent<PlayerIntentInput2D>();
        if (combatInput == null) combatInput = GetComponent<PlayerCombatInput2D>();
        if (aimInput == null) aimInput = GetComponent<PlayerAim2D>();
        if (hitFeedback == null) hitFeedback = GetComponent<PlayerHitFeedback2D>();
        if (deathPresentation == null) deathPresentation = GetComponent<PlayerDeathPresentation2D>();

        if (collidersToDisable == null || collidersToDisable.Length == 0)
            collidersToDisable = GetComponentsInChildren<Collider2D>(includeInactive: false);
    }

    private void OnEnable()
    {
        if (attributeSet != null)
            attributeSet.OnAttributeChanged += HandleAttributeChanged;

        TryStartDeathSequenceFromCurrentHp();
    }

    private void OnDisable()
    {
        if (attributeSet != null)
            attributeSet.OnAttributeChanged -= HandleAttributeChanged;
    }

    private void HandleAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        if (attribute != hpDef || isDeathSequenceRunning)
            return;

        if (newValue > 0f)
            return;

        StartCoroutine(CoDeathSequence());
    }

    private void TryStartDeathSequenceFromCurrentHp()
    {
        if (isDeathSequenceRunning || attributeSet == null || hpDef == null)
            return;

        if (attributeSet.GetAttributeValue(hpDef) <= 0f)
            StartCoroutine(CoDeathSequence());
    }

    private IEnumerator CoDeathSequence()
    {
        if (isDeathSequenceRunning)
            yield break;

        isDeathSequenceRunning = true;

        BlockPlayerControl();

        if (deathPresentation != null)
        {
            yield return deathPresentation.Play();
        }
        else if (fallbackDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(fallbackDelaySeconds);
        }

        ReturnToHub();
    }

    private void BlockPlayerControl()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseAllPopups();
            UIManager.Instance.HideHoverImmediate();
            UIManager.Instance.HideWorldPrompt();
        }

        hitFeedback?.ForceEndReaction();

        if (abilitySystem != null)
        {
            abilitySystem.CancelCasting(force: true);
            abilitySystem.CancelExecution(force: true);
            abilitySystem.enabled = false;
        }

        if (player != null)
            player.SetInteractState(InteractState.None);

        if (intentInput != null) intentInput.enabled = false;
        if (combatInput != null) combatInput.enabled = false;
        if (aimInput != null) aimInput.enabled = false;
        if (player != null) player.enabled = false;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        if (collidersToDisable != null)
        {
            for (int i = 0; i < collidersToDisable.Length; i++)
            {
                if (collidersToDisable[i] != null)
                    collidersToDisable[i].enabled = false;
            }
        }

        if (additionalBehavioursToDisable != null)
        {
            for (int i = 0; i < additionalBehavioursToDisable.Length; i++)
            {
                if (additionalBehavioursToDisable[i] != null)
                    additionalBehavioursToDisable[i].enabled = false;
            }
        }
    }

    private void ReturnToHub()
    {
        if (GamePlayDataManager.Instance != null)
            GamePlayDataManager.Instance.EndRun(RunEndReason.Defeat);

        SceneManager.LoadScene(hubSceneName);
    }
}
