Shader "Custom/SpriteHitFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0

        [Header(Hit Flash)]
        _FlashColor ("Flash Color", Color) = (1,0.2,0.2,1)
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0
        _FlashMultiply ("Flash Multiply", Range(0, 4)) = 1.5
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

            fixed4 _FlashColor;
            float _FlashAmount;
            float _FlashMultiply;

            /// <summary>
            /// 책임 :
            /// - Unity 기본 Sprite 샘플링 결과를 바탕으로,
            ///   _FlashAmount에 따라 FlashColor를 덧입혀 피격 플래시를 표현한다.
            /// - 알파는 원본 스프라이트 알파를 유지한다.
            /// </summary>
            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 baseCol = SampleSpriteTexture(IN.texcoord) * IN.color;

                // 알파 0인 부분은 그대로 투명
                if (baseCol.a <= 0.001f)
                    discard;

                // 플래시 색을 단순 lerp가 아니라 약간 강조해서 얹는다.
                fixed3 flashedRgb = lerp(baseCol.rgb, _FlashColor.rgb * _FlashMultiply, saturate(_FlashAmount));

                return fixed4(flashedRgb, baseCol.a);
            }
            ENDCG
        }
    }
}
