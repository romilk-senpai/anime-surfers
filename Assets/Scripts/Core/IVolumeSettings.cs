using System;

namespace Game.Core
{
	/// <summary>
	/// Player volume preferences, kept between sessions. Changes apply at once but are only written to disk by
	/// <see cref="Save"/>, so dragging a slider doesn't save every step.
	/// </summary>
	public interface IVolumeSettings
	{
		/// <summary>0..1</summary>
		float Music { get; set; }

		/// <summary>0..1</summary>
		float Effects { get; set; }

		bool Muted { get; set; }

		event Action Changed;

		void Save();
	}
}
