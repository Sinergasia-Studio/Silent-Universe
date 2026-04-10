using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// NarratorDialogueTrigger — pasang pada GameObject dengan Collider (Is Trigger = true).
/// Saat player masuk trigger, dialogue narator otomatis dimulai.
/// </summary>
public class NarratorDialogueTrigger : MonoBehaviour
{
    [Header("Dialogue")]
    [SerializeField] private DialogueData narratorData;

    [Header("Settings")]
    [SerializeField] private string playerTag    = "Player";
    [Tooltip("Hanya trigger sekali. Matikan untuk bisa trigger berkali-kali.")]
    [SerializeField] private bool   triggerOnce  = true;

    [Header("Events")]
    public UnityEvent onPlayerEntered;      // saat player masuk collider
    public UnityEvent onNarratorStarted;    // saat narrator mulai
    public UnityEvent onNarratorEnded;      // saat narrator selesai
    public UnityEvent onPlayerExited;       // saat player keluar collider

    private bool _hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        onPlayerEntered.Invoke();

        if (triggerOnce && _hasTriggered)  return;
        if (DialogueManager.Instance == null)
        {
            Debug.LogWarning("[NarratorTrigger] DialogueManager tidak ditemukan!");
            return;
        }
        if (DialogueManager.Instance.IsActive) return;

        _hasTriggered = true;
        onNarratorStarted.Invoke();

        DialogueManager.Instance.onDialogueEnd.AddListener(OnNarratorEnd);
        DialogueManager.Instance.StartDialogue(narratorData);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        onPlayerExited.Invoke();
    }

    private void OnNarratorEnd()
    {
        onNarratorEnded.Invoke();
        DialogueManager.Instance?.onDialogueEnd.RemoveListener(OnNarratorEnd);
    }

    public void ResetTrigger() => _hasTriggered = false;

    public DialogueData GetCurrentData() => _hasTriggered ? narratorData : null;
}