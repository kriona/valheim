using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Keeps the night players sleep through from wearing down living vikings' food, rest and forsaken power cooldown -
	/// the world's clock skips ahead to morning, which a player's food, rest and cooldown don't count, since they wear
	/// down with the time that passes for the player rather than with the world's clock
	/// </summary>
	/// <remarks>
	/// The server runs the world's clock ahead over a few seconds, and each client's follows it, so each client watches
	/// for its clock running ahead of the time that passed while a sleep is underway, and puts off the food, rest and
	/// cooldown of the vikings it owns by that much
	/// </remarks>
	internal static class SleepSkip
	{
		/// <summary>
		/// The world's clock, in seconds, and the time that had passed for this client, in Time.time seconds, when last
		/// looked at, or a negative clock before it's been looked at in this world
		/// </summary>
		private static double lastWorld = -1.0;
		private static float lastReal;

		/// <summary>
		/// Watches the world's clock for skipping ahead while players sleep, putting off the food, rest and cooldown of
		/// the vikings this client owns by how far it skipped - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (ZNet.instance == null || EnvMan.instance == null)
			{
				lastWorld = -1.0;
				return;
			}
			double world = ZNet.instance.GetTimeSeconds();
			float real = Time.time;
			if (lastWorld >= 0.0 && Sleeping())
			{
				double ahead = (world - lastWorld) - (real - lastReal);
				if (ahead > 0.0)
				{
					Postpone(NetTime.Ticks((float)ahead));
				}
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
		/// Puts off the food, rest and forsaken power cooldown of every living viking this client owns
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
