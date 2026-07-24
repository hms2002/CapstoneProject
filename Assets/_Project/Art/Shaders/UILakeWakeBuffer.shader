Shader "Hidden/UI/Lake Wake Buffer"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Simulate"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _WakeDecay;
            float _WakePropagation;
            float _DeltaTime;

            float4 frag(v2f_img IN) : SV_Target
            {
                float2 uv = IN.uv;
                float4 center = tex2D(_MainTex, uv);
                float current = center.r;
                float previous = center.g;
                float neighbor =
                    tex2D(_MainTex, uv + float2(_MainTex_TexelSize.x, 0.0)).r +
                    tex2D(_MainTex, uv - float2(_MainTex_TexelSize.x, 0.0)).r +
                    tex2D(_MainTex, uv + float2(0.0, _MainTex_TexelSize.y)).r +
                    tex2D(_MainTex, uv - float2(0.0, _MainTex_TexelSize.y)).r;
                neighbor *= 0.25;

                float propagation = saturate(_WakePropagation * _DeltaTime);
                float decay = exp(-max(_WakeDecay, 0.0) * _DeltaTime);
                float laplacian = neighbor - current;
                float velocity = (current - previous) * 0.985;
                float next = (current + velocity + laplacian * propagation) * decay;

                return float4(clamp(next, -4.0, 4.0), current, 0.0, 1.0);
            }
            ENDCG
        }

        Pass
        {
            Name "Stamp"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _StampUv;
            float4 _StampDirection;
            float _StampIntensity;
            float _StampLength;
            float _StampWidth;
            float _StampAspect;
            float _StampSeed;

            float4 frag(v2f_img IN) : SV_Target
            {
                float4 previous = tex2D(_MainTex, IN.uv);

                float2 moveDir = normalize(float2(_StampDirection.x * max(_StampAspect, 0.0001), _StampDirection.y) + float2(0.0001, 0.0));
                float2 sideDir = float2(-moveDir.y, moveDir.x);
                float2 delta = float2((IN.uv.x - _StampUv.x) * max(_StampAspect, 0.0001), IN.uv.y - _StampUv.y);

                float forwardRadius = max(_StampLength, 0.002);
                float lateralRadius = max(_StampWidth, 0.001);
                float forward = dot(delta, moveDir);
                float lateral = dot(delta, sideDir);
                float2 shaped = float2(
                    forward / max(forwardRadius, 0.0001),
                    lateral / max(lateralRadius, 0.0001));
                float normalizedDistance = length(shaped);
                float core = exp(-(normalizedDistance * normalizedDistance) * 3.2);
                float ringDistance = abs(normalizedDistance - 1.06);
                float ring = exp(-(ringDistance * ringDistance) * 26.0);
                float trailingBias = lerp(0.84, 1.12, smoothstep(forwardRadius * 0.65, -forwardRadius * 0.45, forward));
                float shimmer = sin(forward * 91.0 + lateral * 57.0 + _StampSeed) * 0.5 + 0.5;

                float positive = core * 0.27 * trailingBias;
                float negative = ring * lerp(0.08, 0.14, shimmer);
                float intensity = max(_StampIntensity, 0.0);
                float impulse = (positive - negative) * intensity;

                float4 result = previous;
                result.r = clamp(result.r + impulse, -4.0, 4.0);
                result.a = 1.0;
                return result;
            }
            ENDCG
        }
    }
}
