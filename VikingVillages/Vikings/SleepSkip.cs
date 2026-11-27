using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Keeps the night players sleep through from wearing down PC vikings' food, rest and forsaken power cooldown -
	/// the world's clock skips ahead to morning, which a player's food, rest and cooldown don't count, since they wear
	/// down with the time that passes for the player rather than with the world's clock - and notes that the night was
	/// slept through, so no viking goes to bed in the morning after
	/// </summary>
	/// <remarks>
	/// The server runs the world's clock ahead over a few seconds, and each client's follows it, so each client watches
	/// for its clock running ahead of the time that passed while a sleep is underway, and puts off the food, rest and
	/// cooldown of the vikings it owns by that much. The clock stops at the game's morning (see
	/// EnvMan.GetMorningStartSec), a while before EnvMan.IsNight says the night is over
	/// </remarks>
	internal static class SleepSkip
	{
		/// <summary>
		/// How far the world's clock has to run ahead of the time that passed during a sleep before the night counts as
		/// slept through, in seconds, so the clock catching up with the server now and then while a player lies in bed
		/// doesn't count
		/// </summary>
		private const double SkipNoticed = 5.0;

		/// <summary>
		/// The world's clock, in seconds, and the time that had passed for this client, in Time.time seconds, when last
		/// looked at, or a negative clock before it's been looked at in this world
		/// </summary>
		private static double lastWorld = -1.0;
		private static float lastReal;

		/// <summary>
		/// How far the world's clock has run ahead during the sleep underway, in seconds, and whether the players have
		/// slept through the night - from when the clock has run SkipNoticed ahead until the night is over
		/// </summary>
		private static double skipped;
		private static bool sleptThrough;

		/// <summary>
		/// Checks whether the players slept through the night that's still ending - from when the world's clock skipped
		/// ahead until EnvMan.IsNight says it's over, some while after the morning the players woke to
		/// </summary>
		/// <returns>True while it is</returns>
		public static bool SleptThroughNight()
		{
			return(sleptThrough);
		}

		/// <summary>
		/// Watches the world's clock for skipping ahead while players sleep, putting off the food, rest and cooldown of
		/// the vikings this client owns by how far it skipped, and noting the night as slept through until it's over -
		/// call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (ZNet.instance == null || EnvMan.instance == null)
			{
				lastWorld = -1.0;
				skipped = 0.0;
				sleptThrough = false;
				return;
			}
			double world = ZNet.instance.GetTimeSeconds();
			float real = Time.time;
			bool sleeping = Sleeping();
			if (lastWorld >= 0.0 && sleeping)
			{
				double ahead = (world - lastWorld) - (real - lastReal);
				if (ahead > 0.0)
				{
					Postpone(NetTime.Ticks((float)ahead));
					skipped += ahead;
					sleptThrough |= (skipped >= SkipNoticed);
				}
			}
			else
			{
				skipped = 0.0;
			}
			// Kept while the sleep is underway, since one that starts in the afternoon skips through the end of the day
			// before the night
			if (sleptThrough && !sleeping && !EnvMan.IsNight())
			{
				sleptThrough = false;
			}
			lastWorld = world;
			lastReal = real;
		}

		/// <summary>
		/// Checks whether a sleep is underway - the server skipping the world ahead, or this client's player asleep in
		/// it
		/// </summary>
		/// <returns>True when it is</returns>
		private static bool Sleeping()
		{
			return(EnvMan.instance.IsTimeSkipping() || (Player.m_localPlayer != null && Player.m_localPlayer.IsSleeping()));
		}

		/// <summary>
		/// Puts off the food, rest and forsaken power cooldown of every PC viking this client owns
		/// </summary>
		/// <param name="skipped">How far the world skipped ahead, in ZNet time ticks</param>
		private static void Postpone(long skipped)
		{
			foreach (VillageLife life in VillageLife.All)
			{
				ZNetView view = ((life != null) ? life.GetComponent<ZNetView>() : null);
				if (view == null || !view.IsValid() || !view.IsOwner())
				{
					continue;
				}
				VikingFood.Postpone(view.GetZDO(), skipped);
				VikingRested.Postpone(view.GetZDO(), skipped);
				VikingPowers.Postpone(view.GetZDO(), skipped);
			}
		}
	}
}
