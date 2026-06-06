Shader "UI/Affection Gradient Border"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

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
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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
                float4 texcoord : TEXCOORD0;
                float4 texcoord1 : TEXCOORD1;
                float4 texcoord2 : TEXCOORD2;
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float4 data0         : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 data1         : TEXCOORD2;
                float4 data2         : TEXCOORD3;
            };

            fixed4 _Color;
            float4 _ClipRect;

            float Smooth01(float value)
            {
                value = saturate(value);
                return value * value * (3.0 - 2.0 * value);
            }

            float ResolveEdgeGlow(float distanceFromEdge, float fadeDistance, float falloff)
            {
                float normalizedDistance = saturate(distanceFromEdge / max(fadeDistance, 0.0001));
                float glow = 1.0 - Smooth01(normalizedDistance);
                return pow(saturate(glow), max(falloff, 0.001));
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.color = IN.color * _Color;
                OUT.data0 = IN.texcoord;
                OUT.data1 = IN.texcoord1;
                OUT.data2 = IN.texcoord2;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = saturate(IN.data0.xy);
                float revealProgress = saturate(IN.data0.z);
                float intensity = saturate(IN.data0.w);

                float borderThicknessRatio = clamp(IN.data1.x, 0.001, 0.5);
                float cornerBlend = saturate(IN.data1.y / 0.35);
                float gradientStrength = max(IN.data1.z, 0.0);
                float gradientSoftness = max(IN.data1.w, 0.001);

                float gradientFalloff = max(IN.data2.x, 0.001);
                float revealFeatherRatio = max(IN.data2.y, 0.001);
                float aspect = max(IN.data2.z, 0.0001);

                float2 axisScale = float2(max(aspect, 1.0), max(1.0 / aspect, 1.0));
                float fadeDistance = max(borderThicknessRatio * gradientSoftness, 0.0001);
                float revealOffset = (1.0 - revealProgress) * fadeDistance * (1.0 + revealFeatherRatio);

                float leftDistance = uv.x * axisScale.x + revealOffset;
                float rightDistance = (1.0 - uv.x) * axisScale.x + revealOffset;
                float bottomDistance = uv.y * axisScale.y + revealOffset;
                float topDistance = (1.0 - uv.y) * axisScale.y + revealOffset;

                float horizontalGlow = max(
                    ResolveEdgeGlow(leftDistance, fadeDistance, gradientFalloff),
                    ResolveEdgeGlow(rightDistance, fadeDistance, gradientFalloff));
                float verticalGlow = max(
                    ResolveEdgeGlow(bottomDistance, fadeDistance, gradientFalloff),
                    ResolveEdgeGlow(topDistance, fadeDistance, gradientFalloff));

                float edgeGlow = max(horizontalGlow, verticalGlow);
                float additiveCornerGlow = 1.0 - (1.0 - horizontalGlow) * (1.0 - verticalGlow);
                float gradient = lerp(edgeGlow, additiveCornerGlow, cornerBlend);

                float alpha = saturate(gradient * gradientStrength) * intensity;

                fixed4 color = IN.color;
                color.a *= alpha;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
