/// <summary>
/// Static accessor untuk IAudioManager — ditaruh di _Core.
///
/// AudioManager (di GameSystems) mendaftarkan dirinya ke sini saat Awake.
/// Assembly lain (InventorySystem, Narrative, dll) pakai AudioServices.Manager
/// tanpa perlu tahu AudioManager ada di assembly mana.
///
/// Cara pakai:
///   AudioServices.Manager?.RegisterSource(AudioCategory.Flashlight, mySource);
///   AudioServices.Manager?.SetVolumeMusic(0.8f);
/// </summary>
public static class AudioServices
{
    public static IAudioManager Manager { get; private set; }

    /// <summary>Dipanggil dari AudioManager.Awake()</summary>
    public static void Register(IAudioManager manager) => Manager = manager;

    /// <summary>Dipanggil dari AudioManager.OnDestroy() — optional tapi clean</summary>
    public static void Unregister(IAudioManager manager)
    {
        if (Manager == manager) Manager = null;
    }
}