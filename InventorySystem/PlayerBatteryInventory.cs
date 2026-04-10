using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// PlayerBatteryInventory — Singleton, pasang pada Player.
/// Menyimpan stack baterai yang dimiliki player.
/// </summary>
public class PlayerBatteryInventory : MonoBehaviour
{
    public static PlayerBatteryInventory Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Maksimum baterai yang bisa dibawa")]
    [SerializeField] private int maxBatteries = 5;

    [Header("Battery Assets (untuk restore save)")]
    [Tooltip("Daftarkan SEMUA BatteryItem asset di sini agar bisa di-restore dari save.")]
    [SerializeField] private List<BatteryItem> allBatteryAssets = new();

    [Header("Events")]
    public UnityEvent<int>    onBatteryCountChanged;  // (count) saat jumlah berubah
    public UnityEvent<string> onBatteryAdded;         // (itemName)
    public UnityEvent         onBatteryUsed;
    public UnityEvent         onInventoryFull;
    public UnityEvent         onInventoryEmpty;

    private readonly List<BatteryItem> _batteries = new();

    public int  Count   => _batteries.Count;
    public bool IsFull  => _batteries.Count >= maxBatteries;
    public bool IsEmpty => _batteries.Count == 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        GameSave.RegisterPersistCallback(PersistBatteries);
    }

    private void OnDestroy()
    {
        GameSave.UnregisterPersistCallback(PersistBatteries);
    }

    private void Start()
    {
        LoadFromSave();
    }

    public void AddBattery(BatteryItem item)
    {
        if (IsFull) { onInventoryFull.Invoke(); return; }
        _batteries.Add(item);
        onBatteryAdded.Invoke(item != null ? item.itemName : "Baterai");
        onBatteryCountChanged.Invoke(_batteries.Count);
        PersistBatteries();
        SaveFile.ForceWrite();
    }

    /// Pakai 1 baterai — kembalikan rechargeAmount, -1 jika kosong
    public float UseBattery()
    {
        if (IsEmpty) return -1f;

        var item = _batteries[_batteries.Count - 1];
        _batteries.RemoveAt(_batteries.Count - 1);
        onBatteryUsed.Invoke();
        onBatteryCountChanged.Invoke(_batteries.Count);

        if (IsEmpty) onInventoryEmpty.Invoke();

        PersistBatteries();
        SaveFile.ForceWrite();

        return item != null ? item.rechargeAmount : 60f;
    }

    public void PersistBatteries()
    {
        var names = new List<string>();
        foreach (var b in _batteries)
            if (b != null) names.Add(b.itemName);
        SaveFile.Data.batteryInventory = string.Join("|", names);
    }

    private void LoadFromSave()
    {
        string raw = SaveFile.Data.batteryInventory ?? "";
        if (string.IsNullOrEmpty(raw)) return;

        foreach (var name in raw.Split('|'))
        {
            if (string.IsNullOrEmpty(name)) continue;
            if (_batteries.Count >= maxBatteries) break;
            var asset = allBatteryAssets.Find(b => b != null && b.itemName == name);
            if (asset == null)
            {
                Debug.LogWarning($"[BatteryInventory] Battery '{name}' ada di save tapi asset tidak ditemukan di allBatteryAssets.");
                continue;
            }
            _batteries.Add(asset);
            onBatteryAdded.Invoke(asset.itemName);
            onBatteryCountChanged.Invoke(_batteries.Count);
            Debug.Log($"[BatteryInventory] Battery di-restore dari save: {asset.itemName}");
        }
    }
}