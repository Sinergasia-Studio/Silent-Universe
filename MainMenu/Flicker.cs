using UnityEngine;

public class FlashlightFlicker : MonoBehaviour
{
    public Light flashlight;

    [Header("Flicker Settings")]
    public float minIntensity = 0.2f;
    public float maxIntensity = 1.2f;
    public float flickerSpeed = 0.05f;

    [Header("Random Blink")]
    public float blinkChance = 0.05f; // peluang mati total
    public float blinkDuration = 0.1f;

    private float targetIntensity;
    private float timer;

    void Start()
    {
        if (flashlight == null)
            flashlight = GetComponent<Light>();

        targetIntensity = maxIntensity;
    }

    void Update()
    {
        // Flicker halus (kayak listrik ga stabil)
        if (Time.time > timer)
        {
            targetIntensity = Random.Range(minIntensity, maxIntensity);
            timer = Time.time + flickerSpeed;
        }

        flashlight.intensity = Mathf.Lerp(flashlight.intensity, targetIntensity, Time.deltaTime * 10f);

        // Blink mati total (random)
        if (Random.value < blinkChance * Time.deltaTime)
        {
            StartCoroutine(BlinkOff());
        }
    }

    System.Collections.IEnumerator BlinkOff()
    {
        float original = flashlight.intensity;

        flashlight.intensity = 0;
        yield return new WaitForSeconds(blinkDuration);

        flashlight.intensity = original;
    }
}