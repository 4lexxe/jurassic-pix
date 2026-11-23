Shader "Hidden/Jurassic Pix/PSX Color"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Levels ("Color levels per channel", Range(4, 256)) = 32
        _Dither ("Dither strength", Range(0, 1)) = 1
        _Stipple ("Square stipple strength", Range(0, 1)) = 0.14
        _Gamma ("Brightness curve", Range(0.5, 2)) = 0.8
        _Scanlines ("Scanlines", Range(0, 0.3)) = 0
        _Retro ("Retro treatment strength", Range(0, 1)) = 0.15
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Levels, _Dither, _Stipple, _Gamma, _Scanlines, _Retro;
            float4 _SunScreen, _SunTint, _ValleyWeather;
            float _ForestNight;

            float rainHash(float2 p)
            {
                p = frac(p * float2(.1031, .1099));
                p += dot(p, p.yx + 19.19);
                return frac((p.x + p.y) * p.x);
            }

            // Cortina de lluvia en pantalla: columnas de trazos que caen inclinados por el viento.
            float rainStreaks(float2 uv, float columns, float speed, float seed)
            {
                float aspect = _MainTex_TexelSize.z * abs(_MainTex_TexelSize.y);
                float2 p = float2((uv.x * aspect + uv.y * (.08 + _ValleyWeather.x * .34)) * columns,
                    uv.y * columns * .1 + _Time.y * speed);
                float2 cell = floor(p);
                float2 f = frac(p);
                float h = rainHash(cell + seed);
                float core = abs(f.x - (.25 + .5 * frac(h * 9.7)));
                return step(.6, h) * smoothstep(.075, 0, core) * smoothstep(0, .3, f.y) * smoothstep(1, .55, f.y);
            }

            float3 rainCurtain(float2 uv)
            {
                #if UNITY_UV_STARTS_AT_TOP
                    if (_MainTex_TexelSize.y < 0)
                        uv.y = 1 - uv.y;
                #endif
                float streaks = rainStreaks(uv, 44, 2.7, 0) * .5 + rainStreaks(uv + .37, 78, 3.5, 17) * .33
                    + rainStreaks(uv + .71, 128, 4.3, 43) * .21;
                return streaks * _ValleyWeather.y * lerp(float3(.26, .30, .34), float3(.02, .028, .04), _ForestNight);
            }

            float sunMask(float2 uv, float2 sun)
            {
                float3 light = tex2Dlod(_MainTex, float4(uv, 0, 0)).rgb;
                float2 offset = (uv - sun) * float2(_SunScreen.w, 1);
                float inside = step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
                return saturate((dot(light, float3(0.2126, 0.7152, 0.0722)) - 0.8) * 5)
                    * saturate(1 - length(offset) * 2.6) * inside;
            }

            float3 sunEffects(float2 uv)
            {
                float2 sun = _SunScreen.xy;
                #if UNITY_UV_STARTS_AT_TOP
                    if (_MainTex_TexelSize.y < 0)
                        sun.y = 1 - sun.y;
                #endif
                float2 aspect = float2(_SunScreen.w, 1);
                float2 offset = (uv - sun) * aspect;
                float range = length(offset);
                float shafts = 0;
                for (int tapIndex = 1; tapIndex <= 14; tapIndex++)
                    shafts += sunMask(lerp(uv, sun, tapIndex / 14.0), sun);
                shafts *= saturate(1 - range * 0.85) / 14;

                float2 tap = float2(0.012 / _SunScreen.w, 0.012);
                float visible = (sunMask(sun, sun) + sunMask(sun + tap, sun) + sunMask(sun - tap, sun)
                    + sunMask(sun + tap * float2(1, -1), sun) + sunMask(sun + tap * float2(-1, 1), sun)) * 0.2;

                float angle = atan2(offset.y, offset.x);
                float star = pow(abs(cos(angle * 3 + 0.4)), 48) + 0.5 * pow(abs(cos(angle * 4 - 0.9)), 90);
                float glow = pow(saturate(1 - range * 2.6), 3) * 0.45 + star * pow(saturate(1 - range * 2.9), 2) * 0.5;
                float streak = exp(-abs(offset.y) * 70) * saturate(1 - abs(offset.x) * 1.5) * 0.35;
                float ring = (range - 0.19) / 0.014;
                float halo = exp(-ring * ring) * 0.16;
                float3 flare = (glow + streak) * _SunTint.rgb + halo * float3(1.0, 0.75, 0.5);

                float2 axis = float2(0.5, 0.5) - sun;
                flare += smoothstep(0.035, 0.018, length((uv - sun - axis * 0.55) * aspect)) * float3(0.30, 0.20, 0.08);
                flare += smoothstep(0.075, 0.055, length((uv - sun - axis * 0.95) * aspect)) * float3(0.07, 0.13, 0.09);
                flare += smoothstep(0.022, 0.010, length((uv - sun - axis * 1.4) * aspect)) * float3(0.12, 0.16, 0.30);
                float ghost = (length((uv - sun - axis * 1.85) * aspect) - 0.10) / 0.012;
                flare += exp(-ghost * ghost) * float3(0.16, 0.10, 0.05);
                return (shafts * 0.85 * _SunTint.rgb + flare * visible) * _SunScreen.z;
            }

            // Recursive 2x2 pattern gives the standard 4x4 Bayer matrix.
            float bayer2(float2 p)
            {
                return 2 * p.x + 3 * p.y - 4 * p.x * p.y;
            }

            float3 applyStipple(float3 rgb, float rank, float amount)
            {
                float light = dot(rgb, float3(0.2126, 0.7152, 0.0722));
                if (amount <= 0 || light <= 0)
                    return rgb;

                // Quantize luminance perceptually so a single game pixel reads as a colored dot.
                float tone = light;
                #ifndef UNITY_COLORSPACE_GAMMA
                    tone = LinearToGammaSpaceExact(light);
                #endif
                const float bands = 10;
                float band = tone * bands;
                float ordered = (floor(band) + step((rank + 0.5) / 16, frac(band))) / bands;
                float strength = amount * (1 - smoothstep(0.52, 0.9, tone));
                float stippledTone = lerp(tone, ordered, strength);
                #ifndef UNITY_COLORSPACE_GAMMA
                    stippledTone = GammaToLinearSpaceExact(stippledTone);
                #endif
                return rgb * (stippledTone / light);
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float2 pixel = floor(i.uv * _MainTex_TexelSize.zw);
                float rank = 4 * bayer2(fmod(pixel, 2)) + bayer2(fmod(floor(pixel / 2), 2));
                float threshold = rank / 16 - 0.5;
                float2 center = (pixel + 0.5) * abs(_MainTex_TexelSize.xy);
                float4 sceneColor = tex2D(_MainTex, center);
                float3 extra = 0;
                UNITY_BRANCH
                if (_SunScreen.z > .001)
                    extra = sunEffects(center);
                UNITY_BRANCH
                if (_ValleyWeather.y > .01)
                    extra += rainCurtain(center);
                float3 clean = pow(saturate(sceneColor.rgb + extra), _Gamma);
                float3 rgb = applyStipple(clean, rank, _Stipple * sceneColor.a);
                rgb *= 1 - fmod(pixel.y, 2) * _Scanlines;
                float levels = _Levels - 1;
                rgb = floor(rgb * levels + 0.5 + threshold * _Dither) / levels;
                return float4(saturate(lerp(clean, rgb, _Retro)), 1);
            }
            ENDCG
        }
    }
}
