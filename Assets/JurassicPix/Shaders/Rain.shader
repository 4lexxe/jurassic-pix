Shader "Hidden/Jurassic Pix/Rain"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _ForestNight;
            struct Input { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Interpolated { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            Interpolated vert(Input v)
            {
                Interpolated o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(Interpolated i) : SV_Target
            {
                i.color.rgb *= lerp(1, .07, _ForestNight);
                float core = 1 - abs(i.uv.x * 2 - 1);
                i.color.a *= core * core * smoothstep(0, .22, i.uv.y) * (1 - smoothstep(.5, 1, i.uv.y));
                return i.color;
            }
            ENDCG
        }
    }
}
