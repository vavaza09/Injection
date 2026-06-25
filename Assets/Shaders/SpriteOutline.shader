Shader "Custom/SpriteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineSize ("Outline Size (px)", Float) = 2
        [Range(0,1)] _AlphaThreshold ("Alpha Threshold", Float) = 0.5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment OutlineFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float4 _OutlineColor;
            float  _OutlineSize;
            float  _AlphaThreshold;
            float4 _MainTex_TexelSize;

            fixed4 OutlineFrag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;

                if (c.a < _AlphaThreshold)
                {
                    float2 offset = _MainTex_TexelSize.xy * _OutlineSize;
                    float n = SampleSpriteTexture(IN.texcoord + float2(      0,  offset.y)).a;
                    float s = SampleSpriteTexture(IN.texcoord + float2(      0, -offset.y)).a;
                    float e = SampleSpriteTexture(IN.texcoord + float2( offset.x,       0)).a;
                    float w = SampleSpriteTexture(IN.texcoord + float2(-offset.x,       0)).a;

                    if (max(max(n, s), max(e, w)) >= _AlphaThreshold)
                    {
                        fixed4 outline = _OutlineColor;
                        outline.rgb *= outline.a;
                        return outline;
                    }
                }

                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
