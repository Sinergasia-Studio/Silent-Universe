using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// QuestUI — tampilkan objective quest di HUD.
///
/// Perubahan dari versi lama:
///   - Hapus singleton _instance — QuestUI tidak perlu DontDestroyOnLoad,
///     cukup hidup di scene yang membutuhkannya
///   - Subscribe ke events di OnEnable/OnDisable bukan Start/OnDestroy
///     agar subscribe selalu fresh dan tidak ada stale listener
///   - RestoreState() pull langsung dari QuestManager.CurrentObjective()
///     tanpa bergantung pada timing event — tidak ada race condition
///   - Tidak lagi DontDestroyOnLoad — UI dibuat ulang tiap scene
/// </summary>
public class QuestUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private CanvasGroup questPanel;
    [SerializeField] private TMP_Text    objectiveText;

    [Header("Warna")]
    [SerializeField] private Color objectiveColor = Color.white;
    [SerializeField] private Color completedColor = new Color(0.6f, 0.6f, 0.6f);

    [Header("Animasi")]
    [SerializeField] private float fadeSpeed         = 4f;
    [SerializeField] private float holdAfterComplete = 1.2f;

    private Coroutine _routine;

    private void Awake()
    {
        if (questPanel == null)    questPanel    = GetComponentInChildren<CanvasGroup>(true);
        if (objectiveText == null) objectiveText = GetComponentInChildren<TMP_Text>(true);
        SetAlpha(0f);
    }

    // FIX: Subscribe di OnEnable/OnDisable bukan Start/OnDestroy
    // Ini memastikan listener selalu fresh dan tidak leak saat GameObject di-disable/enable
    private void OnEnable()
    {
        var qm = QuestManager.Instance;
        if (qm == null) return;
        qm.onStepStarted.AddListener(OnStepStarted);
        qm.onStepCompleted.AddListener(OnStepCompleted);
        qm.onAllQuestsCompleted.AddListener(OnAllQuestsCompleted);

        // FIX: Pull state langsung dari QuestManager saat UI aktif
        // Tidak bergantung pada timing event — tidak ada race condition
        RestoreState();
    }

    private void OnDisable()
    {
        var qm = QuestManager.Instance;
        if (qm == null) return;
        qm.onStepStarted.RemoveListener(OnStepStarted);
        qm.onStepCompleted.RemoveListener(OnStepCompleted);
        qm.onAllQuestsCompleted.RemoveListener(OnAllQuestsCompleted);
    }

    // ── Event Handlers ──

    private void OnStepStarted(string questTitle, string objective)
    {
        Restart(ShowStep(objective));
    }

    private void OnStepCompleted(string objective)
    {
        if (objectiveText == null) return;
        objectiveText.text  = $"<s>{objective}</s>";
        objectiveText.color = completedColor;
    }

    private void OnAllQuestsCompleted() => Restart(ShowAllDone());

    // ── Restore ──

    private void RestoreState()
    {
        var qm = QuestManager.Instance;
        if (qm == null || !qm.IsQuestActive) return;

        // Pull langsung tanpa coroutine — tidak ada timing issue
        string objective = qm.CurrentObjective();
        if (string.IsNullOrEmpty(objective)) return;

        if (objectiveText != null)
        {
            objectiveText.text  = objective;
            objectiveText.color = objectiveColor;
        }
        SetAlpha(1f);
    }

    // ── Coroutines ──

    private IEnumerator ShowStep(string objective)
    {
        if (questPanel != null && questPanel.alpha > 0f)
        {
            yield return Fade(0f);
            yield return new WaitForSeconds(0.15f);
        }

        if (objectiveText != null)
        {
            objectiveText.text  = objective;
            objectiveText.color = objectiveColor;
        }

        yield return Fade(1f);
    }

    private IEnumerator ShowAllDone()
    {
        yield return new WaitForSeconds(holdAfterComplete);
        if (objectiveText != null) objectiveText.text = "SEMUA MISI SELESAI";
        yield return new WaitForSeconds(holdAfterComplete);
        yield return Fade(0f);
    }

    private IEnumerator Fade(float target)
    {
        if (questPanel == null) yield break;
        while (!Mathf.Approximately(questPanel.alpha, target))
        {
            questPanel.alpha = Mathf.MoveTowards(questPanel.alpha, target, fadeSpeed * Time.deltaTime);
            yield return null;
        }
        questPanel.alpha = target;
    }

    private void Restart(IEnumerator routine)
    {
        if (!gameObject.activeInHierarchy) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(routine);
    }

    private void SetAlpha(float alpha)
    {
        if (questPanel == null) return;
        questPanel.alpha          = alpha;
        questPanel.interactable   = false;
        questPanel.blocksRaycasts = false;
    }
}