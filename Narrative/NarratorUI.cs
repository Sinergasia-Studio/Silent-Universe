using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using TMPro;

public class NarratorUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject narratorPanel;

    [Header("Text")]
    [SerializeField] private TMP_Text narratorText;

    [Header("Typewriter")]
    [SerializeField] private float charDelay = 0.03f;

    [Header("Skip")]
    [SerializeField] private Key skipKey = Key.Space;

    [Header("Events")]
    public UnityEvent          onPanelOpened;          // saat panel pertama kali tampil
    public UnityEvent          onPanelClosed;          // saat panel disembunyikan
    public UnityEvent<int>     onNodeStarted;          // (index) saat node mulai diketik
    public UnityEvent<int>     onNodeFinished;         // (index) saat typewriter selesai
    public UnityEvent<int>     onNodeSkipped;          // (index) saat node di-skip
    public UnityEvent          onLastNodeReached;      // saat node terakhir tampil
    public UnityEvent          onNarratorCompleted;    // saat semua node selesai & panel tutup

    // ── state ──
    private Coroutine    _typeRoutine;
    private bool         _isTyping;
    private string       _currentFullText;

    // track node index secara lokal
    private DialogueData _activeData;
    private int          _nodeIndex;

    // ── lifecycle ──
    private void Start()
    {
        var dm = DialogueManager.Instance;
        if (dm == null) { Debug.LogError("[NarratorUI] DialogueManager tidak ditemukan!"); return; }

        dm.onDialogueStart.AddListener(OnDialogueStart);
        dm.onDialogueEnd.AddListener(OnDialogueEnd);

        HidePanel();
        AddPanelClickHandler();
    }

    private void OnDestroy()
    {
        var dm = DialogueManager.Instance;
        if (dm == null) return;

        dm.onDialogueStart.RemoveListener(OnDialogueStart);
        dm.onDialogueEnd.RemoveListener(OnDialogueEnd);
    }

    private void Update()
    {
        if (!narratorPanel.activeSelf) return;
        if (Keyboard.current != null && Keyboard.current[skipKey].wasPressedThisFrame)
            HandleInput();
    }

    // ── DialogueManager callbacks ──
    private void OnDialogueStart(string npcName, string firstText)
    {
        if (!DialogueManager.Instance.IsNarrator) return;

        _activeData = GetActiveDialogueData();
        _nodeIndex  = 0;

        if (_activeData == null) return;

        narratorPanel.SetActive(true);
        onPanelOpened.Invoke();
        PlayNode(_nodeIndex);
    }

    private void OnDialogueEnd()
    {
        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _isTyping   = false;
        _activeData = null;
        HidePanel();
        onNarratorCompleted.Invoke();
    }

    // ── input ──
    private void HandleInput()
    {
        if (_isTyping)
            SkipTypewriter();
        else
            AdvanceNode();
    }

    // ── node navigation (self-managed) ──
    private void PlayNode(int index)
    {
        if (_activeData == null || index >= _activeData.nodes.Length)
        {
            DialogueManager.Instance?.EndDialogue();
            return;
        }

        if (index == _activeData.nodes.Length - 1)
            onLastNodeReached.Invoke();

        onNodeStarted.Invoke(index);
        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _typeRoutine = StartCoroutine(TypewriterRoutine(_activeData.nodes[index].npcText));
    }

    private void AdvanceNode()
    {
        _nodeIndex++;
        if (_activeData == null || _nodeIndex >= _activeData.nodes.Length)
            DialogueManager.Instance?.EndDialogue();
        else
            PlayNode(_nodeIndex);
    }

    // ── typewriter ──
    private IEnumerator TypewriterRoutine(string fullText)
    {
        _isTyping         = true;
        _currentFullText  = fullText;
        narratorText.text = string.Empty;

        foreach (char c in fullText)
        {
            narratorText.text += c;
            yield return new WaitForSeconds(charDelay);
        }

        _isTyping = false;
        onNodeFinished.Invoke(_nodeIndex);
    }

    private void SkipTypewriter()
    {
        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _typeRoutine      = null;
        _isTyping         = false;
        narratorText.text = _currentFullText;
        onNodeSkipped.Invoke(_nodeIndex);
    }

    private void HidePanel()
    {
        if (narratorPanel != null) narratorPanel.SetActive(false);
        onPanelClosed.Invoke();
    }

    // ambil DialogueData aktif dari NarratorDialogueTrigger yang baru trigger
    private DialogueData GetActiveDialogueData()
    {
        // cari semua trigger di scene, ambil yang datanya sama dengan yang sedang jalan
        var triggers = FindObjectsByType<NarratorDialogueTrigger>(FindObjectsSortMode.None);
        foreach (var t in triggers)
        {
            var data = t.GetCurrentData();
            if (data != null && data.isNarrator) return data;
        }
        return null;
    }

    private void AddPanelClickHandler()
    {
        if (narratorPanel == null) return;

        var trigger = narratorPanel.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (trigger == null)
            trigger = narratorPanel.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        var entry = new UnityEngine.EventSystems.EventTrigger.Entry
            { eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick };
        entry.callback.AddListener(_ => HandleInput());
        trigger.triggers.Add(entry);
    }
}