#ifndef ANIME_SURFERS_WATER_WAVES_INCLUDED
#define ANIME_SURFERS_WATER_WAVES_INCLUDED

// Gerstner waves (see catlikecoding.com/unity/tutorials/flow/waves).
// Globals are pushed every frame by Surf.WaterSurface and must stay in sync with WaterSurface.EvaluateWaves.
#define WATER_MAX_WAVES 8
// A wave's geometry fades out between these many mesh cells per wavelength.
#define WATER_MIN_WAVE_CELLS 3.0
#define WATER_FULL_WAVE_CELLS 6.0
// A wave's shading fades out between these many pixels per wavelength, before it aliases into noise.
#define WATER_MIN_WAVE_PIXELS 2.0
#define WATER_FULL_WAVE_PIXELS 4.0

float4 _WaterGerstnerWaves[WATER_MAX_WAVES];  // xy = direction, z = wave number (2pi / wavelength), w = angular frequency
float4 _WaterGerstnerShapes[WATER_MAX_WAVES]; // x = steepness, y = amplitude, z = wavelength, w = phase at _WaterOrigin now
float4 _WaterOrigin;     // xy = xz the phases are measured from, near the board
float _WaterWaveTime;
float _WaterAmplitudeSum;
float _WaterBaseHeight;
float4 _WaterMeshBounds; // xy = fade center xz, z = half size where the waves are flat, w = width of the flattened border
float4 _WaterLodParams;  // x = finest cell size, y = 2 / finest level's half size, z = snap margin

// Chebyshev distance from the fade center; the mesh levels are squares.
float WaterCenterDistance(float2 xz)
{
    float2 local = abs(xz - _WaterMeshBounds.xy);
    return max(local.x, local.y);
}

// 1 inside the waves, falling to 0 before the mesh's outer edge and beyond, where the flat far water starts.
float WaterEdgeFade(float2 xz)
{
    return saturate((_WaterMeshBounds.z - WaterCenterDistance(xz)) / max(_WaterMeshBounds.w, 1e-4));
}

// One over an upper bound of the mesh's cell size at xz: each level doubles the cells of the one inside it.
float WaterLodInverseCell(float2 xz)
{
    float distance = WaterCenterDistance(xz) + _WaterLodParams.z;
    return 1.0 / (_WaterLodParams.x * max(1.0, distance * _WaterLodParams.y));
}

void AddWaterWave(int i, float2 xz, float scale, inout float3 offset, inout float3 tangent, inout float3 binormal)
{
    float4 wave = _WaterGerstnerWaves[i];
    float4 shape = _WaterGerstnerShapes[i];
    float2 d = wave.xy;
    float steepness = shape.x * scale;
    float a = shape.y * scale;
    float s, c;
    // Measured from the origin with the clock folded into the phase (wrapped on the CPU): world positions and the
    // clock grow without bound over a run, and mobile GPUs lose sin/cos precision on large arguments.
    sincos(wave.z * dot(d, xz - _WaterOrigin.xy) + shape.w, s, c);

    offset += float3(d.x * a * c, a * s, d.y * a * c);
    tangent += float3(-d.x * d.x * steepness * s, d.x * steepness * c, -d.x * d.y * steepness * s);
    binormal += float3(-d.x * d.y * steepness * s, d.y * steepness * c, -d.y * d.y * steepness * s);
}

// Adds waves [first, end) at full strength. Waves are packed longest first.
void AddWaterWaves(float2 xz, int first, int end, inout float3 offset, inout float3 tangent, inout float3 binormal)
{
    [unroll]
    for (int i = first; i < end; i++)
    {
        AddWaterWave(i, xz, 1.0, offset, tangent, binormal);
    }
}

// Adds waves [first, end) for shading a pixel whose footprint spans gridDx / gridDy (its xz derivatives): waves too
// short to resolve along their direction fade out instead of aliasing. Returns the slope variance they leave out, to
// widen the highlights in their place.
// offset: displacement of the grid point at xz.
// tangent / binormal: unnormalized surface derivatives along x / z; normal = normalize(cross(binormal, tangent)).
float AddWaterWavesFiltered(float2 xz, float2 gridDx, float2 gridDy, int first, int end, inout float3 offset,
    inout float3 tangent, inout float3 binormal)
{
    float variance = 0.0;

    [unroll]
    for (int i = first; i < end; i++)
    {
        float2 d = _WaterGerstnerWaves[i].xy;
        float4 shape = _WaterGerstnerShapes[i];
        float footprint = max(abs(dot(d, gridDx)), abs(dot(d, gridDy)));
        float pixels = shape.z / max(footprint, 1e-5);
        float fade = saturate((pixels - WATER_MIN_WAVE_PIXELS) / (WATER_FULL_WAVE_PIXELS - WATER_MIN_WAVE_PIXELS));
        AddWaterWave(i, xz, fade, offset, tangent, binormal);

        // A sine's slope amplitude is its steepness, so its mean squared slope is half that squared.
        float removed = shape.x * (1.0 - fade);
        variance += 0.5 * removed * removed;
    }

    return variance;
}

// Displacement of the mesh vertex at xz: waves too short for the cells there are flattened, so the geometry never
// aliases. Before the edge fade.
float3 EvaluateWaterWavesLod(float2 xz)
{
    float3 offset = 0.0;
    float3 tangent = 0.0;
    float3 binormal = 0.0;
    float inverseCell = WaterLodInverseCell(xz);

    [unroll]
    for (int i = 0; i < WATER_MAX_WAVES; i++)
    {
        float cells = _WaterGerstnerShapes[i].z * inverseCell;
        float fade = saturate((cells - WATER_MIN_WAVE_CELLS) / (WATER_FULL_WAVE_CELLS - WATER_MIN_WAVE_CELLS));
        AddWaterWave(i, xz, fade, offset, tangent, binormal);
    }

    return offset;
}

#endif
