using Game.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;
using VContainer.Unity;

namespace Game.Surf
{
	/// <summary>
	/// Puts the selected character's rider on the board. Riders are Addressable prefabs from the
	/// <see cref="CharacterRoster"/>, loaded when picked and released when replaced, so only the one riding is in memory.
	/// The loaded prefab is kept in the <see cref="RiderCache"/>, so after a scene reload she is placed right away.
	/// Each prefab's root transform is her stance on the board.
	/// </summary>
	public class RiderSpawner : MonoBehaviour
	{
		private ICharacterSelection _selection;
		private CharacterRoster _roster;
		private RiderCache _cache;
		private IObjectResolver _resolver;

		private GameObject _rider;
		private string _riderId;
		// The character being loaded; a newer pick makes the older load release itself when it finishes.
		private string _loadingId;

		[Inject]
		public void Construct(ICharacterSelection selection, CharacterRoster roster, RiderCache cache,
			IObjectResolver resolver)
		{
			_selection = selection;
			_roster = roster;
			_cache = cache;
			_resolver = resolver;
			_selection.Changed += Spawn;
			Spawn();
		}

		private void OnDestroy()
		{
			// The rider goes with the scene; her prefab stays loaded in the cache for the next one.
			if (_selection != null)
			{
				_selection.Changed -= Spawn;
			}
		}

		private void Spawn()
		{
			CharacterRoster.Character character = _roster[_roster.IndexOf(_selection.Selected)];
			string id = character.Id;
			if (id == _loadingId)
			{
				return;
			}

			if (id == _riderId)
			{
				// Back to the one riding: drop a newer pick that is still loading.
				_loadingId = null;
				return;
			}

			if (id == _cache.Id)
			{
				// The scene was reloaded and she is still loaded.
				_loadingId = null;
				Place(id, _cache.Prefab);
				return;
			}

			if (!character.Rider.RuntimeKeyIsValid())
			{
				Debug.LogError($"RiderSpawner: '{id}' has no rider prefab in the CharacterRoster.", this);
				return;
			}

			_loadingId = id;
			// By key rather than through the reference, which can only hold one load at a time.
			AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(character.Rider);
			handle.Completed += h => OnLoaded(id, h);
		}

		private void OnLoaded(string id, AsyncOperationHandle<GameObject> handle)
		{
			if (this == null || id != _loadingId)
			{
				Addressables.Release(handle);
				return;
			}

			_loadingId = null;

			if (handle.Status != AsyncOperationStatus.Succeeded)
			{
				Debug.LogError($"RiderSpawner: could not load the rider for '{id}'.", this);
				Addressables.Release(handle);
				return;
			}

			RemoveRider();
			// Releases the previous rider's prefab, now that nothing uses it.
			_cache.Set(id, handle);
			Place(id, handle.Result);
		}

		private void Place(string id, GameObject prefab)
		{
			RemoveRider();
			_riderId = id;
			_rider = _resolver.Instantiate(prefab, transform);
			_rider.name = prefab.name;
		}

		private void RemoveRider()
		{
			if (_rider != null)
			{
				// Destroy waits for the end of the frame; inactive, she already ignores the board's events.
				_rider.SetActive(false);
				Destroy(_rider);
				_rider = null;
			}
		}
	}
}
