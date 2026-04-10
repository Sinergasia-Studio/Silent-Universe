using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

/// <summary>
/// DelayedInteraction — Unity 6000.3.8f1 | New Input System
/// Raycast dipancarkan dari Camera.main ke tengah layar.
///
/// File pendukung (harus ada di project):
///   IInteractable.cs, InteractableObject.cs, InteractionUI.cs
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class DelayedInteraction : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────
    [Header("Raycast")]
    [SerializeField] private float     interactRange = 2.5f;
    [SerializeField] private LayerMask interactMask  = ~0;

    [Header("Timing")]
    [SerializeField] private float holdDuration = 1.2f;

    [Header("UI (opsional)")]
    [SerializeField] private InteractionUI interactionUI;

    [Header("Events")]
    public UnityEvent<IInteractable> onInteractionComplete;
    public UnityEvent                onInteractionCancelled;

    // ─────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────
    private IInteractable _currentTarget;
    private IInteractable _activeTarget;
    private Coroutine     _holdRoutine;
    private bool          _isHolding;
    private float         _holdProgress;

    public float HoldProgress => _holdProgress;

    // cache kamera
    private Camera _cam;

    // ─────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────
    private void Awake()
    {
        _cam = Camera.main;
    }

    private void Update()
    {
        // refresh kamera kalau belum ada (misal scene load)
        if (_cam == null) _cam = Camera.main;
        ScanForTarget();
    }

    // ─────────────────────────────────────────────
    //  Input System Callback
    //  Action name: "Interact" (Button, started + canceled phases)
    // ─────────────────────────────────────────────
    public void OnInteract(InputValue value)
    {
        if (value.isPressed)
            StartHold();
        else
            CancelHold();
    }

    // ─────────────────────────────────────────────
    //  Private Logic
    // ─────────────────────────────────────────────
    private void ScanForTarget()
    {
        IInteractable found = null;

        if (_cam != null)
        {
            // ray dari tengah layar (viewport center)
            Ray ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask,
                                QueryTriggerInteraction.Collide))
            {
                hit.collider.TryGetComponent(out found);
            }
        }

        if (found != _currentTarget)
        {
            if (_isHolding) CancelHold();
            _currentTarget = found;
        }

        interactionUI?.SetPromptVisible(_currentTarget != null && !_isHolding,
                                        _currentTarget?.PromptText);
    }

    private void StartHold()
    {
        if (_currentTarget == null || !_currentTarget.CanInteract) return;
        if (_isHolding) return;

        _activeTarget = _currentTarget;
        _isHolding    = true;
        _holdRoutine  = StartCoroutine(HoldRoutine());
    }

    private void CancelHold()
    {
        if (!_isHolding) return;

        if (_holdRoutine != null) StopCoroutine(_holdRoutine);
        _holdRoutine  = null;
        _isHolding    = false;
        _holdProgress = 0f;

        interactionUI?.SetProgress(0f, false);
        onInteractionCancelled.Invoke();
    }

    private IEnumerator HoldRoutine()
    {
        interactionUI?.SetProgress(0f, true);

        float elapsed = 0f;

        while (elapsed < holdDuration)
        {
            elapsed       += Time.deltaTime;
            _holdProgress  = Mathf.Clamp01(elapsed / holdDuration);
            interactionUI?.SetProgress(_holdProgress, true);
            yield return null;
        }

        _holdProgress = 1f;
        _isHolding    = false;

        _activeTarget?.OnInteract(gameObject);
        onInteractionComplete.Invoke(_activeTarget);

        interactionUI?.SetProgress(0f, false);
        _activeTarget = null;
    }

    // ── Gizmos ──
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Camera cam = Application.isPlaying ? _cam : Camera.main;
        if (cam == null) return;

        Ray     ray     = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 pos     = ray.origin;
        Vector3 forward = ray.direction;
        Vector3 endPos  = pos + forward * interactRange;

        bool hasTarget = _currentTarget != null;

        // garis ray
        Gizmos.color = hasTarget ? Color.green : new Color(1f, 0.6f, 0f, 0.9f);
        Gizmos.DrawLine(pos, endPos);

        // titik ujung
        Gizmos.color = hasTarget ? Color.green : new Color(1f, 0.6f, 0f, 0.9f);
        Gizmos.DrawSphere(endPos, 0.04f);

        // wire sphere
        Gizmos.color = hasTarget
            ? new Color(0f, 1f, 0f, 0.08f)
            : new Color(1f, 0.6f, 0f, 0.08f);
        Gizmos.DrawSphere(pos, interactRange);

        Gizmos.color = hasTarget
            ? new Color(0f, 1f, 0f, 0.8f)
            : new Color(1f, 0.6f, 0f, 0.8f);
        Gizmos.DrawWireSphere(pos, interactRange);

        // label range
        UnityEditor.Handles.color = hasTarget ? Color.green : new Color(1f, 0.6f, 0f, 1f);
        UnityEditor.Handles.Label(endPos + Vector3.up * 0.1f, $"  {interactRange:F1}m");

        // label target
        if (hasTarget)
        {
            UnityEditor.Handles.color = Color.green;
            UnityEditor.Handles.Label(endPos + Vector3.up * 0.25f,
                $"  ▶ {_currentTarget.PromptText}");
        }
    }
#endif
}