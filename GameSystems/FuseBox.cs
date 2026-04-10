using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// FuseBox — pasang pada GameObject fuse box di scene.
/// Player hold E → pasang fuse dari inventory.
/// Player hold E lagi (saat ada fuse) → eject fuse ke scene sebagai drop.
///
/// Save state:
///   - WorldFlags "FuseBox_Installed_{name}" = true/false
///   - WorldFlags "FuseBox_Item_{name}"      = itemName fuse yang terpasang
///
/// Events:
///   onFuseInstalled      — saat fuse berhasil dipasang
///   onFuseInstalledItem  — (FuseItem) saat fuse berhasil dipasang
///   onFuseEjected        — saat fuse dilepas oleh player (eject)
///   onFuseEjectedItem    — (FuseItem) saat fuse dilepas oleh player
///   onFuseRemoved        — saat fuse dihapus secara programatik (RemoveFuse())
///   onStateChanged       — setiap kali state berubah (installed ↔ empty)
/// </summary>
public class FuseBox : MonoBehaviour, IInteractable, IFuseReceiver
{
    [Header("Settings")]
    [Tooltip("Fuse spesifik yang dibutuhkan. Kosongkan = terima semua fuse.")]
    [SerializeField] private FuseItem requiredFuse;
    [Tooltip("Jika true, player bisa hold E untuk eject fuse yang sudah terpasang.")]
    [SerializeField] private GameObject fuseDropPrefab;

    [Header("Visuals")]
    [SerializeField] private GameObject fuseInstalledVisual;

    [Header("Prompts")]
    [SerializeField] private string promptNoFuse    = "[Butuh fuse]";
    [SerializeField] private string promptInstall   = "Tahan [E] untuk pasang fuse";
    [SerializeField] private string promptInstalled = "Fuse sudah terpasang";

    [Header("Events")]
    public UnityEvent            onFuseInstalled;      // fuse dipasang
    public UnityEvent<FuseItem>  onFuseInstalledItem;  // (item) fuse dipasang
    public UnityEvent            onFuseEjected;        // fuse dilepas player
    public UnityEvent<FuseItem>  onFuseEjectedItem;    // (item) fuse dilepas player
    public UnityEvent            onFuseRemoved;        // fuse dihapus programatik
    public UnityEvent            onStateChanged;       // setiap perubahan state

    // ── state ──
    private bool     _fuseInstalled;
    private FuseItem _installedFuse;

    public bool     FuseInstalled => _fuseInstalled;
    public FuseItem InstalledFuse => _installedFuse;

    // Save keys — nama GameObject harus unik di scene
    private string SaveKeyInstalled => $"FuseBox_Installed_{gameObject.name}";
    private string SaveKeyItem      => $"FuseBox_Item_{gameObject.name}";

    private void Start()
    {
        RestoreFromSave();
    }

    private void RestoreFromSave()
    {
        if (!WorldFlags.Get(SaveKeyInstalled))
        {
            // Tidak ada fuse terpasang
            if (fuseInstalledVisual != null) fuseInstalledVisual.SetActive(false);
            return;
        }

        // Ada fuse terpasang — cari item berdasarkan nama tersimpan
        string savedName = WorldFlags.GetString(SaveKeyItem);
        FuseItem fuse = null;

        if (!string.IsNullOrEmpty(savedName))
        {
            var inv = PlayerFuseInventory.Instance;
            if (inv != null)
            {
                // Cari di semua asset yang terdaftar via allFuseAssets di PlayerFuseInventory
                // Fallback: pakai requiredFuse jika nama cocok
                if (requiredFuse != null && requiredFuse.itemName == savedName)
                    fuse = requiredFuse;
            }
            // Jika tidak ketemu, tetap restore state visual saja
        }

        _fuseInstalled = true;
        _installedFuse = fuse; // bisa null jika asset tidak ketemu, tapi state tetap installed
        if (fuseInstalledVisual != null) fuseInstalledVisual.SetActive(true);
        Debug.Log($"[FuseBox] '{gameObject.name}' di-restore — fuse terpasang: {savedName}");
    }

    public string PromptText
    {
        get
        {
            if (_fuseInstalled) return promptInstalled;

            var inv = PlayerFuseInventory.Instance;
            bool hasFuse = inv != null && (requiredFuse == null
                           ? inv.HasAnyFuse
                           : inv.HasFuse(requiredFuse));
            return hasFuse ? promptInstall : promptNoFuse;
        }
    }

