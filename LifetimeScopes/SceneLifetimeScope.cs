using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// SceneLifetimeScope — composition root untuk komponen per scene.
///
/// Fase 3 Step 4: Register EnemyAI agar bisa di-inject NoiseTracker + SanitySystem
/// dari ProjectLifetimeScope (parent scope).
///
/// Setup di Unity:
///   1. Buat GameObject "SceneLifetimeScope" di scene CCTV
///   2. Pasang script ini
///   3. Set Parent Scope ke ProjectLifetimeScope agar bisa resolve
///      NoiseTracker dan SanitySystem dari parent
///   4. Drag EnemyAI component dari scene ke field enemyAI di Inspector
///
/// Fase 4: Field player components akan ditambah di sini.
/// </summary>
public class SceneLifetimeScope : LifetimeScope
{
    [Header("Fase 3 — Scene Components")]
    [Tooltip("Drag EnemyAI component dari scene — bukan prefab")]
    [SerializeField] private EnemyAI enemyAI;

    // Fase 4 — akan ditambah:
    // [SerializeField] private PlayerInventory playerInventory;
    // [SerializeField] private PlayerDiskInventory playerDiskInventory;
    // [SerializeField] private PlayerEquipment playerEquipment;
    // [SerializeField] private FlashlightController flashlightController;
    // [SerializeField] private ItemDropper itemDropper;
    // [SerializeField] private InventoryUI inventoryUI;

    protected override void Configure(IContainerBuilder builder)
    {
        // Register EnemyAI — VContainer akan inject NoiseTracker + SanitySystem
        // dari ProjectLifetimeScope (parent scope) secara otomatis
        if (enemyAI != null)
            builder.RegisterComponent(enemyAI);
        else
            Debug.LogWarning("[SceneLifetimeScope] enemyAI belum di-assign di Inspector!");
    }
}