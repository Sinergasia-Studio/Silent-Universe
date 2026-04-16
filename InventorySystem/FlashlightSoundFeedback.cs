using UnityEngine;
using VContainer;

public class FlashlightSoundFeedback : MonoBehaviour
{
    [Header("Audio Source — Loop (untuk suara On)")]
    [SerializeField] private AudioSource loopSource;

    [Header("Audio Source — OneShot (untuk suara lain)")]
    [SerializeField] private AudioSource oneShotSource;

    [Header("Sounds")]
    [SerializeField] private AudioClip soundOn;
    [SerializeField] private AudioClip soundOff;
    [SerializeField] private AudioClip soundBatteryDepleted;
    [SerializeField] private AudioClip soundOverheatStart;
    [SerializeField] private AudioClip soundOverheatEnd;
    [SerializeField] private AudioClip soundRechargeComplete;
    [SerializeField] private AudioClip soundBrokenStart;
    [SerializeField] private AudioClip soundBrokenEnd;

    [Header("Volume")]
    [SerializeField] [Range(0f, 1f)] private float loopVolume    = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float oneShotVolume = 0.8f;

    [Header("Reverb Settings")]
    [SerializeField] [Range(0f, 1f)] private float onReverbLevel = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float offReverbLevel = 0f;

    

    [Inject] private FlashlightController _flashlight;

    private FlashlightController _fl;

    private void Awake()
    {
        var sources = GetComponents<AudioSource>();

        if (loopSource == null)
            loopSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();

        if (oneShotSource == null)
            oneShotSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();

        loopSource.playOnAwake    = false;
        loopSource.loop           = true;
        oneShotSource.playOnAwake = false;
        oneShotSource.loop        = false;

        // Register ke AudioManager — mixer group Flashlight di-assign otomatis
        AudioServices.Manager?.RegisterSource(AudioCategory.Flashlight, loopSource);
        AudioServices.Manager?.RegisterSource(AudioCategory.Flashlight, oneShotSource);
    }

    private void Start()
    {
        _fl = _flashlight ?? FlashlightController.Instance;
        if (_fl == null) return;

        _fl.onFlashlightOn.AddListener(OnOn);
        _fl.onFlashlightOff.AddListener(OnOff);
        _fl.onBatteryDepleted.AddListener(OnBatteryDepleted);
        _fl.onOverheatStart.AddListener(OnOverheatStart);
        _fl.onOverheatEnd.AddListener(OnOverheatEnd);
        _fl.onRechargeComplete.AddListener(OnRechargeComplete);
        _fl.onBrokenStart.AddListener(OnBrokenStart);
        _fl.onBrokenEnd.AddListener(OnBrokenEnd);
    }

    private void OnDestroy()
    {
        // Unregister saat scene unload
        AudioServices.Manager?.UnregisterSource(AudioCategory.Flashlight, loopSource);
        AudioServices.Manager?.UnregisterSource(AudioCategory.Flashlight, oneShotSource);

        if (_fl == null) return;

        _fl.onFlashlightOn.RemoveListener(OnOn);
        _fl.onFlashlightOff.RemoveListener(OnOff);
        _fl.onBatteryDepleted.RemoveListener(OnBatteryDepleted);
        _fl.onOverheatStart.RemoveListener(OnOverheatStart);
        _fl.onOverheatEnd.RemoveListener(OnOverheatEnd);
        _fl.onRechargeComplete.RemoveListener(OnRechargeComplete);
        _fl.onBrokenStart.RemoveListener(OnBrokenStart);
        _fl.onBrokenEnd.RemoveListener(OnBrokenEnd);

        StopLoop();
    }

    // ── Handlers ──

    private void OnOn()
    {
if (soundOn == null) return;
    loopSource.clip   = soundOn;
    loopSource.volume = loopVolume;
    loopSource.Play();
    
    // Mengaktifkan reverb saat senter nyala
    AudioServices.Manager?.SetFlashlightReverb(onReverbLevel); // Aktifkan reverb
    }

    private void OnOff()
    {
    StopLoop();
    PlayOneShot(soundOff);
    
    // Mematikan/mengurangi reverb saat senter mati
    AudioServices.Manager?.SetFlashlightReverb(offReverbLevel); // Matikan reverb
    }

    private void OnBatteryDepleted()    => PlayOneShot(soundBatteryDepleted);
    private void OnOverheatStart()      { StopLoop(); PlayOneShot(soundOverheatStart); }
    private void OnOverheatEnd()        => PlayOneShot(soundOverheatEnd);
    private void OnRechargeComplete()   => PlayOneShot(soundRechargeComplete);
    private void OnBrokenStart(float _) { StopLoop(); PlayOneShot(soundBrokenStart); }
    private void OnBrokenEnd()          => PlayOneShot(soundBrokenEnd);

    // ── Helpers ──

    private void StopLoop()
    {
        if (loopSource.isPlaying)
            loopSource.Stop();
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null || oneShotSource == null) return;
        oneShotSource.PlayOneShot(clip, oneShotVolume);
    }
}