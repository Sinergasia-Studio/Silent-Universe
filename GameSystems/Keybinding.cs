using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class Keybinding : MonoBehaviour
{
    #region Editable Fields
    [Tooltip("Add the keyname that u like to bind")]
    [SerializeField]private string actionName;
    [SerializeField] private InputActionAsset inputActionAsset;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private GameObject rebindingUI;
    [SerializeField] private TextMeshProUGUI bindLabel;
    [SerializeField] private TextMeshProUGUI actionLabel;
    #endregion
    #region Non Editable Fields
    private InputAction action;
    private InputActionRebindingExtensions.RebindingOperation rebindingOperation;
    #endregion

    private void Awake()
    {
        action = inputActionAsset.FindAction("Player/" + actionName);
        if (action != null)
        {
            bindLabel.text = action.GetBindingDisplayString(group:"Keyboard&Mouse");
            actionLabel.text = actionName;
        }
        else
            Debug.LogError($"Action '{actionName}' not found in InputActionAsset.");

    }
    public void StartRebinding()
    {
        if (action == null)
        {
            Debug.LogError($"Action '{actionName}' not found in InputActionAsset.");
            return;
        }
        rebindingUI.SetActive(true);
        inputActionAsset.FindActionMap("Player").Disable();
        label.text = "Press a key to rebind...";
        rebindingOperation = action.PerformInteractiveRebinding()
            .WithControlsExcluding("Mouse")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(operation =>
            {
                OnRebindingComplete(operation);
            })
            .Start();
    }
    private void OnRebindingComplete(InputActionRebindingExtensions.RebindingOperation operation)
    {
        bindLabel.text = action.GetBindingDisplayString(group: "Keyboard&Mouse");
        rebindingUI.SetActive(false);
        inputActionAsset.FindActionMap("Player").Enable();
        operation.Dispose();
        var rebinds = inputActionAsset.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString("_rebinds", rebinds);
    }
}
