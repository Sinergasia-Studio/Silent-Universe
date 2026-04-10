using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// CrouchSystem — mengelola seluruh logika crouch secara mandiri.
///
/// Tanggung jawab:
///   • Baca input crouch (hold behavior via InputAction)
///   • Cek ceiling sebelum membiarkan player berdiri
///   • Smooth lerp CharacterController height + center
///   • Smooth lerp posisi kamera
///   • Expose state ke sistem lain (IsCrouching, dll.)
///
/// Cara pakai:
///   1. Attach ke GameObject yang sama dengan PlayerMovement.
///   2. Assign inputActions, actionMapName, actionCrouch di Inspector.
///   3. Assign cameraTarget dan characterController di Inspector
///      (atau biarkan auto-resolve via GetComponent).
///   4. Panggil crouchSystem.SpeedMultiplier dari PlayerMovement
///      untuk scale kecepatan saat jongkok.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class CrouchSystem : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════
    //  Inspector — Input
    // ═══════════════════════════════════════════════════════════════

    [Header("── Input ──────────────────────────────────────────────")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string actionCrouch  = "Crouch";

    // ═══════════════════════════════════════════════════════════════
    //  Inspector — Shape
    // ═══════════════════════════════════════════════════════════════

    [Header("── Controller Height ──────────────────────────────────")]
    [Tooltip("Tinggi CharacterController saat berdiri")]
    [SerializeField] private float heightStand  = 2f;

    [Tooltip("Tinggi CharacterController saat jongkok")]
    [SerializeField] private float heightCrouch = 1f;

    [Tooltip("Kecepatan lerp perubahan tinggi badan (lebih tinggi = lebih cepat)")]
    [SerializeField] private float heightLerpSpeed = 12f;

    // ═══════════════════════════════════════════════════════════════
    //  Inspector — Camera
    // ═══════════════════════════════════════════════════════════════

    [Header("── Camera ──────────────────────────────────────────────")]
    [Tooltip("Transform kamera atau camera target (child dari Player)")]
    [SerializeField] private Transform cameraTarget;

    [Tooltip("localPosition.y kamera saat berdiri")]
    [SerializeField] private float cameraHeightStand  = 1.7f;

    [Tooltip("localPosition.y kamera saat jongkok")]
    [SerializeField] private float cameraHeightCrouch = 0.7f;

    [Tooltip("Kecepatan lerp pergerakan kamera (bisa beda dengan height lerp)")]
    [SerializeField] private float cameraLerpSpeed = 14f;

    // ═══════════════════════════════════════════════════════════════
    //  Inspector — Ceiling Check
    // ═══════════════════════════════════════════════════════════════

    [Header("── Ceiling Check ───────────────────────────────────────")]
    [Tooltip("Layer mask untuk ceiling check. Default ~0 (semua layer)")]
    [SerializeField] private LayerMask ceilingMask = ~0;

    [Tooltip("Toleransi radius sphere saat cek atap (0.85 = sedikit lebih kecil dari radius CC)")]
    [SerializeField] [Range(0.5f, 1f)] private float ceilingRadiusFactor = 0.85f;

    // ═══════════════════════════════════════════════════════════════
    //  Inspector — Speed
    // ═══════════════════════════════════════════════════════════════

    [Header("── Speed ───────────────────────────────────────────────")]
    [Tooltip("Multiplier kecepatan saat jongkok (misal 0.55 = 55% dari walk speed)")]
    [SerializeField] [Range(0.1f, 1f)] private float crouchSpeedMultiplier = 0.55f;

    // ═══════════════════════════════════════════════════════════════
    //  Private
    // ═══════════════════════════════════════════════════════════════

    private CharacterController _cc;
    private InputAction         _action;
    private bool                _wantsToStand; // true = input crouch dilepas, ingin berdiri

    // ═══════════════════════════════════════════════════════════════
    //  Public Read-Only
    // ═══════════════════════════════════════════════════════════════

    /// <summary>True selama player dalam state jongkok.</summary>
    public bool  IsCrouching       { get; private set; }

    /// <summary>
    /// True jika player ingin berdiri tapi ada atap menghalangi.
    /// Berguna untuk animasi "kepala nabrak langit-langit".
    /// </summary>
    public bool  IsCeilingBlocked  { get; private set; }

    /// <summary>
    /// Multiplier yang harus dikalikan ke move speed oleh PlayerMovement.
    /// Bernilai crouchSpeedMultiplier saat jongkok, 1.0 saat berdiri.
    /// </summary>
    public float SpeedMultiplier   => IsCrouching ? crouchSpeedMultiplier : 1f;

    /// <summary>
    /// Progress transisi 0→1 (0 = berdiri penuh, 1 = jongkok penuh).
    /// Bisa dipakai animator atau efek lain.
    /// </summary>
    public float CrouchProgress    { get; private set; }

    // ═══════════════════════════════════════════════════════════════
    //  Unity Lifecycle
    // ═══════════════════════════════════════════════════════════════

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();

        // Auto-resolve camera jika kosong
        if (cameraTarget == null && Camera.main != null)
            cameraTarget = Camera.main.transform;

        // Resolve action dari asset
        _action = inputActions?
            .FindActionMap(actionMapName, throwIfNotFound: false)?
            .FindAction(actionCrouch, throwIfNotFound: false);

        if (_action == null)
            Debug.LogError($"[CrouchSystem] Action '{actionCrouch}' tidak ditemukan di map '{actionMapName}'.", this);
    }

    private void Start()
    {
        // Snap ke stand height di frame pertama tanpa lerp
        SnapControllerHeight(heightStand);
        if (cameraTarget != null)
            SnapCameraHeight(cameraHeightStand);
    }

    private void OnEnable()
    {
        if (_action == null) return;
        _action.started  += OnCrouchStarted;
        _action.canceled += OnCrouchCanceled;
        inputActions?.FindActionMap(actionMapName)?.Enable();
    }

    private void OnDisable()
    {
        if (_action == null) return;
        _action.started  -= OnCrouchStarted;
        _action.canceled -= OnCrouchCanceled;
    }

    private void Update()
    {
        TickCrouch();
    }

    // ═══════════════════════════════════════════════════════════════
    //  Input Callbacks
    // ═══════════════════════════════════════════════════════════════

    private void OnCrouchStarted(InputAction.CallbackContext ctx)
    {
        IsCrouching  = true;
        _wantsToStand = false;
    }

    private void OnCrouchCanceled(InputAction.CallbackContext ctx)
    {
        _wantsToStand = true; // ingin berdiri, tapi tergantung ceiling check
    }

    // ═══════════════════════════════════════════════════════════════
    //  Tick
    // ═══════════════════════════════════════════════════════════════

    private void TickCrouch()
    {
        // Jika ingin berdiri, cek ceiling dulu
        if (_wantsToStand)
        {
            IsCeilingBlocked = CheckCeiling();

            if (!IsCeilingBlocked)
            {
                IsCrouching   = false;
                _wantsToStand = false;
            }
            // Jika masih blocked → tetap jongkok, cek lagi frame berikutnya
        }
        else
        {
            IsCeilingBlocked = false;
        }

        // Target tinggi berdasarkan state
        float targetHeight = IsCrouching ? heightCrouch : heightStand;
        float targetCamY   = IsCrouching ? cameraHeightCrouch : cameraHeightStand;

        // Lerp CC height & center
        LerpControllerHeight(targetHeight);

        // Lerp posisi kamera
        if (cameraTarget != null)
            LerpCameraHeight(targetCamY);

        // Update progress (0 = stand, 1 = crouch) untuk keperluan eksternal
        float range = heightStand - heightCrouch;
        CrouchProgress = range > 0f
            ? 1f - Mathf.Clamp01((_cc.height - heightCrouch) / range)
            : (IsCrouching ? 1f : 0f);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Ceiling Check
    // ═══════════════════════════════════════════════════════════════

    private bool CheckCeiling()
    {
        // Sphere cast dari posisi kepala saat berdiri
        Vector3 origin = transform.position + Vector3.up * (heightStand - _cc.radius);
        return Physics.CheckSphere(
            origin,
            _cc.radius * ceilingRadiusFactor,
            ceilingMask,
            QueryTriggerInteraction.Ignore
        );
    }

    // ═══════════════════════════════════════════════════════════════
    //  Height Helpers
    // ═══════════════════════════════════════════════════════════════

    private void LerpControllerHeight(float targetHeight)
    {
        float t = heightLerpSpeed * Time.deltaTime;
        float newHeight  = Mathf.Lerp(_cc.height, targetHeight, t);
        float newCenterY = Mathf.Lerp(_cc.center.y, newHeight * 0.5f, t);

        _cc.height = newHeight;
        _cc.center = new Vector3(_cc.center.x, newCenterY, _cc.center.z);
    }

    private void LerpCameraHeight(float targetY)
    {
        Vector3 pos = cameraTarget.localPosition;
        pos.y = Mathf.Lerp(pos.y, targetY, cameraLerpSpeed * Time.deltaTime);
        cameraTarget.localPosition = pos;
    }

    private void SnapControllerHeight(float height)
    {
        _cc.height = height;
        _cc.center = new Vector3(0f, height * 0.5f, 0f);
    }

    private void SnapCameraHeight(float y)
    {
        Vector3 pos = cameraTarget.localPosition;
        pos.y = y;
        cameraTarget.localPosition = pos;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Public API — dipanggil oleh sistem lain jika perlu
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Force crouch dari luar (cutscene, trigger, dsb.).
    /// </summary>
    public void ForceCrouch()
    {
        IsCrouching   = true;
        _wantsToStand = false;
    }

    /// <summary>
    /// Force berdiri dari luar — akan diabaikan jika ceiling blocked.
    /// </summary>
    public void ForceStand()
    {
        _wantsToStand = true;
    }
}