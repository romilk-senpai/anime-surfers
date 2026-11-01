using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core
{
	public sealed class InputService : IInputService, ITouchInput, IDisposable
	{
		private readonly SurfersInputActions _actions = new SurfersInputActions();

		// A swipe arrives from a UI pointer event, which can run before or after the gameplay tick in a frame, so it
		// is kept for one extra frame and cleared by the reader instead of tested against this frame only.
		private Swipe? _swipe;
		private int _swipeFrame;
		private bool _touchBoost;

		public InputService()
		{
			_actions.Menu.Enable();
		}

		public bool IsEnabled => _actions.Player.enabled;

		public int LaneChange
		{
			get
			{
				if (TakeSwipe(Swipe.Left))
				{
					return -1;
				}

				if (TakeSwipe(Swipe.Right))
				{
					return 1;
				}

				InputAction move = _actions.Player.Move;

				if (!move.triggered)
				{
					return 0;
				}

				return move.ReadValue<float>() > 0f ? 1 : -1;
			}
		}

		public bool JumpPressed => _actions.Player.Jump.WasPerformedThisFrame() | TakeSwipe(Swipe.Up);
		public bool SlidePressed => _actions.Player.Slide.WasPerformedThisFrame() | TakeSwipe(Swipe.Down);
		public bool BoostHeld => _actions.Player.Boost.IsPressed() || (IsEnabled && _touchBoost);
		public bool ConfirmPressed => _actions.Menu.Confirm.WasPerformedThisFrame();
		public bool PausePressed => _actions.Menu.Pause.WasPerformedThisFrame();

		public void Enable()
		{
			_actions.Player.Enable();
		}

		public void Disable()
		{
			_actions.Player.Disable();
			_swipe = null;
		}

		public void PushSwipe(Swipe swipe)
		{
			if (!IsEnabled)
			{
				return;
			}

			_swipe = swipe;
			_swipeFrame = Time.frameCount;
		}

		public void SetBoostHeld(bool held)
		{
			_touchBoost = held;
		}

		public void Dispose()
		{
			_actions.Dispose();
		}

		private bool TakeSwipe(Swipe swipe)
		{
			if (_swipe != swipe)
			{
				return false;
			}

			_swipe = null;
			return Time.frameCount - _swipeFrame <= 1;
		}
	}
}
