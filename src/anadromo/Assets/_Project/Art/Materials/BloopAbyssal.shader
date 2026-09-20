Shader "Anadromo/Bloop Abyssal Skin"
{
    Properties
    {
        [MainColor] _BaseColor("Skin / Slate Blue", Color) = (0.21,0.34,0.41,1)
        _MottleColor("Deep Pigmentation", Color) = (0.09,0.18,0.23,1)
        _MottleStrength("Pigmentation Variation", Range(0,1)) = 0.42
        _PatternScale("Pigmentation Scale", Float) = 0.32
        _DetailScale("Pore Scale", Float) = 12
        _BumpStrength("Skin Micro Relief", Range(0,0.15)) = 0.025
        _Smoothness("Wet Skin Smoothness", Range(0,1)) = 0.56
        _RoughnessVariation("Roughness Variation", Range(0,0.4)) = 0.15
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" }
        LOD 300
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _MottleColor;
            float _MottleStrength;
            float _PatternScale;
            float _DetailScale;
            float _BumpStrength;
            float _Smoothness;
            float _RoughnessVariation;
            float _Cull;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float3 positionOS : TEXCOORD2;
            half fog : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings SkinVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = position.positionCS;
            output.positionWS = position.positionWS;
            output.positionOS = input.positionOS.xyz;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.fog = ComputeFogFactor(position.positionCS.z);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SkinVertex
            #pragma fragment SkinFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float SkinHash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }
            float SkinNoise(float3 p)
            {
                float3 cell = floor(p);
                float3 f = frac(p);
                f = f*f*(3.0-2.0*f);
                return lerp(lerp(lerp(SkinHash(cell), SkinHash(cell+float3(1,0,0)), f.x),
                                 lerp(SkinHash(cell+float3(0,1,0)), SkinHash(cell+float3(1,1,0)), f.x), f.y),
                            lerp(lerp(SkinHash(cell+float3(0,0,1)), SkinHash(cell+float3(1,0,1)), f.x),
                                 lerp(SkinHash(cell+float3(0,1,1)), SkinHash(cell+float3(1,1,1)), f.x), f.y), f.z);
            }
            half4 SkinFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // Object-space detail follows the animal and does not depend on UV seams.
                float3 p = input.positionOS * _PatternScale;
                float mottle = SkinNoise(p) * 0.7 + SkinNoise(p * 3.17) * 0.3;
                float3 porePosition = input.positionOS * _DetailScale;
                // Fade subpixel pores to prevent shimmering at gameplay distances.
                float footprint = max(length(ddx(porePosition)), length(ddy(porePosition)));
                float detailFade = 1.0 - smoothstep(0.35, 1.5, footprint);
                float pores = SkinNoise(porePosition);
                float height = (pores - 0.5) * _BumpStrength * detailFade;
                float3 normal = normalize(input.normalWS);
                float3 dpdx = ddx(input.positionWS);
                float3 dpdy = ddy(input.positionWS);
                float3 r1 = cross(dpdy, normal);
                float3 r2 = cross(normal, dpdx);
                float det = dot(dpdx, r1);
                float3 gradient = sign(det) * (ddx(height)*r1 + ddy(height)*r2);
                normal = normalize(abs(det)*normal - gradient + normal*1e-8);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = lerp(_BaseColor.rgb, _MottleColor.rgb,
                    saturate(mottle * _MottleStrength));
                surface.albedo *= 1.0 + (pores - 0.5) * 0.06 * detailFade;
                surface.metallic = 0;
                surface.specular = half3(0.04,0.04,0.04);
                surface.smoothness = saturate(_Smoothness + (mottle - 0.5) * _RoughnessVariation);
                surface.normalTS = half3(0,0,1);
                surface.occlusion = 1;
                surface.alpha = 1;

                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.positionCS = input.positionCS;
                lighting.normalWS = normal;
                lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    lighting.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #endif
                lighting.bakedGI = SampleSH(normal);
                lighting.vertexLighting = VertexLighting(input.positionWS, normal);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask = half4(1,1,1,1);
                half4 color = UniversalFragmentPBR(lighting, surface);
                color.rgb = MixFog(color.rgb, input.fog);
                return half4(color.rgb,1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            Varyings ShadowVertex(Attributes input)
            {
                Varyings output = SkinVertex(input);
                float3 lightDirection = _LightDirection;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    lightDirection = normalize(_LightPosition - output.positionWS);
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, normalize(output.normalWS), lightDirection));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #endif
                return output;
            }
            half4 DepthFragment(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SkinVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            half4 DepthFragment(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SkinVertex
            #pragma fragment NormalFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            half4 NormalFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normal = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormal = PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(octNormal*0.5+0.5)),0);
                #else
                    return half4(normal,0);
                #endif
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
