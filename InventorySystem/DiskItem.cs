using UnityEngine;

/// <summary>
/// DiskItem — ScriptableObject identitas disk.
/// Buat via: klik kanan Project → Create → Items → Disk Item
/// </summary>
[CreateAssetMenu(fileName = "NewDisk", menuName = "Items/Disk Item")]
public class DiskItem : ScriptableObject
{
    public string itemName = "Disk";
    [TextArea] public string description;
    public Sprite icon;
}
