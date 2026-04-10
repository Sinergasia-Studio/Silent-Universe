using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MonitorInteractable : MonoBehaviour, IInteractable
{
    [Header("Raw Images")]
    [SerializeField] private GameObject rawImage1;
    [SerializeField] private GameObject rawImageDefault;
    [SerializeField] private CCTVCamera defaultCamera;
    [Tooltip("Daftarkan SEMUA RawImage CCTV di sini — pastikan tidak ada yang ketinggalan")]
    [SerializeField] private List<GameObject> allCCTVRawImages = new();

    [Header("Disk Requirement")]
    [SerializeField] private DiskBox diskBox;

    [Header("CCTV Switch Buttons")]
    [SerializeField] private List<CCTVSwitchButton> switchButtons = new();

    [Header("FNAF Pan Settings")]
    [SerializeField] [Range(0.1f, 0.5f)] private float deadZone     = 0.25f;
    [SerializeField] private float maxPanSpeed  = 55f;
    [SerializeField] private float acceleration = 6f;
    [SerializeField] private float friction     = 8f;
    [SerializeField] [Range(0.1f, 1f)] private float verticalScale = 0.4f;

    [Header("Switch Raycast")]
    [SerializeField] private LayerMask switchButtonMask = ~0;
    [SerializeField] private float     raycastDistance  = 30f;

    [Header("Prompts")]
    [SerializeField] private string promptNoDisk = "[Monitor mati — butuh disk]";
    [SerializeField] private string promptEnter  = "Tahan [E] untuk lihat kamera";
    [SerializeField] private string promptExit   = "Tekan [Esc] untuk keluar";

    [Header("Player")]
    [SerializeField] private PlayerMovement playerMovement;

    private bool             _isCCTVActive;
    private bool             _isPaused;        // true saat di scene Dampener
    private bool             _isTransitioning;
    private float            _lastSwitchTime = -10f;
    private float            _lastFuseTime   = -10f;
    private float            _velH;

    /// BUG FIX #2 — Ekspor flag CCTV aktif agar DialogueManager bisa
    /// menghindari hardlock cursor ke Locked saat player masih di mode CCTV.
    public bool IsCCTVActive => _isCCTVActive;
    private float            _velV;
    private CCTVCamera       _activeCCTVCamera;
    private Camera           _activeCam;
    private GameObject       _activeRawImage;
    private CCTVSwitchButton _highlightedButton;

    // ── Public property untuk CCTVAutoRestore & ScenePortal ──
    public CCTVCamera ActiveCCTVCamera => _activeCCTVCamera;

    private bool DiskReady => diskBox != null && diskBox.DiskInserted;

    public string PromptText  => _isCCTVActive ? promptExit : (DiskReady ? promptEnter : promptNoDisk);
    public bool   CanInteract => DiskReady && !_isCCTVActive;

    private void Start()
    {
        rawImage1.SetActive(true);
        DisableAllCCTVRawImages();
    }

    public void OnInteract(GameObject interactor)
    {
        if (!CanInteract) return;

        _isCCTVActive = true;
        GameState.IsCCTVActive = true; // BUG FIX #2 — sync ke Core flag
        _velH = 0f;
        _velV = 0f;

        rawImage1.SetActive(false);
        ActivateRawImage(rawImageDefault, defaultCamera);

        if (playerMovement != null) playerMovement.SetInputEnabled(false);
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible   = true;

        SanitySystem.Instance?.SetCCTVActive(true);
    }

    /// <summary>
    /// Versi OnInteract yang bypass cek CanInteract.
    /// Dipakai oleh CCTVAutoRestore saat restore state setelah load scene.
    /// </summary>
    public void ForceEnterCCTV()
    {
        _isCCTVActive = true;
        GameState.IsCCTVActive = true; // BUG FIX #2 — sync ke Core flag
        _velH = 0f;
        _velV = 0f;

        rawImage1.SetActive(false);
        ActivateRawImage(rawImageDefault, defaultCamera);

        if (playerMovement != null) playerMovement.SetInputEnabled(false);
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible   = true;

        // Tampilkan prompt Esc
        var ui = FindFirstObjectByType<InteractionUI>();
        if (ui != null) ui.SetPromptVisible(true, promptExit);
    }

    private InteractionUI _interactionUI;

    private void Update()
    {
        if (!_isCCTVActive) return;
        if (_isPaused) return;  // sedang di scene Dampener, skip semua input

        // Selalu tampilkan prompt Esc selama CCTV aktif
        if (_interactionUI == null) _interactionUI = FindFirstObjectByType<InteractionUI>();
        if (_interactionUI != null) _interactionUI.SetPromptVisible(true, promptExit);

        HandleSwitchRaycast();
        HandlePan();

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            ExitCCTV();
    }

    private void HandleSwitchRaycast()
    {
        if (_activeCam == null || Mouse.current == null) return;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();

        Vector2 viewport;
        if (_activeRawImage != null)
        {
            RectTransform rt = _activeRawImage.GetComponent<RectTransform>();
            if (rt == null) return;

            // BUG FIX #4 — Gunakan canvas camera yang sesuai (bukan null).
            // null hanya benar untuk Screen Space Overlay; World/Camera Space membutuhkan camera eksplisit.
            Canvas canvas = rt.GetComponentInParent<Canvas>();
            Camera canvasCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera ?? Camera.main
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rt, mouseScreen, canvasCam, out Vector2 localPoint)) return;

            Rect rect = rt.rect;
            viewport = new Vector2(
                Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x),
                Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y)
            );

            if (viewport.x < 0f || viewport.x > 1f ||
                viewport.y < 0f || viewport.y > 1f)
            {
                ClearHighlight();
                return;
            }
        }
        else
        {
            viewport = new Vector2(mouseScreen.x / Screen.width, mouseScreen.y / Screen.height);
        }

        Ray ray = _activeCam.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
        Debug.DrawRay(ray.origin, ray.direction * raycastDistance, Color.red);

        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, switchButtonMask))
        {
            var btn = hit.collider.GetComponent<CCTVSwitchButton>();
            if (btn != null)
            {
                if (_highlightedButton != btn)
                {
                    _highlightedButton?.SetActive(false);
                    _highlightedButton = btn;
                    _highlightedButton.SetActive(true);
                }
                if (Mouse.current.leftButton.wasPressedThisFrame && Time.time - _lastSwitchTime > 0.5f)
                {
                    _lastSwitchTime = Time.time;
                    SwitchTo(btn);
                }
                return;
            }

            var portal = hit.collider.GetComponent<ScenePortalButton>();
            if (portal != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame && !_isTransitioning)
                {
                    _isTransitioning = true;
                    portal.Enter();
                }
                return;
            }

            var fuse = hit.collider.GetComponent<FuseSwitcher>();
            if (fuse != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame && Time.time - _lastFuseTime > 0.5f)
                {
                    _lastFuseTime = Time.time;
                    fuse.Toggle();
                }
                return;
            }
        }

        ClearHighlight();
    }

    private void SwitchTo(CCTVSwitchButton btn, bool addNoise = true)
    {
        if (btn.targetRawImage == _activeRawImage) return;
        ActivateRawImage(btn.targetRawImage, btn.targetCamera);
        _velH = 0f;
        _velV = 0f;

        if (addNoise)
            NoiseTracker.Instance?.AddNoiseSwitchCamera();
    }

    /// <summary>
    /// Switch ke kamera berdasarkan referensi CCTVCamera langsung.
    /// Dipakai oleh CCTVAutoRestore untuk restore kamera terakhir.
    /// </summary>
    public void SwitchToCameraByRef(CCTVCamera targetCamera)
    {
        if (targetCamera == null) return;

        foreach (var btn in switchButtons)
        {
            if (btn != null && btn.targetCamera == targetCamera)
            {
                SwitchTo(btn, false); // restore — tidak trigger noise
                return;
            }
        }

        // Fallback jika tidak ada button yang cocok
        Debug.LogWarning($"[Monitor] Tidak ada SwitchButton untuk kamera '{targetCamera.cameraName}', aktifkan langsung.");
        ActivateRawImage(null, targetCamera);
    }

    private void DisableAllCCTVRawImages()
    {
        foreach (var ri in allCCTVRawImages)
            if (ri != null) ri.SetActive(false);

        if (rawImageDefault != null) rawImageDefault.SetActive(false);
        foreach (var b in switchButtons)
            if (b != null && b.targetRawImage != null) b.targetRawImage.SetActive(false);
    }

    private void ActivateRawImage(GameObject targetRawImage, CCTVCamera targetCamera)
    {
        if (_activeCCTVCamera != null) _activeCCTVCamera.ResetPan();

        DisableAllCCTVRawImages();

        if (targetRawImage != null) targetRawImage.SetActive(true);
        _activeRawImage   = targetRawImage;
        _activeCCTVCamera = targetCamera;
        _activeCam        = targetCamera != null ? targetCamera.GetComponent<Camera>() : null;

        _velH = 0f;
        _velV = 0f;

        foreach (var b in switchButtons)
            if (b != null) b.SetActive(b.targetRawImage == targetRawImage);
    }

    private void ClearHighlight()
    {
        if (_highlightedButton != null)
        {
            _highlightedButton.SetActive(false);
            _highlightedButton = null;
        }
    }

    private void HandlePan()
    {
        if (_activeCCTVCamera == null || Mouse.current == null) return;

        float nx = Mouse.current.position.ReadValue().x / Screen.width;
        float ny = Mouse.current.position.ReadValue().y / Screen.height;

        float inputH = ComputeInput(nx);
        float inputV = ComputeInput(ny) * verticalScale;

        _velH = Mathf.Abs(inputH) > 0.01f
            ? Mathf.MoveTowards(_velH, inputH * maxPanSpeed, acceleration * maxPanSpeed * Time.deltaTime)
            : Mathf.MoveTowards(_velH, 0f, friction * maxPanSpeed * Time.deltaTime);

        _velV = Mathf.Abs(inputV) > 0.01f
            ? Mathf.MoveTowards(_velV, inputV * maxPanSpeed, acceleration * maxPanSpeed * Time.deltaTime)
            : Mathf.MoveTowards(_velV, 0f, friction * maxPanSpeed * Time.deltaTime);

        bool rightClick = Mouse.current != null && Mouse.current.rightButton.isPressed;
        _activeCCTVCamera.ApplyPan(_velH, _velV, rightClick, Time.deltaTime);
    }

    private float ComputeInput(float normalized)
    {
        float fromCenter = (normalized - 0.5f) / 0.5f;
        float absF       = Mathf.Abs(fromCenter);
        if (absF < deadZone) return 0f;
        float t = (absF - deadZone) / (1f - deadZone);
        t = t * t;
        return Mathf.Sign(fromCenter) * t;
    }

    /// <summary>
    /// Pause/resume MonitorInteractable saat player di scene Dampener.
    /// CCTV tetap aktif (IsCCTVActive = true), hanya input yang di-pause.
    /// </summary>
    public void SetPaused(bool paused)
    {
        _isPaused = paused;

        // Reset flag transisi agar portal bisa dipakai lagi
        if (!paused)
            _isTransitioning = false;
    }

    public void ExitCCTV()
    {
        if (_activeCCTVCamera != null) _activeCCTVCamera.ResetPan();

        DisableAllCCTVRawImages();
        ClearHighlight();
        foreach (var b in switchButtons) if (b != null) b.SetActive(false);

        if (rawImage1 != null) rawImage1.SetActive(true);

        CCTVSaveData.Instance.ClearCCTV(); // hapus save state CCTV saat keluar normal
        SanitySystem.Instance?.SetCCTVActive(false);

        _isCCTVActive     = false;
        GameState.IsCCTVActive = false; // BUG FIX #2 — sync ke Core flag
        _activeRawImage   = null;
        _activeCCTVCamera = null;
        _activeCam        = null;
        _velH = 0f;
        _velV = 0f;

        if (playerMovement != null) playerMovement.SetInputEnabled(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }
}