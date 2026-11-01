using System;
using UnityEngine;

namespace Game.Core
{
	/// <summary>
	/// The defaults match the project as authored, or the cheapest settings on phones and tablets (including mobile
	/// browsers). Values are cached, so reading them every frame is cheap.
	/// </summary>
	public sealed class GraphicsSettings : IGraphicsSettings
	{
		private const string QualityKey = "graphics.quality";
		private const string DistanceKey = "graphics.distance";
		private const string ShadowsKey = "graphics.shadows";
		private const string FogKey = "graphics.fog";

		/// <summary>Indexed by <see cref="ViewDistance"/>.</summary>
		private static readonly float[] DistanceScales = { 0.7f, 1f, 1.35f };

		private GraphicsQuality _quality;
		private ViewDistance _distance;
		private ShadowDetail _shadows;
		private bool _fog;

		public GraphicsSettings()
		{
			bool mobile = Application.isMobilePlatform;
			_quality = (GraphicsQuality)PlayerPrefs.GetInt(QualityKey,
				(int)(mobile ? GraphicsQuality.Low : GraphicsQuality.Medium));
			_distance = (ViewDistance)PlayerPrefs.GetInt(DistanceKey,
				(int)(mobile ? ViewDistance.Near : ViewDistance.Medium));
			_shadows = (ShadowDetail)PlayerPrefs.GetInt(ShadowsKey, (int)(mobile ? ShadowDetail.Low : ShadowDetail.High));
			_fog = PlayerPrefs.GetInt(FogKey, 1) != 0;
		}

		public GraphicsQuality Quality
		{
			get => _quality;
			set
			{
				_quality = value;
				SetInt(QualityKey, (int)value);
			}
		}

		public ViewDistance Distance
		{
			get => _distance;
			set
			{
				_distance = value;
				SetInt(DistanceKey, (int)value);
			}
		}

		public float DistanceScale => DistanceScales[(int)_distance];

		public ShadowDetail Shadows
		{
			get => _shadows;
			set
			{
				_shadows = value;
				SetInt(ShadowsKey, (int)value);
			}
		}

		public bool Fog
		{
			get => _fog;
			set
			{
				_fog = value;
				SetInt(FogKey, value ? 1 : 0);
			}
		}

		public event Action Changed;

		public void Save()
		{
			PlayerPrefs.Save();
		}

		private void SetInt(string key, int value)
		{
			PlayerPrefs.SetInt(key, value);
			Changed?.Invoke();
		}
	}
}
