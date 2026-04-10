using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the progress bar slider purely via events from ScoreManager.
/// No polling in Update — slider lerps smoothly toward the latest score.
/// </summary>
public class UIHandler : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────
    [Header("Progress Bar")]
    [SerializeField] private Slider progressSlider;
    [SerializeField] private float  lerpSpeed = 5f;

    // ── Runtime ──────────────────────────────────────────────────
    private float targetValue;
    private bool  needsLerp;

    // ─────────────────────────────────────────────────────────────
    private void Start()
    {
        if (progressSlider == null)
        {
            Debug.LogError("UIHandler: progressSlider not assigned.");
            enabled = false;
            return;
        }

        var sm = ScoreManager.Instance;
        if (sm == null)
        {
            Debug.LogError("UIHandler: ScoreManager not found.");
            enabled = false;
            return;
        }

        float max = Mathf.Max(sm.MaxScore, 1f);
        progressSlider.maxValue = max;
        progressSlider.value    = 0f;
        targetValue             = 0f;

        sm.OnScoreChanged += OnScoreChanged;
    }

    private void OnDestroy()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnScoreChanged -= OnScoreChanged;
    }

    // ── Update: only lerp when value changed ─────────────────────
    private void Update()
    {
        if (!needsLerp) return;

        progressSlider.value = Mathf.Lerp(progressSlider.value, targetValue, lerpSpeed * Time.deltaTime);

        // Stop lerping once close enough (avoid infinite tiny steps)
        if (Mathf.Abs(progressSlider.value - targetValue) < 0.01f)
        {
            progressSlider.value = targetValue;
            needsLerp = false;
        }
    }

    // ── Event handler ─────────────────────────────────────────────
    private void OnScoreChanged(float score)
    {
        targetValue = score;
        needsLerp   = true;
    }
}
