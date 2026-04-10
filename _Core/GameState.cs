/// <summary>
/// GameState — shared runtime flags yang bisa diakses oleh semua assembly.
/// Karena Core tidak punya dependency ke assembly lain, class ini aman
/// direferensikan dari Narrative, InventorySystem, GameSystems, dll.
///
/// BUG FIX #1 : IsInputLocked  — diset oleh PlayerMovement (Narrative),
///              dibaca oleh FlashlightController (InventorySystem).
/// BUG FIX #2 : IsCCTVActive   — diset oleh MonitorInteractable (GameSystems),
///              dibaca oleh DialogueManager (Narrative).
/// </summary>
public static class GameState
{
    /// True saat input player dikunci (dialog, cutscene, dll.)
    /// Diset oleh PlayerMovement.SetInputEnabled().
    public static bool IsInputLocked { get; set; }

    /// True saat player sedang dalam mode CCTV monitor.
    /// Diset oleh MonitorInteractable saat enter/exit CCTV.
    public static bool IsCCTVActive { get; set; }

    /// BUG FIX #3 — Nilai noise yang akan disimpan ke disk.
    /// NoiseTracker mengisi ini via PushNoiseToSave() sebelum GameSave.Save() dipanggil.
    /// GameSave.Save() membaca nilai ini dan menulis ke SaveFile.Data.savedNoise.
    /// NoiseTracker membaca SaveFile.Data.savedNoise saat scene di-load untuk restore.
    public static float SavedNoise { get; set; }
}
