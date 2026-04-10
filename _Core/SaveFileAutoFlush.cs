using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveFileAutoFlush : MonoBehaviour
{
    private static SaveFileAutoFlush _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;

        // DontDestroyOnLoad hanya bekerja pada root GameObject.
        // Jika script ini dipasang pada child object, detach dulu dari parent.
        if (transform.parent != null)
            transform.SetParent(null);

        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // FIX — Skip re-read saat Additive load (scene rhythm, UI, dll.).
        // Additive load tidak mengganti scene utama, jadi tidak perlu re-read.
        // Re-read saat Additive bisa overwrite data in-memory yang belum di-write.
        if (mode == LoadSceneMode.Additive) return;

        SaveFile.Read();
        Debug.Log($"[SaveFileAutoFlush] SaveFile di-reload untuk scene: {scene.name}");
    }

    private void LateUpdate()
    {
        SaveFile.FlushPending();
    }

    private void OnApplicationQuit()
    {
        SaveFile.ForceWrite();
    }
}