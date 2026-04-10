using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

/// <summary>
/// FlashlightController — pasang pada Player.
///
/// Mechanic baru:
///   - Tahan F → senter nyala | Lepas F → langsung mati
///   - Ada overheat threshold — tahan terlalu lama → overheat → cooldown
///   - Saat overheat → senter tidak bisa dinyalakan, battery tidak bisa dipakai
///   - Battery habis → isi ulang dengan R, ada progress bar (lama)
/// </summary>
public class FlashlightController : MonoBehaviour
{
    public static FlashlightController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Light flashlightLight;

    [Header("Battery Override")]
    [SerializeField] private bool  useOverride              = false;
    [SerializeField] private float batteryDurationOverride  = 120f;
    [Range(0f, 0.5f)]
    [SerializeField] private float flickerThresholdOverride = 0.15f;

    [Header("Overheat")]
    [Tooltip("Berapa detik tahan F sebelum overheat")]
    [SerializeField] private float overheatThreshold  = 5f;
    [Tooltip("Berapa detik cooldown setelah overheat")]
    [SerializeField] private float overheatCooldown   = 4f;
    [Tooltip("Detik sebelum overheat mulai flicker cepat")]
    [SerializeField] private float overheatWarningAt  = 1f;

    [Header("Recharge")]
    [Tooltip("Berapa detik proses mengisi 1 baterai")]
    [SerializeField] private float rechargeDuration   = 3f;

    [Header("Flicker")]
    [SerializeField] private float flickerMinInterval    = 0.05f;
    [SerializeField] private float flickerMaxInterval    = 0.2f;
    [SerializeField] private float flickerMinIntensity   = 0.1f;
    [SerializeField] private float overheatFlickerSpeed  = 0.03f; // interval flicker saat mau overheat

    [Header("Spam Detection")]
    [Tooltip("Berapa kali F ditekan dalam spamWindow detik sebelum dianggap spam")]
    [SerializeField] private int   spamPressLimit  = 5;
    [Tooltip("Window waktu untuk deteksi spam (detik)")]
    [SerializeField] private float spamWindow      = 2f;
    [Tooltip("Berapa detik senter rusak akibat spam")]
    [SerializeField] private float spamBrokenDuration = 30f;

    [Header("Events")]
    public UnityEvent          onFlashlightOn;
    public UnityEvent          onFlashlightOff;
    public UnityEvent          onBatteryDepleted;
    public UnityEvent<float>   onBatteryChanged;       // (0-1)
    public UnityEvent          onOverheatStart;        // saat mulai overheat
    public UnityEvent          onOverheatEnd;          // saat cooldown selesai
    public UnityEvent<float>   onOverheatProgress;     // (0-1) progress overheat saat tahan F
    public UnityEvent<float>   onRechargeProgress;     // (0-1) progress mengisi battery
    public UnityEvent          onRechargeComplete;     // saat battery selesai diisi
    public UnityEvent<float>   onBrokenStart;          // (durasi) saat senter rusak akibat spam
    public UnityEvent          onBrokenEnd;             // saat senter selesai rusak

    // ── state ──
    private bool           _isOn;
    private bool           _hasFlashlight;
    private bool           _isOverheated;
    private bool           _isRecharging;
    private float          _holdTime;              // berapa lama F ditahan sesi ini
    private float          _batteryRemaining;
    private float          _activeBatteryDuration;
    private float          _activeFlickerThreshold = 0.15f;
    private float          _baseIntensity;
    private Coroutine      _flickerRoutine;
    private Coroutine      _batteryRoutine;
    private Coroutine      _cooldownRoutine;
    private Coroutine      _rechargeRoutine;
    private bool           _fHeld;

    // ── Spam Detection ──
    private int            _spamPressCount;
    private float          _spamWindowTimer;
    private bool           _isBroken;
    private Coroutine      _brokenRoutine;

    // ── State tracking untuk persist saat drop ──
    private float          _overheatTimeRemaining;
    private float          _brokenTimeRemaining;

    // ── State Snapshot (untuk save saat drop) ──
    public struct FlashlightState
    {
        public bool  isOverheated;
        public float overheatTimeRemaining; // sisa cooldown
        public bool  isBroken;
        public float brokenTimeRemaining;   // sisa broken
        public float batteryRemaining;
    }

    // ── Public Properties ──
    public bool  IsOn              => _isOn;
    public bool  IsOverheated      => _isOverheated;
    public bool  IsRecharging      => _isRecharging;
    public float BatteryRemaining  => _batteryRemaining;
    public float BatteryPercent    => _activeBatteryDuration > 0
                                      ? _batteryRemaining / _activeBatteryDuration : 1f;
    public float OverheatPercent   => Mathf.Clamp01(_holdTime / overheatThreshold);
    public bool  IsBroken          => _isBroken;

