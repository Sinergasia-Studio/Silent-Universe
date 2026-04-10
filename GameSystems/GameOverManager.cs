using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GameOverManager — Singleton guard untuk semua sistem game over.
///
/// BUG FIX D1: Mencegah Triple Game Over tanpa guard.
///   EnemyAI, SanitySystem, dan ScoreManager sebelumnya bisa masing-masing
///   memanggil SceneManager.LoadScene() secara independen di frame yang sama.
///   Sekarang semua sistem wajib memanggil GameOverManager.TriggerGameOver().
///
/// BUG FIX D2: Mencegah LoadScene ke scene yang salah.
///   Saat scene ritme di-load Additive dan di-set sebagai active scene,
///   GetActiveScene().buildIndex mengembalikan index scene ritme — bukan CCTV.
///   GameOverManager menyimpan nama scene utama sebelum scene ritme dimuat.
///
/// BUG FIX #19: Double Death Race Condition.
///   ScoreManager.TriggerLose() dan EnemyAI.JumpscareRoutine() bisa terpicu
///   di frame yang sama. _alreadyTriggered bool mencegah LoadScene ganda.
/// </summary>
public static class GameOverManager
{
    // ── State ──
    private static bool   _alreadyTriggered;
    private static string _mainSceneName     = "";
    private static bool   _mainSceneRegistered;
    private static string _gameOverSceneName = "GameOverScreen";

    /// Set nama scene Game Over — panggil dari GameOverScreen atau Bootstrap.
    /// Default: "GameOver"
    public static void SetGameOverScene(string sceneName)
    {
        _gameOverSceneName = sceneName;
    }

    /// <summary>
    /// True jika scene utama sudah terdaftar untuk sesi ini.
    /// Digunakan EnemyAI.Start() agar hanya instance pertama yang memanggil
    /// RegisterMainScene() — tidak ada double-reset di tengah sesi.
    /// </summary>
    public static bool IsMainSceneRegistered => _mainSceneRegistered;

    /// <summary>
    /// Dipanggil saat scene utama (CCTV/main) pertama kali dimuat.
    /// Simpan nama scene ini agar game over selalu kembali ke scene yang benar,
    /// bukan ke scene ritme yang dimuat Additive.
    /// </summary>
    public static void RegisterMainScene(string sceneName)
    {
        _mainSceneName       = sceneName;
        _mainSceneRegistered = true;
        Debug.Log($"[GameOverManager] Main scene terdaftar: '{_mainSceneName}'");
    }

    /// <summary>
    /// Reset SEMUA state — wajib dipanggil saat memulai sesi baru (New Game)
    /// atau saat kembali ke Main Menu setelah game over.
    /// Memanggil ini dari EnemyAI.Start() TIDAK lagi dilakukan langsung —
    /// hanya MainMenuHandler.NewGame() yang boleh memanggil Reset().
    /// </summary>
    public static void Reset()
    {
        _alreadyTriggered    = false;
        _mainSceneRegistered = false;
        _mainSceneName       = "";
        Debug.Log("[GameOverManager] State direset untuk sesi baru.");
    }

    /// <summary>
    /// Satu-satunya titik masuk untuk semua game over.
    /// Memanggil ini lebih dari sekali (dari sistem berbeda) aman — hanya
    /// eksekusi pertama yang diproses, sisanya diabaikan.
    /// </summary>
    public static void TriggerGameOver()
    {
        if (_alreadyTriggered)
        {
            Debug.LogWarning("[GameOverManager] TriggerGameOver dipanggil lagi — diabaikan (sudah triggered).");
            return;
        }
        _alreadyTriggered = true;

        // Hapus save
        SaveFile.Delete();
        Debug.Log("[GameOverManager] Game over — save.json dihapus.");

        // Reset QuestManager agar state RAM sinkron dengan save yang dihapus (Bug #22)
        QuestManager.Instance?.ResetAllProgress();

        // Reset kursor agar tidak hilang setelah game over (Bug #20)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        // Load scene Game Over untuk tampilkan layar game over sebelum ke Main Menu.
        // GameOverScreen yang handle reset state dan redirect ke Main Menu.
        Debug.Log($"[GameOverManager] Load game over screen: '{_gameOverSceneName}'");
        SceneManager.LoadScene(_gameOverSceneName);
    }

    /// <summary>
    /// Apakah game over sudah di-trigger? Digunakan sistem lain untuk
    /// skip logika yang tidak perlu setelah game over.
    /// </summary>
    public static bool IsGameOver => _alreadyTriggered;
}