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
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionHCS  : SV_POSITION;
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
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(int, _WoundSliceIndex)
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

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionOS = input.positionOS.xyz;

                if (_EnableBillboard > 0.5) 
                {
                    // Construct a horizontal billboard rotation entirely on the GPU
                    float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
                    float3 viewDir = _WorldSpaceCameraPos - centerWS;
                    viewDir.y = 0; // Horizontal rotation only
                    float len = length(viewDir);
                    if (len > 0.001) viewDir /= len; else viewDir = float3(0,0,-1);
                    
                    float3 upWS = float3(0, 1, 0);
                    float3 rightWS = cross(upWS, viewDir);
                    
                    // Extract non-uniform scales accurately from the matrix!
                    float scaleX = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                    float scaleY = length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y));
                    
                    float3 billboardPosWS = centerWS + rightWS * positionOS.x * scaleX + upWS * positionOS.y * scaleY;
                    output.positionHCS = TransformWorldToHClip(billboardPosWS);
                }
                else
                {
                    output.positionHCS = TransformObjectToHClip(positionOS);
                }

                output.uv = input.uv;
                return output;
            }

            half4 SampleLayerRaw(float2 uv, int index)
            {
                index = clamp(index, 0, _LayerCount - 1);
                half4 c = SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, uv, index);
                
                // Advanced HSV Chroma Keying for incredibly robust despilling and isolation!
                float3 hsv = RGBtoHSV(c.rgb);
                float3 key1HSV = RGBtoHSV(_ChromaKey1.rgb);
                float3 key2HSV = RGBtoHSV(_ChromaKey2.rgb);
                
                // Hue distance wraps around 1.0!
                float hd1 = abs(hsv.x - key1HSV.x); hd1 = min(hd1, 1.0 - hd1);
                float hd2 = abs(hsv.x - key2HSV.x); hd2 = min(hd2, 1.0 - hd2);
                
                // Weight the Hue much harder than saturation/value to cleanly kill pure greens
                float d1 = length(float3(hd1 * 2.0, abs(hsv.y - key1HSV.y)*0.5, abs(hsv.z - key1HSV.z)*0.2));
                float d2 = length(float3(hd2 * 2.0, abs(hsv.y - key2HSV.y)*0.5, abs(hsv.z - key2HSV.z)*0.2));
                
                float d = min(d1, d2);
                float chromaMask = smoothstep(_ChromaTolerance, _ChromaTolerance + _ChromaSoftness, d);
                c.a = min(c.a, chromaMask);
                
                return c;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                int sliceIndex = (int)UNITY_ACCESS_INSTANCED_PROP(Props, _WoundSliceIndex);

                float splatVal = SAMPLE_TEXTURE2D_ARRAY(_GlobalWoundSplatmap, sampler_GlobalWoundSplatmap, input.uv, sliceIndex).r;
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv * _NoiseScale).r;

                // Modulate splat value by noise to get jagged rims
                float depth = splatVal > 0.01 ? max(0.0, splatVal + (noise - 0.5) * _NoiseStrength) : 0.0;
                
                int layerIndex = clamp((int)floor(depth), 0, _LayerCount - 1);
                half4 finalColor = SampleLayerRaw(input.uv, layerIndex);
                
                float progressToNextLayer = frac(depth);
                
                float rimStart = max(0.0, 1.0 - _RimThickness);
                if (layerIndex < _LayerCount - 1 && progressToNextLayer > rimStart) 
                {
                    float rimBlend = smoothstep(rimStart, rimStart + _RimSoftness, progressToNextLayer);
                    
                    // Simulate physical tissue depth/thickness using warped inner textures!
                    float2 rimUV = input.uv + (noise - 0.5) * _NoiseUVOffset;
                    half4 rimTexColor = SampleLayerRaw(rimUV, layerIndex + _RimLayerOffset);
                    
                    // Darken the "walls" of the hole to give the wound fake self-shadowed depth
                    half3 darkenedRim = rimTexColor.rgb * (1.0 - _RimDarken);
                    half3 darkenedFinal = finalColor.rgb * (1.0 - _RimDarken);
                    
                    // If the sampled rim pixel is transparent (edge of body), fall back to original layer
                    half3 rimTargetColor = lerp(darkenedFinal, darkenedRim, rimTexColor.a);
                    
                    finalColor.rgb = lerp(finalColor.rgb, rimTargetColor, rimBlend);
                }

                if (finalColor.a < 0.1)
                    discard;

                return finalColor;
            }
            ENDHLSL
        }
    }
}
