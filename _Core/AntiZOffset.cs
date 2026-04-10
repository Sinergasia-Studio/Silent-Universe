using UnityEngine;

/// <summary>
/// ZFightingFixer — fix z-fighting (texture blink) saat 2 mesh bertemu.
/// Pasang pada GameObject yang mesh-nya blink, atau pada parent-nya.
///
/// Cara kerja:
///   Menerapkan Polygon Offset pada material objek agar GPU
///   menggambar mesh sedikit lebih dekat ke kamera secara depth,
///   tanpa mengubah posisi visual sama sekali.
///
/// Setup:
///   1. Pasang script ini pada GameObject yang blink
///   2. Atur offsetFactor dan offsetUnits di Inspector
///   3. Klik "Terapkan Offset Sekarang" atau aktifkan jalankanSaatStart
/// </summary>
public class ZFightingFixer : MonoBehaviour
{
    [Header("Polygon Offset")]
    [Tooltip("Factor: seberapa besar offset berdasarkan slope polygon. -1 biasanya cukup.")]
    public float offsetFactor = -1f;
    [Tooltip("Units: offset tetap dalam depth buffer units. -1 sampai -5 biasanya cukup.")]
    public float offsetUnits  = -1f;

    [Header("Target")]
    [Tooltip("Jika true, juga terapkan ke semua anak objek ini.")]
    public bool includeChildren = true;
    [Tooltip("Buat instance material baru agar tidak mempengaruhi material asli (asset).")]
    public bool useMaterialInstance = true;

    [Header("Otomatis")]
    public bool jalankanSaatStart = true;

    private void Start()
    {
        if (jalankanSaatStart)
            Terapkan();
    }

    [ContextMenu("Terapkan Offset Sekarang")]
    public void Terapkan()
    {
        var renderers = includeChildren
            ? GetComponentsInChildren<Renderer>()
            : new Renderer[] { GetComponent<Renderer>() };

        int count = 0;
        foreach (var r in renderers)
        {
            if (r == null) continue;

            Material[] mats = useMaterialInstance ? r.materials : r.sharedMaterials;

            foreach (var mat in mats)
            {
                if (mat == null) continue;
                mat.SetFloat("_ZTestMode", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                mat.renderQueue = mat.renderQueue > 0 ? mat.renderQueue : 2000;

                // Polygon Offset — kunci utama fix z-fighting
                mat.SetFloat("_OffsetFactor", offsetFactor);
                mat.SetFloat("_OffsetUnits",  offsetUnits);

                // Enable keyword jika shader support
                mat.EnableKeyword("_POLYGONOFFSET");
            }

            if (useMaterialInstance)
                r.materials = mats;

            count++;
        }

        Debug.Log("[ZFightingFixer] Diterapkan ke " + count + " renderer.");
    }

    [ContextMenu("Reset ke Default")]
    public void Reset()
    {
        var renderers = includeChildren
            ? GetComponentsInChildren<Renderer>()
            : new Renderer[] { GetComponent<Renderer>() };

        foreach (var r in renderers)
        {
            if (r == null) continue;
            Material[] mats = useMaterialInstance ? r.materials : r.sharedMaterials;
            foreach (var mat in mats)
            {
                if (mat == null) continue;
                mat.SetFloat("_OffsetFactor", 0f);
                mat.SetFloat("_OffsetUnits",  0f);
            }
            if (useMaterialInstance) r.materials = mats;
        }
        Debug.Log("[ZFightingFixer] Reset selesai.");
    }
}