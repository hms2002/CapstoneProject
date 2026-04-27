Shader "Custom/PuddleShaderVisual"
{
    Properties
    {
        [Header(Texture Sockets)]
        _MainTex ("Optional Shape Mask", 2D) = "white" {}
        _NoiseTex ("Optional Noise Texture", 2D) = "white" {}
        _FireProjectileTex ("Fire Projectile Texture", 2D) = "white" {}
        _AlcoholProjectileTex ("Alcohol Projectile Texture", 2D) = "white" {}

        [Header(Shape)]
        _PixelDensity ("Pixel Density", Range(8, 128)) = 42
        _BlobNoiseAmount ("Blob Noise Amount", Range(0, 0.35)) = 0.16
        _EdgeWidth ("Edge Width", Range(0.01, 0.25)) = 0.08
        _InnerHighlightAmount ("Inner Highlight Amount", Range(0, 1)) = 0.55
        _ProjectileLength ("Projectile Length", Range(0.5, 4)) = 2.35
        _ProjectileWidth ("Projectile Width", Range(0.25, 2)) = 0.85
        _ProjectileBlendStart ("Projectile Blend Start", Range(0.1, 0.95)) = 0.78
        _CondensePullStrength ("Condense Pull Strength", Range(0, 2)) = 1.25
        _CondenseShrinkStrength ("Condense Shrink Strength", Range(0, 1)) = 0.32

        [Header(Alcohol)]
        _AlcoholDark ("Alcohol Dark", Color) = (0.48, 0.28, 0.06, 0.92)
        _AlcoholMid ("Alcohol Mid", Color) = (0.95, 0.62, 0.18, 0.92)
        _AlcoholHot ("Alcohol Highlight", Color) = (1.0, 0.84, 0.38, 0.95)
        _AlcoholFoam ("Alcohol Foam", Color) = (1.0, 0.92, 0.72, 0.98)

        [Header(Fire)]
        _FireOutline ("Fire Outline", Color) = (0.32, 0.07, 0.025, 1)
        _FireDark ("Fire Dark", Color) = (0.72, 0.08, 0.02, 0.96)
        _FireMid ("Fire Mid", Color) = (1.0, 0.24, 0.02, 0.98)
        _FireHot ("Fire Hot", Color) = (1.0, 0.78, 0.12, 1)
        _FireWhite ("Fire White", Color) = (1.0, 0.96, 0.62, 1)

        [Header(Runtime)]
        _ElementType ("Element Type", Float) = 0
        _Mode ("Mode", Float) = 0
        _Radius ("Radius", Float) = 1
        _IgnitionProgress ("Ignition Progress", Range(0, 1)) = 0
        _AbsorbProgress ("Absorb Progress", Range(0, 1)) = 0
        _AbsorbDirection ("Absorb Direction", Vector) = (0, 1, 0, 0)
        _TimeOffset ("Time Offset", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _NoiseTex;
            sampler2D _FireProjectileTex;
            sampler2D _AlcoholProjectileTex;
            float4 _MainTex_ST;
            float _PixelDensity;
            float _BlobNoiseAmount;
            float _EdgeWidth;
            float _InnerHighlightAmount;
            float _ProjectileLength;
            float _ProjectileWidth;
            float _ProjectileBlendStart;
            float _CondensePullStrength;
            float _CondenseShrinkStrength;

            fixed4 _AlcoholDark;
            fixed4 _AlcoholMid;
            fixed4 _AlcoholHot;
            fixed4 _AlcoholFoam;
            fixed4 _FireOutline;
            fixed4 _FireDark;
            fixed4 _FireMid;
            fixed4 _FireHot;
            fixed4 _FireWhite;

            float _ElementType;
            float _Mode;
            float _Radius;
            float _IgnitionProgress;
            float _AbsorbProgress;
            float4 _AbsorbDirection;
            float _TimeOffset;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float SampleControlNoise(float2 uv, float2 offset, float scale)
            {
                return tex2D(_NoiseTex, frac(uv * scale + offset)).r;
            }

            fixed4 ResolveFireColor(float heat, float edgeBand)
            {
                fixed4 color = _FireDark;
                color = lerp(color, _FireMid, step(0.28, heat));
                color = lerp(color, _FireHot, step(0.58, heat));
                color = lerp(color, _FireWhite, step(0.82, heat));
                color = lerp(color, _FireOutline, edgeBand);
                return color;
            }

            fixed4 ResolveAlcoholColor(float heat, float edgeBand, float foamBand)
            {
                fixed4 color = lerp(_AlcoholDark, _AlcoholMid, step(0.25, heat));
                color = lerp(color, _AlcoholHot, step(0.76, heat) * _InnerHighlightAmount);
                color = lerp(color, _AlcoholFoam, foamBand);
                color.rgb = lerp(color.rgb, _AlcoholDark.rgb * 0.65, edgeBand * 0.55);
                return color;
            }

            fixed4 SampleProjectileTexture(float2 centered, float2 direction)
            {
                float2 sideAxis = float2(-direction.y, direction.x);
                float along = dot(centered, direction);
                float side = dot(centered, sideAxis);
                float2 projectileUv = float2(along / _ProjectileLength + 0.5, side / _ProjectileWidth + 0.5);
                float inBounds =
                    step(0.0, projectileUv.x) * step(projectileUv.x, 1.0) *
                    step(0.0, projectileUv.y) * step(projectileUv.y, 1.0);

                fixed4 fireProjectile = tex2D(_FireProjectileTex, projectileUv);
                fixed4 alcoholProjectile = tex2D(_AlcoholProjectileTex, projectileUv);
                fixed4 sample = _ElementType > 0.5 ? fireProjectile : alcoholProjectile;
                sample.a *= inBounds;
                return sample;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 centered = i.uv * 2.0 - 1.0;
                float2 absorbDir = normalize(_AbsorbDirection.xy + float2(0.0001, 0.0001));
                float flow = dot(centered, absorbDir);
                float preparing = 1.0 - step(0.5, abs(_Mode - 2.0));
                float projectile = 1.0 - step(0.5, abs(_Mode - 3.0));
                float prepareProgress = preparing * _AbsorbProgress;
                float projectileProgress = projectile * _AbsorbProgress;
                float condenseProgress = prepareProgress * prepareProgress * (3.0 - 2.0 * prepareProgress);
                float projectileBlend = saturate(projectile + preparing * smoothstep(_ProjectileBlendStart, 1.0, _AbsorbProgress));
                float2 pixelUv = floor((i.uv + absorbDir * _AbsorbProgress * 0.12) * _PixelDensity) / _PixelDensity;
                float time = _Time.y + _TimeOffset;
                float coarse = valueNoise(pixelUv * 5.0 + time * 0.18);
                float detail = valueNoise(pixelUv * 17.0 - time * 0.35);
                float texCoarse = SampleControlNoise(pixelUv, time * float2(0.022, -0.017), 1.25);
                float texDetail = SampleControlNoise(pixelUv, time * float2(-0.035, 0.026), 3.5);
                coarse = lerp(coarse, texCoarse, 0.65);
                detail = lerp(detail, texDetail, 0.7);

                float radiusNoise = (coarse - 0.5) * _BlobNoiseAmount;
                float frontPull = saturate(flow * 0.5 + 0.5);
                float backPull = saturate(-flow * 0.5 + 0.5);
                float centerPull = condenseProgress * (1.0 - abs(flow)) * 0.22 * _CondensePullStrength;
                float prepareStretch = condenseProgress * frontPull * frontPull * 0.72 * _CondensePullStrength;
                float projectileStretch = projectileProgress * frontPull * 0.25;
                float2 stretched = centered - absorbDir * (prepareStretch + projectileStretch);
                stretched -= absorbDir * centerPull;
                stretched += absorbDir * (condenseProgress * backPull * 0.10 * _CondensePullStrength);
                stretched *= 1.0 + condenseProgress * backPull * 0.18 * _CondensePullStrength;
                float dist = length(stretched);
                float condenseShrink = condenseProgress * _CondenseShrinkStrength * (0.55 + backPull * 0.45);
                float shapeRadius = 0.78 + radiusNoise - projectileProgress * 0.12 - condenseShrink;
                float mask = tex2D(_MainTex, i.uv).a;
                float puddleInside = step(dist, shapeRadius) * mask;
                fixed4 projectileSample = SampleProjectileTexture(centered, absorbDir);
                float projectileInside = projectileSample.a;
                float inside = max(puddleInside * (1.0 - projectileBlend), projectileInside * projectileBlend);

                if (inside <= 0.001)
                    discard;

                float edgeStart = max(0.01, shapeRadius - _EdgeWidth);
                float edgeBand = 1.0 - smoothstep(edgeStart, shapeRadius, dist);
                edgeBand = 1.0 - edgeBand;
                float foamBand = saturate(edgeBand * 1.35) * step(_ElementType, 0.5);
                float heat = saturate(detail * 0.72 + coarse * 0.42 + _AbsorbProgress * 0.28);
                heat += saturate(flow) * (prepareProgress + projectileProgress) * 0.25;

                float fireBlend = saturate(max(_ElementType, _IgnitionProgress));
                fixed4 alcoholColor = ResolveAlcoholColor(heat, edgeBand, foamBand * (1.0 - fireBlend));
                fixed4 fireColor = ResolveFireColor(saturate(heat + _IgnitionProgress * 0.2), edgeBand);
                fixed4 color = lerp(alcoholColor, fireColor, fireBlend);

                float projectileEdge = 1.0 - smoothstep(0.18, 0.55, projectileSample.a);
                float projectileHeat = saturate(projectileSample.r * 0.85 + projectileSample.g * 0.25 + detail * 0.18);
                fixed4 projectileColor = fireBlend > 0.5
                    ? ResolveFireColor(projectileHeat, projectileEdge * 0.65)
                    : ResolveAlcoholColor(projectileHeat, projectileEdge * 0.35, projectileSample.g);

                color = lerp(color, projectileColor, projectileBlend);
                color.a *= inside;
                return color;
            }
            ENDCG
        }
    }
}
