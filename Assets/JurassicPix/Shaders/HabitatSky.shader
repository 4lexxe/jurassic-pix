Shader "Jurassic Pix/Habitat Sky"
{
    Properties
    {
        _DayPanorama ("Day panorama", 2D) = "gray" {}
        _NightPanorama ("Night panorama", 2D) = "black" {}
        _DayRotation ("Day rotation", Range(0,1)) = 0.15
        _NightRotation ("Night rotation", Range(0,1)) = 0.63
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _DayPanorama, _NightPanorama;
            float _DayRotation, _NightRotation, _ForestNight;
            float4 _HabitatFog, _ValleyWeather, _ForestSunDisk, _ForestMoonDirection, _StormBolt;

            float Hash(float2 p)
            {
                p = frac(p * float2(.1031, .1099));
                p += dot(p, p.yx + 19.19);
                return frac((p.x + p.y) * p.x);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 d = frac(p);
                d = d * d * (3 - 2 * d);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), d.x),
                    lerp(Hash(cell + float2(0,1)), Hash(cell + 1), d.x), d.y);
            }

            float3 Stars(float3 ray, float scale, float rarity)
            {
                float3 p = ray * scale;
                float3 cell = floor(p);
                float seed = Hash(float2(cell.x + cell.z * 17.31, cell.y - cell.z * 9.73));
                float3 place = .25 + .5 * float3(Hash(float2(cell.x + 7.3, cell.y + cell.z * 5.7)),
                    Hash(float2(cell.y + 13.1, cell.z + cell.x * 3.1)), Hash(float2(cell.z + 2.9, cell.x + cell.y * 9.4)));
                float spark = smoothstep(.22, 0, length(p - cell - place)) * step(rarity, seed);
                float twinkle = .68 + .32 * sin(_Time.y * (1.4 + seed * 5) + seed * 47);
                return spark * twinkle * (.35 + 1.5 * frac(seed * 13.7)) * lerp(float3(1, .86, .7), float3(.75, .86, 1), frac(seed * 5.3));
            }

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings vert(appdata_base v)
            {
                Varyings o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.direction = v.vertex.xyz;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 ray = normalize(i.direction);
                float2 uv = float2(atan2(ray.z, ray.x) * (0.5 / UNITY_PI) + .5,
                    asin(clamp(ray.y, -1, 1)) / UNITY_PI + .5);
                float3 day = tex2D(_DayPanorama, uv + float2(_DayRotation, 0)).rgb * .89;
                float3 night = float3(.0006, .0011, .0032) + float3(.0012, .0022, .0052) * pow(1 - saturate(ray.y), 3);
                float3 color = lerp(day, night, _ForestNight);
                float3 sky = color / (.75 + color);
                float rain = _ValleyWeather.y;
                float lane = dot(ray, float3(.518, .309, .797)) * 4.2;
                float band = exp(-lane * lane);
                float dust = Noise(ray.xz * 6 + float2(3, 0)) * .6 + Noise(ray.yz * 13 + float2(0, 5)) * .4;
                float3 stars = Stars(ray, 80, .8) + Stars(ray, 170, .62) * (.22 + band * .5);
                for (int meteor = 0; meteor < 2; meteor++)
                {
                    float period = 7 + meteor * 4.3;
                    float slot = floor(_Time.y / period) + meteor * 31;
                    float along = frac(_Time.y / period) * 4.5;
                    float h1 = Hash(float2(slot, 1.7)), h2 = Hash(float2(slot, 9.2)), h3 = Hash(float2(slot, 4.4));
                    float3 origin = normalize(float3(cos(h1 * 6.283) * .8, .42 + h2 * .5, sin(h1 * 6.283) * .8));
                    float3 travel = normalize(float3(cos(h3 * 6.283), -.4, sin(h3 * 6.283)));
                    float head = along * .32;
                    float nearest = clamp(dot(ray - origin, travel), head - .1, head);
                    float gap = length(ray - normalize(origin + travel * nearest));
                    stars += smoothstep(.0045, 0, gap) * saturate((nearest - head + .1) * 10) * step(along, 1) * step(.35, h2) *
                        float3(1, .96, .85) * 1.6;
                }
                float3 moonDirection = _ForestMoonDirection.xyz;
                float moonward = saturate(dot(ray, moonDirection));
                float moon = smoothstep(.99938, .99956, moonward);
                float3 east = cross(float3(0, 1, 0), moonDirection);
                float2 face = float2(dot(ray, east), dot(ray, cross(moonDirection, east))) * 39;
                float seas = smoothstep(.4, .62, Noise(face * 2.3 + float2(11.7, 4.1)) * .7 + Noise(face * 5.1 + float2(3.2, 8.6)) * .3);
                float3 surface = float3(.80, .84, .93) * lerp(1, .55, seas) * lerp(.62, 1, sqrt(saturate(1 - dot(face, face))));
                float3 moonlight = (pow(moonward, 700) * .05 + pow(moonward, 60) * .012) * float3(.62, .74, 1) * (1 - rain);
                sky += (band * dust * float3(.0035, .0042, .0065) + stars * (1 - moon) * smoothstep(-.02, .12, ray.y) + moonlight) * _ForestNight * (1 - rain * .96);
                sky = lerp(sky, surface, moon * _ForestNight);
                float sunward = saturate(dot(ray, _ForestSunDisk.xyz));
                float sun = smoothstep(.99955, .99985, sunward);
                float corona = pow(sunward, 900) * .9 + pow(sunward, 120) * .32 + pow(sunward, 14) * .14;
                float lowSun = saturate(1 - _ForestSunDisk.y * 2.2);
                float3 sunColor = lerp(float3(1, .95, .82), float3(1, .62, .30), lowSun);
                float3 sunlight = sunColor * (1 - _ForestNight) * (1 - rain);
                // Al amanecer y al atardecer el cielo se enciende alrededor del sol y a lo largo del horizonte.
                float dusk = lowSun * saturate(_ForestSunDisk.y * 6 + .9) * (1 - rain);
                sky = lerp(sky, sky * float3(1.25, .86, .66) + float3(.05, .018, 0), dusk * (1 - _ForestNight) * (1 - saturate(ray.y * 1.6)) * .7);
                sky += pow(sunward, 5) * float3(.42, .17, .04) * dusk * (1 - _ForestNight * .6);
                sky += (sun * 1.4 + corona) * sunlight;
                float2 drift = float2(.017, .007) * _ValleyWeather.w;
                float2 cloudUV = ray.xz * (1.72 / max(.12, ray.y + .15)) + drift;
                float density = Noise(cloudUV) * .62 + Noise(cloudUV * 2.17 + 3) * .26 + Noise(cloudUV * 4.4) * .12;
                float cloud = smoothstep(.53 - rain * .26, .72 - rain * .35, density) * smoothstep(.025, .25, ray.y);
                float3 cloudColor = lerp(float3(.63, .68, .65), float3(.16, .23, .27), rain);
                float3 nightCloud = float3(.0012, .0017, .0032) * lerp(1, .6, rain) + pow(moonward, 20) * float3(.02, .025, .036) * (1 - rain);
                cloudColor = lerp(cloudColor, nightCloud, _ForestNight);
                sky = lerp(sky * lerp(1, .68, rain), cloudColor, cloud * lerp(.77, .94, rain));
                sky += corona * cloud * .22 * sunlight;
                // Con lluvia el cielo se funde con la bruma para que el horizonte no quede recortado.
                sky = lerp(sky, _HabitatFog.rgb, rain * .62);
                sky += _ValleyWeather.z * float3(.29, .32, .36) * (.65 + cloud * .35);
                float horizon = smoothstep(-.075, .18, ray.y);
                float3 result = lerp(_HabitatFog.rgb, sky, horizon);
                if (_StormBolt.w > .001)
                {
                    float2 flat = normalize(ray.xz + 1e-5);
                    float front = dot(flat, _StormBolt.xy);
                    float side = flat.x * _StormBolt.y - flat.y * _StormBolt.x;
                    float taper = smoothstep(.62, .1, ray.y);
                    float path = (Noise(float2(ray.y * 21, _StormBolt.z)) - .5) * .11 + (Noise(float2(ray.y * 75, _StormBolt.z + 9)) - .5) * .028;
                    float gap = abs(side - path * taper);
                    float fork = abs(side - path * taper - (ray.y - .34) * .55 - (Noise(float2(ray.y * 40, _StormBolt.z + 4)) - .5) * .04);
                    float strike = smoothstep(.0045, .0008, gap) * 2.2 + exp(-gap * 55) * .55
                        + (smoothstep(.003, .0006, fork) * .9 + exp(-fork * 80) * .2) * step(ray.y, .34) * step(.12, ray.y);
                    result += strike * step(0, front) * smoothstep(-.02, .03, ray.y) * smoothstep(.66, .5, ray.y)
                        * _StormBolt.w * float3(.78, .84, 1);
                }
                return float4(result, 1);
            }
            ENDCG
        }
    }
}
