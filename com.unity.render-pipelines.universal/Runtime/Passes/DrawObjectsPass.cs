using System;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Profiling;

namespace UnityEngine.Rendering.Universal.Internal
{
    /// <summary>
    /// Draw  objects into the given color and depth target
    ///
    /// You can use this pass to render objects that have a material and/or shader
    /// with the pass names UniversalForward or SRPDefaultUnlit.
    /// </summary>
    public class DrawObjectsPass : ScriptableRenderPass
    {
        FilteringSettings m_FilteringSettings;
        RenderStateBlock m_RenderStateBlock;
        List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();
        string m_ProfilerTag;
        ProfilingSampler m_ProfilingSampler;
        bool m_IsOpaque;

        bool m_UseDepthPriming;

        static readonly int s_DrawObjectPassDataPropID = Shader.PropertyToID("_DrawObjectPassData");

        /// <summary>
        /// Creates a new <c>DrawObjectsPass</c> instance.
        /// </summary>
        /// <param name="profilerTag"></param>
        /// <param name="shaderTagIds"></param>
        /// <param name="opaque"></param>
        /// <param name="evt">The <c>RenderPassEvent</c> to use.</param>
        /// <param name="renderQueueRange"></param>
        /// <param name="layerMask"></param>
        /// <param name="stencilState"></param>
        /// <param name="stencilReference"></param>
        /// <seealso cref="ShaderTagId"/>
        /// <seealso cref="RenderPassEvent"/>
        /// <seealso cref="RenderQueueRange"/>
        /// <seealso cref="LayerMask"/>
        /// <seealso cref="StencilState"/>
        public DrawObjectsPass(string profilerTag, ShaderTagId[] shaderTagIds, bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask, StencilState stencilState, int stencilReference)
        {
            base.profilingSampler = new ProfilingSampler(nameof(DrawObjectsPass));

            m_ProfilerTag = profilerTag;
            m_ProfilingSampler = new ProfilingSampler(profilerTag);
            foreach (ShaderTagId sid in shaderTagIds)
                m_ShaderTagIdList.Add(sid);
            renderPassEvent = evt;
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            m_IsOpaque = opaque;

            if (stencilState.enabled)
            {
                m_RenderStateBlock.stencilReference = stencilReference;
                m_RenderStateBlock.mask = RenderStateMask.Stencil;
                m_RenderStateBlock.stencilState = stencilState;
            }
        }

        /// <summary>
        /// Creates a new <c>DrawObjectsPass</c> instance.
        /// </summary>
        /// <param name="profilerTag"></param>
        /// <param name="opaque"></param>
        /// <param name="evt"></param>
        /// <param name="renderQueueRange"></param>
        /// <param name="layerMask"></param>
        /// <param name="stencilState"></param>
        /// <param name="stencilReference"></param>
        /// <seealso cref="RenderPassEvent"/>
        /// <seealso cref="RenderQueueRange"/>
        /// <seealso cref="LayerMask"/>
        /// <seealso cref="StencilState"/>
        public DrawObjectsPass(string profilerTag, bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask, StencilState stencilState, int stencilReference)
            : this(profilerTag,
            new ShaderTagId[] { new ShaderTagId("SRPDefaultUnlit"), new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly") },
            opaque, evt, renderQueueRange, layerMask, stencilState, stencilReference)
        { }

        internal DrawObjectsPass(URPProfileId profileId, bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask, StencilState stencilState, int stencilReference)
            : this(profileId.GetType().Name, opaque, evt, renderQueueRange, layerMask, stencilState, stencilReference)
        {
            m_ProfilingSampler = ProfilingSampler.Get(profileId);
        }

        /// <inheritdoc />
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            PassData passData = new PassData();
            passData.m_RenderingData = renderingData;
            passData.m_IsOpaque = m_IsOpaque;
            passData.m_RenderStateBlock = m_RenderStateBlock;
            passData.m_FilteringSettings = m_FilteringSettings;
            passData.m_ShaderTagIdList = m_ShaderTagIdList;
            passData.m_ProfilingSampler = m_ProfilingSampler;

            CameraSetup(cmd, passData);
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            PassData passData = new PassData();
            passData.m_RenderingData = renderingData;
            passData.m_IsOpaque = m_IsOpaque;
            passData.m_RenderStateBlock = m_RenderStateBlock;
            passData.m_FilteringSettings = m_FilteringSettings;
            passData.m_ShaderTagIdList = m_ShaderTagIdList;
            passData.m_ProfilingSampler = m_ProfilingSampler;

            ExecutePass(context, passData, renderingData.cameraData.IsCameraProjectionMatrixFlipped());
        }

        private static void CameraSetup(CommandBuffer cmd, PassData data)
        {
            if (data.m_RenderingData.cameraData.renderer.useDepthPriming && data.m_IsOpaque && (data.m_RenderingData.cameraData.renderType == CameraRenderType.Base || data.m_RenderingData.cameraData.clearDepth))
            {
                data.m_RenderStateBlock.depthState = new DepthState(false, CompareFunction.Equal);
                data.m_RenderStateBlock.mask |= RenderStateMask.Depth;
            }
            else if (data.m_RenderStateBlock.depthState.compareFunction == CompareFunction.Equal)
            {
                data.m_RenderStateBlock.depthState = new DepthState(true, CompareFunction.LessEqual);
                data.m_RenderStateBlock.mask |= RenderStateMask.Depth;
            }
        }

