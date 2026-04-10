using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// RhythmBackButton — pasang pada Button di Canvas scene rhythm game.
///
/// Setup:
///   1. Buat Button di Canvas
///   2. Pasang script ini
///   3. Assign rhythmGameReturn di Inspector
///   4. Button.onClick → RhythmBackButton.OnClick()
///      ATAU assign langsung ke Button.onClick → RhythmGameReturn.GoBack()
///
/// Tombol menampilkan warning saat sanity masih tinggi.
/// </summary>
public class RhythmBackButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RhythmGameReturn rhythmGameReturn;

    [Header("UI")]
    [Tooltip("Label tombol back")]
    [SerializeField] private TMP_Text buttonLabel;
    [Tooltip("Warning text saat sanity rendah")]
    [SerializeField] private TMP_Text warningText;

    [Header("Settings")]
    [Tooltip("Sanity threshold untuk tampilkan warning (0-1)")]
    [SerializeField] private float warningThreshold = 0.3f;

    private void Start()
    {
        if (rhythmGameReturn == null)
            rhythmGameReturn = FindFirstObjectByType<RhythmGameReturn>();

        UpdateUI();
    }

    private void Update()
    {
        UpdateUI();
    }

    public void OnClick()
    {
        rhythmGameReturn?.GoBack();
    }

    private void UpdateUI()
    {
        if (warningText == null) return;

        var sanity = SanitySystem.Instance;
        if (sanity == null) { warningText.gameObject.SetActive(false); return; }

        bool showWarning = sanity.SanityPercent <= warningThreshold;
        warningText.gameObject.SetActive(showWarning);

        if (showWarning)
        {
            int pct = Mathf.RoundToInt(sanity.SanityPercent * 100f);
            warningText.text  = "⚠ SANITY " + pct + "% — Segera nonaktifkan Fuse 2!";
            warningText.color = Color.Lerp(Color.red, Color.yellow, sanity.SanityPercent / warningThreshold);
        }
    }
}