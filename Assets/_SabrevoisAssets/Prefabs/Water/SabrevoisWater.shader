// Sabrevois tessellated water.
// DX11/12 tessellation that displaces the water plane by the ripple/wind heightfield
// (_WaterRipplesTex.a) so the waves are real 3D geometry. Lighting uses the same
// simulation's normals (_WaterRipplesTex.rgb) for a consistent surface.
//
// Performance: tessellation factors are distance-based (near camera = dense,
// far = coarse), the base grid is a uniform subdivided plane (WaterTessellationMesh),
// and the displacement is a single texture fetch in the domain shader.

Shader "Sabrevois/WaterTessellation"
{
    Properties
    {
        _Color ("Water Color", Color) = (0.31, 0.5, 0.66, 0.45)
        _ShallowColor ("Shallow Color", Color) = (0.37, 0.68, 0.85, 1)
        _DeepColor ("Deep Color", Color) = (0.06, 0.08, 0.31, 1)
        _MaxDepth ("Max Depth", Float) = 5
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3
        _Reflectivity ("Reflectivity", Range(0, 1)) = 0.9
        _Roughness ("Roughness", Range(0, 1)) = 0.15
        _HeightScale ("Wave Height Scale", Range(0, 3)) = 1
        _FoamColor ("Foam Color", Color) = (0.87, 0.87, 0.87, 1)
        _FoamThreshold ("Foam Threshold", Range(0, 2)) = 0.5
        _FoamSoftness ("Foam Softness", Range(0.01, 2)) = 0.3
        _FoamStrength ("Foam Strength", Range(0, 1)) = 0.6
        [Header(Tessellation)]
        _TessFactorNear ("Tessellation Near Factor", Range(1, 32)) = 10
        _TessFactorFar ("Tessellation Far Factor", Range(1, 8)) = 1
        _TessNearDist ("Tessellation Near Distance", Float) = 5
        _TessFarDist ("Tessellation Far Distance", Float) = 400
        _TessMax ("Tessellation Max Factor", Range(1, 32)) = 16
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 5.0
            #pragma vertex vert
            #pragma hull hull
            #pragma domain domain
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                half _MaxDepth;
                half _FresnelPower;
                half _Reflectivity;
                half _Roughness;
                half _HeightScale;
                half _FoamThreshold;
                half _FoamSoftness;
                half _FoamStrength;
                half _TessFactorNear;
                half _TessFactorFar;
                half _TessNearDist;
                half _TessFarDist;
                half _TessMax;
            CBUFFER_END

            // Simulation globals set by WaterRipplesRenderFeature every frame.
            float4 _WaterRipplesOrigin;
            float _WaterRipplesAreaSize;

            TEXTURE2D(_WaterRipplesTex);   SAMPLER(sampler_WaterRipplesTex);
            TEXTURECUBE(_PlayerCubemap);   SAMPLER(sampler_PlayerCubemap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct ControlPoint
            {
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            ControlPoint vert(Attributes input)
            {
                ControlPoint output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float CalcTessFactor(float3 positionWS)
            {
                float dist = distance(positionWS, _WorldSpaceCameraPos);
                float t = saturate((dist - _TessNearDist) / max(_TessFarDist - _TessNearDist, 0.001));
                return clamp(lerp(_TessFactorNear, _TessFactorFar, t), 1.0, _TessMax);
            }

            struct PatchTess
            {
                float edge[3] : SV_TessFactor;
                float inside : SV_InsideTessFactor;
            };

            // Edge factors from each edge midpoint so adjacent patches agree (no cracks).
            PatchTess PatchConstantFunction(InputPatch<ControlPoint, 3> patch)
            {
                PatchTess tess;
                float3 e0 = (patch[0].positionWS + patch[1].positionWS) * 0.5;
                float3 e1 = (patch[1].positionWS + patch[2].positionWS) * 0.5;
                float3 e2 = (patch[2].positionWS + patch[0].positionWS) * 0.5;
                tess.edge[0] = CalcTessFactor(e0);
                tess.edge[1] = CalcTessFactor(e1);
                tess.edge[2] = CalcTessFactor(e2);
                tess.inside = (tess.edge[0] + tess.edge[1] + tess.edge[2]) * (1.0 / 3.0);
                return tess;
            }

            [domain("tri")]
            [partitioning("fractional_odd")]
            [outputtopology("triangle_cw")]
            [patchconstantfunc("PatchConstantFunction")]
            [outputcontrolpoints(3)]
            ControlPoint hull(InputPatch<ControlPoint, 3> patch, uint id : SV_OutputControlPointID)
            {
                return patch[id];
            }

            struct DomainOutput
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            [domain("tri")]
            DomainOutput domain(const OutputPatch<ControlPoint, 3> patch, float3 bary : SV_DomainLocation, const PatchTess tessFactors)
            {
                DomainOutput output;
                float3 positionWS = bary.x * patch[0].positionWS + bary.y * patch[1].positionWS + bary.z * patch[2].positionWS;
                float2 uv = bary.x * patch[0].uv + bary.y * patch[1].uv + bary.z * patch[2].uv;

                // Displace the surface by the ripple/wind simulation heightfield.
                float2 simUV = (positionWS.xz - _WaterRipplesOrigin.xy) / _WaterRipplesAreaSize + 0.5;
                float waveHeight = _WaterRipplesTex.SampleLevel(sampler_WaterRipplesTex, simUV, 0).a;
                positionWS.y += waveHeight * _HeightScale;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = uv;
                output.positionWS = positionWS;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(DomainOutput input) : SV_Target
            {
                float2 simUV = (input.positionWS.xz - _WaterRipplesOrigin.xy) / _WaterRipplesAreaSize + 0.5;
                float4 ripples = _WaterRipplesTex.SampleLevel(sampler_WaterRipplesTex, simUV, 0);

                float3 normalWS = ripples.rgb;
                if (length(normalWS) < 0.5)
                    normalWS = float3(0.0, 1.0, 0.0);
                normalWS = normalize(normalWS);
                float waveHeight = ripples.a;

                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Depth-based water tint.
                float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float waterEye = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                float depth = saturate(sceneEye - waterEye);
                float depthFactor = saturate(depth / _MaxDepth);
                half3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthFactor) * _Color.rgb;

                // Fresnel + cubemap reflection.
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                float3 reflectDirWS = reflect(-viewDirWS, normalWS);
                half3 reflectionColor = SAMPLE_TEXTURECUBE(_PlayerCubemap, sampler_PlayerCubemap, reflectDirWS).rgb;

                // Main light diffuse + specular.
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS);
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float spec = pow(saturate(dot(normalWS, halfDir)), (1.0 - _Roughness) * 128.0 + 8.0);

                half3 color = waterColor * (ambient + mainLight.color * NdotL);
                color += mainLight.color * spec * NdotL * (1.0 - _Roughness);
                color = lerp(color, reflectionColor * _Reflectivity, fresnel);

                // Crest foam from wave height.
                float foam = smoothstep(_FoamThreshold, _FoamThreshold + _FoamSoftness, abs(waveHeight));
                color = lerp(color, _FoamColor.rgb, foam * _FoamStrength);

                half alpha = lerp(_Color.a, 1.0, fresnel * _Reflectivity);

                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
