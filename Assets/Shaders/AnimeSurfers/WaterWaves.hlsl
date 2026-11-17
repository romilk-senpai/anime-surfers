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

// Adds waves [first, end) at the given strength (see BigWaveCalm). Waves are packed longest first.
void AddWaterWaves(float2 xz, int first, int end, float calm, inout float3 offset, inout float3 tangent,
    inout float3 binormal)
{
    [unroll]
    for (int i = first; i < end; i++)
    {
        AddWaterWave(i, xz, calm, offset, tangent, binormal);
    }
}

// Adds waves [first, end) for shading a pixel whose footprint spans gridDx / gridDy (its xz derivatives): waves too
// short to resolve along their direction fade out instead of aliasing. Returns the slope variance they leave out, to
// widen the highlights in their place.
// offset: displacement of the grid point at xz.
// tangent / binormal: unnormalized surface derivatives along x / z; normal = normalize(cross(binormal, tangent)).
float AddWaterWavesFiltered(float2 xz, float2 gridDx, float2 gridDy, int first, int end, float calm,
    inout float3 offset, inout float3 tangent, inout float3 binormal)
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
        AddWaterWave(i, xz, fade * calm, offset, tangent, binormal);

        // A sine's slope amplitude is its steepness, so its mean squared slope is half that squared.
        float removed = shape.x * (1.0 - fade);
        variance += 0.5 * removed * removed;
    }

    return variance;
}

// The big wave (Surf.BigWave): a ridge along the barrel's straight, pushed every frame by Surf.WaterSurface and
// mirrored in WaterSurface.EvaluateBigWave. Across runs from the ridge line toward the wave side; the face rises from
// base to crest, the back falls away behind it, and both ends taper off along the ridge.
float4 _BigWaveLine;    // xy = origin xz, zw = direction along the ridge (unit xz)
float4 _BigWaveShape;   // x = height now (0 = no wave), y = side (+1 right of the direction, -1 left), zw = along start, end
float4 _BigWaveProfile; // x = face base across, y = crest across, z = back width, w = end taper length
float4 _BigWaveLip;     // x = lip along (the rider's, from the barrel's start on), y = throw 0..1, z = lip reach (crest to
                        // where it lands), w = mouth length (the lip pitches out over this far from along 0)
float4 _BigWaveLipSpan; // x = lip start along, y = fully pitched lip's end along (both absolute), z = face hollow 0..1,
                        // w = 0

// Mirrors BigWaveShape.FaceProfile: the face's height 0..1 at t (base 0, crest 1), slope in dProfile. t² over the lower
// half, then a cubic up to the crest that comes in steeper with the hollow.
float BigWaveFace(float t, out float dProfile)
{
    const float hollowStart = 0.5;
    if (t <= hollowStart)
    {
        dProfile = 2.0 * t;
        return t * t;
    }

    const float h = 1.0 - hollowStart;
    float s = (t - hollowStart) / h;
    float y0 = hollowStart * hollowStart;
    float m0 = 2.0 * hollowStart * h;
    float m1 = lerp(2.0, 4.3, _BigWaveLipSpan.z) * h;
    float s2 = s * s;
    float s3 = s2 * s;
    dProfile = ((6.0 * s2 - 6.0 * s) * y0 + (3.0 * s2 - 4.0 * s + 1.0) * m0 + (6.0 * s - 6.0 * s2) +
        (3.0 * s2 - 2.0 * s) * m1) / h;
    return (2.0 * s3 - 3.0 * s2 + 1.0) * y0 + (s3 - 2.0 * s2 + s) * m0 + (3.0 * s2 - 2.0 * s3) + (s3 - s2) * m1;
}

float2 BigWaveSide()
{
    // Right of the direction, as Vector3.Cross(up, forward) on the CPU.
    return float2(_BigWaveLine.w, -_BigWaveLine.z) * _BigWaveShape.y;
}

// 1 along the middle of the ridge, easing to 0 at its ends; derivative along in dEnds.
float BigWaveEnds(float along, out float dEnds)
{
    float taper = max(_BigWaveProfile.w, 1e-3);
    float rise = saturate((along - _BigWaveShape.z) / taper);
    float fall = saturate((_BigWaveShape.w - along) / taper);
    float a = rise * rise * (3.0 - 2.0 * rise);
    float b = fall * fall * (3.0 - 2.0 * fall);
    dEnds = 6.0 * rise * (1.0 - rise) / taper * b - a * 6.0 * fall * (1.0 - fall) / taper;
    return a * b;
}

