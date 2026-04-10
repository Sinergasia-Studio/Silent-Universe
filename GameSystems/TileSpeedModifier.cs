using System.Collections;
using UnityEngine;

public class TileSpeedModifier : MonoBehaviour
{
    [Header("=== WAJIB DIISI ===")]
    [SerializeField] private SpeedEventChannel speedChannel;

    private void Awake()
    {
        if (speedChannel == null)
            Debug.LogError("[TileSpeedModifier] speedChannel BELUM DIISI!");
        else
            Debug.Log("[TileSpeedModifier] Terhubung ke '" + speedChannel.name + "', LastMultiplier=" + speedChannel.LastMultiplier);
    }

    private void OnEnable()
    {
        if (speedChannel == null) return;
        speedChannel.OnSpeedChanged += ApplySpeedMultiplier;
    }

    private void OnDisable()
    {
        if (speedChannel != null)
            speedChannel.OnSpeedChanged -= ApplySpeedMultiplier;
    }

    private void Start()
    {
        // Delay 1 frame agar Spawner.Awake() selesai dulu
        StartCoroutine(ApplyNextFrame());
    }

    private IEnumerator ApplyNextFrame()
    {
        yield return null;

        if (speedChannel == null) yield break;

        if (!Mathf.Approximately(speedChannel.LastMultiplier, 1f))
        {
            Debug.Log("[TileSpeedModifier] Late-apply LastMultiplier=" + speedChannel.LastMultiplier);
            ApplySpeedMultiplier(speedChannel.LastMultiplier);
        }
        else
        {
            Debug.Log("[TileSpeedModifier] Speed normal (x1).");
        }
    }

    private void ApplySpeedMultiplier(float multiplier)
    {
        multiplier = Mathf.Max(0.01f, multiplier);

        if (Spawner.Instance != null)
        {
            Spawner.Instance.SetGlobalSpeedMultiplier(multiplier);
        }
        else
        {
            Debug.LogWarning("[TileSpeedModifier] Spawner.Instance null!");
        }

        // Update tile yang sudah ada di scene
        var tiles = FindObjectsByType<Tile>(FindObjectsSortMode.None);
        foreach (var t in tiles) t.SetSpeedMultiplier(multiplier);

        Debug.Log("[TileSpeedModifier] Apply x" + multiplier + " ke " + tiles.Length + " tile(s)");
    }
}