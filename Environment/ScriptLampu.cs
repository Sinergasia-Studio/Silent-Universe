using UnityEngine;

public class LightFlicker : MonoBehaviour
{
    public Light lampu;

    [Header("Intensity")]
    public float minIntensity = 0.3f;
    public float maxIntensity = 1.5f;

    [Header("Speed Flicker")]
    public float flickerSpeed = 0.05f;

    [Header("Blink Random (mati total)")]
    public float blinkChance = 0.05f; // peluang mati
    public float blinkDuration = 0.1f;

    private float timer;

    void Start()
    {
        if (lampu == null)
            lampu = GetComponent<Light>();
    }

    void Update()
    {
        timer += Time.deltaTime;

        // Flicker biasa
        if (timer >= flickerSpeed)
        {
            lampu.intensity = Random.Range(minIntensity, maxIntensity);
            timer = 0f;

            // Random mati total (blink)
            if (Random.value < blinkChance)
            {
                StartCoroutine(Blink());
            }
        }
    }

    System.Collections.IEnumerator Blink()
    {
        float originalIntensity = lampu.intensity;
        lampu.intensity = 0f;

        yield return new WaitForSeconds(blinkDuration);

        lampu.intensity = originalIntensity;
    }
}