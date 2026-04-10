using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// SprintSystem — mengelola seluruh logika sprint secara mandiri.
///
/// Tanggung jawab:
///   • Baca input sprint (hold behavior via InputAction)
///   • Validasi sprint: tidak bisa sprint saat jongkok atau diam
///   • Expose SpeedMultiplier ke PlayerMovement
///   • Expose state bersih (IsSprinting) ke sistem lain (FootstepSystem, dll.)
///
/// Cara pakai:
///   1. Attach ke GameObject yang sama dengan PlayerMovement & CrouchSystem.
///   2. Assign inputActions di Inspector (bisa share asset yang sama).
///   3. Assign referensi CrouchSystem di Inspector.
///   4. Di PlayerMovement, kalikan base speed dengan sprintSystem.SpeedMultiplier.
/// </summary>
public sealed class SprintSystem : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════
    //  Inspector — Input
    // ═══════════════════════════════════════════════════════════════

    [Header("── Input ──────────────────────────────────────────────")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string actionSprint  = "Sprint";

    // ═══════════════════════════════════════════════════════════════
    //  Inspector — Speed
    // ═══════════════════════════════════════════════════════════════

    [Header("── Speed ───────────────────────────────────────────────")]
    [Tooltip("Multiplier kecepatan saat sprint (misal 1.75 = 175% dari walk speed)")]
    [SerializeField] [Range(1f, 3f)] private float sprintSpeedMultiplier = 1.75f;

    // ═══════════════════════════════════════════════════════════════
    //  Inspector — Dependencies
    // ═══════════════════════════════════════════════════════════════

    [Header("── Dependencies ────────────────────────────────────────")]
    [Tooltip("Referensi CrouchSystem — sprint tidak aktif saat jongkok")]
    [SerializeField] private CrouchSystem crouchSystem;

    // ═══════════════════════════════════════════════════════════════
    //  Private
    // ═══════════════════════════════════════════════════════════════

    private InputAction _action;
    private bool        _inputHeld; // true selama tombol sprint ditekan

    // ═══════════════════════════════════════════════════════════════
    //  Public Read-Only
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// True hanya jika tombol sprint ditekan DAN player tidak jongkok.
    /// PlayerMovement harus cek IsMoving sendiri sebelum apply multiplier.
    /// </summary>
    public bool IsSprinting => _inputHeld && !IsCrouching;

    /// <summary>
    /// Multiplier yang harus dikalikan ke walk speed oleh PlayerMovement.
    /// Bernilai sprintSpeedMultiplier saat sprint aktif, 1.0 saat tidak.
    /// </summary>
    public float SpeedMultiplier => IsSprinting ? sprintSpeedMultiplier : 1f;

    // Shortcut — baca dari CrouchSystem jika ada, default false jika tidak
    private bool IsCrouching => crouchSystem != null && crouchSystem.IsCrouching;

    // ═══════════════════════════════════════════════════════════════
    //  Unity Lifecycle
    // ═══════════════════════════════════════════════════════════════

    private void Awake()
    {
        // Auto-resolve CrouchSystem dari GameObject yang sama jika tidak di-assign
        if (crouchSystem == null)
            crouchSystem = GetComponent<CrouchSystem>();

        _action = inputActions?
            .FindActionMap(actionMapName, throwIfNotFound: false)?
            .FindAction(actionSprint, throwIfNotFound: false);

        if (_action == null)
            Debug.LogError($"[SprintSystem] Action '{actionSprint}' tidak ditemukan di map '{actionMapName}'.", this);
    }

    private void OnEnable()
    {
        if (_action == null) return;
        _action.started  += OnSprintStarted;
        _action.canceled += OnSprintCanceled;
        inputActions?.FindActionMap(actionMapName)?.Enable();
    }

    private void OnDisable()
    {
        if (_action == null) return;
        _action.started  -= OnSprintStarted;
        _action.canceled -= OnSprintCanceled;

        // Pastikan state reset saat disabled
        _inputHeld = false;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Input Callbacks
    // ═══════════════════════════════════════════════════════════════

    private void OnSprintStarted(InputAction.CallbackContext ctx)  => _inputHeld = true;
    private void OnSprintCanceled(InputAction.CallbackContext ctx) => _inputHeld = false;
}