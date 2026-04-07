Shader "Custom/2D/SoftRadialHalo2D_Fixed"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _GlowColor ("Glow Color", Color) = (1, 0.55, 0.15, 1)
        _Intensity ("Intensity", Range(0, 10)) = 2.5
        _Opacity ("Opacity", Range(0, 1)) = 0.7

        _InnerRadius ("Inner Radius", Range(0, 1.5)) = 0.0
        _OuterRadius ("Outer Radius", Range(0.01, 2.0)) = 0.9

        _CenterX ("Center X", Range(0, 1)) = 0.5
        _CenterY ("Center Y", Range(0, 1)) = 0.5
        _Softness ("Softness", Range(0.01, 4)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            Tags { "LightMode"="Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
            };

            // SpriteRenderer 요구사항용
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                half _Intensity;
                half _Opacity;
                half _InnerRadius;
                half _OuterRadius;
                half _CenterX;
                half _CenterY;
                half _Softness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 center = float2(_CenterX, _CenterY);
                float2 p = (IN.uv - center) * 2.0;

                float dist = length(p);

                float mask = 1.0 - smoothstep(_InnerRadius, _OuterRadius, dist);
                mask = saturate(pow(mask, _Softness));

                half alpha = mask * _Opacity * IN.color.a;
                half3 rgb = _GlowColor.rgb * _Intensity * alpha * IN.color.rgb;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}