// Height of the big wave at xz above the still water, with its xz gradient and where xz lies across and along it.
float BigWaveHeight(float2 xz, out float2 gradient, out float across, out float along)
{
    float2 direction = _BigWaveLine.zw;
    float2 side = BigWaveSide();
    float2 relative = xz - _BigWaveLine.xy;
    along = dot(relative, direction);
    across = dot(relative, side);
    gradient = 0.0;

    float base = _BigWaveProfile.x;
    float crest = _BigWaveProfile.y;
    float back = max(_BigWaveProfile.z, 1e-3);
    float profile;
    float dProfile;

    if (across < crest)
    {
        // Concave face, steepest at the crest where the lip throws out.
        float width = max(crest - base, 1e-3);
        float t = saturate((across - base) / width);
        float dFace;
        profile = BigWaveFace(t, dFace);
        dProfile = across > base ? dFace / width : 0.0;
    }
    else
    {
        float t = saturate((across - crest) / back);
        profile = 1.0 - t * t * (3.0 - 2.0 * t);
        dProfile = -6.0 * t * (1.0 - t) / back;
    }

    float dEnds;
    float ends = BigWaveEnds(along, dEnds);
    float height = _BigWaveShape.x;
    gradient = height * (dProfile * ends * side + profile * dEnds * direction);
    return height * profile * ends;
}

// How much of the small waves rides on the big wave at xz, 0..1: they die down up its face and over its crest, which
// stays a smooth line (on the coarse far mesh, waves on the crest draw a jagged ridge that jumps as the mesh moves).
// Mirrors WaterSurface.BigWaveCalm.
float BigWaveCalm(float2 xz)
{
    float calm = 1.0;

    UNITY_BRANCH
    if (_BigWaveShape.x > 0.0)
    {
        float2 gradient;
        float across;
        float along;
        calm = 1.0 - 0.85 * smoothstep(2.5, 5.5, BigWaveHeight(xz, gradient, across, along));
    }

    return calm;
}

// Adds the big wave to a surface point's displacement and derivatives (see AddWaterWave). Returns how far up the face
// it is, 0..1.
float AddBigWave(float2 xz, inout float3 offset, inout float3 tangent, inout float3 binormal)
{
    float fraction = 0.0;

    UNITY_BRANCH
    if (_BigWaveShape.x > 0.0)
    {
        float2 gradient = 0.0;
        float across = 0.0;
        float along = 0.0;
        float height = BigWaveHeight(xz, gradient, across, along);
        offset.y += height;
        tangent.y += gradient.x;
        binormal.y += gradient.y;
        fraction = height / _BigWaveShape.x;
    }

    return fraction;
}

// Displacement of the mesh vertex at xz: waves too short for the cells there are flattened, so the geometry never
// aliases. The big wave is long enough for every level. Before the edge fade.
// The Gerstner waves alone, as the mesh at xz draws them (see EvaluateWaterWavesLod).
float3 EvaluateSmallWavesLod(float2 xz, inout float3 tangent, inout float3 binormal)
{
    float3 offset = 0.0;
    float inverseCell = WaterLodInverseCell(xz);
    float calm = BigWaveCalm(xz);

    [unroll]
    for (int i = 0; i < WATER_MAX_WAVES; i++)
    {
        float cells = _WaterGerstnerShapes[i].z * inverseCell;
        float fade = saturate((cells - WATER_MIN_WAVE_CELLS) / (WATER_FULL_WAVE_CELLS - WATER_MIN_WAVE_CELLS));
        AddWaterWave(i, xz, fade * calm, offset, tangent, binormal);
    }

    return offset;
}

float3 EvaluateWaterWavesLod(float2 xz)
{
    float3 tangent = 0.0;
    float3 binormal = 0.0;
    float3 offset = EvaluateSmallWavesLod(xz, tangent, binormal);
    AddBigWave(xz, offset, tangent, binormal);
    return offset;
}

// Height of the big wave at xz above the still water, 0 without one; for the haze.
float BigWaveHazeHeight(float2 xz)
{
    float height = 0.0;

    UNITY_BRANCH
    if (_BigWaveShape.x > 0.0)
    {
        float2 gradient;
        float across;
        float along;
        height = BigWaveHeight(xz, gradient, across, along);
    }

    return height;
}

// How much of the haze (fog distance) is left this high above the still water: it lies low over the sea.
float BigWaveHazeScale(float height)
{
    return lerp(1.0, 0.35, saturate(height / 6.0));
}

