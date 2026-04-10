using UnityEngine;
using UnityEngine.Events;

public class DiskBox : MonoBehaviour, IInteractable
{
    [Header("Settings")]
    [Tooltip("Disk spesifik yang dibutuhkan. Kosongkan = terima semua disk.")]
    [SerializeField] private DiskItem requiredDisk;

    [Header("Visuals")]
    [Tooltip("Aktif saat disk terpasang")]
    [SerializeField] private GameObject diskInsertedVisual;

    [Header("Prompts")]
    [SerializeField] private string promptNoDisk   = "[Butuh disk]";
    [SerializeField] private string promptInsert   = "Tahan [E] untuk masukkan disk";
    [SerializeField] private string promptInserted = "Disk sudah terpasang";
    [SerializeField] private string promptEject    = "Tahan [E] untuk ambil disk";

    [Header("Events")]
    public UnityEvent               onDiskInserted;
    public UnityEvent               onDiskEjected;
    public UnityEvent<DiskItem>     onDiskInsertedItem;

    private bool     _diskInserted;
    private DiskItem _insertedDisk;
    private bool     _isEjectable;

    // BUG FIX — Flag untuk blokir restore di Start()
    // Di-set oleh DiskRepairHandler.Awake() sebelum Start() dipanggil
    // agar DiskBox tidak restore disk corrupt saat win flag sudah aktif.
    private bool _blockRestore = false;

    public bool     DiskInserted => _diskInserted;
    public DiskItem InsertedDisk => _insertedDisk;
    public bool     IsEjectable  => _isEjectable;

    private string SaveKey     => $"DiskBox_Inserted_{gameObject.name}";
    private string SaveKeyItem => $"DiskBox_Item_{gameObject.name}";

    /// Dipanggil oleh DiskRepairHandler.Awake() untuk mencegah Start() restore disk lama.
    public void BlockRestore() => _blockRestore = true;

    private void Start()
    {
        // BUG FIX — Skip restore jika DiskRepairHandler sudah mengambil alih
        if (_blockRestore)
        {
            if (diskInsertedVisual != null)
                diskInsertedVisual.SetActive(false);
            return;
        }

        if (WorldFlags.Get(SaveKey))
        {
            string savedDiskName = WorldFlags.GetString(SaveKeyItem);
            DiskItem diskToRestore = null;

            if (!string.IsNullOrEmpty(savedDiskName))
                diskToRestore = DiskRegistry.Find(savedDiskName);

            if (diskToRestore == null)
                diskToRestore = requiredDisk;

            if (diskToRestore != null)
            {
                _diskInserted = true;
                _insertedDisk = diskToRestore;
                if (diskInsertedVisual != null)
                    diskInsertedVisual.SetActive(true);
            }
        }
        else if (diskInsertedVisual != null)
        {
            diskInsertedVisual.SetActive(false);
        }
    }

    public string PromptText
    {
        get
        {
            if (_diskInserted && _isEjectable) return promptEject;
            if (_diskInserted) return promptInserted;
            var inv = PlayerDiskInventory.Instance;
            bool hasDisk = inv != null && (requiredDisk == null
                           ? inv.HasAnyDisk
                           : inv.HasDisk(requiredDisk));
            return hasDisk ? promptInsert : promptNoDisk;
        }
    }

    public bool CanInteract
    {
        get
        {
            if (_diskInserted && _isEjectable) return true;
            if (_diskInserted) return false;
            var inv = PlayerDiskInventory.Instance;
            if (inv == null) return false;
            return requiredDisk == null ? inv.HasAnyDisk : inv.HasDisk(requiredDisk);
        }
    }

    public void OnInteract(GameObject interactor)
    {
        if (!CanInteract) return;

        if (_diskInserted && _isEjectable)
        {
            var inv = PlayerDiskInventory.Instance;
            if (inv != null)
            {
                var disk = _insertedDisk;
                EjectDisk();
                inv.AddDisk(disk);
                _isEjectable = false;
                Debug.Log($"[DiskBox] Disk diambil player: {disk?.itemName}");
            }
            return;
        }

        var inventory = PlayerDiskInventory.Instance;
        if (inventory == null) return;

        DiskItem insertDisk = requiredDisk != null && inventory.HasDisk(requiredDisk)
                        ? requiredDisk
                        : inventory.GetLatest();

        if (insertDisk == null) return;

        inventory.RemoveDisk(insertDisk);
        inventory.PersistDisks();

        _diskInserted = true;
        _insertedDisk = insertDisk;

        if (diskInsertedVisual != null)
            diskInsertedVisual.SetActive(true);

        SaveInsertedDisk(insertDisk);

        onDiskInserted.Invoke();
        onDiskInsertedItem.Invoke(insertDisk);
        Debug.Log($"[DiskBox] Disk dimasukkan: {insertDisk.itemName}");
    }

    public void ForceInsertDisk(DiskItem disk)
    {
        if (disk == null || _diskInserted) return;

        _diskInserted = true;
        _insertedDisk = disk;

        if (diskInsertedVisual != null)
            diskInsertedVisual.SetActive(true);

        SaveInsertedDisk(disk);

        onDiskInserted.Invoke();
        onDiskInsertedItem.Invoke(disk);
        Debug.Log($"[DiskBox] Disk force-inserted: {disk.itemName}");
    }

    public void SetEjectable(bool ejectable)
    {
        _isEjectable = ejectable;
        Debug.Log($"[DiskBox] Ejectable: {ejectable}");
    }

    public void EjectDisk()
    {
        if (!_diskInserted) return;
        _diskInserted = false;
        _insertedDisk = null;

        if (diskInsertedVisual != null)
            diskInsertedVisual.SetActive(false);

        WorldFlags.Remove(SaveKey);
        WorldFlags.RemoveString(SaveKeyItem);
        onDiskEjected.Invoke();
    }

    private void SaveInsertedDisk(DiskItem disk)
    {
        WorldFlags.SetString(SaveKeyItem, disk != null ? disk.itemName : "");
        WorldFlags.Set(SaveKey, true);
    }
}