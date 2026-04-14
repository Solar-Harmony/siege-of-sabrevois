using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace Sabrevois.Level.Water
{
    public class WaterRipplesRenderFeature : ScriptableRendererFeature
    {
        class CustomRenderPass : ScriptableRenderPass
        {
            public ComputeShader computeShader;
            public int resolution = 512;
            public float dampening = 0.99f;
            public float propagationSpeed = 0.5f;
            public float areaSize = 50f;
            public Vector2 waterOrigin = Vector2.zero;
            
            private RTHandle rt0;
            private RTHandle rt1;
            private RTHandle rt2;
            
            private int state = 0;

            public CustomRenderPass()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            }

            public void Setup()
            {
                if (rt0 == null)
                {
                    rt0 = RTHandles.Alloc(resolution, resolution, colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, filterMode: FilterMode.Bilinear, enableRandomWrite: true, name: "WaterRipplesRT0");
                    rt1 = RTHandles.Alloc(resolution, resolution, colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, filterMode: FilterMode.Bilinear, enableRandomWrite: true, name: "WaterRipplesRT1");
                    rt2 = RTHandles.Alloc(resolution, resolution, colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, filterMode: FilterMode.Bilinear, enableRandomWrite: true, name: "WaterRipplesRT2");
                    state = 0;
                }
            }

            [System.Obsolete]
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (computeShader == null || rt0 == null) return;

                CommandBuffer cmd = CommandBufferPool.Get("WaterRipplesCompute");

                int kernelInit = computeShader.FindKernel("CSInit");
                int threadGroupsX = Mathf.CeilToInt(resolution / 8.0f);
                int threadGroupsY = Mathf.CeilToInt(resolution / 8.0f);

                if (state == 0)
                {
                    cmd.SetComputeTextureParam(computeShader, kernelInit, "ResultInit", rt0);
                    cmd.DispatchCompute(computeShader, kernelInit, threadGroupsX, threadGroupsY, 1);
                    cmd.SetComputeTextureParam(computeShader, kernelInit, "ResultInit", rt1);
                    cmd.DispatchCompute(computeShader, kernelInit, threadGroupsX, threadGroupsY, 1);
                    cmd.SetComputeTextureParam(computeShader, kernelInit, "ResultInit", rt2);
                    cmd.DispatchCompute(computeShader, kernelInit, threadGroupsX, threadGroupsY, 1);
                }

                int kernelMain = computeShader.FindKernel("CSMain");

                int prevStateIndex = state % 3;
                int currStateIndex = (state + 1) % 3;
                int nextStateIndex = (state + 2) % 3;

                RTHandle[] rts = new[] { rt0, rt1, rt2 };

                threadGroupsX = Mathf.CeilToInt(resolution / 8.0f);
                threadGroupsY = Mathf.CeilToInt(resolution / 8.0f);

                int kernelDisturb = computeShader.FindKernel("CSDisturb");
                Vector4[] distArray = new Vector4[64];
                int distCount = 0;

                while (WaterRipplesInteraction.TryGetDisturbance(out var disturbance))
                {
                    distArray[distCount++] = new Vector4(disturbance.position.x, disturbance.position.y, disturbance.radius, disturbance.strength);
                    if (distCount == 64)
                    {
                        cmd.SetComputeVectorParam(computeShader, "WaterOrigin", new Vector4(waterOrigin.x, waterOrigin.y, 0, 0));
                        cmd.SetComputeFloatParam(computeShader, "AreaSize", areaSize);
                        cmd.SetComputeIntParam(computeShader, "DisturbCount", distCount);
                        cmd.SetComputeVectorArrayParam(computeShader, "Disturbances", distArray);
                        cmd.SetComputeTextureParam(computeShader, kernelDisturb, "Result", rts[currStateIndex]);
                        cmd.DispatchCompute(computeShader, kernelDisturb, threadGroupsX, threadGroupsY, 1);
                        distCount = 0;
                    }
                }

                if (distCount > 0)
                {
                    cmd.SetComputeVectorParam(computeShader, "WaterOrigin", new Vector4(waterOrigin.x, waterOrigin.y, 0, 0));
                    cmd.SetComputeFloatParam(computeShader, "AreaSize", areaSize);
                    cmd.SetComputeIntParam(computeShader, "DisturbCount", distCount);
                    cmd.SetComputeVectorArrayParam(computeShader, "Disturbances", distArray);
                    cmd.SetComputeTextureParam(computeShader, kernelDisturb, "Result", rts[currStateIndex]);
                    cmd.DispatchCompute(computeShader, kernelDisturb, threadGroupsX, threadGroupsY, 1);
                }

                cmd.SetComputeFloatParam(computeShader, "Dampening", dampening);
                cmd.SetComputeFloatParam(computeShader, "PropagationSpeed", propagationSpeed);
                cmd.SetComputeFloatParam(computeShader, "AreaSize", areaSize);
                
                cmd.SetComputeTextureParam(computeShader, kernelMain, "PrevState", rts[prevStateIndex]);
                cmd.SetComputeTextureParam(computeShader, kernelMain, "CurrentState", rts[currStateIndex]);
                cmd.SetComputeTextureParam(computeShader, kernelMain, "Result", rts[nextStateIndex]);

                cmd.DispatchCompute(computeShader, kernelMain, threadGroupsX, threadGroupsY, 1);

                cmd.SetGlobalTexture("_WaterRipplesTex", rts[nextStateIndex]);
                cmd.SetGlobalFloat("_WaterRipplesAreaSize", areaSize);
                cmd.SetGlobalVector("_WaterRipplesOrigin", new Vector4(waterOrigin.x, waterOrigin.y, 0, 0));

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);

                // Assuming RenderGraph handles incrementing `state` in its branch, we increment it here too for the fallback.
                state++;
            }

            private class PassData
            {
                public ComputeShader compute;
                public float damp;
                public float propSpeed;
                public int res;
                public int stateIndex;
                public float areaSize;
                public Vector2 waterOrigin;
                public RTHandle r0;
                public RTHandle r1;
                public RTHandle r2;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (computeShader == null || rt0 == null) return;

                using (var builder = renderGraph.AddUnsafePass<PassData>("WaterRipplesComputeGraph", out var passData))
                {
                    passData.compute = computeShader;
                    passData.damp = dampening;
                    passData.propSpeed = propagationSpeed;
                    passData.res = resolution;
                    passData.stateIndex = state;
                    passData.areaSize = areaSize;
                    passData.waterOrigin = waterOrigin;
                    passData.r0 = rt0;
                    passData.r1 = rt1;
                    passData.r2 = rt2;

                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                        int kernelMain = data.compute.FindKernel("CSMain");

                        int prevStateIndex = data.stateIndex % 3;
                        int currStateIndex = (data.stateIndex + 1) % 3;
                        int nextStateIndex = (data.stateIndex + 2) % 3;

                        RTHandle[] rts = new[] { data.r0, data.r1, data.r2 };

                        int threadGroupsX = Mathf.CeilToInt(data.res / 8.0f);
                        int threadGroupsY = Mathf.CeilToInt(data.res / 8.0f);

                        // If state is 0, initialize all 3 textures to 0 flat to prevent infinite NaN propagation
                        if (data.stateIndex <= 0)
                        {
                            int kernelInit = data.compute.FindKernel("CSInit");
                            cmd.SetComputeTextureParam(data.compute, kernelInit, "ResultInit", rts[0]);
                            cmd.DispatchCompute(data.compute, kernelInit, threadGroupsX, threadGroupsY, 1);
                            cmd.SetComputeTextureParam(data.compute, kernelInit, "ResultInit", rts[1]);
                            cmd.DispatchCompute(data.compute, kernelInit, threadGroupsX, threadGroupsY, 1);
                            cmd.SetComputeTextureParam(data.compute, kernelInit, "ResultInit", rts[2]);
                            cmd.DispatchCompute(data.compute, kernelInit, threadGroupsX, threadGroupsY, 1);
                        }

                        int kernelDisturb = data.compute.FindKernel("CSDisturb");
                        Vector4[] distArrayRG = new Vector4[64];
                        int distCountRG = 0;

                        while (WaterRipplesInteraction.TryGetDisturbance(out var disturbance))
                        {
                            distArrayRG[distCountRG++] = new Vector4(disturbance.position.x, disturbance.position.y, disturbance.radius, disturbance.strength);
                            if (distCountRG == 64)
                            {
                                cmd.SetComputeVectorParam(data.compute, "WaterOrigin", new Vector4(data.waterOrigin.x, data.waterOrigin.y, 0, 0));
                                cmd.SetComputeFloatParam(data.compute, "AreaSize", data.areaSize);
                                cmd.SetComputeIntParam(data.compute, "DisturbCount", distCountRG);
                                cmd.SetComputeVectorArrayParam(data.compute, "Disturbances", distArrayRG);
                                cmd.SetComputeTextureParam(data.compute, kernelDisturb, "Result", rts[currStateIndex]);
                                cmd.DispatchCompute(data.compute, kernelDisturb, threadGroupsX, threadGroupsY, 1);
                                distCountRG = 0;
                            }
                        }

                        if (distCountRG > 0)
                        {
                            cmd.SetComputeVectorParam(data.compute, "WaterOrigin", new Vector4(data.waterOrigin.x, data.waterOrigin.y, 0, 0));
                            cmd.SetComputeFloatParam(data.compute, "AreaSize", data.areaSize);
                            cmd.SetComputeIntParam(data.compute, "DisturbCount", distCountRG);
                            cmd.SetComputeVectorArrayParam(data.compute, "Disturbances", distArrayRG);
                            cmd.SetComputeTextureParam(data.compute, kernelDisturb, "Result", rts[currStateIndex]);
                            cmd.DispatchCompute(data.compute, kernelDisturb, threadGroupsX, threadGroupsY, 1);
                        }

                        cmd.SetComputeFloatParam(data.compute, "Dampening", data.damp);
                        cmd.SetComputeFloatParam(data.compute, "PropagationSpeed", data.propSpeed);
                        cmd.SetComputeFloatParam(data.compute, "AreaSize", data.areaSize);
                        
                        cmd.SetComputeTextureParam(data.compute, kernelMain, "PrevState", rts[prevStateIndex]);
                        cmd.SetComputeTextureParam(data.compute, kernelMain, "CurrentState", rts[currStateIndex]);
                        cmd.SetComputeTextureParam(data.compute, kernelMain, "Result", rts[nextStateIndex]);

                        cmd.DispatchCompute(data.compute, kernelMain, threadGroupsX, threadGroupsY, 1);

                        // Process main wave propagation
                        cmd.SetGlobalTexture("_WaterRipplesTex", rts[nextStateIndex]);
                        cmd.SetGlobalFloat("_WaterRipplesAreaSize", data.areaSize);
                        cmd.SetGlobalVector("_WaterRipplesOrigin", new Vector4(data.waterOrigin.x, data.waterOrigin.y, 0, 0));
                    });
                }
                state++;
            }

            public void Cleanup()
            {
                rt0?.Release();
                rt1?.Release();
                rt2?.Release();
            }
        }

        [System.Serializable]
        public class Settings
        {
            public ComputeShader computeShader;
            
            [Tooltip("Texture resolution for the wave simulation data.")]
            public int resolution = 512;
            
            [Range(0.9f, 1.0f)] 
            [Tooltip("How quickly waves settle over time. 0.99 is long ripples, 0.9 is short.")]
            public float dampening = 0.99f;
            
            [Range(0.01f, 1.0f)]
            [Tooltip("Speed at which waves travel. Lower values make the ripples slower.")]
            public float propagationSpeed = 0.15f;
            
            [Tooltip("How many world-space units the resolution grid covers. If your water plane is 100x100 units, set this to 100.")]
            public float areaSize = 50f;
            
            [Tooltip("The World Space Center (X, Z) of your water plane. Helps shader map UVs correctly.")]
            public Vector2 waterOrigin = Vector2.zero;
        }

        public Settings settings = new Settings();
        CustomRenderPass m_ScriptablePass;

        public override void Create()
        {
            m_ScriptablePass = new CustomRenderPass
            {
                computeShader = settings.computeShader,
                resolution = settings.resolution,
                dampening = settings.dampening,
                propagationSpeed = settings.propagationSpeed,
                areaSize = settings.areaSize,
                waterOrigin = settings.waterOrigin
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            m_ScriptablePass.Setup();
            renderer.EnqueuePass(m_ScriptablePass);
        }

        protected override void Dispose(bool disposing)
        {
            m_ScriptablePass.Cleanup();
            base.Dispose(disposing);
        }
    }
}

