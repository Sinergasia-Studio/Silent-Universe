using UnityEngine;

/// <summary>
/// Interface audio manager — ditaruh di _Core supaya semua assembly bisa akses
/// tanpa harus reference GameSystems secara langsung.
///
/// Implementasi: AudioManager.cs di GameSystems.
/// Konsumen: FlashlightSoundFeedback, ItemSoundFeedback, dll di InventorySystem.
///
/// Cara pakai di assembly manapun:
///   AudioServices.Manager?.RegisterSource(AudioCategory.Flashlight, mySource);
/// </summary>
public interface IAudioManager
{
    void  RegisterSource(AudioCategory category, AudioSource source);
    void  UnregisterSource(AudioCategory category, AudioSource source);
    void  SetReverbSend(float normalizedValue);
    float GetReverbSend();
    void SetFlashlightReverb(float normalizedValue);
    void  SetVolumeMaster(float value);
    void  SetVolumeSFX(float value);
    void  SetVolumeFootstep(float value);
    void  SetVolumeFlashlight(float value);
    void  SetVolumeItem(float value);
    void  SetVolumeEnemy(float value);
    void  SetVolumeNPC(float value);
    void  SetVolumeGenerator(float value);
    void  SetVolumeMusic(float value);
    void  SetVolumeAmbience(float value);
}