using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
	/// <summary>
	/// The scene's UI Toolkit panel. The panel renderer can build a new visual tree after the first one (live reload
	/// of the UXML, re-enabling), so presenters register a reload callback and look their screens up by name in each
	/// tree they get.
	/// </summary>
	[RequireComponent(typeof(PanelRenderer))]
	public class GameUi : MonoBehaviour
	{
		private readonly List<Action<VisualElement>> _reloadCallbacks = new List<Action<VisualElement>>();
		private PanelRenderer _renderer;
		private VisualElement _root;
		private int _version = -1;

		private void OnEnable()
		{
			_renderer = GetComponent<PanelRenderer>();
			_renderer.RegisterUIReloadCallback(OnUIReload);
		}

		private void OnDisable()
		{
			_renderer.UnregisterUIReloadCallback(OnUIReload);
		}

		/// <summary>Calls <paramref name="callback"/> with the current tree if it is loaded, then with every new one.</summary>
		public void RegisterReloadCallback(Action<VisualElement> callback)
		{
			_reloadCallbacks.Add(callback);

			if (_root != null)
			{
				callback(_root);
			}
		}

		public void UnregisterReloadCallback(Action<VisualElement> callback)
		{
			_reloadCallbacks.Remove(callback);
		}

		public static void SetVisible(VisualElement element, bool visible)
		{
			element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}

		/// <summary>
		/// Toggles <paramref name="className"/> every <paramref name="intervalMs"/>; with a USS transition on the class,
		/// the element throbs. Returned paused: Resume it when the element is shown. Runs on real time, so it keeps
		/// going while the game is paused.
		/// </summary>
		public static IVisualElementScheduledItem Pulse(VisualElement element, string className, long intervalMs)
		{
			IVisualElementScheduledItem pulse = element.schedule
				.Execute(() => element.ToggleInClassList(className))
				.Every(intervalMs);
			pulse.Pause();
			return pulse;
		}

		private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
		{
			// Called again for the same content too, like on the first frame.
			if (version == _version && root == _root)
			{
				return;
			}

			_version = version;
			_root = root;

			for (int i = 0; i < _reloadCallbacks.Count; i++)
			{
				// Each presenter hides its own screen here, so one that throws must not leave the rest showing.
				try
				{
					_reloadCallbacks[i](root);
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
				}
			}
		}
	}
}
