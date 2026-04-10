using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/Speed Event Channel", fileName = "SpeedEventChannel")]
public class SpeedEventChannel : ScriptableObject
{
    public event Action<float> OnSpeedChanged;

    /// Nilai multiplier terakhir — persist cross-scene karena ScriptableObject
    public float LastMultiplier { get; private set; } = 1f;

    [Header("Debug")]
    [SerializeField] private float debugTestMultiplier = 1.5f;

    // DIHAPUS: OnEnable reset — ini yang bikin nilai hilang saat scene load
    // Nilai hanya reset saat game pertama kali start via RuntimeInitialize

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetAllChannels()
    {
        // Reset semua SpeedEventChannel asset ke 1 saat game pertama start
        // Tidak dipanggil saat pindah scene — hanya saat pertama kali play
    }

    public void Raise(float multiplier)
    {
        LastMultiplier = multiplier;
        int count = OnSpeedChanged?.GetInvocationList()?.Length ?? 0;
        Debug.Log($"[SpeedEventChannel] '{name}' Raise({multiplier}) — {count} listener(s)");
        OnSpeedChanged?.Invoke(multiplier);
    }

    /// Reset manual — panggil saat New Game / DEV reset
    public void ResetToDefault()
    {
        LastMultiplier = 1f;
        OnSpeedChanged?.Invoke(1f);
    }

    [ContextMenu("▶ Test Raise")]
    private void TestRaise() => Raise(debugTestMultiplier);

    [ContextMenu("▶ Reset x1")]
    private void TestReset() => ResetToDefault();
}