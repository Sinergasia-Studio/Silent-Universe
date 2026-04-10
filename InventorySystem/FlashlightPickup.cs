using UnityEngine;
using UnityEngine.Events;

public class FlashlightPickup : MonoBehaviour, IInteractable
{
    [Header("Item")]
    [SerializeField] private FlashlightItem flashlightItem;

    [Header("Interact Settings")]
    [SerializeField] private string promptText       = "Tahan [E] untuk ambil senter";
    [SerializeField] private string promptAlreadyHas = "[Sudah membawa senter]";

    [Header("Settings")]
    [SerializeField] private bool hideOnPickup    = true;
    [SerializeField] private bool destroyOnPickup = false;

    [Header("Events")]
    public UnityEvent          onPickedUp;
    public UnityEvent<string>  onPickedUpName;

    private float                                _savedBattery = -1f;
    private bool                                 _hasSavedState;
    private FlashlightController.FlashlightState _savedState;
    private bool                                 _pickedUp;

    private string SaveKey => $"FlashlightPickup_Picked_{gameObject.name}";

    private void Awake()
    {
        // Hide pickup jika sudah diambil.
        // Equip ke PlayerEquipment dihandle oleh PlayerEquipment.LoadFromSave().
        if (WorldFlags.Get(SaveKey))
        {
            _pickedUp = true;
            gameObject.SetActive(false);
        }
    }

    public string PromptText  => PlayerEquipment.Instance != null && PlayerEquipment.Instance.HasFlashlight
                                 ? promptAlreadyHas : promptText;
    public bool   CanInteract => !_pickedUp &&
                                 (PlayerEquipment.Instance == null || !PlayerEquipment.Instance.HasFlashlight);

    public void OnInteract(GameObject interactor)
    {
        if (!CanInteract) return;

        var equip = PlayerEquipment.Instance;
        if (equip == null) return;

        _pickedUp = true;
        equip.EquipFlashlight(flashlightItem); // EquipFlashlight → PersistEquipment → ForceWrite
        WorldFlags.Set(SaveKey, true);          // Hide pickup object di scene

        var fc = FlashlightController.Instance;
        if (fc != null)
        {
            if (_hasSavedState)
                fc.RestoreState(_savedState);
            else if (_savedBattery >= 0f)
                fc.SetBattery(_savedBattery);
        }

        onPickedUp.Invoke();
        onPickedUpName.Invoke(flashlightItem != null ? flashlightItem.itemName : "Senter");

        if (destroyOnPickup) Destroy(gameObject);
        else if (hideOnPickup) gameObject.SetActive(false);
    }

    /// Dipanggil ItemDropper saat drop.
    public void SaveState(FlashlightController.FlashlightState state)
    {
        _savedState    = state;
        _hasSavedState = true;
        _savedBattery  = state.batteryRemaining;

        WorldFlags.Remove(SaveKey);
        _pickedUp = false;
        gameObject.SetActive(true);
    }

    public void SaveBattery(float remaining)
    {
        _savedBattery  = remaining;
        _hasSavedState = false;
    }

    public void ResetPickup()
    {
        _pickedUp      = false;
        _hasSavedState = false;
        _savedBattery  = -1f;
        WorldFlags.Remove(SaveKey);
        gameObject.SetActive(true);
    }

    public void SetDestroyOnPickup(bool v) => destroyOnPickup = v;
}