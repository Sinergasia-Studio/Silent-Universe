using System.Collections;
using UnityEngine;

/// <summary>
/// CCTVAutoRestore — pasang pada GameObject yang sama dengan MonitorInteractable.
///
/// Saat scene di-load, script ini:
///   1. Restore disk ke DiskBox (jika disk sudah terpasang sebelumnya)
///   2. Auto-enter CCTV mode (jika player datang dari portal di dalam CCTV)
///   3. Aktifkan kamera yang terakhir dilihat
///
/// Setup:
///   1. Pasang script ini pada GameObject Monitor (yang punya MonitorInteractable).
///   2. Assign diskBox dan monitor di Inspector.
///   3. Assign allCCTVCameras — list semua CCTVCamera di scene (urutan bebas).
/// </summary>
public class CCTVAutoRestore : MonoBehaviour
{
    [Header("References")]
    [Tooltip("DiskBox yang terhubung ke monitor ini")]
    [SerializeField] private DiskBox diskBox;

    [Tooltip("MonitorInteractable di scene ini")]
    [SerializeField] private MonitorInteractable monitor;

    [Tooltip("DiskItem asset yang dipakai (untuk restore ke DiskBox)")]
    [SerializeField] private DiskItem diskItem;

    [Header("CCTV Cameras")]
    [Tooltip("Semua CCTVCamera di scene — dipakai untuk cari kamera berdasarkan nama")]
    [SerializeField] private CCTVCamera[] allCCTVCameras;

    private void Start()
    {
        if (CCTVSaveData.Instance == null) return;
        if (!CCTVSaveData.Instance.ShouldRestoreCCTV()) return;

        // Delay 1 frame agar semua script lain (InteractionUI, dll) selesai Start()
        StartCoroutine(RestoreNextFrame());
    }

    private IEnumerator RestoreNextFrame()
    {
        yield return null; // tunggu 1 frame

        // 1. Restore disk ke DiskBox
        RestoreDisk();

        // 2. Auto-enter CCTV (bypass CanInteract check)
        if (monitor != null)
        {
            monitor.ForceEnterCCTV();
            Debug.Log("[CCTVRestore] Auto-enter CCTV.");
        }

        // 3. Switch ke kamera terakhir
        string savedCam = CCTVSaveData.Instance.GetSavedCameraName();
        if (!string.IsNullOrEmpty(savedCam))
            SwitchToCamera(savedCam);

        // Clear save state CCTV setelah restore selesai
        // supaya tidak restore lagi jika scene di-load ulang tanpa lewat portal
        CCTVSaveData.Instance.ClearCCTV();
    }

    private void RestoreDisk()
    {
        if (diskBox == null || diskItem == null) return;

        // Guard 1: sudah ada disk di dalam box — skip
        if (diskBox.DiskInserted) return;

        // BUG FIX #5 — Guard 2: disk yang sama sudah ada di inventory player.
        // Skenario duplikasi:
        //   1. Player ambil Disk → ada di inventory
        //   2. Player Save
        //   3. Player masuk CCTV → CCTVAutoRestore terpicu
        //   4. Tanpa guard ini, disk di-restore ke DiskBox padahal player masih pegang
        //   → player bisa punya 2 disk (1 di box, 1 di inventory)
        // PlayerDiskInventory.HasDisk() cek by ScriptableObject reference,
        // aman karena diskItem di Inspector adalah asset yang sama persis.
        if (PlayerDiskInventory.Instance != null && PlayerDiskInventory.Instance.HasDisk(diskItem))
        {
            Debug.Log($"[CCTVRestore] Skip restore — disk '{diskItem.itemName}' sudah ada di inventory player.");
            return;
        }

        // Force insert disk langsung ke DiskBox tanpa lewat inventory
        diskBox.ForceInsertDisk(diskItem);
        Debug.Log($"[CCTVRestore] Disk '{diskItem.itemName}' di-restore ke DiskBox.");
    }

    private void SwitchToCamera(string cameraName)
    {
        if (allCCTVCameras == null || allCCTVCameras.Length == 0) return;

        foreach (var cam in allCCTVCameras)
        {
            if (cam == null) continue;
            if (cam.cameraName == cameraName)
            {
                monitor.SwitchToCameraByRef(cam);
                Debug.Log($"[CCTVRestore] Kamera di-restore ke: {cameraName}");
                return;
            }
        }

        Debug.LogWarning($"[CCTVRestore] Kamera '{cameraName}' tidak ditemukan di scene.");
    }
}
