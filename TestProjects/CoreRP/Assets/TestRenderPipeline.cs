using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

//class CullerStatisticsDebug
//{
//    public CullerStatistics stats;

//    public void RegisterCullingStatsDebug(List<DebugUI.Widget> widgets)
//    {
//        widgets.AddRange(
//            new DebugUI.Widget[]
//            {
//                new DebugUI.Foldout
//                {
//                    displayName = "Renderers",
//                    children =
//                    {
//                        new DebugUI.Value { displayName = "Tested Objects", getter = () => stats.renderers.culling.testedObjects },
//                        new DebugUI.Value { displayName = "Visible Objects", getter = () => stats.renderers.culling.visibleObjects},
//                        new DebugUI.Value { displayName = "Main thread Objects", getter = () => stats.renderers.mainThreadObjectCount},
//                    }
//                 },
//            });
//        widgets.AddRange(
//            new DebugUI.Widget[]
//            {
//                new DebugUI.Foldout
//                {
//                    displayName = "Lights",
//                    children =
//                    {
//                        new DebugUI.Value { displayName = "Tested Objects", getter = () => stats.lights.culling.testedObjects },
//                        new DebugUI.Value { displayName = "Visible Objects", getter = () => stats.lights.culling.visibleObjects},
//                    }
//                 },
//            });
//        widgets.AddRange(
//            new DebugUI.Widget[]
//            {
//                new DebugUI.Foldout
//                {
//                    displayName = "Reflection Probes",
//                    children =
//                    {
//                        new DebugUI.Value { displayName = "Tested Objects", getter = () => stats.reflectionProbes.culling.testedObjects },
//                        new DebugUI.Value { displayName = "Visible Objects", getter = () => stats.reflectionProbes.culling.visibleObjects},
//                    }
//                 },
//            });
//    }
//}

class CullingDebugParameters
{
    public bool                     useNewCulling = false;
    public bool                     freezeVisibility = false;
    public bool                     enableJobs = false;
    public bool                     gatherStats = false;
    public CullingTestMask          disabledTests = 0;
    //public CullerStatisticsDebug    statistics = new CullerStatisticsDebug();
}

[ExecuteInEditMode]
public class TestRenderPipeline : UnityEngine.Rendering.RenderPipeline
{
    Culler                          m_Culler = new Culler();
    RenderersCullingResult          m_Result = new RenderersCullingResult();
    static ShaderTagId[]            m_PassNames = { new ShaderTagId("Forward") };
    RendererListSettings[]          m_RendererListSettings = {
                                                            new RendererListSettings(renderQueueMin: (int)RenderQueue.Geometry, renderQueueMax: (int)RenderQueue.GeometryLast, shaderPassNames: m_PassNames),
                                                            new RendererListSettings(renderQueueMin: (int)RenderQueue.Transparent, renderQueueMax: (int)RenderQueue.Transparent + 100, shaderPassNames: m_PassNames),
                                                            };
    RendererList[]                  m_RendererLists = { new RendererList(), new RendererList() };
    LightCullingResult              m_LightResult = new LightCullingResult();
    ReflectionProbeCullingResult    m_ProbeResult = new ReflectionProbeCullingResult();
    CullingDebugParameters          m_CullingDebug = new CullingDebugParameters();
    CullingParameters               m_CullingParameters = new CullingParameters();

    TestRenderPipelineAsset m_Asset;

    public TestRenderPipeline(TestRenderPipelineAsset asset)
    {
        m_Asset = asset;

        //List<DebugUI.Widget> widgets = new List<DebugUI.Widget>();
        //widgets.AddRange(
        //    new DebugUI.Widget[]
        //    {
        //        new DebugUI.BoolField { displayName = "Enable New Culling", getter = () => m_CullingDebug.useNewCulling, setter = value => m_CullingDebug.useNewCulling = value },
        //        new DebugUI.BoolField { displayName = "Enable Culling Jobs", getter = () => m_CullingDebug.enableJobs, setter = value => m_CullingDebug.enableJobs = value },
        //        new DebugUI.BitField  { displayName = "Disabled Tests", getter = () => m_CullingDebug.disabledTests, setter = value => m_CullingDebug.disabledTests = (CullingTestMask)value, enumType = typeof(CullingTestMask) },
        //        new DebugUI.BoolField { displayName = "Gather Statistics", getter = () => m_CullingDebug.gatherStats, setter = value => m_CullingDebug.gatherStats = value },
        //        new DebugUI.BoolField { displayName = "Freeze Visibility", getter = () => m_CullingDebug.freezeVisibility, setter = value => m_CullingDebug.freezeVisibility = value },
        //    });

        //m_CullingDebug.statistics.RegisterCullingStatsDebug(widgets);

        //var panel = DebugManager.instance.GetPanel("Culling", true);
        //panel.flags |= DebugUI.Flags.EditorForceUpdate;
        //panel.children.Add(widgets.ToArray());
    }

