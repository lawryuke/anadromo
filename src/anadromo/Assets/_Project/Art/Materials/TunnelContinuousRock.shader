Shader "Anadromo/Tunnel Continuous Rock"
{
    Properties
    {
        [MainColor] _BaseColor("Wet stone tint", Color) = (.38,.36,.33,1)
        _Smoothness("Wet smoothness", Range(0,1)) = .2
        _BumpScale("Rock relief", Range(0,2)) = 1.15
        [HideInInspector] _BaseMap("Base", 2D) = "white" {}
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _Cutoff("Cutoff", Float) = .5
        [HideInInspector] _AlphaToMask("Alpha to mask", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Lit" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LitPassVertex
            #pragma fragment RockFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            float Hash(float3 p)
            {
                p = frac(p * .1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }
            float Noise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i+float3(1,0,0)), f.x),
                    lerp(Hash(i+float3(0,1,0)), Hash(i+float3(1,1,0)), f.x), f.y),
                    lerp(lerp(Hash(i+float3(0,0,1)), Hash(i+float3(1,0,1)), f.x),
                    lerp(Hash(i+float3(0,1,1)), Hash(i+float3(1,1,1)), f.x), f.y), f.z);
            }
            half4 RockFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 p = input.positionWS;
                // Continuous world-space stone: no atlas UV islands stretched across the bore.
                float macro = Noise(p * 1.6);
                float mineral = Noise(p * 5.5 + macro * 2);
                float grain = Noise(p * 32.0);
                float strata = .5 + .5 * sin(p.y * 14 + macro * 8 + p.x * .8);
                float fissure = pow(saturate(1 - abs(mineral - .46) * 14), 3);
                float h = macro * .10 + mineral * .025 + grain * .004
                    + strata * .014 - fissure * .013;
                float3 n = normalize(input.normalWS);
                float3 dx = ddx(p), dy = ddy(p);
                float3 r1 = cross(dy, n), r2 = cross(n, dx);
                float det = dot(dx, r1);
                float3 gradient = (r1 * ddx(h) + r2 * ddy(h)) * sign(det) / max(abs(det), 1e-7);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = _BaseColor.rgb * lerp(.50, 1.22, macro);
                surface.albedo *= lerp(.86, 1.08, grain) * lerp(.8, 1, strata);
                surface.albedo *= 1 - .36 * fissure;
                surface.alpha = 1;
                surface.smoothness = _Smoothness * lerp(.65, 1, mineral) * (1 - .35 * fissure);
                surface.occlusion = lerp(.65, 1, macro) * (1 - .3 * fissure);
                surface.normalTS = half3(0,0,1);
                InputData lighting;
                InitializeInputData(input, surface.normalTS, lighting);
                lighting.normalWS = normalize(n - gradient * _BumpScale);
                InitializeBakedGIData(input, lighting);
                half4 color = UniversalFragmentPBR(lighting, surface);
                color.rgb = MixFog(color.rgb, lighting.fogCoord);
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
