using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InteractionUI : MonoBehaviour
{
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private TMP_Text   promptLabel;
    [SerializeField] private GameObject progressRoot;
    [SerializeField] private Image      progressFill;

    private bool _dialogueActive;

    private void Start()
    {
        var dm = DialogueManager.Instance;
        if (dm == null) return;

        dm.onDialogueStart.AddListener((_, __) => OnDialogueStart());
        dm.onDialogueEnd.AddListener(OnDialogueEnd);
    }

    private void OnDialogueStart()
    {
        _dialogueActive = true;
        // sembunyikan prompt & progress saat dialogue
        if (promptRoot   != null) promptRoot.SetActive(false);
        if (progressRoot != null) progressRoot.SetActive(false);
    }

    private void OnDialogueEnd()
    {
        _dialogueActive = false;
    }

    public void SetPromptVisible(bool visible, string text = null)
    {
        // block saat dialogue aktif
        if (_dialogueActive) return;

        if (promptRoot  != null) promptRoot.SetActive(visible);
        if (promptLabel != null && text != null) promptLabel.text = text;
    }

    public void SetProgress(float t, bool visible)
    {
        // block saat dialogue aktif
        if (_dialogueActive) return;

        if (progressRoot != null) progressRoot.SetActive(visible);
        if (progressFill != null) progressFill.fillAmount = t;
    }
}