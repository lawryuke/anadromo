Shader "Anadromo/Medusa Glow"
{
    Properties
    {
        [HDR] _Color ("Emission", Color) = (1,1,0.6,1)
        _Halo ("Billboard halo", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _Visibility ("Visibility start / end", Vector) = (5,10,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color, _Visibility;
                float _Halo;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float distanceWS : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 center = TransformObjectToWorld(float3(0,0,0));
                float3 world = TransformObjectToWorld(input.positionOS);
                if (_Halo > 0.5)
                {
                    float sizeX = length(TransformObjectToWorldDir(float3(1,0,0), false));
                    float sizeY = length(TransformObjectToWorldDir(float3(0,1,0), false));
                    world = center + UNITY_MATRIX_V[0].xyz * input.positionOS.x * sizeX
                                   + UNITY_MATRIX_V[1].xyz * input.positionOS.y * sizeY;
                }
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.distanceWS = distance(center, GetCameraPositionWS());
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float radial = saturate(1 - dot(input.uv * 2 - 1, input.uv * 2 - 1));
                float alpha = _Halo > 0.5 ? radial * radial * radial : 1;
                float visibility = 1 - saturate((input.distanceWS - _Visibility.x) / max(0.01, _Visibility.y - _Visibility.x));
                return half4(_Color.rgb, _Color.a * alpha * visibility);
            }
            ENDHLSL
        }
    }
}
