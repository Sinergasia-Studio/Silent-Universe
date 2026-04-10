using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// DiskRepairHandler — pasang pada DiskBox GameObject di scene CCTV.
///
/// Flow:
///   1. Player masukkan disk rusak ke DiskBox
///   2. Player masuk CCTV → portal ke rhythm game
///   3. Player win → RhythmGameReturn simpan win flag ke JSON save
///   4. Scene CCTV di-load → DiskRepairHandler.Start() baca win flag
///   5. Disk di DiskBox otomatis diganti ke disk normal, bisa diambil player
///
/// BUG FIX:
///   - DiskBox.Start() tidak lagi restore disk saat win flag aktif — DiskRepairHandler
///     yang mengambil alih sepenuhnya lewat BlockDiskBoxRestore().
///   - Setelah player ambil disk normal dari box, DiskBox_Inserted flag dihapus
///     agar box tidak muncul lagi saat load ulang.
///   - diskInventory di save di-update ke nama disk normal setelah repair.
/// </summary>
public class DiskRepairHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DiskBox  diskBox;

    [Header("Disk")]
    [Tooltip("DiskItem asset disk yang sudah diperbaiki (disk normal)")]
    [SerializeField] private DiskItem repairedDisk;

    [Header("Save Key")]
    [Tooltip("Harus SAMA dengan diskRepairWinKey di RhythmGameReturn")]
    [SerializeField] private string repairSaveKey = "DiskWin_1";

    private bool _repairApplied = false;

    [Header("Events")]
    public UnityEvent onDiskRepaired;

    private void Awake()
    {
        if (diskBox == null)
            diskBox = GetComponent<DiskBox>();

        // BUG FIX — Jika win flag sudah aktif, blokir DiskBox.Start() agar tidak
        // restore disk corrupt dari save. DiskRepairHandler yang akan handle di Start().
        if (WorldFlags.Get(repairSaveKey) && diskBox != null)
            diskBox.BlockRestore();
    }

    private IEnumerator Start()
    {
        yield return null; // tunggu 1 frame
        CheckAndApply();
    }

    private void Update()
    {
        if (!_repairApplied && WorldFlags.Get(repairSaveKey))
        {
            Debug.Log("[DiskRepair] Kemenangan terdeteksi secara real-time!");
            CheckAndApply();
        }
    }

    private void ApplyRepaired(bool invokeEvents)
    {
        if (diskBox == null) return;

        // Eject disk lama (rusak) — ini juga hapus WorldFlags DiskBox_Inserted
        if (diskBox.DiskInserted)
            diskBox.EjectDisk();

        // Insert disk normal — ini set WorldFlags DiskBox_Inserted + DiskBox_Item
        if (repairedDisk != null)
            diskBox.ForceInsertDisk(repairedDisk);

        // Disk bisa diambil player
        diskBox.SetEjectable(true);

        // Listener: saat disk diambil dari box ke inventory
        diskBox.onDiskEjected.AddListener(OnDiskTakenByPlayer);

        // BUG FIX DROP — Listener: saat disk dikeluarkan dari inventory (termasuk drop).
        // onDiskEjected hanya trigger saat eject dari box, tidak saat drop.
        // Dengan ini repairSaveKey juga terhapus jika player drop disk setelah mengambilnya.
        var diskInvRef = PlayerDiskInventory.Instance;
        if (diskInvRef != null)
            diskInvRef.onDiskRemoved.AddListener(OnRepairedDiskRemovedFromInventory);

        if (invokeEvents)
            onDiskRepaired.Invoke();

        Debug.Log($"[DiskRepair] Disk diganti ke: {repairedDisk?.itemName}");
    }

    // Dipanggil saat disk normal diambil dari DiskBox ke inventory
    private void OnDiskTakenByPlayer()
    {
        diskBox.onDiskEjected.RemoveListener(OnDiskTakenByPlayer);

        // BUG FIX — Hapus repairSaveKey agar load ulang tidak jalankan
        // ApplyRepaired() lagi dan mengisi ulang DiskBox.
        // onDiskRemoved listener tetap aktif untuk handle kasus drop.
        WorldFlags.Remove(repairSaveKey);

        var diskInv = PlayerDiskInventory.Instance;
        if (diskInv != null)
        {
            diskInv.PersistDisks();
            SaveFile.ForceWrite();
        }
        Debug.Log("[DiskRepair] Disk normal diambil — repairSaveKey dihapus.");
    }

    // Dipanggil saat disk normal dikeluarkan dari inventory (termasuk saat di-drop).
    // Ini memastikan repairSaveKey dihapus sehingga DiskBox tidak restore disk lagi.
    private void OnRepairedDiskRemovedFromInventory(string diskName)
    {
        if (repairedDisk == null || diskName != repairedDisk.itemName) return;

        // Unregister agar tidak trigger berkali-kali
        var diskInv = PlayerDiskInventory.Instance;
        if (diskInv != null)
            diskInv.onDiskRemoved.RemoveListener(OnRepairedDiskRemovedFromInventory);
        diskBox.onDiskEjected.RemoveListener(OnDiskTakenByPlayer);

        // BUG FIX KRITIS — Hapus repairSaveKey agar load ulang tidak
        // menjalankan ApplyRepaired() dan mengisi ulang DiskBox.
        WorldFlags.Remove(repairSaveKey);
        Debug.Log($"[DiskRepair] repairSaveKey '{repairSaveKey}' dihapus — disk '{diskName}' keluar dari inventory.");
    }

    [ContextMenu("DEV: Simulate Win")]
    public void DEV_SimulateWin()
    {
        WorldFlags.Set(repairSaveKey, true);
        ApplyRepaired(true);
    }

    public void ResetRepair()
    {
        WorldFlags.Remove(repairSaveKey);
    }

    // Dibuat public agar RhythmGameReturn bisa panggil langsung saat win
    public void CheckAndApply()
    {
        if (_repairApplied) return;

        if (WorldFlags.Get(repairSaveKey))
        {
            ApplyRepaired(true);
            _repairApplied = true;
        }
    }
}