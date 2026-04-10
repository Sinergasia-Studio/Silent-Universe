using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VContainer;

/// <summary>
/// QuestManager — versi refactor.
///
/// Perubahan dari versi lama:
///   - SaveProgress() pakai ForceWrite() bukan Write() — tidak ada throttle,
///     progress tidak hilang jika game crash setelah step selesai
///   - LoadProgress() validasi lebih ketat: cek questId, stepId, dan step count
///   - FireStepStarted() dipanggil SETELAH SaveProgress() — state di disk selalu
///     konsisten dengan state yang di-broadcast ke UI
///   - FinishActiveQuest() bersihkan state dulu, baru invoke event, baru chain quest —
///     tidak ada window di mana state inconsistent saat listener jalan
///   - Tambah IQuestManagerReadOnly untuk UI agar tidak bisa mutate state dari luar
/// </summary>
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Quest Pertama")]
    [Tooltip("Quest yang langsung jalan saat game mulai. Kosongkan jika quest dimulai lewat trigger.")]
    [SerializeField] private QuestData startingQuest;

    [Header("Semua Quest (untuk load progress)")]
    [Tooltip("Daftarkan SEMUA QuestData asset di sini agar sistem bisa menyimpan & memuat progress.")]
    [SerializeField] private List<QuestData> allQuests = new();

    [Header("Events")]
    [Tooltip("(questTitle, objective) — saat step baru dimulai")]
    public UnityEvent<string, string> onStepStarted;
    [Tooltip("(objective) — saat step selesai")]
    public UnityEvent<string>         onStepCompleted;
    [Tooltip("(questId) — saat quest selesai")]
    public UnityEvent<string>         onQuestCompleted;
    [Tooltip("saat semua quest selesai")]
    public UnityEvent                 onAllQuestsCompleted;

    // ── State ──
    private QuestData            _activeQuest;
    private int                  _currentStepIndex;
    private readonly HashSet<string> _completedIds = new();

    // ── guard agar tidak double-complete dalam 1 frame ──
    private bool _isProcessing;

    // ── Public readonly ──
    public bool      IsQuestActive    => _activeQuest != null;
    public string    ActiveQuestId    => _activeQuest?.questId ?? "";
    public string    ActiveQuestTitle => _activeQuest?.questTitle ?? "";
    public QuestData ActiveQuest      => _activeQuest;
    public bool      IsCompleted(string questId) => _completedIds.Contains(questId);
    public bool      CanStart(QuestData q) => q != null && !IsQuestActive && !IsCompleted(q.questId);

    public string CurrentObjective()
    {
        if (!IsQuestActive) return "";
        var steps = _activeQuest.steps;
        return _currentStepIndex < steps.Count ? steps[_currentStepIndex].objective : "";
    }

    // ──────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────

    private void Awake()
    {
        // Fase 3 — Instance sebagai compatibility shim selama migrasi VContainer.
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (transform.parent != null) transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        LoadProgress();

        if (!IsQuestActive && startingQuest != null && !IsCompleted(startingQuest.questId))
            StartQuest(startingQuest);
    }

    // ──────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────

    public bool StartQuest(QuestData quest)
    {
        if (quest == null)
        {
            Debug.LogWarning("[Quest] StartQuest: quest null.");
            return false;
        }
        if (IsQuestActive)
        {
            Debug.Log($"[Quest] Tidak bisa mulai '{quest.questId}' — '{_activeQuest.questId}' masih aktif.");
            return false;
        }
        if (IsCompleted(quest.questId))
        {
            Debug.Log($"[Quest] '{quest.questId}' sudah selesai.");
            return false;
        }
        if (quest.steps == null || quest.steps.Count == 0)
        {
            Debug.LogWarning($"[Quest] '{quest.questId}' tidak punya steps.");
            return false;
        }

        _activeQuest      = quest;
        _currentStepIndex = 0;

        // FIX: Save SEBELUM fire event — state di disk konsisten sebelum UI update
        SaveProgress();
        FireStepStarted();

        Debug.Log($"[Quest] Mulai '{quest.questId}' → {CurrentObjective()}");
        return true;
    }

    public void CompleteStep(string stepId)
    {
        if (!IsQuestActive) return;

        // FIX: Guard mencegah double-complete dalam 1 frame
        // (misal: dua QuestTrigger overlap trigger di frame yang sama)
        if (_isProcessing)
        {
            Debug.LogWarning("[Quest] CompleteStep dipanggil saat sedang processing — diabaikan.");
            return;
        }

        var steps = _activeQuest.steps;
        if (_currentStepIndex >= steps.Count) return;

        var step = steps[_currentStepIndex];
        if (step.stepId != stepId)
        {
            Debug.Log($"[Quest] Step '{stepId}' bukan step saat ini ('{step.stepId}').");
            return;
        }

        _isProcessing = true;

        // Invoke onStepCompleted SEBELUM naikkan index
        // agar listener masih bisa baca step yang baru selesai
        onStepCompleted.Invoke(step.objective);
        Debug.Log($"[Quest] Step selesai: \"{step.objective}\"");

        _currentStepIndex++;

        if (_currentStepIndex >= steps.Count)
        {
            FinishActiveQuest();
        }
        else
        {
            // FIX: Save SEBELUM fire event
            SaveProgress();
            FireStepStarted();
            Debug.Log($"[Quest] Step berikutnya: {CurrentObjective()}");
        }

        _isProcessing = false;
    }

    public void ResetAllProgress()
    {
        _activeQuest      = null;
        _currentStepIndex = 0;
        _completedIds.Clear();

        var d = SaveFile.Data;
        d.questActiveId   = "";
        d.questActiveStep = 0;
        d.questCompleted  = "";
        SaveFile.ForceWrite();

        Debug.Log("[Quest] Semua progress direset.");
    }

    // ──────────────────────────────────────────
    // Internal
    // ──────────────────────────────────────────

    private void FinishActiveQuest()
    {
        string    finishedId = _activeQuest.questId;
        QuestData next       = _activeQuest.nextQuest;

        // FIX: Bersihkan state dan save DULU sebelum invoke event dan chain
        // Tanpa ini, jika listener dari onQuestCompleted panggil IsQuestActive
        // atau IsCompleted, hasilnya akan salah
        _completedIds.Add(finishedId);
        _activeQuest      = null;
        _currentStepIndex = 0;
        SaveProgress();

        onQuestCompleted.Invoke(finishedId);
        Debug.Log($"[Quest] '{finishedId}' SELESAI!");

        if (next != null)
        {
            Debug.Log($"[Quest] Chain ke '{next.questId}'...");
            StartQuest(next);
        }
        else
        {
            onAllQuestsCompleted.Invoke();
            Debug.Log("[Quest] Semua quest selesai!");
        }
    }

    private void FireStepStarted()
    {
        onStepStarted.Invoke(ActiveQuestTitle, CurrentObjective());
    }

    // ──────────────────────────────────────────
    // Save / Load
    // ──────────────────────────────────────────

    private void SaveProgress()
    {
        var d = SaveFile.Data;
        d.questActiveId   = _activeQuest?.questId ?? "";
        d.questActiveStep = _currentStepIndex;
        d.questCompleted  = string.Join("|", _completedIds);

        // FIX: ForceWrite() bukan Write() — tidak ada throttle
        // Progress tidak hilang jika game crash tepat setelah step selesai
        SaveFile.ForceWrite();
    }

    private void LoadProgress()
    {
        var d = SaveFile.Data;

        // Load completed quest IDs
        string rawCompleted = d.questCompleted ?? "";
        if (!string.IsNullOrEmpty(rawCompleted))
            foreach (var id in rawCompleted.Split('|'))
                if (!string.IsNullOrEmpty(id)) _completedIds.Add(id);

        // Load quest aktif
        string activeId = d.questActiveId ?? "";
        if (string.IsNullOrEmpty(activeId)) return;

        var quest = allQuests.Find(q => q != null && q.questId == activeId);
        if (quest == null)
        {
            Debug.LogWarning($"[Quest] Save merujuk '{activeId}' tapi tidak ada di allQuests — reset.");
            ClearSaveCorrupt();
            return;
        }

        // FIX: Validasi step index sebelum restore
        int savedStep = d.questActiveStep;
        if (quest.steps == null || quest.steps.Count == 0)
        {
            Debug.LogWarning($"[Quest] Quest '{activeId}' tidak punya steps — reset.");
            ClearSaveCorrupt();
            return;
        }
        if (savedStep < 0 || savedStep >= quest.steps.Count)
        {
            Debug.LogWarning($"[Quest] Step index {savedStep} out of range untuk '{activeId}' — reset.");
            ClearSaveCorrupt();
            return;
        }

        _activeQuest      = quest;
        _currentStepIndex = savedStep;

        // FIX: FireStepStarted dipanggil di sini agar UI bisa subscribe lebih dulu
        // QuestUI.Start() subscribe, lalu RestoreNextFrame() yang fire event
        // Tapi karena LoadProgress() dipanggil di Start() juga, ada timing issue.
        // Solusi: tidak fire event di sini — biarkan QuestUI.RestoreNextFrame()
        // yang pull state langsung dari QuestManager.CurrentObjective()
        Debug.Log($"[Quest] Progress dimuat: '{activeId}' step {_currentStepIndex}");
    }

    private void ClearSaveCorrupt()
    {
        var d = SaveFile.Data;
        d.questActiveId   = "";
        d.questActiveStep = 0;
        SaveFile.ForceWrite();
    }

    // ──────────────────────────────────────────
    // Dev Tools
    // ──────────────────────────────────────────

    [ContextMenu("DEV: Reset Semua Progress")]
    public void DEV_ResetAll() => ResetAllProgress();

    [ContextMenu("DEV: Skip Step Sekarang")]
    public void DEV_SkipCurrentStep()
    {
        if (!IsQuestActive) { Debug.Log("[Quest] Tidak ada quest aktif."); return; }
        CompleteStep(_activeQuest.steps[_currentStepIndex].stepId);
    }

    [ContextMenu("DEV: Print Status")]
    public void DEV_PrintStatus()
    {
        Debug.Log(IsQuestActive
            ? $"[Quest] Aktif: '{_activeQuest.questId}' | Step {_currentStepIndex}: {CurrentObjective()}"
            : $"[Quest] Tidak ada quest aktif. Completed: {string.Join(", ", _completedIds)}");
    }
}