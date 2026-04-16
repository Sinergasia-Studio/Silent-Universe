using TMPro;
using UnityEngine;

public class ButtonBlinking : MonoBehaviour
{
    private TextMeshProUGUI text;

    private void Awake()
    {
        text = GetComponent<TextMeshProUGUI>();
    }
    
    private void Update()
    {
        float alpha = Mathf.PingPong(Time.time, 1f);
        text.color = new Color(text.color.r, text.color.g, text.color.b, alpha);
    }
}
