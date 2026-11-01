using System;
using Game.Core;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace Game.UI
{
	/// <summary>
	/// Graphics options over the settings: quality, view distance, shadows and fog. Changes apply at once and are
	/// saved on closing.
	/// </summary>
	public sealed class GraphicsPresenter : IScreen, IStartable, IDisposable
	{
		private readonly IGraphicsSettings _graphics;
		private readonly ScreenStack _screens;
		private readonly GameUi _ui;
		private readonly OptionGroup[] _options;

		private VisualElement _screen;
		private Button _done;
		private bool _isOpen;

		public GraphicsPresenter(IGraphicsSettings graphics, ScreenStack screens, GameUi ui)
		{
			_graphics = graphics;
			_screens = screens;
			_ui = ui;

			_options = new[]
			{
				new OptionGroup("quality", 3, () => (int)_graphics.Quality, v => _graphics.Quality = (GraphicsQuality)v),
				new OptionGroup("distance", 3, () => (int)_graphics.Distance, v => _graphics.Distance = (ViewDistance)v),
				new OptionGroup("shadows", 3, () => (int)_graphics.Shadows, v => _graphics.Shadows = (ShadowDetail)v),
				new OptionGroup("fog", 2, () => _graphics.Fog ? 1 : 0, v => _graphics.Fog = v != 0)
			};
		}

		/// <summary>After the screen is closed, so the screen below can take the focus back.</summary>
		public event Action Closed;

		public void Start()
		{
			_graphics.Changed += UpdateButtons;
			_ui.RegisterReloadCallback(Bind);
		}

		public void Dispose()
		{
			_graphics.Changed -= UpdateButtons;
			_ui.UnregisterReloadCallback(Bind);
			Unbind();
		}

		public void Open()
		{
			if (_screen == null)
			{
				return;
			}

			_isOpen = true;
			Refresh();
			_screens.Show(this);
			_done.Focus();
		}

		public void Close()
		{
			if (!_isOpen)
			{
				return;
			}

			_graphics.Save();
			_isOpen = false;
			_screens.Hide(this);

			if (_screen != null)
			{
				GameUi.SetVisible(_screen, false);
			}

			Closed?.Invoke();
		}

		public void OnConfirm()
		{
		}

		public void OnBack()
		{
			Close();
		}

		private void Bind(VisualElement root)
		{
			Unbind();

			_screen = root.Q("graphics");
			_done = _screen.Q<Button>("done-button");
			_done.clicked += Close;

			foreach (OptionGroup group in _options)
			{
				group.Bind(_screen);
			}

			Refresh();
		}

		private void Unbind()
		{
			if (_screen == null)
			{
				return;
			}

			_done.clicked -= Close;

			foreach (OptionGroup group in _options)
			{
				group.Unbind();
			}
		}

		private void Refresh()
		{
			UpdateButtons();
			GameUi.SetVisible(_screen, _isOpen);
		}

		private void UpdateButtons()
		{
			foreach (OptionGroup group in _options)
			{
				group.Refresh();
			}
		}

		/// <summary>A row of buttons "{name}-0", "{name}-1"… that picks one value by index.</summary>
		private sealed class OptionGroup
		{
			private readonly string _name;
			private readonly Func<int> _selected;
			private readonly Action[] _select;
			private Button[] _buttons;

			public OptionGroup(string name, int count, Func<int> selected, Action<int> select)
			{
				_name = name;
				_selected = selected;
				_select = new Action[count];

				for (int i = 0; i < count; i++)
				{
					int value = i;
					_select[i] = () => select(value);
				}
			}

			public void Bind(VisualElement screen)
			{
				_buttons = new Button[_select.Length];

				for (int i = 0; i < _select.Length; i++)
				{
					_buttons[i] = screen.Q<Button>(_name + "-" + i);
					_buttons[i].clicked += _select[i];
				}
			}

			public void Unbind()
			{
				if (_buttons == null)
				{
					return;
				}

				for (int i = 0; i < _buttons.Length; i++)
				{
					_buttons[i].clicked -= _select[i];
				}

				_buttons = null;
			}

			public void Refresh()
			{
				if (_buttons == null)
				{
					return;
				}

				int selected = _selected();

				for (int i = 0; i < _buttons.Length; i++)
				{
					_buttons[i].EnableInClassList("option-button--selected", i == selected);
				}
			}
		}
	}
}
