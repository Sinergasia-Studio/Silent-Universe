using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// BatteryPickup — pasang pada GameObject baterai di scene.
/// Player hold E → baterai masuk PlayerBatteryInventory.
///
/// Save key sekarang pakai SceneItemID (GUID stabil) alih-alih gameObject.name.
/// Tambahkan komponen SceneItemID ke setiap BatteryPickup GameObject di scene.
/// </summary>
public class BatteryPickup : MonoBehaviour, IInteractable
{
    [Header("Item")]
    [SerializeField] private BatteryItem batteryItem;

    [Header("Interact Settings")]
    [SerializeField] private string promptText = "Tahan [E] untuk ambil baterai";
    [SerializeField] private string promptFull = "[Tas penuh — max baterai tercapai]";

    [Header("Settings")]
    [SerializeField] private bool hideOnPickup    = true;
    [SerializeField] private bool destroyOnPickup = false;

    [Header("Events")]
    public UnityEvent         onPickedUp;
    public UnityEvent<string> onPickedUpName;

    private bool   _pickedUp;
    private string _saveKey;

    public string PromptText  => PlayerBatteryInventory.Instance != null &&
                                 PlayerBatteryInventory.Instance.IsFull
                                 ? promptFull : promptText;

    public bool   CanInteract => !_pickedUp &&
                                 (PlayerBatteryInventory.Instance == null ||
                                  !PlayerBatteryInventory.Instance.IsFull);

    private void Awake()
    {
        _saveKey = "BP_" + SceneItemID.Of(gameObject);

        if (WorldFlags.Get(_saveKey))
        {
            _pickedUp = true;
            gameObject.SetActive(false);
        }
    }

    public void OnInteract(GameObject interactor)
    {
        if (!CanInteract) return;

        var inv = PlayerBatteryInventory.Instance;
        if (inv == null)
        {
            Debug.LogWarning("[BatteryPickup] PlayerBatteryInventory tidak ditemukan!");
            return;
        }

        _pickedUp = true;
        inv.AddBattery(batteryItem);
        onPickedUp.Invoke();
        onPickedUpName.Invoke(batteryItem != null ? batteryItem.itemName : "Baterai");

        WorldFlags.Set(_saveKey, true);
        Debug.Log($"[BatteryPickup] Mengambil baterai: {batteryItem?.itemName} (id: {_saveKey})");

        if (destroyOnPickup) Destroy(gameObject);
        else if (hideOnPickup) gameObject.SetActive(false);
    }

    public void ResetPickup()
    {
        _pickedUp = false;
        WorldFlags.Remove(_saveKey);
        gameObject.SetActive(true);
    }

    public void SetDestroyOnPickup(bool value) => destroyOnPickup = value;
}