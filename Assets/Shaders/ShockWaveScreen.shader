Shader "Custom/ShockWaveScreen"
{
    Properties
    {
        _MainTex             ("Sprite Texture",           2D)     = "white" {}
        _RingSpawnPosition   ("Ring Spawn Position",      Vector) = (0.5, 0.5, 0, 0)
        _WaveDistanceFromCenter ("Wave Distance",         Float)  = -1
        _ShockWaveStrength   ("ShockWave Strength",       Float)  = 0.05
        _Size                ("Ring Width",               Float)  = 0.08
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline"  = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest  Always
        Cull   Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Populated by the URP 2D renderer when useCameraSortingLayerTexture is enabled.
            TEXTURE2D(_CameraSortingLayerTexture);
            SAMPLER(sampler_CameraSortingLayerTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _RingSpawnPosition;
                float  _WaveDistanceFromCenter;
                float  _ShockWaveStrength;
                float  _Size;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.screenPos  = ComputeScreenPos(OUT.positionCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // Screen-space UV (0..1)
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                float2 diff = screenUV - _RingSpawnPosition.xy;
                float  dist = length(diff);

                // Ring band centred on _WaveDistanceFromCenter, width = _Size
                float waveOffset = dist - _WaveDistanceFromCenter;
                float ring = smoothstep(_Size, 0.0, abs(waveOffset));

                // Inactive until wave starts
                if (_WaveDistanceFromCenter < -0.05 || ring < 0.001)
                    return half4(0, 0, 0, 0);

                // Push UVs outward from the spawn centre
                float2 dir = normalize(diff + float2(0.0001, 0.0001));
                float2 distortedUV = screenUV + dir * ring * _ShockWaveStrength;

                half4 col = SAMPLE_TEXTURE2D(
                    _CameraSortingLayerTexture,
                    sampler_CameraSortingLayerTexture,
                    distortedUV);
                col.a = ring;
                return col;
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/InternalErrorShader"
}
