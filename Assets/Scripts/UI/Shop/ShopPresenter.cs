using System;
using System.Globalization;
using Game.Core;
using Game.Surf;
using Game.UI.Elements;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace Game.UI
{
	/// <summary>
	/// Shop over the main menu: buys duration upgrades for each power-up with the coins from past runs.
	/// </summary>
	public sealed class ShopPresenter : IScreen, IStartable, IDisposable
	{
		private readonly IPlayerProgress _progress;
		private readonly PowerUpUpgrades _upgrades;
		private readonly ScreenStack _screens;
		private readonly GameUi _ui;
		private readonly UpgradeRow[] _rows;

		private VisualElement _screen;
		private Label _balance;
		private Button _done;
		private bool _isOpen;

		public ShopPresenter(IPlayerProgress progress, PowerUpUpgrades upgrades, ScreenStack screens, GameUi ui)
		{
			_progress = progress;
			_upgrades = upgrades;
			_screens = screens;
			_ui = ui;

			_rows = new[]
			{
				new UpgradeRow("magnet", PickupKind.Magnet, this),
				new UpgradeRow("multiplier", PickupKind.ScoreMultiplier, this),
				new UpgradeRow("shield", PickupKind.Shield, this),
				new UpgradeRow("super-jump", PickupKind.SuperJump, this),
				new UpgradeRow("coin-doubler", PickupKind.CoinDoubler, this)
			};
		}

		/// <summary>After the screen is closed, so the screen below can take the focus back.</summary>
		public event Action Closed;

		public void Start()
		{
			_upgrades.Changed += UpdateRows;
			_ui.RegisterReloadCallback(Bind);
		}

		public void Dispose()
		{
			_upgrades.Changed -= UpdateRows;
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

			_screen = root.Q("shop");
			_balance = _screen.Q<Label>("balance");
			_done = _screen.Q<Button>("done-button");
			_done.clicked += Close;

			foreach (UpgradeRow row in _rows)
			{
				row.Bind(_screen, _upgrades.MaxLevel);
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

			foreach (UpgradeRow row in _rows)
			{
				row.Unbind();
			}
		}

		private void Refresh()
		{
			UpdateRows();
			GameUi.SetVisible(_screen, _isOpen);
		}

		private void UpdateRows()
		{
			if (_screen == null)
			{
				return;
			}

			_balance.text = FormatCoins(_progress.TotalCoins);

			foreach (UpgradeRow row in _rows)
			{
				row.Refresh(_upgrades);
			}
		}

		private void Buy(UpgradeRow row)
		{
			if (!_upgrades.TryBuy(row.Kind))
			{
				return;
			}

			// The button disables itself once maxed or out of reach, which drops the focus.
			if (!row.CanBuy)
			{
				_done.Focus();
			}
		}

		private static string FormatCoins(int coins) => coins.ToString("N0", CultureInfo.InvariantCulture);

		private static string FormatPercent(float scale) =>
			Math.Round(scale * 100f).ToString(CultureInfo.InvariantCulture) + "%";

		/// <summary>Row "{name}" with "{name}-buy", "-cost", "-value", "-next" and the pip container "-pips".</summary>
		private sealed class UpgradeRow
		{
			private readonly string _name;
			private readonly Action _buy;
			private VisualElement _row;
			private Button _button;
			private Label _cost;
			private Label _value;
			private Label _next;
			private VisualElement[] _pips;

			public UpgradeRow(string name, PickupKind kind, ShopPresenter shop)
			{
				_name = name;
				Kind = kind;
				_buy = () => shop.Buy(this);
			}

			public PickupKind Kind { get; }

			public bool CanBuy => _button != null && _button.enabledSelf;

			public void Bind(VisualElement screen, int levels)
			{
				_row = screen.Q(_name);
				_button = _row.Q<Button>(_name + "-buy");
				_cost = _row.Q<Label>(_name + "-cost");
				_value = _row.Q<Label>(_name + "-value");
				_next = _row.Q<Label>(_name + "-next");
				_button.clicked += _buy;

				VisualElement pips = _row.Q(_name + "-pips");
				pips.Clear();
				_pips = new VisualElement[levels];

				for (int i = 0; i < levels; i++)
				{
					_pips[i] = new InkPanel { pickingMode = PickingMode.Ignore };
					_pips[i].AddToClassList("shop-pip");
					pips.Add(_pips[i]);
				}
			}

			public void Unbind()
			{
				if (_button != null)
				{
					_button.clicked -= _buy;
				}

				_button = null;
			}

			public void Refresh(PowerUpUpgrades upgrades)
			{
				if (_button == null)
				{
					return;
				}

				int level = upgrades.Level(Kind);
				bool maxed = upgrades.IsMaxed(Kind);

				_row.EnableInClassList("shop-row--maxed", maxed);
				_button.SetEnabled(upgrades.CanAfford(Kind));
				_value.text = FormatPercent(upgrades.DurationScale(Kind));

				if (!maxed)
				{
					_cost.text = FormatCoins(upgrades.NextCost(Kind));
					_next.text = "→ " + FormatPercent(upgrades.DurationScaleAt(level + 1));
				}

				for (int i = 0; i < _pips.Length; i++)
				{
					_pips[i].EnableInClassList("shop-pip--filled", i < level);
				}
			}
		}
	}
}
