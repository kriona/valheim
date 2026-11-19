using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Gives a zombie - or a corpse getting up on its own - the draugr's swarm of flies and the faint green haze around
	/// them
	/// </summary>
	/// <remarks>
	/// The draugr carries both effects on its spine, so copies of them are taken from the Draugr prefab and put on the
	/// zombie's bone nearest the middle of its body, so they move with its torso, even while it gets up. Each is
	/// sized to how big the creature is next to a draugr, from the size it has on the draugr - which its skeleton's
	/// own scale is part of - and filled in ahead so the swarm is full from the start. A zombie also gets the beehive's
	/// swarm buzzing, without its wasp sound, at ZombieBuzzVolume. Every client makes its own copies, which go with the
	/// zombie when it's destroyed. They're shown or hidden every second to follow ZombieFlies
	/// </remarks>
	internal class ZombieFlies : MonoBehaviour
	{
		private const string SourcePrefab = "Draugr";

		/// <summary>
		/// The draugr's effects, by their path from the prefab's root
		/// </summary>
		private static readonly string[] SourcePaths = new string[]
		{
			"Visual/_draugr_base/Armature/Hips/Spine0/Spine1/flies",
			"Visual/_draugr_base/Armature/Hips/Spine0/Spine1/evil_smoke"
		};

		/// <summary>
		/// The beehive's swarm sound, by its path from the prefab's root, and how far a zombie's buzzing carries
		/// </summary>
		private const string BuzzPrefab = "Beehive";
		private const string BuzzPath = "BeeEffect/SFX";
		private const float BuzzRange = 8f;

		private const float RefreshInterval = 1f;

		/// <summary>
		/// The smallest and largest the effects are scaled to, next to their size on a draugr
		/// </summary>
		private const float MinScale = 0.6f;
		private const float MaxScale = 3f;

		private static bool loggedMissing;

		/// <summary>
		/// The creature the effects are sized for, when they're on something other than the creature itself (e.g., a
		/// corpse getting up on its own)
		/// </summary>
		public Character Creature;

		private GameObject[] effects;
		private float nextRefresh;

		/// <summary>
		/// Copies the draugr's effects onto the zombie's or corpse's body
		/// </summary>
		private void Start()
		{
			Character character = GetComponent<Character>();
			Character creature = ((Creature != null) ? Creature : character);
			GameObject source = GamePrefabs.Find(SourcePrefab);
			if (creature == null || source == null)
			{
				LogMissing(SourcePrefab);
				return;
			}
			Ragdoll ragdoll = GetComponent<Ragdoll>();
			Vector3 center = ((character != null) ? character.GetCenterPoint() : ((ragdoll != null) ? ragdoll.GetAverageBodyPosition() : transform.position));
			Transform bone = FindBodyBone(center);
			CapsuleCollider sourceCollider = source.GetComponent<CapsuleCollider>();
			float sourceRadius = ((sourceCollider != null) ? sourceCollider.radius * source.transform.lossyScale.x : 0.5f);
			float size = Mathf.Clamp(creature.GetRadius() / sourceRadius, MinScale, MaxScale);
			effects = new GameObject[SourcePaths.Length + 1];
			for (int i = 0; i < SourcePaths.Length; i++)
			{
				Transform part = source.transform.Find(SourcePaths[i]);
				if (part == null)
				{
					LogMissing(SourcePrefab + "/" + SourcePaths[i]);
					continue;
				}
				effects[i] = Copy(part, bone, size);
			}
			// Only the zombie buzzes - a corpse getting up on its own has a hidden zombie buzzing from the same spot
			if (character != null)
			{
				effects[SourcePaths.Length] = AddBuzz(bone);
			}
			Refresh();
		}

		/// <summary>
		/// Copies the beehive's swarm buzzing onto a bone, with only its fly sounds, quieter and with a shorter range
		/// </summary>
		/// <param name="bone">The zombie's bone to put it on</param>
		/// <returns>The copy, or null when the beehive's sound couldn't be found</returns>
		private static GameObject AddBuzz(Transform bone)
		{
			GameObject hive = GamePrefabs.Find(BuzzPrefab);
			Transform part = ((hive != null) ? hive.transform.Find(BuzzPath) : null);
			if (part == null)
			{
				LogMissing(BuzzPrefab + "/" + BuzzPath);
				return(null);
			}
			GameObject copy = GamePrefabs.SpawnLocal(part.gameObject, bone.position, bone.rotation);
			copy.name = "ZombieBuzz";
			copy.transform.SetParent(bone, true);
			copy.transform.localPosition = Vector3.zero;
			// ZSFX picks its clip and volume when it first updates, after this
			ZSFX sound = copy.GetComponent<ZSFX>();
			if (sound != null)
			{
				List<AudioClip> clips = new List<AudioClip>();
				foreach (AudioClip clip in sound.m_audioClips)
				{
					if (clip != null && clip.name.IndexOf("Wasp", System.StringComparison.OrdinalIgnoreCase) < 0)
					{
						clips.Add(clip);
					}
				}
				if (clips.Count > 0)
				{
					sound.m_audioClips = clips.ToArray();
				}
				float volume = Plugin.ZombieBuzzVolume.Value;
				sound.m_minVol = volume;
				sound.m_maxVol = volume;
			}
			AudioSource audio = copy.GetComponent<AudioSource>();
			if (audio != null)
			{
				audio.maxDistance = BuzzRange;
			}
			return(copy);
		}

		/// <summary>
		/// Shows or hides the effects to follow ZombieFlies, every second
		/// </summary>
		private void Update()
		{
			if (Time.time >= nextRefresh)
			{
				Refresh();
			}
		}

		/// <summary>
		/// Shows or hides the effects to follow ZombieFlies
		/// </summary>
		private void Refresh()
		{
			nextRefresh = Time.time + RefreshInterval;
			if (effects == null)
			{
				return;
			}
			bool on = Plugin.ZombieFlies.Value;
			foreach (GameObject effect in effects)
			{
				if (effect != null && effect.activeSelf != on)
				{
					effect.SetActive(on);
				}
			}
		}

		/// <summary>
		/// Copies one of the draugr's effects onto a bone, at the size it has on the draugr scaled for the creature
		/// </summary>
		/// <param name="part">The effect on the draugr prefab</param>
		/// <param name="bone">The zombie's bone to put it on</param>
		/// <param name="size">How big the creature is next to a draugr</param>
		/// <returns>The copy</returns>
		private static GameObject Copy(Transform part, Transform bone, float size)
		{
			ParticleSystem[] sourceSystems = part.GetComponentsInChildren<ParticleSystem>(true);
			// A system scaled by its own transform alone ignores the skeleton's scale above it, so its size on the
			// draugr is its own scale, otherwise the scale of everything above it too
			bool ownScaleOnly = (sourceSystems.Length > 0 && sourceSystems[0].main.scalingMode == ParticleSystemScalingMode.Local);
			Vector3 sourceScale = (ownScaleOnly ? part.localScale : part.lossyScale);
			GameObject copy = GamePrefabs.SpawnLocal(part.gameObject, bone.position, bone.rotation);
			copy.name = part.name;
			copy.transform.SetParent(bone, true);
			copy.transform.localPosition = Vector3.zero;
			Vector3 boneScale = bone.lossyScale;
			Vector3 wanted = sourceScale * size;
			copy.transform.localScale = new Vector3(SafeDivide(wanted.x, boneScale.x), SafeDivide(wanted.y, boneScale.y), SafeDivide(wanted.z, boneScale.z));
			foreach (ParticleSystem particles in copy.GetComponentsInChildren<ParticleSystem>(true))
			{
				ParticleSystem.MainModule main = particles.main;
				main.scalingMode = ParticleSystemScalingMode.Hierarchy;
				// Filled in ahead, so the swarm is already full rather than building up over its particles' lifetime
				particles.Simulate(main.startLifetime.constantMax, true, true);
				particles.Play(true);
			}
			return(copy);
		}

		/// <summary>
		/// Finds the skinned bone nearest the middle of the body
		/// </summary>
		/// <param name="center">The middle of the body</param>
		/// <returns>The bone, or this object's own transform when it has no skinned bones</returns>
		private Transform FindBodyBone(Vector3 center)
		{
			Transform best = transform;
			float bestDistance = float.MaxValue;
			foreach (SkinnedMeshRenderer skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
			{
				foreach (Transform bone in skin.bones)
				{
					if (bone == null)
					{
						continue;
					}
					float distance = (bone.position - center).sqrMagnitude;
					if (distance < bestDistance)
					{
						bestDistance = distance;
						best = bone;
					}
				}
			}
			return(best);
		}

		/// <summary>
		/// Divides, giving the numerator back when the divisor is too small to divide by
		/// </summary>
		/// <param name="value">The numerator</param>
		/// <param name="divisor">The divisor</param>
		/// <returns>The quotient</returns>
		private static float SafeDivide(float value, float divisor)
		{
			return((Mathf.Abs(divisor) > 0.0001f) ? value / divisor : value);
		}

		/// <summary>
		/// Logs, once, that something the effects are copied from couldn't be found
		/// </summary>
		/// <param name="what">What's missing</param>
		private static void LogMissing(string what)
		{
			if (!loggedMissing)
			{
				loggedMissing = true;
				Plugin.Log.LogInfo(what + " couldn't be found, so zombies have no flies");
			}
		}
	}
}
