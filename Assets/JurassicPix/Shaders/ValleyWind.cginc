#ifndef JURASSIC_VALLEY_WIND_INCLUDED
#define JURASSIC_VALLEY_WIND_INCLUDED
float4 _ValleyWeather;
float _TreeWind;

float4 ValleyWind(float4 vertex)
{
    if (_TreeWind < .001 || _ValleyWeather.x < .001)
        return vertex;
    float3 world = mul(unity_ObjectToWorld, vertex).xyz;
    float3 pivot = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
    float height = max(0, world.y - pivot.y);
    float root = smoothstep(0, 7, height);
    float wave = sin(_ValleyWeather.w * 2.48 + dot(pivot.xz, float2(.044, .032)));
    float sway = root * height * _TreeWind * _ValleyWeather.x * (.066 + wave * .041);
    world.xz += float2(.94, .37) * sway;
    return mul(unity_WorldToObject, float4(world, 1));
}
#endif
