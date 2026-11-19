// Oyuncak görünümlü bloklar (yılan, köprü, göz...). URP Lit yerine basit bir
// "toon" ışık: üst yüz kendi renginde, yanlar daha koyu ve doygun, düşen gölge
// renkli bir tonla çarpılır. Referans (Level_13.png, 3× yakın plan): yeşil üst
// RGB(84,198,88), ön yan RGB(29,102,15); pembe üst (244,83,168), yan (174,23,110).
//
// Yan renk = pow(üst, _SideGamma) · _SideScale (lineer uzayda). pow > 1 koyu
// yüzü doygunlaştırır. Yuvarlatılmış kenarlarda normal yumuşak geçtiği için
// üst/yan arası da yumuşak geçer.
Shader "SnakeBlockJam/ToonBlock"
{
    Properties
    {
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _RoadShading("Road end shading", Range(0, 1)) = 0
        _SideScale("Side Scale", Range(0, 1)) = 0.42
        _SideGamma("Side Gamma", Range(1, 2)) = 1.2
        _LightInfluence("Directional Light Influence", Range(0, 1)) = 0.15
        _ShadowTint("Received Shadow Tint", Color) = (0.80, 0.76, 0.75, 1)

        // Eski Lit materyalinden kalan alanlar (BoardView bazılarını ayarlıyor).
        _Smoothness("Smoothness (unused)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _SideScale;
            half _SideGamma;
            half _LightInfluence;
            half4 _ShadowTint;
            half _Smoothness;
            half _RoadShading;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardToon"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ _BURROW_RIM
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half2 localXZ : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.localXZ = input.positionOS.xz * 2.0h;
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                half3 top = _BaseColor.rgb * (1.0h - 0.70h * _RoadShading * saturate(input.localXZ.y * input.localXZ.y));
                #if defined(_BURROW_RIM)
                    half angle = atan2(input.localXZ.y, input.localXZ.x);
                    half band = cos(6.0h * angle + 3.5h * length(input.localXZ));
                    top *= lerp(0.66h, 1.42h, smoothstep(0.22h, 0.36h, band));
                #endif
                half3 side = pow(max(top, 1e-4h), _SideGamma) * _SideScale;

                // Üst yüz (n.y = 1) kendi renginde; dik yanlar koyu.
                half up = smoothstep(0.15h, 0.95h, n.y);
                half3 color = lerp(side, top, up);

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndl = saturate(dot(n, light.direction));
                color *= lerp(1.0h, 0.85h + 0.3h * ndl, _LightInfluence);
                color *= lerp(_ShadowTint.rgb, 1.0h, light.shadowAttenuation);

                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirection = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirection = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half DepthFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
