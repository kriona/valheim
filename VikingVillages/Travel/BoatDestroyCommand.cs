using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The kri_vv_destroyboats console command, for clearing boats away while testing - taking the vikings' boats near
	/// the player out of the world, or every ship near them, dropping nothing
	/// </summary>
	/// <remarks>
	/// Vikings on board a boat it takes away end up in the water, and go on as they would from any boat gone from under
	/// them - climbing onto the nearest boat, or swimming for land (see VillageLife.SwimAshore)
	/// </remarks>
	internal static class BoatDestroyCommand
	{
		/// <summary>
		/// The command's name, the word for every ship rather than only the vikings' boats, how near the player boats are
		/// taken away by default, in meters, and the farthest that can be asked for
		/// </summary>
		private const string Command = "kri_vv_destroyboats";
		private const string AllWord = "all";
		private const float DefaultReach = 50f;
		private const float MostReach = 1000f;

		/// <summary>
		/// Adds the command to the console
		/// </summary>
		public static void Register()
		{
			new Terminal.ConsoleCommand(Command, "[meters] [" + AllWord + "] - takes away the vikings' boats within " + DefaultReach.ToString("F0") + " meters of you, or as far as given, dropping nothing - those on the road, on quests and spawned with kri_vv_spawnboat. " + AllWord + " takes away every ship there, players' and the villages' own too, but the one you're on. Vikings on board end up in the water", Run, isCheat: true, optionsFetcher: () => new List<string> { AllWord });
		}

		/// <summary>
		/// Runs the command
		/// </summary>
		/// <param name="args">The command line</param>
		private static void Run(Terminal.ConsoleEventArgs args)
		{
			Player player = Player.m_localPlayer;
			if (player == null)
			{
				args.Context.AddString("Load into a world first");
				return;
			}
			bool all = false;
			float reach = DefaultReach;
			for (int i = 1; i < args.Length; i++)
			{
				if (string.Equals(args[i], AllWord, System.StringComparison.OrdinalIgnoreCase))
				{
					all = true;
				}
				else if (float.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float meters) && meters > 0f)
				{
					reach = Mathf.Min(meters, MostReach);
				}
				else
				{
					args.Context.AddString("Give a distance in meters, " + AllWord + ", or nothing");
					return;
				}
			}
			Ship standing = player.GetStandingOnShip();
			int taken = 0;
			foreach (Ship ship in new List<Ship>(VikingBoat.Ships()))
			{
				if (ship == null || ship == standing || Vector3.Distance(ship.transform.position, player.transform.position) > reach)
				{
					continue;
				}
				if (!all && VikingBoat.Of(ship) == null)
				{
					continue;
				}
				ZNetView view = ship.GetComponent<ZNetView>();
				if (view == null || !view.IsValid())
				{
					continue;
				}
				view.ClaimOwnership();
				view.Destroy();
				taken++;
			}
			string message = "Took away " + taken + " " + (all ? "ships" : "vikings' boats") + " within " + reach.ToString("F0") + " meters";
			Plugin.Log.LogInfo("Boat: " + message);
			args.Context.AddString(message);
		}
	}
}
