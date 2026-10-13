Shader "Project/Monster HP Instanced"
{
    Properties
    {
        _BorderColor ("Border", Color) = (0,0,0,1)
        _FrameColor ("Frame", Color) = (0.56,0.55,0.57,1)
        _TrackColor ("Track", Color) = (0.025,0.02,0.03,1)
        _TrailColor ("Damage Trail", Color) = (1,1,1,1)
        _FillColor ("Health", Color) = (0.8,0.025,0.08,1)
        _ShineColor ("Health Highlight", Color) = (1,0.17,0.23,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

        HLSLINCLUDE
        #include "UnityCG.cginc"
        float4 _BorderColor, _FrameColor, _TrackColor, _TrailColor, _FillColor, _ShineColor;
        UNITY_INSTANCING_BUFFER_START(ActorHealth)
            UNITY_DEFINE_INSTANCED_PROP(float4, _HealthData)
        UNITY_INSTANCING_BUFFER_END(ActorHealth)
        struct Attributes
        {
            float4 position : POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 position : SV_POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        Varyings Vertex(Attributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);
            Varyings output;
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            output.position = UnityObjectToClipPos(input.position);
            output.uv = input.uv;
            return output;
        }
        float4 Fragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            float4 data = UNITY_ACCESS_INSTANCED_PROP(ActorHealth, _HealthData);
            float2 p = input.uv * data.xy;
            float4 color = _BorderColor;
            if (p.x >= 1 && p.y >= 1 && p.x < data.x - 1 && p.y < data.y - 1) color = _FrameColor;
            if (p.x >= 2 && p.y >= 2 && p.x < data.x - 2 && p.y < data.y - 2) color = _TrackColor;
            float innerWidth = max(0, data.x - 6);
            float innerHeight = max(0, data.y - 6);
            if (p.y >= 3 && p.y < data.y - 3 && p.x >= 3)
            {
                if (data.w > 0 && p.x < 3 + innerWidth * data.w) color = _TrailColor;
                if (data.z > 0 && p.x < 3 + innerWidth * data.z)
                    color = p.y >= 3 + innerHeight * 0.65 ? _ShineColor : _FillColor;
            }
            return color;
        }
        ENDHLSL

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
