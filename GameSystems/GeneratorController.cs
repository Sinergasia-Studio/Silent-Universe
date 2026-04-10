using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// GeneratorController — pasang pada GameObject generator.
/// Generator hanya bisa dinyalakan jika fuse box sudah terpasang fuse.
///
/// Setup:
///   GeneratorObject
///     ├── Collider
///     ├── AudioSource          ← suara generator
///     └── GeneratorController  (script ini)
///
/// Assign di Inspector:
///   - fuseBox         → FuseBox script di scene
///   - generatorLights → list Light yang nyala saat generator on
///   - audioSource     → AudioSource pada generator
///   - audioOn / audioOff → AudioClip saat nyala/mati
///   - audioRunning    → AudioClip loop saat generator berjalan
/// </summary>
public class GeneratorController : MonoBehaviour, IInteractable
{
    [Header("References")]
    [Tooltip("FuseBox yang harus sudah terpasang fuse")]
    [SerializeField] private FuseBox fuseBox;

    [Header("Lights")]
    [Tooltip("List semua Light yang ikut nyala saat generator on")]
    [SerializeField] private List<Light> generatorLights = new();
    [SerializeField] private float       lightFadeInDuration  = 1.5f;
    [SerializeField] private float       lightFadeOutDuration = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   audioOn;
    [SerializeField] private AudioClip   audioOff;
    [SerializeField] private AudioClip   audioRunning;   // loop

    [Header("Prompts")]
    [SerializeField] private string promptNoFuse     = "[Fuse box belum terpasang]";
    [SerializeField] private string promptTurnOn     = "Tahan [E] untuk nyalakan generator";
    [SerializeField] private string promptTurnOff    = "Tahan [E] untuk matikan generator";
    [SerializeField] private string promptRunning    = "Generator menyala";

    [Header("Settings")]
    [SerializeField] private bool canTurnOff = true;

    [Header("Events")]
    public UnityEvent onGeneratorOn;    // saat generator berhasil dinyalakan
    public UnityEvent onGeneratorOff;   // saat generator dimatikan
    public UnityEvent onNoFuse;         // saat player coba nyalakan tanpa fuse

    // ── state ──
    private bool      _isOn;
    private Coroutine _lightFadeRoutine;

    public bool IsOn => _isOn;

    private void Start()
    {
        // pastikan semua light mati di awal
        SetLightsImmediate(0f);
    }

    // ── IInteractable ──
    public string PromptText
    {
        get
        {
            if (_isOn) return canTurnOff ? promptTurnOff : promptRunning;
            bool fuseReady = fuseBox != null && fuseBox.FuseInstalled;
            return fuseReady ? promptTurnOn : promptNoFuse;
        }
    }

    public bool CanInteract
    {
        get
        {
            if (_isOn) return canTurnOff;
            return fuseBox != null && fuseBox.FuseInstalled;
        }
    }

    public void OnInteract(GameObject interactor)
    {
        if (fuseBox == null || !fuseBox.FuseInstalled)
        {
            onNoFuse.Invoke();
            Debug.Log("[Generator] Fuse box belum terpasang!");
            return;
        }

        if (_isOn) TurnOff();
        else       TurnOn();
    }

    // ── Public API ──
    public void TurnOn()
    {
        if (_isOn) return;
        if (fuseBox == null || !fuseBox.FuseInstalled)
        {
            onNoFuse.Invoke();
            return;
        }

        _isOn = true;

        // suara
        if (audioSource != null)
        {
            if (audioOn != null) audioSource.PlayOneShot(audioOn);

            if (audioRunning != null)
            {
                audioSource.clip = audioRunning;
                audioSource.loop = true;
                // delay sedikit agar audioOn selesai dulu
                StartCoroutine(PlayRunningDelayed(audioOn != null ? audioOn.length : 0f));
            }
        }

        // lampu fade in
        if (_lightFadeRoutine != null) StopCoroutine(_lightFadeRoutine);
        _lightFadeRoutine = StartCoroutine(FadeLights(0f, 1f, lightFadeInDuration));

        onGeneratorOn.Invoke();
        Debug.Log("[Generator] Generator menyala!");
    }

    public void TurnOff()
    {
        if (!_isOn) return;
        _isOn = false;

        // suara
        if (audioSource != null)
        {
            audioSource.Stop();
            if (audioOff != null) audioSource.PlayOneShot(audioOff);
        }

        // lampu fade out
        if (_lightFadeRoutine != null) StopCoroutine(_lightFadeRoutine);
        _lightFadeRoutine = StartCoroutine(FadeLights(1f, 0f, lightFadeOutDuration));

        onGeneratorOff.Invoke();
        Debug.Log("[Generator] Generator mati.");
    }

    // ── lights ──
    private void SetLightsImmediate(float intensity)
    {
        foreach (var light in generatorLights)
        {
            if (light == null) continue;
            light.intensity = intensity;
            light.enabled   = intensity > 0f;
        }
    }

    private IEnumerator FadeLights(float from, float to, float duration)
    {
        // aktifkan semua light sebelum fade in
        if (to > 0f)
            foreach (var l in generatorLights)
                if (l != null) { l.intensity = 0f; l.enabled = true; }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            foreach (var light in generatorLights)
            {
                if (light == null) continue;
                light.intensity = Mathf.Lerp(from, to, t);
            }
            yield return null;
        }

        // matikan light jika fade out selesai
        if (to <= 0f)
            foreach (var l in generatorLights)
                if (l != null) { l.intensity = 0f; l.enabled = false; }
    }

    private IEnumerator PlayRunningDelayed(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (_isOn && audioSource != null && audioRunning != null)
            audioSource.Play();
    }
}
