using System;

namespace Game.Surf
{
	/// <summary>
	/// The run's phase and the commands screens send to change it. Each command is ignored outside the phases it
	/// applies to.
	/// </summary>
	public interface IRunFlow
	{
		RunPhase Phase { get; }

		event Action<RunPhase> PhaseChanged;

		/// <summary>From the main menu.</summary>
		void StartRun();

		/// <summary>During a run.</summary>
		void Pause();

		void Resume();

		/// <summary>From the results screen: reloads straight into a new run.</summary>
		void Restart();

		/// <summary>From the pause menu or the results screen.</summary>
		void QuitToMenu();
	}
}
