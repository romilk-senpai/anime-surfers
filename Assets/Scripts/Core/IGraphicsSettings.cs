using System;

namespace Game.Core
{
	/// <summary>Render scale, anti-aliasing, bloom resolution and water shading detail.</summary>
	public enum GraphicsQuality
	{
		Low,
		Medium,
		High
	}

	/// <summary>
	/// How far the fog, the camera's far plane, the water and spawning reach, relative to the authored values.
	/// </summary>
	public enum ViewDistance
	{
		Near,
		Medium,
		Far
	}

	/// <summary>Low: half the distance and resolution, one cascade, hard edges.</summary>
	public enum ShadowDetail
	{
		Off,
		Low,
		High
	}

	/// <summary>
	/// Player graphics preferences, kept between sessions. Changes apply at once but are only written to disk by
	/// <see cref="Save"/>.
	/// </summary>
	public interface IGraphicsSettings
	{
		GraphicsQuality Quality { get; set; }

		ViewDistance Distance { get; set; }

		/// <summary>Multiplier for authored distances that <see cref="Distance"/> stands for; 1 at Medium.</summary>
		float DistanceScale { get; }

		ShadowDetail Shadows { get; set; }

		bool Fog { get; set; }

		event Action Changed;

		void Save();
	}
}
