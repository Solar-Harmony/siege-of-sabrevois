Shader "Sabrevois/BillboardWounds"
{
    Properties
    {
        _MainTex ("Sprite Texture Layers (Texture2DArray)", 2DArray) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _WoundSliceIndex ("Wound Slice Index", Float) = 0
        _LayerCount ("Layer Count", Int) = 2
        
        [Header(Chroma Keying)]
        _ChromaKey1 ("Chroma Key Color 1 (RGB)", Color) = (0, 1, 0, 1)
        _ChromaKey2 ("Chroma Key Color 2 (RGB)", Color) = (1, 0, 1, 1)
        _ChromaTolerance ("Chroma Tolerance", Range(0.0, 1.0)) = 0.02
        _ChromaSoftness ("Chroma Softness Mask", Range(0.001, 1.0)) = 0.05

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
        float3 normalOS     : NORMAL;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float2 uv           : TEXCOORD0;
        float4 positionHCS  : SV_POSITION;
        float3 positionWS   : TEXCOORD1;
        float3 normalWS     : TEXCOORD2;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    TEXTURE2D_ARRAY(_MainTex);
    SAMPLER(sampler_MainTex);

    TEXTURE2D(_NoiseTex);
    SAMPLER(sampler_NoiseTex);

    TEXTURE2D_ARRAY(_GlobalWoundSplatmap);
    SAMPLER(sampler_GlobalWoundSplatmap);

    CBUFFER_START(UnityPerMaterial)
        int _LayerCount;
        half4 _ChromaKey1;
        half4 _ChromaKey2;
        float _ChromaTolerance;
        float _ChromaSoftness;
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

    float3 RGBtoHSV(float3 arg1)
    {
        float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
        float4 p = lerp(float4(arg1.bg, K.wz), float4(arg1.gb, K.xy), step(arg1.b, arg1.g));
        float4 q = lerp(float4(p.xyw, arg1.r), float4(arg1.r, p.yzx), step(p.x, arg1.r));
        float d = q.x - min(q.w, q.y);
        float e = 1.0e-10;
        return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
    }

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
        return output;
    }

    half4 SampleLayerRaw(float2 uv, int index)
    {
        index = clamp(index, 0, _LayerCount - 1);
        half4 c = SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, uv, index);
        
        float3 hsv = RGBtoHSV(c.rgb);
        float3 key1HSV = RGBtoHSV(_ChromaKey1.rgb);
        float3 key2HSV = RGBtoHSV(_ChromaKey2.rgb);
        
        float hd1 = abs(hsv.x - key1HSV.x); hd1 = min(hd1, 1.0 - hd1);
        float hd2 = abs(hsv.x - key2HSV.x); hd2 = min(hd2, 1.0 - hd2);
        
        float d1 = length(float3(hd1 * 2.0, abs(hsv.y - key1HSV.y)*0.5, abs(hsv.z - key1HSV.z)*0.2));
        float d2 = length(float3(hd2 * 2.0, abs(hsv.y - key2HSV.y)*0.5, abs(hsv.z - key2HSV.z)*0.2));
        
        float d = min(d1, d2);
        float chromaMask = smoothstep(_ChromaTolerance, _ChromaTolerance + _ChromaSoftness, d);
        c.a = min(c.a, chromaMask);
        
        return c;
    }

    half4 GetFinalColor(Varyings input, out float outDepth, out float3 outNormalTS)
    {
        int sliceIndex = (int)UNITY_ACCESS_INSTANCED_PROP(Props, _WoundSliceIndex);

        float3 viewDirWS = normalize(_WorldSpaceCameraPos - input.positionWS);
        float3 flatViewDirWS = viewDirWS;
        flatViewDirWS.y = 0;
        float len = length(flatViewDirWS);
        if (len > 0.001) flatViewDirWS /= len; else flatViewDirWS = float3(0,0,-1);
        float2 viewDirUV = float2(dot(viewDirWS, cross(float3(0,1,0), flatViewDirWS)), dot(viewDirWS, float3(0,1,0)));

        float2 splatData = SAMPLE_TEXTURE2D_ARRAY(_GlobalWoundSplatmap, sampler_GlobalWoundSplatmap, input.uv, sliceIndex).rg;
        float splatVal = splatData.r;
        float hasBlood = splatData.g;
        float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv * _NoiseScale).r;

        float noiseAmount = (noise - 0.5) * _NoiseStrength * smoothstep(0.0, 0.25, splatVal);
        float depth = max(0.0, splatVal + noiseAmount);
        outDepth = depth;
        
        int layerIndex = clamp((int)floor(depth), 0, _LayerCount - 1);
        float2 paraUV1 = input.uv - viewDirUV * (layerIndex * _ParallaxStrength);
        half4 finalColor = SampleLayerRaw(paraUV1, layerIndex);
        
        float progressToNextLayer = frac(depth);
        float layerBlend = smoothstep(0.0, 0.35, progressToNextLayer);
        
        if (layerIndex < _LayerCount - 1 && progressToNextLayer > 0.01)
        {
            float2 paraUV2 = input.uv - viewDirUV * ((layerIndex + 1) * _ParallaxStrength);
            half4 nextLayerColor = SampleLayerRaw(paraUV2, layerIndex + 1);
            finalColor = lerp(finalColor, nextLayerColor, layerBlend);
        }

        if (layerIndex >= _LayerCount - 1)
        {
            float holeFeather = smoothstep(_LayerCount - 0.8, _LayerCount + 0.8, depth);
            finalColor.a *= 1.0 - holeFeather;
        }
        
        float rimBoundary = round(depth);
        float rimHalfWidth = _RimThickness * 0.5;
        float rimDist = abs(depth - rimBoundary);
        float rimBlend = 1.0 - smoothstep(max(0.0, rimHalfWidth - _RimSoftness), rimHalfWidth, rimDist);
        if (rimBoundary >= 1.0 && rimBoundary <= (float)(_LayerCount - 1) && rimBlend > 0.001)
        {
            int rimShallowLayer = (int)rimBoundary - 1;
            float2 rimParaUV = input.uv - viewDirUV * (rimShallowLayer * _ParallaxStrength);
            float2 rimUV = rimParaUV + (noise - 0.5) * _NoiseUVOffset;
            half4 rimTexColor = SampleLayerRaw(rimUV, rimShallowLayer + _RimLayerOffset);
            
            half3 darkenedRim = rimTexColor.rgb * (1.0 - _RimDarken);
            half3 darkenedFinal = finalColor.rgb * (1.0 - _RimDarken);
            half3 rimTargetColor = lerp(darkenedFinal, darkenedRim, rimTexColor.a);
            
            // Replaced unreliable ddy with a physical upward shift sampling to firmly isolate the bottom sill geometry!
            float splatUp = SAMPLE_TEXTURE2D_ARRAY(_GlobalWoundSplatmap, sampler_GlobalWoundSplatmap, input.uv + float2(0, 0.01 * _BloodAmountMultiplier), sliceIndex).r;
            float bottomFactor = saturate((splatUp - splatVal) * 10.0);
            
            float bloodAmount = bottomFactor * step(0.1, hasBlood);
            half3 bloodColor = _BloodColor.rgb;
            
            rimTargetColor = lerp(rimTargetColor, bloodColor, bloodAmount * 0.95);
            finalColor.rgb = lerp(finalColor.rgb, rimTargetColor, rimBlend);
        }

        half4 cleanBase = SampleLayerRaw(input.uv, 0);
        float woundEdge = smoothstep(0.0, 0.25, depth);
        finalColor = lerp(cleanBase, finalColor, woundEdge);

        // --- Procedural Normal Generation ---
        
        // 1. Volumetric Normal (capsule approximation based on UV)
        // Treats the 2D plane like a 3D cylindrical/spherical volume for lighting
        float2 centeredUV = input.uv * 2.0 - 1.0;
        // Dampen Y to make it more like a capsule since characters are taller than wide
        centeredUV.y *= 0.5; 
        float sqrDist = saturate(dot(centeredUV, centeredUV));
        float zDepth = sqrt(max(0.001, 1.0 - sqrDist));
        float3 volumeNormal = normalize(float3(centeredUV.x * _VolumeDepth, centeredUV.y * _VolumeDepth, zDepth));
        
        // 2. Calculate an artificial height using the luminance of the final color. 
        // Multiplying by alpha ensures a bevel along the outer edges of the sprite.
        float baseHeight = dot(finalColor.rgb, float3(0.299, 0.587, 0.114)) * finalColor.a;
        
        // 3. Use screen-space derivatives to compute normal gradients for the base texture
        float dhx = ddx(baseHeight);
        float dhy = ddy(baseHeight);
        float3 detailNormal = normalize(float3(-dhx * _BaseBumpScale, -dhy * _BaseBumpScale, 1.0));
        
        // Blend volume and detail normal
        float3 baseNormal = normalize(float3(volumeNormal.xy + detailNormal.xy, volumeNormal.z * detailNormal.z));
        
        // 4. Extract the wound normal from the procedural wound depth the same way
        float wdx = ddx(outDepth);
        float wdy = ddy(outDepth);
        float3 woundNormal = normalize(float3(-wdx * _WoundBumpScale, -wdy * _WoundBumpScale, 1.0));
        
        // 5. Combine the base sprite depth with the wound depth
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
                // Note: Standard shadow caster applies a bias to positionHCS
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