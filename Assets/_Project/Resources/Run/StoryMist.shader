Shader "TypingRoguelike/UI/StoryMist"
{
    Properties
    {
        [PerRendererData] _MainTex ("Cloud noise", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            sampler2D _MainTex;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                // Two differently moving cloud scales avoid a flat, sliding sheet.
                float broad = tex2D(_MainTex, i.uv).r;
                float detail = tex2D(_MainTex, -i.uv * 1.7 + float2(0.37, 0.19)).r;
                float cloud = saturate(broad * 0.7 + detail * 0.3);
                // Vertex alpha controls density, not uniform screen opacity.
                // Both endpoints are exact: clear at 0, completely covered at 1.
                float density = lerp(-0.25, 1.25, i.color.a);
                float alpha = smoothstep(cloud - 0.18, cloud + 0.18, density);
                float light = smoothstep(0.12, 0.82, cloud);
                fixed3 fog = lerp(fixed3(0.23, 0.25, 0.29), fixed3(0.59, 0.62, 0.66), light);
                return fixed4(fog * i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