// Whitewater on the water around the big wave, 0..1: where the water off the lip's tip rains down in front of the face,
// and over the face behind the lip inside the barrel, where the wave has already broken.
float BigWaveFoamAt(float2 xz)
{
    float2 gradient = 0.0;
    float across = 0.0;
    float along = 0.0;
    float height = BigWaveHeight(xz, gradient, across, along);
    float throwAmount = _BigWaveLip.y;
    float mouth = max(_BigWaveLip.w, 1e-3);
    // Under the fully pitched lip: past most of the mouth, up to just ahead of the rider.
    float inLip = saturate((along - _BigWaveLipSpan.x) / 6.0) * saturate((_BigWaveLipSpan.y - along) / 12.0) *
        saturate((along - mouth * 0.7) / (mouth * 0.3));
    float landing = _BigWaveProfile.y - _BigWaveLip.z;
    // Only once the lip is out.
    float plunge = (1.0 - smoothstep(0.0, 2.5, abs(across - landing))) * inLip * smoothstep(0.75, 1.0, throwAmount);

    // Broken water behind the lip, over the face and spilling a little in front of it: behind the rider, and from
    // the peak (the tube's mouth, along 0) back, where the wave broke first and the curl peels away from. Lace, not
    // solid.
    float behind = 0.5 * max(saturate((_BigWaveLipSpan.x - along) / 10.0) * saturate(along / 10.0),
        saturate((8.0 - along) / 14.0) * saturate((along + 70.0) / 30.0));
    float broken = behind * saturate((across - (_BigWaveProfile.x - 4.0)) / 4.0) *
        saturate((_BigWaveProfile.y + 4.0 - across) / 4.0);

    float dEnds;
    float ends = BigWaveEnds(along, dEnds);
    return saturate(max(plunge, broken) * throwAmount * ends);
}

float BigWaveFoam(float2 xz)
{
    float foam = 0.0;

    UNITY_BRANCH
    if (_BigWaveShape.x > 0.0 && _BigWaveLip.y > 0.0)
    {
        foam = BigWaveFoamAt(xz);
    }

    return foam;
}

// How far inside the tube xz is, 0..1: under the thrown lip, from where its tip lands to the crest, along the stretch
// where it's pitched. It fades in over the mouth and out ahead of the rider, so the tube's eye stays bright.
float BigWaveTube(float2 xz)
{
    float tube = 0.0;

    UNITY_BRANCH
    if (_BigWaveShape.x > 0.0 && _BigWaveLip.y > 0.0)
    {
        float2 relative = xz - _BigWaveLine.xy;
        float along = dot(relative, _BigWaveLine.zw);
        float across = dot(relative, BigWaveSide());
        float mouth = max(_BigWaveLip.w, 1e-3);
        float span = saturate((along - _BigWaveLipSpan.x) / 10.0) * saturate(1.0 - (along - _BigWaveLipSpan.y) / 40.0) *
            saturate(along / mouth);
        float crest = _BigWaveProfile.y;
        float landing = crest - _BigWaveLip.z;
        float under = smoothstep(landing - 1.0, landing + 4.0, across) * (1.0 - smoothstep(crest - 1.0, crest + 1.0, across));
        float dEnds;
        tube = span * under * smoothstep(0.5, 1.0, _BigWaveLip.y) * BigWaveEnds(along, dEnds);
    }

    return tube;
}

// The lip: a sheet curling from the crest over the barrel. Its mesh (Surf.BigWaveLip) holds the cross-section in
// (across from the crest, height from the crest, along from the lip's place), already thrown as far as it is at each
// point; the sheet follows the rider along the ridge and shrinks into the crest where the ridge tapers.
float3 BigWaveLipPosition(float3 positionOS, float3 normalOS, out float3 normalWS)
{
    float2 direction = _BigWaveLine.zw;
    float2 side = BigWaveSide();
    float along = _BigWaveLip.x + positionOS.z;
    float dEnds;
    float ends = BigWaveEnds(along, dEnds);
    float scale = ends;
    float crestHeight = _BigWaveShape.x * ends;

    float2 xz = _BigWaveLine.xy + direction * along + side * (_BigWaveProfile.y + positionOS.x * scale);
    float y = crestHeight + positionOS.y * scale;
    normalWS = normalize(float3(side.x, 0.0, side.y) * normalOS.x + float3(0.0, normalOS.y, 0.0) +
        float3(direction.x, 0.0, direction.y) * normalOS.z);
    return float3(xz.x, y, xz.y);
}

#endif
