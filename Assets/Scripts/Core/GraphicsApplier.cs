using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Game.Core
{
	/// <summary>
	/// Applies <see cref="IGraphicsSettings"/>: quality and shadows on the URP asset, the sun and a bloom override,
	/// view distance and fog on each loaded scene's fog and main camera. The water and the spawners read the view
	/// distance themselves. The authored values are the "Medium"/"High" baseline; the URP asset is put back on dispose
	/// so play mode in the editor leaves it unchanged.
	/// </summary>
	public sealed class GraphicsApplier : IStartable, IDisposable
	{
		/// <summary>Indexed by <see cref="GraphicsQuality"/>.</summary>
		private static readonly float[] RenderScales = { 0.7f, 1f, 1f };

		/// <summary>Indexed by <see cref="GraphicsQuality"/>.</summary>
		private static readonly int[] MsaaSamples = { 1, 1, 4 };

		private const float LowShadowScale = 0.5f;

		// Water shading for low quality: the long waves are shaded from the vertices.
		private static readonly GlobalKeyword WaterLowKeyword = GlobalKeyword.Create("_WATER_LOW");

		private readonly IGraphicsSettings _settings;

		private UniversalRenderPipelineAsset _pipeline;
		private float _renderScale;
		private int _msaaSamples;
		private float _shadowDistance;
		private int _shadowResolution;
		private int _shadowCascades;

		private Scene _scene;
		private bool _sceneFog;
		private float _fogStart;
		private float _fogEnd;
		private Camera _camera;
		private float _farClip;
		private Light _sun;
		private LightShadows _sunShadows;

		// Global volume over the scene's own, overriding only what low quality changes.
		private GameObject _volumeObject;
		private VolumeProfile _volumeProfile;

		public GraphicsApplier(IGraphicsSettings settings)
		{
			_settings = settings;
		}

		public void Start()
		{
			_pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

			if (_pipeline != null)
			{
				_renderScale = _pipeline.renderScale;
				_msaaSamples = _pipeline.msaaSampleCount;
				_shadowDistance = _pipeline.shadowDistance;
				_shadowResolution = _pipeline.mainLightShadowmapResolution;
				_shadowCascades = _pipeline.shadowCascadeCount;
			}

			CreateVolume();
			ReadScene();
			Apply();

			_settings.Changed += Apply;
			SceneManager.sceneLoaded += OnSceneLoaded;
		}

		public void Dispose()
		{
			_settings.Changed -= Apply;
			SceneManager.sceneLoaded -= OnSceneLoaded;

			if (_pipeline != null)
			{
				_pipeline.renderScale = _renderScale;
				_pipeline.msaaSampleCount = _msaaSamples;
				_pipeline.shadowDistance = _shadowDistance;
				_pipeline.mainLightShadowmapResolution = _shadowResolution;
				_pipeline.shadowCascadeCount = _shadowCascades;
			}

			// Global keywords outlive play mode in the editor.
			Shader.SetKeyword(WaterLowKeyword, false);

			if (_volumeObject != null)
			{
				Object.Destroy(_volumeObject);
				Object.Destroy(_volumeProfile);
			}
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			// An additive scene keeps the active scene's fog, which already has the settings applied. Reading a scene
			// twice would scale its values twice.
			if (mode == LoadSceneMode.Single && scene != _scene)
			{
				ReadScene();
			}

			Apply();
		}

		private void ReadScene()
		{
			_scene = SceneManager.GetActiveScene();
			_sceneFog = RenderSettings.fog;
			_fogStart = RenderSettings.fogStartDistance;
			_fogEnd = RenderSettings.fogEndDistance;
			_camera = Camera.main;
			_farClip = _camera != null ? _camera.farClipPlane : 0f;
			_sun = RenderSettings.sun;
			_sunShadows = _sun != null ? _sun.shadows : LightShadows.None;
		}

		private void CreateVolume()
		{
			_volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
			_volumeProfile.name = "Graphics Settings";
			_volumeProfile.Add<Bloom>();

			_volumeObject = new GameObject("Graphics Settings Volume");
			Object.DontDestroyOnLoad(_volumeObject);
			var volume = _volumeObject.AddComponent<Volume>();
			volume.isGlobal = true;
			volume.priority = 100f;
			volume.sharedProfile = _volumeProfile;
		}

		private void Apply()
		{
			float distanceScale = _settings.DistanceScale;
			bool low = _settings.Quality == GraphicsQuality.Low;

			RenderSettings.fog = _sceneFog && _settings.Fog;
			RenderSettings.fogStartDistance = _fogStart * distanceScale;
			RenderSettings.fogEndDistance = _fogEnd * distanceScale;

			if (_camera != null)
			{
				_camera.farClipPlane = _farClip * distanceScale;
			}

			Shader.SetKeyword(WaterLowKeyword, low);

			// Bloom from a quarter-size image instead of half: much cheaper, a little softer.
			if (_volumeProfile.TryGet(out Bloom bloom))
			{
				bloom.downscale.overrideState = low;
				bloom.downscale.value = BloomDownscaleMode.Quarter;
			}

			if (_sun != null && _sunShadows != LightShadows.None)
			{
				// URP only filters soft shadows for lights set to soft.
				_sun.shadows = _settings.Shadows == ShadowDetail.High ? _sunShadows : LightShadows.Hard;
			}

			if (_pipeline == null)
			{
				return;
			}

			int quality = (int)_settings.Quality;
			_pipeline.renderScale = _renderScale * RenderScales[quality];
			_pipeline.msaaSampleCount = MsaaSamples[quality];

			switch (_settings.Shadows)
			{
				case ShadowDetail.Off:
					// URP skips shadow rendering at a shadow distance of 0.
					_pipeline.shadowDistance = 0f;
					break;
				case ShadowDetail.Low:
					_pipeline.shadowDistance = _shadowDistance * LowShadowScale;
					_pipeline.mainLightShadowmapResolution = Mathf.Max(256, (int)(_shadowResolution * LowShadowScale));
					_pipeline.shadowCascadeCount = 1;
					break;
				default:
					_pipeline.shadowDistance = _shadowDistance;
					_pipeline.mainLightShadowmapResolution = _shadowResolution;
					_pipeline.shadowCascadeCount = _shadowCascades;
					break;
			}
		}
	}
}
