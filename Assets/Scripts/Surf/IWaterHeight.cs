using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Water height lookup used by the board simulation, so it doesn't depend on the scene's water component.
	/// </summary>
	public interface IWaterHeight
	{
		/// <summary>World-space height of the water surface at the given XZ position.</summary>
		float GetHeight(Vector3 position);
	}
}
