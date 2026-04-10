using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PlayerSpawner — pasang pada Player GameObject di setiap scene.
/// Restore posisi dari JSON save saat scene di-load.
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    [Tooltip("Jika true, posisi Y dari save diabaikan dan player di-snap ke tanah.")]
    [SerializeField] private bool  snapToGround         = false;
    [SerializeField] private float groundCheckDistance  = 5f;

    private void Awake()
    {
        if (!GameSave.HasSave()) return;

        // Hanya restore jika scene ini adalah scene yang tersimpan
        if (SaveFile.Data.sceneName != SceneManager.GetActiveScene().name) return;

        Vector3 savedPos = GameSave.GetSavedPosition();

        if (snapToGround)
        {
            if (Physics.Raycast(savedPos + Vector3.up * 2f, Vector3.down,
                                out RaycastHit hit, groundCheckDistance))
                savedPos.y = hit.point.y;
        }

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = savedPos;
        if (cc != null) cc.enabled = true;

        Debug.Log($"[PlayerSpawner] Player di-spawn ke posisi: {savedPos}");
    }
}
