using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class GlitchHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public TMP_Text text;

    [Header("Color")]
    public Color normalColor = Color.white;
    public Color hoverColor = Color.red;

    [Header("Glitch Settings")]
    public float glitchIntensity = 5f;
    public float glitchSpeed = 0.05f;

    [Header("Rotation Glitch")]
    public float rotationAmount = 10f;

    private Vector3 originalPos;
    private Vector3 originalScale;
    private Quaternion originalRot;

    private bool isHovering = false;
    private float glitchTimer;

    void Start()
    {
        originalPos = transform.localPosition;
        originalScale = transform.localScale;
        originalRot = transform.localRotation;

        text.color = normalColor;
    }

    void Update()
    {
        if (isHovering)
        {
            glitchTimer -= Time.deltaTime;

            if (glitchTimer <= 0)
            {
                glitchTimer = glitchSpeed;

                // SCALE glitch
                float randomScale = 1 + Random.Range(-0.2f, 0.3f);
                transform.localScale = originalScale * randomScale;

                // POSITION glitch
                float offsetX = Random.Range(-glitchIntensity, glitchIntensity);
                float offsetY = Random.Range(-glitchIntensity * 0.5f, glitchIntensity * 0.5f);
                transform.localPosition = originalPos + new Vector3(offsetX, offsetY, 0);

                // ROTATION glitch (INI YANG BARU 🔥)
                float rotZ = Random.Range(-rotationAmount, rotationAmount);
                transform.localRotation = originalRot * Quaternion.Euler(0, 0, rotZ);

                // warna flicker dikit
                text.color = Random.value > 0.5f ? hoverColor : Color.white;
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        text.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;

        transform.localPosition = originalPos;
        transform.localScale = originalScale;
        transform.localRotation = originalRot;
        text.color = normalColor;
    }
}