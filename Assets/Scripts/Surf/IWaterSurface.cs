using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// The water surface as gameplay and its views sample it: heights, normals, the wave clock and the storm scale,
	/// so they don't depend on the scene's water component.
	/// </summary>
	public interface IWaterSurface : IWaterHeight
	{
		/// <summary>
		/// Cheaper <see cref="IWaterHeight.GetHeight"/> for things that only hover over the water, about a centimeter
		/// off instead of exact.
		/// </summary>
		float GetHoverHeight(Vector3 position);

		/// <summary>World-space surface normal at the given XZ position.</summary>
		Vector3 GetNormal(Vector3 position);

		/// <summary>The wave clock, in seconds, matching what the GPU renders this frame.</summary>
		float WaveTime { get; }

		/// <summary>World-space height of the still water.</summary>
		float BaseHeight { get; }

		/// <summary>Multiplies every wave's height, e.g. 2 in a storm.</summary>
		float WaveScale { get; set; }
	}
}
