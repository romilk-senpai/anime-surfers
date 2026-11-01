using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using VContainer.Unity;

namespace Game.UI
{
	/// <summary>A screen that answers the menu keys while it is the top-most open one.</summary>
	public interface IScreen
	{
		void OnConfirm();

		/// <summary>The pause key.</summary>
		void OnBack();
	}

	/// <summary>
	/// The open screens, top-most last. Reads the menu keys once per frame and hands them to the top screen only,
	/// so screens don't need to know about each other.
	/// </summary>
	public sealed class ScreenStack : ITickable
	{
		private readonly List<IScreen> _screens = new List<IScreen>();
		private readonly IInputService _input;
		private int _changedFrame = -1;

		public ScreenStack(IInputService input)
		{
			_input = input;
		}

		/// <summary>Puts the screen on top; a screen already open keeps its place.</summary>
		public void Show(IScreen screen)
		{
			if (_screens.Contains(screen))
			{
				return;
			}

			_screens.Add(screen);
			_changedFrame = Time.frameCount;
		}

		public void Hide(IScreen screen)
		{
			if (_screens.Remove(screen))
			{
				_changedFrame = Time.frameCount;
			}
		}

		public void Tick()
		{
			// The key that opened or closed a screen this frame (Space on a focused button) must not also reach
			// the screen below.
			if (_screens.Count == 0 || _changedFrame == Time.frameCount)
			{
				return;
			}

			IScreen top = _screens[_screens.Count - 1];

			if (_input.ConfirmPressed)
			{
				top.OnConfirm();
			}
			else if (_input.PausePressed)
			{
				top.OnBack();
			}
		}
	}
}
