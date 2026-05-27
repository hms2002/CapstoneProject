Shader "Custom/SpriteFloweringWeaponReveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0

        [Header(Reveal)]
        _RevealProgress ("Reveal Progress", Range(0, 1)) = 1
        _RevealFeather ("Reveal Feather", Range(0, 0.25)) = 0.08
        _RevealAxis ("Reveal Axis", Vector) = (-1,1,0,0)
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

            float _RevealProgress;
            float _RevealFeather;
            float4 _RevealAxis;

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 axis = _RevealAxis.xy;
                axis = dot(axis, axis) > 0.0001 ? normalize(axis) : normalize(float2(-1.0, 1.0));

                float p = dot(IN.texcoord, axis);
                float p0 = dot(float2(0.0, 0.0), axis);
                float p1 = dot(float2(1.0, 0.0), axis);
                float p2 = dot(float2(0.0, 1.0), axis);
                float p3 = dot(float2(1.0, 1.0), axis);
                float minP = min(min(p0, p1), min(p2, p3));
                float maxP = max(max(p0, p1), max(p2, p3));
                float revealT = saturate((p - minP) / max(maxP - minP, 0.0001));

                float feather = max(_RevealFeather, 0.0001);
                float mask = 1.0 - smoothstep(_RevealProgress, _RevealProgress + feather, revealT);

                fixed4 col = SampleSpriteTexture(IN.texcoord) * IN.color;
                col.a *= mask;
                if (col.a <= 0.001)
                    discard;

                col.rgb *= mask;
                return col;
            }
            ENDCG
        }
    }
}
