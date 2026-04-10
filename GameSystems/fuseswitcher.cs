using UnityEngine;
using UnityEngine.Events;

public class FuseSwitcher : MonoBehaviour
{
    [Header("Pair")]
    [SerializeField] private FuseSwitcher partner;
    [SerializeField] private bool activeByDefault = false;

    [Header("Visual Feedback")]
    [SerializeField] private Renderer buttonRenderer;
    [SerializeField] private Color    colorNormal = Color.white;
    [SerializeField] private Color    colorHover  = Color.cyan;

    [Header("Speed Event")]
    [SerializeField] private SpeedEventChannel speedChannel;
    [SerializeField] private float speedMultiplier = 1f;

    // BUG FIX D — Ganti heuristik speedMultiplier<=1 dengan field eksplisit.
    // Heuristik lama rapuh: jika designer set speedMultiplier=1 untuk fuse2,
    // NotifySanity() akan memanggil OnFuse1Activated() padahal yang aktif fuse2.
    [Header("Sanity")]
    [Tooltip("Centang jika ini adalah Fuse 2 (menyebabkan sanity turun). " +
             "Kosongkan untuk Fuse 1 (sanity naik).")]
    [SerializeField] private bool isFuse2 = false;

    [Header("Unity Events")]
    public UnityEvent onActivated;
    public UnityEvent onDeactivated;

    // Save key berdasarkan nama GameObject — pastikan nama unik di scene
    private string SaveKey => $"FuseSwitcher_Active_{gameObject.name}";

    private void Start()
    {
        // Cek apakah ada save state
        if (WorldFlags.Has(SaveKey))
        {
            bool savedActive = WorldFlags.Get(SaveKey);
            gameObject.SetActive(savedActive);

            // Jika aktif, broadcast speed dan sync sanity.
            // BUG FIX — NotifySanity() tidak dipanggil saat restore dari save,
            // sehingga _isFuse2Active di SanitySystem tetap false meski Fuse 2
            // seharusnya aktif → sanity tidak turun setelah load.
            if (savedActive) { BroadcastSpeed(); NotifySanity(); }
        }
        else
        {
            // Tidak ada save — pakai default
            gameObject.SetActive(activeByDefault);
            if (activeByDefault) { BroadcastSpeed(); NotifySanity(); }
        }
    }

    private float _lastToggleTime = -10f;

    public void Toggle()
    {
        // Cooldown 0.5 detik agar tidak dipanggil 2x berturut-turut
        if (Time.time - _lastToggleTime < 0.5f) return;
        _lastToggleTime = Time.time;

        Debug.Log($"[FuseSwitcher] '{name}' Toggle()");

        // Trigger noise saat toggle fuse
        NoiseTracker.Instance?.AddNoiseToggleFuse();

        onDeactivated.Invoke();
        SaveState(false); // simpan diri sendiri = tidak aktif

        if (partner != null)
        {
            partner.gameObject.SetActive(true);
            partner.SaveState(true); // simpan partner = aktif
            partner.onActivated.Invoke();
            partner.BroadcastSpeed();
            partner.NotifySanity();
            Debug.Log($"[FuseSwitcher] Partner '{partner.name}' aktif, speed x{partner.speedMultiplier}");
        }
        else
        {
            Debug.LogWarning($"[FuseSwitcher] '{name}' tidak punya partner!");
        }

        gameObject.SetActive(false);
    }

    public void BroadcastSpeed()
    {
        if (speedChannel != null)
            speedChannel.Raise(speedMultiplier);
        else
            Debug.LogWarning($"[FuseSwitcher] '{name}' speedChannel belum diisi!");
    }

    public void NotifySanity()
    {
        if (SanitySystem.Instance == null) return;
        // BUG FIX D — Gunakan field isFuse2 yang eksplisit, bukan heuristik speedMultiplier.
        if (isFuse2)
            SanitySystem.Instance.OnFuse2Activated();
        else
            SanitySystem.Instance.OnFuse1Activated();
    }

    public void SaveState(bool active)
    {
        WorldFlags.Set(SaveKey, active);
    }

    /// Reset save state ke default (panggil dari DEV_ResetAll)
    public void ResetSave()
    {
        WorldFlags.Remove(SaveKey);
    }

    public void SetHover(bool hover)
    {
        if (buttonRenderer == null) return;
        buttonRenderer.material.color = hover ? colorHover : colorNormal;
    }
}