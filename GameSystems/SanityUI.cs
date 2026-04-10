using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SanityUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Slider      sanityBar;
    [SerializeField] private Image       barFill;
    [SerializeField] private TMP_Text    sanityLabel;
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("Warna")]
    [SerializeField] private Color colorHigh = new Color(0.2f, 0.9f, 0.3f);
    [SerializeField] private Color colorMid  = new Color(1f, 0.8f, 0f);
    [SerializeField] private Color colorLow  = new Color(1f, 0.3f, 0.1f);

    [Header("Settings")]
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private float fadeSpeed   = 3f;
    [SerializeField] private string rhythmSceneName = "Restorasi";

    private float _displayValue = 1f;

    private void Start()
    {
        if (sanityBar != null)
        {
            sanityBar.minValue = 0f;
            sanityBar.maxValue = 1f;
            sanityBar.value    = 1f;
        }
        if (panelCanvasGroup != null)
            panelCanvasGroup.alpha = 0f;
    }

    private void Update()
    {
        var sanity = SanitySystem.Instance;
        if (sanity == null) return;

        float target  = sanity.SanityPercent;
        _displayValue = Mathf.Lerp(_displayValue, target, smoothSpeed * Time.deltaTime);

        if (sanityBar != null)
            sanityBar.value = _displayValue;

        if (barFill != null)
        {
            Color targetColor = _displayValue > 0.5f
                ? Color.Lerp(colorMid, colorHigh, (_displayValue - 0.5f) / 0.5f)
                : Color.Lerp(colorLow, colorMid, _displayValue / 0.5f);
            barFill.color = Color.Lerp(barFill.color, targetColor, smoothSpeed * Time.deltaTime);
        }

        if (sanityLabel != null)
            sanityLabel.text = "SANITY: " + Mathf.RoundToInt(_displayValue * 100f) + "%";

        if (panelCanvasGroup != null)
        {
            bool isRhythmScene = UnityEngine.SceneManagement.SceneManager
                .GetActiveScene().name == rhythmSceneName;
            bool show         = sanity.IsCCTVActive || isRhythmScene;
            float targetAlpha = show ? 1f : 0f;

            float newAlpha = Mathf.Lerp(panelCanvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);

            // Snap ke 0 atau 1 kalau sudah sangat dekat — cegah nilai epsilon aneh
            if (newAlpha < 0.001f) newAlpha = 0f;
            if (newAlpha > 0.999f) newAlpha = 1f;

            panelCanvasGroup.alpha = newAlpha;
        }
    }
}