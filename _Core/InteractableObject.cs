using UnityEngine;
using UnityEngine.Events;

public class InteractableObject : MonoBehaviour, IInteractable
{
    [SerializeField] private string promptText  = "Tahan [E] untuk berinteraksi";
    [SerializeField] private bool   canInteract = true;

    public string PromptText  => promptText;
    public bool   CanInteract => canInteract;

    public UnityEvent<GameObject> onInteracted;

    public void OnInteract(GameObject interactor)
    {
        Debug.Log($"[Interactable] '{name}' diinteraksi oleh {interactor.name}");
        onInteracted?.Invoke(interactor);
    }
}
