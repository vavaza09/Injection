Shader "Custom/SpriteGaussianBlur"
{
    Properties
    {
        _MainTex  ("Sprite Texture", 2D)             = "white" {}
        _Color    ("Tint",           Color)           = (1,1,1,1)
        _BlurSize ("Blur Size (px)", Range(0, 10))   = 2
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull   Off
        ZWrite Off

        Pass
        {
            Name "SpriteGaussianBlur"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize; // (.xy = 1/size, .zw = size)
                float4 _Color;
                float  _BlurSize;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color      = IN.color * _Color;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // Convert pixel offset to UV offset
                float2 o = _MainTex_TexelSize.xy * _BlurSize;

                // 3x3 Gaussian kernel  (weights: 1 2 1 / 2 4 2 / 1 2 1, sum = 16)
                float4 col =
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-o.x, -o.y)) * 1.0 +
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( 0,   -o.y)) * 2.0 +
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( o.x, -o.y)) * 1.0 +
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-o.x,  0  )) * 2.0 +
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv                      ) * 4.0 +
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( o.x,  0  )) * 2.0 +
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-o.x,  o.y)) * 1.0 +
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( 0,    o.y)) * 2.0 +
                    SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( o.x,  o.y)) * 1.0;

                col /= 16.0;
                col *= IN.color;
                return col;
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/InternalErrorShader"
}
