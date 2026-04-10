using UnityEngine;

/// <summary>
/// FuseItem — ScriptableObject identitas fuse.
/// Buat via: klik kanan Project → Create → Items → Fuse Item
/// </summary>
[CreateAssetMenu(fileName = "NewFuse", menuName = "Items/Fuse Item")]
public class FuseItem : ScriptableObject
{
    public string itemName = "Fuse";
    [TextArea] public string description;
}
