using UnityEngine;

public interface IInteractable
{
    string PromptText  { get; }
    bool   CanInteract { get; }
    void   OnInteract(GameObject interactor);
}
