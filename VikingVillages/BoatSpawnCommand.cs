using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The kri_vv_spawnboat console command, for testing where the vikings sit on their boats - spawning one of the boats
	/// they build on the water in front of the player, moored, with living vikings in its seats, a full crew or as many
	/// as asked for (see BoatSeats)
	/// </summary>
	/// <remarks>
	/// The boat never sets off, and its vikings sit in their seats doing nothing else until it's broken - then they swim
	/// ashore and go their way like any viking with no village. It drops nothing when it breaks, like any of the vikings'
	/// boats
	/// </remarks>
	internal static class BoatSpawnCommand
	{
		/// <summary>
		/// The command's name
		/// </summary>
		private const string Command = "kri_vv_spawnboat";

		/// <summary>
		/// How the water in front of the player is looked for - the nearest and farthest out, in what steps, how deep it
		/// has to be, and how far past that the boat goes, in meters
		/// </summary>
		private const float WaterNearest = 6f;
		private const float WaterFarthest = 80f;
		private const float WaterStep = 2f;
		private const float WaterDepth = 2f;
		private const float WaterOut = 5f;

		/// <summary>
		/// Adds the command to the console
		/// </summary>
		public static void Register()
		{
			new Terminal.ConsoleCommand(Command, "<raft|karve|longship|drakkar> [vikings] - spawns a vikings' boat moored on the water in front of you, with a full crew or as many vikings as given sitting in its seats until it's broken", Run, isCheat: true, optionsFetcher: () => new List<string>(TravelTime.BoatNames()));
		}

		/// <summary>
		/// Runs the command
		/// </summary>
		/// <param name="args">The command line</param>
		private static void Run(Terminal.ConsoleEventArgs args)
		{
			Player player = Player.m_localPlayer;
			if (player == null || ZNetScene.instance == null || ZoneSystem.instance == null)
			{
				args.Context.AddString("Load a world first");
				return;
			}
			int biome = ((args.Length > 1) ? TravelTime.BiomeOfBoat(args[1]) : -1);
			if (biome < 0)
			{
				args.Context.AddString("Usage: " + Command + " <" + string.Join("|", TravelTime.BoatNames()) + "> [vikings]");
				return;
			}
			int crew = TravelTime.BoatCrew(biome);
			int count = crew;
			if (args.Length > 2 && int.TryParse(args[2], out int asked))
			{
				count = Mathf.Clamp(asked, 1, crew);
			}
			Vector3 facing = player.transform.forward;
			facing.y = 0f;
			facing = ((facing.sqrMagnitude > 0.01f) ? facing.normalized : Vector3.forward);
			if (!FindWater(player.transform.position, facing, out Vector3 spot))
			{
				args.Context.AddString("Face open water within " + WaterFarthest.ToString("F0") + " meters to spawn a boat on it");
				return;
			}
			GameObject boatPrefab = ZNetScene.instance.GetPrefab(TravelTime.BoatPrefab(biome));
			GameObject vikingPrefab = ZNetScene.instance.GetPrefab(VikingCore.LivingPrefabHash);
			if (boatPrefab == null || vikingPrefab == null)
			{
				args.Context.AddString("Couldn't find the boat's or the viking's prefab");
				return;
			}
			string group = "moored" + NetTime.Now();
			VikingBoat boat = VikingBoat.Build(boatPrefab, spot, facing, group, 0, new List<string>(), new TravelRoad(), spot, 0, 0);
			if (boat == null)
			{
				args.Context.AddString("Couldn't build the boat");
				return;
			}
			boat.Moor();
			List<string> ids = new List<string>();
			for (int i = 0; i < count; i++)
			{
				Transform seat = boat.Seat(i);
				GameObject instance = Object.Instantiate(vikingPrefab, seat.position, seat.rotation);
				ZNetView view = instance.GetComponent<ZNetView>();
				string id = VikingIdentity.Ensure(view);
				if (id.Length == 0)
				{
					continue;
				}
				ZDO zdo = view.GetZDO();
				zdo.Set(VillageTravel.ConfusedKey, false);
				zdo.Set(VillageTravel.GroupKey, group);
				zdo.Set(VikingBoat.BoatKey, boat.Key);
				zdo.Set(VikingBoat.BoardKey, VikingBoat.Aboard);
				ids.Add(id);
			}
			boat.SetCrew(ids);
			string message = "Spawned a " + TravelTime.BoatName(biome) + " moored at " + spot.ToString("F0") + " with " + ids.Count + " of its " + crew + " seats taken, out of " + boat.SeatCount + " laid out";
			Plugin.Log.LogInfo("Boat: " + message);
			args.Context.AddString(message);
		}

		/// <summary>
		/// Finds deep enough water in front of the player - the first spot out from them where it is, a little past it, on
		/// the water
		/// </summary>
		/// <param name="from">Where the player stands</param>
		/// <param name="facing">Which way they face, flat</param>
		/// <param name="spot">Set to the spot</param>
		/// <returns>False when there's none within WaterFarthest</returns>
		private static bool FindWater(Vector3 from, Vector3 facing, out Vector3 spot)
		{
			float water = ZoneSystem.instance.m_waterLevel;
			for (float out1 = WaterNearest; out1 <= WaterFarthest; out1 += WaterStep)
			{
				Vector3 point = from + facing * out1;
				if (ZoneSystem.instance.GetGroundHeight(point) < water - WaterDepth)
				{
					spot = point + facing * WaterOut;
					spot.y = water;
					return(true);
				}
			}
			spot = Vector3.zero;
			return(false);
		}
	}
}
