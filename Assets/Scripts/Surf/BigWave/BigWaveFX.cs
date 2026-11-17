using Game.Audio;
using UnityEngine;
using VContainer;

namespace Game.Surf
{
	/// <summary>
	/// Sound for the big wave: its roar, rising with the wave and louder once the lip is out; a splash as the lip first
	/// comes out; and a splash as the rider kicks out of the barrel, or a bigger one as she's wiped out.
	/// </summary>
	public class BigWaveFX : MonoBehaviour
	{
		[Header("Roar")]
		[Tooltip("Looping rush of water, pitched down into a roar.")]
		[SerializeField] private AudioSource roarLoop;
		[SerializeField, Range(0f, 1f)] private float roarVolume = 0.35f;
		[Tooltip("Share of the roar while the wave stands without its lip thrown.")]
		[SerializeField, Range(0f, 1f)] private float standingRoar = 0.4f;
		[SerializeField] private float roarSmoothing = 3f;

		[Header("Sounds")]
		[SerializeField] private AudioClip[] splashClips;
		[SerializeField] private AudioClip kickOutClip;
		[SerializeField, Range(0f, 1f)] private float splashVolume = 0.6f;
		[SerializeField, Range(0f, 1f)] private float kickOutVolume = 0.5f;

		private BigWaveSettings _settings;
		private IWaterSurface _water;
		private IBoardState _board;
		private BarrelRide _barrel;
		private ISoundController _sound;
		private bool _lipOut;

		[Inject]
		public void Construct(BigWaveSettings settings, IWaterSurface water, IBoardState board, BarrelRide barrel,
			ISoundController sound)
		{
			_settings = settings;
			_water = water;
			_board = board;
			_barrel = barrel;
			_sound = sound;
			_barrel.Ended += OnRideEnded;
			_board.Crashed += OnCrashed;
		}

		private void OnDestroy()
		{
			if (_barrel != null)
			{
				_barrel.Ended -= OnRideEnded;
				_board.Crashed -= OnCrashed;
			}
		}

		private void Update()
		{
			if (_water == null)
			{
				return;
			}

			BigWaveShape wave = _water.BigWave;
			UpdateRoar(wave);

			bool lipOut = wave.Exists && wave.Throw > 0.95f;
			if (lipOut && !_lipOut)
			{
				PlaySplash(PlungePoint(wave, Mathf.Max(wave.LipAlong, wave.MouthLength)), splashVolume);
			}

			_lipOut = lipOut;
		}

		private void UpdateRoar(BigWaveShape wave)
		{
			if (roarLoop == null)
			{
				return;
			}

			float target = wave.Exists
				? wave.Height / _settings.WaveHeight * Mathf.Lerp(standingRoar, 1f, wave.Throw) * roarVolume *
				  _sound.EffectsVolume
				: 0f;
			roarLoop.volume = Mathf.Lerp(roarLoop.volume, target, 1f - Mathf.Exp(-roarSmoothing * Time.deltaTime));

			if (roarLoop.volume > 0.001f && !roarLoop.isPlaying)
			{
				roarLoop.Play();
			}
			else if (roarLoop.volume <= 0.001f && roarLoop.isPlaying)
			{
				roarLoop.Stop();
			}
		}

		private void OnRideEnded(bool success)
		{
			if (!success)
			{
				return;
			}

			Vector3 rider = RiderPoint();
			PlaySplash(rider, splashVolume * 0.6f);

			if (kickOutClip != null)
			{
				_sound.PlayClipAtPosition(kickOutClip, rider, kickOutVolume);
			}
		}

		private void OnCrashed()
		{
			if (_board.CrashKind == CrashKind.Wipeout)
			{
				PlaySplash(RiderPoint(), splashVolume);
			}
		}

		private void PlaySplash(Vector3 position, float volume)
		{
			if (splashClips != null && splashClips.Length > 0)
			{
				_sound.PlayClipAtPosition(splashClips[Random.Range(0, splashClips.Length)], position, volume);
			}
		}

		/// <summary>On the water under the lip's tip at the given distance along the ridge.</summary>
		private Vector3 PlungePoint(BigWaveShape wave, float along)
		{
			float across = wave.Crest - wave.LipReach;
			Vector2 xz = wave.Origin + wave.Direction * along + wave.SideDirection * across;
			var position = new Vector3(xz.x, 0f, xz.y);
			position.y = _water.GetHoverHeight(position);
			return position;
		}

		private Vector3 RiderPoint()
		{
			return _board.PlanarPosition + Vector3.up * (_board.Height + 0.5f);
		}
	}
}
