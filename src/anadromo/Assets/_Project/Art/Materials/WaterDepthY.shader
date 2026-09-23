Shader "Anadromo/Water Depth Y"
{
    Properties
    {
        _SurfaceY ("Water surface (world Y)", Float) = 10
        _ShallowY ("Turquoise zone (world Y)", Float) = 7
        _DeepY ("Dark blue zone (world Y)", Float) = -18
        _ShallowColor ("Shallow water", Color) = (0.035, 0.36, 0.4, 1)
        _DeepColor ("Deep water", Color) = (0.008, 0.025, 0.09, 1)
        _Density ("Water haze (not depth darkening)", Range(0, 0.1)) = 0.025
        _VisibilityStart ("Visibility fade start (metres)", Float) = 5
        _VisibilityEnd ("Visibility fade end (metres)", Float) = 10
        _DeepLight ("Light remaining at depth", Range(0, 1)) = 0.12
        _OpenWaterDistance ("Open water sampling distance", Float) = 60
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _SurfaceY, _ShallowY, _DeepY, _Density, _DeepLight, _OpenWaterDistance;
                float _VisibilityStart, _VisibilityEnd;
                float4 _ShallowColor, _DeepColor;
            CBUFFER_END

            float DepthBlend(float worldY)
            {
                return smoothstep(0.0, 1.0, saturate((_ShallowY - worldY) / max(0.1, _ShallowY - _DeepY)));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool sky = raw < 0.00001;
                    float deviceDepth = max(raw, 0.00001);
                #else
                    bool sky = raw > 0.99999;
                    float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, min(raw, 0.99999));
                #endif
                float3 cameraWS = GetCameraPositionWS();
                float3 pointWS = ComputeWorldSpacePosition(uv, deviceDepth, UNITY_MATRIX_I_VP);
                float3 ray = normalize(pointWS - cameraWS);
                float distanceToPoint = sky ? _OpenWaterDistance : length(pointWS - cameraWS);
                pointWS = cameraWS + ray * distanceToPoint;

                // Clip the optical path to the underwater part of the ray.
                float entry = 0.0;
                float exitDistance = distanceToPoint;
                if (cameraWS.y > _SurfaceY)
                {
                    if (ray.y >= -0.0001) return source;
                    entry = (_SurfaceY - cameraWS.y) / ray.y;
                }
                else if (ray.y > 0.0001)
                    exitDistance = min(exitDistance, (_SurfaceY - cameraWS.y) / ray.y);
                float waterDistance = max(0.0, exitDistance - entry);
                if (waterDistance <= 0.0) return source;

                float endY = cameraWS.y + ray.y * exitDistance;
                float depth = DepthBlend(endY);
                float middleY = cameraWS.y + ray.y * (entry + exitDistance) * 0.5;
                float haze = 1.0 - exp(-_Density * waterDistance * lerp(1.0, 1.8, DepthBlend(middleY)));
                // The fog endpoint must match the empty-water background on the SAME ray.
                // Sampling the terrain's Y here instead created silhouettes at fully fogged edges.
                float backgroundDistance = max(_OpenWaterDistance, _VisibilityEnd);
                if (ray.y > 0.0001)
                    backgroundDistance = min(backgroundDistance, max(0.0, (_SurfaceY - cameraWS.y) / ray.y));
                float backgroundY = cameraWS.y + ray.y * backgroundDistance;
                float3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, DepthBlend(backgroundY));
                float fadeStart = max(0.0, _VisibilityStart);
                float fadeEnd = max(fadeStart + 0.01, _VisibilityEnd);
                float visibilityFade = saturate((waterDistance - fadeStart) / (fadeEnd - fadeStart));
                haze = 1.0 - (1.0 - haze) * (1.0 - visibilityFade);
                // Darkness depends on world Y, not view-space Z or camera pitch.
                float3 lit = source.rgb * lerp(1.0, _DeepLight, depth);
                return half4(sky ? waterColor : lerp(lit, waterColor, haze), source.a);
            }
            ENDHLSL
        }
    }
}
