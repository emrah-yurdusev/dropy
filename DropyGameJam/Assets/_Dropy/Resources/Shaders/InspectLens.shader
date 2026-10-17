// Bina inceleme kartı: dışarıda maket (_MainTex), büyüteç dairesinin içinde iskelet (_WireTex).
// Merkez ve yarıçap piksel cinsinden; iskelet merkez etrafında _Zoom kadar büyütülür.
Shader "Dropy/InspectLens"
{
    Properties
    {
        [PerRendererData] _MainTex ("Model", 2D) = "white" {}
        _WireTex ("Wire", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _LensCenter ("Lens Center (uv)", Vector) = (0.5, 0.5, 0, 0)
        _ViewSize ("View Size (px)", Vector) = (400, 300, 0, 0)
        _LensRadius ("Lens Radius (px)", Float) = 70
        _LensOn ("Lens On", Float) = 0
        _Zoom ("Zoom", Float) = 1.25
        _RimColor ("Rim Color", Color) = (0.12, 0.09, 0.07, 1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            sampler2D _WireTex;
            fixed4 _Color;
            float4 _LensCenter;
            float4 _ViewSize;
            float _LensRadius;
            float _LensOn;
            float _Zoom;
            fixed4 _RimColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 model = tex2D(_MainTex, i.uv);
                float2 px = (i.uv - _LensCenter.xy) * _ViewSize.xy;
                float d = length(px);
                float r = _LensRadius;
                if (_LensOn < 0.5 || d > r + 7.0)
                    return model * i.color;

                // Büyüteç: iskelet merkez etrafında büyütülür, kenara doğru hafif fıçı bükülmesi.
                float bend = 1.0 + 0.12 * (d / r) * (d / r);
                float2 wireUv = _LensCenter.xy + (i.uv - _LensCenter.xy) / (_Zoom * bend);
                fixed4 wire = tex2D(_WireTex, wireUv);
                // Kenarda içe doğru gölge ve sol üstte cam parıltısı.
                float vignette = smoothstep(r, r * 0.55, d);
                wire.rgb *= lerp(0.55, 1.0, vignette);
                float2 glintPos = float2(-0.38, 0.38) * r;
                float glint = smoothstep(r * 0.32, 0.0, length(px - glintPos)) * 0.18;
                wire.rgb += glint;
                wire.a = 1.0;

                float inside = smoothstep(r + 1.0, r - 1.0, d);
                float rim = smoothstep(r - 1.0, r + 1.0, d) * smoothstep(r + 7.0, r + 5.0, d);
                fixed4 col = lerp(model, wire, inside);
                col = lerp(col, _RimColor, rim);
                // Çerçevenin iç kenarında ince açık halka.
                float edge = smoothstep(2.0, 0.0, abs(d - (r - 1.5)));
                col.rgb = lerp(col.rgb, float3(0.85, 0.75, 0.6), edge * 0.6);
                return col * i.color;
            }
            ENDCG
        }
    }
}
