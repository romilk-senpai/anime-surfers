using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Surf
{
	/// <summary>
	/// Generates rows of obstacles ahead of the board, one obstacle (or nothing) per lane, always leaving a passable
	/// lane: trains (rows of moored boats) block the lane but can be ridden on, climbable trains have a ramp in front,
	/// and barriers (shimenawa ropes) are jumped over or slid under. Obstacles left behind are removed.
	/// </summary>
	public sealed class ObstacleGenerator
	{
		private readonly ObstacleSettings _settings;
		private readonly BoardSettings _boardSettings;
		private readonly BoardState _board;
		private readonly ObstacleField _field;
		private readonly IGraphicsSettings _graphics;
		private readonly RandomStream _random;
		/// <summary>The obstacle in each lane of the row being generated; null for none.</summary>
		private readonly ObstacleKind?[] _row = new ObstacleKind?[8];

		public ObstacleGenerator(ObstacleSettings settings, BoardSettings boardSettings, BoardState board,
			ObstacleField field, IGraphicsSettings graphics, RunRandom random)
		{
			_settings = settings;
			_boardSettings = boardSettings;
			_board = board;
			_field = field;
			_graphics = graphics;
			_random = random.Stream(RunRandom.ObstacleStream);
			GeneratedUntil = settings.FirstRowDistance;
		}

		/// <summary>Every obstacle that starts before this distance has been generated.</summary>
		public float GeneratedUntil { get; private set; }

		public void Tick()
		{
			float distance = _board.Distance;

			while (GeneratedUntil < distance + _settings.SpawnAhead * _graphics.DistanceScale)
			{
				GeneratedUntil = GenerateRow(GeneratedUntil) + _random.Range(_settings.RowGap.x, _settings.RowGap.y);
			}

			IReadOnlyList<ObstacleBox> boxes = _field.Boxes;

			for (int i = boxes.Count - 1; i >= 0; i--)
			{
				if (boxes[i].End < distance - _settings.DespawnBehind)
				{
					_field.Remove(boxes[i]);
				}
			}
		}

		/// <summary>Generates one row starting at the given distance and returns where it ends.</summary>
		private float GenerateRow(float start)
		{
			int laneCount = Mathf.Min(_boardSettings.LaneCount, _row.Length);
			bool passable = false;
			bool any = false;

			for (int lane = 0; lane < laneCount; lane++)
			{
				_row[lane] = PickObstacle();
				passable |= _row[lane] != ObstacleKind.Train;
				any |= _row[lane] != null;
			}

			if (!passable)
			{
				_row[_random.Range(0, laneCount)] = null;
			}
			else if (!any)
			{
				_row[_random.Range(0, laneCount)] = ObstacleKind.Barrier;
			}

			float end = start;

			for (int lane = 0; lane < laneCount; lane++)
			{
				float lateral = _boardSettings.LaneOffset(lane);

				ObstacleBox box = _row[lane] switch
				{
					ObstacleKind.Barrier => Barrier(start, lateral),
					ObstacleKind.Train => Train(start, lateral,
						_random.Range(_settings.TrainCars.x, _settings.TrainCars.y + 1), false),
					ObstacleKind.ClimbableTrain => Train(start, lateral,
						_random.Range(_settings.ClimbableTrainCars.x, _settings.ClimbableTrainCars.y + 1), true),
					_ => null
				};

				if (box != null)
				{
					_field.Add(box);
					end = Mathf.Max(end, box.End);
				}
			}

			return end;
		}

		/// <summary>An obstacle for one lane, or null for none.</summary>
		private ObstacleKind? PickObstacle()
		{
			float noneWeight = _settings.NoneWeight;
			float barrierWeight = _settings.BarrierWeight;
			float trainWeight = _settings.TrainWeight;
			float roll = _random.Range(0f, noneWeight + barrierWeight + trainWeight + _settings.ClimbableTrainWeight);

			if ((roll -= noneWeight) < 0f)
			{
				return null;
			}

			if ((roll -= barrierWeight) < 0f)
			{
				return ObstacleKind.Barrier;
			}

			return roll - trainWeight < 0f ? ObstacleKind.Train : ObstacleKind.ClimbableTrain;
		}

		private ObstacleBox Train(float start, float lateral, int cars, bool withRamp)
		{
			float ramp = withRamp ? _settings.RampLength : 0f;
			float halfWidth = _settings.TrainWidth * 0.5f;

			return new ObstacleBox
			{
				Kind = withRamp ? ObstacleKind.ClimbableTrain : ObstacleKind.Train,
				Cars = cars,
				Start = start,
				End = start + ramp + cars * _settings.CarLength + (cars - 1) * _settings.CarGap,
				Left = lateral - halfWidth,
				Right = lateral + halfWidth,
				Bottom = _settings.TrainBottom,
				Top = _settings.TrainHeight,
				RampLength = ramp,
				RampStartHeight = _settings.RampStartHeight,
				Walkable = true
			};
		}

		private ObstacleBox Barrier(float start, float lateral)
		{
			float halfWidth = _settings.BarrierWidth * 0.5f;

			return new ObstacleBox
			{
				Kind = ObstacleKind.Barrier,
				Start = start,
				End = start + _settings.BarrierDepth,
				Left = lateral - halfWidth,
				Right = lateral + halfWidth,
				Bottom = _settings.BarrierBottom,
				Top = _settings.BarrierTop,
				FollowsWater = true
			};
		}
	}
}
