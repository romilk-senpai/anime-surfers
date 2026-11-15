using System.Collections.Generic;
using UnityEngine;

namespace Game.Surf
{
	public enum WavePhase
	{
		None,
		/// <summary>The wave rises ahead while the passage runs straight and clear.</summary>
		Approach,
		/// <summary>The passage turns to run along the wave, into the barrel.</summary>
		DropIn,
		Barrel,
		/// <summary>The passage turns back out of the wave.</summary>
		Exit
	}

	/// <summary>
	/// One big wave along the passage. All distances are along the path.
	/// </summary>
	public sealed class WaveEvent
	{
		public float Start;
		public float DropInStart;
		public float BarrelStart;
		public float BarrelEnd;
		/// <summary>End of the exit turn.</summary>
		public float End;
		/// <summary>Obstacles and pickups stay out of <see cref="Start"/>..<see cref="ClearEnd"/>.</summary>
		public float ClearEnd;
		/// <summary>Side the passage turns to if it's heading close to its starting direction: -1 left, 1 right.</summary>
		public int PreferredTurn;
		/// <summary>
		/// Side the passage turns into the wave, -1 left or 1 right; picked by <see cref="SurfPath"/> when it
		/// generates the start of the wave, 0 until then.
		/// </summary>
		public int Turn;

		public WavePhase PhaseAt(float distance)
		{
			if (distance < Start || distance >= End)
			{
				return WavePhase.None;
			}

			if (distance < DropInStart)
			{
				return WavePhase.Approach;
			}

			if (distance < BarrelStart)
			{
				return WavePhase.DropIn;
			}

			return distance < BarrelEnd ? WavePhase.Barrel : WavePhase.Exit;
		}
	}

	/// <summary>
	/// Where the big waves come along the passage, picked from their own random stream so the path, the generators
	/// and the weather all agree on them however far ahead each one looks. Waves are generated on demand ahead.
	/// </summary>
	public sealed class WaveSchedule
	{
		private readonly BigWaveSettings _settings;
		private readonly RandomStream _random;
		private readonly List<WaveEvent> _events = new List<WaveEvent>();
		private float _nextStart;

		public WaveSchedule(BigWaveSettings settings, RunRandom random)
		{
			_settings = settings;
			_random = random.Stream(RunRandom.WaveStream);
			_nextStart = _random.Range(settings.FirstDistance.x, settings.FirstDistance.y);
		}

		/// <summary>The wave whose <see cref="WaveEvent.Start"/>..<see cref="WaveEvent.End"/>, widened by the margin
		/// on both sides, contains the distance.</summary>
		public bool TryGet(float distance, float margin, out WaveEvent wave)
		{
			return TryGetOverlap(distance - margin, distance + margin, false, out wave);
		}

		/// <summary>The first wave whose clear range overlaps from..to.</summary>
		public bool TryGetClearOverlap(float from, float to, out WaveEvent wave)
		{
			return TryGetOverlap(from, to, true, out wave);
		}

		private bool TryGetOverlap(float from, float to, bool clear, out WaveEvent wave)
		{
			EnsureUntil(to);

			foreach (WaveEvent e in _events)
			{
				if (e.Start < to && from < (clear ? e.ClearEnd : e.End))
				{
					wave = e;
					return true;
				}
			}

			wave = null;
			return false;
		}

		private void EnsureUntil(float distance)
		{
			while (_nextStart <= distance)
			{
				var wave = new WaveEvent { Start = _nextStart };
				wave.DropInStart = wave.Start + _settings.ApproachLength;
				wave.BarrelStart = wave.DropInStart + _settings.DropInLength;
				wave.BarrelEnd = wave.BarrelStart + _settings.BarrelLength;
				wave.End = wave.BarrelEnd + _settings.ExitLength;
				wave.ClearEnd = wave.End + _settings.ClearAfter;
				wave.PreferredTurn = _random.Value < 0.5f ? -1 : 1;
				_events.Add(wave);

				_nextStart = wave.End + _random.Range(_settings.GapDistance.x, _settings.GapDistance.y);
			}
		}
	}
}
