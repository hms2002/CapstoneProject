Shader "Capstone/2D/Pixel Ambient Dust"
{
    Properties
    {
        _MainTex("Dust", 2D) = "white" {}
        _PixelsPerUnit("Pixels Per Unit", Float) = 24
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex DustVertex
            #pragma fragment DustFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _PixelsPerUnit;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half2 lightingUV : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings DustVertex(Attributes input)
            {
                Varyings output;
                float3 worldPosition = TransformObjectToWorld(input.positionOS);
                float ppu = max(_PixelsPerUnit, 1.0);
                // Only the rendered position is snapped; native particle motion stays continuous.
                worldPosition.xy = floor(worldPosition.xy * ppu + 0.5) / ppu;
                output.positionCS = TransformWorldToHClip(worldPosition);
                output.uv = input.uv;
                output.lightingUV = ComputeScreenPos(output.positionCS / output.positionCS.w).xy;
                output.color = input.color;
                return output;
            }

            half4 DustFragment(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                SurfaceData2D surface;
                InputData2D lighting;
                InitializeSurfaceData(color.rgb, color.a, half4(1, 1, 1, 1), surface);
                InitializeInputData(input.uv, input.lightingUV, lighting);
                return CombinedShapeLightShared(surface, lighting);
            }
            ENDHLSL
        }
    }
}
