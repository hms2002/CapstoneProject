Shader "Custom/SpriteHitFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

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
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            /// <summary>
            /// 책임 :
            /// - 스프라이트 렌더링에 필요한 정점 입력 데이터를 전달한다.
            /// - SpriteRenderer의 버텍스 컬러를 받아 틴트 계산에 사용한다.
            /// </summary>
            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            /// <summary>
            /// 책임 :
            /// - 정점 셰이더에서 프래그먼트 셰이더로 넘길 보간 데이터를 보관한다.
            /// </summary>
            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _FlashColor;
            float _FlashAmount;
            float _FlashMultiply;

            /// <summary>
            /// 책임 :
            /// - 오브젝트 정점을 클립 공간으로 변환하고,
            ///   UV와 버텍스 컬러를 다음 단계로 전달한다.
            /// </summary>
            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            /// <summary>
            /// 책임 :
            /// - 원본 스프라이트 색을 계산하고,
            ///   _FlashAmount에 따라 FlashColor를 덧입혀 피격 플래시를 표현한다.
            /// - 알파는 원본 스프라이트 알파를 유지한다.
            /// </summary>
            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, IN.texcoord) * IN.color;

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