using UnityEngine;

/// <summary>
/// KeyItem — ScriptableObject sebagai identitas kunci.
/// Buat via: klik kanan di Project → Create → Door System → Key Item
/// Assign KeyItem yang sama ke PlayerInventory dan DoorInteractable agar cocok.
/// </summary>
[CreateAssetMenu(fileName = "NewKey", menuName = "Door System/Key Item")]
public class KeyItem : ScriptableObject
{
    public string keyName;
    [TextArea] public string description;
}
