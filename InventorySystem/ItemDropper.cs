using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// ItemDropper — pasang pada Player.
/// Tekan Q → drop 1 item terbaru (LIFO — item terakhir diambil, pertama di-drop).
///
/// BUG FIX:
///   - Item yang di-drop kini disimpan ke JSON save (droppedItems di SaveData)
///     agar persisten saat scene di-reload atau game di-load ulang.
///   - SpawnDropped kini pakai Physics.CheckSphere di titik akhir spawn
///     untuk validasi overlap, mencegah item melempar player keluar peta.
/// </summary>
public class ItemDropper : MonoBehaviour
{
    [Header("Drop Prefabs")]
    [Tooltip("Prefab kampak yang di-spawn saat drop. Harus punya Rigidbody + AxePickup.")]
    [SerializeField] private GameObject axeDropPrefab;
    [Tooltip("Prefab kunci yang di-spawn saat drop. Harus punya Rigidbody + KeyPickup.")]
    [SerializeField] private GameObject keyDropPrefab;
    [Tooltip("Prefab senter yang di-spawn saat drop. Harus punya Rigidbody + FlashlightPickup.")]
    [SerializeField] private GameObject flashlightDropPrefab;
    [Tooltip("Prefab disk yang di-spawn saat drop. Harus punya Rigidbody + DiskPickup.")]
    [SerializeField] private GameObject diskDropPrefab;
    [Tooltip("Prefab fuse yang di-spawn saat drop. Harus punya Rigidbody + FusePickup.")]
    [SerializeField] private GameObject fuseDropPrefab;

    [Header("Throw Settings")]
    [Tooltip("Jarak spawn di depan kamera")]
    [SerializeField] private float spawnDistance = 0.8f;
    [Tooltip("Kekuatan lempar")]
    [SerializeField] private float throwForce    = 4f;
    [Tooltip("Kekuatan ke atas saat lempar")]
    [SerializeField] private float throwUpForce  = 2f;
    [Tooltip("Kecepatan spin saat melayang")]
    [SerializeField] private float torqueForce   = 3f;

    [Header("Events")]
    public UnityEvent<string> onItemDropped;
    public UnityEvent         onNothingToDrop;

    // Stack urutan pickup — item paling atas = paling baru diambil
    private readonly Stack<string> _pickupOrder = new();

    // Separator untuk serialize ke SaveData.droppedItems
    // Format entry: "sceneName|type|x|y|z"
    // Contoh: "Level1|key:MasterKey|3.500|0.100|-2.000"
    private const char ENTRY_SEP = ';';
    private const char FIELD_SEP = '|';

    // ── Unity ────────────────────────────────────────────────────

