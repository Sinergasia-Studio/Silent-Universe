using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// AxeItem — ScriptableObject identitas kampak.
/// Buat via: klik kanan Project → Create → Items → Axe Item
/// </summary>
[CreateAssetMenu(fileName = "NewAxe", menuName = "Items/Axe Item")]
public class AxeItem : ScriptableObject
{
    public string itemName = "Kampak";
    [TextArea] public string description;
    public int damagePerHit = 1;
}
