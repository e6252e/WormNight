Shader "OZ/Map/Dirt Road Texture Blend"
{
    Properties
    {
        _MainTex01 ("01 Dirt", 2D) = "white" {}
        _MainTex02 ("02 Dirt Grass Blend", 2D) = "white" {}
        _MainTex03 ("03 Grass", 2D) = "white" {}
        _ControlTex ("Terrain Control Map", 2D) = "white" {}
        _NormalTex01 ("01 Dirt Normal", 2D) = "bump" {}
        _NormalTex02 ("02 Dirt Grass Normal", 2D) = "bump" {}
        _NormalTex03 ("03 Grass Normal", 2D) = "bump" {}
        _Tint01 ("01 Tint", Color) = (1, 1, 1, 1)
        _Tint02 ("02 Tint", Color) = (1, 1, 1, 1)
        _Tint03 ("03 Tint", Color) = (1, 1, 1, 1)
        _TileSize01 ("01 World Tile Size", Float) = 5
        _TileSize02 ("02 World Tile Size", Float) = 5
        _TileSize03 ("03 World Tile Size", Float) = 6
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.75
        _BlendNoiseScale ("Blend Noise Scale", Float) = 0.16
        _BlendNoiseStrength ("Blend Noise Strength", Range(0, 0.5)) = 0.16
        _MacroTintScale ("Macro Tint Scale", Float) = 0.045
        _MacroTintStrength ("Macro Tint Strength", Range(0, 0.18)) = 0.055
        _ControlMapBlend ("Control Map Blend", Range(0, 1)) = 1
        _ControlMapCenterSize ("Control Map Center Size", Vector) = (0, 0, 200, 0)
        _DetailScale ("Procedural Detail Scale", Float) = 38
        _DetailStrength ("Procedural Detail Strength", Range(0, 0.25)) = 0.035
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.42
        _AntiTileStrength ("Anti Tile Strength", Range(0, 1)) = 0.18
        _UvWarpStrength ("UV Warp Strength", Range(0, 0.08)) = 0.012
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex01);
            SAMPLER(sampler_MainTex01);
            TEXTURE2D(_MainTex02);
            SAMPLER(sampler_MainTex02);
            TEXTURE2D(_MainTex03);
            SAMPLER(sampler_MainTex03);
            TEXTURE2D(_ControlTex);
            SAMPLER(sampler_ControlTex);
            TEXTURE2D(_NormalTex01);
            SAMPLER(sampler_NormalTex01);
            TEXTURE2D(_NormalTex02);
            SAMPLER(sampler_NormalTex02);
            TEXTURE2D(_NormalTex03);
            SAMPLER(sampler_NormalTex03);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex01_ST;
                half4 _Tint01;
                half4 _Tint02;
                half4 _Tint03;
                float _TileSize01;
                float _TileSize02;
                float _TileSize03;
                half _NormalStrength;
                float _BlendNoiseScale;
                half _BlendNoiseStrength;
                float _MacroTintScale;
                half _MacroTintStrength;
                half _ControlMapBlend;
                float4 _ControlMapCenterSize;
                float _DetailScale;
                half _DetailStrength;
                half _ShadowStrength;
                half _AntiTileStrength;
                half _UvWarpStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half3 weights : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half3 tangentWS : TEXCOORD3;
                half3 bitangentWS : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.weights = saturate(input.color.rgb);

                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = normalInputs.tangentWS;
                output.bitangentWS = normalInputs.bitangentWS;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float2 RotateUv(float2 uv, float sinValue, float cosValue)
            {
                return float2(uv.x * cosValue - uv.y * sinValue, uv.x * sinValue + uv.y * cosValue);
            }

            half3 DecodeNormal(half4 packedNormal)
            {
                packedNormal.x *= packedNormal.w;
                half3 normal;
                normal.xy = packedNormal.xy * 2.0h - 1.0h;
                normal.z = sqrt(saturate(1.0h - dot(normal.xy, normal.xy)));
                return normal;
            }

            half4 SampleAlbedo(TEXTURE2D_PARAM(textureName, samplerName), float2 uv, float2 worldXZ)
            {
                float2 warp = float2(
                    ValueNoise(worldXZ * 0.23 + 17.31) - 0.5,
                    ValueNoise(worldXZ * 0.19 + 53.72) - 0.5) * _UvWarpStrength;

                float2 uvA = uv + warp;
                float2 uvB = RotateUv(uv + float2(19.13, 7.91), 0.5, 0.8660254) * 1.17;
                half blend = (half)ValueNoise(worldXZ * 0.055 + 4.7) * _AntiTileStrength;
                return lerp(
                    SAMPLE_TEXTURE2D(textureName, samplerName, uvA),
                    SAMPLE_TEXTURE2D(textureName, samplerName, uvB),
                    blend);
            }

            half3 SampleLayerNormal(TEXTURE2D_PARAM(textureName, samplerName), float2 uv, float2 worldXZ)
            {
                float2 warp = float2(
                    ValueNoise(worldXZ * 0.21 + 7.18) - 0.5,
                    ValueNoise(worldXZ * 0.17 + 31.43) - 0.5) * _UvWarpStrength;
                return DecodeNormal(SAMPLE_TEXTURE2D(textureName, samplerName, uv + warp));
            }

            half3 NormalizeWeights(half3 weights)
            {
                half weightSum = max(weights.r + weights.g + weights.b, 0.001h);
                return weights / weightSum;
            }

            half3 ApplyBlendNoise(half3 weights, float2 worldXZ)
            {
                weights = NormalizeWeights(weights);
                half dominant = max(weights.r, max(weights.g, weights.b));
                half edgeAmount = saturate((1.0h - dominant) * 2.2h);
                half noise = (half)(ValueNoise(worldXZ * _BlendNoiseScale + 23.1) - 0.5) * _BlendNoiseStrength * edgeAmount;
                weights.r = saturate(weights.r + noise * 0.75h);
                weights.g = saturate(weights.g - noise * 0.2h);
                weights.b = saturate(weights.b - noise * 0.55h);
                return NormalizeWeights(weights);
            }

            half3 SampleControlWeights(half3 vertexWeights, float2 worldXZ)
            {
                float size = max(_ControlMapCenterSize.z, 0.01);
                float2 controlUv = (worldXZ - _ControlMapCenterSize.xy) / size + 0.5;
                half inside = step(0.0, controlUv.x) * step(controlUv.x, 1.0) * step(0.0, controlUv.y) * step(controlUv.y, 1.0);
                half3 controlWeights = SAMPLE_TEXTURE2D(_ControlTex, sampler_ControlTex, controlUv).rgb;
                controlWeights = NormalizeWeights(controlWeights);
                half blend = saturate(_ControlMapBlend * inside);
                return NormalizeWeights(lerp(vertexWeights, controlWeights, blend));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 worldXZ = input.positionWS.xz;
                half3 weights = ApplyBlendNoise(SampleControlWeights(input.weights, worldXZ), worldXZ);

                float2 uv01 = worldXZ / max(_TileSize01, 0.01);
                float2 uv02 = worldXZ / max(_TileSize02, 0.01);
                float2 uv03 = worldXZ / max(_TileSize03, 0.01);

                half4 tex01 = SampleAlbedo(TEXTURE2D_ARGS(_MainTex01, sampler_MainTex01), uv01, worldXZ) * _Tint01;
                half4 tex02 = SampleAlbedo(TEXTURE2D_ARGS(_MainTex02, sampler_MainTex02), uv02, worldXZ + 11.0) * _Tint02;
                half4 tex03 = SampleAlbedo(TEXTURE2D_ARGS(_MainTex03, sampler_MainTex03), uv03, worldXZ + 29.0) * _Tint03;
                half3 albedo = tex01.rgb * weights.r + tex02.rgb * weights.g + tex03.rgb * weights.b;

                half3 n01 = SampleLayerNormal(TEXTURE2D_ARGS(_NormalTex01, sampler_NormalTex01), uv01, worldXZ);
                half3 n02 = SampleLayerNormal(TEXTURE2D_ARGS(_NormalTex02, sampler_NormalTex02), uv02, worldXZ + 11.0);
                half3 n03 = SampleLayerNormal(TEXTURE2D_ARGS(_NormalTex03, sampler_NormalTex03), uv03, worldXZ + 29.0);
                half3 normalTS = normalize(n01 * weights.r + n02 * weights.g + n03 * weights.b);
                normalTS.xy *= _NormalStrength;
                normalTS.z = sqrt(saturate(1.0h - dot(normalTS.xy, normalTS.xy)));

                half3 normalWS = normalize(
                    input.tangentWS * normalTS.x +
                    input.bitangentWS * normalTS.y +
                    input.normalWS * normalTS.z);

                half macro = (half)(ValueNoise(worldXZ * _MacroTintScale) - 0.5) * _MacroTintStrength;
                half detailA = (half)ValueNoise(worldXZ * _DetailScale);
                half detailB = (half)ValueNoise((worldXZ + 17.37) * (_DetailScale * 0.43));
                half detail = ((detailA + detailB) * 0.5h - 0.5h) * _DetailStrength;
                albedo *= 1.0h + macro + detail;

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 bakedAmbient = SampleSH(normalWS);
                half3 direct = mainLight.color * (0.28h + ndotl * 0.72h) * mainLight.distanceAttenuation;
                half shadow = lerp(1.0h - _ShadowStrength, 1.0h, mainLight.shadowAttenuation);
                half3 lit = albedo * (bakedAmbient * 0.62h + direct * shadow);

                return half4(saturate(lit), 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
