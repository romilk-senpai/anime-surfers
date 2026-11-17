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
		private bool _touchHold;

		public InputService()
		{
			_actions.Menu.Enable();
		}

		public bool IsEnabled => _actions.Player.enabled;

		public GameplayInput ReadGameplay()
		{
			Swipe? swipe = TakeSwipe();

			return new GameplayInput(LaneChange(swipe),
				_actions.Player.Jump.WasPerformedThisFrame() || swipe == Swipe.Up,
				_actions.Player.Slide.WasPerformedThisFrame() || swipe == Swipe.Down,
				_actions.Player.Boost.IsPressed() || (IsEnabled && _touchBoost),
				_actions.Player.Jump.IsPressed() || (IsEnabled && _touchHold));
		}

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

		public void SetScreenHeld(bool held)
		{
			_touchHold = held;
		}

		public void Dispose()
		{
			_actions.Dispose();
		}

		private int LaneChange(Swipe? swipe)
		{
			if (swipe == Swipe.Left)
			{
				return -1;
			}

			if (swipe == Swipe.Right)
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

		private Swipe? TakeSwipe()
		{
			Swipe? swipe = _swipe;
			_swipe = null;
			return Time.frameCount - _swipeFrame <= 1 ? swipe : null;
		}
	}
}
