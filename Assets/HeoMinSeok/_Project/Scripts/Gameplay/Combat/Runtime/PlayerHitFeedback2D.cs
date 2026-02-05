using System.Collections;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Player hit feedback:
    /// 1) white flash
    /// 2) hit animation trigger
    /// 3) stun (disable control components for a short time) + cancel ability execution
    /// 4) camera shake
    ///
    /// Tag-based immunity (optional):
    /// - hitReactImmuneTag: skips flash/animation/shake
    /// - stunImmuneTag: skips stun + ability cancel
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHitFeedback2D : MonoBehaviour, IHitFeedbackReceiver2D
    {
        [Header("Rendering")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private float flashSeconds = 0.08f;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string hitTrigger = "Hit";

        [Header("Stun")]
        [SerializeField] private float defaultStunSeconds = 0.3f;
        [Tooltip("Components to disable during stun (movement / input / attack scripts, etc).")]
        [SerializeField] private Behaviour[] componentsToDisable;

        [Header("Camera Shake")]
        [SerializeField] private SimpleCameraShake2D cameraShake;
        [SerializeField] private float defaultShake = 0.10f;

        [Header("Immunity Tags (Optional)")]
        [Tooltip("If the target has this tag, flash/animation/camera shake are ignored.")]
        [SerializeField] private GameplayTag hitReactImmuneTag;

        [Tooltip("If the target has this tag, stun + ability cancel are ignored.")]
        [SerializeField] private GameplayTag stunImmuneTag;

        private TagSystem _tags;
        private AbilitySystem _abilitySystem;
        private int _hitTriggerHash;
        private Coroutine _stunRoutine;
        private Coroutine _flashRoutine;
        private Color _originalColor;
        private bool _hasOriginalColor;

        private void Awake()
        {
            _tags = GetComponent<TagSystem>();
            _abilitySystem = GetComponent<AbilitySystem>();

            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (cameraShake == null && Camera.main != null) cameraShake = Camera.main.GetComponent<SimpleCameraShake2D>();

            _hitTriggerHash = !string.IsNullOrEmpty(hitTrigger) ? Animator.StringToHash(hitTrigger) : 0;

            if (spriteRenderer != null)
            {
                _originalColor = spriteRenderer.color;
                _hasOriginalColor = true;
            }
        }

        public void OnHitFeedback(HitFeedbackPayload payload)
        {
            bool reactImmune = hitReactImmuneTag != null && _tags != null && _tags.HasTag(hitReactImmuneTag);
            bool stunImmune = stunImmuneTag != null && _tags != null && _tags.HasTag(stunImmuneTag);

            float stun = payload.StunSeconds > 0f ? payload.StunSeconds : defaultStunSeconds;
            float shake = payload.CameraShake > 0f ? payload.CameraShake : defaultShake;

            if (!reactImmune)
            {
                if (_hitTriggerHash != 0 && animator != null)
                    animator.SetTrigger(_hitTriggerHash);

                if (spriteRenderer != null)
                {
                    if (_flashRoutine != null) StopCoroutine(_flashRoutine);
                    _flashRoutine = StartCoroutine(CoFlashWhite());
                }

                if (cameraShake != null && shake > 0f)
                    cameraShake.Shake(shake);
            }

            if (!stunImmune && stun > 0f)
            {
                if (_stunRoutine != null) StopCoroutine(_stunRoutine);
                _stunRoutine = StartCoroutine(CoStun(stun));
            }
        }

        private IEnumerator CoFlashWhite()
        {
            if (spriteRenderer == null) yield break;

            // If you use a special shader for hit flash, replace this with that logic.
            if (!_hasOriginalColor)
            {
                _originalColor = spriteRenderer.color;
                _hasOriginalColor = true;
            }

            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(flashSeconds);
            if (spriteRenderer != null)
                spriteRenderer.color = _originalColor;
        }

        private IEnumerator CoStun(float seconds)
        {
            // cancel current ability motion (if any)
            if (_abilitySystem != null)
            {
                // These methods exist in your UnityGAS implementation (used elsewhere).
                _abilitySystem.CancelCasting(force: true);
                _abilitySystem.CancelExecution(force: true);
            }

            if (componentsToDisable != null)
            {
                for (int i = 0; i < componentsToDisable.Length; i++)
                    if (componentsToDisable[i] != null)
                        componentsToDisable[i].enabled = false;
            }

            yield return new WaitForSeconds(seconds);

            if (componentsToDisable != null)
            {
                for (int i = 0; i < componentsToDisable.Length; i++)
                    if (componentsToDisable[i] != null)
                        componentsToDisable[i].enabled = true;
            }
        }
    }
}