    // ──────────────────────────────────────────
    // Unity Lifecycle
    // ──────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        if (flashlightLight != null)
        {
            _baseIntensity          = flashlightLight.intensity;
            flashlightLight.enabled = false;
        }

        // Daftarkan persist callback agar GameSave.Save() otomatis flush state senter.
        // Pattern sama dengan PlayerInventory dan PlayerDiskInventory.
        GameSave.RegisterPersistCallback(PersistFlashlight);
    }

    private void Start()
    {
        var equip = PlayerEquipment.Instance;
        if (equip != null)
        {
            equip.onFlashlightEquipped.AddListener(OnFlashlightEquipped);
            equip.onFlashlightUnequipped.AddListener(OnFlashlightUnequipped);
        }
    }

    private void OnDestroy()
    {
        GameSave.UnregisterPersistCallback(PersistFlashlight);
        var equip = PlayerEquipment.Instance;
        if (equip != null)
        {
            equip.onFlashlightEquipped.RemoveListener(OnFlashlightEquipped);
            equip.onFlashlightUnequipped.RemoveListener(OnFlashlightUnequipped);
        }
    }

    private void Update()
    {
        if (!_hasFlashlight) return;

        // Fallback polling keyboard langsung — lebih reliable untuk detect released
        // BUG FIX #1 — Cek GameState.IsInputLocked (Core assembly) agar tidak butuh
        // referensi ke Narrative assembly. Senter tidak bisa dinyalakan saat dialog.
        if (UnityEngine.InputSystem.Keyboard.current != null && !GameState.IsInputLocked)
        {
            var key = UnityEngine.InputSystem.Keyboard.current.fKey;
            if (key.wasPressedThisFrame)  OnToggleFlashlightStarted();
            if (key.wasReleasedThisFrame) OnToggleFlashlightCanceled();
        }

        // Reset spam counter setelah window habis
        if (_spamPressCount > 0)
        {
            _spamWindowTimer += Time.deltaTime;
            if (_spamWindowTimer >= spamWindow)
            {
                _spamPressCount  = 0;
                _spamWindowTimer = 0f;
            }
        }

        if (_isBroken || _isOverheated || _isRecharging) return;
        if (_batteryRemaining <= 0 && _activeBatteryDuration > 0) return;

        if (_fHeld)
        {
            // Akumulasi waktu tahan
            _holdTime += Time.deltaTime;
            onOverheatProgress.Invoke(OverheatPercent);

            // Warning flicker saat mendekati overheat
            float timeLeft = overheatThreshold - _holdTime;
            if (timeLeft <= overheatWarningAt && _flickerRoutine == null)
                _flickerRoutine = StartCoroutine(OverheatWarningFlicker());

            // Overheat
            if (_holdTime >= overheatThreshold)
                TriggerOverheat();
        }
    }

    // ──────────────────────────────────────────
    // Input System Callbacks
    // ──────────────────────────────────────────

    // Dipanggil saat F ditekan (phase: Started)
    public void OnToggleFlashlightStarted()
    {
        if (!_hasFlashlight) return;
        if (_isBroken || _isOverheated || _isRecharging) return;
        if (_batteryRemaining <= 0 && _activeBatteryDuration > 0) return;

        // Spam detection
        _spamPressCount++;
        if (_spamPressCount >= spamPressLimit)
        {
            TriggerBroken();
            return;
        }

        _fHeld = true;
        TurnOn();
    }

    // Dipanggil saat F dilepas (phase: Canceled)
    public void OnToggleFlashlightCanceled()
    {
        if (_isBroken) return;
        _fHeld    = false;
        _holdTime = 0f;
        onOverheatProgress.Invoke(0f);
        StopOverheatWarning();
        TurnOff();
    }

    // Fallback jika masih pakai InputValue
    public void OnToggleFlashlight(InputValue value)
    {
        if (value.isPressed) OnToggleFlashlightStarted();
        else                 OnToggleFlashlightCanceled();
    }

    public void OnRecharge(InputValue value)
    {
        if (!value.isPressed) return;
        if (!_hasFlashlight) return;
        if (_isOn || _isOverheated || _isRecharging) return;
        if (_batteryRemaining > 0 && _activeBatteryDuration > 0) return; // battery masih ada

        var inv = PlayerBatteryInventory.Instance;
        if (inv == null || inv.IsEmpty) return;

        StartRecharge(inv);
    }

    // ──────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────

    /// Ambil snapshot state saat ini (dipanggil ItemDropper sebelum drop)
    public FlashlightState GetState() => new FlashlightState
    {
        isOverheated          = _isOverheated,
        overheatTimeRemaining = _overheatTimeRemaining,
        isBroken              = _isBroken,
        brokenTimeRemaining   = _brokenTimeRemaining,
        batteryRemaining      = _batteryRemaining,
    };

    /// Restore state setelah pickup (dipanggil FlashlightPickup)
    public void RestoreState(FlashlightState state)
    {
        _batteryRemaining = state.batteryRemaining;
        onBatteryChanged.Invoke(BatteryPercent);

        if (state.isOverheated && state.overheatTimeRemaining > 0f)
        {
            _isOverheated          = true;
            _overheatTimeRemaining = state.overheatTimeRemaining;
            onOverheatStart.Invoke();
            if (_cooldownRoutine != null) StopCoroutine(_cooldownRoutine);
            _cooldownRoutine = StartCoroutine(CooldownRoutine());
            Debug.Log($"[Flashlight] Restore overheat — sisa {state.overheatTimeRemaining:F1}s");
        }

        if (state.isBroken && state.brokenTimeRemaining > 0f)
        {
            _isBroken            = true;
            _brokenTimeRemaining = state.brokenTimeRemaining;
            onBrokenStart.Invoke(state.brokenTimeRemaining);
            if (_brokenRoutine != null) StopCoroutine(_brokenRoutine);
            _brokenRoutine = StartCoroutine(BrokenRoutine());
            Debug.Log($"[Flashlight] Restore broken — sisa {state.brokenTimeRemaining:F1}s");
        }
    }

    public void TurnOn()
    {
        if (_isOn || !_hasFlashlight || flashlightLight == null) return;
        if (_isOverheated || _isRecharging) return;
        if (_batteryRemaining <= 0 && _activeBatteryDuration > 0) return;

        _isOn                     = true;
        flashlightLight.enabled   = true;
        flashlightLight.intensity = _baseIntensity;
        onFlashlightOn.Invoke();

        if (_activeBatteryDuration > 0)
        {
            if (_batteryRoutine != null) StopCoroutine(_batteryRoutine);
            _batteryRoutine = StartCoroutine(BatteryDrainRoutine());
        }
    }

    public void TurnOff()
    {
        if (!_isOn) return;
        _isOn = false;
        if (flashlightLight != null) flashlightLight.enabled = false;

        StopOverheatWarning();
        if (_batteryRoutine  != null) { StopCoroutine(_batteryRoutine);  _batteryRoutine  = null; }
        if (_flickerRoutine  != null) { StopCoroutine(_flickerRoutine);  _flickerRoutine  = null; }

        onFlashlightOff.Invoke();
    }

    public void RechargeBattery(float amount)
    {
        if (!_hasFlashlight) return;
        _batteryRemaining = _activeBatteryDuration > 0
            ? Mathf.Min(_batteryRemaining + amount, _activeBatteryDuration)
            : float.MaxValue;
        onBatteryChanged.Invoke(BatteryPercent);
    }

    public void SetBattery(float remaining)
    {
        _batteryRemaining = _activeBatteryDuration > 0
            ? Mathf.Clamp(remaining, 0f, _activeBatteryDuration)
            : remaining;
        onBatteryChanged.Invoke(BatteryPercent);
    }

    // ──────────────────────────────────────────
    // Overheat
    // ──────────────────────────────────────────

    private void TriggerOverheat()
    {
        _fHeld      = false;
        _holdTime   = 0f;
        _isOverheated = true;

        TurnOff();
        onOverheatProgress.Invoke(1f);
        onOverheatStart.Invoke();

        Debug.Log("[Flashlight] OVERHEAT!");

        if (_cooldownRoutine != null) StopCoroutine(_cooldownRoutine);
        _cooldownRoutine = StartCoroutine(CooldownRoutine());
    }

    private IEnumerator CooldownRoutine()
    {
        _overheatTimeRemaining = overheatCooldown;
        while (_overheatTimeRemaining > 0f)
        {
            _overheatTimeRemaining -= Time.deltaTime;
            yield return null;
        }
        _overheatTimeRemaining = 0f;
        _isOverheated = false;
        onOverheatProgress.Invoke(0f);
        onOverheatEnd.Invoke();
        Debug.Log("[Flashlight] Cooldown selesai.");
    }

    private IEnumerator OverheatWarningFlicker()
    {
        while (_isOn && _fHeld && flashlightLight != null)
        {
            // Makin dekat overheat, makin cepat flicker
            float t        = Mathf.Clamp01((_holdTime - (overheatThreshold - overheatWarningAt)) / overheatWarningAt);
            float interval = Mathf.Lerp(flickerMaxInterval, overheatFlickerSpeed, t);

            yield return new WaitForSeconds(interval);
            if (!_isOn || !_fHeld) break;

            flashlightLight.intensity = Random.Range(flickerMinIntensity, _baseIntensity);
            yield return new WaitForSeconds(interval * 0.5f);
            if (_isOn) flashlightLight.intensity = _baseIntensity;
        }
        _flickerRoutine = null;
    }

    private void StopOverheatWarning()
    {
        if (_flickerRoutine != null) { StopCoroutine(_flickerRoutine); _flickerRoutine = null; }
        if (flashlightLight != null) flashlightLight.intensity = _baseIntensity;
    }

    // ──────────────────────────────────────────
    // Recharge (lama, pakai progress)
    // ──────────────────────────────────────────

    private void StartRecharge(PlayerBatteryInventory inv)
    {
        if (_rechargeRoutine != null) StopCoroutine(_rechargeRoutine);
        _rechargeRoutine = StartCoroutine(RechargeRoutine(inv));
    }

    private IEnumerator RechargeRoutine(PlayerBatteryInventory inv)
    {
        _isRecharging = true;
        Debug.Log("[Flashlight] Mulai mengisi battery...");

        float elapsed = 0f;
        while (elapsed < rechargeDuration)
        {
            elapsed += Time.deltaTime;
            onRechargeProgress.Invoke(Mathf.Clamp01(elapsed / rechargeDuration));
            yield return null;
        }

        // Selesai — ambil 1 battery dari inventory
        float amount = inv.UseBattery();
        if (amount > 0) RechargeBattery(amount);

        _isRecharging = false;
        onRechargeProgress.Invoke(0f);
        onRechargeComplete.Invoke();

        Debug.Log($"[Flashlight] Battery terisi +{amount}s");
    }

    // ──────────────────────────────────────────
    // Battery Drain
    // ──────────────────────────────────────────

    private IEnumerator BatteryDrainRoutine()
    {
        while (_isOn && _batteryRemaining > 0)
        {
            // Drain per frame — smooth, tidak loncat
            _batteryRemaining -= Time.deltaTime;
            _batteryRemaining  = Mathf.Max(0f, _batteryRemaining);
            onBatteryChanged.Invoke(BatteryPercent);

            // Cek flicker battery lemah
            bool shouldFlicker = BatteryPercent <= _activeFlickerThreshold && !_fHeld;
            if (shouldFlicker && _flickerRoutine == null)
                _flickerRoutine = StartCoroutine(BatteryLowFlicker());

            if (_batteryRemaining <= 0)
            {
                TurnOff();
                _fHeld = false;
                onBatteryDepleted.Invoke();
                Debug.Log("[Flashlight] Battery habis!");
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator BatteryLowFlicker()
    {
        while (_isOn && flashlightLight != null && BatteryPercent <= _activeFlickerThreshold)
        {
            float interval = Random.Range(flickerMinInterval, flickerMaxInterval);
            yield return new WaitForSeconds(interval);
            if (!_isOn) break;

            flashlightLight.intensity = Random.Range(flickerMinIntensity, _baseIntensity);
            yield return new WaitForSeconds(Random.Range(0.02f, 0.08f));
            if (_isOn) flashlightLight.intensity = _baseIntensity;
        }
        _flickerRoutine = null;
    }

    // ──────────────────────────────────────────
    // Save / Load
    // ──────────────────────────────────────────

    /// Dipanggil oleh GameSave.Save() via callback sebelum ForceWrite().
    /// Menyimpan seluruh state senter ke SaveFile.Data.
    public void PersistFlashlight()
    {
        var d = SaveFile.Data;
        d.flashlightBattery           = _batteryRemaining;
        d.flashlightOverheat          = _isOverheated;
        d.flashlightOverheatRemaining = _overheatTimeRemaining;
        d.flashlightBroken            = _isBroken;
        d.flashlightBrokenRemaining   = _brokenTimeRemaining;
        SaveFile.MarkDirty();
    }

    /// Dipanggil oleh OnFlashlightEquipped() setelah setup battery default,
    /// lalu override dengan nilai dari save jika ada.
    private void LoadFromSave()
    {
        var d = SaveFile.Data;

        // Tidak ada save flashlight — pakai default dari item
        if (d.flashlightBattery < 0f) return;

        // Restore battery
        _batteryRemaining = d.flashlightBattery;
        onBatteryChanged.Invoke(BatteryPercent);

        // Restore overheat
        if (d.flashlightOverheat && d.flashlightOverheatRemaining > 0f)
        {
            _isOverheated          = true;
            _overheatTimeRemaining = d.flashlightOverheatRemaining;
            onOverheatStart.Invoke();
            if (_cooldownRoutine != null) StopCoroutine(_cooldownRoutine);
            _cooldownRoutine = StartCoroutine(CooldownRoutine());
            Debug.Log($"[Flashlight] Restore overheat dari save — sisa {d.flashlightOverheatRemaining:F1}s");
        }

        // Restore broken
        if (d.flashlightBroken && d.flashlightBrokenRemaining > 0f)
        {
            _isBroken            = true;
            _brokenTimeRemaining = d.flashlightBrokenRemaining;
            onBrokenStart.Invoke(d.flashlightBrokenRemaining);
            if (_brokenRoutine != null) StopCoroutine(_brokenRoutine);
            _brokenRoutine = StartCoroutine(BrokenRoutine());
            Debug.Log($"[Flashlight] Restore broken dari save — sisa {d.flashlightBrokenRemaining:F1}s");
        }

        Debug.Log($"[Flashlight] State di-restore dari save — battery: {_batteryRemaining:F1}s");
    }

    // ──────────────────────────────────────────
    // Equipment Callbacks
    // ──────────────────────────────────────────

    private void OnFlashlightEquipped(FlashlightItem item)
    {
        _hasFlashlight = true;

        float duration  = batteryDurationOverride;
        float threshold = flickerThresholdOverride;

        if (!useOverride && item != null)
        {
            duration  = item.batteryDuration;
            threshold = item.flickerThreshold;
        }

        _activeFlickerThreshold = threshold;
        _batteryRemaining       = duration > 0 ? duration : float.MaxValue;
        _activeBatteryDuration  = duration;

        if (flashlightLight != null)
            _baseIntensity = flashlightLight.intensity;

        // Setelah setup default, override dengan nilai dari save jika ada.
        // LoadFromSave() hanya override jika flashlightBattery >= 0 di SaveFile.
        // Ini aman karena FlashlightPickup.OnInteract() memanggil RestoreState()
        // sesudahnya untuk kasus drop/pickup in-session (lebih spesifik dari save).
        LoadFromSave();
    }

    // ──────────────────────────────────────────
    // Broken (Spam)
    // ──────────────────────────────────────────

    private void TriggerBroken()
    {
        _isBroken       = true;
        _spamPressCount = 0;
        _spamWindowTimer = 0f;
        _fHeld          = false;
        _holdTime       = 0f;

        TurnOff();
        onBrokenStart.Invoke(spamBrokenDuration);
        Debug.Log($"[Flashlight] RUSAK akibat spam! Nunggu {spamBrokenDuration}s");

        if (_brokenRoutine != null) StopCoroutine(_brokenRoutine);
        _brokenRoutine = StartCoroutine(BrokenRoutine());
    }

    private IEnumerator BrokenRoutine()
    {
        _brokenTimeRemaining = spamBrokenDuration;
        while (_brokenTimeRemaining > 0f)
        {
            _brokenTimeRemaining -= Time.deltaTime;
            yield return null;
        }
        _brokenTimeRemaining = 0f;
        _isBroken = false;
        onBrokenEnd.Invoke();
        Debug.Log("[Flashlight] Senter sudah bisa dipakai lagi.");
    }

    private void OnFlashlightUnequipped()
    {
        TurnOff();
        _hasFlashlight   = false;
        _fHeld           = false;
        _holdTime        = 0f;
        _isRecharging    = false;
        _spamPressCount  = 0;
        _spamWindowTimer = 0f;

        // Stop coroutine tapi TIDAK reset state overheat/broken
        // State disimpan di _overheatTimeRemaining dan _brokenTimeRemaining
        // dan akan di-restore via RestoreState() saat pickup lagi
        if (_cooldownRoutine != null) { StopCoroutine(_cooldownRoutine); _cooldownRoutine = null; }
        if (_rechargeRoutine != null) { StopCoroutine(_rechargeRoutine); _rechargeRoutine = null; }
        if (_brokenRoutine   != null) { StopCoroutine(_brokenRoutine);   _brokenRoutine   = null; }

        // Reset flag tapi waktu tersisa tetap tersimpan
        _isOverheated = false;
        _isBroken     = false;
    }
}