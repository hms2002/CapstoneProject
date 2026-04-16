Shader "Custom/SpriteRadialTimerRing"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0

        _FillAmount ("Fill Amount", Range(0, 1)) = 1
        _StartAngleDegrees ("Start Angle Degrees", Range(0, 360)) = 90
        _InvertFill ("Invert Fill", Float) = 0
        _FillMode ("Fill Mode", Float) = 0
        _InnerRadiusNormalized ("Inner Radius Normalized", Range(0, 1)) = 0
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

            float _FillAmount;
            float _StartAngleDegrees;
            float _InvertFill;
            float _FillMode;
            float _InnerRadiusNormalized;

            /// <summary>
            /// 책임 :
            /// - 링 스프라이트의 알파를 유지한 채, 중심 기준 각도 계산으로 남은 타이머 비율만큼만 표시한다.
            /// - 스프라이트 자체가 링 모양이면 원형 타이머처럼 보이고, 다른 모양이면 동일 로직의 방사형 fill로 재사용 가능하다.
            /// </summary>
            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 baseCol = SampleSpriteTexture(IN.texcoord) * IN.color;
                if (baseCol.a <= 0.001f)
                    discard;

                float2 centered = IN.texcoord - 0.5;
                float fill = saturate(_FillAmount);
                float visible = 0.0;

                if (_FillMode > 0.5)
                {
                    float radiusNormalized = saturate(length(centered) / 0.5);
                    float innerRadius = saturate(_InnerRadiusNormalized);
                    float visibleOuterRadius = lerp(innerRadius, 1.0, fill);

                    visible = step(0.001, fill) * step(radiusNormalized, visibleOuterRadius);
                }
                else
                {
                    float angle = degrees(atan2(centered.y, centered.x));
                    angle = frac((angle - _StartAngleDegrees) / 360.0 + 1.0);

                    visible = (_InvertFill > 0.5)
                        ? step(fill, angle)
                        : step(angle, fill);
                }

                if (visible <= 0.0)
                    discard;

                return baseCol;
            }
            ENDCG
        }
    }
}
