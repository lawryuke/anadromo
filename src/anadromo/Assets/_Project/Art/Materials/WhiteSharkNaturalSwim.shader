Shader "Anadromo/White Shark Natural Swim"
{
    Properties
    {
        _DorsalColor("Dorsal Color (Lomo)", Color) = (0.09, 0.13, 0.17, 1.0)
        _VentralColor("Ventral Color (Vientre)", Color) = (0.92, 0.94, 0.96, 1.0)
        _CounterShadeHeight("Countershade Height", Range(-1.0, 1.0)) = 0.0
        _CounterShadeSoftness("Countershade Softness", Range(0.01, 1.0)) = 0.20
        _ColorBlend("Vertex Color Blend", Range(0.0, 1.0)) = 0.15
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.70
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.04
        _FresnelStrength("Fresnel Sheen", Range(0.0, 1.0)) = 0.40
        _SwimPhase("Swim phase", Float) = 0
        _SwimStrength("Swim strength", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" }
        LOD 300
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _DorsalColor;
            float4 _VentralColor;
            float _CounterShadeHeight;
            float _CounterShadeSoftness;
            float _ColorBlend;
            float _Smoothness;
            float _Metallic;
            float _FresnelStrength;
            float _SwimPhase;
            float _SwimStrength;
            float _Cull;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float3 positionOS : TEXCOORD2;
            half fog : TEXCOORD3;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings SkinVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float3 p = input.positionOS.xyz;
            // The source mesh faces +Z. An increasing wave travels rearward along the body.
            float amplitude = 0.45 * _SwimStrength;
            
            // 1. Rearward body undulation (Spine & Tail)
            float q = saturate((0.35 - p.z) / 3.6);
            float wave = _SwimPhase - q * 4.8;
            float bend = amplitude * pow(q, 2.2) * sin(wave);
            float slope = -amplitude / 3.6 * (2.2 * pow(q, 1.2) * sin(wave) - 4.8 * pow(q, 2.2) * cos(wave));
            if (q <= 0 || q >= 1) slope = 0;
            p.x += bend;
            
            // Caudal fin tip whip (secondary phase lag for tail elasticity)
            float caudalMask = saturate((q - 0.65) / 0.35);
            float caudalWhip = amplitude * 0.22 * caudalMask * caudalMask * sin(wave - 1.15);
            p.x += caudalWhip;
            input.normalOS.z -= slope * input.normalOS.x;
            
            // 2. Head counter-oscillation (Natural head sway out of phase with tail)
            float headQ = saturate((p.z - 0.35) / 1.5);
            float headBend = -0.11 * amplitude * pow(headQ, 1.3) * sin(_SwimPhase + 0.35);
            p.x += headBend;
            
            // 3. Pectoral Fins (Aleteos naturales con aleteo vertical y pitch twist)
            float sideSign = sign(input.positionOS.x);
            float finSpan = saturate((abs(input.positionOS.x) - 0.25) / 0.65);
            float finMask = (1.0 - smoothstep(0.12, 0.75, abs(input.positionOS.y))) *
                            (1.0 - smoothstep(0.15, 1.75, abs(input.positionOS.z - 0.5)));
            
            float finPhase = _SwimPhase * 0.5;
            float finFlap = sin(finPhase + sideSign * 0.4) * 0.085 * _SwimStrength;
            float finTwist = cos(finPhase + sideSign * 0.4) * 0.06 * _SwimStrength * (input.positionOS.z - 0.5);
            
            p.y += (finFlap + finTwist) * finSpan * finSpan * finMask;
            p.x += sideSign * finSpan * finSpan * finMask * 0.025 * sin(finPhase);

            // 4. Dorsal Fin Subtle Flex
            float dorsalMask = saturate((p.y - 0.35) / 0.5) * (1.0 - smoothstep(0.0, 1.4, abs(p.z - 0.1)));
            p.x += dorsalMask * 0.04 * amplitude * sin(wave - 0.4);

            input.normalOS = normalize(input.normalOS);
            output.color = input.color;
            VertexPositionInputs position = GetVertexPositionInputs(p);
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

            half4 SkinFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normal = normalize(input.normalWS);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Realist counter-shading
                float heightNorm = input.positionOS.y;
                float normalUp = normal.y;
                float blendFactor = saturate((heightNorm - _CounterShadeHeight) / max(0.001, _CounterShadeSoftness));
                blendFactor = saturate(blendFactor * 0.6 + saturate(normalUp * 0.5 + 0.5) * 0.4);

                half3 baseSharkColor = lerp(_VentralColor.rgb, _DorsalColor.rgb, blendFactor);

                // Fin tips & tail edge subtle darkening
                float finDarkening = saturate((input.positionOS.y - 0.35) * 1.8);
                finDarkening += saturate((-input.positionOS.z - 1.2) * 0.4);
                half3 finalColor = lerp(baseSharkColor, baseSharkColor * 0.72, finDarkening * 0.3);

                // Blend with vertex colors if present
                finalColor = lerp(finalColor, input.color.rgb * finalColor * 1.5, _ColorBlend);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = finalColor;
                surface.metallic = _Metallic;
                surface.specular = half3(0.04, 0.04, 0.04);
                surface.smoothness = _Smoothness;
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 1;
                surface.alpha = 1;

                InputData lighting = (InputData)0;
                lighting.positionWS = input.positionWS;
                lighting.positionCS = input.positionCS;
                lighting.normalWS = normal;
                lighting.viewDirectionWS = viewDir;
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    lighting.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #endif
                lighting.bakedGI = SampleSH(normal);
                lighting.vertexLighting = VertexLighting(input.positionWS, normal);
                lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask = half4(1,1,1,1);
                half4 color = UniversalFragmentPBR(lighting, surface);

                // Wet skin Fresnel rim sheen
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), 4.0) * _FresnelStrength;
                color.rgb += fresnel * half3(0.12, 0.18, 0.24);

                color.rgb = MixFog(color.rgb, input.fog);
                return half4(color.rgb, 1);
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
