using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// PlayerInventory — Singleton, pasang pada Player GameObject.
///
/// BUG FIX #6: Memanggil GameSave.RegisterPlayer(gameObject) di Awake
/// agar GameSave tidak perlu FindWithTag("Player") yang lambat.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [Header("Events")]
    public UnityEvent<string> onKeyAdded;
    public UnityEvent<string> onKeyRemoved;

    [Header("Key Assets (untuk restore save)")]
    [Tooltip("Daftarkan SEMUA KeyItem asset di sini agar bisa di-restore dari save.")]
    [SerializeField] private List<KeyItem> allKeyAssets = new();

    private readonly List<KeyItem> _keys = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        // BUG FIX #6 — Daftarkan player ke GameSave agar tidak pakai FindWithTag
        GameSave.RegisterPlayer(gameObject);

        // BUG FIX ASMDEF — Daftarkan PersistKeys sebagai callback sinkronisasi.
        // GameSave.Save() akan invoke ini sebelum ForceWrite() tanpa perlu referensikan
        // tipe PlayerInventory langsung (menghindari circular dependency Core ↔ InventorySystem).
        GameSave.RegisterPersistCallback(PersistKeys);
    }

    private void OnDestroy()
    {
        GameSave.UnregisterPersistCallback(PersistKeys);
    }

    private void Start()
    {
        LoadFromSave();
    }

    public void AddKey(KeyItem key)
    {
        if (key == null) return;
        if (_keys.Contains(key)) return;
        _keys.Add(key);
        onKeyAdded.Invoke(key.keyName);
        Debug.Log($"[Inventory] Key ditambahkan: {key.keyName}");
        PersistKeys();
    }

    public void RemoveKey(KeyItem key)
    {
        if (key == null) return;
        _keys.Remove(key);
        onKeyRemoved.Invoke(key.keyName);
        PersistKeys();
    }

    public bool HasKey(KeyItem key) => key != null && _keys.Contains(key);
    public KeyItem[] GetAllKeys()   => _keys.ToArray();
    public KeyItem GetLatestKey()   => _keys.Count > 0 ? _keys[_keys.Count - 1] : null;

    // BUG FIX — Dibuat public agar GameSave.Save() bisa memaksa sync sebelum ForceWrite()
    public void PersistKeys()
    {
        var names = new List<string>();
        foreach (var key in _keys)
            if (key != null) names.Add(key.keyName);
        SaveFile.Data.inventoryKeys = string.Join("|", names);
        // BUG FIX A — Ganti ForceWrite() dengan MarkDirty().
        // PersistKeys() dipanggil dari 2 jalur:
        //   1. GameSave.Save() via callback → GameSave sudah ForceWrite() sesudahnya.
        //   2. AddKey/RemoveKey langsung → cukup MarkDirty, SaveFileAutoFlush flush di LateUpdate.
        // ForceWrite() di sini menyebabkan double I/O dan bisa tumpang tindih
        // dengan atomic write dari jalur checkpoint.
        SaveFile.MarkDirty();
    }

    private void LoadFromSave()
    {
        var savedNames = GameSave.GetSavedKeyNames();
        if (savedNames.Count == 0) return;

        foreach (var name in savedNames)
        {
            var asset = allKeyAssets.Find(k => k != null && k.keyName == name);
            if (asset == null)
            {
                Debug.LogWarning($"[Inventory] Key '{name}' ada di save tapi asset tidak ditemukan di allKeyAssets.");
                continue;
            }
            if (!_keys.Contains(asset))
            {
                _keys.Add(asset);
                onKeyAdded.Invoke(asset.keyName);
                Debug.Log($"[Inventory] Key di-restore dari save: {asset.keyName}");
            }
        }
    }
}