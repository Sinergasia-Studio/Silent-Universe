using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns tiles across lanes at a configurable interval.
/// Uses a simple object pool to avoid per-spawn allocations.
/// Mendukung perubahan kecepatan runtime via SetSpawnInterval() dan SetGlobalSpeedMultiplier().
/// </summary>
public class Spawner : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────
    public static Spawner Instance { get; private set; }

    // ── Inspector ────────────────────────────────────────────────
    [Header("Tile Prefab")]
    [SerializeField] private Tile tilePrefab;

    [Header("Lanes & Press Areas (must match in count)")]
    [SerializeField] private RectTransform[] lanes;
    [SerializeField] private RectTransform[] pressingAreas;

    [Header("Timing")]
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float spawnOffsetX  = 50f;

    [Header("Wiring")]
    [SerializeField] private GameManager gameManager;

    [Header("Pool Settings")]
    [SerializeField] private int initialPoolSize = 10;

    // ── Pool ─────────────────────────────────────────────────────
    private Queue<Tile> pool;

    // ── Runtime ──────────────────────────────────────────────────
    private float timer;

    /// <summary>
    /// Multiplier kecepatan global untuk tile yang baru di-spawn.
    /// Diset oleh TileSpeedModifier via SetGlobalSpeedMultiplier().
    /// </summary>
    public float GlobalSpeedMultiplier { get; private set; } = 1f;

    // ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        pool = new Queue<Tile>(initialPoolSize);
        for (int i = 0; i < initialPoolSize; i++)
            pool.Enqueue(CreateTileInstance());
    }

    private void Start()
    {
        if (!Validate()) { enabled = false; return; }
    }

    private void Update()
    {
        if (spawnInterval <= 0f) return;

        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer -= spawnInterval;
            SpawnTile();
        }
    }

    // ── Public API (dipanggil oleh TileSpeedModifier) ────────────

    /// <summary>
    /// Ubah interval spawn secara runtime (misal dipersingkat agar tile lebih rapat).
    /// </summary>
    public void SetSpawnInterval(float interval)
    {
        spawnInterval = Mathf.Max(0.1f, interval);
    }

    /// <summary>
    /// Simpan multiplier global agar tile baru yang di-spawn langsung pakai speed yang benar.
    /// </summary>
    public void SetGlobalSpeedMultiplier(float multiplier)
    {
        GlobalSpeedMultiplier = Mathf.Max(0.01f, multiplier);
    }

    // ── Spawn ────────────────────────────────────────────────────
    private void SpawnTile()
    {
        if (ScoreManager.Instance != null && !ScoreManager.Instance.IsPlaying) return;

        int lane = Random.Range(0, lanes.Length);
        RectTransform selectedLane = lanes[lane];
        if (selectedLane == null) return;

        Tile tile = GetFromPool();
        tile.transform.SetParent(selectedLane, false);
        tile.transform.localRotation = Quaternion.identity;
        tile.transform.localScale    = Vector3.one;

        if (tile.TryGetComponent<RectTransform>(out var tileRt))
        {
            float startX = selectedLane.rect.width * 0.5f + spawnOffsetX;
            tileRt.anchoredPosition = new Vector2(startX, 0f);
        }

        tile.gameObject.SetActive(true);
        tile.Init(gameManager, pressingAreas[lane], lane, GlobalSpeedMultiplier);
    }

    // ── Pool management ──────────────────────────────────────────
    private Tile GetFromPool()
    {
        while (pool.Count > 0)
        {
            var t = pool.Dequeue();
            if (t != null) return t;
        }
        return CreateTileInstance();
    }

    public void ReturnToPool(Tile tile)
    {
        if (tile == null) return;
        tile.gameObject.SetActive(false);
        tile.transform.SetParent(transform, false);
        pool.Enqueue(tile);
    }

    private Tile CreateTileInstance()
    {
        var t = Instantiate(tilePrefab, transform);
        t.gameObject.SetActive(false);
        return t;
    }

    // ── Validation ───────────────────────────────────────────────
    private bool Validate()
    {
        if (tilePrefab == null)                                   { Debug.LogError("Spawner: tilePrefab not assigned.");              return false; }
        if (lanes == null || lanes.Length == 0)                   { Debug.LogError("Spawner: lanes not assigned.");                   return false; }
        if (gameManager == null)                                  { Debug.LogError("Spawner: gameManager not assigned.");             return false; }
        if (pressingAreas == null || pressingAreas.Length != lanes.Length)
                                                                  { Debug.LogError("Spawner: pressingAreas must match lane count.");  return false; }
        return true;
    }
}