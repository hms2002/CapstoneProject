Shader "UI/Pulse Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _GlowColor ("Glow Color", Color) = (1.0, 0.9, 0.4, 1.0)
        _CoreAlpha ("Core Alpha", Range(0, 1)) = 0.2
        _GlowAlpha ("Outer Glow Alpha", Range(0, 2)) = 0.9
        _GlowSize ("Glow Size (px)", Range(0, 6)) = 1
        _PulseSpeed ("Pulse Speed", Range(0, 20)) = 8
        _PulseMin ("Pulse Min", Range(0, 1)) = 0.5
        _PulseMax ("Pulse Max", Range(0, 2)) = 1.0
        _AlphaThreshold ("Alpha Threshold", Range(0, 1)) = 0.1

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
            fixed4 _GlowColor;
            float _CoreAlpha;
            float _GlowAlpha;
            float _GlowSize;
            float _PulseSpeed;
            float _PulseMin;
            float _PulseMax;
            float _AlphaThreshold;
            float4 _ClipRect;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            float SampleAlpha(float2 uv)
            {
                return tex2D(_MainTex, uv).a;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, IN.texcoord) * IN.color;
                float baseAlpha = baseCol.a;

                float2 px = _MainTex_TexelSize.xy * max(_GlowSize, 0.0);

                float neighborAlpha = 0.0;
                neighborAlpha = max(neighborAlpha, SampleAlpha(IN.texcoord + float2(-px.x, 0)));
                neighborAlpha = max(neighborAlpha, SampleAlpha(IN.texcoord + float2( px.x, 0)));
                neighborAlpha = max(neighborAlpha, SampleAlpha(IN.texcoord + float2(0, -px.y)));
                neighborAlpha = max(neighborAlpha, SampleAlpha(IN.texcoord + float2(0,  px.y)));
                neighborAlpha = max(neighborAlpha, SampleAlpha(IN.texcoord + float2(-px.x, -px.y)));
                neighborAlpha = max(neighborAlpha, SampleAlpha(IN.texcoord + float2(-px.x,  px.y)));
                neighborAlpha = max(neighborAlpha, SampleAlpha(IN.texcoord + float2( px.x, -px.y)));
                neighborAlpha = max(neighborAlpha, SampleAlpha(IN.texcoord + float2( px.x,  px.y)));

                float coreMask = step(_AlphaThreshold, baseAlpha) * baseAlpha;
                float glowMask = saturate(neighborAlpha - baseAlpha);
                float pulse = lerp(_PulseMin, _PulseMax, (sin(_Time.y * _PulseSpeed) + 1.0) * 0.5);

                fixed4 outCol;
                outCol.rgb = _GlowColor.rgb;
                outCol.a = saturate((coreMask * _CoreAlpha + glowMask * _GlowAlpha) * pulse) * _GlowColor.a * IN.color.a;

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
