Shader "TypingRoguelike/KimonoSway"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _MaskTex ("Sway Mask", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Canvas ("Canvas Size", Vector) = (1024,1536,0,0)
        _Amp ("Amplitude px", Vector) = (3.5,0.8,0,0)
        _Period ("Seconds", Float) = 2
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            sampler2D _MaskTex;
            fixed4 _Color;
            float4 _ClipRect;
            float4 _Canvas;
            float4 _Amp;
            float _Period;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 canvas = max(_Canvas.xy, float2(1, 1));
                float2 pix = float2(i.texcoord.x * canvas.x, (1 - i.texcoord.y) * canvas.y);
                float mask = tex2D(_MaskTex, i.texcoord).r;
                float period = max(_Period, 0.01);
                float ph = _Time.y * 6.2831853 / period;
                float baseWave = ph - pix.y * 0.010 + pix.x * 0.006;
                float2 shifted = float2(
                    pix.x - _Amp.x * mask * sin(baseWave),
                    pix.y - _Amp.y * mask * sin(baseWave + 1.3));
                float2 uv = float2(shifted.x / canvas.x, 1 - shifted.y / canvas.y);
                half4 color = tex2D(_MainTex, uv) * i.color;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
