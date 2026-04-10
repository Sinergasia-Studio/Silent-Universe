using UnityEngine;

[CreateAssetMenu(fileName = "DampenerState", menuName = "Events/Dampener State")]
public class DampenerState : ScriptableObject
{
    [Header("Noise Modifiers saat Dampener ON")]
    [Tooltip("Multiplier decay noise (1.5 = 50% lebih cepat turun)")]
    public float decayMultiplier = 1.5f;
    [Tooltip("Noise penalty saat pertama kali ON")]
    public float noisePenalty    = 50f;
    [Tooltip("Berapa detik dampener aktif (0 = tidak ada batas)")]
    public float activeDuration  = 30f;

    // Baca dari JSON save via SaveFile
    public bool  IsOn           => SaveFile.Data.dampenerOn;
    public float PendingPenalty => SaveFile.Data.dampenerPendingPenalty;

    public bool IsExpired()
    {
        if (!IsOn) return false;
        if (activeDuration <= 0f) return false;
        // BUG FIX B — Gunakan Time.time (game time) bukan realtimeSinceStartup.
        // realtimeSinceStartup reset ke 0 setiap kali game di-launch ulang, sehingga
        // (realtimeSinceStartup - dampenerTurnOnTime) bisa negatif atau sangat besar
        // tergantung kapan save ditulis vs kapan game dijalankan lagi.
        // Time.time konsisten dalam satu sesi dan tidak terpengaruh scene reload.
        return Time.time - SaveFile.Data.dampenerTurnOnTime >= activeDuration;
    }

    public void TurnOn()
    {
        if (IsOn) return;
        var d = SaveFile.Data;
        d.dampenerOn             = true;
        // BUG FIX B — Simpan Time.time, bukan realtimeSinceStartup.
        d.dampenerTurnOnTime     = Time.time;
        d.dampenerPendingPenalty = noisePenalty;
        SaveFile.Write();
        Debug.Log($"[Dampener] ON — penalty {noisePenalty} pending, durasi {activeDuration}s");
    }

    public void TurnOff()
    {
        var d = SaveFile.Data;
        d.dampenerOn             = false;
        d.dampenerTurnOnTime     = 0f;
        d.dampenerPendingPenalty = 0f;
        SaveFile.Write();
        Debug.Log("[Dampener] OFF");
    }

    public void ConsumePenalty()
    {
        SaveFile.Data.dampenerPendingPenalty = 0f;
        SaveFile.Write();
    }

    public void ResetState()
    {
        var d = SaveFile.Data;
        d.dampenerOn             = false;
        d.dampenerTurnOnTime     = 0f;
        d.dampenerPendingPenalty = 0f;
        SaveFile.Write();
    }
}