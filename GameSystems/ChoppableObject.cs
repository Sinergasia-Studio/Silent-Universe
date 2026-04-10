using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ChoppableObject — pasang pada object yang bisa ditebang/dihancurkan dengan kampak.
/// Mendukung: butuh beberapa hit, hilang, berubah jadi object baru, spawn item pickup.
///
/// Optimasi v2:
///   - Save key di-cache saat Awake (zero allocation saat runtime)
///   - Save hanya terjadi saat pertama kali hancur (satu kali ForceWrite)
///   - SpawnDropItems() bug fix: kini benar-benar dipanggil saat Deplete()
/// </summary>
public class ChoppableObject : MonoBehaviour, IInteractable
{
    [Header("Interact Settings")]
    [SerializeField] private string promptText       = "Tahan [E] untuk tebang";
    [SerializeField] private string promptNoAxe      = "[Butuh kampak]";
    [SerializeField] private string promptDepleted   = "[Sudah habis]";

    [Header("Chop Settings")]
    [Tooltip("Berapa kali hit sampai hancur")]
    [SerializeField] private int maxHits = 3;

    [Header("Result — pilih salah satu atau keduanya")]
    [Tooltip("Prefab yang muncul menggantikan object ini (misal tunggul pohon). Kosongkan jika tidak perlu.")]
    [SerializeField] private GameObject resultPrefab;

    [Tooltip("Prefab item pickup yang di-spawn setelah hancur (misal kayu, batu). Bisa lebih dari satu.\n" +
             "Hanya di-spawn saat pertama kali hancur — TIDAK diulang saat load dari save.")]
    [SerializeField] private SpawnEntry[] spawnOnDestroy;

    [Header("Destroy Settings")]
    [Tooltip("Hapus object ini setelah hancur? Jika false, hanya disable.")]
    [SerializeField] private bool destroyOnDepleted = true;

    [Header("Save Settings")]
    [Tooltip("ID unik object ini untuk disimpan ke save file. WAJIB diisi jika ingin state tersimpan.\n" +
             "Contoh: 'Tree_Forest_01', 'Crate_Warehouse_03'.\n" +
             "Jangan ada dua object dengan SaveKey yang sama di scene yang sama.\n" +
             "Kosongkan jika tidak perlu di-save (object sementara, dll).")]
    [SerializeField] private string saveKey = "";

    [Header("Events")]
    public UnityEvent<int> onHit;
    public UnityEvent      onDepleted;
    public UnityEvent      onNoAxe;

    // ── Cached save key (di-build sekali saat Awake, zero allocation saat runtime) ──
    private string _fullSaveKey;
    private bool   _hasSaveKey;

    // ── state ──
    private int  _hitsRemaining;
    private bool _isDepleted;

    // ── IInteractable ──
    public bool CanInteract => !_isDepleted;

    public string PromptText
    {
        get
        {
            if (_isDepleted) return promptDepleted;
            bool hasAxe = PlayerEquipment.Instance != null && PlayerEquipment.Instance.HasAxe;
            if (!hasAxe) return promptNoAxe;
            return $"{promptText} ({_hitsRemaining}x)";
        }
    }

    private void Awake()
    {
        _hasSaveKey = !string.IsNullOrEmpty(saveKey);
        if (_hasSaveKey)
            _fullSaveKey = "Chop_" + saveKey;

        _hitsRemaining = maxHits;

        if (_hasSaveKey && WorldFlags.Get(_fullSaveKey))
            RestoreDepletedState();
    }

    public void OnInteract(GameObject interactor)
    {
        if (_isDepleted) return;

        var equip = PlayerEquipment.Instance;
        if (equip == null || !equip.HasAxe)
        {
            onNoAxe.Invoke();
            Debug.Log("[Choppable] Player tidak membawa kampak!");
            return;
        }

        int damage = equip.EquippedAxe != null ? equip.EquippedAxe.damagePerHit : 1;
        _hitsRemaining -= damage;
        _hitsRemaining  = Mathf.Max(0, _hitsRemaining);

        onHit.Invoke(_hitsRemaining);
        Debug.Log($"[Choppable] Hit! Sisa: {_hitsRemaining}/{maxHits}");

        if (_hitsRemaining <= 0)
            Deplete();
    }

    private void Deplete()
    {
        _isDepleted = true;
        onDepleted.Invoke();

        if (_hasSaveKey)
        {
            WorldFlags.Set(_fullSaveKey, true);
            Debug.Log($"[Choppable] State disimpan — key: {_fullSaveKey}");
        }

        SpawnResult();
        SpawnDropItems(); // BUG FIX: sebelumnya method ini tidak dipanggil

        if (destroyOnDepleted)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    private void RestoreDepletedState()
    {
        _isDepleted    = true;
        _hitsRemaining = 0;

        Debug.Log($"[Choppable] Restore '{_fullSaveKey}' sudah dihancurkan sebelumnya.");

        SpawnResult(); // tunggul/reruntuhan muncul kembali
        // SpawnDropItems() sengaja TIDAK dipanggil di sini — item sudah ada di dunia
        // atau sudah diambil player di session sebelumnya.

        if (destroyOnDepleted)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    private void SpawnResult()
    {
        if (resultPrefab != null)
            Instantiate(resultPrefab, transform.position, transform.rotation);
    }

    private void SpawnDropItems()
    {
        if (spawnOnDestroy == null || spawnOnDestroy.Length == 0) return;

        foreach (var entry in spawnOnDestroy)
        {
            if (entry.prefab == null) continue;
            int count = Random.Range(entry.minCount, entry.maxCount + 1);
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = new Vector3(
                    Random.Range(-entry.scatterRadius, entry.scatterRadius),
                    0.1f,
                    Random.Range(-entry.scatterRadius, entry.scatterRadius)
                );
                Instantiate(entry.prefab, transform.position + offset, Quaternion.identity);
            }
        }
    }

    public void Reset()
    {
        _hitsRemaining = maxHits;
        _isDepleted    = false;
        gameObject.SetActive(true);

        if (_hasSaveKey)
            WorldFlags.Remove(_fullSaveKey);
    }
}

[System.Serializable]
public class SpawnEntry
{
    public GameObject prefab;
    [Min(1)] public int   minCount      = 1;
    [Min(1)] public int   maxCount      = 1;
    [Min(0)] public float scatterRadius = 0.5f;
}