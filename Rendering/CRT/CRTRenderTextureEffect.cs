using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Pasang pada Camera yang Output Texture-nya ke RT (Camera 1/2/3).
/// Renderer camera ini harus pakai PC_Renderer yang sudah ada CRTRendererFeature.
/// Script ini handle pixelation — render ke RT kecil dulu, baru upscale ke output RT.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CRTRenderTextureEffect : MonoBehaviour
{
    [Header("CRT Material")]
    public Material crtMaterial;

    [Header("Pixelation")]
    public bool  enablePixelation = true;
    public int   pixelWidth       = 320;
    public int   pixelHeight      = 240;

    private Camera         _cam;
    private RenderTexture  _pixelRT;   // resolusi rendah
    private RenderTexture  _outputRT;  // RT asli yang di-assign di camera
    private static readonly int TimePropID = Shader.PropertyToID("_Time_Custom");

    private void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        // Simpan RT output asli
        _outputRT = _cam.targetTexture;

        if (!enablePixelation || crtMaterial == null)
            return;

        // Buat RT kecil untuk render pixelated
        _pixelRT = new RenderTexture(pixelWidth, pixelHeight, 24)
        {
            filterMode = FilterMode.Point,
            wrapMode   = TextureWrapMode.Clamp,
            antiAliasing = 1,
        };
        _pixelRT.Create();

        // Camera render ke RT kecil dulu
        _cam.targetTexture = _pixelRT;

        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

        // Kembalikan RT output asli
        if (_cam != null && _outputRT != null)
            _cam.targetTexture = _outputRT;

        if (_pixelRT != null)
        {
            _pixelRT.Release();
            _pixelRT = null;
        }
    }

    private void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != _cam) return;
        if (_pixelRT == null || _outputRT == null || crtMaterial == null) return;

        crtMaterial.SetFloat(TimePropID, Time.time);

        // Blit pixelRT → outputRT dengan CRT effect
        var cmd = CommandBufferPool.Get("CRT Blit");
        cmd.Blit(_pixelRT, _outputRT, crtMaterial);
        ctx.ExecuteCommandBuffer(cmd);
        ctx.Submit();
        CommandBufferPool.Release(cmd);
    }

    private void OnValidate()
    {
        if (Application.isPlaying && enabled)
        {
            OnDisable();
            OnEnable();
        }
    }
}