Shader "Anadromo/Escenario4 Warning"
{
    Properties { _BaseColor("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 fogDistance:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.fogDistance = float2(ComputeFogFactor(o.positionCS.z), distance(positionWS, GetCameraPositionWS()));
                o.color = input.color * _BaseColor;
                return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 color = lerp(input.color.rgb, MixFog(input.color.rgb, input.fogDistance.x), .45);
                return half4(color, input.color.a * (1 - smoothstep(18, 24, input.fogDistance.y)));
            }
            ENDHLSL
        }
    }
}
