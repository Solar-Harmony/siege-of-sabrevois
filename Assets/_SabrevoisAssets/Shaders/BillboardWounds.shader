Shader "Sabrevois/BillboardWounds"
{
    Properties
    {
        _MainTex ("Sprite Atlas Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _LayerCount ("Layer Count", Int) = 2
        _LayerUV00_01 ("Layer UV0-1 (x,y / z,w)", Vector) = (0,0,0,0)
        _LayerUV02_03 ("Layer UV2-3 (x,y / z,w)", Vector) = (0,0,0,0)
        _LayerUV04_05 ("Layer UV4-5 (x,y / z,w)", Vector) = (0,0,0,0)
        _LayerUV06_07 ("Layer UV6-7 (x,y / z,w)", Vector) = (0,0,0,0)

        [Header(Outline Settings)]
        _RimLayerOffset ("Rim Tex Layer Offset (e.g. -1 for Previous)", Int) = -1
        _RimThickness ("Rim Thickness", Range(0.01, 1.0)) = 0.2
        _RimSoftness ("Rim Border Softness", Range(0.001, 0.5)) = 0.05
        _RimDarken ("Rim Darken Multiplier", Range(0.0, 1.0)) = 0.3

        [Header(GPU Billboard Settings)]
        [Toggle] _EnableBillboard ("Enable Horizontal GPU Billboard", Float) = 1.0

        [Header(Noise Settings)]
        _NoiseScale ("Noise Scale", Float) = 5.0
        _NoiseStrength ("Noise Tearing Strength", Float) = 0.8
        _NoiseUVOffset ("Rim Parallax (UV Warp)", Range(0.0, 0.2)) = 0.02

        [Header(Parallax Settings)]
        _ParallaxStrength ("Parallax Strength", Range(0.0, 0.2)) = 0.03

        [Header(Hit Feedback)]
        _HitImpulse ("Hit Impulse (X, Y, Strength, 0)", Vector) = (0, 0, 0, 0)
        _BloodColor ("Blood Color", Color) = (0.5, 0, 0, 1)
        _BloodAmountMultiplier ("Blood Prominence", Range(1.0, 10.0)) = 1.0

        [Header(Lighting)]
        _VolumeDepth ("Volume Depth (Capsule)", Range(0.0, 2.0)) = 1.0
        _BaseBumpScale ("Base Procedural Normal Scale", Range(0.0, 20.0)) = 2.0
        _WoundBumpScale ("Wound Normal Bump Scale", Range(0.0, 20.0)) = 5.0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    struct Attributes
    {
        float4 positionOS   : POSITION;
        float2 uv           : TEXCOORD0;
        float2 charUV       : TEXCOORD1;
        float3 normalOS     : NORMAL;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float2 uv           : TEXCOORD0;
        float2 charUV       : TEXCOORD3;
        float4 positionHCS  : SV_POSITION;
        float3 positionWS   : TEXCOORD1;
        float3 normalWS     : TEXCOORD2;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    TEXTURE2D(_NoiseTex);
    SAMPLER(sampler_NoiseTex);

    TEXTURE2D_ARRAY(_GlobalWoundSplatmap);
    SAMPLER(sampler_GlobalWoundSplatmap);
    float4 _GlobalWoundSplatmap_TexelSize;

    CBUFFER_START(UnityPerMaterial)
        int _LayerCount;
        float4 _LayerUV00_01;
        float4 _LayerUV02_03;
        float4 _LayerUV04_05;
        float4 _LayerUV06_07;
        int _RimLayerOffset;
        float _RimThickness;
        float _RimSoftness;
        float _RimDarken;
        float _EnableBillboard;
        float _NoiseScale;
        float _NoiseStrength;
        float _NoiseUVOffset;
        float _ParallaxStrength;
        float _VolumeDepth;
        float _BaseBumpScale;
        float _WoundBumpScale;
        half4 _BloodColor;
        float _BloodAmountMultiplier;
    CBUFFER_END

    UNITY_INSTANCING_BUFFER_START(Props)
        UNITY_DEFINE_INSTANCED_PROP(int, _WoundSliceIndex)
        UNITY_DEFINE_INSTANCED_PROP(float4, _HitImpulse)
    UNITY_INSTANCING_BUFFER_END(Props)

    Varyings vertCommon(Attributes input)
    {
        Varyings output = (Varyings)0;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);

        float3 positionOS = input.positionOS.xyz;

        float4 hitImpulse = UNITY_ACCESS_INSTANCED_PROP(Props, _HitImpulse);
        float push = ((input.uv.x - 0.5) * 2.0 * hitImpulse.x + (input.uv.y - 0.5) * 2.0 * hitImpulse.y) * hitImpulse.z;
        positionOS.z += push;

        if (_EnableBillboard > 0.5)
        {
            float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
            float3 viewDir = _WorldSpaceCameraPos - centerWS;
            viewDir.y = 0;
            float len = length(viewDir);
            if (len > 0.001) viewDir /= len; else viewDir = float3(0,0,-1);

            float3 upWS = float3(0, 1, 0);
            float3 rightWS = cross(upWS, viewDir);
            float3 forwardWS = -viewDir;

            float scaleX = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
            float scaleY = length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y));

            float3 billboardPosWS = centerWS + rightWS * positionOS.x * scaleX + upWS * positionOS.y * scaleY + forwardWS * positionOS.z;

            output.positionWS = billboardPosWS;
            output.positionHCS = TransformWorldToHClip(billboardPosWS);

            float3 trueViewDirWS = _WorldSpaceCameraPos - centerWS;
            output.normalWS = length(trueViewDirWS) > 0.001 ? normalize(trueViewDirWS) : float3(0, 0, 1);
        }
        else
        {
            output.positionWS = TransformObjectToWorld(positionOS);
            output.positionHCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
        }

        output.uv = input.uv;
        output.charUV = input.charUV;
        return output;
    }

    half4 SampleLayerAtlas(float2 uv, int layerIndex)
    {
        layerIndex = clamp(layerIndex, 0, _LayerCount - 1);
        float2 delta;
        if (layerIndex == 0) delta = _LayerUV00_01.xy;
        else if (layerIndex == 1) delta = _LayerUV00_01.zw;
        else if (layerIndex == 2) delta = _LayerUV02_03.xy;
        else if (layerIndex == 3) delta = _LayerUV02_03.zw;
        else if (layerIndex == 4) delta = _LayerUV04_05.xy;
        else if (layerIndex == 5) delta = _LayerUV04_05.zw;
        else if (layerIndex == 6) delta = _LayerUV06_07.xy;
        else delta = _LayerUV06_07.zw;
        return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + delta);
    }

    float GetWoundDepth(float2 uv, int sliceIndex)
    {
        float splat = SAMPLE_TEXTURE2D_ARRAY(_GlobalWoundSplatmap, sampler_GlobalWoundSplatmap, uv, sliceIndex).r;
        float n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv * _NoiseScale).r;
        float noiseAmount = (n - 0.5) * _NoiseStrength * smoothstep(0.0, 0.25, splat);
        return max(0.0, splat + noiseAmount);
    }

    half4 GetFinalColor(Varyings input, out float outDepth, out float3 outNormalTS)
    {
        int sliceIndex = (int)UNITY_ACCESS_INSTANCED_PROP(Props, _WoundSliceIndex);

        float2 charUV = input.charUV;
        float2 atlasUV = input.uv;

        float3 viewDirWS = normalize(_WorldSpaceCameraPos - input.positionWS);
        float3 flatViewDirWS = viewDirWS;
        flatViewDirWS.y = 0;
        float len = length(flatViewDirWS);
        if (len > 0.001) flatViewDirWS /= len; else flatViewDirWS = float3(0,0,-1);
        float3 rightWS = cross(float3(0,1,0), flatViewDirWS);
        float2 viewDirTS = float2(dot(viewDirWS, rightWS), dot(viewDirWS, float3(0,1,0)));
        float viewZ = max(dot(viewDirWS, flatViewDirWS), 0.3);
        float2 parallaxDir = viewDirTS / viewZ;

        float surfaceDepth = GetWoundDepth(charUV, sliceIndex);
        float depth = surfaceDepth;
        float2 charUVF = charUV;
        [unroll]
        for (int it = 0; it < 2; it++)
        {
            charUVF = charUV - parallaxDir * (min(depth, (float)_LayerCount) * _ParallaxStrength);
            depth = GetWoundDepth(charUVF, sliceIndex);
        }
        outDepth = depth;

        float2 splatDataF = SAMPLE_TEXTURE2D_ARRAY(_GlobalWoundSplatmap, sampler_GlobalWoundSplatmap, charUVF, sliceIndex).rg;
        float splatVal = splatDataF.r;
        float hasBlood = splatDataF.g;
        float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, charUVF * _NoiseScale).r;

        float2 parallaxOffset = charUV - charUVF;

        int layerIndex = clamp((int)floor(depth), 0, _LayerCount - 1);
        half4 finalColor = SampleLayerAtlas(atlasUV - parallaxOffset, layerIndex);

        float progressToNextLayer = frac(depth);
        float layerBlend = smoothstep(0.0, 0.35, progressToNextLayer);

        if (layerIndex < _LayerCount - 1 && progressToNextLayer > 0.01)
        {
            half4 nextLayerColor = SampleLayerAtlas(atlasUV - parallaxOffset, layerIndex + 1);
            finalColor = lerp(finalColor, nextLayerColor, layerBlend);
        }

        float holeFeather = smoothstep(_LayerCount - 0.8, _LayerCount + 0.8, surfaceDepth);
        finalColor.a *= 1.0 - holeFeather;

        float rimBoundary = round(depth);
        float rimHalfWidth = _RimThickness * 0.5;
        float rimDist = abs(depth - rimBoundary);
        float rimBlend = 1.0 - smoothstep(max(0.0, rimHalfWidth - _RimSoftness), rimHalfWidth, rimDist);
        if (rimBoundary >= 1.0 && rimBoundary <= (float)(_LayerCount - 1) && rimBlend > 0.001)
        {
            float2 rimCharUV = charUVF + (noise - 0.5) * _NoiseUVOffset;
            float2 rimAtlasUV = atlasUV - parallaxOffset + (noise - 0.5) * _NoiseUVOffset;
            half4 rimTexColor = SampleLayerAtlas(rimAtlasUV, (int)rimBoundary + _RimLayerOffset);

            half3 darkenedRim = rimTexColor.rgb * (1.0 - _RimDarken);
            half3 darkenedFinal = finalColor.rgb * (1.0 - _RimDarken);
            half3 rimTargetColor = lerp(darkenedFinal, darkenedRim, rimTexColor.a);

            float splatUp = SAMPLE_TEXTURE2D_ARRAY(_GlobalWoundSplatmap, sampler_GlobalWoundSplatmap, charUVF + float2(0, 0.01 * _BloodAmountMultiplier), sliceIndex).r;
            float bottomFactor = saturate((splatUp - splatVal) * 10.0);

            float bloodAmount = bottomFactor * step(0.1, hasBlood);
            half3 bloodColor = _BloodColor.rgb;

            rimTargetColor = lerp(rimTargetColor, bloodColor, bloodAmount * 0.95);
            finalColor.rgb = lerp(finalColor.rgb, rimTargetColor, rimBlend);
        }

        half4 cleanBase = SampleLayerAtlas(atlasUV, 0);
        float woundEdge = smoothstep(0.0, 0.25, surfaceDepth);
        finalColor = lerp(cleanBase, finalColor, woundEdge);

        float2 centeredUV = charUV * 2.0 - 1.0;
        centeredUV.y *= 0.5;
        float sqrDist = saturate(dot(centeredUV, centeredUV));
        float zDepth = sqrt(max(0.001, 1.0 - sqrDist));
        float3 volumeNormal = normalize(float3(centeredUV.x * _VolumeDepth, centeredUV.y * _VolumeDepth, zDepth));

        float baseHeight = dot(finalColor.rgb, float3(0.299, 0.587, 0.114)) * finalColor.a;

        float dhx = ddx(baseHeight);
        float dhy = ddy(baseHeight);
        float3 detailNormal = normalize(float3(-dhx * _BaseBumpScale, -dhy * _BaseBumpScale, 1.0));

        float3 baseNormal = normalize(float3(volumeNormal.xy + detailNormal.xy, volumeNormal.z * detailNormal.z));

        float2 gradStep = max(_GlobalWoundSplatmap_TexelSize.xy, 1.0 / 1024.0) * 1.5;
        float dRight = GetWoundDepth(charUVF + float2(gradStep.x, 0), sliceIndex);
        float dLeft  = GetWoundDepth(charUVF - float2(gradStep.x, 0), sliceIndex);
        float dUp    = GetWoundDepth(charUVF + float2(0, gradStep.y), sliceIndex);
        float dDown  = GetWoundDepth(charUVF - float2(0, gradStep.y), sliceIndex);
        float2 depthDelta = clamp(float2(dRight - dLeft, dUp - dDown) * 0.5, -2.0, 2.0);
        float3 woundNormal = normalize(float3(depthDelta * _WoundBumpScale, 1.0));

        outNormalTS = normalize(float3(baseNormal.xy + woundNormal.xy, baseNormal.z * woundNormal.z));

        return finalColor;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _DBUFFER
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            Varyings vert(Attributes input)
            {
                return vertCommon(input);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float outDepth = 0.0;
                float3 normalTS = float3(0,0,1);
                half4 finalColor = GetFinalColor(input, outDepth, normalTS);

                clip(finalColor.a - 0.001);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalColor.rgb;
                surfaceData.alpha = finalColor.a;
                surfaceData.metallic = 0.0;
                surfaceData.smoothness = 0.1;

                surfaceData.emission = float3(0, 0, 0);
                surfaceData.occlusion = 1.0;
                surfaceData.clearCoatMask = 0.0;
                surfaceData.clearCoatSmoothness = 0.0;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionHCS);

                float3 n;
                if (_EnableBillboard < 0.5f)
                    n = inputData.viewDirectionWS;
                else
                    n = normalize(input.normalWS);
                float3 up = abs(n.y) > 0.999 ? float3(0,0,1) : float3(0,1,0);
                float3 t = normalize(cross(up, n));
                float3 b = normalize(cross(n, t));
                half3x3 tbn = half3x3(t, b, n);
                inputData.tangentToWorld = tbn;

                float3 perturbedNormalWS = normalize(t * normalTS.x + b * normalTS.y + n * normalTS.z);
                inputData.normalWS = perturbedNormalWS;

                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                surfaceData.normalTS = float3(0, 0, 1);

                half4 litColor = UniversalFragmentPBR(inputData, surfaceData);
                litColor.a = finalColor.a;

                return litColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #pragma multi_compile_shadowcaster

            Varyings vert(Attributes input)
            {
                Varyings output = vertCommon(input);
                #if UNITY_REVERSED_Z
                output.positionHCS.z = min(output.positionHCS.z, output.positionHCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                output.positionHCS.z = max(output.positionHCS.z, output.positionHCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float dummyDepth = 0.0;
                float3 dummyNormal = float3(0,0,1);
                half4 finalColor = GetFinalColor(input, dummyDepth, dummyNormal);

                clip(finalColor.a - 0.001);

                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            Varyings vert(Attributes input)
            {
                return vertCommon(input);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float dummyDepth = 0.0;
                float3 dummyNormal = float3(0,0,1);
                half4 finalColor = GetFinalColor(input, dummyDepth, dummyNormal);

                clip(finalColor.a - 0.001);

                return 0;
            }
            ENDHLSL
        }
    }
}
