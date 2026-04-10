using UnityEngine;
using UnityEngine.InputSystem;

public class CanvasGyro : MonoBehaviour
{
    [Header("Settings")]
    public float moveAmount = 30f;
    public float rotateAmount = 15f;
    public float smoothSpeed = 5f;

    private Vector3 targetPos;
    private Quaternion targetRot;

    void Update()
    {
        // Ambil posisi mouse dari Input System baru
        Vector2 mousePos = Mouse.current.position.ReadValue();

        // Normalize ke -1 sampai 1
        float mouseX = (mousePos.x / Screen.width - 0.5f) * 2f;
        float mouseY = (mousePos.y / Screen.height - 0.5f) * 2f;

        // Target posisi
        targetPos = new Vector3(mouseX * moveAmount, mouseY * moveAmount, 0);

        // Target rotasi (biar ada efek tilt)
        targetRot = Quaternion.Euler(-mouseY * rotateAmount, mouseX * rotateAmount, 0);

        // Smooth movement
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * smoothSpeed);
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRot, Time.deltaTime * smoothSpeed);
    }
}