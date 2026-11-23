using System.Collections.Generic;
using UnityEngine;

namespace ZombieApocalypse
{
	/// <summary>
	/// Quiets each loaded village's fire to Volume of the game's loudness, on every client - the crackle and roar of a
	/// fire the vikings keep burning and gather around all night - and puts a fire back to full once it's no longer a
	/// village's
	/// </summary>
	/// <remarks>
	/// The fire's sounds are looped by the game's sound effects under it, whose volume modifier the game leaves alone
	/// for a fire, so it holds once it's set
	/// </remarks>
	internal static class VillageFireSound
	{
		/// <summary>
		/// How loud a village's fire is, as a share of the game's loudness
		/// </summary>
		private const float Volume = 0.5f;

		/// <summary>
		/// Seconds between looks for the villages' fires
		/// </summary>
		private const float CheckInterval = 2f;

		/// <summary>
		/// When the fires are next looked for, in Time.time seconds
		/// </summary>
		private static float nextCheck;

		/// <summary>
		/// The fires quieted, and the villages' fires found in the latest look
		/// </summary>
		private static readonly HashSet<Fireplace> quieted = new HashSet<Fireplace>();
		private static readonly HashSet<Fireplace> found = new HashSet<Fireplace>();

		/// <summary>
		/// Reused for the fires that are no longer a village's
		/// </summary>
		private static readonly List<Fireplace> released = new List<Fireplace>();

		/// <summary>
		/// Quiets the loaded villages' fires and puts those that are no longer a village's back to full, every
		/// CheckInterval seconds - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (Time.time < nextCheck)
			{
				return;
			}
			nextCheck = Time.time + CheckInterval;
			found.Clear();
			foreach (Village village in Village.Loaded())
			{
				Fireplace fire = village.Fire();
				if (fire == null)
				{
					continue;
				}
				found.Add(fire);
				if (quieted.Add(fire))
				{
					SetVolume(fire, Volume);
				}
			}
			foreach (Fireplace fire in quieted)
			{
				if (!found.Contains(fire))
				{
					released.Add(fire);
				}
			}
			foreach (Fireplace fire in released)
			{
				quieted.Remove(fire);
				if (fire != null)
				{
					SetVolume(fire, 1f);
				}
			}
			released.Clear();
		}

		/// <summary>
		/// Sets how loud a fire's sounds are, as a share of the game's loudness
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <param name="volume">The share</param>
		private static void SetVolume(Fireplace fire, float volume)
		{
			foreach (ZSFX sound in fire.GetComponentsInChildren<ZSFX>(true))
			{
				sound.SetVolumeModifier(volume);
			}
		}
	}
}
