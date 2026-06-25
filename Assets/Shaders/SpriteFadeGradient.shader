Shader "Custom/SpriteFadeGradient"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0

        [Header(Gradient Fade)]
        _FadeAngle  ("Fade Angle (0=right  90=up  180=left  270=down)", Range(0, 360)) = 0
        _FadeStart  ("Fade Start  (opaque edge)", Range(0, 1)) = 0.4
        _FadeEnd    ("Fade End    (transparent edge)", Range(0, 1)) = 0.9
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha   // premultiplied-alpha, same as default sprite shader

        Pass
        {
            CGPROGRAM
            #pragma vertex   SpriteVert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float _FadeAngle;
            float _FadeStart;
            float _FadeEnd;

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;

                // Project centered UVs onto the gradient direction
                float2 uv  = IN.texcoord - 0.5;                          // -0.5 .. +0.5
                float  rad = _FadeAngle * (UNITY_PI / 180.0);
                float2 dir = float2(cos(rad), sin(rad));
                float  t   = dot(uv, dir) + 0.5;                         // 0 .. 1 across sprite

                // smoothstep: 0 = opaque side, 1 = transparent side
                float mask = smoothstep(_FadeStart, _FadeEnd, t);

                c.a   *= 1.0 - mask;
                c.rgb *= c.a;   // premultiply for Blend One OneMinusSrcAlpha
                return c;
            }
            ENDCG
        }
    }
}
