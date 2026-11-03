namespace Game.Surf
{
	/// <summary>
	/// Simulation time of the run, in seconds. Advanced by <see cref="RunLoop"/> before anything else each frame, so
	/// the board systems don't depend on <see cref="UnityEngine.Time.time"/>.
	/// </summary>
	public sealed class RunClock
	{
		public float Time { get; private set; }

		public void Advance(float dt)
		{
			Time += dt;
		}
	}
}
