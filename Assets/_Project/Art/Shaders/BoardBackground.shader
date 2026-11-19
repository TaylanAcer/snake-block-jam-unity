// Zemin: ekran uzayında dikey degrade + üzerine düşen yumuşak gölge.
// Referans (Level_01/13/16.png, x=30 sütunu): ekranın üstü RGB(245,249,251),
// alt kısmı RGB(210,241,252); degrade ekran yüksekliğinin ~%25'inden ~%95'ine
// kadar. Işık hesabı yok: renk, kamerayla ve tahta boyutuyla değişmez.
Shader "SnakeBlockJam/BoardBackground"
{
    Properties
    {
        _TopColor("Top Color", Color) = (0.961, 0.976, 0.984, 1)
        _BottomColor("Bottom Color", Color) = (0.824, 0.945, 0.988, 1)
        _GradientStart("Gradient Start (from top, 0-1)", Range(0, 1)) = 0.25
        _GradientEnd("Gradient End (from top, 0-1)", Range(0, 1)) = 0.95
        _ShadowTint("Shadow Tint", Color) = (0.80, 0.76, 0.75, 1)

        // BoardView eski zemin materyaline renk veriyordu; burada kullanılmıyor.
        [HideInInspector] _BaseColor("Unused", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Background"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _BottomColor;
                half _GradientStart;
                half _GradientEnd;
                half4 _ShadowTint;
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 0 = ekranın üstü, 1 = altı.
                float fromTop = 1.0 - input.screenPos.y / input.screenPos.w;
                half t = smoothstep(_GradientStart, _GradientEnd, fromTop);
                half3 color = lerp(_TopColor.rgb, _BottomColor.rgb, t);

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                color *= lerp(_ShadowTint.rgb, 1.0h, smoothstep(0.28h, 0.78h, light.shadowAttenuation));

                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
