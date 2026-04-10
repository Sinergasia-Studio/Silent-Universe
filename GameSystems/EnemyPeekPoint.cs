using UnityEngine;

/// <summary>
/// EnemyPeekPoint — titik enemy bisa muncul dengan wujud/mesh sendiri.
///
/// Setiap peek point assign prefab yang berbeda — enemy bisa muncul
/// dalam bentuk apapun di tiap titik tanpa perlu satu model global.
///
/// Setup:
///   1. Buat empty GameObject di posisi yang diinginkan
///   2. Pasang script ini
///   3. Assign enemyPrefab — prefab harus punya Renderer[] untuk fade
///   4. Assign ke EnemyAI.peekPoints di Inspector
/// </summary>
public class EnemyPeekPoint : MonoBehaviour
{
    [Tooltip("Prefab enemy yang di-spawn saat peek di titik ini. Harus punya Renderer untuk fade.")]
    public GameObject enemyPrefab;

    [Tooltip("Suara khusus saat muncul di titik ini — opsional, fallback ke peekSound di EnemyAI")]
    public AudioClip overrideSound;

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        Gizmos.DrawSphere(transform.position, 0.2f);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1f);

        if (enemyPrefab != null)
        {
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.4f,
                enemyPrefab.name,
                UnityEditor.EditorStyles.miniLabel
            );
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = enemyPrefab != null
            ? new Color(1f, 0.3f, 0.3f, 0.3f)
            : new Color(0.5f, 0.5f, 0.5f, 0.3f); // abu-abu jika prefab belum assign
        Gizmos.DrawSphere(transform.position, 0.15f);
    }
#endif
}