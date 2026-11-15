using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Game.Surf
{
	/// <summary>
	/// Keeps the riding character's prefab loaded across scene loads, so "Surf again" and going back to the menu
	/// don't load her bundle again and spawn her in the middle of the intro. Filled by <see cref="RiderSpawner"/>.
	/// </summary>
	public sealed class RiderCache : IDisposable
	{
		private AsyncOperationHandle<GameObject> _handle;

		/// <summary>Id of the loaded character, or null while none is.</summary>
		public string Id { get; private set; }

		public GameObject Prefab => _handle.IsValid() ? _handle.Result : null;

		/// <summary>Takes over a finished load and releases the previous one.</summary>
		public void Set(string id, AsyncOperationHandle<GameObject> handle)
		{
			Release();
			Id = id;
			_handle = handle;
		}

		public void Dispose()
		{
			Release();
		}

		private void Release()
		{
			if (_handle.IsValid())
			{
				Addressables.Release(_handle);
			}

			_handle = default;
			Id = null;
		}
	}
}
