Shader "Custom/AlcoholMugFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Mug Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        _EmptyColor ("Uncharged Wood", Color) = (0.20,0.22,0.24,1)
        _BeerColor ("Beer", Color) = (1,0.62,0.12,1)
        _FoamColor ("Foam", Color) = (1,0.97,0.82,1)
        _FillAmount ("Fill Amount", Range(0,1)) = 0
        [PerRendererData] _StatusActive ("Status Active", Float) = 0
        [Toggle] _DropletMode ("Overflow Droplet", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" "DisableBatching"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnitySprites.cginc"

            fixed4 _EmptyColor;
            fixed4 _BeerColor;
            fixed4 _FoamColor;
            float _FillAmount;
            float _StatusActive;
            float _DropletMode;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };
            struct AlcoholVaryings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 localPosition : TEXCOORD1;
                fixed4 color : COLOR;
            };

            AlcoholVaryings vert(appdata v)
            {
                AlcoholVaryings o;
                o.position = UnityObjectToClipPos(UnityFlipSprite(v.vertex, _Flip));
                o.uv = v.uv;
                o.localPosition = v.vertex.xy;
                o.color = v.color * _Color * _RendererColor;
                return o;
            }

            fixed4 frag(AlcoholVaryings i) : SV_Target
            {
                if (_DropletMode > 0.5)
                {
                    float2 p = (i.localPosition * 32.0 - float2(0.0, 11.0)) / float2(6.5, 8.0);
                    p.x *= 1.0 + max(0.0, p.y) * 0.8;
                    fixed4 drop = _BeerColor;
                    drop.a *= step(dot(p, p), 1.0);
                    return drop * i.color;
                }

                fixed4 color = tex2D(_MainTex, i.uv);
                // The oak front between the two hoops shows exposure. Keep the handle and status eyes distinct.
                float2 pixel = floor(i.localPosition * 32.0 + float2(16.0, 0.0)) + 0.5;
                float fill = saturate(_FillAmount);
                float active = saturate(_StatusActive);
                float luminance = dot(color.rgb, fixed3(0.30, 0.59, 0.11));
                fixed3 dormant = lerp(_EmptyColor.rgb, luminance * fixed3(0.46, 0.40, 0.37), 0.60);
                bool oak = pixel.x >= 4.0 && pixel.x < 22.0 && pixel.y >= 7.0 && pixel.y < 21.0
                    && color.r > 2.0 * color.b + 0.07 && color.g > 1.4 * color.b + 0.025;
                if (oak)
                {
                    float height = lerp(7.0, 21.0, fill);
                    float filled = step(pixel.y, height);
                    float surface = filled * step(height - 1.0, pixel.y) * (1.0 - step(0.999, fill));
                    fixed3 oakAboveFill = lerp(dormant, color.rgb, 0.38 * active);
                    fixed3 oakBelowFill = lerp(color.rgb, _FoamColor.rgb, 0.44 * surface);
                    color.rgb = lerp(oakAboveFill, oakBelowFill, filled);
                    color.a *= lerp(0.57, 1.0, max(active, filled));
                }
                else
                {
                    color.rgb = lerp(dormant, color.rgb, active);
                    color.a *= lerp(0.57, 1.0, active);
                }
                return color * i.color;
            }
            ENDCG
        }
    }
}
