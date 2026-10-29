#ifndef ANIME_SURFERS_WATER_WAVES_INCLUDED
#define ANIME_SURFERS_WATER_WAVES_INCLUDED

// Gerstner waves (see catlikecoding.com/unity/tutorials/flow/waves).
// Globals are pushed every frame by Surf.WaterSurface and must stay in sync with WaterSurface.EvaluateWaves.
#define WATER_MAX_WAVES 8

float4 _WaterGerstnerWaves[WATER_MAX_WAVES]; // xy = wave vector (direction * 2pi / wavelength), z = steepness, w = angular frequency
float _WaterWaveTime;
float _WaterAmplitudeSum;
float _WaterBaseHeight;

// offset: displacement of the grid point at xz.
// tangent / binormal: unnormalized surface derivatives along x / z; normal = normalize(cross(binormal, tangent)).
void EvaluateWaterWaves(float2 xz, out float3 offset, out float3 tangent, out float3 binormal)
{
    offset = 0.0;
    tangent = float3(1.0, 0.0, 0.0);
    binormal = float3(0.0, 0.0, 1.0);

    [unroll]
    for (int i = 0; i < WATER_MAX_WAVES; i++)
    {
        float4 wave = _WaterGerstnerWaves[i];
        float k = max(length(wave.xy), 1e-4);
        float2 d = wave.xy / k;
        float steepness = wave.z;
        float a = steepness / k;
        float f = dot(wave.xy, xz) - wave.w * _WaterWaveTime;
        float s = sin(f);
        float c = cos(f);

        offset += float3(d.x * a * c, a * s, d.y * a * c);
        tangent += float3(-d.x * d.x * steepness * s, d.x * steepness * c, -d.x * d.y * steepness * s);
        binormal += float3(-d.x * d.y * steepness * s, d.y * steepness * c, -d.y * d.y * steepness * s);
    }
}

#endif
