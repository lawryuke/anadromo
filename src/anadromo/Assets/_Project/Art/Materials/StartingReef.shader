Shader "Anadromo/Starting Reef"
{
    Properties
    {
        _BaseMap("OceanViz rock", 2D) = "white" {}
        _BumpMap("Rock normal", 2D) = "bump" {}
        _SandMap("Sand", 2D) = "white" {}
        _Vegetation("Vegetation", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_SandMap); SAMPLER(sampler_SandMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Vegetation;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 tangentWS:TEXCOORD2;float2 uv:TEXCOORD3;half4 color:COLOR;half fog:TEXCOORD4;};
            Varyings Vert(Attributes i)
            {
                Varyings o;
                float3 p=i.positionOS.xyz;
                if(_Vegetation>.5) p.xz+=float2(sin(_Time.y*.8+p.x*1.7+p.z),cos(_Time.y*.65+p.z*1.4))*.055*i.uv.y*i.uv.y;
                VertexPositionInputs pos=GetVertexPositionInputs(p);
                VertexNormalInputs norm=GetVertexNormalInputs(i.normalOS,i.tangentOS);
                o.positionCS=pos.positionCS;o.positionWS=pos.positionWS;
                o.normalWS=norm.normalWS;o.tangentWS=half4(norm.tangentWS,i.tangentOS.w*GetOddNegativeScale());
                o.uv=i.uv;o.color=i.color;o.fog=ComputeFogFactor(pos.positionCS.z);return o;
            }
            half4 Frag(Varyings i, FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target
            {
                half3 normal=normalize(i.normalWS);
                half3 albedo;
                if(_Vegetation>.5)
                {
                    albedo=i.color.rgb;
                    normal*=IS_FRONT_VFACE(front,1,-1);
                }
                else
                {
                    half3 rock=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                    half3 sand=SAMPLE_TEXTURE2D(_SandMap,sampler_SandMap,i.uv*.65).rgb;
                    albedo=lerp(sand*half3(.79,.86,.78),rock*half3(.74,.83,.68),i.color.r);
                    albedo=lerp(albedo,albedo*half3(.44,.65,.29),i.color.g*.7);
                    half3 n=UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv));
                    n.xy*=.65*i.color.r;
                    half3 tangent=normalize(i.tangentWS.xyz);
                    normal=normalize(mul(n,half3x3(tangent,cross(normal,tangent)*i.tangentWS.w,normal)));
                }
                Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half diffuse=saturate(dot(normal,light.direction));
                if(_Vegetation>.5) diffuse=.35+.65*abs(dot(normal,light.direction));
                half3 illumination=SampleSH(normal)+light.color*diffuse*light.shadowAttenuation*light.distanceAttenuation;
                return half4(MixFog(albedo*illumination,i.fog),1);
            }
            ENDHLSL
        }
        // Keep this material buffer separate from URP Lit's incompatible layout.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Vegetation;
            CBUFFER_END
            struct DepthInput {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            float4 DepthVert(DepthInput i):SV_POSITION
            {
                float3 p=i.positionOS.xyz;
                if(_Vegetation>.5) p.xz+=float2(sin(_Time.y*.8+p.x*1.7+p.z),cos(_Time.y*.65+p.z*1.4))*.055*i.uv.y*i.uv.y;
                return TransformObjectToHClip(p);
            }
            half4 DepthFrag():SV_Target {return 0;}
            ENDHLSL
        }
    }
}
