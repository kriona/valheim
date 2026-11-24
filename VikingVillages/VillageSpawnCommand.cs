using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The kri_vv_spawnvillage console command, which spawns an abandoned farm centered where the player stands, for its
	/// village to be built and set up the way one found in the world is - leaving the player where they are as it goes
	/// up, and moving them to the open ground nearest them only when they're caught in it: inside something built, by
	/// the bonfire's flames, under the farm's ground or fenced in a pen
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
		/// How long the player is watched after the village's pieces are all placed and noted, so the last of them are
		/// in the scene, in seconds
		/// </summary>
		private const float WatchAfter = 2f;

		/// <summary>
		/// How long the player is watched at most, for a village that's never set up, in seconds
		/// </summary>
		private const float WatchMax = 300f;

		/// <summary>
		/// How far from the farm the player can go and still be watched, in meters - farther than that, they've left it
		/// </summary>
		private const float WatchReach = 150f;

		/// <summary>
		/// Seconds between looks for the village while it isn't among the loaded ones yet
		/// </summary>
		private const float FindInterval = 0.25f;

		/// <summary>
		/// How the player's body is checked against what's built, in meters - how far above their feet the check starts,
		/// so the ground or floor they stand on doesn't count, and how much narrower than their body it is, so what they
		/// only brush against doesn't either
		/// </summary>
		private const float BodyLift = 0.3f;
		private const float BodyShrink = 0.1f;

		/// <summary>
		/// How far under the ground the player's feet can be before they're moved, in meters - the farm's ground can rise
		/// over them as it's leveled
		/// </summary>
		private const float BuriedDepth = 0.5f;

		/// <summary>
		/// The layers of what's built and placed that the player can be caught inside
		/// </summary>
		private static int bodyMask;

		/// <summary>
		/// A village being set up around the player who spawned it
		/// </summary>
		private class Watch
		{
			/// <summary>
			/// Where the farm was spawned
			/// </summary>
			public Vector3 Farm;

			/// <summary>
			/// When the player stops being watched
			/// </summary>
			public float Until;

			/// <summary>
			/// Whether the village's pieces are all placed and noted
			/// </summary>
			public bool Noted;

			/// <summary>
			/// When to look for the village again while it isn't among the loaded ones
			/// </summary>
			public float NextFind;
		}

		/// <summary>
		/// The village being set up around the player, or null when there's none to watch
		/// </summary>
		private static Watch watch;

		/// <summary>
		/// Adds the command to the console
		/// </summary>
		public static void Register()
		{
			new Terminal.ConsoleCommand(Command, "[farm] - spawns a village where you stand, moving you a little only if it's built on you (single player or host)", Run, isCheat: true, optionsFetcher: Farms);
		}

		/// <summary>
		/// Watches the player who spawned a village while it goes up, and moves them to the open ground nearest them (see
		/// Village.OpenSpotNear) whenever they're caught in it (see Trouble) - until WatchAfter after its pieces are all
		/// placed and noted (see VillageRecords.IsNoted), WatchMax at most, or until they go more than WatchReach from it
		/// - call it from the plugin's Update
		/// </summary>
		public static void Update()
		{
			if (watch == null)
			{
				return;
			}
			Player player = Player.m_localPlayer;
			if (player == null || ZoneSystem.instance == null || Time.time >= watch.Until || Utils.DistanceXZ(player.transform.position, watch.Farm) > WatchReach)
			{
				watch = null;
				return;
			}
			Village village = FindVillage();
			if (village == null)
			{
				return;
			}
			if (!watch.Noted && VillageRecords.IsNoted(village))
			{
				watch.Noted = true;
				watch.Until = Time.time + WatchAfter;
			}
			Vector3 from = player.transform.position;
			string trouble = Trouble(player, from);
			if (trouble == null || player.IsTeleporting())
			{
				return;
			}
			// The navmesh under what was just built isn't rebuilt yet, so each spot is checked for room for the player too.
			// With none found, the navmesh there may still be building, so it's looked for again next frame
			if (!village.OpenSpotNear(from, out Vector3 spot, (Vector3 at) => Trouble(player, at) == null))
			{
				return;
			}
			Nudge(player, spot);
			Plugin.Log.LogInfo("Moved the player " + Utils.DistanceXZ(spot, from).ToString("F1") + "m from " + from.ToString("F1") + " to " + spot.ToString("F1") + " in the village at " + watch.Farm.ToString("F0") + ", as they were " + trouble);
		}

		/// <summary>
		/// Finds the village being watched, looking for the loaded villages again every FindInterval while it isn't among
		/// them yet, since a farm just generated isn't until the next look
		/// </summary>
		/// <returns>The village, or null</returns>
		private static Village FindVillage()
		{
			Village village = Village.ForFarm(watch.Farm);
			if (village == null && Time.time >= watch.NextFind)
			{
				watch.NextFind = Time.time + FindInterval;
				Village.RefreshSoon();
				village = Village.ForFarm(watch.Farm);
			}
			return(village);
		}

		/// <summary>
		/// Checks whether the player would be caught at a spot in the village going up around them - by the flames of
		/// its bonfire, under its ground, inside something built or placed, or fenced in a pen
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="feet">Where their feet are, or would be</param>
		/// <returns>What they're caught in, for the log, or null when they aren't</returns>
		private static string Trouble(Player player, Vector3 feet)
		{
			if (Village.NearFlames(feet))
			{
				return("by the fire");
			}
			if (feet.y < ZoneSystem.instance.GetGroundHeight(feet) - BuriedDepth)
			{
				return("under the ground");
			}
			Collider inside = Inside(player, feet);
			if (inside != null)
			{
				return("inside " + Utils.GetPrefabName(inside.transform.root.gameObject));
			}
			if (Village.FencedIn(feet))
			{
				return("fenced in");
			}
			return(null);
		}

		/// <summary>
		/// Finds something built or placed that the player's body is, or would be, inside at a spot - checked a little
		/// narrower than their body, from a little above their feet, so the floor they stand on and what they brush
		/// against don't count
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="feet">Where their feet are, or would be</param>
		/// <returns>Its collider, or null when there's none</returns>
		private static Collider Inside(Player player, Vector3 feet)
		{
			CapsuleCollider body = player.m_collider;
			if (body == null)
			{
				return(null);
			}
			if (bodyMask == 0)
			{
				bodyMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece");
			}
			float radius = Mathf.Max(0.05f, body.radius - BodyShrink);
			Vector3 bottom = feet + Vector3.up * (BodyLift + radius);
			Vector3 top = feet + Vector3.up * Mathf.Max(BodyLift + radius, body.height - radius);
			Collider[] hits = Physics.OverlapCapsule(bottom, top, radius, bodyMask, QueryTriggerInteraction.Ignore);
			return((hits.Length > 0) ? hits[0] : null);
		}

		/// <summary>
		/// Moves the player a short way at once, keeping the way they face - rather than teleporting them, which fades
		/// the screen and won't go again within 2 seconds of the last teleport (see Player.TeleportTo)
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="spot">Where they go</param>
		private static void Nudge(Player player, Vector3 spot)
		{
			player.transform.position = spot;
			if (player.m_body != null)
			{
				player.m_body.position = spot;
				player.m_body.linearVelocity = Vector3.zero;
			}
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
			if (StartClearance.NearStart(center))
			{
				args.Context.AddString("Too close to the world's start location - move farther away to spawn a village");
				return;
			}
			center.y = ZoneSystem.instance.GetGroundHeight(center);
			Quaternion rotation = Quaternion.Euler(0f, Random.Range(0, 16) * 22.5f, 0f);
			watch = new Watch { Farm = center, Until = Time.time + WatchMax };
			ZoneSystem.instance.SpawnLocation(location, Random.Range(0, 99999), center, rotation, ZoneSystem.SpawnMode.Full, new List<GameObject>(), false);
			Vector2s zone = ZoneSystem.GetZone(center);
			bool planned = !ZoneSystem.instance.m_locationInstances.ContainsKey(zone);
			if (planned)
			{
				ZoneSystem.instance.RegisterLocation(location, center, true);
			}
			string where = center.ToString("F0");
			args.Context.AddString("Spawned " + Name(location) + " at " + where + (planned ? "" : ", not added to the world's plan since its zone already has a location") + " - its village is set up over the next few seconds, and you're moved a little if it's built on you");
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
