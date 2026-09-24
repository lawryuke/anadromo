Shader "Anadromo/Escenario4 Layered Cave"
{
    Properties
    {
        _BaseColor("Wet mineral tint", Color) = (.4,.5,.48,1)
        _BaseMap("Rock scan", 2D) = "white" {}
        _BumpMap("Rock detail", 2D) = "bump" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _BaseMap_ST;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; half fog:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(input.positionOS.xyz);
                o.normal=TransformObjectToWorldNormal(input.normalOS);
                o.positionCS=TransformWorldToHClip(o.world);
                o.fog=ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 n=normalize(input.normal);
                half3 weights=pow(abs(n),4); weights/=max(.001,weights.x+weights.y+weights.z);
                float3 p=input.world*.42;
                half3 textureColor=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.yz).rgb*weights.x
                    +SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz).rgb*weights.y
                    +SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy).rgb*weights.z;
                float strata=.88+.12*sin(input.world.y*4.5+sin(input.world.x*.7)+cos(input.world.z*.6));
                Light sun=GetMainLight();
                half light=.48+.4*saturate(dot(n,sun.direction));
                // Soft cyan fill keeps wet rock detail readable inside the enclosed shaft.
                half3 fill=half3(.42,.68,.7)*light;
                half3 stone=lerp(textureColor,half3(.58,.65,.62),.32)*_BaseColor.rgb*strata;
                half rim=pow(1-saturate(dot(n,normalize(GetCameraPositionWS()-input.world))),3)*.04;
                return half4(MixFog(stone*(fill+sun.color*.28)+half3(.12,.45,.46)*rim,input.fog),1);
            }
            ENDHLSL
        }
    }
}
