using System;
using System.Collections.Generic;
using Game.Core;
using Game.Surf;
using Game.UI.Elements;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace Game.UI
{
	/// <summary>
	/// Character tiles on the main menu: one slanted portrait per roster entry with its name above, the selected one
	/// highlighted. Picking one swaps the rider on the board right away.
	/// </summary>
	public sealed class CharacterPickerPresenter : IStartable, IDisposable
	{
		private const string SelectedClass = "character-tile--selected";

		private readonly ICharacterSelection _selection;
		private readonly CharacterRoster _roster;
		private readonly GameUi _ui;
		private readonly List<Button> _tiles = new List<Button>();

		private VisualElement _picker;

		public CharacterPickerPresenter(ICharacterSelection selection, CharacterRoster roster, GameUi ui)
		{
			_selection = selection;
			_roster = roster;
			_ui = ui;
		}

		public void Start()
		{
			_selection.Changed += Refresh;
			_ui.RegisterReloadCallback(Bind);
		}

		public void Dispose()
		{
			_selection.Changed -= Refresh;
			_ui.UnregisterReloadCallback(Bind);
			Unbind();
		}

		private void Bind(VisualElement root)
		{
			Unbind();

			_picker = root.Q("character-picker");

			for (int i = 0; i < _roster.Count; i++)
			{
				Button tile = CreateTile(_roster[i]);
				_picker.Add(tile);
				_tiles.Add(tile);
			}

			Refresh();
		}

		private void Unbind()
		{
			if (_picker == null)
			{
				return;
			}

			foreach (Button tile in _tiles)
			{
				tile.RemoveFromHierarchy();
			}

			_tiles.Clear();
			_picker = null;
		}

		private Button CreateTile(CharacterRoster.Character character)
		{
			Button tile = new Button(() => _selection.Select(character.Id));
			tile.AddToClassList("character-tile");

			Label name = new Label { pickingMode = PickingMode.Ignore };
			name.AddToClassList("character-tile__name");
			name.AddToClassList("display");
			name.SetBinding("text", character.DisplayName);
			tile.Add(name);

			InkPanel frame = new InkPanel { pickingMode = PickingMode.Ignore };
			frame.AddToClassList("character-tile__frame");
			tile.Add(frame);

			SlantImage portrait = new SlantImage { Image = character.Portrait, pickingMode = PickingMode.Ignore };
			portrait.AddToClassList("character-tile__portrait");
			frame.Add(portrait);

			return tile;
		}

		private void Refresh()
		{
			if (_picker == null)
			{
				return;
			}

			int selected = _roster.IndexOf(_selection.Selected);

			for (int i = 0; i < _tiles.Count; i++)
			{
				_tiles[i].EnableInClassList(SelectedClass, i == selected);
			}
		}
	}
}
