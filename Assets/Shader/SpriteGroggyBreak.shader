Shader "Custom/SpriteGroggyBreak"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0

        [Header(Break)]
        _BreakProgress ("Break Progress", Range(0, 1)) = 0
        _ShardScatter ("Shard Scatter", Range(0, 0.2)) = 0.035
        _NoiseScale ("Noise Scale", Range(1, 64)) = 18
        _VerticalBias ("Vertical Bias", Range(-1, 1)) = 0.15

        [Header(Edge)]
        _EdgeColor ("Edge Color", Color) = (1.0, 0.85, 0.35, 1.0)
        _EdgeWidth ("Edge Width", Range(0.001, 0.2)) = 0.04
        _EdgeEmission ("Edge Emission", Range(0, 4)) = 1.5

        [Header(Fade)]
        _AlphaFade ("Alpha Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "UnitySprites.cginc"

            fixed4 _EdgeColor;
            float _BreakProgress;
            float _ShardScatter;
            float _NoiseScale;
            float _VerticalBias;
            float _EdgeWidth;
            float _EdgeEmission;
            float _AlphaFade;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);

                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            /// <summary>
            /// 책임 :
            /// - 깨짐 진행도에 따라 sprite UV를 약간 흩뜨리고, 노이즈 임계값으로 픽셀을 제거한다.
            /// - 잘려나가는 경계에는 EdgeColor를 발광처럼 얹어 "깨지는 순간"의 읽힘을 강화한다.
            /// </summary>
            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float2 centeredUv = uv - 0.5;
                float radial = saturate(length(centeredUv) * 1.4);

                float noise = valueNoise(uv * _NoiseScale);
                float directionalBias = ((1.0 - uv.y) * 0.5) + (radial * 0.35) + _VerticalBias;
                float breakThreshold = saturate(_BreakProgress + directionalBias - 0.35);

                float2 scatterDirection = normalize(centeredUv + float2(0.001, 0.12));
                float2 scatteredUv = uv - scatterDirection * (_BreakProgress * _ShardScatter * (0.35 + noise));

                fixed4 baseCol = SampleSpriteTexture(scatteredUv) * IN.color;
                if (baseCol.a <= 0.001f)
                    discard;

                float breakMask = step(noise, breakThreshold);
                if (breakMask >= 1.0)
                    discard;

                float edgeMask = 1.0 - saturate(abs(noise - breakThreshold) / max(_EdgeWidth, 0.0001));
                fixed3 edgeRgb = _EdgeColor.rgb * _EdgeEmission * edgeMask * _EdgeColor.a;
                fixed3 whiteSilhouette = fixed3(1.0, 1.0, 1.0);
                fixed3 finalRgb = saturate(whiteSilhouette + edgeRgb);
                float finalAlpha = baseCol.a * saturate((1.0 - _BreakProgress) + (edgeMask * 0.25)) * _AlphaFade;

                return fixed4(finalRgb, finalAlpha);
            }
            ENDCG
        }
    }
}
