using System.Collections.Generic;

/// <summary>
/// GameSaveService — versi injectable dari GameSave static.
///
/// Fase 2: Class ini disiapkan sekarang tapi belum dipakai.
/// Fase 4: Setelah semua singleton player dimigrasi ke VContainer,
/// CheckpointTrigger dan ScenePortal akan di-inject GameSaveService
/// ini dan berhenti memanggil GameSave.Save() static.
///
/// GameSave static tetap ada selama migrasi sebagai compatibility shim.
/// </summary>
public class GameSaveService
{
    private readonly IReadOnlyList<IPersistable> _persistables;

    public GameSaveService(IReadOnlyList<IPersistable> persistables)
        => _persistables = persistables;

    /// Flush semua inventory lalu tulis ke disk — satu titik masuk untuk semua save.
    public void Save()
    {
        foreach (var p in _persistables)
            p.Persist();

        SaveFile.ForceWrite();
    }
}