        private static void ExecutePass(ScriptableRenderContext context, PassData data, bool yFlip)
        {
            var renderingData = data.m_RenderingData;
            var cmd = renderingData.commandBuffer;
            using (new ProfilingScope(cmd, data.m_ProfilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                // Global render pass data containing various settings.
                // x,y,z are currently unused
                // w is used for knowing whether the object is opaque(1) or alpha blended(0)
                Vector4 drawObjectPassData = new Vector4(0.0f, 0.0f, 0.0f, (data.m_IsOpaque) ? 1.0f : 0.0f);
                cmd.SetGlobalVector(s_DrawObjectPassDataPropID, drawObjectPassData);

                // scaleBias.x = flipSign
                // scaleBias.y = scale
                // scaleBias.z = bias
                // scaleBias.w = unused
                float flipSign = yFlip ? -1.0f : 1.0f; ; //(renderingData.cameraData.IsCameraProjectionMatrixFlipped()) ? -1.0f : 1.0f;
                Vector4 scaleBias = (flipSign < 0.0f)
                    ? new Vector4(flipSign, 1.0f, -1.0f, 1.0f)
                    : new Vector4(flipSign, 0.0f, 1.0f, 1.0f);
                cmd.SetGlobalVector(ShaderPropertyId.scaleBiasRt, scaleBias);

                Camera camera = renderingData.cameraData.camera;
                var sortFlags = (data.m_IsOpaque) ? renderingData.cameraData.defaultOpaqueSortFlags : SortingCriteria.CommonTransparent;
                if (renderingData.cameraData.renderer.useDepthPriming && data.m_IsOpaque && (renderingData.cameraData.renderType == CameraRenderType.Base || renderingData.cameraData.clearDepth))
                    sortFlags = SortingCriteria.SortingLayer | SortingCriteria.RenderQueue | SortingCriteria.OptimizeStateChanges | SortingCriteria.CanvasOrder;

                var filterSettings = data.m_FilteringSettings;

#if UNITY_EDITOR
                // When rendering the preview camera, we want the layer mask to be forced to Everything
                if (renderingData.cameraData.isPreviewCamera)
                {
                    filterSettings.layerMask = -1;
                }
#endif

                DrawingSettings drawSettings = CreateDrawingSettings(data.m_ShaderTagIdList, ref renderingData, sortFlags);

                var activeDebugHandler = GetActiveDebugHandler(renderingData);
                if (activeDebugHandler != null)
                {
                    activeDebugHandler.DrawWithDebugRenderState(context, cmd, ref renderingData, ref drawSettings, ref filterSettings, ref data.m_RenderStateBlock,
                        (ScriptableRenderContext ctx, ref RenderingData data, ref DrawingSettings ds, ref FilteringSettings fs, ref RenderStateBlock rsb) =>
                        {
                            ctx.DrawRenderers(data.cullResults, ref ds, ref fs, ref rsb);
                        });
                }
                else
                {
                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings, ref data.m_RenderStateBlock);

                    // Render objects that did not match any shader pass with error shader
                    RenderingUtils.RenderObjectsWithError(context, ref renderingData.cullResults, camera, filterSettings, SortingCriteria.None);
                }
            }
        }

        private class PassData
        {
            public TextureHandle m_Albedo;
            public TextureHandle m_Depth;

            public RenderingData m_RenderingData;

            public bool m_IsOpaque;
            public RenderStateBlock m_RenderStateBlock;
            public FilteringSettings m_FilteringSettings;
            public List<ShaderTagId> m_ShaderTagIdList;
            public ProfilingSampler m_ProfilingSampler;
        }

        public void Render(TextureHandle colorTarget, TextureHandle depthTarget, TextureHandle mainShadowsTexture, TextureHandle additionalShadowsTexture, ref RenderingData renderingData)
        {
            RenderGraph graph = renderingData.renderGraph;
            Camera camera = renderingData.cameraData.camera;

            using (var builder = graph.AddRenderPass<PassData>("Draw Objects Pass", out var passData,
                new ProfilingSampler("Draw Objects Pass Profiler")))
            {
                passData.m_Albedo = builder.UseColorBuffer(colorTarget, 0);
                passData.m_Depth = builder.UseDepthBuffer(depthTarget, DepthAccess.Write);

                if (mainShadowsTexture.IsValid())
                    builder.ReadTexture(mainShadowsTexture);
                if (additionalShadowsTexture.IsValid())
                    builder.ReadTexture(additionalShadowsTexture);

                passData.m_RenderingData = renderingData;

                builder.AllowPassCulling(false);

                passData.m_IsOpaque = m_IsOpaque;
                passData.m_RenderStateBlock = m_RenderStateBlock;
                passData.m_FilteringSettings = m_FilteringSettings;
                passData.m_ShaderTagIdList = m_ShaderTagIdList;
                passData.m_ProfilingSampler = m_ProfilingSampler;

                builder.SetRenderFunc((PassData data, RenderGraphContext context) =>
                {
                    // TODO RENDERGRAPH add proper yFlip logic
                    bool renderToBackbuffer = false;

                    CameraSetup(context.cmd, data);
                    ExecutePass(context.renderContext, data, !renderToBackbuffer);
                });

            }
        }

    }
}
