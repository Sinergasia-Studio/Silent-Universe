using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ItemInventoryUI — tampil di kiri atas, menampilkan semua item yang dimiliki player.
/// Mendukung Key dan Kampak (dan item lain di masa depan).
///
/// Setup UI (Canvas):
///   Canvas
///     └── ItemInventoryUI         [script ini]
///           └── ItemListPanel     [VerticalLayoutGroup + CanvasGroup]
///                 └── (runtime)   ← slot di-spawn otomatis
///
/// SlotPrefab structure:
///   SlotRoot    [HorizontalLayoutGroup]
///     ├── Icon  [Image]
///     └── Label [TMP_Text]
///
/// Cara pakai:
///   - KeyPickup   → onKeyPickedUp   → ItemInventoryUI.ShowItem(string)
///   - AxePickup   → onPickedUp      → ItemInventoryUI.ShowAxe
///   - DoorInteractable → onDoorUnlocked → ItemInventoryUI.RemoveItem(string)
/// </summary>
public class ItemInventoryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject itemListPanel;
    [SerializeField] private GameObject slotPrefab;     // prefab dengan Image + TMP_Text

    [Header("Icons (opsional)")]
    [SerializeField] private Sprite keyIcon;
    [SerializeField] private Sprite axeIcon;
    [SerializeField] private Sprite flashlightOnIcon;
    [SerializeField] private Sprite flashlightOffIcon;
    [SerializeField] private Sprite diskIcon;
    [SerializeField] private Sprite fuseIcon;
    [SerializeField] private Sprite defaultIcon;

    [Header("Animation")]
    [SerializeField] private float fadeInDuration  = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.5f;

    // ── state ──
    private CanvasGroup                             _canvasGroup;
    private Coroutine                               _fadeRoutine;
    private readonly Dictionary<string, GameObject> _slots = new();
    private string _equippedAxeName;
    private string _equippedFlashlightName;

    private void Awake()
    {
        _canvasGroup = itemListPanel != null
            ? itemListPanel.GetComponent<CanvasGroup>() ?? itemListPanel.AddComponent<CanvasGroup>()
            : null;

        HideImmediate();
    }

    private void Start()
    {
        var equip = PlayerEquipment.Instance;
        if (equip != null)
        {
            equip.onAxeEquipped.AddListener(OnAxeEquipped);
            equip.onAxeUnequipped.AddListener(OnAxeUnequipped);
            equip.onFlashlightEquipped.AddListener(OnFlashlightEquipped);
            equip.onFlashlightUnequipped.AddListener(OnFlashlightUnequipped);
        }

        var inv = PlayerInventory.Instance;
        if (inv != null)
        {
            inv.onKeyAdded.AddListener(OnKeyAdded);
            inv.onKeyRemoved.AddListener(OnKeyRemoved);
        }

        var diskInv = PlayerDiskInventory.Instance;
        if (diskInv != null)
        {
            diskInv.onDiskAdded.AddListener(OnDiskAdded);
            diskInv.onDiskRemoved.AddListener(OnDiskRemoved);
        }

        var fuseInv = PlayerFuseInventory.Instance;
        if (fuseInv != null)
        {
            fuseInv.onFuseAdded.AddListener(OnFuseAdded);
            fuseInv.onFuseRemoved.AddListener(OnFuseRemoved);
        }

        var fl = FindFirstObjectByType<FlashlightController>();
        if (fl != null)
        {
            fl.onFlashlightOn.AddListener(OnFlashlightToggled);
            fl.onFlashlightOff.AddListener(OnFlashlightToggled);
        }

        // BUG FIX TIMING — PlayerInventory.Start() mungkin sudah invoke onKeyAdded/onDiskAdded
        // sebelum UI ini subscribe (urutan Start() antar script tidak dijamin).
        // Solusi: sync UI langsung dari state inventory yang sudah ada di memory.
        SyncFromCurrentState();
    }

    /// Populate UI dari state inventory saat ini — dipanggil sekali di Start()
    /// untuk menangkap item yang sudah di-restore sebelum UI subscribe ke events.
    private void SyncFromCurrentState()
    {
        var inv = PlayerInventory.Instance;
        if (inv != null)
            foreach (var key in inv.GetAllKeys())
                if (key != null) OnKeyAdded(key.keyName);

        var diskInv = PlayerDiskInventory.Instance;
        if (diskInv != null)
            foreach (var disk in diskInv.GetAll())
                if (disk != null) OnDiskAdded(disk.itemName);

        var fuseInv = PlayerFuseInventory.Instance;
        if (fuseInv != null)
            for (int i = 0; i < fuseInv.Count; i++)
                OnFuseAdded("Fuse");

        // Sync axe dan flashlight — PlayerEquipment.LoadFromSave() jalan di Start()
        // yang mungkin sudah selesai sebelum InventoryUI.Start() subscribe ke events.
        var equip = PlayerEquipment.Instance;
        if (equip != null)
        {
            if (equip.HasAxe)        OnAxeEquipped(equip.EquippedAxe);
            if (equip.HasFlashlight) OnFlashlightEquipped(equip.EquippedFlashlight);
        }
    }

    private void OnDestroy()
    {
        var equip = PlayerEquipment.Instance;
        if (equip != null)
        {
            equip.onAxeEquipped.RemoveListener(OnAxeEquipped);
            equip.onAxeUnequipped.RemoveListener(OnAxeUnequipped);
            equip.onFlashlightEquipped.RemoveListener(OnFlashlightEquipped);
            equip.onFlashlightUnequipped.RemoveListener(OnFlashlightUnequipped);
        }

        var inv = PlayerInventory.Instance;
        if (inv != null)
        {
            inv.onKeyAdded.RemoveListener(OnKeyAdded);
            inv.onKeyRemoved.RemoveListener(OnKeyRemoved);
        }

        var diskInv = PlayerDiskInventory.Instance;
        if (diskInv != null)
        {
            diskInv.onDiskAdded.RemoveListener(OnDiskAdded);
            diskInv.onDiskRemoved.RemoveListener(OnDiskRemoved);
        }

        var fuseInv = PlayerFuseInventory.Instance;
        if (fuseInv != null)
        {
            fuseInv.onFuseAdded.RemoveListener(OnFuseAdded);
            fuseInv.onFuseRemoved.RemoveListener(OnFuseRemoved);
        }
    }

    // ── Public API ──

    /// Dipanggil manual jika perlu, tapi sudah otomatis via onKeyAdded
    public void ShowItem(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return;
        if (_slots.ContainsKey(itemName)) return;

        AddSlot(itemName, keyIcon);
        FadeIn();
    }

    private void OnKeyAdded(string keyName)
    {
        if (string.IsNullOrEmpty(keyName)) return;
        if (_slots.ContainsKey(keyName)) return;

        AddSlot(keyName, keyIcon);
        FadeIn();
    }

    /// Dipanggil dari DoorInteractable.onDoorUnlocked (key dipakai)
    public void RemoveItem(string itemName)
    {
        if (_slots.TryGetValue(itemName, out var slot))
        {
            Destroy(slot);
            _slots.Remove(itemName);
        }

        if (_slots.Count == 0)
            StartFadeOut();
    }

    /// Alternatif — hapus key berdasarkan KeyItem asset langsung
    public void RemoveKey(KeyItem key)
    {
        if (key == null) return;
        RemoveItem(key.keyName);
    }

    public void HideImmediate()
    {
        if (itemListPanel != null) itemListPanel.SetActive(false);
        if (_canvasGroup  != null) _canvasGroup.alpha = 0f;
    }

    // ── callbacks dari PlayerEquipment ──
    private void OnAxeEquipped(AxeItem axe)
    {
        if (axe == null) return;
        _equippedAxeName = axe.itemName;
        if (_slots.ContainsKey(_equippedAxeName)) return;

        AddSlot(_equippedAxeName, axeIcon);
        FadeIn();
    }

    private void OnAxeUnequipped()
    {
        if (string.IsNullOrEmpty(_equippedAxeName)) return;
        RemoveItem(_equippedAxeName);
        _equippedAxeName = null;
    }

    private void OnFlashlightEquipped(FlashlightItem fl)
    {
        if (fl == null) return;
        _equippedFlashlightName = fl.itemName;
        if (_slots.ContainsKey(_equippedFlashlightName)) return;

        AddSlot(_equippedFlashlightName, flashlightOffIcon);
        FadeIn();
    }

    private void OnFlashlightUnequipped()
    {
        if (string.IsNullOrEmpty(_equippedFlashlightName)) return;
        RemoveItem(_equippedFlashlightName);
        _equippedFlashlightName = null;
    }

    private void OnFlashlightToggled()
    {
        if (string.IsNullOrEmpty(_equippedFlashlightName)) return;
        if (!_slots.TryGetValue(_equippedFlashlightName, out var slot)) return;

        var fl = FindFirstObjectByType<FlashlightController>();
        if (fl == null) return;

        // update ikon sesuai state nyala/mati
        var img = slot.GetComponentInChildren<Image>();
        if (img != null)
        {
            Sprite target = fl.IsOn ? flashlightOnIcon : flashlightOffIcon;
            if (target != null) img.sprite = target;
        }
    }

    private void OnKeyRemoved(string keyName)
    {
        RemoveItem(keyName);
    }

    private void OnDiskAdded(string diskName)
    {
        if (string.IsNullOrEmpty(diskName)) return;
        if (_slots.ContainsKey(diskName)) return;

        // pakai icon dari DiskItem jika ada, fallback ke diskIcon
        Sprite icon = diskIcon;
        var diskInv = PlayerDiskInventory.Instance;
        if (diskInv != null)
        {
            foreach (var d in diskInv.GetAll())
            {
                if (d.itemName == diskName && d.icon != null) { icon = d.icon; break; }
            }
        }

        AddSlot(diskName, icon);
        FadeIn();
    }

    private void OnDiskRemoved(string diskName) => RemoveItem(diskName);

    // ── Fuse ──
    // Fuse bisa stackable (lebih dari 1), jadi pakai key unik per slot: "Fuse_0", "Fuse_1", dst.
    private void OnFuseAdded(string fuseName)
    {
        // Cari slot key yang belum dipakai
        int index = 0;
        while (_slots.ContainsKey($"fuse_{index}")) index++;
        string slotKey = $"fuse_{index}";

        AddSlot(slotKey, fuseIcon, fuseName);
        FadeIn();
    }

    private void OnFuseRemoved(string fuseName)
    {
        // Hapus slot fuse tertinggi yang ada
        int index = 0;
        while (_slots.ContainsKey($"fuse_{index}")) index++;
        index--;
        if (index >= 0) RemoveItem($"fuse_{index}");
    }

    // ── slot management ──
    private void AddSlot(string slotKey, Sprite icon, string displayLabel = null)
    {
        if (slotPrefab == null || itemListPanel == null) return;

        GameObject slot = Instantiate(slotPrefab, itemListPanel.transform);
        _slots[slotKey] = slot;

        // set icon
        var img = slot.GetComponentInChildren<Image>();
        if (img != null)
        {
            img.sprite  = icon != null ? icon : defaultIcon;
            img.enabled = img.sprite != null;
        }

        // set label — pakai displayLabel jika ada, fallback ke slotKey
        var txt = slot.GetComponentInChildren<TMP_Text>();
        if (txt != null) txt.text = displayLabel ?? slotKey;
    }

    // ── fade ──
    private void FadeIn()
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeInRoutine());
    }

    private void StartFadeOut()
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        itemListPanel.SetActive(true);
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            if (_canvasGroup != null)
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOutRoutine()
    {
        float start   = _canvasGroup != null ? _canvasGroup.alpha : 1f;
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            if (_canvasGroup != null)
                _canvasGroup.alpha = Mathf.Lerp(start, 0f, elapsed / fadeOutDuration);
            yield return null;
        }
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        itemListPanel.SetActive(false);
    }
}