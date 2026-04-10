using UnityEngine;

public class FlashlightBob : MonoBehaviour
{
    [Header("Bob Settings")]
    public float bobSpeed = 6f;
    public float bobAmount = 0.05f;

    [Header("Side Movement")]
    public float sideAmount = 0.03f;

    [Header("Rotation")]
    public float rotAmount = 2f;

    [Header("Smooth")]
    public float smoothSpeed = 8f;

    private Vector3 startPos;
    private Quaternion startRot;

    private float timer;

    void Start()
    {
        startPos = transform.localPosition;
        startRot = transform.localRotation;
    }

    void Update()
    {
        // Gerakan bobbing (naik turun + kiri kanan)
        timer += Time.deltaTime * bobSpeed;

        float bobY = Mathf.Sin(timer) * bobAmount;
        float bobX = Mathf.Cos(timer * 0.5f) * sideAmount;

        Vector3 targetPos = startPos + new Vector3(bobX, bobY, 0);

        // Rotasi sedikit biar lebih hidup
        float rotX = -bobY * rotAmount;
        float rotY = bobX * rotAmount;

        Quaternion targetRot = startRot * Quaternion.Euler(rotX, rotY, 0);

        // Smooth biar ga bikin pusing
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * smoothSpeed);
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRot, Time.deltaTime * smoothSpeed);
    }
}