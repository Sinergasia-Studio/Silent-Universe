using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using Mygame.Rendering.CRT;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CRTToggle : MonoBehaviour
{
    private CRTRendererFeature cRT;

    private void Start()
    {
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null)
        {
            Debug.LogError("NO pipeline");
            return;
        }

        // Try common public property first (newer URP versions)
        try
        {
            var rendererDataListProp = pipeline.GetType().GetProperty("rendererDataList", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (rendererDataListProp != null)
            {
                var listObj = rendererDataListProp.GetValue(pipeline) as IEnumerable;
                if (listObj != null)
                {
                    foreach (var rd in listObj)
                    {
                        // rd may be a ScriptableRendererData
                        var srd = rd as ScriptableRendererData;
                        if (srd == null) continue;
                        foreach (var feature in srd.rendererFeatures)
                        {
                            if (feature is CRTRendererFeature found)
                            {
                                cRT = found;
                                return;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception) { /* ignore and fallback */ }

        // Fallback: try to call GetRenderer(index) to obtain runtime ScriptableRenderer and its features
        var getRendererMethod = pipeline.GetType().GetMethod("GetRenderer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (getRendererMethod != null)
        {
            for (int i = 0; ; i++)
            {
                object rendererObj;
                try
                {
                    rendererObj = getRendererMethod.Invoke(pipeline, new object[] { i });
                }
                catch
                {
                    break; // no more renderers or method failed
                }
                if (rendererObj == null) break;

                // Try to get rendererFeatures field/property from the renderer instance
                var rendererType = rendererObj.GetType();
                FieldInfo rfField = rendererType.GetField("rendererFeatures", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                PropertyInfo rfProp = rendererType.GetProperty("rendererFeatures", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                IEnumerable features = null;
                if (rfField != null)
                    features = rfField.GetValue(rendererObj) as IEnumerable;
                else if (rfProp != null)
                    features = rfProp.GetValue(rendererObj) as IEnumerable;

                if (features == null) continue;

                foreach (var f in features)
                {
                    if (f is CRTRendererFeature found)
                    {
                        cRT = found;
                        return;
                    }
                }
            }
        }

        Debug.LogError("CRTRendererFeature not found in URP renderer features.");
    }

    public void Toggle()
    {
        if (cRT == null)
        {
            Debug.LogWarning("CRTRendererFeature not assigned.");
            return;
        }

        cRT.SetEnabled(!cRT.IsEnabled());
        Debug.Log($"CRT toggled: {cRT.IsEnabled()}");
    }
}
