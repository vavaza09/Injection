Shader "Custom/SpriteSilhouette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SilhouetteColor ("Silhouette Color", Color) = (0.4,0.6,1,1)
        _RimColor ("Rim Color", Color) = (0.6,0.8,1,1)
        [Range(0,5)] _RimPower ("Rim Power", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
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
            #pragma fragment SilhouetteFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float4 _SilhouetteColor;
            float4 _RimColor;
            float _RimPower;

            fixed4 SilhouetteFrag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;

                fixed3 col = _SilhouetteColor.rgb;

                if (_RimPower > 0.001)
                {
                    float2 dx = float2(0.003, 0);
                    float2 dy = float2(0, 0.003);
                    float aL = SampleSpriteTexture(IN.texcoord - dx).a;
                    float aR = SampleSpriteTexture(IN.texcoord + dx).a;
                    float aU = SampleSpriteTexture(IN.texcoord + dy).a;
                    float aD = SampleSpriteTexture(IN.texcoord - dy).a;
                    float edge = saturate(_RimPower * (4.0 * c.a - aL - aR - aU - aD));
                    col = lerp(col, _RimColor.rgb, edge);
                }

                // Replace sprite colour with silhouette colour, keep shape from alpha
                c.rgb = col;
                c.rgb *= c.a; // premultiply (same as SpriteFlash)
                return c;
            }
            ENDCG
        }
    }
}
