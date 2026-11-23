// 着弾エフェクト用 UI シェーダー（グラデーションマップでパレット着色）
// _MainTex : mask スプライト（R=熱 0..1, A=形）。Import は sRGB(Color Texture) を OFF にする。
// _RampTex : ramps/StrikeRamp_<Palette>.png（横256px）。Wrap=Clamp, Filter=Bilinear, Mipmap OFF。
// Image.color の a で全体の不透明度（UIに重なる分の 35% など）を掛けられる。
Shader "Game/UI/StrikeFxRamp"
{
    Properties
    {
        [PerRendererData] _MainTex ("Mask (R=heat, A=alpha)", 2D) = "white" {}
        _RampTex ("Palette Ramp", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _HeatBoost ("Heat Boost", Range(0.5, 1.5)) = 1.0

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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; float4 worldPosition : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            sampler2D _MainTex; float4 _MainTex_ST;
            sampler2D _RampTex;
            fixed4 _Color; float _HeatBoost;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 m = tex2D(_MainTex, i.texcoord);
                float heat = saturate(m.r * _HeatBoost);
                fixed3 rgb = tex2D(_RampTex, float2(heat, 0.5)).rgb * i.color.rgb;
                fixed a = m.a * i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(a - 0.001);
                #endif
                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
}
