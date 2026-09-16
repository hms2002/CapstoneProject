Shader "Capstone/2D/Shadow Monster Reveal Lit"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _FlashColor("Flash Color", Color) = (1,1,1,1)
        _FlashAmount("Flash Amount", Range(0,1)) = 0
        _FlashMultiply("Flash Multiply", Float) = 1.5
        [HideInInspector] _RevealCount("Reveal Count", Int) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex RevealVertex
            #pragma fragment RevealFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _FlashColor;
                float _FlashAmount;
                float _FlashMultiply;
                int _RevealCount;
                float4 _RevealAreas[32];
            CBUFFER_END

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
            };
            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                float3 revealWorld : TEXCOORD4;
                half4 color : COLOR;
            };

            Varyings RevealVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                output.revealWorld = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.revealWorld);
                output.uv = input.uv;
                output.lightingUV = ComputeScreenPos(output.positionCS / output.positionCS.w).xy;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 RevealFragment(Varyings input) : SV_Target
            {
                bool visible = false;
                for (int i = 0; i < min(_RevealCount, 32); i++)
                {
                    float2 delta = input.revealWorld.xy - _RevealAreas[i].xy;
                    visible = visible || dot(delta, delta) <= _RevealAreas[i].z * _RevealAreas[i].z;
                }
                // Clip before lighting or hit flash so neither can reveal a hidden pixel.
                clip(visible ? 1.0 : -1.0);
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                clip(sprite.a - 0.001);
                SurfaceData2D surface;
                InputData2D lighting;
                InitializeSurfaceData(sprite.rgb, sprite.a, half4(1,1,1,1), surface);
                InitializeInputData(input.uv, input.lightingUV, lighting);
                half4 color = CombinedShapeLightShared(surface, lighting);
                color.rgb = lerp(color.rgb, _FlashColor.rgb * _FlashMultiply, saturate(_FlashAmount));
                return color;
            }
            ENDHLSL
        }
    }
}
