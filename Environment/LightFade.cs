using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// EmergencyLightSystem — trigger via UnityEvent atau public method.
///
/// Flow:
///   1. Putih  — kondisi normal
///   2. Fliker — lampu kedip-kedip cepat masih putih (transisi chaos)
///   3. Merah  — warna snap ke merah, lampu stabil sebentar
///   4. Fade   — tiap lampu mati nyala smooth secara independen (loop)
///
/// Array lights[] hanya berisi Light component.
/// Semua parameter (intensitas, warna, durasi, dll) dikontrol secara global.
/// Async flicker dijaga dengan seed Perlin berbeda per index.
/// </summary>
public class EmergencyLightSystem : MonoBehaviour
{
    // ── Lights ────────────────────────────────────────────────────

    [Header("Lights")]
    [SerializeField] private Light[] lights;

    // ── Global Light Settings ─────────────────────────────────────

    [Header("Emergency Color & Intensity")]
    [SerializeField] private Color  emergencyColor  = new Color(1f, 0.05f, 0.05f);
    [SerializeField] private float  onIntensity     = 1.5f;

    [Header("Fade Loop (Phase 4)")]
    [Tooltip("Intensitas minimum saat redup (0 = mati total)")]
    [Range(0f, 1f)]
    [SerializeField] private float  minIntensity    = 0f;

    [Tooltip("Durasi fase ON sebelum fade out")]
    [SerializeField] private float  onDuration      = 0.4f;

    [Tooltip("Durasi fase OFF sebelum fade in")]
    [SerializeField] private float  offDuration     = 0.6f;

    [Tooltip("Kecepatan fade (lebih tinggi = lebih cepat)")]
    [SerializeField] private float  fadeSpeed       = 6f;

    [Header("Phase 2 — Fliker (putih, chaos)")]
    [Tooltip("Durasi fase fliker putih sebelum warna berubah merah")]
    [SerializeField] private float  flickerDuration = 1.5f;

    [Tooltip("Kecepatan fliker — lebih tinggi = lebih panik")]
    [SerializeField] private float  flickerSpeed    = 20f;

    [Header("Phase 3 — Merah stabil")]
    [Tooltip("Durasi lampu merah stabil sebelum mulai fade loop")]
    [SerializeField] private float  redHoldDuration = 0.5f;

    [Header("Events")]
    public UnityEvent onEmergencyStarted;
    public UnityEvent onEmergencyStopped;

    // ── Private State ─────────────────────────────────────────────

    // Cached original values — parallel array dengan lights[]
    private Color[] _originalColors;
    private float[] _originalIntensities;

    private bool       _active;
    private Coroutine  _mainRoutine;
    private Coroutine[] _flickerRoutines;

    // ── Public API ────────────────────────────────────────────────

    public void TriggerEmergency()
    {
        if (_active) return;
        _active = true;
        if (_mainRoutine != null) StopCoroutine(_mainRoutine);
        _mainRoutine = StartCoroutine(EmergencyRoutine());
    }

    public void StopEmergency()
    {
        if (!_active) return;
        _active = false;

        StopAllFlickers();
        if (_mainRoutine != null) { StopCoroutine(_mainRoutine); _mainRoutine = null; }

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
                StartCoroutine(RestoreLight(i));
        }

        onEmergencyStopped.Invoke();
    }

    // ── Lifecycle ─────────────────────────────────────────────────

    private void Awake()
    {
        int count = lights != null ? lights.Length : 0;

        _originalColors      = new Color[count];
        _originalIntensities = new float[count];
        _flickerRoutines     = new Coroutine[count];

        for (int i = 0; i < count; i++)
        {
            if (lights[i] == null) continue;
            _originalColors[i]      = lights[i].color;
            _originalIntensities[i] = lights[i].intensity;
        }
    }

    // ── Main Routine ──────────────────────────────────────────────

    private IEnumerator EmergencyRoutine()
    {
        onEmergencyStarted.Invoke();

        yield return PhaseFlicker();
        yield return PhaseRedHold();

        PhaseStartFadeLoop();
    }

    // ── Phase 2: Fliker putih ─────────────────────────────────────

    private IEnumerator PhaseFlicker()
    {
        float elapsed = 0f;
        while (elapsed < flickerDuration)
        {
            elapsed += Time.deltaTime;

            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;

                // Seed berbeda per index agar Perlin tidak sync antar lampu.
                // i * 17.3f memberikan jarak yang cukup di noise space.
                float seed  = i * 17.3f;
                float noise = Mathf.PerlinNoise(Time.time * flickerSpeed + seed, 0f);

                lights[i].intensity = Mathf.Lerp(0f, _originalIntensities[i], noise);
                lights[i].color     = _originalColors[i];
            }

            yield return null;
        }
    }

    // ── Phase 3: Snap merah, tahan ────────────────────────────────

    private IEnumerator PhaseRedHold()
    {
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null) continue;
            lights[i].color     = emergencyColor;
            lights[i].intensity = onIntensity;
        }

        yield return new WaitForSeconds(redHoldDuration);
    }

    // ── Phase 4: Fade loop ────────────────────────────────────────

    private void PhaseStartFadeLoop()
    {
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null) continue;

            // Offset awal acak agar tiap lampu tidak mulai fase fade bersamaan
            float startOffset   = Random.Range(0f, offDuration);
            _flickerRoutines[i] = StartCoroutine(FadeLoopRoutine(i, startOffset));
        }
    }

    private IEnumerator FadeLoopRoutine(int index, float startDelay)
    {
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            // Fade OUT
            yield return Fade(index, onIntensity, minIntensity, offDuration);
            yield return new WaitForSeconds(offDuration * Random.Range(0.5f, 2f));

            // Fade IN
            yield return Fade(index, minIntensity, onIntensity, onDuration);
            yield return new WaitForSeconds(onDuration * Random.Range(0.5f, 1.5f));
        }
    }

    private IEnumerator Fade(int index, float from, float to, float duration)
    {
        Light light   = lights[index];
        float elapsed = 0f;
        float t       = 0f;

        while (t < 1f)
        {
            elapsed        += Time.deltaTime;
            t               = Mathf.Clamp01(elapsed * fadeSpeed / duration);
            light.intensity = Mathf.Lerp(from, to, EaseInOut(t));
            light.color     = emergencyColor;
            yield return null;
        }

        light.intensity = to;
    }

    // ── Restore ───────────────────────────────────────────────────

    private IEnumerator RestoreLight(int index)
    {
        Light  light    = lights[index];
        float  elapsed  = 0f;
        float  duration = 0.5f;
        Color  startCol = light.color;
        float  startInt = light.intensity;

        while (elapsed < duration)
        {
            elapsed        += Time.deltaTime;
            float t         = elapsed / duration;
            light.color     = Color.Lerp(startCol, _originalColors[index],      t);
            light.intensity = Mathf.Lerp(startInt, _originalIntensities[index], t);
            yield return null;
        }

        light.color     = _originalColors[index];
        light.intensity = _originalIntensities[index];
    }

    // ── Helpers ───────────────────────────────────────────────────

    private void StopAllFlickers()
    {
        if (_flickerRoutines == null) return;
        for (int i = 0; i < _flickerRoutines.Length; i++)
        {
            if (_flickerRoutines[i] != null)
            {
                StopCoroutine(_flickerRoutines[i]);
                _flickerRoutines[i] = null;
            }
        }
    }

    private static float EaseInOut(float t) => t * t * (3f - 2f * t);
}