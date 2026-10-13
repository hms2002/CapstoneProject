Shader "Custom/AlcoholMugFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Mug Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        _EmptyColor ("Empty Glass", Color) = (0.20,0.22,0.24,1)
        _BeerColor ("Beer", Color) = (1,0.62,0.12,1)
        _FoamColor ("Foam", Color) = (1,0.97,0.82,1)
        _FillAmount ("Fill Amount", Range(0,1)) = 0
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
                float2 pixel : TEXCOORD1;
                fixed4 color : COLOR;
            };

            AlcoholVaryings vert(appdata v)
            {
                AlcoholVaryings o;
                o.position = UnityObjectToClipPos(UnityFlipSprite(v.vertex, _Flip));
                o.uv = v.uv;
                // Drink_0 is 24x23, 32 pixels/unit, with its pivot at the bottom center.
                // Local coordinates keep the interior mask valid if the sprite is atlased.
                o.pixel = v.vertex.xy * 32.0 + float2(12.0, 0.0);
                o.color = v.color * _Color * _RendererColor;
                return o;
            }

            fixed4 frag(AlcoholVaryings i) : SV_Target
            {
                if (_DropletMode > 0.5)
                {
                    float2 p = (i.pixel - float2(12.0, 11.0)) / float2(6.5, 8.0);
                    p.x *= 1.0 + max(0.0, p.y) * 0.8;
                    fixed4 drop = _BeerColor;
                    drop.a *= step(dot(p, p), 1.0);
                    return drop * i.color;
                }

                fixed4 color = tex2D(_MainTex, i.uv);
                float2 pixel = floor(i.pixel) + 0.5;
                float fill = saturate(_FillAmount);
                float interior = step(6.0, pixel.x) * step(pixel.x, 18.0)
                    * step(2.0, pixel.y) * step(pixel.y, 15.0);
                float height = 2.0 + 13.0 * fill;
                float ink = step(0.03, max(color.r, max(color.g, color.b)));
                float highlight = step(0.75, min(color.r, min(color.g, color.b)));
                float shade = lerp(0.65, 1.0, saturate(max(color.r, max(color.g, color.b))));
                fixed3 empty = _EmptyColor.rgb * shade;
                fixed3 beer = _BeerColor.rgb * shade;
                fixed3 liquid = lerp(empty, beer, step(pixel.y, height) * step(0.001, fill));
                liquid = lerp(liquid, _FoamColor.rgb, highlight * 0.55);
                float surface = step(height - 1.0, pixel.y) * step(pixel.y, height)
                    * step(0.001, fill);
                liquid = lerp(liquid, _FoamColor.rgb, surface);
                color.rgb = lerp(color.rgb, liquid, interior * ink);

                // The authored handle and mug outline always stay whole; only its top foam appears at full charge.
                float topFoam = step(15.0, pixel.y) * step(pixel.x, 21.0);
                color.a *= 1.0 - topFoam * (1.0 - step(0.999, fill));
                return color * i.color;
            }
            ENDCG
        }
    }
}
