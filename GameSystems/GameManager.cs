using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    [Header("Per-lane key bindings (must match lane count)")]
    [SerializeField] private InputActionReference[] laneActions;

    private LinkedList<Tile>[] tilesPerLane;
    private int laneCount;

    private void Awake()
    {
        laneCount = laneActions != null ? Mathf.Max(1, laneActions.Length) : 1;
        tilesPerLane = new LinkedList<Tile>[laneCount];
        for (int i = 0; i < laneCount; i++)
            tilesPerLane[i] = new LinkedList<Tile>();
    }

    private void OnEnable()
    {
        if (laneActions == null) return;
        foreach (var a in laneActions)
            a?.action?.Enable();
    }

    private void OnDisable()
    {
        if (laneActions == null) return;
        foreach (var a in laneActions)
            a?.action?.Disable();
    }

    [Header("Hit Validation")]
    [Tooltip("Tile hanya bisa dihit jika ada di dalam pressing area (IsInPressArea).")]
    [SerializeField] private bool requireTileInPressArea = true;

    private void Update()
    {
        if (laneActions == null) return;
        for (int i = 0; i < laneActions.Length; i++)
        {
            if (laneActions[i]?.action?.WasPressedThisFrame() == true)
                HitLane(i);
        }
    }

    public void RegisterTile(Tile tile, int lane)
    {
        if (!IsValidLane(lane) || tile == null) return;
        tilesPerLane[lane].AddLast(tile);
    }

    public void UnregisterTile(Tile tile, int lane)
    {
        if (!IsValidLane(lane) || tile == null) return;
        tilesPerLane[lane].Remove(tile);
    }

    private void HitLane(int lane)
    {
        if (!IsValidLane(lane)) return;
        var list = tilesPerLane[lane];
        if (list.Count == 0) return;

        var tile = list.First.Value;

        if (requireTileInPressArea && tile != null && !tile.IsInPressArea)
            return;

        // BUG FIX #5 — Hapus dari list SEBELUM OnHit() dipanggil.
        // OnHit() memanggil UnregisterTile() secara internal, tapi karena
        // tile sudah tidak ada di list, Remove() akan no-op (tidak crash,
        // tidak double-count). Urutan ini mencegah redundansi tanpa perlu
        // mengubah class Tile sama sekali.
        list.RemoveFirst();
        tile?.OnHit();
    }

    private bool IsValidLane(int lane) =>
        tilesPerLane != null && lane >= 0 && lane < tilesPerLane.Length;
}