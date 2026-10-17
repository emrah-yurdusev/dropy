// Bina inceleme kartındaki maket ve iskelet: köşe rengi × _Color, isteğe bağlı sabit yönlü gölgelendirme.
// Işık sahneden gelmez; maket kendi render texture'ına CommandBuffer ile çizilir.
Shader "Dropy/InspectModel"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Shade ("Shade (0 = düz renk)", Range(0,1)) = 1
        _LightDir ("Light Dir (world)", Vector) = (-0.45, 0.8, -0.4, 0)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Shade;
            float4 _LightDir;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                float lambert = saturate(dot(n, normalize(_LightDir.xyz)));
                // Üst yüz en açık, gölgedeki yüzler koyu ama siyah değil (low-poly düz gölge).
                float light = lerp(1.0, 0.55 + 0.5 * lambert + 0.08 * n.y, _Shade);
                o.color = v.color * _Color;
                o.color.rgb *= light;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}
