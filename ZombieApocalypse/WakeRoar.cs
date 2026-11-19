using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Plays a roar when a zombie troll throws its head back in its wake-up animation, which has no sound of its own
	/// </summary>
	/// <remarks>
	/// The troll's Wakeup clip throws the head back about 2.9 seconds in, read from the clip's head and neck curves,
	/// but carries no animation event to play a sound there. This watches the animator and plays the roar once the
	/// clip reaches that point. Every client animates the zombie, so each plays the roar for itself, with the sound
	/// spawned locally rather than over the network. The sound is the fire troll's unused roar from the game's files,
	/// or the troll's alert growl when that isn't loaded
	/// </remarks>
	internal class WakeRoar : MonoBehaviour
	{
		/// <summary>
		/// The wake-up clip and how far into it the head is thrown back, in seconds
		/// </summary>
		private const string WakeClip = "Wakeup";
		private const float RoarTime = 2.9f;

		private const string RoarSound = "sfx_trollfire_roar";
		private const string FallbackSound = "sfx_troll_alerted";

		/// <summary>
		/// Seconds to wait for the roar before giving up, in case the wake-up animation never plays
		/// </summary>
		private const float GiveUpTime = 12f;

		/// <summary>
		/// Seconds a roar's sound is kept before it's removed, since it isn't networked and won't remove itself
		/// </summary>
		private const float SoundLifetime = 10f;

		private static bool loggedFallback;

		private Character character;
		private float elapsed;

		/// <summary>
		/// Checks whether a creature roars as it wakes up
		/// </summary>
		/// <param name="character">The creature</param>
		/// <returns>True for trolls, whether they were sleeping or not</returns>
		public static bool Roars(Character character)
		{
			string name = Utils.GetPrefabName(character.gameObject);
			return(name == "Troll" || name == "Troll_sleeping");
		}

		/// <summary>
		/// Finds the zombie's creature
		/// </summary>
		private void Awake()
		{
			character = GetComponent<Character>();
		}

		/// <summary>
		/// Plays the roar once the wake-up clip reaches the head throw, or gives up
		/// </summary>
		private void Update()
		{
			elapsed += Time.deltaTime;
			if (elapsed > GiveUpTime || character == null || character.m_animator == null)
			{
				Destroy(this);
				return;
			}
			Animator animator = character.m_animator;
			AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
			foreach (AnimatorClipInfo info in animator.GetCurrentAnimatorClipInfo(0))
			{
				if (info.clip != null && info.clip.name == WakeClip && state.normalizedTime * info.clip.length >= RoarTime)
				{
					Play();
					Destroy(this);
					return;
				}
			}
		}

		/// <summary>
		/// Spawns the roar's sound at the zombie's head, on this client only
		/// </summary>
		private void Play()
		{
			GameObject prefab = FindRoar();
			if (prefab == null)
			{
				return;
			}
			Vector3 position = ((character.m_eye != null) ? character.m_eye.position : transform.position);
			Destroy(GamePrefabs.SpawnLocal(prefab, position, Quaternion.identity), SoundLifetime);
		}

		/// <summary>
		/// Finds the roar's sound prefab, falling back to the troll's alert growl, logging the fallback once
		/// </summary>
		/// <returns>The prefab, or null when neither could be found</returns>
		private static GameObject FindRoar()
		{
			GameObject prefab = GamePrefabs.Find(RoarSound);
			if (prefab != null)
			{
				return(prefab);
			}
			if (!loggedFallback)
			{
				loggedFallback = true;
				Plugin.Log.LogInfo(RoarSound + " isn't loaded, so zombie trolls roar with " + FallbackSound);
			}
			return(GamePrefabs.Find(FallbackSound));
		}
	}
}