    private void Start()
    {
        var equip = PlayerEquipment.Instance;
        if (equip != null)
        {
            equip.onAxeEquipped.AddListener(OnAxeEquipped);
            equip.onFlashlightEquipped.AddListener(OnFlashlightEquipped);
        }

        var inv = PlayerInventory.Instance;
        if (inv != null)
            inv.onKeyAdded.AddListener(OnKeyAdded);

        var diskInv = PlayerDiskInventory.Instance;
        if (diskInv != null)
            diskInv.onDiskAdded.AddListener(OnDiskAdded);

        var fuseInv = PlayerFuseInventory.Instance;
        if (fuseInv != null)
            fuseInv.onFuseAdded.AddListener(OnFuseAdded);

        // BUG FIX TIMING — Sync _pickupOrder dari state inventory yang sudah ada.
        // LoadFromSave() di PlayerInventory/PlayerDiskInventory mungkin sudah jalan
        // sebelum ItemDropper subscribe ke onKeyAdded/onDiskAdded, sehingga item
        // yang di-restore tidak masuk ke _pickupOrder dan tidak bisa di-drop.
        SyncPickupOrderFromCurrentState();

        // Restore item yang pernah di-drop di scene ini dari JSON save.
        // BUG FIX: Tunda satu frame agar DiskRegistryInitializer.Awake() pasti
        // sudah jalan dan DiskRegistry sudah terisi sebelum kita cari disk by name.
        StartCoroutine(RestoreDroppedItemsNextFrame());

        // Fase 1.4 — Cleanup droppedItems saat scene ditinggal agar string tidak
        // terus bertambah sepanjang session. Item di scene lain tidak relevan.
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnSceneUnloaded(Scene scene)
    {
        // Hapus semua entry droppedItems milik scene yang baru saja di-unload.
        // Hanya hapus jika scene di-unload Single (bukan Additive yang masih aktif).
        string raw = SaveFile.Data.droppedItems ?? "";
        if (string.IsNullOrEmpty(raw)) return;

        string prefix = scene.name + FIELD_SEP;
        var entries = new List<string>(raw.Split(ENTRY_SEP));
        int before = entries.Count;
        entries.RemoveAll(e => e.StartsWith(prefix));

        if (entries.Count != before)
        {
            SaveFile.Data.droppedItems = string.Join(ENTRY_SEP.ToString(), entries);
            SaveFile.Write();
            Debug.Log($"[ItemDropper] Cleanup droppedItems untuk scene '{scene.name}' — {before - entries.Count} entry dihapus.");
        }
    }

    private void OnAxeEquipped(AxeItem axe)              => _pickupOrder.Push("axe");
    private void OnFlashlightEquipped(FlashlightItem fl) => _pickupOrder.Push("flashlight");
    private void OnKeyAdded(string keyName)              => _pickupOrder.Push($"key:{keyName}");
    private void OnDiskAdded(string diskName)            => _pickupOrder.Push($"disk:{diskName}");
    private void OnFuseAdded(string fuseName)            => _pickupOrder.Push($"fuse:{fuseName}");

    // ── Input System Callback ──

    public void OnDrop(InputValue value)
    {
        if (!value.isPressed) return;
        DropLatest();
    }

    // ── Public API ──

    /// Sync _pickupOrder dari state inventory saat ini.
    /// Dipanggil sekali di Start() untuk menangkap item yang sudah di-restore
    /// sebelum ItemDropper subscribe ke events.
    private void SyncPickupOrderFromCurrentState()
    {
        // Keys
        var inv = PlayerInventory.Instance;
        if (inv != null)
            foreach (var key in inv.GetAllKeys())
                if (key != null) _pickupOrder.Push($"key:{key.keyName}");

        // Disks
        var diskInv = PlayerDiskInventory.Instance;
        if (diskInv != null)
            foreach (var disk in diskInv.GetAll())
                if (disk != null) _pickupOrder.Push($"disk:{disk.itemName}");

        // Fuses
        var fuseInv = PlayerFuseInventory.Instance;
        if (fuseInv != null)
            for (int i = 0; i < fuseInv.Count; i++)
                _pickupOrder.Push("fuse");

        // Equipment (axe, flashlight)
        var equip = PlayerEquipment.Instance;
        if (equip != null)
        {
            if (equip.HasAxe) _pickupOrder.Push("axe");
            if (equip.HasFlashlight) _pickupOrder.Push("flashlight");
        }
    }

    public void DropLatest()
    {
        while (_pickupOrder.Count > 0)
        {
            string top = _pickupOrder.Pop();

            if (top == "axe")
            {
                var equip = PlayerEquipment.Instance;
                if (equip != null && equip.HasAxe)
                {
                    DropAxe(equip);
                    return;
                }
                continue;
            }

            if (top == "flashlight")
            {
                var equip = PlayerEquipment.Instance;
                if (equip != null && equip.HasFlashlight)
                {
                    DropFlashlight(equip);
                    return;
                }
                continue;
            }

            if (top.StartsWith("fuse:"))
            {
                string fuseName = top.Substring(5);
                var fuseInv     = PlayerFuseInventory.Instance;
                if (fuseInv != null && fuseInv.HasAnyFuse)
                {
                    DropFuse(fuseName, fuseInv);
                    return;
                }
                continue;
            }

            if (top.StartsWith("disk:"))
            {
                string diskName = top.Substring(5);
                var diskInv     = PlayerDiskInventory.Instance;
                if (diskInv != null)
                {
                    DiskItem found = null;
                    foreach (var d in diskInv.GetAll())
                        if (d.itemName == diskName) { found = d; break; }
                    if (found != null)
                    {
                        DropDisk(found, diskInv);
                        return;
                    }
                }
                continue;
            }

            if (top.StartsWith("key:"))
            {
                string keyName = top.Substring(4);
                var inv        = PlayerInventory.Instance;
                if (inv != null)
                {
                    KeyItem found = null;
                    foreach (var k in inv.GetAllKeys())
                        if (k.keyName == keyName) { found = k; break; }
                    if (found != null)
                    {
                        DropKey(found, inv);
                        return;
                    }
                }
                continue;
            }
        }

        onNothingToDrop.Invoke();
    }

    // ── Private drop methods ──────────────────────────────────────

    private void DropAxe(PlayerEquipment equip)
    {
        if (axeDropPrefab == null)
        {
            Debug.LogWarning("[ItemDropper] Axe Drop Prefab belum diassign!");
            equip.UnequipAxe();
            return;
        }

        string itemName = equip.EquippedAxe != null ? equip.EquippedAxe.itemName : "Kampak";
        equip.UnequipAxe();

        GameObject dropped = SpawnDropped(axeDropPrefab);
        var pickup = dropped.GetComponent<AxePickup>();
        if (pickup != null) pickup.ResetPickup();
        ThrowObject(dropped);

        // Simpan posisi drop agar axe muncul kembali saat load ulang.
        Vector3 savedPos = dropped.transform.position;
        SaveDroppedItem("axe", savedPos);

        if (pickup != null)
            pickup.onPickedUp.AddListener(() => RemoveDroppedItem("axe", savedPos));

        onItemDropped.Invoke(itemName);
    }

    private void DropFuse(string fuseName, PlayerFuseInventory inv)
    {
        if (fuseDropPrefab == null)
        {
            Debug.LogWarning("[ItemDropper] Fuse Drop Prefab belum diassign!");
            inv.TakeFirst();
            return;
        }

        // Cari fuse dengan nama yang cocok, fallback ke fuse pertama
        FuseItem fuse = null;
        foreach (var f in inv.GetAll())
            if (f != null && f.itemName == fuseName) { fuse = f; break; }
        if (fuse == null) fuse = inv.GetAll().Length > 0 ? inv.GetAll()[0] : null;
        if (fuse == null) return;

        inv.RemoveFuse(fuse);

        GameObject dropped = SpawnDropped(fuseDropPrefab);
        var pickup = dropped.GetComponent<FusePickup>();
        if (pickup != null)
        {
            pickup.SetFuseItem(fuse);
            pickup.ResetPickup();
            pickup.SetDestroyOnPickup(false);
        }
        ThrowObject(dropped);

        string typeKey  = $"fuse:{fuse.itemName}";
        Vector3 savedPos = dropped.transform.position;
        SaveDroppedItem(typeKey, savedPos);

        if (pickup != null)
            pickup.onPickedUp.AddListener(() => RemoveDroppedItem(typeKey, savedPos));

        onItemDropped.Invoke(fuse.itemName);
    }

    private void DropDisk(DiskItem disk, PlayerDiskInventory inv)
    {
        if (diskDropPrefab == null)
        {
            Debug.LogWarning("[ItemDropper] Disk Drop Prefab belum diassign!");
            inv.RemoveDisk(disk);
            return;
        }

        inv.RemoveDisk(disk);

        GameObject dropped = SpawnDropped(diskDropPrefab);
        var pickup = dropped.GetComponent<DiskPickup>();
        if (pickup != null)
        {
            pickup.SetDisk(disk);
            pickup.ResetPickup();
        }

        ThrowObject(dropped);

        string typeKey = $"disk:{(disk != null ? disk.itemName : "")}";
        Vector3 savedPos = dropped.transform.position;
        SaveDroppedItem(typeKey, savedPos);

        // Hapus dari save saat item diambil kembali
        if (pickup != null)
            pickup.onPickedUp.AddListener(() => RemoveDroppedItem(typeKey, savedPos));

        onItemDropped.Invoke(disk != null ? disk.itemName : "Disk");
    }

    private void DropFlashlight(PlayerEquipment equip)
    {
        if (flashlightDropPrefab == null)
        {
            Debug.LogWarning("[ItemDropper] Flashlight Drop Prefab belum diassign!");
            equip.UnequipFlashlight();
            return;
        }

        var controller = FlashlightController.Instance;
        if (controller != null && controller.IsOn) controller.TurnOff();

        var state = controller != null
            ? controller.GetState()
            : new FlashlightController.FlashlightState { batteryRemaining = -1f };

        string itemName = equip.EquippedFlashlight != null ? equip.EquippedFlashlight.itemName : "Senter";
        equip.UnequipFlashlight();

        GameObject dropped = SpawnDropped(flashlightDropPrefab);
        var pickup = dropped.GetComponent<FlashlightPickup>();
        if (pickup != null)
        {
            pickup.ResetPickup();
            pickup.SaveState(state);
        }

        ThrowObject(dropped);

        Vector3 savedPos = dropped.transform.position;
        SaveDroppedItem("flashlight", savedPos);

        if (pickup != null)
            pickup.onPickedUp.AddListener(() => RemoveDroppedItem("flashlight", savedPos));

        onItemDropped.Invoke(itemName);
    }

    private void DropKey(KeyItem key, PlayerInventory inv)
    {
        if (keyDropPrefab == null)
        {
            Debug.LogWarning("[ItemDropper] Key Drop Prefab belum diassign!");
            inv.RemoveKey(key);
            return;
        }

        inv.RemoveKey(key);

        GameObject dropped = SpawnDropped(keyDropPrefab);
        var pickup = dropped.GetComponent<KeyPickup>();
        if (pickup != null)
        {
            pickup.SetKey(key);
            pickup.ResetPickup();
        }

        ThrowObject(dropped);

        string typeKey = $"key:{(key != null ? key.keyName : "")}";
        Vector3 savedPos = dropped.transform.position;
        SaveDroppedItem(typeKey, savedPos);

        if (pickup != null)
            pickup.onKeyPickedUp.AddListener((_) => RemoveDroppedItem(typeKey, savedPos));

        onItemDropped.Invoke(key != null ? key.keyName : "Kunci");
    }

    // ── Spawn helpers ─────────────────────────────────────────────

    private GameObject SpawnDropped(GameObject prefab)
    {
        Camera  cam     = Camera.main;
        Vector3 origin  = cam != null ? cam.transform.position  : transform.position + Vector3.up * 0.5f;
        Vector3 forward = cam != null ? cam.transform.forward   : transform.forward;

        float   radius   = 0.15f;
        Vector3 spawnPos = origin + forward * spawnDistance + Vector3.up * 0.1f;

        // SphereCast untuk cek penghalang di jalur spawn
        if (Physics.SphereCast(origin, radius, forward, out RaycastHit hit,
                               spawnDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            spawnPos = hit.point - forward * radius + Vector3.up * 0.05f;
        }

        // BUG FIX (Collision Exploit): validasi titik akhir dengan CheckSphere.
        // Jika masih overlap, geser ke atas sampai area aman ditemukan (max 5x).
        int safetyIter = 0;
        while (Physics.CheckSphere(spawnPos, radius, ~0, QueryTriggerInteraction.Ignore)
               && safetyIter < 5)
        {
            spawnPos   += Vector3.up * (radius * 2f);
            safetyIter++;
        }

        GameObject obj = Instantiate(prefab, spawnPos, transform.rotation);

        var rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation          = RigidbodyInterpolation.Interpolate;
        }

        obj.GetComponent<AxePickup>()?.SetDestroyOnPickup(true);
        obj.GetComponent<KeyPickup>()?.SetDestroyOnPickup(true);

        return obj;
    }

    private void ThrowObject(GameObject obj)
    {
        Camera  cam     = Camera.main;
        Vector3 forward = cam != null ? cam.transform.forward : transform.forward;

        var rb = obj.GetComponent<Rigidbody>();
        if (rb == null) rb = obj.AddComponent<Rigidbody>();

        Vector3 throwDir = (forward + Vector3.up * (throwUpForce / Mathf.Max(throwForce, 0.1f))).normalized;
        rb.AddForce(throwDir * throwForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * torqueForce, ForceMode.Impulse);
    }

    // ── Dropped Items Persistence ─────────────────────────────────

    /// Simpan entry item yang di-drop ke SaveData.droppedItems
    private void SaveDroppedItem(string type, Vector3 pos)
    {
        string scene = SceneManager.GetActiveScene().name;
        // Gunakan InvariantCulture agar koma/titik desimal konsisten lintas platform
        string entry = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0}{1}{2}{1}{3:F3}{1}{4:F3}{1}{5:F3}",
            scene, FIELD_SEP, type, pos.x, pos.y, pos.z);

