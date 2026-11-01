using System.Collections.Generic;
using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Obstacle volume in passage space: distance along the path, lateral offset from the centerline and height.
	/// Working in path space keeps collisions exact on curves, where the visuals are only approximated.
	/// </summary>
	public sealed class ObstacleBox
	{
		public float Start;
		public float End;
		public float Left;
		public float Right;
		public float Bottom;
		public float Top;
		/// <summary>Length of the slope at the start; the surface rises from <see cref="RampStartHeight"/> to <see cref="Top"/>.</summary>
		public float RampLength;
		public float RampStartHeight;
		/// <summary>The board can ride on top of it.</summary>
		public bool Walkable;
		/// <summary>Heights are relative to the water surface under the board instead of absolute.</summary>
		public bool FollowsWater;
		/// <summary>Broken by the shield: no longer collides or supports the board.</summary>
		public bool Smashed;

		public float SurfaceAt(float distance)
		{
			if (RampLength <= 0f || distance >= Start + RampLength)
			{
				return Top;
			}

			return Mathf.Lerp(RampStartHeight, Top, (distance - Start) / RampLength);
		}

		public bool OverlapsLateral(float lateral, float halfWidth)
		{
			return lateral + halfWidth > Left && lateral - halfWidth < Right;
		}
	}

	/// <summary>
	/// Active obstacles the board collides with and rides on.
	/// </summary>
	public sealed class ObstacleField
	{
		private readonly List<ObstacleBox> _boxes = new List<ObstacleBox>();

		public IReadOnlyList<ObstacleBox> Boxes => _boxes;

		public void Add(ObstacleBox box)
		{
			_boxes.Add(box);
		}

		public void Remove(ObstacleBox box)
		{
			_boxes.Remove(box);
		}

		/// <summary>
		/// Highest walkable surface under the given point that was no higher than <paramref name="maxSurface"/> at
		/// <paramref name="fromDistance"/>. Measuring the step where the board came from lets it follow a ramp however
		/// far it moved this frame.
		/// </summary>
		public bool TryGetSupport(float distance, float fromDistance, float lateral, float halfWidth, float maxSurface,
			float waterHeight, out float height)
		{
			height = float.NegativeInfinity;

			foreach (ObstacleBox box in _boxes)
			{
				if (box.Smashed || !box.Walkable || distance < box.Start || distance > box.End ||
					!box.OverlapsLateral(lateral, halfWidth))
				{
					continue;
				}

				float offset = box.FollowsWater ? waterHeight : 0f;
				if (box.SurfaceAt(fromDistance) + offset > maxSurface)
				{
					continue;
				}

				float surface = box.SurfaceAt(distance) + offset;
				if (surface > height)
				{
					height = surface;
				}
			}

			return height > float.NegativeInfinity;
		}

		/// <summary>
		/// First obstacle the body intersects. Walkable surfaces within <paramref name="stepHeight"/> of the body bottom
		/// are stepped onto rather than hit.
		/// </summary>
		public bool TryGetHit(float distance, float halfLength, float lateral, float halfWidth, float bodyBottom,
			float bodyTop, float stepHeight, float waterHeight, out ObstacleBox hit)
		{
			foreach (ObstacleBox box in _boxes)
			{
				if (box.Smashed || distance + halfLength < box.Start || distance - halfLength > box.End ||
					!box.OverlapsLateral(lateral, halfWidth))
				{
					continue;
				}

				float offset = box.FollowsWater ? waterHeight : 0f;
				float surface = box.SurfaceAt(Mathf.Clamp(distance + halfLength, box.Start, box.End)) + offset;

				if (box.Walkable && bodyBottom + stepHeight >= surface)
				{
					continue;
				}

				if (bodyBottom < surface && bodyTop > box.Bottom + offset)
				{
					hit = box;
					return true;
				}
			}

			hit = null;
			return false;
		}
	}
}
