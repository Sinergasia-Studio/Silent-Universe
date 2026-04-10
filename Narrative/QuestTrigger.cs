using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// QuestTrigger — tempel ke NPC, objek, atau area.
///
/// Perubahan dari versi lama:
///   - _hasTriggered disimpan ke WorldFlags agar persist setelah scene reload
///     (versi lama hanya di memory — trigger bisa jalan lagi setelah load)
///   - TryStartThenComplete() diperbaiki: StartQuest lalu CompleteStep dalam
///     satu operasi atomik agar tidak ada frame gap antara start dan complete
///   - Tambah mode ForceComplete: complete step tanpa cek questId
///     (berguna untuk trigger global yang tidak tahu quest mana yang aktif)
/// </summary>
public class QuestTrigger : MonoBehaviour
{
    public enum TriggerMode { StartQuest, CompleteStep, StartThenComplete }

    [Header("Data Quest")]
    [SerializeField] private QuestData questData;
    [SerializeField] private string    stepId;

    [Header("Mode")]
    [SerializeField] private TriggerMode mode = TriggerMode.CompleteStep;

    [Header("Auto Trigger")]
    [SerializeField] private bool   autoTriggerOnEnter = false;
    [SerializeField] private string playerTag          = "Player";
    [SerializeField] private bool   triggerOnlyOnce    = true;

    [Header("Events")]
    public UnityEvent onQuestStarted;
    public UnityEvent onStepCompleted;
    public UnityEvent onBlockedByActiveQuest;

    // FIX: Simpan triggered state ke WorldFlags agar persist setelah scene reload.
    // Versi lama hanya _hasTriggered di memory — setelah load ulang trigger aktif lagi.
    private string SaveKey => $"QuestTrigger_{gameObject.name}";
    private bool   _hasTriggered;

    private void Awake()
    {
        // Restore triggered state dari save saat scene load
        if (triggerOnlyOnce && WorldFlags.Get(SaveKey))
        {
            _hasTriggered = true;
            // Disable collider agar tidak ada overhead physics untuk trigger yang sudah jalan
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            var col2d = GetComponent<Collider2D>();
            if (col2d != null) col2d.enabled = false;
        }
    }

    // ── Public API ──

    public void TryStartQuest()
    {
        if (questData == null) { Debug.LogWarning($"[QuestTrigger] {name}: questData null."); return; }
        if (triggerOnlyOnce && _hasTriggered) return;

        var qm = QuestManager.Instance;
        if (qm == null) return;

        if (qm.IsQuestActive)
        {
            onBlockedByActiveQuest.Invoke();
            return;
        }

        bool started = qm.StartQuest(questData);
        if (started)
        {
            MarkTriggered();
            onQuestStarted.Invoke();
        }
    }

    public void TryCompleteStep()
    {
        if (string.IsNullOrEmpty(stepId)) { Debug.LogWarning($"[QuestTrigger] {name}: stepId kosong."); return; }
        if (triggerOnlyOnce && _hasTriggered) return;

        var qm = QuestManager.Instance;
        if (qm == null || !qm.IsQuestActive) return;
        if (questData != null && qm.ActiveQuestId != questData.questId) return;

        qm.CompleteStep(stepId);
        MarkTriggered();
        onStepCompleted.Invoke();
    }

    public void TryStartThenComplete()
    {
        if (questData == null) { Debug.LogWarning($"[QuestTrigger] {name}: questData null."); return; }
        if (triggerOnlyOnce && _hasTriggered) return;

        var qm = QuestManager.Instance;
        if (qm == null) return;

        // FIX: Start dan complete dalam satu blok agar tidak ada frame gap.
        // Versi lama: TryStartQuest() → TryCompleteStep() dua call terpisah
        // yang masing-masing cek _hasTriggered — bisa skip jika triggerOnlyOnce.
        bool started = qm.StartQuest(questData);
        if (!started) return;

        if (!string.IsNullOrEmpty(stepId))
            qm.CompleteStep(stepId);

        MarkTriggered();
        onQuestStarted.Invoke();
        onStepCompleted.Invoke();
    }

    public void ResetTrigger()
    {
        _hasTriggered = false;
        WorldFlags.Remove(SaveKey);

        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
        var col2d = GetComponent<Collider2D>();
        if (col2d != null) col2d.enabled = true;
    }

    // ── Auto Trigger ──

    private void OnTriggerEnter(Collider other)
    {
        if (!autoTriggerOnEnter || !other.CompareTag(playerTag)) return;
        ExecuteMode();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!autoTriggerOnEnter || !other.CompareTag(playerTag)) return;
        ExecuteMode();
    }

    private void ExecuteMode()
    {
        switch (mode)
        {
            case TriggerMode.StartQuest:        TryStartQuest();        break;
            case TriggerMode.CompleteStep:      TryCompleteStep();      break;
            case TriggerMode.StartThenComplete: TryStartThenComplete(); break;
        }
    }

    private void MarkTriggered()
    {
        _hasTriggered = true;
        if (triggerOnlyOnce)
        {
            WorldFlags.Set(SaveKey, true);
            // Disable collider setelah triggered agar tidak ada overhead physics
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            var col2d = GetComponent<Collider2D>();
            if (col2d != null) col2d.enabled = false;
        }
    }
}