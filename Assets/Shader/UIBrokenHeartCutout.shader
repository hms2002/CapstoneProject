Shader "UI/Broken Heart Cutout"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Side ("Side", Range(-1, 1)) = -1
        _CutX ("Cut X", Range(0, 1)) = 0.5
        _JaggedAmplitude ("Jagged Amplitude", Range(0, 0.18)) = 0.055
        _JaggedFrequency ("Jagged Frequency", Range(1, 24)) = 9
        _JaggedPhase ("Jagged Phase", Range(0, 6.28318)) = 0.7
        _CutFeather ("Cut Feather", Range(0.0001, 0.06)) = 0.01

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
            fixed4 _Color;
            float _Side;
            float _CutX;
            float _JaggedAmplitude;
            float _JaggedFrequency;
            float _JaggedPhase;
            float _CutFeather;
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

            float ResolveJaggedCut(float y)
            {
                float wave = sin((y + _JaggedPhase) * _JaggedFrequency * 6.28318);
                float tooth = abs(frac(y * _JaggedFrequency + _JaggedPhase) * 2.0 - 1.0) * 2.0 - 1.0;
                return saturate(_CutX + (wave * 0.55 + tooth * 0.45) * _JaggedAmplitude);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, IN.texcoord) * IN.color;
                float cut = ResolveJaggedCut(IN.texcoord.y);
                float feather = max(_CutFeather, 0.0001);
                float leftMask = 1.0 - smoothstep(cut, cut + feather, IN.texcoord.x);
                float rightMask = smoothstep(cut - feather, cut, IN.texcoord.x);
                float sideMask = _Side < 0.0 ? leftMask : rightMask;
                color.a *= sideMask;

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
