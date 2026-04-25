Shader "UI/Lake Surface"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _DeepColor ("Deep Color", Color) = (0.025, 0.07, 0.11, 1)
        _ShallowColor ("Shallow Color", Color) = (0.07, 0.23, 0.28, 1)
        _CausticColor ("Caustic Color", Color) = (0.62, 0.95, 0.87, 1)
        _BottomTex ("Bottom Texture", 2D) = "white" {}
        _BottomTint ("Bottom Tint", Color) = (0.18, 0.75, 0.82, 1)
        _BottomStrength ("Bottom Visibility", Range(0, 1)) = 0.22
        _BottomTiling ("Bottom Tiling", Range(0.1, 24)) = 3.2
        _BottomDrift ("Bottom Drift", Vector) = (0.012, -0.007, 0, 0)
        _HeightTex ("Height Texture", 2D) = "gray" {}
        _HeightTexStrength ("Height Texture Strength", Range(0, 1)) = 0.18
        _HeightTexTiling ("Height Texture Tiling", Range(0.1, 32)) = 5.8
        _HeightTexDrift ("Height Texture Drift", Vector) = (-0.028, 0.018, 0, 0)
        _CausticTex ("Caustic Net Texture", 2D) = "black" {}
        _CausticTextureStrength ("Caustic Texture Strength", Range(0, 2)) = 0.38
        _CausticTextureTiling ("Caustic Texture Tiling", Range(0.1, 48)) = 4.6
        _CausticTextureSpeed ("Caustic Texture Speed", Vector) = (0.05, -0.032, -0.038, 0.044)
        _CausticTextureSharpness ("Caustic Texture Sharpness", Range(0.25, 8)) = 2.6
        _FoamTex ("Foam Texture", 2D) = "black" {}
        _FoamColor ("Foam Color", Color) = (0.72, 1, 0.92, 0.62)
        _FoamStrength ("Foam Strength", Range(0, 1)) = 0.13
        _FoamTiling ("Foam Tiling", Range(0.1, 64)) = 8.5
        _FoamSpeed ("Foam Speed", Vector) = (0.018, 0.012, -0.021, 0.015)
        _FoamThreshold ("Foam Threshold", Range(0, 1)) = 0.52
        _Alpha ("Alpha", Range(0, 1)) = 0.94
        _WaveScale ("Wave Scale", Range(0.1, 24)) = 5.5
        _WaveSpeed ("Wave Speed", Range(0, 5)) = 0.32
        _WaveStrength ("Wave Strength", Range(0, 0.25)) = 0.055
        _CausticStrength ("Caustic Strength", Range(0, 1)) = 0.18
        _TopDownDepthBias ("Top-Down Depth Bias", Range(0, 1)) = 0.18
        _Parallax ("Content Parallax", Range(0, 2)) = 0.22
        _BackgroundDistortion ("Background Distortion", Range(0, 0.08)) = 0.018
        _InteractionDistortion ("Interaction Distortion", Range(0, 0.12)) = 0.045
        _InteractionCompression ("Ripple Compression Strength", Range(0, 1)) = 0.25
        _DepthNoiseStrength ("Depth Noise Strength", Range(0, 1)) = 0.28
        _SurfaceNormalStrength ("Surface Normal Strength", Range(0, 1)) = 0.46
        _SpecularStrength ("Specular Strength", Range(0, 1)) = 0.26
        _EdgeDarkening ("Edge Darkening", Range(0, 1)) = 0.22
        _InteractionStrength ("Interaction Strength", Range(0, 2)) = 0.72
        _WakeTex ("Wake Texture", 2D) = "black" {}
        _WakeTextureStrength ("Wake Texture Strength", Range(0, 2)) = 1
        _OuterRippleDuration ("Outer Ripple Duration", Range(0, 10)) = 6.2
        _OuterRippleStrength ("Outer Ripple Strength", Range(0, 1)) = 0.16
        _OuterRippleSpeed ("Outer Ripple Speed", Range(0, 3)) = 0.94
        _SurfaceAspect ("Surface Aspect", Float) = 1
        _UnscaledTime ("Unscaled Time", Float) = 0
        _ContentOffset ("Content Offset", Vector) = (0,0,0,0)

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
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #define MAX_SURFACE_RIPPLES 8

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
            sampler2D _WakeTex;
            sampler2D _BottomTex;
            float4 _BottomTex_ST;
            sampler2D _HeightTex;
            float4 _HeightTex_ST;
            sampler2D _CausticTex;
            float4 _CausticTex_ST;
            sampler2D _FoamTex;
            float4 _FoamTex_ST;
            fixed4 _Color;
            fixed4 _DeepColor;
            fixed4 _ShallowColor;
            fixed4 _CausticColor;
            fixed4 _BottomTint;
            fixed4 _FoamColor;
            float _BottomStrength;
            float _BottomTiling;
            float4 _BottomDrift;
            float _HeightTexStrength;
            float _HeightTexTiling;
            float4 _HeightTexDrift;
            float _CausticTextureStrength;
            float _CausticTextureTiling;
            float4 _CausticTextureSpeed;
            float _CausticTextureSharpness;
            float _FoamStrength;
            float _FoamTiling;
            float4 _FoamSpeed;
            float _FoamThreshold;
            float _Alpha;
            float _WaveScale;
            float _WaveSpeed;
            float _WaveStrength;
            float _CausticStrength;
            float _TopDownDepthBias;
            float _Parallax;
            float _BackgroundDistortion;
            float _InteractionDistortion;
            float _InteractionCompression;
            float _DepthNoiseStrength;
            float _SurfaceNormalStrength;
            float _SpecularStrength;
            float _EdgeDarkening;
            float _InteractionStrength;
            float _WakeTextureStrength;
            float _OuterRippleDuration;
            float _OuterRippleStrength;
            float _OuterRippleSpeed;
            float _SurfaceAspect;
            float _UnscaledTime;
            float4 _ContentOffset;
            float4 _ClipRect;
            int _SurfaceRippleCount;
            float4 _SurfaceRippleData[MAX_SURFACE_RIPPLES];
            float4 _SurfaceRippleExtra[MAX_SURFACE_RIPPLES];

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            float2 AspectCorrectDelta(float2 delta)
            {
                return float2(delta.x * max(_SurfaceAspect, 0.0001), delta.y);
            }

            float2 AspectVectorToUv(float2 value)
            {
                return float2(value.x / max(_SurfaceAspect, 0.0001), value.y);
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 37.37);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                value += ValueNoise(p) * amplitude;
                p = p * 2.03 + float2(19.19, 7.31);
                amplitude *= 0.5;
                value += ValueNoise(p) * amplitude;
                p = p * 2.01 + float2(5.17, 23.83);
                amplitude *= 0.5;
                value += ValueNoise(p) * amplitude;
                p = p * 2.07 + float2(31.11, 11.47);
                amplitude *= 0.5;
                value += ValueNoise(p) * amplitude;

                return value;
            }

            float2 LakeTextureUv(float2 uv, float tiling, float2 drift, float time, float4 textureTransform)
            {
                float2 textureUv = uv * max(tiling, 0.001) + drift * time;
                return textureUv * textureTransform.xy + textureTransform.zw;
            }

            float SampleHeightTexture(float2 uv, float time)
            {
                if (_HeightTexStrength <= 0.0001)
                    return 0.0;

                float2 uvA = LakeTextureUv(uv, _HeightTexTiling, _HeightTexDrift.xy, time, _HeightTex_ST);
                float2 uvB = LakeTextureUv(uv + float2(0.173, -0.091), _HeightTexTiling * 1.73, _HeightTexDrift.zw + float2(0.021, -0.016), time, _HeightTex_ST);
                float heightA = tex2D(_HeightTex, uvA).r * 2.0 - 1.0;
                float heightB = tex2D(_HeightTex, uvB).r * 2.0 - 1.0;
                return (heightA * 0.72 + heightB * 0.28) * _HeightTexStrength;
            }

            float2 EvaluateTextureHeightGradient(float2 uv, float time)
            {
                if (_HeightTexStrength <= 0.0001)
                    return float2(0.0, 0.0);

                float gradientStep = 0.003;
                float stepX = gradientStep / max(_SurfaceAspect, 0.0001);
                float heightX =
                    SampleHeightTexture(uv + float2(stepX, 0.0), time) -
                    SampleHeightTexture(uv - float2(stepX, 0.0), time);
                float heightY =
                    SampleHeightTexture(uv + float2(0.0, gradientStep), time) -
                    SampleHeightTexture(uv - float2(0.0, gradientStep), time);

                return float2(heightX, heightY) / max(gradientStep * 2.0, 0.0001);
            }

            float3 SampleBottomColor(float2 uv, float time)
            {
                float2 uvA = LakeTextureUv(uv, _BottomTiling, _BottomDrift.xy, time, _BottomTex_ST);
                return tex2D(_BottomTex, uvA).rgb * _BottomTint.rgb;
            }

            float SampleCausticTexture(float2 uv, float time)
            {
                if (_CausticTextureStrength <= 0.0001)
                    return 0.0;

                float2 uvA = LakeTextureUv(uv, _CausticTextureTiling, _CausticTextureSpeed.xy, time, _CausticTex_ST);
                float2 uvB = LakeTextureUv(uv + float2(0.31, 0.47), _CausticTextureTiling * 1.37, _CausticTextureSpeed.zw, time, _CausticTex_ST);
                float causticA = tex2D(_CausticTex, uvA).a;
                float causticB = tex2D(_CausticTex, uvB).a;
                float caustic = max(causticA, causticB * 0.76);
                return pow(saturate(caustic), max(_CausticTextureSharpness, 0.001)) * _CausticTextureStrength;
            }

            float SampleFoamMask(float2 uv, float time, float interactionHeight, float2 interactionGradient, float depth)
            {
                if (_FoamStrength <= 0.0001)
                    return 0.0;

                float2 uvA = LakeTextureUv(uv, _FoamTiling, _FoamSpeed.xy, time, _FoamTex_ST);
                float2 uvB = LakeTextureUv(uv + float2(0.59, -0.26), _FoamTiling * 1.62, _FoamSpeed.zw, time, _FoamTex_ST);
                float foamA = tex2D(_FoamTex, uvA).a;
                float foamB = tex2D(_FoamTex, uvB).a;
                float foam = max(foamA, foamB * 0.68);
                float mask = smoothstep(min(_FoamThreshold, 0.999), 1.0, foam);
                float interactionMask = saturate(abs(interactionHeight) * 0.75 + length(interactionGradient) * 0.012);
                float shallowMask = lerp(0.55, 1.15, saturate(depth));
                return saturate(mask * _FoamStrength * shallowMask + interactionMask * _FoamStrength * 0.42);
            }

            float EvaluateLakeDetailHeight(float2 uv, float time)
            {
                float2 p = float2(uv.x * max(_SurfaceAspect, 0.0001), uv.y);
                float scale = max(_WaveScale, 0.1);
                float broad = Fbm(p * scale * 0.36 + float2(time * 0.10, -time * 0.055));
                float fine = Fbm(p * scale * 1.28 + float2(-time * 0.18, time * 0.12));
                float warp = Fbm(p * scale * 0.74 + float2(time * 0.035, time * 0.045));
                float cross =
                    sin((p.x * scale * 1.34 + warp * 1.8 + time * 0.46) * 6.2831853) *
                    sin((p.y * scale * 1.18 - warp * 1.45 - time * 0.32) * 6.2831853);

                return (broad - 0.5) * 0.62 + (fine - 0.5) * 0.26 + cross * 0.09;
            }

            float2 EvaluateLakeDetailGradient(float2 uv, float time)
            {
                float gradientStep = 0.003;
                float stepX = gradientStep / max(_SurfaceAspect, 0.0001);
                float heightX =
                    EvaluateLakeDetailHeight(uv + float2(stepX, 0.0), time) -
                    EvaluateLakeDetailHeight(uv - float2(stepX, 0.0), time);
                float heightY =
                    EvaluateLakeDetailHeight(uv + float2(0.0, gradientStep), time) -
                    EvaluateLakeDetailHeight(uv - float2(0.0, gradientStep), time);

                return float2(heightX, heightY) / max(gradientStep * 2.0, 0.0001);
            }

            float EvaluateCircularRipples(float2 uv, float now)
            {
                float height = 0.0;

                for (int i = 0; i < MAX_SURFACE_RIPPLES; i++)
                {
                    if (i >= _SurfaceRippleCount)
                        break;

                    float4 data = _SurfaceRippleData[i];
                    float4 extra = _SurfaceRippleExtra[i];
                    float age = now - data.z;
                    float duration = max(extra.y, 0.0001);
                    float outerDuration = max(_OuterRippleDuration, 0.0);
                    float totalDuration = duration + outerDuration;
                    if (age < 0.0 || age > totalDuration)
                        continue;

                    float totalT = saturate(age / totalDuration);
                    float impactT = saturate(age / duration);
                    float maxRadius = max(extra.x, 0.004);
                    float thickness = max(extra.z, 0.001);
                    float dist = length(AspectCorrectDelta(uv - data.xy));
                    float travelSpeed = maxRadius / duration * max(_OuterRippleSpeed, 0.0001);
                    float radius = 0.004 + age * travelSpeed;
                    float frontDelta = dist - radius;
                    float frontWidth = max(lerp(thickness * 3.4, thickness * 0.72, pow(totalT, 0.82)), 0.001);
                    float frontMask = exp(-(frontDelta * frontDelta) / max(frontWidth * frontWidth, 0.000001));
                    float backDelta = frontDelta + frontWidth * 1.16;
                    float backMask = exp(-(backDelta * backDelta) / max(frontWidth * frontWidth * 1.45, 0.000001));
                    float waveProfile = frontMask - (backMask * 0.42);
                    float startFade = smoothstep(0.0, 0.055, totalT);
                    float endFade = 1.0 - smoothstep(0.94, 1.0, totalT);
                    float travelFade = lerp(1.0, _OuterRippleStrength, pow(totalT, 0.78));
                    float impactBoost = 1.0 + (1.0 - smoothstep(0.0, 0.18, totalT)) * 0.32;
                    float centerDip = -0.26 * exp(-(dist * dist) / max(thickness * thickness * 10.0, 0.000001)) * pow(1.0 - impactT, 2.2);

                    height += ((waveProfile * startFade * endFade * travelFade * impactBoost) + centerDip) * data.w;
                }

                return height;
            }

            float EvaluateWakeTexture(float2 uv)
            {
                float4 wake = tex2D(_WakeTex, uv);
                float height = wake.r;
                float velocity = wake.r - wake.g;
                return (height + velocity * 0.35) * _WakeTextureStrength;
            }

            float EvaluateInteractionHeight(float2 uv, float now)
            {
                return EvaluateCircularRipples(uv, now) + EvaluateWakeTexture(uv);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, IN.texcoord) * IN.color;
                float time = _UnscaledTime * _WaveSpeed;

                float interactionHeight = EvaluateInteractionHeight(IN.texcoord, _UnscaledTime);
                float sampleStep = 0.004;
                float sampleStepX = sampleStep / max(_SurfaceAspect, 0.0001);
                float rippleGradientX =
                    EvaluateInteractionHeight(IN.texcoord + float2(sampleStepX, 0.0), _UnscaledTime) -
                    EvaluateInteractionHeight(IN.texcoord - float2(sampleStepX, 0.0), _UnscaledTime);
                float rippleGradientY =
                    EvaluateInteractionHeight(IN.texcoord + float2(0.0, sampleStep), _UnscaledTime) -
                    EvaluateInteractionHeight(IN.texcoord - float2(0.0, sampleStep), _UnscaledTime);
                float2 heightGradient = float2(rippleGradientX, rippleGradientY) / max(sampleStep * 2.0, 0.0001);
                float compressionResponse = saturate(_InteractionCompression);
                float compressionCurve = compressionResponse * compressionResponse;
                float gradientMagnitude = length(heightGradient);
                float gradientCeiling = 18.0 * compressionCurve;
                float gradientLimit = gradientCeiling <= 0.0001 ? 0.0 : min(1.0, gradientCeiling / max(gradientMagnitude, 0.0001));
                float2 compressedHeightGradient = heightGradient * gradientLimit;

                float2 centeredBase = AspectCorrectDelta(IN.texcoord - float2(0.5, 0.5));
                float radiusBase = length(centeredBase);
                float angleBase = atan2(centeredBase.y, centeredBase.x);
                float radialA = sin((radiusBase * 9.8 - time * 0.54) * 6.2831853);
                float radialB = sin((radiusBase * 15.2 + time * 0.37 + sin(angleBase * 4.0) * 0.08) * 6.2831853);
                float angularA = sin(angleBase * 6.0 + time * 2.2) * 0.25;
                float radialOffset = (radialA * 0.62 + radialB * 0.38 + angularA) * _BackgroundDistortion;
                float2 radialDirection = radiusBase > 0.0001 ? centeredBase / radiusBase : float2(0.0, 0.0);
                float2 tangentDirection = float2(-radialDirection.y, radialDirection.x);
                float2 baseDistortion = AspectVectorToUv((radialDirection * radialOffset) + (tangentDirection * angularA * _BackgroundDistortion * 0.22));
                float2 interactionDistortion = AspectVectorToUv(compressedHeightGradient * _InteractionDistortion * 0.022);
                float2 distortedTexcoord = IN.texcoord + baseDistortion + interactionDistortion;
                float2 uv = distortedTexcoord + (_ContentOffset.xy * _Parallax * 0.00045);
                float2 p = uv * max(_WaveScale, 0.001);
                float lakeDetailHeight = EvaluateLakeDetailHeight(distortedTexcoord, time);
                float2 lakeDetailGradient = EvaluateLakeDetailGradient(distortedTexcoord, time);
                float textureHeight = SampleHeightTexture(distortedTexcoord, time);
                float2 textureHeightGradient = EvaluateTextureHeightGradient(distortedTexcoord, time);

                float2 centeredWave = AspectCorrectDelta(distortedTexcoord - float2(0.5, 0.5));
                float waveRadius = length(centeredWave);
                float waveAngle = atan2(centeredWave.y, centeredWave.x);
                float waveA = sin((waveRadius * 4.7 - time * 0.75) * 6.2831853);
                float waveB = sin((waveRadius * 8.6 + time * 0.41 + sin(waveAngle * 5.0) * 0.12) * 6.2831853);
                float waveC = sin((waveRadius * 12.4 - time * 0.26 + cos(waveAngle * 7.0) * 0.08) * 6.2831853);
                float wave = (waveA + waveB + waveC) * 0.3333333;
                float heightCeiling = 2.5 * compressionCurve;
                float heightLimit = heightCeiling <= 0.0001 ? 0.0 : min(1.0, heightCeiling / max(abs(interactionHeight), 0.0001));
                float compressedInteractionHeight = interactionHeight * heightLimit;
                float shapedHeight = compressedInteractionHeight * _InteractionStrength;

                float2 centeredUv = centeredWave;
                float radialDepth = 1.0 - saturate(waveRadius * 1.42);
                float depthBase = lerp(0.5, radialDepth, _TopDownDepthBias);
                float depthVariation = lakeDetailHeight * _DepthNoiseStrength + textureHeight;
                float depth = saturate(depthBase + depthVariation + wave * _WaveStrength + shapedHeight * 0.34 * compressionResponse);
                fixed4 water = lerp(_DeepColor, _ShallowColor, depth);
                float bottomVisibility = saturate(_BottomStrength * _BottomTint.a * lerp(0.52, 1.0, depth));
                water.rgb = lerp(water.rgb, SampleBottomColor(distortedTexcoord, time), bottomVisibility);

                float causticWarp = Fbm(p * 0.82 + float2(time * 0.16, -time * 0.11));
                float shimmerA = sin((p.x * 2.9 + causticWarp * 2.4 + time * 0.58) * 6.2831853);
                float shimmerB = sin((p.y * 3.3 - causticWarp * 1.8 - time * 0.43) * 6.2831853);
                float shimmerC = sin(((p.x + p.y) * 1.9 + causticWarp * 1.2 + time * 0.24) * 6.2831853);
                float caustic = pow(saturate((shimmerA * shimmerB + shimmerC * 0.36) * 0.5 + 0.5), 7.5) * _CausticStrength;
                water.rgb += _CausticColor.rgb * caustic * _CausticColor.a;
                water.rgb += _CausticColor.rgb * SampleCausticTexture(distortedTexcoord, time) * _CausticColor.a;

                float2 totalGradient =
                    compressedHeightGradient * _InteractionStrength * 0.38 +
                    lakeDetailGradient * _SurfaceNormalStrength * 0.12 +
                    textureHeightGradient * 0.14;
                float relief = saturate(length(totalGradient) * 0.28);
                float3 surfaceNormal = normalize(float3(-totalGradient.x, -totalGradient.y, 1.0));
                float3 lightDirection = normalize(float3(-0.28, 0.36, 0.89));
                float normalLight = dot(surfaceNormal, lightDirection);
                float raisedLight = saturate((normalLight - 0.78) * 4.5) * relief;
                float recessedShadow = saturate((0.72 - normalLight) * 3.8) * relief;
                float3 viewDirection = float3(0.0, 0.0, 1.0);
                float specular = pow(saturate(dot(reflect(-lightDirection, surfaceNormal), viewDirection)), 48.0);
                float glintMask = pow(saturate(Fbm(p * 3.8 + float2(time * 0.30, -time * 0.22))), 4.2);
                water.rgb += _CausticColor.rgb * raisedLight * 0.42 * _CausticColor.a;
                water.rgb += _CausticColor.rgb * specular * glintMask * _SpecularStrength * _CausticColor.a;
                water.rgb *= 1.0 - recessedShadow * 0.28;
                float foamMask = SampleFoamMask(distortedTexcoord, time, compressedInteractionHeight, compressedHeightGradient, depth);
                water.rgb = lerp(water.rgb, _FoamColor.rgb, foamMask * _FoamColor.a);

                float dist = length(centeredUv);
                float edge = smoothstep(0.48, 0.82, dist);
                water.rgb *= 1.02 - edge * _EdgeDarkening;

                fixed4 outCol = water;
                outCol.a = saturate(_Alpha) * tex.a * IN.color.a;

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
