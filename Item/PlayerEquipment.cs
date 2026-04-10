using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// PlayerEquipment — Singleton, pasang pada Player.
/// Menyimpan item yang sedang dipegang player.
///
/// Save system mengikuti pola PlayerInventory:
///   - PersistEquipment() → tulis ke SaveData → ForceWrite ke disk
///   - LoadFromSave() di Start() → restore dari SaveData
///   - GameSave.RegisterPersistCallback() → otomatis dipanggil saat checkpoint
///
/// Axe dan Flashlight masing-masing butuh allAxeAssets / allFlashlightAssets
/// di-assign di Inspector agar bisa di-restore by name dari save.
/// </summary>
public class PlayerEquipment : MonoBehaviour
{
    public static PlayerEquipment Instance { get; private set; }

    [Header("Axe Assets (untuk restore save)")]
    [Tooltip("Daftarkan SEMUA AxeItem asset di sini.")]
    [SerializeField] private System.Collections.Generic.List<AxeItem> allAxeAssets = new();

    [Header("Flashlight Assets (untuk restore save)")]
    [Tooltip("Daftarkan SEMUA FlashlightItem asset di sini.")]
    [SerializeField] private System.Collections.Generic.List<FlashlightItem> allFlashlightAssets = new();

    [Header("Events — Axe")]
    public UnityEvent<AxeItem>        onAxeEquipped;
    public UnityEvent                 onAxeUnequipped;

    [Header("Events — Flashlight")]
    public UnityEvent<FlashlightItem> onFlashlightEquipped;
    public UnityEvent                 onFlashlightUnequipped;

    private AxeItem        _equippedAxe;
    private FlashlightItem _equippedFlashlight;

    public bool           HasAxe              => _equippedAxe != null;
    public AxeItem        EquippedAxe         => _equippedAxe;
    public bool           HasFlashlight       => _equippedFlashlight != null;
    public FlashlightItem EquippedFlashlight  => _equippedFlashlight;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        GameSave.RegisterPersistCallback(PersistEquipment);
    }

    private void OnDestroy()
    {
        GameSave.UnregisterPersistCallback(PersistEquipment);
    }

    private void Start()
    {
        LoadFromSave();
    }

    // ── Axe ──────────────────────────────────────────────────────

    public void EquipAxe(AxeItem axe)
    {
        _equippedAxe = axe;
        onAxeEquipped.Invoke(axe);
        PersistEquipment();
        Debug.Log($"[Equipment] Equipped axe: {axe?.itemName}");
    }

    public void UnequipAxe()
    {
        _equippedAxe = null;
        onAxeUnequipped.Invoke();
        PersistEquipment();
    }

    // ── Flashlight ───────────────────────────────────────────────

    public void EquipFlashlight(FlashlightItem flashlight)
    {
        _equippedFlashlight = flashlight;
        onFlashlightEquipped.Invoke(flashlight);
        PersistEquipment();
        Debug.Log($"[Equipment] Equipped flashlight: {flashlight?.itemName}");
    }

    public void UnequipFlashlight()
    {
        _equippedFlashlight = null;
        onFlashlightUnequipped.Invoke();
        PersistEquipment();
    }

    // ── Persist / Load ───────────────────────────────────────────

    public void PersistEquipment()
    {
        var d = SaveFile.Data;
        d.equippedAxe        = _equippedAxe != null;
        d.equippedFlashlight = _equippedFlashlight != null ? _equippedFlashlight.itemName : "";
        SaveFile.ForceWrite();
    }

    private void LoadFromSave()
    {
        var d = SaveFile.Data;

        // Restore axe
        if (d.equippedAxe && _equippedAxe == null)
        {
            // Cari asset pertama yang tersedia (axe tidak punya itemName di save,
            // cukup tahu player equipped axe — ambil asset pertama)
            var axeAsset = allAxeAssets.Count > 0 ? allAxeAssets[0] : null;
            if (axeAsset != null)
            {
                _equippedAxe = axeAsset;
                onAxeEquipped.Invoke(_equippedAxe);
                Debug.Log($"[Equipment] Axe di-restore dari save: {_equippedAxe.itemName}");
            }
            else
            {
                Debug.LogWarning("[Equipment] equippedAxe=true di save tapi allAxeAssets kosong!");
            }
        }

        // Restore flashlight
        if (!string.IsNullOrEmpty(d.equippedFlashlight) && _equippedFlashlight == null)
        {
            var asset = allFlashlightAssets.Find(f => f != null && f.itemName == d.equippedFlashlight);
            if (asset != null)
            {
                _equippedFlashlight = asset;
                onFlashlightEquipped.Invoke(_equippedFlashlight);
                Debug.Log($"[Equipment] Flashlight di-restore dari save: {_equippedFlashlight.itemName}");
            }
            else
            {
                Debug.LogWarning($"[Equipment] Flashlight '{d.equippedFlashlight}' tidak ditemukan di allFlashlightAssets!");
            }
        }
    }
}