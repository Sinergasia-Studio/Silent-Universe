using UnityEngine;
using TMPro;

public class MainMenuHandler : MonoBehaviour
{
    public TMP_Text quitText;

    private bool isConfirmingQuit = false;

    public void QuitGame()
    {
        if (!isConfirmingQuit)
        {
            // klik pertama
            quitText.text = "Sure ?";
            isConfirmingQuit = true;
        }
        else
        {
            // klik kedua → keluar
            Application.Quit();

            // buat testing di editor
            Debug.Log("Game Quit");
        }
    }
}