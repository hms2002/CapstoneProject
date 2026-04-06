Shader "UI/Alpha Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineThickness ("Outline Thickness (px)", Range(0, 8)) = 1
        _AlphaThreshold ("Alpha Threshold", Range(0,1)) = 0.1
        _InnerPadding ("Inner Padding UV (L,B,R,T)", Vector) = (0.03, 0.03, 0.03, 0.03)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineThickness;
            float _AlphaThreshold;
            float4 _InnerPadding;
            float4 _ClipRect;

            float2 GetInsetMin()
            {
                return saturate(_InnerPadding.xy);
            }

            float2 GetInsetMax()
            {
                return saturate(1.0 - _InnerPadding.zw);
            }

            float2 GetInsetSpan()
            {
                float2 minUv = GetInsetMin();
                float2 maxUv = GetInsetMax();
                return max(maxUv - minUv, float2(1e-5, 1e-5));
            }

            bool IsInsideInset(float2 uv)
            {
                float2 minUv = GetInsetMin();
                float2 maxUv = GetInsetMax();
                return uv.x >= minUv.x && uv.x <= maxUv.x &&
                       uv.y >= minUv.y && uv.y <= maxUv.y;
            }

            fixed4 SampleInsetSprite(float2 uv)
            {
                if (!IsInsideInset(uv))
                    return fixed4(0, 0, 0, 0);

                float2 minUv = GetInsetMin();
                float2 span = GetInsetSpan();
                float2 sampleUv = saturate((uv - minUv) / span);
                return tex2D(_MainTex, sampleUv);
            }

            float SampleInsetAlpha(float2 uv)
            {
                return SampleInsetSprite(uv).a;
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 baseCol = SampleInsetSprite(IN.texcoord) * IN.color;
                float baseA = baseCol.a;
                float inside = step(_AlphaThreshold, baseA);

                float2 px = _MainTex_TexelSize.xy * GetInsetSpan() * max(_OutlineThickness, 0.0);

                float sampleA = 0.0;
                sampleA = max(sampleA, SampleInsetAlpha(IN.texcoord + float2(-px.x, 0)));
                sampleA = max(sampleA, SampleInsetAlpha(IN.texcoord + float2( px.x, 0)));
                sampleA = max(sampleA, SampleInsetAlpha(IN.texcoord + float2(0, -px.y)));
                sampleA = max(sampleA, SampleInsetAlpha(IN.texcoord + float2(0,  px.y)));
                sampleA = max(sampleA, SampleInsetAlpha(IN.texcoord + float2(-px.x, -px.y)));
                sampleA = max(sampleA, SampleInsetAlpha(IN.texcoord + float2(-px.x,  px.y)));
                sampleA = max(sampleA, SampleInsetAlpha(IN.texcoord + float2( px.x, -px.y)));
                sampleA = max(sampleA, SampleInsetAlpha(IN.texcoord + float2( px.x,  px.y)));

                float neighbor = step(_AlphaThreshold, sampleA);
                float outlineMask = neighbor * (1.0 - inside);

                fixed4 outCol = baseCol;
                outCol.rgb = lerp(outCol.rgb, _OutlineColor.rgb, outlineMask);
                outCol.a = max(outCol.a, _OutlineColor.a * IN.color.a * outlineMask);

                #ifdef UNITY_UI_CLIP_RECT
                outCol.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(outCol.a - 0.001);
                #endif

                return outCol;
            }
            ENDCG
        }
    }
}
