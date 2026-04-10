using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// KeyPickup — pasang pada GameObject kunci di scene.
/// Hanya bisa diambil jika player belum punya key yang sama.
///
/// Save key sekarang pakai SceneItemID (GUID stabil) alih-alih gameObject.name.
/// Tambahkan komponen SceneItemID ke setiap KeyPickup GameObject di scene.
/// </summary>
public class KeyPickup : MonoBehaviour, IInteractable
{
    [Header("Key")]
    [SerializeField] private KeyItem keyItem;

    [Header("Interact Settings")]
    [SerializeField] private string promptText       = "Tahan [E] untuk ambil";
    [SerializeField] private string promptAlreadyHas = "[Sudah dimiliki]";

    [Header("Settings")]
    [SerializeField] private bool hideOnPickup    = true;
    [SerializeField] private bool destroyOnPickup = false;

    [Header("Events")]
    public UnityEvent<string> onKeyPickedUp;
    public UnityEvent         onAlreadyOwned;

    private bool   _pickedUp;
    private string _saveKey; // di-cache dari SceneItemID saat Awake

    public string PromptText
    {
        get
        {
            if (_pickedUp) return promptAlreadyHas;
            var inv = PlayerInventory.Instance;
            if (inv != null && keyItem != null && inv.HasKey(keyItem)) return promptAlreadyHas;
            return promptText;
        }
    }

    public bool CanInteract
    {
        get
        {
            if (_pickedUp) return false;
            var inv = PlayerInventory.Instance;
            if (inv != null && keyItem != null && inv.HasKey(keyItem)) return false;
            return true;
        }
    }

    private void Awake()
    {
        // Cache save key dari SceneItemID — stabil meski GameObject di-rename
        _saveKey = "KP_" + SceneItemID.Of(gameObject);

        if (WorldFlags.Get(_saveKey))
        {
            _pickedUp = true;
            gameObject.SetActive(false);
        }
    }

    public void OnInteract(GameObject interactor)
    {
        if (_pickedUp) return;

        var inventory = PlayerInventory.Instance;
        if (inventory == null)
        {
            Debug.LogWarning("[KeyPickup] PlayerInventory tidak ditemukan!");
            return;
        }

        if (keyItem != null && inventory.HasKey(keyItem))
        {
            onAlreadyOwned.Invoke();
            return;
        }

        _pickedUp = true;
        inventory.AddKey(keyItem);
        WorldFlags.Set(_saveKey, true);
        onKeyPickedUp.Invoke(keyItem != null ? keyItem.keyName : "");
        Debug.Log($"[KeyPickup] Mengambil key: {keyItem?.keyName} (id: {_saveKey})");

        if (destroyOnPickup)
            Destroy(gameObject);
        else if (hideOnPickup)
            gameObject.SetActive(false);
    }

    public void ResetPickup()
    {
        _pickedUp = false;
        WorldFlags.Remove(_saveKey);
        gameObject.SetActive(true);
    }

    public void SetKey(KeyItem key)            => keyItem = key;
    public void SetDestroyOnPickup(bool value) => destroyOnPickup = value;
}