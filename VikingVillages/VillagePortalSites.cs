using System.Collections.Generic;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Finds the village an Ashlands or Deep North village's portal leads to, on the server - a farm in the Plains or an
	/// easier biome no one has found yet, its zone never generated; or with none left, a new farm in a zone no one has
	/// been to; or with nowhere left like that, a new farm on ground already generated, with nothing players built and
	/// no other location near it - and generates the farm at once, names its village and puts its portal up at the
	/// farm's middle, waiting there for its paths to be laid (see VillagePortals)
	/// </summary>
	/// <remarks>
	/// The portal goes up as soon as the farm is picked, so a farm is never picked twice, and a server that stops
	/// before it's done finds the portal and carries on when it's asked again. A new farm goes where the game would put
	/// one (see FitsFarm), but in any of the biomes a portal leads to. A zone no one has been to is generated the way
	/// the game generates the zones just past where players are, without being shown, and a farm on ground already
	/// generated is placed the same way
	/// </remarks>
	internal static class VillagePortalSites
	{
		/// <summary>
		/// How many spots are tried for a new farm, in zones no one has been to and then on ground already generated
		/// </summary>
		private const int SiteTries = 2000;

		/// <summary>
		/// How far from the world's middle a new farm can go, in meters - short of the world's edge
		/// </summary>
		private const float WorldReach = WorldGenerator.worldSize - 500f;

		/// <summary>
		/// How far from a new farm on ground already generated nothing players built, no portal or village and no other
		/// location can be, in meters
		/// </summary>
		private const float ClearReach = 150f;

		/// <summary>
		/// Seconds the server works on a question before giving up on generating the farm, and the portal with it
		/// </summary>
		private const float GiveUpTime = 300f;

		/// <summary>
		/// Works on a client's question about an Ashlands or Deep North village's portal - finding the village it leads
		/// to and putting its portal up the first time, then generating its farm and naming it
		/// </summary>
		/// <param name="request">The question</param>
		/// <param name="answer">What was found - nothing, when the portal leads nowhere</param>
		/// <returns>False while the farm is still being generated or its village named</returns>
		internal static bool Advance(VillagePortals.Request request, out VillagePortals.Answer answer)
		{
			answer = new VillagePortals.Answer();
			ZDO link = FindLink(request.Farm);
			if (link == null)
			{
				if (!Pick(out Vector3 destination, out string how))
				{
					Plugin.Log.LogInfo("Found no village for the portal of the village at " + request.Farm.ToString("F0") + " to lead to, so it has none");
					return(true);
				}
				link = PutUp(destination, request);
				if (link == null)
				{
					return(true);
				}
				Plugin.Log.LogInfo("The portal " + link.GetString(ZDOVars.s_tag) + " of the village at " + request.Farm.ToString("F0") + " leads to " + how + " at " + destination.ToString("F0"));
			}
			Vector3 farm = link.GetVec3(VillagePortals.VillageKey, VillagePortals.Nowhere);
			ZDO generator = VillageRespawn.FindGenerator(farm);
			if (generator == null)
			{
				if (Time.time - request.Since > GiveUpTime || !Generate(farm))
				{
					Plugin.Log.LogWarning("Couldn't generate the farm at " + farm.ToString("F0") + " for the portal of the village at " + request.Farm.ToString("F0") + " to lead to, so it has none");
					VillagePortals.TakeDown(link);
					return(true);
				}
				generator = VillageRespawn.FindGenerator(farm);
				if (generator == null)
				{
					return(false);
				}
			}
			if (!Name(generator, farm, request.Name, out string name))
			{
				return(false);
			}
			answer.Found = true;
			answer.Tag = link.GetString(ZDOVars.s_tag);
			answer.Destination = farm;
			answer.DestinationName = name;
			return(true);
		}

		/// <summary>
		/// Finds the portal of the village an Ashlands or Deep North village's portal leads to - the one whose other side
		/// is that village
		/// </summary>
		/// <param name="farm">Where the Ashlands or Deep North village's farm stands</param>
		/// <returns>The portal's ZDO, or null when none has been put up for it</returns>
		private static ZDO FindLink(Vector3 farm)
		{
			foreach (ZDO zdo in ZDOMan.instance.GetPortalList())
			{
				if (Utils.DistanceXZ(zdo.GetVec3(VillagePortals.OtherKey, VillagePortals.Nowhere), farm) <= VillagePortals.FarmMatch)
				{
					return(zdo);
				}
			}
			return(null);
		}

		/// <summary>
		/// Lists where the farms with a village's portal standing in them are
		/// </summary>
		/// <returns>The farms</returns>
		private static List<Vector3> PortalFarms()
		{
			List<Vector3> farms = new List<Vector3>();
			foreach (ZDO zdo in ZDOMan.instance.GetPortalList())
			{
				Vector3 farm = zdo.GetVec3(VillagePortals.VillageKey, VillagePortals.Nowhere);
				if (farm != VillagePortals.Nowhere)
				{
					farms.Add(farm);
				}
			}
			return(farms);
		}

		/// <summary>
		/// Picks where the village a portal leads to stands - a farm no one has found yet, or else a new one in a zone
		/// no one has been to, added to the world's plan of its locations, or else a spot for a new one on ground already
		/// generated, placed as it's generated (see Generate)
		/// </summary>
		/// <param name="destination">Where the farm stands</param>
		/// <param name="how">Which of those it is, for the log</param>
		/// <returns>False when there's nowhere</returns>
		private static bool Pick(out Vector3 destination, out string how)
		{
			how = "";
			if (PickUndiscovered(out destination))
			{
				how = "a farm no one has found yet";
				return(true);
			}
			ZoneSystem.ZoneLocation location = RandomFarm();
			if (location == null)
			{
				return(false);
			}
			if (PickSite(location, false, out destination))
			{
				ZoneSystem.instance.RegisterLocation(location, destination, false);
				how = "a new farm where no one has been";
				return(true);
			}
			if (PickSite(location, true, out destination))
			{
				how = "a new farm on open ground";
				return(true);
			}
			return(false);
		}

		/// <summary>
		/// Picks one of the world's planned farms in the Plains or an easier biome at random, whose zone has never been
		/// generated and with no portal leading to it already
		/// </summary>
		/// <param name="farm">The farm's planned spot</param>
		/// <returns>False when there's none</returns>
		private static bool PickUndiscovered(out Vector3 farm)
		{
			farm = Vector3.zero;
			List<Vector3> portals = PortalFarms();
			List<Vector3> found = new List<Vector3>();
			foreach (ZoneSystem.LocationInstance instance in ZoneSystem.instance.m_locationInstances.Values)
			{
				Vector3 spot = instance.m_position;
				if (instance.m_placed || !FarmSpawns.IsFarm(instance.m_location) || ZoneSystem.instance.IsZoneGenerated(ZoneSystem.GetZone(spot)) || !VillagePortals.IsDestination(WorldGenerator.instance.GetBiome(spot)))
				{
					continue;
				}
				if (!portals.Exists((Vector3 portal) => Utils.DistanceXZ(portal, spot) <= VillagePortals.FarmMatch))
				{
					found.Add(spot);
				}
			}
			if (found.Count == 0)
			{
				return(false);
			}
			farm = found[Random.Range(0, found.Count)];
			return(true);
		}

		/// <summary>
		/// Picks a spot for a new farm at random across the world, in a zone with no location planned - one never
		/// generated, or one already generated with nothing near it (see IsClear)
		/// </summary>
		/// <param name="location">The farm's location</param>
		/// <param name="generated">True for a zone already generated</param>
		/// <param name="spot">The spot, on the ground</param>
		/// <returns>False when none of SiteTries spots will do</returns>
		private static bool PickSite(ZoneSystem.ZoneLocation location, bool generated, out Vector3 spot)
		{
			float radius = Mathf.Max(location.m_exteriorRadius, location.m_interiorRadius);
			for (int i = 0; i < SiteTries; i++)
			{
				Vector2 point = Random.insideUnitCircle * WorldReach;
				Vector2s zone = ZoneSystem.GetZone(new Vector3(point.x, 0f, point.y));
				if (ZoneSystem.instance.IsZoneGenerated(zone) != generated || ZoneSystem.instance.m_locationInstances.ContainsKey(zone))
				{
					continue;
				}
				spot = ZoneSystem.GetRandomPointInZone(zone, radius);
				if (FitsFarm(location, ref spot) && (!generated || IsClear(spot)))
				{
					return(true);
				}
			}
			spot = Vector3.zero;
			return(false);
		}

		/// <summary>
		/// Checks whether a farm can stand at a spot, the way ZoneSystem.GenerateLocationsTimeSliced checks a spot for a
		/// location - its biome area, height above the water, how much the ground rises and falls across it and how far
		/// it is from the farms near it - in the Plains or an easier biome rather than the farm's own
		/// </summary>
		/// <param name="location">The farm's location</param>
		/// <param name="spot">The spot, put at the ground's height</param>
		/// <returns>True when it can</returns>
		private static bool FitsFarm(ZoneSystem.ZoneLocation location, ref Vector3 spot)
		{
			if (!VillagePortals.IsDestination(WorldGenerator.instance.GetBiome(spot)) || (location.m_biomeArea & WorldGenerator.instance.GetBiomeArea(spot)) == 0)
			{
				return(false);
			}
			spot.y = WorldGenerator.instance.GetHeight(spot.x, spot.z);
			float altitude = spot.y - ZoneSystem.instance.m_waterLevel;
			if (altitude < location.m_minAltitude || altitude > location.m_maxAltitude)
			{
				return(false);
			}
			WorldGenerator.instance.GetTerrainDelta(spot, location.m_exteriorRadius, out float delta, out Vector3 _);
			if (delta > location.m_maxTerrainDelta || delta < location.m_minTerrainDelta)
			{
				return(false);
			}
			return(location.m_minDistanceFromSimilar <= 0f || !ZoneSystem.instance.HaveLocationInRange(location.m_prefab.m_assetID, location.m_group, spot, location.m_minDistanceFromSimilar));
		}

		/// <summary>
		/// Checks whether there's nothing near a spot on ground already generated - nothing players built, no portal,
		/// farm or village a player started, and no other location - within ClearReach
		/// </summary>
		/// <param name="spot">The spot</param>
		/// <returns>True when there's nothing</returns>
		private static bool IsClear(Vector3 spot)
		{
			Vector2s center = ZoneSystem.GetZone(spot);
			int zones = Mathf.CeilToInt(ClearReach / ZoneSystem.c_ZoneSize);
			for (int x = -zones; x <= zones; x++)
			{
				for (int y = -zones; y <= zones; y++)
				{
					if (ZoneSystem.instance.m_locationInstances.TryGetValue(new Vector2s(center.x + x, center.y + y), out ZoneSystem.LocationInstance instance) && Utils.DistanceXZ(instance.m_position, spot) <= ClearReach)
					{
						return(false);
					}
				}
			}
			List<ZDO> zdos = new List<ZDO>();
			VillageRespawn.ZdosNear(spot, ClearReach, zdos);
			foreach (ZDO zdo in zdos)
			{
				if (Utils.DistanceXZ(zdo.GetPosition(), spot) > ClearReach)
				{
					continue;
				}
				int prefab = zdo.GetPrefab();
				if (zdo.GetLong(ZDOVars.s_creator, 0L) != 0L || Game.instance.PortalPrefabHash.Contains(prefab) || prefab == VillagePaths.GeneratorHash || VillageFounding.IsAnchor(prefab))
				{
					return(false);
				}
			}
			return(true);
		}

		/// <summary>
		/// Picks one of the game's farm locations at random
		/// </summary>
		/// <returns>The location, or null when the game has none</returns>
		private static ZoneSystem.ZoneLocation RandomFarm()
		{
			List<ZoneSystem.ZoneLocation> farms = VillageSpawnCommand.FarmLocations();
			return((farms.Count > 0) ? farms[Random.Range(0, farms.Count)] : null);
		}

		/// <summary>
		/// Puts up the portal of the village a portal leads to at its farm's spot, waiting for its paths to be laid - a
		/// wood portal named after the Ashlands or Deep North village, or with a number after the name when another
		/// portal has it - saved without being shown, the way a zone's objects are as it's generated out of sight
		/// </summary>
		/// <param name="farm">The farm's spot</param>
		/// <param name="request">The question about the Ashlands or Deep North village</param>
		/// <returns>The portal's ZDO, or null when it couldn't be put up</returns>
		private static ZDO PutUp(Vector3 farm, VillagePortals.Request request)
		{
			GameObject prefab = ZNetScene.instance.GetPrefab(VillagePortals.WoodPortal);
			if (prefab == null)
			{
				Plugin.Log.LogWarning("Couldn't find the " + VillagePortals.WoodPortal + " prefab to put up a portal at " + farm.ToString("F0"));
				return(null);
			}
			List<string> tags = new List<string>();
			foreach (ZDO portal in ZDOMan.instance.GetPortalList())
			{
				tags.Add(portal.GetString(ZDOVars.s_tag));
			}
			Vector3 at = farm;
			at.y = (ZoneSystem.instance.IsZoneLoaded(farm) ? ZoneSystem.instance.GetGroundHeight(farm) : WorldGenerator.instance.GetHeight(farm.x, farm.z)) - VillagePortals.PortalSink;
			GameObject instance;
			ZNetView.StartGhostInit();
			try
			{
				instance = Object.Instantiate(prefab, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
			}
			finally
			{
				ZNetView.FinishGhostInit();
			}
			ZNetView view = instance.GetComponent<ZNetView>();
			ZDO zdo = ((view != null) ? view.GetZDO() : null);
			if (zdo != null)
			{
				zdo.Set(ZDOVars.s_tag, VillagePortals.UniqueTag(request.Name, tags));
				zdo.Set(VillagePortals.VillageKey, farm);
				zdo.Set(VillagePortals.OtherKey, request.Farm);
				zdo.Set(VillagePortals.WaitingKey, true);
				zdo.Set(VillagePortals.SignTextKey, request.Name);
			}
			Object.Destroy(instance);
			return(zdo);
		}

		/// <summary>
		/// Generates the farm a portal leads to - its zone, when it's never been generated, with the farm planned there,
		/// or else the farm itself, placed on the ground already generated. Either waits until the zone's ground has been
		/// worked out and the farm is ready to place, so it's called again until the farm's generator turns up
		/// </summary>
		/// <param name="farm">The farm's spot</param>
		/// <returns>False when it can't be generated - its zone was generated with a location planned there that left no
		/// generator, or the game has no farms</returns>
		private static bool Generate(Vector3 farm)
		{
			Vector2s zone = ZoneSystem.GetZone(farm);
			if (!ZoneSystem.instance.IsZoneGenerated(zone))
			{
				// Planned when the farm was picked, unless the server stopped before the world was saved
				if (!ZoneSystem.instance.m_locationInstances.ContainsKey(zone))
				{
					ZoneSystem.ZoneLocation planned = RandomFarm();
					if (planned == null)
					{
						return(false);
					}
					ZoneSystem.instance.RegisterLocation(planned, farm, false);
				}
				ZoneSystem.instance.SpawnZone(zone, ZoneSystem.SpawnMode.Ghost, out GameObject _);
				return(true);
			}
			if (ZoneSystem.instance.m_locationInstances.ContainsKey(zone))
			{
				return(false);
			}
			ZoneSystem.ZoneLocation location = RandomFarm();
			if (location == null)
			{
				return(false);
			}
			Quaternion rotation = Quaternion.Euler(0f, Random.Range(0, 16) * 22.5f, 0f);
			int seed = Random.Range(0, 99999);
			List<GameObject> ghosts = new List<GameObject>();
			if (ZoneSystem.instance.IsZoneLoaded(zone))
			{
				farm.y = ZoneSystem.instance.GetGroundHeight(farm);
				ZoneSystem.instance.SpawnLocation(location, seed, farm, rotation, ZoneSystem.SpawnMode.Full, ghosts);
			}
			else
			{
				Heightmap map = ZoneSystem.instance.m_zonePrefab.GetComponentInChildren<Heightmap>();
				Vector3 zonePos = ZoneSystem.GetZonePos(zone);
				if (!HeightmapBuilder.instance.IsTerrainReady(zonePos, map.m_width, map.m_scale, map.IsDistantLod, WorldGenerator.instance) || !ZoneSystem.instance.PokeCanSpawnLocation(location, true))
				{
					return(true);
				}
				// The zone's ground, for the farm to be placed on as it is when a zone is generated, taken away after
				GameObject root = Object.Instantiate(ZoneSystem.instance.m_zonePrefab, zonePos, Quaternion.identity);
				try
				{
					farm.y = ZoneSystem.instance.GetGroundHeight(farm);
					ZoneSystem.instance.SpawnLocation(location, seed, farm, rotation, ZoneSystem.SpawnMode.Ghost, ghosts);
				}
				finally
				{
					foreach (GameObject ghost in ghosts)
					{
						Object.Destroy(ghost);
					}
					Object.Destroy(root);
				}
			}
			ZoneSystem.instance.RegisterLocation(location, farm, true);
			Plugin.Log.LogInfo("Placed the farm " + location.m_prefabName + " at " + farm.ToString("F0") + " on ground already generated, for a portal to lead to");
			return(true);
		}

		/// <summary>
		/// Names the village a portal leads to on the server, unless it's named already, the way its own client would
		/// name it (see VillageNames.TryAutoName) - leaving out the name of the Ashlands or Deep North village, which the
		/// server may not have heard yet. A village a client is running is left for that client to name
		/// </summary>
		/// <param name="generator">The village's farm's generator</param>
		/// <param name="farm">The farm's spot</param>
		/// <param name="other">The Ashlands or Deep North village's name</param>
		/// <param name="name">The village's name, or "" when it has none</param>
		/// <returns>False while it waits for the names to be known, or for the client running it to name it</returns>
		private static bool Name(ZDO generator, Vector3 farm, string other, out string name)
		{
			if (VillageNames.At(generator, out name, out string _) || VillageNames.EverNamed(generator))
			{
				return(true);
			}
			if (!VillagePlannedNames.Ready || !VillageNameList.Ready)
			{
				return(false);
			}
			if (!generator.IsOwner())
			{
				long owner = generator.GetOwner();
				if (owner != 0L && ZNet.instance.GetPeer(owner) != null)
				{
					return(false);
				}
				generator.SetOwner(ZDOMan.GetSessionID());
			}
			HashSet<string> taken = VillageNames.TakenNames(generator);
			taken.Add(other.ToLowerInvariant());
			name = (VillageNames.PickName(farm, taken) ?? "");
			if (name.Length > 0)
			{
				VillageNames.Give(generator, farm, name);
			}
			return(true);
		}
	}
}
