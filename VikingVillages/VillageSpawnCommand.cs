using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The kri_vv_spawnvillage console command, which spawns an abandoned farm centered where the player stands, for its
	/// village to be built and set up the way one found in the world is - moving the player out past where its fence can
	/// stand, so they aren't caught in a house or the bonfire as they go up
	/// </summary>
	/// <remarks>
	/// Only the server places locations, so it works in single player and for the host. The farm is added to the world's
	/// plan of its locations too, when its zone has no location planned already, so it counts as one of the world's farms
	/// </remarks>
	internal static class VillageSpawnCommand
	{
		/// <summary>
		/// The command's name
		/// </summary>
		private const string Command = "kri_vv_spawnvillage";

		/// <summary>
		/// The start of the names of the game's farm locations
		/// </summary>
		private const string FarmPrefix = "WoodFarm";

		/// <summary>
		/// How far past the farthest a farm's fence can stand the player is moved, in meters
		/// </summary>
		private const float PlayerClearance = 8f;

		/// <summary>
		/// Adds the command to the console
		/// </summary>
		public static void Register()
		{
			new Terminal.ConsoleCommand(Command, "[farm] - spawns a village where you stand, moving you out past its fence (single player or host)", Run, isCheat: true, optionsFetcher: Farms);
		}

		/// <summary>
		/// Runs the command - spawning the farm named, or one picked at random
		/// </summary>
		/// <param name="args">The command line</param>
		private static void Run(Terminal.ConsoleEventArgs args)
		{
			Player player = Player.m_localPlayer;
			if (player == null || ZoneSystem.instance == null || ZNet.instance == null)
			{
				args.Context.AddString("Load a world first");
				return;
			}
			if (!ZNet.instance.IsServer())
			{
				args.Context.AddString("Only the server can place a village - use it in single player or as the host");
				return;
			}
			List<ZoneSystem.ZoneLocation> farms = FarmLocations();
			if (args.Length > 1)
			{
				farms = farms.FindAll((ZoneSystem.ZoneLocation farm) => string.Equals(Name(farm), args[1], System.StringComparison.OrdinalIgnoreCase));
			}
			if (farms.Count == 0)
			{
				args.Context.AddString("Found no farm location" + ((args.Length > 1) ? " called " + args[1] : "") + " - try one of: " + string.Join(", ", Farms()));
				return;
			}
			ZoneSystem.ZoneLocation location = farms[Random.Range(0, farms.Count)];
			Vector3 center = player.transform.position;
			center.y = ZoneSystem.instance.GetGroundHeight(center);
			// Out past where the fence can stand, behind the player, facing the farm
			Vector3 back = -player.transform.forward;
			back.y = 0f;
			back = ((back.sqrMagnitude > 0.01f) ? back.normalized : Vector3.back);
			Vector3 standing = center + back * (VillageFarmhouses.FenceRadiusMax + PlayerClearance);
			standing.y = ZoneSystem.instance.GetGroundHeight(standing);
			player.TeleportTo(standing, Quaternion.LookRotation(-back), false);
			Quaternion rotation = Quaternion.Euler(0f, Random.Range(0, 16) * 22.5f, 0f);
			ZoneSystem.instance.SpawnLocation(location, Random.Range(0, 99999), center, rotation, ZoneSystem.SpawnMode.Full, new List<GameObject>(), false);
			Vector2s zone = ZoneSystem.GetZone(center);
			bool planned = !ZoneSystem.instance.m_locationInstances.ContainsKey(zone);
			if (planned)
			{
				ZoneSystem.instance.RegisterLocation(location, center, true);
			}
			string where = center.ToString("F0");
			args.Context.AddString("Spawned " + Name(location) + " at " + where + (planned ? "" : ", not added to the world's plan since its zone already has a location") + " - its village is set up over the next few seconds");
			Plugin.Log.LogInfo("Spawned the farm " + Name(location) + " at " + where + " with " + Command + (planned ? ", adding it to the world's plan" : ", leaving it out of the world's plan, whose zone already has a location"));
		}

		/// <summary>
		/// Lists the game's farm locations whose prefab is there to place
		/// </summary>
		/// <returns>The locations</returns>
		internal static List<ZoneSystem.ZoneLocation> FarmLocations()
		{
			List<ZoneSystem.ZoneLocation> farms = new List<ZoneSystem.ZoneLocation>();
			if (ZoneSystem.instance == null)
			{
				return(farms);
			}
			foreach (ZoneSystem.ZoneLocation location in ZoneSystem.instance.m_locations)
			{
				if (location != null && location.m_prefab.IsValid && Name(location).StartsWith(FarmPrefix, System.StringComparison.OrdinalIgnoreCase))
				{
					farms.Add(location);
				}
			}
			return(farms);
		}

		/// <summary>
		/// Lists the names of the game's farm locations, for the console to complete
		/// </summary>
		/// <returns>The names</returns>
		private static List<string> Farms()
		{
			List<string> names = new List<string>();
			foreach (ZoneSystem.ZoneLocation location in FarmLocations())
			{
				if (!names.Contains(Name(location)))
				{
					names.Add(Name(location));
				}
			}
			return(names);
		}

		/// <summary>
		/// Gets a location's prefab name
		/// </summary>
		/// <param name="location">The location</param>
		/// <returns>The name</returns>
		private static string Name(ZoneSystem.ZoneLocation location)
		{
			return(!string.IsNullOrEmpty(location.m_prefabName) ? location.m_prefabName : location.m_prefab.Name);
		}
	}
}