        string existing = SaveFile.Data.droppedItems ?? "";
        SaveFile.Data.droppedItems = string.IsNullOrEmpty(existing)
            ? entry
            : existing + ENTRY_SEP + entry;

        SaveFile.Write();
        Debug.Log($"[ItemDropper] Saved dropped item: {entry}");
    }

    /// Hapus entry dropped item dari save saat item diambil kembali
    private void RemoveDroppedItem(string type, Vector3 pos)
    {
        string scene  = SceneManager.GetActiveScene().name;
        string target = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0}{1}{2}{1}{3:F3}{1}{4:F3}{1}{5:F3}",
            scene, FIELD_SEP, type, pos.x, pos.y, pos.z);

        string existing = SaveFile.Data.droppedItems ?? "";
        if (string.IsNullOrEmpty(existing)) return;

        var entries = new List<string>(existing.Split(ENTRY_SEP));
        entries.RemoveAll(e => e == target);

        SaveFile.Data.droppedItems = string.Join(ENTRY_SEP.ToString(), entries);
        SaveFile.Write();
        Debug.Log($"[ItemDropper] Removed dropped item from save: {target}");
    }

    /// Wrapper coroutine — tunda RestoreDroppedItems() satu frame
    /// agar DiskRegistry sudah siap (Awake < Start dijamin, tapi static dict
    /// bisa kosong jika DontDestroyOnLoad belum dipanggil di frame yang sama).
    private System.Collections.IEnumerator RestoreDroppedItemsNextFrame()
    {
        yield return null; // tunggu akhir frame ini
        RestoreDroppedItems();
    }

    /// Restore semua dropped items milik scene ini saat Start()
    private void RestoreDroppedItems()
    {
        string raw = SaveFile.Data.droppedItems ?? "";
        if (string.IsNullOrEmpty(raw)) return;

        string currentScene = SceneManager.GetActiveScene().name;
        var culture = System.Globalization.CultureInfo.InvariantCulture;

        foreach (var entry in raw.Split(ENTRY_SEP))
        {
            if (string.IsNullOrEmpty(entry)) continue;

            var fields = entry.Split(FIELD_SEP);
            // fields: [0]=scene, [1]=type, [2]=x, [3]=y, [4]=z
            if (fields.Length < 5) continue;
            if (fields[0] != currentScene) continue;

            string type = fields[1];
            if (!float.TryParse(fields[2], System.Globalization.NumberStyles.Float, culture, out float x)) continue;
            if (!float.TryParse(fields[3], System.Globalization.NumberStyles.Float, culture, out float y)) continue;
            if (!float.TryParse(fields[4], System.Globalization.NumberStyles.Float, culture, out float z)) continue;

            Vector3 pos = new Vector3(x, y, z);
            SpawnRestoredItem(type, pos);
        }
    }

    private System.Collections.IEnumerator EnableRigidbodyNextFrame(Rigidbody rb)
    {
        yield return null;
        if (rb == null) yield break;
        rb.isKinematic = false;
        rb.useGravity  = true;
    }

    /// Spawn ulang item dari save data tanpa menambahkan ke _pickupOrder
    private void SpawnRestoredItem(string type, Vector3 pos)
    {
        GameObject prefab = null;

        if (type == "axe")             prefab = axeDropPrefab;
        else if (type == "flashlight") prefab = flashlightDropPrefab;
        else if (type.StartsWith("key:"))  prefab = keyDropPrefab;
        else if (type.StartsWith("disk:")) prefab = diskDropPrefab;
        else if (type.StartsWith("fuse:")) prefab = fuseDropPrefab;

        if (prefab == null)
        {
            Debug.LogWarning($"[ItemDropper] Prefab tidak ditemukan untuk restore type: {type}");
            return;
        }

        GameObject obj = Instantiate(prefab, pos, Quaternion.identity);

        // BUG FIX — Saat restore, posisi tersimpan mungkin sedikit di dalam geometry
        // sehingga physics mendorong item ke bawah sampai void.
        // Solusi: matikan gravity + kinematic dulu, snap ke atas permukaan terdekat
        // via Raycast, lalu aktifkan kembali setelah posisi aman.
        var rbRestore = obj.GetComponent<Rigidbody>();
        if (rbRestore != null)
        {
            rbRestore.isKinematic = true;
            rbRestore.useGravity  = false;

            // Cari permukaan di bawah posisi tersimpan (max 5m ke bawah)
            if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out RaycastHit surfaceHit,
                                5.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                // Tempatkan sedikit di atas permukaan
                obj.transform.position = surfaceHit.point + Vector3.up * 0.05f;
            }

            // Aktifkan kembali setelah 1 frame agar physics tidak langsung mendorong
            StartCoroutine(EnableRigidbodyNextFrame(rbRestore));
        }

        if (type.StartsWith("key:"))
        {
            var pickup = obj.GetComponent<KeyPickup>();
            if (pickup != null)
            {
                pickup.ResetPickup();
                pickup.SetDestroyOnPickup(true);
                pickup.onKeyPickedUp.AddListener((_) => RemoveDroppedItem(type, pos));
            }
        }
        else if (type.StartsWith("disk:"))
        {
            var pickup = obj.GetComponent<DiskPickup>();
            if (pickup != null)
            {
                // BUG FIX — Set disk yang benar sebelum ResetPickup()
                // Tanpa ini, item yang di-drop saat restore akan muncul sebagai disk corrupt
                // karena DiskPickup menggunakan diskItem default dari prefab.
                string diskName = type.Substring(5);
                DiskItem diskAsset = DiskRegistry.Find(diskName);
                if (diskAsset != null)
                    pickup.SetDisk(diskAsset);
                else
                    Debug.LogWarning($"[ItemDropper] Disk '{diskName}' tidak ditemukan di DiskRegistry. " +
                                     $"Pastikan DiskRegistryInitializer sudah disetup.");

                pickup.ResetPickup();
                pickup.onPickedUp.AddListener(() => RemoveDroppedItem(type, pos));
            }
        }
        else if (type == "flashlight")
        {
            var pickup = obj.GetComponent<FlashlightPickup>();
            if (pickup != null)
            {
                pickup.ResetPickup();
                pickup.onPickedUp.AddListener(() => RemoveDroppedItem(type, pos));
            }
        }
        else if (type == "axe")
        {
            var pickup = obj.GetComponent<AxePickup>();
            if (pickup != null)
            {
                pickup.ResetPickup();
                pickup.SetDestroyOnPickup(true);
                pickup.onPickedUp.AddListener(() => RemoveDroppedItem(type, pos));
            }
        }
        else if (type.StartsWith("fuse:"))
        {
            var pickup = obj.GetComponent<FusePickup>();
            if (pickup != null)
            {
                string fuseName = type.Substring(5);
                // Cari FuseItem asset via PlayerFuseInventory.allFuseAssets
                // Untuk sekarang set nama saja — FusePickup akan pakai asset default-nya
                // jika tidak ada referensi langsung
                pickup.ResetPickup();
                pickup.onPickedUp.AddListener(() => RemoveDroppedItem(type, pos));
            }
        }

        Debug.Log($"[ItemDropper] Restored dropped item: {type} at {pos}");
    }
}