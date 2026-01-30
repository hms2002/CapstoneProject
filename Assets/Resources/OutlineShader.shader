Shader "Sprites/Unlit Outline (Code)"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineThickness ("Outline Thickness (px)", Float) = 1
        _AlphaThreshold ("Alpha Threshold", Range(0,1)) = 0.1
        [Toggle] _OutlineEnabled ("Outline Enabled", Float) = 1
        [Toggle] _OutlineOnly ("Outline Only", Float) = 0
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;      // SpriteRenderer의 Vertex Color
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;     // (1/width, 1/height, width, height)

            fixed4 _Color;
            fixed4 _OutlineColor;
            float  _OutlineThickness;
            float  _AlphaThreshold;
            float  _OutlineEnabled;
            float  _OutlineOnly;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;

                #ifdef PIXELSNAP_ON
                o.vertex = UnityPixelSnap(o.vertex);
                #endif

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float enabled    = step(0.5, _OutlineEnabled);
                float outlineOnly = step(0.5, _OutlineOnly);

                fixed4 baseCol = tex2D(_MainTex, i.uv) * i.color;
                float baseA = baseCol.a;

                float2 px = _MainTex_TexelSize.xy * max(_OutlineThickness, 0.0);

                // 4방향 샘플 (총 5샘플)
                float aL = tex2D(_MainTex, i.uv + float2(-px.x, 0)).a;
                float aR = tex2D(_MainTex, i.uv + float2( px.x, 0)).a;
                float aU = tex2D(_MainTex, i.uv + float2(0,  px.y)).a;
                float aD = tex2D(_MainTex, i.uv + float2(0, -px.y)).a;

                float neighborA = max(max(aL, aR), max(aU, aD));

                float inside = step(_AlphaThreshold, baseA);
                float edge   = step(_AlphaThreshold, neighborA) * (1.0 - inside); // 바깥쪽만
                float mask   = edge * enabled;

                fixed4 outCol;

                if (outlineOnly > 0.5)
                {
                    outCol = _OutlineColor;
                    outCol.a *= mask;
                }
                else
                {
                    outCol = baseCol;
                    // outline 픽셀은 색을 outline로
                    outCol.rgb = lerp(outCol.rgb, _OutlineColor.rgb, mask);
                    // 알파는 합집합
                    outCol.a = max(outCol.a, _OutlineColor.a * mask);
                }

                // premultiplied alpha (Blend One OneMinusSrcAlpha)
                outCol.rgb *= outCol.a;
                return outCol;
            }
            ENDCG
        }
    }
}
