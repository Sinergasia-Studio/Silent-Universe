using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

public class DialogueUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject dialoguePanel;

    [Header("Text")]
    [SerializeField] private TMP_Text npcNameText;
    [SerializeField] private TMP_Text npcDialogueText;

    [Header("Choices")]
    [SerializeField] private Transform  choicesContainer;
    [SerializeField] private GameObject choiceButtonPrefab;

    [Header("Continue Button")]
    [Tooltip("Tampil saat tidak ada choices — klik untuk tutup dialogue")]
    [SerializeField] private Button continueButton;

    [Header("Typewriter")]
    [SerializeField] private float charDelay = 0.04f;

    [Header("Skip")]
    [Tooltip("Tekan tombol ini untuk skip typewriter / lanjut dialogue")]
    [SerializeField] private Key skipKey = Key.Space;

    [Header("Events")]
    public UnityEvent          onPanelOpened;        // saat dialogue panel pertama tampil
    public UnityEvent          onPanelClosed;        // saat dialogue panel ditutup
    public UnityEvent<string>  onTypewriterStarted;  // (teks) saat mulai mengetik
    public UnityEvent          onTypewriterFinished;  // saat typewriter selesai
    public UnityEvent          onTypewriterSkipped;   // saat typewriter di-skip
    public UnityEvent          onChoicesShown;        // saat pilihan tampil
    public UnityEvent<int>     onChoiceClicked;       // (index) saat choice diklik
    public UnityEvent          onContinueClicked;     // saat continue button diklik

    // ── state ──
    private readonly List<GameObject> _choiceInstances = new();
    private DialogueChoice[]          _pendingChoices;
    private Coroutine                 _typeRoutine;
    private bool                      _isTyping;
    private string                    _currentFullText;
    private bool                      _panelWasOpen;

    // ── lifecycle ──
    private void Start()
    {
        var dm = DialogueManager.Instance;
        if (dm == null)
        {
            Debug.LogError("[DialogueUI] DialogueManager tidak ditemukan!");
            return;
        }

        dm.onNodeShow.AddListener(ShowNode);
        dm.onChoicesShow.AddListener(ShowChoices);
        dm.onDialogueEnd.AddListener(HidePanel);

        HidePanel();

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);

        // pasang click handler pada dialoguePanel
        AddPanelClickHandler();
    }

    private void OnDestroy()
    {
        var dm = DialogueManager.Instance;
        if (dm == null) return;

        dm.onNodeShow.RemoveListener(ShowNode);
        dm.onChoicesShow.RemoveListener(ShowChoices);
        dm.onDialogueEnd.RemoveListener(HidePanel);
    }

    private void Update()
    {
        if (!dialoguePanel.activeSelf) return;

        if (Keyboard.current != null && Keyboard.current[skipKey].wasPressedThisFrame)
            HandleSkipOrContinue();
    }

    // ── event handlers ──
    private void ShowNode(string npcName, string text)
    {
        // abaikan jika ini dialogue narator
        if (DialogueManager.Instance.IsNarrator) return;

        bool wasHidden = !dialoguePanel.activeSelf;
        dialoguePanel.SetActive(true);

        if (wasHidden && !_panelWasOpen)
        {
            _panelWasOpen = true;
            onPanelOpened.Invoke();
        }

        if (npcNameText != null) npcNameText.text = npcName;

        // reset pending choices setiap node baru
        _pendingChoices = null;

        HideChoices();
        if (continueButton != null) continueButton.gameObject.SetActive(false);

        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _typeRoutine = StartCoroutine(TypewriterRoutine(text));
    }

    private void ShowChoices(DialogueChoice[] choices)
    {
        // abaikan jika ini dialogue narator
        if (DialogueManager.Instance.IsNarrator) return;

        _pendingChoices = choices;
    }

    private void HidePanel()
    {
        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _isTyping     = false;
        _panelWasOpen = false;

        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        HideChoices();
        onPanelClosed.Invoke();
    }

    /// Skip typewriter jika masih berjalan, TIDAK menutup dialogue
    private void HandleSkipOrContinue()
    {
        if (_isTyping)
            SkipTypewriter();
        // kalau sudah selesai mengetik, tidak lakukan apa-apa — biarkan player klik Continue Button
    }

    private void OnContinueClicked()
    {
        if (_isTyping)
        {
            SkipTypewriter();
            return;
        }

        onContinueClicked.Invoke();
        DialogueManager.Instance?.EndDialogue();
    }

    // ── typewriter ──
    private IEnumerator TypewriterRoutine(string fullText)
    {
        _isTyping        = true;
        _currentFullText = fullText;
        npcDialogueText.text = string.Empty;
        onTypewriterStarted.Invoke(fullText);

        foreach (char c in fullText)
        {
            npcDialogueText.text += c;
            yield return new WaitForSeconds(charDelay);
        }

        _isTyping = false;
        onTypewriterFinished.Invoke();
        ShowPendingChoices();
    }

    private void SkipTypewriter()
    {
        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _typeRoutine = null;
        _isTyping    = false;

        if (npcDialogueText != null)
            npcDialogueText.text = _currentFullText;

        onTypewriterSkipped.Invoke();
        ShowPendingChoices();
    }

    private void ShowPendingChoices()
    {
        var choices = _pendingChoices;
        _pendingChoices = null;

        bool hasChoices = choices != null && choices.Length > 0;

        if (continueButton != null)
            continueButton.gameObject.SetActive(!hasChoices);

        if (!hasChoices) return;

        foreach (var go in _choiceInstances) Destroy(go);
        _choiceInstances.Clear();

        onChoicesShown.Invoke();

        for (int i = 0; i < choices.Length; i++)
        {
            int capturedIndex = i;

            GameObject btn = Instantiate(choiceButtonPrefab, choicesContainer);
            _choiceInstances.Add(btn);

            var label = btn.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = choices[i].choiceText;

            var button = btn.GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(() =>
                {
                    onChoiceClicked.Invoke(capturedIndex);
                    DialogueManager.Instance.SelectChoice(capturedIndex);
                });
        }
    }

    private void HideChoices()
    {
        foreach (var go in _choiceInstances) Destroy(go);
        _choiceInstances.Clear();
    }

    // ── klik panel untuk skip ──
    private void AddPanelClickHandler()
    {
        if (dialoguePanel == null) return;

        var trigger = dialoguePanel.GetComponent<EventTrigger>();
        if (trigger == null) trigger = dialoguePanel.AddComponent<EventTrigger>();

        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        entry.callback.AddListener(_ => HandleSkipOrContinue());
        trigger.triggers.Add(entry);
    }
}