using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Collections;

public class RhythmGameReturn : MonoBehaviour
{
    [Header("Return Settings")]
    [SerializeField] private bool returnOnWin  = true;
    [SerializeField] private bool returnOnLose = false;

    [Header("Delay")]
    [SerializeField] private float delayOnWin  = 2f;

    [Header("Fallback")]
    [SerializeField] private string fallbackSceneName = "";

    [Header("Disk Repair")]
    [Tooltip("Harus sama dengan repairSaveKey di DiskRepairHandler")]
    [SerializeField] private string diskRepairWinKey = "";

    [Header("Back Button")]
    [Tooltip("Nama scene CCTV untuk kembali")]
    [SerializeField] private string backSceneName = "";

    [Header("Events")]
    public UnityEvent onReturnTriggered;
    public UnityEvent onNoSaveFound;

    private bool _hasTriggered;

    private void Start()
    {
        var sm = ScoreManager.Instance;
        if (sm == null) { Debug.LogWarning("[RhythmGameReturn] ScoreManager tidak ditemukan!"); return; }
        if (returnOnWin)  sm.OnWin  += OnWin;
        if (returnOnLose) sm.OnLose += OnLose;

        // Disable AudioListener duplikat saat Additive
        var allAL = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        if (allAL.Length > 1)
        {
            foreach (var al in allAL)
            {
                if (al.gameObject.scene == gameObject.scene)
                {
                    al.enabled = false;
                    break;
                }
            }
        }
    }

    private void OnDestroy()
    {
        var sm = ScoreManager.Instance;
        if (sm == null) return;
        sm.OnWin  -= OnWin;
        sm.OnLose -= OnLose;
    }

    private void OnWin()
    {
        if (_hasTriggered) return;
        _hasTriggered = true;

        // Hentikan EnemyAI DULU sebelum apapun — cegah jumpscare bersamaan dengan win
        var allEnemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (var e in allEnemies) e.Disable();

        // DIUBAH: Noise TIDAK di-reset saat win — pemain melanjutkan dengan
        // nilai noise yang sama seperti sebelum masuk rhythm game.
        // ResetNoise() hanya boleh dipanggil saat game over / new game.
        // NoiseTracker.Instance?.ResetNoise(); ← DIHAPUS

        StartCoroutine(ReturnRoutine(delayOnWin, true));
    }

    private void OnLose()
    {
        if (_hasTriggered) return;
        _hasTriggered = true;

        // BUG FIX: Hanya reset checkpoint (posisi), bukan seluruh progres quest/inventory
        GameSave.ResetCheckpoint();

        string cctvScene = !string.IsNullOrEmpty(backSceneName) ? backSceneName : fallbackSceneName;
        ReturnToCCTV(cctvScene, gameOver: true);
    }

    public void GoBack()
    {
        if (_hasTriggered) return;
        _hasTriggered = true;

        Debug.Log("[RhythmGameReturn] Back — progress rhythm direset");
        ReturnToCCTV(backSceneName, gameOver: false);
    }

    private void ReturnToCCTV(string cctvSceneName, bool gameOver)
    {
        string targetScene = !string.IsNullOrEmpty(cctvSceneName) ? cctvSceneName : fallbackSceneName;
        if (string.IsNullOrEmpty(targetScene))
        {
            Debug.LogWarning("[RhythmGameReturn] Tidak ada scene untuk kembali!");
            return;
        }

        Scene cctvScene = SceneManager.GetSceneByName(targetScene);

        if (cctvScene.IsValid() && cctvScene.isLoaded)
        {
            // Set CCTV scene sebagai active DULU sebelum unload
            SceneManager.SetActiveScene(cctvScene);

            if (!gameOver)
            {
                // Cari MonitorInteractable di scene CCTV (bukan scene ritme)
                MonitorInteractable monitor = null;
                var allMonitors = FindObjectsByType<MonitorInteractable>(FindObjectsSortMode.None);
                foreach (var m in allMonitors)
                {
                    if (m.gameObject.scene == cctvScene)
                    {
                        monitor = m;
                        break;
                    }
                }

                if (monitor != null)
                    monitor.SetPaused(false);

                // Pastikan SanitySystem tahu CCTV masih aktif
                SanitySystem.Instance?.SetCCTVActive(true);

                // BUG FIX — Re-aktifkan semua EnemyAI di CCTV scene setelah kembali dari rhythm.
                // OnWin() memanggil Disable() pada semua EnemyAI untuk mencegah jumpscare
                // bersamaan dengan win. Tapi Disable() set enabled=false dan StopAllCoroutines()
                // — tanpa Enable(), StateMachine tidak pernah restart dan jumpscare tidak
                // bisa terpicu meski noise 100, sampai scene di-reload.
                var allEnemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
                foreach (var e in allEnemies)
                {
                    if (e.gameObject.scene == cctvScene)
                        e.Enable();
                }
            }

            // Unload scene ritme
            SceneManager.UnloadSceneAsync(gameObject.scene);

            // Cursor kembali ke mode CCTV
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible   = true;
        }
        else
        {
            // Fallback — scene CCTV tidak ada di memory
            if (gameOver)
                SceneManager.LoadScene(targetScene);
            else if (GameSave.HasSave())
                GameSave.Load();
            else
                SceneManager.LoadScene(targetScene);
        }
    }

    private IEnumerator ReturnRoutine(float delay, bool isWin)
    {
        // BUG FIX #16 — Simpan progress SEBELUM delay agar tidak hilang jika crash.
        if (isWin && !string.IsNullOrEmpty(diskRepairWinKey))
        {
            WorldFlags.Set(diskRepairWinKey, true);
            Debug.Log($"[RhythmGameReturn] Progress disimpan segera: '{diskRepairWinKey}'");
        }

        yield return new WaitForSeconds(delay);

        onReturnTriggered.Invoke();

        string targetScene = !string.IsNullOrEmpty(fallbackSceneName) ? fallbackSceneName : backSceneName;

        // BUG FIX — Saat win, scene CCTV tidak di-reload (hanya unload rhythm).
        // DiskRepairHandler.Start() tidak dipanggil lagi, jadi CheckAndApply()
        // tidak jalan. Solusi: panggil CheckAndApply() pada semua DiskRepairHandler
        // di scene CCTV sebelum unload, agar disk langsung diganti ke normal.
        if (isWin && !string.IsNullOrEmpty(diskRepairWinKey))
        {
            string cctvSceneName = !string.IsNullOrEmpty(targetScene) ? targetScene : backSceneName;
            Scene cctvScene = SceneManager.GetSceneByName(cctvSceneName);
            if (cctvScene.IsValid() && cctvScene.isLoaded)
            {
                var allHandlers = FindObjectsByType<DiskRepairHandler>(FindObjectsSortMode.None);
                foreach (var handler in allHandlers)
                {
                    if (handler.gameObject.scene == cctvScene)
                    {
                        handler.CheckAndApply();
                        Debug.Log($"[RhythmGameReturn] DiskRepairHandler.CheckAndApply() dipanggil di scene CCTV.");
                    }
                }
            }
        }

        ReturnToCCTV(targetScene, gameOver: false);
    }
}