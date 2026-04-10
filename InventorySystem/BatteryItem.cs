using UnityEngine;

/// <summary>
/// BatteryItem — ScriptableObject identitas baterai.
/// Buat via: klik kanan Project → Create → Items → Battery Item
/// </summary>
[CreateAssetMenu(fileName = "NewBattery", menuName = "Items/Battery Item")]
public class BatteryItem : ScriptableObject
{
    public string itemName           = "Baterai";
    [TextArea] public string description;
    [Tooltip("Berapa detik baterai yang ditambahkan ke senter")]
    public float rechargeAmount      = 60f;
}
