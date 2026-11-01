using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Surf
{
	/// <summary>
	/// Builds particle systems from code for effects that are emitted by script. Each starts empty and playing with no
	/// emission, so the caller sets a rate or calls Emit; modules beyond main/emission/renderer are left to the caller.
	/// </summary>
	public static class RuntimeParticles
	{
		/// <summary>Grows in fast, then shrinks away: a glint that flashes and fades.</summary>
		public static readonly AnimationCurve Twinkle = new AnimationCurve(
			new Keyframe(0f, 0f, 0f, 8f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f));

		public static ParticleSystem Create(string objectName, Transform parent, Material material,
			ParticleSystemSimulationSpace space, int maxParticles)
		{
			var systemObject = new GameObject(objectName);
			systemObject.transform.SetParent(parent, false);

			var system = systemObject.AddComponent<ParticleSystem>();
			system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

			ParticleSystem.MainModule main = system.main;
			main.playOnAwake = false;
			main.loop = true;
			main.simulationSpace = space;
			main.scalingMode = ParticleSystemScalingMode.Shape;
			main.maxParticles = maxParticles;
			main.startSpeed = 0f;
			// Automatic culling pauses a looping system that looks off-screen, and an empty one has no bounds, so it
			// would never start emitting.
			main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

			ParticleSystem.EmissionModule emission = system.emission;
			emission.rateOverTime = 0f;

			ParticleSystem.ShapeModule shape = system.shape;
			shape.enabled = false;

			var renderer = systemObject.GetComponent<ParticleSystemRenderer>();
			renderer.sharedMaterial = material;
			renderer.shadowCastingMode = ShadowCastingMode.Off;
			renderer.receiveShadows = false;

			system.Play();
			return system;
		}

		/// <summary>Size multiplier over each particle's lifetime.</summary>
		public static void SetSizeOverLifetime(ParticleSystem system, AnimationCurve curve)
		{
			ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
			size.enabled = true;
			size.size = new ParticleSystem.MinMaxCurve(1f, curve);
		}

		/// <summary>Opaque at birth, fading out by the end of each particle's lifetime.</summary>
		public static void SetFadeOut(ParticleSystem system, float fadeStart)
		{
			var gradient = new Gradient();
			gradient.SetKeys(
				new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
				new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeStart), new GradientAlphaKey(0f, 1f) });

			ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
			color.enabled = true;
			color.color = gradient;
		}

		public static void SetRate(ParticleSystem system, float rate)
		{
			ParticleSystem.EmissionModule emission = system.emission;
			emission.rateOverTime = rate;
		}
	}
}
