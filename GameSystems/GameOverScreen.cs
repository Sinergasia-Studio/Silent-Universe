using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GameOverScreen — pasang pada Canvas di scene GameOver.
///
/// Setup di Unity:
///   1. Buat scene baru "GameOver" dan tambahkan ke Build Settings
///   2. Buat Canvas dengan CanvasGroup
///   3. Pasang script ini pada Canvas atau root GameObject
///   4. Assign field di Inspector
///   5. Di GameOverManager, set gameOverSceneName = "GameOver"
///
/// Flow:
///   TriggerGameOver() → LoadScene("GameOver") → fade in → tunggu →
///   tombol "Ke Main Menu" atau auto redirect setelah delay
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text    titleText;
    [SerializeField] private TMP_Text    subtitleText;
    [SerializeField] private Button      mainMenuButton;

    [Header("Settings")]
    [Tooltip("Nama scene Main Menu")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [Tooltip("Durasi fade in layar game over (detik)")]
    [SerializeField] private float  fadeInDuration    = 1.5f;
    [Tooltip("Delay sebelum tombol Main Menu muncul (detik)")]
    [SerializeField] private float  buttonDelay       = 2f;
    [Tooltip("Auto redirect ke Main Menu setelah sekian detik. 0 = tidak auto redirect")]
    [SerializeField] private float  autoRedirectDelay = 0f;

    [Header("Text")]
    [SerializeField] private string titleString    = "GAME OVER";
    [SerializeField] private string subtitleString = "Kamu ketahuan...";

    private void Start()
    {
        // Pastikan cursor visible di layar game over
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        // Setup awal
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (mainMenuButton != null) mainMenuButton.gameObject.SetActive(false);
        if (titleText != null) titleText.text = titleString;
        if (subtitleText != null) subtitleText.text = subtitleString;

        StartCoroutine(GameOverSequence());
    }

    private IEnumerator GameOverSequence()
    {
        // Fade in
        yield return StartCoroutine(FadeIn());

        // Delay sebelum tombol muncul
        yield return new WaitForSeconds(buttonDelay);

        // Tampilkan tombol Main Menu
        if (mainMenuButton != null)
            mainMenuButton.gameObject.SetActive(true);

        // Auto redirect jika diset
        if (autoRedirectDelay > 0f)
        {
            yield return new WaitForSeconds(autoRedirectDelay);
            GoToMainMenu();
        }
    }

    private IEnumerator FadeIn()
    {
        if (canvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    /// Hubungkan ke tombol "Ke Main Menu" di Inspector.
    public void GoToMainMenu()
    {
        // Reset semua state sebelum ke Main Menu
        GameOverManager.Reset();
        SanitySystem.ResetStaticData();

        // BUG FIX — Reset noise ke 0 saat game over.
        // NoiseTracker adalah DontDestroyOnLoad — tanpa reset ini noise akan
        // melanjutkan nilai terakhir saat rhythm scene ke sesi berikutnya.
        NoiseTracker.Instance?.ResetNoise();

        Debug.Log($"[GameOverScreen] Menuju Main Menu: '{mainMenuSceneName}'");
        SceneManager.LoadScene(mainMenuSceneName);
    }
}