    protected override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        foreach (var camera in cameras)
        {
            var cmd = CommandBufferPool.Get("");

            renderContext.SetupCameraProperties(camera);

            if (!m_CullingDebug.freezeVisibility)
                ScriptableCulling.FillCullingParameters(camera, ref m_CullingParameters);

            m_CullingParameters.enableJobs = m_CullingDebug.enableJobs;
            m_CullingParameters.extractLightProbes = true;
            m_CullingParameters.gatherStatistics = m_CullingDebug.gatherStats;
            m_CullingParameters.cullingTestParameters.testMask = CullingTestMask.Occlusion | CullingTestMask.Frustum | CullingTestMask.CullingMask | CullingTestMask.LODMask | CullingTestMask.FlagMaskNot;// | CullingTest.SceneMask;
            m_CullingParameters.cullingTestParameters.testMask &= ~m_CullingDebug.disabledTests;
            m_CullingParameters.cullingTestParameters.cullingFlagsMaskNot = CullingFlags.CastShadowsOnly;

            //if (camera.useOcclusionCulling)
            //    cullingParameters.parameters.cullingFlags |= CullFlag.OcclusionCull;

            CullingResults oldResult;
            ScriptableCullingParameters oldCullingParameters = new ScriptableCullingParameters();

            Light dirLight = null;

            if (!camera.TryGetCullingParameters(camera.stereoEnabled, out oldCullingParameters)) // Fixme remove stereo passdown?
            {
                renderContext.Submit();
                continue;
            }

            if (camera.useOcclusionCulling)
                oldCullingParameters.cullingOptions |= CullingOptions.OcclusionCull;

            oldResult = renderContext.Cull(ref oldCullingParameters);

            foreach (var light in oldResult.visibleLights)
            {
                if (light.lightType == LightType.Directional)
                {
                    dirLight = light.light;
                }

                break;
            }

            if (m_CullingDebug.useNewCulling)
            {
                dirLight = null;

                m_Culler.CullRenderers(m_CullingParameters, m_Result);

                m_CullingParameters.cullingTestParameters.testMask = CullingTestMask.Occlusion | CullingTestMask.Frustum | CullingTestMask.CullingMask | CullingTestMask.ComputeScreenRect;
                m_CullingParameters.cullingTestParameters.testMask &= ~m_CullingDebug.disabledTests;
                m_Culler.CullLights(m_CullingParameters, m_LightResult);
                m_CullingParameters.cullingTestParameters.testMask = CullingTestMask.Occlusion | CullingTestMask.Frustum | CullingTestMask.CullingMask;
                m_CullingParameters.cullingTestParameters.testMask &= ~m_CullingDebug.disabledTests;
                m_Culler.CullReflectionProbes(m_CullingParameters, m_ProbeResult);

                //m_CullingDebug.statistics.stats = m_Culler.GetStatistics();

                m_Culler.PreparePerObjectData(m_Result, m_LightResult, m_ProbeResult);

                if (dirLight == null)
                {
                    foreach (var light in m_LightResult.visibleLights)
                    {
                        if (light.lightType == LightType.Directional)
                        {
                            dirLight = light.light;
                            break;
                        }
                    }
                }
            }

            if (dirLight != null)
            {
                Vector3 foward = -dirLight.transform.forward;
                Vector4 color = dirLight.color * dirLight.intensity;
                cmd.SetGlobalVector("_LightDirection", new Vector4(foward.x, foward.y, foward.z, 0.0f));
                cmd.SetGlobalVector("_LightColor", color);
            }
            else
            {
                cmd.SetGlobalVector("_LightDirection", Vector4.zero);
                cmd.SetGlobalVector("_LightColor", Vector4.zero);
            }

            // Render Objects
            if (m_CullingDebug.useNewCulling)
            {
                RendererList.PrepareRendererLists(m_Result, m_RendererListSettings, m_RendererLists);
            }

            int texID = Shader.PropertyToID("_CameraDepthBuffer");
            cmd.GetTemporaryRT(texID, camera.pixelWidth, camera.pixelHeight, 1, FilterMode.Point, RenderTextureFormat.Depth);

            CoreUtils.SetRenderTarget(cmd, BuiltinRenderTextureType.CameraTarget, texID);
            cmd.ClearRenderTarget(true, true, Color.grey);

            if (m_CullingDebug.useNewCulling)
            {
                // Opaque
                cmd.DrawRenderers(m_RendererLists[0], new DrawRendererSettings_New());
                // Transparent
                cmd.DrawRenderers(m_RendererLists[1], new DrawRendererSettings_New());
            }
            else
            {
                renderContext.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var sortingSettings = new SortingSettings(camera)
                {
                    criteria = SortingCriteria.CommonOpaque
                };

                var drawSettings = new DrawingSettings(new ShaderTagId("Forward"), sortingSettings);
                var filterSettings = new FilteringSettings(
                    new RenderQueueRange()
                    {
                        upperBound = (int)RenderQueue.GeometryLast,
                        lowerBound = (int)RenderQueue.Geometry
                    }
                );
                var filterSettingsTransparent = new FilteringSettings(
                new RenderQueueRange()
                    {
                        upperBound = (int)RenderQueue.Transparent + 100,
                        lowerBound = (int)RenderQueue.Transparent
                    }
                );

                renderContext.DrawRenderers(oldResult, ref drawSettings, ref filterSettings);
                renderContext.DrawRenderers(oldResult, ref drawSettings, ref filterSettingsTransparent);
            }

            renderContext.ExecuteCommandBuffer(cmd);
            renderContext.Submit();
            CommandBufferPool.Release(cmd);
        }

        renderContext.Submit();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        //DebugManager.instance.RemovePanel("Culling");
    }
}
