using UnityEngine;

/// <summary>
/// FlashlightItem — ScriptableObject identitas flashlight.
/// Buat via: klik kanan Project → Create → Items → Flashlight Item
/// </summary>
[CreateAssetMenu(fileName = "NewFlashlight", menuName = "Items/Flashlight Item")]
public class FlashlightItem : ScriptableObject
{
    public string itemName        = "Senter";
    [TextArea] public string description;
    [Tooltip("Durasi baterai dalam detik. 0 = tidak terbatas.")]
    public float batteryDuration  = 120f;
    [Tooltip("Persentase baterai mulai flicker (0-1)")]
    [Range(0f, 0.5f)]
    public float flickerThreshold = 0.15f;
}
