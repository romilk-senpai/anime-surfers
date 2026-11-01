using System;
using Game.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Elements
{
	/// <summary>
	/// Turns a drag into one swipe per touch, sent as soon as the finger has moved <see cref="Threshold"/> panel
	/// units, so the board reacts mid-gesture. Follows one finger at a time; buttons drawn on top keep their own
	/// touches.
	/// </summary>
	[UxmlElement]
	public partial class SwipeArea : VisualElement
	{
		private int _pointerId = -1;
		private Vector2 _start;
		private bool _swiped;

		public SwipeArea()
		{
			RegisterCallback<PointerDownEvent>(OnPointerDown);
			RegisterCallback<PointerMoveEvent>(OnPointerMove);
			RegisterCallback<PointerUpEvent>(OnPointerUp);
			RegisterCallback<PointerCancelEvent>(OnPointerCancel);
			RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
		}

		public event Action<Swipe> Swiped;

		[UxmlAttribute("threshold")]
		public float Threshold { get; set; } = 40f;

		private void OnPointerDown(PointerDownEvent evt)
		{
			if (_pointerId != -1)
			{
				return;
			}

			_pointerId = evt.pointerId;
			_start = evt.position;
			_swiped = false;
			this.CapturePointer(_pointerId);
		}

		private void OnPointerMove(PointerMoveEvent evt)
		{
			if (evt.pointerId != _pointerId || _swiped)
			{
				return;
			}

			Vector2 delta = (Vector2)evt.position - _start;

			if (delta.sqrMagnitude < Threshold * Threshold)
			{
				return;
			}

			_swiped = true;

			// Panel y points down.
			Swipe swipe = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
				? delta.x > 0f ? Swipe.Right : Swipe.Left
				: delta.y > 0f ? Swipe.Down : Swipe.Up;
			Swiped?.Invoke(swipe);
		}

		private void OnPointerUp(PointerUpEvent evt)
		{
			Release(evt.pointerId);
		}

		private void OnPointerCancel(PointerCancelEvent evt)
		{
			Release(evt.pointerId);
		}

		private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
		{
			_pointerId = -1;
		}

		private void Release(int pointerId)
		{
			if (pointerId != _pointerId)
			{
				return;
			}

			_pointerId = -1;
			this.ReleasePointer(pointerId);
		}
	}
}
