using UnityEngine;
using UnityEngine.Events;

public class NPCInteractable : MonoBehaviour, IInteractable
{
    [Header("Dialogue")]
    [SerializeField] private DialogueData dialogueData;

    [Header("Interact Settings")]
    [SerializeField] private string promptText  = "Tahan [E] untuk bicara";
    [SerializeField] private bool   canInteract = true;

    [Header("Events")]
    public UnityEvent          onInteractBegin;        // saat player mulai hold E
    public UnityEvent          onDialogueStarted;      // saat dialogue mulai
    public UnityEvent<string>  onNodeShown;            // (teks) tiap node tampil
    public UnityEvent<int>     onChoiceSelected;       // (index) saat choice dipilih
    public UnityEvent          onDialogueEnded;        // saat dialogue selesai

    public string PromptText  => promptText;
    public bool   CanInteract => canInteract && !DialogueManager.Instance.IsActive;

    private bool _isMyDialogue;

    private void Start()
    {
        var dm = DialogueManager.Instance;
        if (dm == null) return;
        dm.onDialogueStart.AddListener(OnDMStart);
        dm.onNodeShow.AddListener(OnDMNodeShow);
        dm.onDialogueEnd.AddListener(OnDMEnd);
    }

    private void OnDestroy()
    {
        var dm = DialogueManager.Instance;
        if (dm == null) return;
        dm.onDialogueStart.RemoveListener(OnDMStart);
        dm.onNodeShow.RemoveListener(OnDMNodeShow);
        dm.onDialogueEnd.RemoveListener(OnDMEnd);
    }

    public void OnInteract(GameObject interactor)
    {
        if (DialogueManager.Instance == null)
        {
            Debug.LogWarning("[NPCInteractable] DialogueManager tidak ditemukan di scene!");
            return;
        }

        onInteractBegin.Invoke();
        DialogueManager.Instance.StartDialogue(dialogueData);
    }

    // dipanggil dari DialogueUI saat choice dipilih
    public void NotifyChoiceSelected(int index) => onChoiceSelected.Invoke(index);

    private void OnDMStart(string npcName, string firstText)
    {
        if (DialogueManager.Instance.IsNarrator) return;
        if (npcName != dialogueData.npcName) return;
        _isMyDialogue = true;
        onDialogueStarted.Invoke();
    }

    private void OnDMNodeShow(string npcName, string text)
    {
        if (!_isMyDialogue) return;
        onNodeShown.Invoke(text);
    }

    private void OnDMEnd()
    {
        if (!_isMyDialogue) return;
        _isMyDialogue = false;
        onDialogueEnded.Invoke();
    }
}