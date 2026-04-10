using System;
using UnityEngine;

/// <summary>
/// SceneItemID — komponen GUID stabil untuk semua pickup di scene.
///
/// MASALAH yang diselesaikan:
///   Semua pickup (KeyPickup, FusePickup, BatteryPickup, DiskPickup, AxePickup)
///   sebelumnya pakai gameObject.name sebagai save key. Ini rentan:
///     - Rename GameObject di Inspector → save key berubah → item respawn / dupe
///     - Duplicate prefab di scene → dua objek nama sama → satu key untuk dua item
///     - Procedural spawn → nama tidak deterministik antar session
///
/// SOLUSI:
///   SceneItemID menyimpan satu string GUID di [SerializeField]. GUID di-generate
///   SEKALI saat OnValidate() (di Editor) dan tidak pernah berubah lagi.
///   Semua pickup membaca ID via SceneItemID.Of(gameObject) di Awake().
///
/// CARA PAKAI:
///   Tambahkan komponen ini ke setiap pickup GameObject di scene.
///   GUID akan auto-generate saat kamu select objek di Editor.
///   Tidak perlu isi apa-apa secara manual.
///
/// CATATAN:
///   - Jangan copy-paste GameObject antar scene tanpa reset ID
///     (gunakan ContextMenu "Regenerate ID" jika perlu)
///   - Prefab yang di-Instantiate runtime tidak perlu komponen ini
///     (item runtime tidak perlu di-persist — mereka spawn ulang dari droppedItems save)
///   - ID hanya untuk item statis di scene hierarchy
/// </summary>
[DisallowMultipleComponent]
public class SceneItemID : MonoBehaviour
{
    [SerializeField, HideInInspector]
    private string _id = "";

    /// <summary>
    /// ID stabil objek ini. Selalu berupa GUID lowercase tanpa tanda hubung (32 karakter).
    /// Dijamin tidak kosong setelah Awake().
    /// </summary>
    public string ID => _id;

    // ── Static helper ─────────────────────────────────────────────────────────

    /// <summary>
    /// Ambil ID dari SceneItemID yang terpasang pada GameObject.
    /// Jika tidak ada komponen, fallback ke gameObject.name (kompatibilitas mundur).
    /// </summary>
    public static string Of(GameObject go)
    {
        var sid = go.GetComponent<SceneItemID>();
        if (sid != null && !string.IsNullOrEmpty(sid._id))
            return sid._id;

        // Fallback: nama GameObject (perilaku lama). Log warning agar tim tahu
        // objek ini belum punya SceneItemID.
        Debug.LogWarning(
            $"[SceneItemID] '{go.name}' tidak punya komponen SceneItemID. " +
            $"Fallback ke nama — rentan dupe jika nama tidak unik. " +
            $"Tambahkan komponen SceneItemID untuk menghilangkan warning ini.", go);
        return go.name;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Jaga-jaga: generate runtime jika ID kosong (misal objek lupa di-setup di editor)
        if (string.IsNullOrEmpty(_id))
        {
            _id = NewGuid();
            Debug.LogWarning(
                $"[SceneItemID] '{name}' ID kosong saat runtime — generated baru: {_id}. " +
                $"ID ini TIDAK persisten (hilang saat restart). " +
                $"Buka scene di Editor agar ID di-generate dan disimpan ke prefab/scene.", this);
        }
    }

    // ── Editor ────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Generate ID SEKALI saat pertama kali komponen ditambahkan atau ID kosong.
        // OnValidate dipanggil setiap kali Inspector berubah — guard dengan IsEmpty.
        if (!string.IsNullOrEmpty(_id)) return;

        _id = NewGuid();

        // Tandai scene/prefab dirty agar ID tersimpan
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[SceneItemID] Generated ID baru untuk '{name}': {_id}", this);
    }

    [ContextMenu("Regenerate ID (gunakan jika duplicate dari scene lain)")]
    private void RegenerateID()
    {
        _id = NewGuid();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[SceneItemID] ID di-regenerate untuk '{name}': {_id}", this);
    }

    [ContextMenu("Copy ID ke Clipboard")]
    private void CopyID()
    {
        UnityEditor.EditorGUIUtility.systemCopyBuffer = _id;
        Debug.Log($"[SceneItemID] ID disalin: {_id}");
    }
#endif

    // ── Private ───────────────────────────────────────────────────────────────

    private static string NewGuid() =>
        Guid.NewGuid().ToString("N"); // 32 karakter hex lowercase, tanpa tanda hubung
}