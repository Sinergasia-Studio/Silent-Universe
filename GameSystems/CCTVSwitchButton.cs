using UnityEngine;

/// <summary>
/// CCTVSwitchButton — pasang pada collider 3D di world.
/// Saat diklik dari MonitorInteractable → aktifkan targetRawImage, nonaktifkan yang lain.
/// </summary>
public class CCTVSwitchButton : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("RawImage yang diaktifkan saat tombol ini diklik")]
    [SerializeField] public GameObject targetRawImage;

    [Tooltip("CCTVCamera yang aktif saat RawImage ini tampil")]
    [SerializeField] public CCTVCamera targetCamera;

    [Header("Visual Feedback")]
    [Tooltip("Renderer tombol ini — berubah warna saat aktif")]
    [SerializeField] private Renderer buttonRenderer;
    [SerializeField] private Color    colorNormal = Color.white;
    [SerializeField] private Color    colorActive = Color.cyan;

    public void SetActive(bool active)
    {
        if (buttonRenderer == null) return;
        buttonRenderer.material.color = active ? colorActive : colorNormal;
    }
}