// StockPanel v4.4 — "ゆらゆら" flame for uGUI (社外秘)
// Route A. Built from Unity's UI/Default structure (stencil / RectMask2D / alpha clip), so it is render-pipeline agnostic
// for Canvas UI (Built-in and URP Screen Space Overlay/Camera). NOT compiled/tested in a Unity editor by the art side — see README.
// Use on a RawImage (texture = flame_lum_A, uvRect 0,0,1,1) so UV = 0..1 over the 86px(1x)/172px(2x) square sprite.
// One material instance per slot (different _Phase). Glow halo = separate Image (flame_<C>_glowhalo) under this one.
// Math = flame4.py (bake) 1:1; numpy emulation vs bake: mean |diff| 1.3-3.0 / 255 (Reference/shader_emulation_vs_baked.png).
Shader "StockPanel/UI_FlameSway"
{
    Properties
    {
        [PerRendererData] _MainTex ("Lum A (set by RawImage)", 2D) = "black" {}
        _LumB ("Lum B", 2D) = "black" {}
        _Ramp ("Colour ramp (flame_ramp_<C>)", 2D) = "white" {}
        _Phase ("Time offset (s) per slot", Float) = 0
        _Period ("Loop period (s)", Float) = 3.6
        _Amp ("Motion amount", Range(0, 1.5)) = 1
        _LeanDeg ("Lean (deg)", Float) = 2.6
        _Tip ("Tip flow", Float) = 0.085
        _Breath ("Breathing scale", Float) = 0.022
        _Pulse ("Inner glow pulse", Float) = 0.055
        _Color ("Tint", Color) = (1,1,1,1)

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
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 worldPosition : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            sampler2D _MainTex; sampler2D _LumB; sampler2D _Ramp;
            float _Phase, _Period, _Amp, _LeanDeg, _Tip, _Breath, _Pulse; fixed4 _Color; float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex; o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord; o.color = v.color * _Color; return o;
            }
            // looped noise (same 8 waves as the bake; period = _Period)
            static const float4 W[8] = {
        float4(-2.542150, 6.009635, 1.0, 1.934320),
        float4(-1.416775, 2.890345, 3.0, 1.685797),
        float4(-0.000705, 5.556535, 1.0, 2.467678),
        float4(1.045983, 5.525428, 1.0, 1.340060),
        float4(-0.287256, 6.690427, 2.0, 4.714790),
        float4(-1.470636, 6.330826, 2.0, 3.444517),
        float4(2.454770, 3.099263, 1.0, 2.244945),
        float4(-1.839866, 2.952133, 2.0, 3.854058)
            };
            static const float WA[8] = { 1.00, 0.80, 0.60, 0.50, 0.40, 0.35, 0.30, 0.25 };
            float noise8(float u, float v, float t, float seed)
            {
                float n = 0;
                [unroll] for (int i = 0; i < 8; i++)
                    n += WA[i] * sin(W[i].x * u + W[i].y * v + 6.2831853 * W[i].z * t / _Period + W[i].w + seed * 1.3);
                return n / 2.6;
            }
            float ss(float e0, float e1, float x) { float k = saturate((x - e0) / (e1 - e0)); return k * k * (3 - 2 * k); }
            // texture frame: x = 0.5 + 0.3u, y_down = 0.58 + 0.3v  ->  uv = (x, 1 - y_down); outside = 0
            float lum(sampler2D tex, float u, float v)
            {
                float2 uv = float2(0.5 + 0.3 * u, 1.0 - (0.58 + 0.3 * v));
                float inside = step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
                return tex2D(tex, uv).r * inside;
            }
            fixed4 frag(v2f IN) : SV_Target
            {
                float t = fmod(_Time.y + _Phase, _Period);
                float p = 6.2831853 * t / _Period;
                float b = 0.5 + 0.5 * sin(p);                         // A<->B dissolve
                float lean = radians(_LeanDeg) * sin(p) * _Amp;
                float tip = _Tip * sin(2 * p + 0.8) * _Amp;
                float breath = 1 + _Breath * sin(3 * p + 1.3) * _Amp;
                float pulse = 1 + _Pulse * sin(4 * p + 2.1) * _Amp;
                float u = (IN.uv.x - 0.5) / 0.3;
                float v = ((1 - IN.uv.y) - 0.58) / 0.3;               // v grows downward, ball centre = 0, ball radius = 1
                u = u / breath; v = 1 + (v - 1) / breath;             // breathe about the ball bottom
                float ca = cos(-lean), sa = sin(-lean); float pu = u, pv = v - 1;
                u = pu * ca - pv * sa; v = pu * sa + pv * ca + 1;     // lean about the bottom pivot
                float hgt = max(0, -(v + 0.2));
                u = u - tip * pow(hgt, 1.4) - 0.035 * _Amp * hgt * noise8(u * 1.2, v * 1.2, t, 2);
                v = v + 0.02 * _Amp * noise8(u * 1.5, v * 1.5, t, 7) * (0.4 + hgt);
                u = u + 0.012 * _Amp * noise8(u * 2.0, v * 2.0, t, 9);
                float l = lerp(lum(_MainTex, u, v), lum(_LumB, u, v), b);
                float d = sqrt(u * u + v * v); float ball = ss(0.92, 0.85, d);
                l = l * (1 + (pulse - 1) * (1 - ss(0.0, 0.9, d)));
                l = saturate(max(l * 0.94, 0.30 * ball));
                fixed3 rgb = tex2D(_Ramp, float2(l * (255.0 / 256.0) + 0.5 / 256.0, 0.5)).rgb;
                float a = max(pow(saturate((l - 0.07) / 0.28), 0.9), ball);
                fixed4 col = fixed4(rgb, a) * IN.color;
                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif
                return col;
            }
        ENDCG
        }
    }
    Fallback "UI/Default"
}
