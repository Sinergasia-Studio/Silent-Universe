using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// CCTVSaveData — Singleton MonoBehaviour untuk save/load state CCTV.
/// Data kini disimpan ke JSON via SaveFile, bukan PlayerPrefs.
/// </summary>
public class CCTVSaveData : MonoBehaviour
{
    public static CCTVSaveData Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (transform.parent != null) transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    public void SetCCTVActive(string activeCameraName)
    {
        var d = SaveFile.Data;
        d.cctvActive = true;
        d.cctvScene  = SceneManager.GetActiveScene().name;
        d.cctvCamera = activeCameraName ?? "";
        d.cctvDiskIn = true;
        SaveFile.Write();
        Debug.Log($"[CCTVSave] State disimpan — cam: {activeCameraName}");
    }

    public void ClearCCTV()
    {
        var d = SaveFile.Data;
        d.cctvActive = false;
        d.cctvScene  = "";
        d.cctvCamera = "";
        d.cctvDiskIn = false;
        SaveFile.Write();
        Debug.Log("[CCTVSave] State dihapus.");
    }

    public bool ShouldRestoreCCTV()
    {
        var d = SaveFile.Data;
        if (!d.cctvActive) return false;
        return d.cctvScene == SceneManager.GetActiveScene().name;
    }

    public string GetSavedCameraName() => SaveFile.Data.cctvCamera;
    public bool   DiskWasInserted()    => SaveFile.Data.cctvDiskIn;

    [ContextMenu("DEV: Reset All Save")]
    public void DEV_ResetAll()
    {
        ClearCCTV();
        GameSave.DeleteSave();
        QuestManager.Instance?.DEV_ResetAll();
        Debug.Log("[CCTVSave] Semua save data direset.");
    }
}