    public bool CanInteract
    {
        get
        {
            // Fuse sudah terpasang — tidak bisa diambil kembali
            if (_fuseInstalled) return false;

            var inv = PlayerFuseInventory.Instance;
            if (inv == null) return false;
            return requiredFuse == null ? inv.HasAnyFuse : inv.HasFuse(requiredFuse);
        }
    }

    public void OnInteract(GameObject interactor)
    {
        if (!CanInteract) return;
        InstallFuse();
    }

    private void InstallFuse()
    {
        var inv = PlayerFuseInventory.Instance;
        if (inv == null) return;

        FuseItem fuse = requiredFuse != null
                        ? (inv.HasFuse(requiredFuse) ? requiredFuse : null)
                        : inv.TakeFirst();

        if (fuse == null) return;

        if (requiredFuse != null) inv.RemoveFuse(requiredFuse);

        _fuseInstalled = true;
        _installedFuse = fuse;

        // Save state
        WorldFlags.Set(SaveKeyInstalled, true);
        WorldFlags.SetString(SaveKeyItem, fuse.itemName);

        if (fuseInstalledVisual != null) fuseInstalledVisual.SetActive(true);

        onFuseInstalled.Invoke();
        onFuseInstalledItem.Invoke(fuse);
        onStateChanged.Invoke();
        Debug.Log($"[FuseBox] '{gameObject.name}' — fuse dipasang: {fuse.itemName}");
    }

    private void EjectFuse(Vector3 playerPos)
    {
        if (!_fuseInstalled) return;

        FuseItem ejected = _installedFuse;

        _fuseInstalled = false;
        _installedFuse = null;

        // Hapus save state
        WorldFlags.Set(SaveKeyInstalled, false);
        WorldFlags.SetString(SaveKeyItem, "");

        if (fuseInstalledVisual != null) fuseInstalledVisual.SetActive(false);

        // Spawn drop prefab di dekat fuse box
        if (fuseDropPrefab != null)
        {
            Vector3 spawnPos = transform.position + transform.forward * 0.4f + Vector3.up * 0.2f;
            var dropped = UnityEngine.Object.Instantiate(fuseDropPrefab, spawnPos, Quaternion.identity);

            var pickup = dropped.GetComponent<FusePickup>();
            if (pickup != null)
            {
                // Set fuse item yang benar ke pickup yang di-spawn
                pickup.SetFuseItem(ejected);
                pickup.ResetPickup();
            }

            var rb = dropped.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 dir = (spawnPos - playerPos).normalized + Vector3.up * 0.5f;
                rb.AddForce(dir.normalized * 2f, ForceMode.Impulse);
            }
        }
        else if (ejected != null)
        {
            // Tidak ada prefab — kembalikan langsung ke inventory
            PlayerFuseInventory.Instance?.AddFuse(ejected);
            Debug.LogWarning($"[FuseBox] fuseDropPrefab belum diassign — fuse dikembalikan ke inventory.");
        }

        onFuseEjected.Invoke();
        if (ejected != null) onFuseEjectedItem.Invoke(ejected);
        onStateChanged.Invoke();
        Debug.Log($"[FuseBox] '{gameObject.name}' — fuse dilepas: {ejected?.itemName}");
    }

    /// Hapus fuse secara programatik (tanpa eject ke scene)
    public void RemoveFuse()
    {
        if (!_fuseInstalled) return;

        _fuseInstalled = false;
        _installedFuse = null;

        WorldFlags.Set(SaveKeyInstalled, false);
        WorldFlags.SetString(SaveKeyItem, "");

        if (fuseInstalledVisual != null) fuseInstalledVisual.SetActive(false);

        onFuseRemoved.Invoke();
        onStateChanged.Invoke();
    }

    /// Force install tanpa cek inventory (untuk restore / scripted event)
    public void ForceInstallFuse(FuseItem fuse)
    {
        if (fuse == null) return;

        _fuseInstalled = true;
        _installedFuse = fuse;

        WorldFlags.Set(SaveKeyInstalled, true);
        WorldFlags.SetString(SaveKeyItem, fuse.itemName);

        if (fuseInstalledVisual != null) fuseInstalledVisual.SetActive(true);

        onFuseInstalled.Invoke();
        onFuseInstalledItem.Invoke(fuse);
        onStateChanged.Invoke();
    }
}