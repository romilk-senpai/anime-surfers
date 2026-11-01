using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using DeviceApplication = UnityEngine.Device.Application;
using DeviceScreen = UnityEngine.Device.Screen;

namespace Game.UI
{
	/// <summary>
	/// The scene's UI Toolkit panel. The panel renderer can build a new visual tree after the first one (live reload
	/// of the UXML, re-enabling), so presenters register a reload callback and look their screens up by name in each
	/// tree they get.
	/// Phones and tablets get the mobile tree, whose HUD and menu are touch screens of their own; the root then has
	/// the <c>mobile</c> class for the screens both trees share, and <c>portrait</c> while taller than wide.
	/// Elements with the <c>safe-area</c> class are kept clear of notches and rounded corners.
	/// </summary>
	[RequireComponent(typeof(PanelRenderer))]
	public class GameUi : MonoBehaviour
	{
		public const string MobileClass = "mobile";
		public const string PortraitClass = "portrait";
		public const string SafeAreaClass = "safe-area";

		[Tooltip("Replaces the panel's tree on phones and tablets.")]
		[SerializeField] private VisualTreeAsset mobileTree;

		[Tooltip("Use the mobile tree on desktop too, to try it in the editor.")]
		[SerializeField] private bool forceMobile;

		private readonly List<Action<VisualElement>> _reloadCallbacks = new List<Action<VisualElement>>();
		private PanelRenderer _renderer;
		private VisualElement _root;
		private int _version = -1;
		private Rect _safeArea;
		private Vector2 _safeAreaPanelSize;

		/// <summary>
		/// True on phones and tablets, including mobile browsers in WebGL builds. Reads the Device API so the Device
		/// Simulator counts as a phone.
		/// </summary>
		public bool IsMobile => forceMobile || DeviceApplication.isMobilePlatform;

		private void Awake()
		{
			_renderer = GetComponent<PanelRenderer>();

			if (IsMobile && mobileTree != null)
			{
				_renderer.visualTreeAsset = mobileTree;
			}
		}

		private void OnEnable()
		{
			_renderer.RegisterUIReloadCallback(OnUIReload);
		}

		private void OnDisable()
		{
			_renderer.UnregisterUIReloadCallback(OnUIReload);
		}

		private void Update()
		{
			if (_root == null)
			{
				return;
			}

			// Rotating the phone changes both the safe area and the panel size.
			Rect safeArea = DeviceScreen.safeArea;
			Vector2 panelSize = _root.layout.size;

			if (safeArea != _safeArea || panelSize != _safeAreaPanelSize)
			{
				ApplySafeArea(safeArea, panelSize);
			}
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

		private static void OnRootGeometryChanged(GeometryChangedEvent evt)
		{
			var root = (VisualElement)evt.target;
			root.EnableInClassList(PortraitClass, root.layout.height > root.layout.width);
		}

		/// <summary>Turns the screen's safe area into margins, in panel units, on every safe-area element.</summary>
		private void ApplySafeArea(Rect safeArea, Vector2 panelSize)
		{
			// Before the first layout the size is NaN; try again next frame.
			if (float.IsNaN(panelSize.x) || panelSize.x <= 0f || DeviceScreen.width <= 0)
			{
				return;
			}

			_safeArea = safeArea;
			_safeAreaPanelSize = panelSize;

			// As fractions of the screen, so the panel's scaling doesn't matter. Screen pixels have y up, the panel y down.
			float scaleX = panelSize.x / DeviceScreen.width;
			float scaleY = panelSize.y / DeviceScreen.height;
			float left = safeArea.xMin * scaleX;
			float right = (DeviceScreen.width - safeArea.xMax) * scaleX;
			float top = (DeviceScreen.height - safeArea.yMax) * scaleY;
			float bottom = safeArea.yMin * scaleY;

			_root.Query(className: SafeAreaClass).ForEach(element =>
			{
				element.style.marginLeft = left;
				element.style.marginRight = right;
				element.style.marginTop = top;
				element.style.marginBottom = bottom;
			});
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
			_safeAreaPanelSize = Vector2.zero;
			root.EnableInClassList(MobileClass, IsMobile);
			root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

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
