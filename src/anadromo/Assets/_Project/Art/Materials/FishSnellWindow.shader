Shader "Anadromo/Snell Window"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS); return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 ray=normalize(i.positionWS-GetCameraPositionWS());
                clip(ray.y);
                float ripple=sin(i.positionWS.x*1.8+_Time.y)*sin(i.positionWS.z*1.3+_Time.y*.8)*.009;
                // Critical angle in water: half aperture 48.3 degrees.
                float window=smoothstep(.652,.678,ray.y+ripple);
                float3 reflected=reflect(ray,float3(0,1,0));
                half4 probe=SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0,samplerunity_SpecCube0,reflected,2);
                half3 reflection=DecodeHDREnvironment(probe,unity_SpecCube0_HDR)*half3(.25,.55,.6);
                float depth=max(0,i.positionWS.y-GetCameraPositionWS().y);
                half3 light=half3(.35,.65,.68)*exp(-depth*.035);
                return half4(lerp(reflection+half3(.008,.025,.035),light,window),.75);
            }
            ENDHLSL
        }
    }
}
