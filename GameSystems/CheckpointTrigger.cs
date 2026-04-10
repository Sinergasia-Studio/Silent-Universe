using UnityEngine;
using UnityEngine.Events;
using VContainer;

/// <summary>
/// CheckpointTrigger — pasang pada Collider 3D (Is Trigger = true).
/// Saat player masuk area ini, otomatis save game.
///
/// Setup:
///   1. Buat GameObject kosong, pasang Collider 3D, centang Is Trigger.
///   2. Pasang script ini.
///   3. Sesuaikan playerTag jika perlu.
///
/// Alternatif: panggil GameSave.Save() manual dari script lain
/// (misal dari ScenePortal saat player pindah scene).
/// </summary>
public class CheckpointTrigger : MonoBehaviour
{
    [SerializeField] private string playerTag   = "Player";
    [SerializeField] private bool   triggerOnce = true;

    [Header("Visual (opsional)")]
    [Tooltip("GameObject yang berubah saat checkpoint aktif (misal light, particle)")]
    [SerializeField] private GameObject activatedVisual;

    public UnityEvent onCheckpointReached;

    // BUG FIX #4 — Inject NoiseTracker agar tetap bekerja setelah migrasi VContainer.
    // Fallback ke .Instance dipertahankan untuk kompatibilitas scene yang belum punya SceneLifetimeScope.
    [Inject] private NoiseTracker _noiseTracker;

    private bool _triggered;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (triggerOnce && _triggered) return;

        _triggered = true;

        // BUG FIX #3 — Push nilai noise ke GameState sebelum save,
        // agar GameSave bisa menulis ke disk tanpa referensi langsung ke NoiseTracker.
        // BUG FIX #4 — Gunakan injected reference, fallback ke Instance.
        (_noiseTracker ?? NoiseTracker.Instance)?.PushNoiseToSave();
        GameSave.Save();

        if (activatedVisual != null) activatedVisual.SetActive(true);
        onCheckpointReached.Invoke();

        Debug.Log($"[Checkpoint] '{gameObject.name}' tercapai — game disimpan.");
    }
}