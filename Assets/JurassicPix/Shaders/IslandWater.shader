Shader "Jurassic Pix/Island Water"
{
    Properties
    {
        _Bed ("Elevation of the island", 2D) = "black" {}
        _IslandRect ("Map min and size", Vector) = (-466,-42,932,932)
        _IslandScale ("Island scale", Float) = 1
        _Lagoon ("Lagoon", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Geometry+20" "RenderType"="Opaque" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _Bed;
            float4 _IslandRect, _ForestSunDirection, _HabitatFog, _ValleyWeather;
            float _ForestNight, _Lagoon, _IslandScale;
            float4 _IslandLakes[8];
            float _IslandLakeLevels[8];

            struct Output { float4 clip : SV_POSITION; float3 world : TEXCOORD0; };

            Output vert(appdata_base v)
            {
                Output o;
                o.clip = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(Output i) : SV_Target
            {
                float2 p = i.world.xz;
                float2 uv = (p - _IslandRect.xy) / _IslandRect.zw;
                float inside = step(0, uv.x) * step(0, uv.y) * step(uv.x, 1) * step(uv.y, 1);
                float bed = lerp(-43, tex2D(_Bed, saturate(uv)).r * 94 - 44, inside);
                clip(i.world.y - bed + 1.15);
                float depth = max(0, i.world.y - bed);
                if (_Lagoon > .5)
                {
                    float within = 0;
                    for (int k = 0; k < 8; k++)
                    {
                        float2 local = p - _IslandLakes[k].xy;
                        float theta = atan2(local.y, local.x);
                        float rim = 1 + .094 * sin(theta * 3 + .48) + .04 * cos(theta * 5);
                        if (_IslandLakes[k].z > 0 && abs(i.world.y - _IslandLakeLevels[k]) < .5)
                            within = max(within, step(length(local / _IslandLakes[k].zw), rim + .055));
                    }
                    clip(within - .5);
                }

                float t = _ValleyWeather.w;
                float2 cell = floor(p * 2.2) / 2.2;
                float a = sin(dot(cell, float2(.22,.44)) + t * 1.5);
                float b = sin(dot(cell, float2(-.37,.29)) - t * (.88 + _ValleyWeather.x));
                float ripple = a * .58 + b * .42;
                float3 deep = lerp(float3(.033,.147,.20), float3(.049,.218,.217), _Lagoon);
                float3 shallow = lerp(float3(.11,.44,.39), float3(.18,.51,.38), _Lagoon);
                float3 color = lerp(shallow, deep, smoothstep(.8, 15, depth));
                color *= 1 + ripple * (.09 + _ValleyWeather.x * .07);
                float surf = 1 - smoothstep(.21,.68, abs(sin(depth * 1.37 + a * .38 - t * 1.71)));
                float foam = surf * (1 - smoothstep(.35, 2.6, depth)) * (.41 - _Lagoon * .19);
                color = lerp(color, float3(.66,.79,.69), foam);
                float3 view = normalize(_WorldSpaceCameraPos - i.world);
                float3 normal = normalize(float3(.085*a, 1, .12*b));
                float glint = pow(saturate(dot(normal, normalize(view + _ForestSunDirection.xyz))), 35);
                color += glint * lerp(float3(.35,.31,.19), float3(.5,.6,.8), _ForestNight) * (1 - _ValleyWeather.y);
                // Las gotas de lluvia marcan la superficie con puntos claros que saltan de lugar.
                float2 drop = floor(p * 1.7) + floor(t * 9) * float2(5.3, 9.1);
                float ring = step(.955, frac(sin(dot(drop, float2(12.9898, 78.233))) * 43758.5453));
                color += ring * _ValleyWeather.y * .13 * (1 - smoothstep(60, 220, length(_WorldSpaceCameraPos.xyz - i.world)));
                color *= lerp(1,.025,_ForestNight) * lerp(1,.76,_ValleyWeather.y);
                color += _ValleyWeather.z * .19;
                float distance = length(_WorldSpaceCameraPos.xyz - i.world);
                return float4(lerp(color, _HabitatFog.rgb, smoothstep(225,1050,distance / sqrt(_IslandScale))), .14);
            }
            ENDCG
        }
    }
}
