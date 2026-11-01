using System;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Time of day driven by run distance: blends the sky, fog, sun, ambient light and water tint through presets,
	/// starting at a random point of the cycle, and darkens them into overcast while a <see cref="Weather"/> storm
	/// passes. The sky shader takes its horizon from the fog color, so both always match.
	/// </summary>
	public class SkyCycle : MonoBehaviour
	{
		[Serializable]
		public struct Preset
		{
			public string name;

			[Header("Sky")]
			public Color zenith;
			public Color sky;
			[Tooltip("Also the sky horizon and the water's reflection tint.")]
			public Color fog;
			public Color cloudLit;
			public Color cloudShadow;
			public Color islands;
			[Range(0f, 3f)] public float stars;

			[Header("Sun")]
			public Color sunColor;
			[Min(0f)] public float sunIntensity;
			[Range(0f, 10f)] public float sunDisc;
			[Range(0f, 2f)] public float sunGlow;
			[Tooltip("Elevation and heading in degrees. Headings are blended without wrapping.")]
			public Vector2 sunAngles;

			[Header("Ambient")]
			public Color ambientSky;
			public Color ambientEquator;
			public Color ambientGround;

			[Header("Water")]
			[Tooltip("Multiplies the water material's crest and trough colors.")]
			public Color waterTint;

			[Header("Lights")]
			[Tooltip("Strength of emissive lamps and windows on toon materials with emission enabled.")]
			[Range(0f, 1f)] public float lights;
		}

		private static readonly int ZenithId = Shader.PropertyToID("_ZenithColor");
		private static readonly int SkyId = Shader.PropertyToID("_SkyColor");
		private static readonly int CloudLitId = Shader.PropertyToID("_CloudColor");
		private static readonly int CloudShadowId = Shader.PropertyToID("_CloudShadowColor");
		private static readonly int IslandId = Shader.PropertyToID("_IslandColor");
		private static readonly int StarsId = Shader.PropertyToID("_StarIntensity");
		private static readonly int SunColorId = Shader.PropertyToID("_SunColor");
		private static readonly int SunDiscId = Shader.PropertyToID("_SunIntensity");
		private static readonly int SunGlowId = Shader.PropertyToID("_SunGlow");
		private static readonly int WaterHorizonId = Shader.PropertyToID("_HorizonColor");
		private static readonly int WaterCrestId = Shader.PropertyToID("_CrestColor");
		private static readonly int WaterTroughId = Shader.PropertyToID("_TroughColor");
		private static readonly int NightLightsId = Shader.PropertyToID("_NightLights");
		private static readonly int SkySunDirectionId = Shader.PropertyToID("_SkySunDirection");
		private static readonly int CloudCoverageId = Shader.PropertyToID("_CloudCoverage");

		[SerializeField] private Light sun;
		[Tooltip("Lowest elevation of the sun light. The sky still draws the sun at the preset angle; this only keeps " +
		         "low dawn and sunset light from stretching shadows across the scene.")]
		[SerializeField, Range(0f, 90f)] private float minLightElevation = 25f;
		[Tooltip("Realtime probe that only renders the sky, so reflections follow the time of day.")]
		[SerializeField] private ReflectionProbe skyProbe;
		[SerializeField, Min(0.05f)] private float probeRefreshInterval = 0.4f;

		[Header("Cycle")]
		[Tooltip("Distance each preset lasts, including the blend into the next one.")]
		[SerializeField, Min(1f)] private float presetLength = 1200f;
		[SerializeField, Min(0f)] private float transitionLength = 350f;
		[Tooltip("Shifts the cycle, e.g. presetLength * 2 starts the run at the third preset.")]
		[SerializeField] private float distanceOffset;
		[Tooltip("Adds a random shift to the offset, so each run starts at a random time of day.")]
		[SerializeField] private bool randomStart = true;

		[Header("Storm")]
		[Tooltip("How far colors fade to grey at full storm.")]
		[SerializeField, Range(0f, 1f)] private float stormDesaturation = 0.85f;
		[Tooltip("Multiplies the sky, fog, cloud and island colors at full storm.")]
		[SerializeField] private Color stormSkyTint = new Color(0.55f, 0.6f, 0.66f);
		[Tooltip("Multiplies the ambient light at full storm.")]
		[SerializeField] private Color stormAmbientTint = new Color(0.75f, 0.78f, 0.85f);
		[Tooltip("Multiplies the water's tint at full storm.")]
		[SerializeField] private Color stormWaterTint = new Color(0.6f, 0.66f, 0.72f);
		[SerializeField, Range(0f, 1f)] private float stormSunIntensity = 0.4f;
		[SerializeField, Range(0f, 1f)] private float stormCloudCoverage = 0.85f;
		[Tooltip("Lamps and windows come on at least this much at full storm.")]
		[SerializeField, Range(0f, 1f)] private float stormLights = 0.5f;

		[SerializeField] private Preset[] presets =
		{
			new Preset
			{
				name = "Noon",
				zenith = new Color(0.16f, 0.42f, 0.88f), sky = new Color(0.45f, 0.72f, 0.98f),
				fog = new Color(0.78f, 0.88f, 0.97f),
				cloudLit = Color.white, cloudShadow = new Color(0.68f, 0.76f, 0.9f),
				islands = new Color(0.42f, 0.58f, 0.74f), stars = 0f,
				sunColor = new Color(1f, 0.95f, 0.86f), sunIntensity = 1.4f, sunDisc = 2.5f, sunGlow = 0.35f,
				sunAngles = new Vector2(40f, 150f),
				ambientSky = new Color(0.55f, 0.7f, 0.95f), ambientEquator = new Color(0.7f, 0.82f, 0.92f),
				ambientGround = new Color(0.3f, 0.45f, 0.5f), waterTint = Color.white, lights = 0f
			},
			new Preset
			{
				name = "Sunset",
				zenith = new Color(0.2f, 0.24f, 0.55f), sky = new Color(0.86f, 0.56f, 0.58f),
				fog = new Color(1f, 0.74f, 0.52f),
				cloudLit = new Color(1f, 0.78f, 0.56f), cloudShadow = new Color(0.56f, 0.42f, 0.58f),
				islands = new Color(0.46f, 0.3f, 0.42f), stars = 0f,
				sunColor = new Color(1f, 0.68f, 0.4f), sunIntensity = 1.15f, sunDisc = 3f, sunGlow = 0.8f,
				sunAngles = new Vector2(9f, 160f),
				ambientSky = new Color(0.5f, 0.42f, 0.6f), ambientEquator = new Color(0.9f, 0.62f, 0.52f),
				ambientGround = new Color(0.3f, 0.24f, 0.3f), waterTint = new Color(0.95f, 0.85f, 0.85f),
				lights = 0.5f
			},
			new Preset
			{
				name = "Night",
				zenith = new Color(0.02f, 0.03f, 0.1f), sky = new Color(0.06f, 0.1f, 0.25f),
				fog = new Color(0.12f, 0.18f, 0.32f),
				cloudLit = new Color(0.32f, 0.38f, 0.55f), cloudShadow = new Color(0.1f, 0.13f, 0.22f),
				islands = new Color(0.05f, 0.07f, 0.14f), stars = 1.2f,
				sunColor = new Color(0.8f, 0.88f, 1f), sunIntensity = 0.55f, sunDisc = 1.5f, sunGlow = 0.25f,
				sunAngles = new Vector2(45f, 200f),
				ambientSky = new Color(0.2f, 0.26f, 0.45f), ambientEquator = new Color(0.17f, 0.22f, 0.36f),
				ambientGround = new Color(0.06f, 0.08f, 0.14f), waterTint = new Color(0.3f, 0.36f, 0.55f),
				lights = 1f
			},
			new Preset
			{
				name = "Dawn",
				zenith = new Color(0.3f, 0.4f, 0.75f), sky = new Color(0.76f, 0.7f, 0.86f),
				fog = new Color(0.96f, 0.8f, 0.78f),
				cloudLit = new Color(1f, 0.88f, 0.86f), cloudShadow = new Color(0.6f, 0.6f, 0.76f),
				islands = new Color(0.5f, 0.5f, 0.66f), stars = 0f,
				sunColor = new Color(1f, 0.85f, 0.72f), sunIntensity = 1.2f, sunDisc = 2.5f, sunGlow = 0.6f,
				sunAngles = new Vector2(14f, 140f),
				ambientSky = new Color(0.55f, 0.6f, 0.82f), ambientEquator = new Color(0.86f, 0.76f, 0.76f),
				ambientGround = new Color(0.3f, 0.3f, 0.36f), waterTint = new Color(0.95f, 0.9f, 0.95f),
				lights = 0.25f
			}
		};

		private BoardState _board;
		private Weather _weather;
		private Renderer _water;
		private MaterialPropertyBlock _waterProperties;
		private Color _waterCrest;
		private Color _waterTrough;
		private Material _originalSky;
		private Material _sky;
		private float _cloudCoverage;
		private int _appliedIndex = -1;
		private float _appliedBlend = -1f;
		private float _appliedStorm = -1f;
		private bool _probeDirty;
		private float _nextProbeRefresh;

		[Inject]
		public void Construct(BoardState board, Weather weather, WaterSurface water)
		{
			_board = board;
			_weather = weather;
			_water = water.GetComponent<Renderer>();
		}

		private void Start()
		{
			// Runtime copy so the cycle never writes into the material asset.
			_originalSky = RenderSettings.skybox;
			_sky = new Material(_originalSky);
			RenderSettings.skybox = _sky;
			_cloudCoverage = _sky.GetFloat(CloudCoverageId);
			RenderSettings.ambientMode = AmbientMode.Trilight;
			_waterProperties = new MaterialPropertyBlock();
			_waterCrest = _water.sharedMaterial.GetColor(WaterCrestId);
			_waterTrough = _water.sharedMaterial.GetColor(WaterTroughId);

			if (randomStart)
			{
				distanceOffset += UnityEngine.Random.Range(0f, presetLength * presets.Length);
			}
		}

		private void OnDestroy()
		{
			if (_sky != null)
			{
				RenderSettings.skybox = _originalSky;
				Destroy(_sky);
			}

			// Globals outlive play mode in the editor; keep the lamps off and the sky on the light in edit mode.
			Shader.SetGlobalFloat(NightLightsId, 0f);
			Shader.SetGlobalVector(SkySunDirectionId, Vector4.zero);
		}

		private void Update()
		{
			if (presets.Length == 0)
			{
				return;
			}

			float cycleLength = presetLength * presets.Length;
			float distance = Mathf.Repeat(_board.Distance + distanceOffset, cycleLength);
			int index = Mathf.Min(Mathf.FloorToInt(distance / presetLength), presets.Length - 1);
			float local = distance - index * presetLength;
			float blend = Mathf.SmoothStep(0f, 1f,
				Mathf.InverseLerp(presetLength - transitionLength, presetLength, local));

			float storm = _weather.Storm;

			if (index != _appliedIndex || Mathf.Abs(blend - _appliedBlend) > 0.001f ||
			    Mathf.Abs(storm - _appliedStorm) > 0.001f)
			{
				_appliedIndex = index;
				_appliedBlend = blend;
				_appliedStorm = storm;
				Apply(Overcast(Lerp(presets[index], presets[(index + 1) % presets.Length], blend), storm));
				_sky.SetFloat(CloudCoverageId, Mathf.Lerp(_cloudCoverage, stormCloudCoverage, storm));
				_probeDirty = true;
			}

			UpdateProbe();
		}

		private void Apply(Preset preset)
		{
			RenderSettings.fogColor = preset.fog;
			RenderSettings.ambientSkyColor = preset.ambientSky;
			RenderSettings.ambientEquatorColor = preset.ambientEquator;
			RenderSettings.ambientGroundColor = preset.ambientGround;

			_sky.SetColor(ZenithId, preset.zenith);
			_sky.SetColor(SkyId, preset.sky);
			_sky.SetColor(CloudLitId, preset.cloudLit);
			_sky.SetColor(CloudShadowId, preset.cloudShadow);
			_sky.SetColor(IslandId, preset.islands);
			_sky.SetFloat(StarsId, preset.stars);
			_sky.SetColor(SunColorId, preset.sunColor);
			_sky.SetFloat(SunDiscId, preset.sunDisc);
			_sky.SetFloat(SunGlowId, preset.sunGlow);
			Shader.SetGlobalFloat(NightLightsId, preset.lights);

			Quaternion skySun = Quaternion.Euler(preset.sunAngles.x, preset.sunAngles.y, 0f);
			Shader.SetGlobalVector(SkySunDirectionId, -(skySun * Vector3.forward));

			if (sun != null)
			{
				sun.color = preset.sunColor;
				sun.intensity = preset.sunIntensity;
				sun.transform.rotation = Quaternion.Euler(Mathf.Max(preset.sunAngles.x, minLightElevation),
					preset.sunAngles.y, 0f);
			}

			_water.GetPropertyBlock(_waterProperties);
			_waterProperties.SetColor(WaterHorizonId, preset.fog);
			_waterProperties.SetColor(WaterCrestId, _waterCrest * preset.waterTint);
			_waterProperties.SetColor(WaterTroughId, _waterTrough * preset.waterTint);
			_water.SetPropertyBlock(_waterProperties);
		}

		private void UpdateProbe()
		{
			if (skyProbe == null)
			{
				return;
			}

			skyProbe.transform.position = _board.PlanarPosition;

			if (_probeDirty && Time.time >= _nextProbeRefresh)
			{
				_probeDirty = false;
				_nextProbeRefresh = Time.time + probeRefreshInterval;
				skyProbe.RenderProbe();
			}
		}

		/// <summary>
		/// Greys out and darkens the time of day by the storm's strength. Works from the preset's own colors, so a
		/// night storm stays dark.
		/// </summary>
		private Preset Overcast(Preset preset, float storm)
		{
			if (storm <= 0f)
			{
				return preset;
			}

			preset.zenith = Overcast(preset.zenith, stormSkyTint, storm);
			preset.sky = Overcast(preset.sky, stormSkyTint, storm);
			preset.fog = Overcast(preset.fog, stormSkyTint, storm);
			preset.cloudLit = Overcast(preset.cloudLit, stormSkyTint, storm);
			preset.cloudShadow = Overcast(preset.cloudShadow, stormSkyTint, storm);
			preset.islands = Overcast(preset.islands, stormSkyTint, storm);
			preset.stars *= 1f - storm;

			preset.sunColor = Overcast(preset.sunColor, Color.white, storm);
			preset.sunIntensity *= Mathf.Lerp(1f, stormSunIntensity, storm);
			preset.sunDisc *= 1f - storm;
			preset.sunGlow *= 1f - storm;

			preset.ambientSky = Overcast(preset.ambientSky, stormAmbientTint, storm);
			preset.ambientEquator = Overcast(preset.ambientEquator, stormAmbientTint, storm);
			preset.ambientGround = Overcast(preset.ambientGround, stormAmbientTint, storm);

			preset.waterTint = Overcast(preset.waterTint, stormWaterTint, storm);
			preset.lights = Mathf.Max(preset.lights, stormLights * storm);
			return preset;
		}

		private Color Overcast(Color color, Color tint, float storm)
		{
			float grey = color.grayscale;
			Color stormy = Color.Lerp(color, new Color(grey, grey, grey), stormDesaturation) * tint;
			return Color.Lerp(color, stormy, storm);
		}

		private static Preset Lerp(Preset a, Preset b, float t)
		{
			return new Preset
			{
				name = t < 0.5f ? a.name : b.name,
				zenith = Color.Lerp(a.zenith, b.zenith, t),
				sky = Color.Lerp(a.sky, b.sky, t),
				fog = Color.Lerp(a.fog, b.fog, t),
				cloudLit = Color.Lerp(a.cloudLit, b.cloudLit, t),
				cloudShadow = Color.Lerp(a.cloudShadow, b.cloudShadow, t),
				islands = Color.Lerp(a.islands, b.islands, t),
				stars = Mathf.Lerp(a.stars, b.stars, t),
				sunColor = Color.Lerp(a.sunColor, b.sunColor, t),
				sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t),
				sunDisc = Mathf.Lerp(a.sunDisc, b.sunDisc, t),
				sunGlow = Mathf.Lerp(a.sunGlow, b.sunGlow, t),
				sunAngles = Vector2.Lerp(a.sunAngles, b.sunAngles, t),
				ambientSky = Color.Lerp(a.ambientSky, b.ambientSky, t),
				ambientEquator = Color.Lerp(a.ambientEquator, b.ambientEquator, t),
				ambientGround = Color.Lerp(a.ambientGround, b.ambientGround, t),
				waterTint = Color.Lerp(a.waterTint, b.waterTint, t),
				lights = Mathf.Lerp(a.lights, b.lights, t)
			};
		}
	}
}
