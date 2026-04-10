using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// ProjectLifetimeScope — composition root untuk sistem global lintas scene.
///
/// Fase 3 Step 1: SanitySystem ✓
/// Fase 3 Step 2: NoiseTracker ✓
/// Fase 3 Step 3: QuestManager (sekarang)
/// </summary>
public class ProjectLifetimeScope : LifetimeScope
{
    [Header("Fase 3 — Global Singletons")]
    [SerializeField] private SanitySystem  sanitySystemPrefab;
    [SerializeField] private NoiseTracker  noiseTrackerPrefab;
    [SerializeField] private QuestManager  questManagerPrefab;

    protected override void Configure(IContainerBuilder builder)
    {
        // Urutan penting — SanitySystem dulu karena NoiseTracker inject ini
        if (sanitySystemPrefab != null)
            builder.RegisterComponentInNewPrefab(sanitySystemPrefab, Lifetime.Singleton);
        else
            Debug.LogWarning("[ProjectLifetimeScope] sanitySystemPrefab belum di-assign!");

        if (noiseTrackerPrefab != null)
            builder.RegisterComponentInNewPrefab(noiseTrackerPrefab, Lifetime.Singleton);
        else
            Debug.LogWarning("[ProjectLifetimeScope] noiseTrackerPrefab belum di-assign!");

        // QuestManager tidak punya dependency ke SanitySystem/NoiseTracker
        // jadi urutan register tidak kritis
        if (questManagerPrefab != null)
            builder.RegisterComponentInNewPrefab(questManagerPrefab, Lifetime.Singleton);
        else
            Debug.LogWarning("[ProjectLifetimeScope] questManagerPrefab belum di-assign!");
    }
}