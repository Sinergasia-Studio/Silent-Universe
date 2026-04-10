using UnityEngine;

public class CinematicCamera : MonoBehaviour
{
    public float speed = 0.8f;

    public float posAmountX = 0.02f; // sedikit banget kiri-kanan
    public float posAmountY = 0.04f; // fokus utama naik-turun

    public float rotAmountX = 0.8f; // angguk dikit
    public float rotAmountZ = 0.5f; // miring dikit

    private Vector3 startPos;
    private Quaternion startRot;

    void Start()
    {
        startPos = transform.localPosition;
        startRot = transform.localRotation;
    }

    void Update()
    {
        float time = Time.time * speed;

        // posisi (lebih dominan Y biar cinematic)
        float offsetX = Mathf.Sin(time * 0.7f) * posAmountX;
        float offsetY = Mathf.Sin(time * 1.2f) * posAmountY;

        transform.localPosition = startPos + new Vector3(offsetX, offsetY, 0);

        // rotasi halus
        float rotX = Mathf.Sin(time * 1.1f) * rotAmountX;
        float rotZ = Mathf.Sin(time * 0.9f) * rotAmountZ;

        transform.localRotation = startRot * Quaternion.Euler(rotX, 0, rotZ);
    }
}