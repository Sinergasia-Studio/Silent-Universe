using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// CRTRendererFeature — Unity 6 URP, RenderGraph API.
/// Tambahkan ke Universal Renderer Data -> Add Renderer Feature.
///
/// Setup:
///   1. Buat Material baru dengan shader "Hidden/CRTEffect"
///   2. Assign material di field "CRT Material" di Inspector
/// </summary>
public class CRTRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material crtMaterial;
        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public Settings settings = new Settings();
    private CRTPass _pass;

    public override void Create()
    {
        _pass = new CRTPass(settings);
        _pass.renderPassEvent = settings.passEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.crtMaterial == null)
        {
            Debug.LogWarning("[CRTRendererFeature] CRT Material belum diassign!");
            return;
        }
        if (renderingData.cameraData.cameraType == CameraType.SceneView) return;

        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
    }
}

class CRTPassData
{
    public TextureHandle src;
    public Material      material;
}

class CRTPass : ScriptableRenderPass, System.IDisposable
{
    private readonly CRTRendererFeature.Settings _settings;
    private static readonly int TimePropID = Shader.PropertyToID("_Time_Custom");

    public CRTPass(CRTRendererFeature.Settings settings)
    {
        _settings        = settings;
        profilingSampler = new ProfilingSampler("CRT Effect");
        requiresIntermediateTexture = true;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        var resourceData = frameData.Get<UniversalResourceData>();
        var cameraData   = frameData.Get<UniversalCameraData>();

        if (resourceData.isActiveTargetBackBuffer) return;

        _settings.crtMaterial.SetFloat(TimePropID, Time.time);

        var desc = cameraData.cameraTargetDescriptor;
        desc.depthBufferBits = 0;
        desc.msaaSamples     = 1;

        TextureHandle src  = resourceData.activeColorTexture;
        TextureHandle dest = UniversalRenderer.CreateRenderGraphTexture(
            renderGraph, desc, "_CRTTemp", false);

        // Pass 1: blit camera color + CRT effect ke temp texture
        using (var builder = renderGraph.AddRasterRenderPass<CRTPassData>(
            "CRT Blit To Temp", out var passData, profilingSampler))
        {
            passData.src      = src;
            passData.material = _settings.crtMaterial;

            builder.UseTexture(src);
            builder.SetRenderAttachment(dest, 0);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc(static (CRTPassData data, RasterGraphContext ctx) =>
            {
                Blitter.BlitTexture(ctx.cmd, data.src,
                    new Vector4(1, 1, 0, 0), data.material, 0);
            });
        }

        // Pass 2: copy temp texture kembali ke camera color
        // activeColorTexture read-only — buat pass manual dengan Blitter
        using (var builder = renderGraph.AddRasterRenderPass<CRTPassData>(
            "CRT Copy Back", out var copyData, profilingSampler))
        {
            copyData.src      = dest;
            copyData.material = null;

            builder.UseTexture(dest);
            builder.SetRenderAttachment(src, 0);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc(static (CRTPassData data, RasterGraphContext ctx) =>
            {
                Blitter.BlitTexture(ctx.cmd, data.src,
                    new Vector4(1, 1, 0, 0), 0, false);
            });
        }
    }

    public void Dispose() { }
}