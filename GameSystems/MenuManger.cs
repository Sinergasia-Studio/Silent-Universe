using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManger : MonoBehaviour
{
    [SerializeField] private GameObject PausePanel;
    [SerializeField] private GameObject SettingsPanel;

    [Header("Scene to Load")]
    [SerializeField] private int SceneToLoad;

    public void OpenSettings()
    {
        SettingsPanel.SetActive(true);
        PausePanel.SetActive(false);
    }

    public void CloseSettings()
    {
        SettingsPanel.SetActive(false);
        PausePanel.SetActive(true);
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
        gameObject.SetActive(false);
    }
    public void ExitToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneToLoad);
    }

}
