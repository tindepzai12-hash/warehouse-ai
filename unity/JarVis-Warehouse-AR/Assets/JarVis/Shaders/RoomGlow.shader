Shader "JarVis/RoomGlow"
{
    Properties { _BaseColor("Color",Color) = (0,0.8,1,0.12) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 samplePosition : TEXCOORD0; };
            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.samplePosition = input.positionOS.xz;
                return output;
            }
            float Segment(float2 samplePosition,float2 start,float2 end)
            {
                float2 direction = end - start;
                float along = saturate(dot(samplePosition - start,direction) / dot(direction,direction));
                return length(samplePosition - start - along * direction);
            }
            half4 Fragment(Varyings input) : SV_Target
            {
                float2 samplePosition = input.samplePosition;
                float distance = Segment(samplePosition,float2(-0.34,-0.20),float2(-0.16,-0.14));
                distance = min(distance,Segment(samplePosition,float2(-0.16,-0.14),float2(0,-0.04)));
                distance = min(distance,Segment(samplePosition,float2(0,-0.04),float2(0.16,-0.14)));
                distance = min(distance,Segment(samplePosition,float2(0.16,-0.14),float2(0.34,-0.20)));
                distance = min(distance,Segment(samplePosition,float2(0.34,-0.20),float2(0,0.34)));
                distance = min(distance,Segment(samplePosition,float2(0,0.34),float2(-0.34,-0.20)));
                half alpha = pow(saturate(1 - distance / 0.10),2) * _BaseColor.a;
                return half4(_BaseColor.rgb,alpha);
            }
            ENDHLSL
        }
    }
}
