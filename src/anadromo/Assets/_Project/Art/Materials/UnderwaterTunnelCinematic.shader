Shader "Anadromo/Underwater Tunnel Cinematic"
{
    Properties
    {
        // ── Base Rock & Sediment ────────────────────────────────────
        [MainColor] _BaseColor      ("Rock Base Color",    Color)  = (0.08, 0.12, 0.16, 1)
        _WetColor                   ("Wet / Crevice Dark", Color)  = (0.02, 0.03, 0.05, 1)
        _AlgaeColor                 ("Algae / Moss Tint",  Color)  = (0.04, 0.15, 0.10, 1)

        // ── PBR Surface Detail ─────────────────────────────────────
        _MacroScale                 ("Rock Macro Scale",   Float)  = 1.5
        _MicroScale                 ("Pore Micro Scale",   Float)  = 8.0
        _CreviceDarkness            ("Crevice Occlusion",  Range(0,1)) = 0.85
        _AlgaeCoverage              ("Algae Amount",       Range(0,1)) = 0.45
        _Smoothness                 ("Wet Smoothness",     Range(0,1)) = 0.08
        _NormalStrength             ("Relief Strength",    Range(0,2)) = 1.35

        // ── Distance Fog & Misterioso Dark Veil ────────────────────
        _DarknessStart              ("Abyssal Dark Start (m)", Float) = 3.0
        _DarknessEnd                ("Abyssal Dark End (m)",   Float) = 22.0
        _DarknessStrength           ("Abyssal Veil Density",  Range(0,1)) = 0.96
        _AbyssalFogColor            ("Abyssal Fog Color",     Color)  = (0.005, 0.012, 0.02, 1)

        // ── Bioluminescent & Caustic Accents ───────────────────────
        _GlowColor                  ("Biolum Crevice Glow", Color)  = (0.01, 0.18, 0.14, 1)
        _GlowIntensity              ("Biolum Intensity",    Range(0,0.5)) = 0.08
        _GlowSpeed                  ("Biolum Pulse Speed",  Float)  = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" }
        LOD 300
        Cull Front // Visible desde el interior del túnel

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4  _BaseColor;
            half4  _WetColor;
            half4  _AlgaeColor;
            float  _MacroScale;
            float  _MicroScale;
            float  _CreviceDarkness;
            float  _AlgaeCoverage;
            float  _Smoothness;
            float  _NormalStrength;
            float  _DarknessStart;
            float  _DarknessEnd;
            float  _DarknessStrength;
            half4  _AbyssalFogColor;
            half4  _GlowColor;
            float  _GlowIntensity;
            float  _GlowSpeed;
        CBUFFER_END

        float Hash3D(float3 p)
        {
            p = frac(p * float3(0.1031, 0.1030, 0.0973));
            p += dot(p, p.yxz + 33.33);
            return frac((p.x + p.y) * p.z);
        }

        float ValueNoise3D(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);
            float3 u = f * f * (3.0 - 2.0 * f);
            return lerp(
                lerp(lerp(Hash3D(i),               Hash3D(i + float3(1,0,0)), u.x),
                     lerp(Hash3D(i + float3(0,1,0)), Hash3D(i + float3(1,1,0)), u.x), u.y),
                lerp(lerp(Hash3D(i + float3(0,0,1)), Hash3D(i + float3(1,0,1)), u.x),
                     lerp(Hash3D(i + float3(0,1,1)), Hash3D(i + float3(1,1,1)), u.x), u.y),
                u.z);
        }

        float FBM(float3 p, int octaves)
        {
            float val = 0.0;
            float amp = 0.5;
            for (int i = 0; i < octaves; i++)
            {
                val += amp * ValueNoise3D(p);
                p *= 2.07;
                amp *= 0.5;
            }
            return val;
        }

        float3 CalculateProceduralNormal(float3 posWS, float scale, float eps)
        {
            float nx = FBM(posWS * scale + float3(eps, 0, 0), 3) - FBM(posWS * scale - float3(eps, 0, 0), 3);
            float ny = FBM(posWS * scale + float3(0, eps, 0), 3) - FBM(posWS * scale - float3(0, eps, 0), 3);
            float nz = FBM(posWS * scale + float3(0, 0, eps), 3) - FBM(posWS * scale - float3(0, 0, eps), 3);
            return normalize(float3(nx, ny, nz));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrmInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = nrmInputs.normalWS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 posWS = input.positionWS;
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(posWS);

                // ── 1. Ruido PBR y Oclusión ──────────────────────────
                float macroNoise = FBM(posWS * _MacroScale, 4);
                float microNoise = FBM(posWS * _MicroScale, 3);

                // Oclusión en grietas
                float crevice = saturate(1.0 - macroNoise * 2.1) * _CreviceDarkness;
                float upFacing = saturate(dot(normalize(input.normalWS), float3(0,1,0)) * 0.5 + 0.5);
                float algae = saturate((macroNoise - 0.45) * 3.0) * upFacing * _AlgaeCoverage;

                // Color Base
                half3 albedo = lerp(_WetColor.rgb, _BaseColor.rgb, macroNoise);
                albedo = lerp(albedo, albedo * 0.2, crevice);
                albedo = lerp(albedo, _AlgaeColor.rgb, algae);
                albedo *= lerp(0.82, 1.0, microNoise);

                // ── 2. Normales Procedurales ─────────────────────────
                float3 procN = CalculateProceduralNormal(posWS, _MacroScale, 0.04);
                float3 N = normalize(input.normalWS + procN * _NormalStrength * 0.3);

                // ── 3. Iluminación Directa + Especular Húmeda ────────
                float4 shadowCoord = TransformWorldToShadowCoord(posWS);
                Light mainLight = GetMainLight(shadowCoord);
                float NdotL = saturate(dot(N, mainLight.direction));

                half3 diffuse = mainLight.color * mainLight.shadowAttenuation * NdotL * albedo;
                half3 ambient = SampleSH(N) * albedo * 0.3;

                float smoothness = _Smoothness * (1.0 - crevice * 0.5);
                float3 H = normalize(mainLight.direction + viewDirWS);
                float NdotH = saturate(dot(N, H));
                half3 specular = mainLight.color * pow(NdotH, exp2(smoothness * 9.0 + 1.0)) * smoothness * 0.45 * saturate(1.0 - N.y * 0.85);

                half3 color = diffuse + ambient + specular;

                // ── 4. Destellos Bioluminiscentes ────────────────────
                float pulse = sin(_Time.y * _GlowSpeed * 3.14) * 0.5 + 0.5;
                float glowMask = saturate((FBM(posWS * 4.0, 2) - 0.52) * 5.0) * crevice * pulse;
                color += _GlowColor.rgb * glowMask * _GlowIntensity;

                // ── 5. Velo de Oscuridad por Distancia (Transición) ────
                float cameraDist = length(posWS - _WorldSpaceCameraPos);
                float depthFactor = saturate((cameraDist - _DarknessStart) / max(_DarknessEnd - _DarknessStart, 0.001));
                depthFactor = depthFactor * depthFactor * (3.0 - 2.0 * depthFactor);

                color = lerp(color, _AbyssalFogColor.rgb, depthFactor * _DarknessStrength);

                // ── 6. Niebla de Escena URP ──────────────────────────
                color = MixFog(color, input.fogFactor);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
