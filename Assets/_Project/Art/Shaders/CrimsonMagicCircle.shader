Shader "Project/Crimson Magic Circle"
{
    Properties
    {
        [PerRendererData] _MainTex ("Approved circle sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable external alpha", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex SpriteVert
            #pragma fragment CircleFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 CircleFragment(v2f input) : SV_Target
            {
                // Preserve thin authored strokes inside one screen-pixel footprint.
                // Select an existing source color, never blur/interpolate the palette.
                float2 dx = ddx(input.texcoord) / 3.0;
                float2 dy = ddy(input.texcoord) / 3.0;
                fixed4 result = SampleSpriteTexture(input.texcoord);
                float best = result.a * (1.0 + dot(result.rgb, float3(.2126, .7152, .0722)));
                [unroll] for (int y = -1; y <= 1; y++)
                {
                    [unroll] for (int x = -1; x <= 1; x++)
                    {
                        if (x == 0 && y == 0) continue;
                        fixed4 candidate = SampleSpriteTexture(input.texcoord + dx * x + dy * y);
                        float score = candidate.a * (1.0 + dot(candidate.rgb, float3(.2126, .7152, .0722)));
                        if (score > best) { result = candidate; best = score; }
                    }
                }
                result *= input.color;
                result.rgb *= result.a;
                return result;
            }
            ENDCG
        }
    }
}
