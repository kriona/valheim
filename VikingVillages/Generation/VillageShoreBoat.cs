using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The boat moored by a generated village whose edge comes near water that runs on to the sea - the best boat of
	/// the village's biome, as its vikings would build (see TravelTime.BoatPrefab), or a drakkar on the Ashlands' boiling
	/// sea - put there once, the first time the village is set up, where VillageBoatSite finds room for it
	/// </summary>
	/// <remarks>
	/// It's a ship like any other, no player's and not one of the village's pieces, so the vikings neither sail it nor
	/// build it back, and it drops what it's built from when it breaks - a third of it, as the game drops from any piece
	/// no player built (see Piece.DropResources). The ground is read from the loaded terrain where it's loaded, so the
	/// ground a village raised out of the water counts, and from the world's plan past that. Its spot can be past the
	/// village's edge, so once the zone the spot is in has loaded, the spot is looked for again with that zone's terrain,
	/// and the boat is put there when its zone is loaded too
	/// </remarks>
	internal static class VillageShoreBoat
	{
		/// <summary>
		/// On a generated village's generator, how far its boat has got (see State), and the spot last found for it
		/// </summary>
		private static readonly int StateKey = "kriona.VikingVillages.villageBoatState".GetStableHashCode();
		private static readonly int SpotKey = "kriona.VikingVillages.villageBoatSpot".GetStableHashCode();

		/// <summary>
		/// How far a village's boat has got - not looked for yet, a spot found and waiting for its zone to load to be looked
		/// for again, or done with, put there or with nowhere to go
		/// </summary>
		private enum State
		{
			Unlooked,
			Waiting,
			Done
		}

		/// <summary>
		/// How much more room than its own size a boat is given all around, and how far around its spot it's given when its
		/// size can't be read, in meters
		/// </summary>
		private const float RoomMargin = 2f;
		private const float FallbackRoom = 12f;

		/// <summary>
		/// How much deeper than its hull reaches below its middle the water under a boat has to be, for the waves, and how
		/// deep it has to be when its hull can't be read, in meters
		/// </summary>
		private const float DraftMargin = 2f;
		private const float FallbackDraft = 3f;

		/// <summary>
		/// How high above the sea's surface something has to stand to be in the boat's way, in meters
		/// </summary>
		private const float ClearHeight = 3f;

		private static int clearMask;

		/// <summary>
		/// Looks for a generated village's boat's spot the first time it's called for the village, and again once that
		/// spot's zone has loaded, putting the boat there when the spot found then has loaded too
		/// </summary>
		/// <param name="village">The village, whose generator this client owns, set up and with its pieces noted</param>
		public static void TryPlace(Village village)
		{
			if (village.IsFounded() || ZNetScene.instance == null || ZoneSystem.instance == null || WorldGenerator.instance == null)
			{
				return;
			}
			ZDO zdo = village.Zdo;
			State state = (State)zdo.GetInt(StateKey);
			if (state == State.Done || (state == State.Waiting && !ZoneSystem.instance.IsZoneLoaded(zdo.GetVec3(SpotKey, Vector3.zero))))
			{
				return;
			}
			// Noted done first, so a village with nowhere for a boat isn't looked around again and again
			zdo.Set(StateKey, (int)State.Done);
			if (!FindSpot(village, out Vector3 spot, out Vector3 facing, out string prefab))
			{
				return;
			}
			if (!ZoneSystem.instance.IsZoneLoaded(spot))
			{
				zdo.Set(SpotKey, spot);
				zdo.Set(StateKey, (int)State.Waiting);
				return;
			}
			Build(village, prefab, spot, facing);
		}

		/// <summary>
		/// Moors a boat by a village the way its own boat is moored (see VillageBoatSite) - a quest's boat its vikings
		/// came home in while no player was near to see them sail in (see VillageQuests.SailHome)
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="prefab">The boat's prefab</param>
		public static void MoorAnother(Village village, string prefab)
		{
			if (ZNetScene.instance == null || ZoneSystem.instance == null || WorldGenerator.instance == null)
			{
				return;
			}
			if (!Find(village, prefab, out Vector3 spot, out Vector3 facing))
			{
				Plugin.Log.LogInfo("The village at " + village.Farm.ToString("F0") + " has no water by it with room to moor a " + prefab);
				return;
			}
			Build(village, prefab, spot, facing);
		}

		/// <summary>
		/// Finds where a village's boat goes and which boat it is - the best of the village's biome, or a drakkar when the
		/// spot found for that is on the Ashlands' boiling sea, which would burn any other
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="spot">Set to where the boat goes, on the water</param>
		/// <param name="facing">Set to which way it faces</param>
		/// <param name="prefab">Set to the boat's prefab</param>
		/// <returns>False when there's no water by the village that runs on to the sea with room for the boat</returns>
		private static bool FindSpot(Village village, out Vector3 spot, out Vector3 facing, out string prefab)
		{
			int biome = VillageCooking.VillageBiome(village);
			prefab = TravelTime.BoatPrefab(biome);
			if (!Find(village, prefab, out spot, out facing))
			{
				Plugin.Log.LogInfo("The village at " + village.Farm.ToString("F0") + " has no water by it running on to the sea with room for a " + TravelTime.BoatName(biome));
				return(false);
			}
			if (WorldGenerator.GetAshlandsOceanGradient(spot) > 0f && !TravelTime.BoatSurvivesAshlands(biome))
			{
				biome = TravelTime.BiomeOfBoat("drakkar");
				prefab = TravelTime.BoatPrefab(biome);
				if (!Find(village, prefab, out spot, out facing))
				{
					Plugin.Log.LogInfo("The village at " + village.Farm.ToString("F0") + " has no water by it running on to the sea with room for a drakkar");
					return(false);
				}
			}
			return(true);
		}

		/// <summary>
		/// Finds where a boat goes by a village (see VillageBoatSite)
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="prefab">The boat's prefab</param>
		/// <param name="spot">Set to where it goes, on the water</param>
		/// <param name="facing">Set to which way it faces</param>
		/// <returns>False when there's nowhere</returns>
		private static bool Find(Village village, string prefab, out Vector3 spot, out Vector3 facing)
		{
			return(VillageBoatSite.Find(village.Middle, village.Radius, Room(prefab), Draft(prefab), ZoneSystem.instance.m_waterLevel, village.DistanceOutside, GroundHeight, (float x, float z) => TravelPlanner.Sample(x, z).Sea, out spot, out facing));
		}

		/// <summary>
		/// Gets the ground's height at a point - the loaded terrain's, with whatever was dug or raised there, or the world's
		/// plan's where it isn't loaded
		/// </summary>
		/// <param name="x">The point's x</param>
		/// <param name="z">The point's z</param>
		/// <returns>The height</returns>
		private static float GroundHeight(float x, float z)
		{
			if (Heightmap.GetHeight(new Vector3(x, 0f, z), out float height))
			{
				return(height);
			}
			return(WorldGenerator.instance.GetHeight(x, z));
		}

		/// <summary>
		/// Works out how far around a boat's spot there has to be deep water - from its middle to its farthest corner, and
		/// RoomMargin more
		/// </summary>
		/// <param name="prefab">The boat's prefab</param>
		/// <returns>How far, in meters</returns>
		private static float Room(string prefab)
		{
			if (!VillageFurnishing.Footprint(prefab, out Bounds size))
			{
				return(FallbackRoom);
			}
			float x = Mathf.Abs(size.center.x) + size.extents.x;
			float z = Mathf.Abs(size.center.z) + size.extents.z;
			return(Mathf.Sqrt(x * x + z * z) + RoomMargin);
		}

		/// <summary>
		/// Works out how deep the water under a boat has to be - as far as its hull reaches below its middle, which it's
		/// put on the water at, and DraftMargin more
		/// </summary>
		/// <param name="prefab">The boat's prefab</param>
		/// <returns>How deep, in meters</returns>
		private static float Draft(string prefab)
		{
			if (!VillageFurnishing.Footprint(prefab, out Bounds size))
			{
				return(FallbackDraft);
			}
			return(Mathf.Max(0f, -size.min.y) + DraftMargin);
		}

		/// <summary>
		/// Puts a village's boat on the water, unless something stands in its way there, like a player's ship
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="prefabName">The boat's prefab</param>
		/// <param name="spot">Where it goes, on the water</param>
		/// <param name="facing">Which way it faces</param>
		private static void Build(Village village, string prefabName, Vector3 spot, Vector3 facing)
		{
			GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
			if (prefab == null || prefab.GetComponent<Ship>() == null)
			{
				return;
			}
			facing.y = 0f;
			Quaternion rotation = ((facing.sqrMagnitude > 0.01f) ? Quaternion.LookRotation(facing) : Quaternion.identity);
			if (clearMask == 0)
			{
				clearMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "vehicle");
			}
			float room = Room(prefabName);
			Vector3 center = spot + Vector3.up * (ClearHeight / 2f);
			if (Physics.CheckBox(center, new Vector3(room, ClearHeight / 2f, room), rotation, clearMask, QueryTriggerInteraction.Ignore))
			{
				Plugin.Log.LogInfo("Something stands where the boat of the village at " + village.Farm.ToString("F0") + " would go, at " + spot.ToString("F0"));
				return;
			}
			GameObject instance = Object.Instantiate(prefab, spot, rotation);
			WearNTear wear = instance.GetComponent<WearNTear>();
			if (wear != null)
			{
				wear.OnPlaced();
			}
			Plugin.Log.LogInfo("Moored a " + prefabName + " at " + spot.ToString("F0") + " by the village at " + village.Farm.ToString("F0"));
		}
	}
}
