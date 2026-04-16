using UnityEngine;
using VContainer;

public class TabManagerView : MonoBehaviour
{
    [SerializeField] private GameObject[] panels;

    public void ChangeTab(int index)
    {
            for (int i = 0; i < panels.Length; i++)
            {
                panels[i].SetActive(i == index);
                if (i == index) Debug.Log($"Showing panel {i}");
            }
            Debug.Log("Changing Tabs");
    }
}
