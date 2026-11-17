namespace Game.Surf
{
	/// <summary>
	/// The run's randomness, seeded from <see cref="PathSettings.Seed"/> so a run can be replayed. Each consumer
	/// draws from its own stream, so how far ahead one of them generates doesn't change what the others get.
	/// </summary>
	public sealed class RunRandom
	{
		public const int PathStream = 1;
		public const int CollisionStream = 2;
		public const int WeatherStream = 3;
		public const int ObstacleStream = 4;
		public const int PickupStream = 5;
		public const int WaveStream = 6;
		public const int BarrelStream = 7;
		public const int SkyCoinStream = 8;

		public RunRandom(PathSettings settings)
		{
			// Picked once, so a random run can still be replayed by putting its seed into the settings.
			Seed = settings.Seed != 0 ? settings.Seed : new System.Random().Next(1, int.MaxValue);
		}

		public int Seed { get; }

		/// <summary>A new stream for the given id; the same id always gives the same sequence for a seed.</summary>
		public RandomStream Stream(int id)
		{
			return new RandomStream(Mix(Seed, id));
		}

		/// <summary>Spreads nearby seeds and ids apart, since System.Random's first values follow the seed closely.</summary>
		private static int Mix(int seed, int id)
		{
			unchecked
			{
				uint hash = (uint)seed ^ (uint)id * 0x9E3779B9u;
				hash ^= hash >> 16;
				hash *= 0x85EBCA6Bu;
				hash ^= hash >> 13;
				hash *= 0xC2B2AE35u;
				hash ^= hash >> 16;
				return (int)hash;
			}
		}
	}

	/// <summary>
	/// One sequence of <see cref="RunRandom"/>, with the parts of <see cref="UnityEngine.Random"/> the simulation uses.
	/// </summary>
	public sealed class RandomStream
	{
		private readonly System.Random _random;

		public RandomStream(int seed)
		{
			_random = new System.Random(seed);
		}

		/// <summary>In 0..1.</summary>
		public float Value => (float)_random.NextDouble();

		public float Range(float min, float max)
		{
			return min + (float)_random.NextDouble() * (max - min);
		}

		/// <summary>Excludes <paramref name="max"/>, like <see cref="UnityEngine.Random.Range(int, int)"/>.</summary>
		public int Range(int min, int max)
		{
			return max > min ? _random.Next(min, max) : min;
		}
	}
}
