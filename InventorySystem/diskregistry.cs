using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DiskRegistry — lookup global DiskItem berdasarkan nama.
///
/// Cara pakai:
///   1. Pasang DiskRegistryInitializer pada Player atau persistent GameObject
///   2. Assign semua DiskItem asset ke field allDiskAssets di Inspector
///   3. DiskBox.Start() / ItemDropper bisa panggil DiskRegistry.Find("NamaDisk")
///      untuk restore disk yang benar saat load ulang.
/// </summary>
public static class DiskRegistry
{
    private static readonly Dictionary<string, DiskItem> _map = new();

    /// True jika sudah ada data yang ter-register (minimal 1 disk).
    public static bool IsReady => _map.Count > 0;

    public static void Register(IEnumerable<DiskItem> disks)
    {
        _map.Clear();
        foreach (var d in disks)
            if (d != null) _map[d.itemName] = d;
        Debug.Log($"[DiskRegistry] Registered {_map.Count} disk(s).");
    }

    public static DiskItem Find(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return null;
        _map.TryGetValue(itemName, out var disk);
        return disk;
    }
}

/// <summary>
/// Pasang MonoBehaviour ini pada GameObject yang PERSISTENT lintas scene
/// (misalnya: Player, GameManager, atau objek ber-DontDestroyOnLoad).
///
/// BUG FIX: DontDestroyOnLoad + singleton guard agar registry tidak kosong
/// saat scene di-reload. Static dictionary di-reset saat domain reload,
/// tapi MonoBehaviour ini tetap hidup dan re-register di setiap Awake.
/// </summary>
public class DiskRegistryInitializer : MonoBehaviour
{
    // Singleton — cegah duplikasi saat scene di-reload
    private static DiskRegistryInitializer _instance;

    [Tooltip("Daftarkan SEMUA DiskItem asset di sini (corrupt, normal, dll).")]
    [SerializeField] private List<DiskItem> allDiskAssets = new();

    private void Awake()
    {
        // Singleton guard
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // Persistent lintas scene agar tidak perlu dipasang ulang di tiap scene
        DontDestroyOnLoad(gameObject);

        // Register sekarang — Awake() dijamin jalan sebelum Start() script lain
        DiskRegistry.Register(allDiskAssets);
    }
}