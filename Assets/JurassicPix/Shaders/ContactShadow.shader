Shader "Jurassic Pix/Contact Shadow"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB
        ZWrite Off
        Offset -1, -1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float2 _HabitatFogRange;

            struct Input
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float4 color : COLOR;
            };

            Varyings vert(Input v)
            {
                Varyings o;
                o.position = UnityObjectToClipPos(v.vertex);
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float distanceToView = distance(world, _WorldSpaceCameraPos);
                float2 range = _HabitatFogRange.y > 0 ? _HabitatFogRange : float2(13, 58);
                o.color = float4(v.color.rgb, v.color.a * (1 - smoothstep(range.x, range.y, distanceToView)));
                return o;
            }

            float4 frag(Varyings i) : SV_Target { return i.color; }
            ENDCG
        }
    }
}
