Shader "Custom/2D/SpriteOuterPixelOutline2D"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1,1,1,1)

        _OutlineColor ("Outline Color", Color) = (1, 0.85, 0.2, 1)
        _OutlineThickness ("Outline Thickness (px)", Range(1, 4)) = 1

        [HDR] _EmissionColor ("Emission Color", Color) = (1, 0.85, 0.2, 1)
        _EmissionStrength ("Emission Strength", Range(0, 8)) = 0
        _AlphaThreshold ("Alpha Threshold", Range(0, 1)) = 0.01
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
        Blend SrcAlpha OneMinusSrcAlpha

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _OutlineColor;
                half4 _EmissionColor;
                half  _OutlineThickness;
                half  _EmissionStrength;
                half  _AlphaThreshold;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half AlphaAt(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 baseTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 baseCol = baseTex * _Color * IN.color;

                // 원본 스프라이트 픽셀은 절대 안 건드림
                if (baseTex.a > _AlphaThreshold)
                {
                    return baseCol;
                }

                float2 stepUV = _MainTex_TexelSize.xy * max(_OutlineThickness, 1.0h);

                // 8방향 이웃 검사
                half aU  = AlphaAt(IN.uv + float2(0,  stepUV.y));
                half aD  = AlphaAt(IN.uv + float2(0, -stepUV.y));
                half aR  = AlphaAt(IN.uv + float2( stepUV.x, 0));
                half aL  = AlphaAt(IN.uv + float2(-stepUV.x, 0));

                half aUR = AlphaAt(IN.uv + float2( stepUV.x,  stepUV.y));
                half aUL = AlphaAt(IN.uv + float2(-stepUV.x,  stepUV.y));
                half aDR = AlphaAt(IN.uv + float2( stepUV.x, -stepUV.y));
                half aDL = AlphaAt(IN.uv + float2(-stepUV.x, -stepUV.y));

                half neighborMax = max(max(max(aU, aD), max(aR, aL)), max(max(aUR, aUL), max(aDR, aDL)));

                // 현재는 투명, 주변에 본체가 있으면 outline
                half outlineMask = step(_AlphaThreshold, neighborMax);

                if (outlineMask <= 0.0h)
                {
                    return half4(0,0,0,0);
                }

                half4 outlineCol = _OutlineColor;
                outlineCol.rgb += _EmissionColor.rgb * _EmissionStrength * _EmissionColor.a;
                outlineCol.a *= outlineMask;

                return outlineCol;
            }
            ENDHLSL
        }
    }
}