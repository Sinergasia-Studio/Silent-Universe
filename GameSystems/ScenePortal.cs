using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using System.Collections;

public class ScenePortalButton : MonoBehaviour
{
    [Header("Target Scene")]
    [SerializeField] private string targetSceneName;

    [Header("Visual Feedback")]
    [SerializeField] private Renderer buttonRenderer;
    [SerializeField] private Color    colorNormal = Color.white;
    [SerializeField] private Color    colorActive = Color.cyan;

    [Header("CCTV Reference")]
    [Tooltip("Assign MonitorInteractable di scene ini agar kamera aktif bisa disimpan")]
    [SerializeField] private MonitorInteractable monitor;

    [Header("Events")]
    public UnityEvent onPortalEnter;

    public void SetActive(bool active)
    {
        if (buttonRenderer == null) return;
        buttonRenderer.material.color = active ? colorActive : colorNormal;
    }

    public void Enter()
    {
        if (string.IsNullOrEmpty(targetSceneName)) return;

        // Simpan nama kamera aktif
        string activeCamName = "";
        if (monitor != null && monitor.ActiveCCTVCamera != null)
            activeCamName = monitor.ActiveCCTVCamera.cameraName;

        CCTVSaveData.Instance.SetCCTVActive(activeCamName);
        GameSave.Save();

        // Pause MonitorInteractable agar tidak proses input selama di Dampener
        // tapi JANGAN exit CCTV (agar IsCCTVActive tetap true)
        if (monitor != null)
            monitor.SetPaused(true);

        onPortalEnter.Invoke();

        StartCoroutine(LoadSceneAdditive(targetSceneName));
    }

    private IEnumerator LoadSceneAdditive(string sceneName)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        while (!op.isDone)
            yield return null;

        Scene loaded = SceneManager.GetSceneByName(sceneName);
        if (loaded.IsValid())
            SceneManager.SetActiveScene(loaded);

        // Unlock cursor untuk klik UI Dampener
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }
}