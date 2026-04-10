using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// DiskActivatedObject — GameObject yang hanya bisa di-interact
/// jika DiskBox yang ditentukan sudah berisi disk.
///
/// Setup:
///   ActivatedObject
///     ├── Collider
///     └── DiskActivatedObject  (script ini)
///
/// Inspector:
///   - diskBox        → assign DiskBox di scene
///   - onActivated    → assign event apapun (buka pintu, trigger cutscene, dll)
/// </summary>
public class DiskActivatedObject : MonoBehaviour, IInteractable
{
    [Header("Requirement")]
    [Tooltip("DiskBox yang harus sudah berisi disk sebelum bisa di-interact")]
    [SerializeField] private DiskBox diskBox;

    [Header("Prompts")]
    [SerializeField] private string promptNoDisk   = "[Disk box belum terisi]";
    [SerializeField] private string promptReady    = "Tahan [E] untuk aktifkan";
    [SerializeField] private string promptUsed     = "[Sudah diaktifkan]";

    [Header("Settings")]
    [Tooltip("Hanya bisa diaktifkan sekali")]
    [SerializeField] private bool oneTimeOnly = true;
    [Tooltip("Visual yang muncul saat sudah diaktifkan (opsional)")]
    [SerializeField] private GameObject activatedVisual;
    [Tooltip("Visual yang muncul saat disk box sudah terisi tapi belum diaktifkan (opsional)")]
    [SerializeField] private GameObject readyVisual;

    [Header("Events")]
    public UnityEvent onActivated;         // event utama saat di-interact
    public UnityEvent onAttemptNoDisk;     // saat player coba interact tapi disk belum terpasang

    // ── state ──
    private bool _activated;

    public bool IsActivated => _activated;

    private void Start()
    {
        if (activatedVisual != null) activatedVisual.SetActive(false);

        // subscribe ke DiskBox untuk update readyVisual otomatis
        if (diskBox != null)
        {
            diskBox.onDiskInserted.AddListener(OnDiskInserted);
            diskBox.onDiskEjected.AddListener(OnDiskEjected);
        }

        UpdateReadyVisual();
    }

    private void OnDestroy()
    {
        if (diskBox != null)
        {
            diskBox.onDiskInserted.RemoveListener(OnDiskInserted);
            diskBox.onDiskEjected.RemoveListener(OnDiskEjected);
        }
    }

    // ── IInteractable ──
    public string PromptText
    {
        get
        {
            if (_activated && oneTimeOnly) return promptUsed;
            bool diskReady = diskBox != null && diskBox.DiskInserted;
            return diskReady ? promptReady : promptNoDisk;
        }
    }

    public bool CanInteract
    {
        get
        {
            if (_activated && oneTimeOnly) return false;
            return diskBox != null && diskBox.DiskInserted;
        }
    }

    public void OnInteract(GameObject interactor)
    {
        if (!CanInteract)
        {
            // player coba interact tapi disk belum terpasang
            if (diskBox == null || !diskBox.DiskInserted)
                onAttemptNoDisk.Invoke();
            return;
        }

        _activated = true;

        if (activatedVisual != null) activatedVisual.SetActive(true);
        if (readyVisual     != null) readyVisual.SetActive(false);

        onActivated.Invoke();
        Debug.Log($"[DiskActivatedObject] {gameObject.name} diaktifkan!");
    }

    // ── callbacks dari DiskBox ──
    private void OnDiskInserted() => UpdateReadyVisual();
    private void OnDiskEjected()  => UpdateReadyVisual();

    private void UpdateReadyVisual()
    {
        if (readyVisual == null) return;
        bool show = diskBox != null && diskBox.DiskInserted && !_activated;
        readyVisual.SetActive(show);
    }

    /// Reset state agar bisa diaktifkan lagi (opsional)
    public void Reset()
    {
        _activated = false;
        if (activatedVisual != null) activatedVisual.SetActive(false);
        UpdateReadyVisual();
    }
}