Shader "Project/Monster HP Instanced"
{
    Properties
    {
        _MainTex ("Aseprite HP Sheet", 2D) = "white" {}
        _FrameRect ("Frame UV Rect", Vector) = (0,0,1,1)
        _TrackRect ("Track UV Rect", Vector) = (0,0,1,1)
        _FillRect ("Health UV Rect", Vector) = (0,0,1,1)
        _TrailRect ("Damage Trail UV Rect", Vector) = (0,0,1,1)
        _FrameSize ("Frame Source Pixels", Vector) = (40,8,0,0)
        _FrameBorder ("Frame Border Pixels", Vector) = (2,2,2,2)
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
        sampler2D _MainTex;
        float4 _FrameRect, _TrackRect, _FillRect, _TrailRect, _FrameSize, _FrameBorder;
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
            // Match the authored sliced Image: pixel scale comes from bar height;
            // the two end caps retain their proportions while only the center widens.
            float pixelScale = data.y / _FrameSize.y;
            float4 border = _FrameBorder * pixelScale;
            float sourceX;
            if (p.x < border.x) sourceX = p.x / pixelScale;
            else if (p.x >= data.x - border.z) sourceX = _FrameSize.x - (data.x - p.x) / pixelScale;
            else sourceX = _FrameBorder.x + (p.x - border.x) / (data.x - border.x - border.z)
                * (_FrameSize.x - _FrameBorder.x - _FrameBorder.z);
            float4 color = tex2D(_MainTex, _FrameRect.xy + float2(sourceX / _FrameSize.x, input.uv.y) * _FrameRect.zw);
            float2 innerSize = data.xy - border.xy - border.zw;
            float2 inner = p - border.xy;
            if (inner.x >= 0 && inner.y >= 0 && inner.x < innerSize.x && inner.y < innerSize.y)
            {
                float2 uv = inner / innerSize;
                color = tex2D(_MainTex, _TrackRect.xy + uv * _TrackRect.zw);
                if (data.w > 0 && uv.x < data.w)
                    color = tex2D(_MainTex, _TrailRect.xy + float2(uv.x / data.w, uv.y) * _TrailRect.zw);
                if (data.z > 0 && uv.x < data.z)
                    color = tex2D(_MainTex, _FillRect.xy + float2(uv.x / data.z, uv.y) * _FillRect.zw);
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
