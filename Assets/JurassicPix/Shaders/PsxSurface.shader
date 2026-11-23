Shader "Jurassic Pix/PSX Surface"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Affine ("Affine mapping", Range(0,1)) = 0.55
        _Jitter ("Vertex snapping grid (px)", Range(0,1)) = 0.5
        _Grain ("World grain", Range(0,0.3)) = 0
        _Unlit ("Emission", Range(0,1)) = 0
        _VertexColor ("Use vertex colors", Range(0,1)) = 1
        _StippleWeight ("Halftone weight", Range(0,1)) = 1
        _FogScale ("Distance fog scale", Range(0,1)) = 1
        _TreeWind ("Tree wind", Range(0,1)) = 0
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle(FOREST_GROUND)] _Ground ("Forest ground", Float) = 0
        _IslandScale ("Island scale", Float) = 1
        _ForestFloor ("Forest floor", 2D) = "white" {}
        _MeadowTex ("Meadow grass", 2D) = "white" {}
        _GravelTex ("Gravel", 2D) = "white" {}
        [Toggle(MOUNTAIN_TERRAIN)] _Mountain ("Mountain terrain", Float) = 0
        _CliffTex ("Cliff texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Cull [_Cull]
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ FOREST_GROUND MOUNTAIN_TERRAIN
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "ValleyWind.cginc"

            sampler2D _MainTex, _ForestFloor, _MeadowTex, _GravelTex, _CliffTex;
            float4 _MainTex_ST, _Color, _HabitatFog, _HabitatLight, _ForestSunDirection;
            float2 _HabitatFogRange;
            float _Affine, _Jitter, _Grain, _Unlit, _VertexColor, _Cutoff, _ForestNight, _StippleWeight, _FogScale;
            float _IslandScale, _SurfaceWetness;
            float4 _StormBolt;
            float4 _IslandLakes[8];
            float _IslandLakeLevels[8];

            struct Input
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 affine : TEXCOORD1;
                float3 world : TEXCOORD2;
                float2 fog : TEXCOORD3;
                float diffuse : TEXCOORD4;
                SHADOW_COORDS(5)
                float3 normal : TEXCOORD6;
                float3 color : COLOR;
            };

            Varyings vert(Input v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                v.vertex = ValleyWind(v.vertex);
                o.pos = UnityObjectToClipPos(v.vertex);
                float w = o.pos.w;
                if (w > 0.001 && _Jitter > 0)
                {
                    float2 halfSize = _ScreenParams.xy * 0.5;
                    float2 grid = halfSize / _Jitter;
                    float2 cell = o.pos.xy / w * grid;
                    o.pos.xy = floor(cell + 0.5) * w / grid;
                }
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float weight = max(0.001, w);
                o.affine = float3(o.uv * weight, weight);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 normal = UnityObjectToWorldNormal(v.normal);
                o.fog = float2(length(o.world - _WorldSpaceCameraPos) * _FogScale, normal.y);
                o.normal = normal;
                float3 direction = _ForestSunDirection.w > 0 ? _ForestSunDirection.xyz : normalize(float3(-0.38, 0.82, -0.42));
                float facing = dot(normal, direction);
                o.diffuse = _Cutoff > 0 ? abs(facing) : saturate(facing);
                o.color = lerp(1, v.color.rgb, _VertexColor);
                TRANSFER_SHADOW(o)
                return o;
            }

            float3 Coat(float3 color, Varyings i, float shadow)
            {
                float3 normal = normalize(i.normal);
                float3 view = normalize(_WorldSpaceCameraPos - i.world);
                if (_Cutoff > 0 && dot(normal, view) < 0)
                    normal = -normal;

                float2 p = i.world.xz;
                float wet = saturate(_SurfaceWetness);
                float patch = sin(dot(p, float2(.42, .31))) + sin(dot(p, float2(-.23, .53))) * .6;
                float pool = 0;
                #if defined(FOREST_GROUND) || defined(MOUNTAIN_TERRAIN)
                    pool = smoothstep(.18, .9, patch) * smoothstep(.65, .95, normal.y);
                    normal = normalize(normal + wet * float3(
                        sin(p.x * 2.4 + p.y * .63), 0, cos(p.y * 1.8 - p.x * .47)) * lerp(.09, .026, pool));
                #endif
                float sheen = lerp(.25, .84, wet);
                #if defined(FOREST_GROUND) || defined(MOUNTAIN_TERRAIN)
                    sheen = lerp(.16, lerp(.52, 1.18, pool), wet);
                #endif
                if (_Cutoff > 0)
                    sheen *= .48;

                float angle = 1 - saturate(dot(normal, view));
                float fresnel = .045 + .68 * pow(angle, 4);
                float3 reflected = reflect(-view, normal);
                float skyline = smoothstep(-.1, .48, reflected.y);
                float cloudBand = .7 + .3 * sin(dot(reflected, float3(7, 4, -5)) + _ValleyWeather.w * .016);
                float3 daySky = lerp(float3(.12, .18, .21), float3(.40, .55, .70), skyline);
                float3 nightSky = lerp(float3(.012, .018, .04), float3(.044, .073, .13), skyline);
                float3 sky = lerp(daySky, nightSky, _ForestNight) * cloudBand;
                float3 film = .82 + .28 * cos((patch * .85 + angle * 2.4) * 3.14159 + float3(.2, 2.1, 4.3));
                sky *= lerp(1, film, wet * lerp(.34, .72, pool));

                float3 light = _ForestSunDirection.w > 0 ? _ForestSunDirection.xyz : normalize(float3(-.38, .82, -.42));
                float3 halfway = normalize(view + light + float3(0, .0001, 0));
                float glint = pow(saturate(dot(normal, halfway)), lerp(18, lerp(48, 100, pool), wet))
                    * saturate(dot(normal, light)) * shadow;
                float3 key = lerp(float3(1, .90, .73), float3(.26, .39, .66), _ForestNight);
                float3 bolt = normalize(float3(_StormBolt.x, .6, _StormBolt.y));
                float flash = _ValleyWeather.z * (.27 + .73 * pow(saturate(dot(reflected, bolt)), 8));
                float3 highlight = sky * fresnel + key * glint * lerp(.72, .32, _ValleyWeather.y)
                    + float3(.52, .74, 1) * flash * (.32 + fresnel);
                return color * (1 - wet * (.12 + pool * .14)) + highlight * sheen;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 uv = lerp(i.uv, i.affine.xy / i.affine.z, _Affine);
                float4 albedo = tex2D(_MainTex, uv);
                clip(albedo.a - _Cutoff);
                #if defined(FOREST_GROUND)
                    float2 layout = i.world.xz / _IslandScale;
                    float2 floorUV = i.world.xz * .22;
                    float3 forest = lerp(albedo.rgb, tex2D(_ForestFloor, floorUV).rgb, .68);
                    float glade = sin(layout.x * .031 + sin(layout.y * .023) * 1.7) * sin(layout.y * .027 + sin(layout.x * .019) * 1.3);
                    forest = lerp(forest, tex2D(_MeadowTex, floorUV * .7).rgb, smoothstep(.02, .38, glade) * .78);
                    albedo.rgb = forest;
                    float sandLevel = 0;
                    for (int k = 0; k < 8; k++)
                    {
                        if (_IslandLakes[k].z > 0 && length((i.world.xz - _IslandLakes[k].xy) / (_IslandLakes[k].zw * 1.04)) < 1.19)
                            sandLevel = _IslandLakeLevels[k];
                    }
                    float beach = 1 - smoothstep(2, 7, i.world.y - sandLevel);
                    float3 sand = float3(.49,.38,.23) * (.76 + tex2D(_GravelTex, floorUV * 1.8).r * .44);
                    albedo.rgb = lerp(albedo.rgb, sand, beach * .94);
                #elif defined(MOUNTAIN_TERRAIN)
                    float3 facing = abs(normalize(i.normal));
                    float3 weights = pow(facing, 4);
                    weights /= dot(weights, float3(1, 1, 1));
                    float3 surface = i.world * .095;
                    float3 rock = weights.x * tex2D(_CliffTex, surface.zy).rgb
                        + weights.y * tex2D(_CliffTex, surface.xz).rgb
                        + weights.z * tex2D(_CliffTex, surface.xy).rgb;
                    albedo.rgb = lerp(albedo.rgb, rock, .76 * (1 - smoothstep(.34, .79, facing.y)));
                #endif
                float shadow = SHADOW_ATTENUATION(i);
                float ambient = lerp(.44, .26, _ForestNight) + _ValleyWeather.z * .41;
                float diffuse = lerp(.78, .31, _ValleyWeather.y) * i.diffuse * shadow;
                float3 illumination = _HabitatFogRange.y > 0 ? _HabitatLight.rgb : float3(1.02, 1.03, .8);
                float3 color = albedo.rgb * _Color.rgb * i.color * lerp((ambient + diffuse) * illumination, 1, _Unlit);
                float3 cell = floor(i.world * 6);
                float grain = frac(dot(cell, float3(0.618, 0.387, 0.513)));
                color *= 1 + (grain - 0.5) * _Grain;
                if (_Unlit < .999)
                    color = lerp(Coat(color, i, shadow), color, _Unlit);
                if (_ValleyWeather.y > .01)
                {
                    // Las gotas que rebotan aportan destellos breves a la película mojada.
                    float up = saturate(i.fog.y * 2 - .5);
                    float2 drop = floor(i.world.xz * 5.5) + floor(_Time.y * 11) * float2(7.13, 3.71);
                    float spark = frac(sin(dot(drop, float2(12.9898, 78.233))) * 43758.5453);
                    color += step(.986, spark) * _ValleyWeather.y * up * (1 - smoothstep(16, 75, i.fog.x))
                        * float3(.15, .18, .21) * (1 - _ForestNight * .88) * (1 - _Unlit);
                }
                float2 range = _HabitatFogRange.y > 0 ? _HabitatFogRange : float2(13, 58);
                float3 fogColor = _HabitatFogRange.y > 0 ? _HabitatFog.rgb : float3(0.23, 0.32, 0.22);
                float fog = smoothstep(range.x, range.y, i.fog.x);
                // The post-process reads the visible surface's halftone weight from alpha.
                return float4(lerp(color, fogColor, fog), _StippleWeight);
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            Cull [_Cull]
            ZWrite On
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "ValleyWind.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Cutoff;

            struct ShadowVertex
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
            };

            ShadowVertex vertShadow(appdata_base v)
            {
                ShadowVertex o;
                UNITY_SETUP_INSTANCE_ID(v);
                v.vertex = ValleyWind(v.vertex);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            float4 fragShadow(ShadowVertex i) : SV_Target
            {
                if (_Cutoff > 0)
                    clip(tex2D(_MainTex, i.uv).a - _Cutoff);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
}
