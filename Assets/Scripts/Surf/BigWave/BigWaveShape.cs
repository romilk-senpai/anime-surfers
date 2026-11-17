using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// The big wave as the water draws it, set by <see cref="BigWave"/> every frame. The ridge runs along a line on the
	/// XZ plane; across is measured from that line toward the wave's side, along from its origin. The default is no wave.
	/// </summary>
	public struct BigWaveShape
	{
		public Vector2 Origin;
		/// <summary>Unit XZ direction along the ridge.</summary>
		public Vector2 Direction;
		/// <summary>1 if the wave is on the right of the direction, -1 on the left.</summary>
		public float Side;
		/// <summary>Crest height above the still water now; 0 means there's no wave.</summary>
		public float Height;
		public float AlongStart;
		public float AlongEnd;
		/// <summary>The ends taper off over this far along.</summary>
		public float Taper;
		/// <summary>Across where the face starts rising.</summary>
		public float FaceBase;
		/// <summary>Across where the crest is.</summary>
		public float Crest;
		/// <summary>The back falls from the crest to the still water over this far across.</summary>
		public float BackWidth;
		/// <summary>How much the face's upper half steepens toward the crest, 0 = an even curve, 1 = hollow.</summary>
		public float FaceHollow;

		/// <summary>Along where the rider is, but no further back than the barrel's start; the lip moves with her.</summary>
		public float LipAlong;
		/// <summary>How far the lip has thrown out over the barrel, 0..1.</summary>
		public float Throw;
		/// <summary>Across from the crest to the fully thrown lip's tip.</summary>
		public float LipReach;
		/// <summary>The lip spans this far behind the rider and this far ahead of her.</summary>
		public float LipBehind;
		public float LipAhead;
		/// <summary>The lip pitches out over this far from the barrel's start (along 0).</summary>
		public float MouthLength;
		/// <summary>The lip is fully pitched up to this far ahead of the rider; past it it pitches less and less.</summary>
		public float PitchAhead;

		public bool Exists => Height > 0f;

		/// <summary>Unit XZ direction across, toward the wave.</summary>
		public Vector2 SideDirection => new Vector2(Direction.y, -Direction.x) * Side;

		/// <summary>Mirrors BigWaveEnds in WaterWaves.hlsl: 1 along the middle of the ridge, easing to 0 at its ends.</summary>
		public float Ends(float along)
		{
			float taper = Mathf.Max(Taper, 1e-3f);
			return Mathf.SmoothStep(0f, 1f, (along - AlongStart) / taper) *
			       Mathf.SmoothStep(0f, 1f, (AlongEnd - along) / taper);
		}

		/// <summary>The face's steepness at the crest as the profile's slope there, even (t²) and fully hollow.</summary>
		public const float EvenCrestSlope = 2f;
		public const float HollowCrestSlope = 4.3f;
		/// <summary>Up to here the face is t² whatever its hollow.</summary>
		private const float HollowStart = 0.5f;

		/// <summary>
		/// Mirrors BigWaveFace in WaterWaves.hlsl: the face's height 0..1 at t, 0 at its base and 1 at the crest, with
		/// its slope in dProfile. It's t² over the lower half, then a cubic up to the crest that comes in steeper with
		/// hollow (at most as steep as a cubic stays monotonic).
		/// </summary>
		public static float FaceProfile(float t, float hollow, out float dProfile)
		{
			if (t <= HollowStart)
			{
				dProfile = 2f * t;
				return t * t;
			}

			const float h = 1f - HollowStart;
			float s = (t - HollowStart) / h;
			float y0 = HollowStart * HollowStart;
			float m0 = 2f * HollowStart * h;
			float m1 = Mathf.Lerp(EvenCrestSlope, HollowCrestSlope, hollow) * h;
			float s2 = s * s;
			float s3 = s2 * s;
			dProfile = ((6f * s2 - 6f * s) * y0 + (3f * s2 - 4f * s + 1f) * m0 + (6f * s - 6f * s2) +
			            (3f * s2 - 2f * s) * m1) / h;
			return (2f * s3 - 3f * s2 + 1f) * y0 + (s3 - 2f * s2 + s) * m0 + (3f * s2 - 2f * s3) + (s3 - s2) * m1;
		}
	}
}
