using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// PlayerFuseInventory — Singleton, pasang pada Player.
/// Menyimpan fuse yang dimiliki player.
/// </summary>
public class PlayerFuseInventory : MonoBehaviour
{
    public static PlayerFuseInventory Instance { get; private set; }

    [Header("Events")]
    public UnityEvent<string> onFuseAdded;    // (fuseName)
    public UnityEvent<string> onFuseRemoved;  // (fuseName)

    [Header("Fuse Assets (untuk restore save)")]
    [Tooltip("Daftarkan SEMUA FuseItem asset di sini agar bisa di-restore dari save.")]
    [SerializeField] private List<FuseItem> allFuseAssets = new();

    private readonly List<FuseItem> _fuses = new();

    public bool  HasAnyFuse              => _fuses.Count > 0;
    public bool  HasFuse(FuseItem fuse)  => fuse != null && _fuses.Contains(fuse);
    public int   Count                   => _fuses.Count;
    public FuseItem[] GetAll()           => _fuses.ToArray();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        GameSave.RegisterPersistCallback(PersistFuses);
    }

    private void OnDestroy()
    {
        GameSave.UnregisterPersistCallback(PersistFuses);
    }

    private void Start()
    {
        LoadFromSave();
    }

    public void AddFuse(FuseItem fuse)
    {
        if (fuse == null) return;
        // Guard duplikat — fuse stackable tapi cek dulu jika non-stackable diinginkan
        _fuses.Add(fuse);
        onFuseAdded.Invoke(fuse.itemName);
        Debug.Log($"[FuseInventory] Fuse ditambahkan: {fuse.itemName}");
        PersistFuses();
        SaveFile.ForceWrite();
    }

    public void RemoveFuse(FuseItem fuse)
    {
        if (fuse == null) return;
        _fuses.Remove(fuse);
        onFuseRemoved.Invoke(fuse.itemName);
        PersistFuses();
        SaveFile.ForceWrite();
    }

    /// Ambil fuse pertama yang tersedia
    public FuseItem TakeFirst()
    {
        if (_fuses.Count == 0) return null;
        var fuse = _fuses[_fuses.Count - 1];
        RemoveFuse(fuse);
        return fuse;
    }

    public void PersistFuses()
    {
        var names = new List<string>();
        foreach (var f in _fuses)
            if (f != null) names.Add(f.itemName);
        SaveFile.Data.fuseInventory = string.Join("|", names);
    }

    private void LoadFromSave()
    {
        string raw = SaveFile.Data.fuseInventory ?? "";
        if (string.IsNullOrEmpty(raw)) return;

        foreach (var name in raw.Split('|'))
        {
            if (string.IsNullOrEmpty(name)) continue;
            var asset = allFuseAssets.Find(f => f != null && f.itemName == name);
            if (asset == null)
            {
                Debug.LogWarning($"[FuseInventory] Fuse '{name}' ada di save tapi asset tidak ditemukan di allFuseAssets.");
                continue;
            }
            _fuses.Add(asset);
            onFuseAdded.Invoke(asset.itemName);
            Debug.Log($"[FuseInventory] Fuse di-restore dari save: {asset.itemName}");
        }
    }
}