using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// Furnishes a village's houses once - a bed for each viking that calls the house home, chests, a table with stools
	/// around it, and the village's workbench and forge each in one of its houses - as much as fits without crowding
	/// the house
	/// </summary>
	/// <remarks>
	/// A village far from every player is generated as ghosts, which are saved and removed before anything can measure
	/// them, so it's furnished instead the first time everything across it has loaded, by whoever owns its generator,
	/// which notes it's been furnished. A house is measured from its colliders. Its floor is laid out as a grid of cells
	/// lined up with its walls, and each cell
	/// is free, taken by something already standing there or where there's no floor, kept clear in front of a door, or
	/// taken by a piece placed. A piece is only placed where every piece can still be reached from a door across free
	/// cells, and while at least FreeShareMin of the floor that was free stays free, so a small house gets fewer pieces.
	/// Pieces against a wall stand with their back to it, facing into the house - pushed back to meet the wall when it
	/// stands past the grid's edge - and stools face their table. Each
	/// piece's size comes from its own colliders, so which way a model faces doesn't matter to the layout. Every piece is
	/// one no player built, so the vikings repair and rebuild it like the rest of their village
	/// </remarks>
	internal static class VillageFurnishing
	{
		private const string BedPrefab = "bed";
		private const string ChestPrefab = "piece_chest_wood";
		private const string TablePrefab = "piece_table";
		private const string RoundTablePrefab = "piece_table_round";
		private const string StoolPrefab = "piece_chair";
		private const string WorkbenchPrefab = "piece_workbench";
		private const string ForgePrefab = "forge";

		/// <summary>
		/// The crafting stations every village has, by prefab - in a house of its core when there's room, or else out on
		/// its open ground
		/// </summary>
		private static readonly string[] StationPrefabs = new[] { WorkbenchPrefab, ForgePrefab };

		/// <summary>
		/// On a farm's generator, that its village has been given the crafting stations it was missing, which happens
		/// once, the first time it's loaded after it was furnished, so no station appears long after
		/// </summary>
		private static readonly int StationsDoneKey = "kriona.VikingVillages.villageStationsDone".GetStableHashCode();

		/// <summary>
		/// On a farm's generator, that its village's crafting stations have been upgraded for its biome (see
		/// AddUpgrades), which happens once, after it's been given its stations
		/// </summary>
		private static readonly int UpgradesDoneKey = "kriona.VikingVillages.villageStationUpgradesDone".GetStableHashCode();

		/// <summary>
		/// How far inside an upgrade's reach of its station it's put, and how far out in front of a station is kept clear
		/// to use it from, in meters
		/// </summary>
		private const float UpgradeReachSlack = 0.3f;
		private const float StationFrontClear = 1f;

		/// <summary>
		/// How a crafting station is put up under a shelter out on a village's open ground when no house has room for
		/// it - how many spots are tried each way (see PlaceSheltered), the most the ground can rise and fall under it,
		/// and the most it can when the ground is flattened for it, how far above the ground the room for it is checked
		/// from, and how far it keeps from the village's fire, past where the vikings gather, in meters
		/// </summary>
		private const int OutsideTries = 40;
		private const float OutsideFlatness = 0.3f;
		private const float OutsideFlattenLimit = 2f;
		private const float OutsideLift = 0.15f;
		private const float OutsideFireClearance = 8f;

		/// <summary>
		/// How far above the water ground flattened for a shelter is kept, so it stays dry, in meters
		/// </summary>
		private const float ShelterWaterClearance = 0.5f;

		/// <summary>
		/// The shelter a station gets out in the open - walls behind it and at both ends and a sloped roof on top, open
		/// at the front - which covers it enough for the vikings and players to use it: the wall and roof pieces, its
		/// width, depth and height, the wall and roof's size, how far the roof rises from its low edge on the back walls
		/// to its high edge over the open front, and how far each wall is sunk into the ground so it stands on it, in
		/// meters
		/// </summary>
		/// <remarks>
		/// The roof is of roof pieces rather than floors, since a floor's collider is tagged leaky and the game doesn't
		/// count it as a roof over a crafting station
		/// </remarks>
		private const string ShelterWall = "woodwall";
		private const string ShelterRoof = "wood_roof";
		private const float ShelterWidth = 4f;
		private const float ShelterDepth = 2f;
		private const float ShelterHeight = 2f;
		private const float ShelterPiece = 2f;
		private const float ShelterRoofRise = 1f;
		private const float ShelterSink = 0.05f;

		/// <summary>
		/// The wooden platform a shelter stands on when no spot is flat enough for it, even flattened (see PlaceRaised) -
		/// its floor, posts and stairs, how far its floor stands above the highest ground under it, how high the ground
		/// under a post or a stair's foot has to be below the floor for one to be put there, how far past the shelter's
		/// front the platform reaches, so there's room to stand at the station, and how high its front edge can stand
		/// above the ground to be stepped up on without stairs, in meters - and the most stairs it gets
		/// </summary>
		/// <remarks>
		/// A floor is 2 meters square with its pivot on its top face, a pole is 2 meters or 1 meter tall with its pivot
		/// in its middle, and a stair is 2 meters wide, rising 1 meter over 2 toward its back, with its pivot at its foot
		/// </remarks>
		private const string PlatformFloor = "wood_floor";
		private const string PlatformPole = "wood_pole2";
		private const string PlatformShortPole = "wood_pole";
		private const string PlatformStair = "wood_stair";
		private const float PlatformLift = 0.3f;
		private const float PlatformPostMin = 0.3f;
		private const float PlatformFront = 2f;
		private const float PlatformStepMax = 0.6f;
		private const int PlatformStairsMax = 8;

		/// <summary>
		/// How many chests a house gets, and the fewest and most stools around its table
		/// </summary>
		private const int ChestsMax = 2;
		private const int StoolsMin = 2;
		private const int StoolsMax = 4;

		/// <summary>
		/// How wide a cell of a house's floor is, in meters
		/// </summary>
		private const float Cell = 0.5f;

		/// <summary>
		/// How many level rays are cast from a house's middle to find its walls, and how high above the floor, in meters
		/// </summary>
		private const int WallRays = 16;
		private const float WallRayHeight = 1f;

		/// <summary>
		/// How high above a point the floor is looked for from - low enough to stay under the roof, since a house's
		/// middle can be partway up its walls - how far down, and how far a cell's floor can be from the house's and
		/// still be the same floor, in meters
		/// </summary>
		private const float FloorSearchUp = 0.5f;
		private const float FloorSearchDown = 6f;
		private const float FloorTolerance = 0.4f;

		/// <summary>
		/// How far above the floor a check for room starts, so the floor itself doesn't count, and how high it reaches
		/// for a cell, in meters
		/// </summary>
		private const float FloorClearance = 0.15f;
		private const float CellCheckHeight = 1.5f;

		/// <summary>
		/// How far around a door the floor is kept clear, and how far from a door the way in starts, in meters
		/// </summary>
		private const float DoorKeepClear = 1.5f;
		private const float DoorEntry = 2.5f;

		/// <summary>
		/// The least share of a house's free floor that stays free
		/// </summary>
		private const float FreeShareMin = 0.5f;

		/// <summary>
		/// How much a piece's size is shrunk for checking it's clear of what's already there, in meters, so it can touch a
		/// wall
		/// </summary>
		private const float FitSlack = 0.05f;

		/// <summary>
		/// The farthest a piece laid out against a wall is pushed back to meet the wall behind it, in meters
		/// </summary>
		private const float MaxPushBack = 2f;

		/// <summary>
		/// How wide across something hit has to be to be a house's wall rather than a post, in meters - a wall piece is
		/// about 2
		/// </summary>
		private const float WallMinWidth = 1f;

		/// <summary>
		/// On a farm's generator, that its village has been furnished
		/// </summary>
		public static readonly int FurnishedKey = "kriona.VikingVillages.villageFurnished".GetStableHashCode();

		/// <summary>
		/// The layers of walls, floors and furniture, and those with the creatures too, which a piece isn't placed on
		/// </summary>
		private static int solidMask;
		private static int pieceMask;

		/// <summary>
		/// Each piece's size from its colliders, around its own origin, by prefab name
		/// </summary>
		private static readonly Dictionary<string, Bounds> footprints = new Dictionary<string, Bounds>();

		/// <summary>
		/// Furnishes a village's houses the first time everything across it has loaded, giving it each crafting station
		/// it doesn't have in one of them - then, once, gives it each station it's still missing (see AddStations), so
		/// every village has them and none appears later on, and once more, upgrades its stations for its biome (see
		/// AddUpgrades)
		/// </summary>
		/// <param name="village">The village, whose generator this client owns, with everything across it loaded</param>
		public static void TryFurnish(Village village)
		{
			if (village.Houses.Count == 0 || ZNetScene.instance == null)
			{
				return;
			}
			// Each is noted first, so a village that can't be furnished isn't tried again and again
			if (!village.Zdo.GetBool(FurnishedKey))
			{
				village.Zdo.Set(FurnishedKey, true);
				Furnish(village);
				StockChests(village);
			}
			if (!village.Zdo.GetBool(StationsDoneKey))
			{
				village.Zdo.Set(StationsDoneKey, true);
				AddStations(village);
			}
			if (!village.Zdo.GetBool(UpgradesDoneKey))
			{
				village.Zdo.Set(UpgradesDoneKey, true);
				AddUpgrades(village);
			}
		}

		/// <summary>
		/// Notes a village a player started as furnished and given its crafting stations and their upgrades, so nothing
		/// is put in the buildings players put up for it - the vikings use the beds, chests and stations players build
		/// </summary>
		/// <param name="anchor">The village's anchor's ZDO, owned by this client</param>
		public static void MarkFurnished(ZDO anchor)
		{
			anchor.Set(FurnishedKey, true);
			anchor.Set(StationsDoneKey, true);
			anchor.Set(UpgradesDoneKey, true);
		}

		/// <summary>
		/// What a newly furnished village's chests start out with, by prefab name - a full stack of each, in a chest picked
		/// at random
		/// </summary>
		private static readonly string[] Stock = { "Wood", "Stone", "Resin" };

		/// <summary>
		/// The food a newly furnished village's chests start out with, so its vikings have something to eat before they
		/// forage, hunt and cook their own - by the index of the village's biome (see VikingGear), two foods of that
		/// biome, StarterFood of each
		/// </summary>
		internal static readonly string[][] StarterFoods =
		{
			// Meadows
			new[] { "CookedMeat", "Raspberry" },
			// Black Forest
			new[] { "CookedDeerMeat", "Blueberries" },
			// Swamp
			new[] { "DeerStew", "FishCooked" },
			// Mountains
			new[] { "Sausages", "OnionSoup" },
			// Plains
			new[] { "CookedLoxMeat", "Bread" },
			// Mistlands
			new[] { "CookedHareMeat", "CookedBugMeat" },
			// Ashlands
			new[] { "CookedAsksvinMeat", "CookedVoltureMeat" },
			// Deep North
			new[] { "CookedMooseMeat", "CookedSealBlubber" }
		};

		/// <summary>
		/// How many of each of its biome's StarterFoods a newly furnished village's chests start out with
		/// </summary>
		internal const int StarterFood = 10;

		/// <summary>
		/// The pickaxe a newly furnished village's chests start out with, by the index of the village's biome (see
		/// VikingGear) - the best its biome's own ore and the biomes before it give - and how many of them
		/// </summary>
		internal static readonly string[] StarterPickaxes =
		{
			// Meadows
			"PickaxeAntler",
			// Black Forest
			"PickaxeAntler",
			// Swamp
			"PickaxeBronze",
			// Mountains
			"PickaxeIron",
			// Plains
			"PickaxeIron",
			// Mistlands
			"PickaxeBlackMetal",
			// Ashlands
			"PickaxeBlackMetal",
			// Deep North
			"PickaxeBlackMetal"
		};
		internal const int StarterPickaxeCount = 2;

		/// <summary>
		/// Puts a full stack of each of Stock, StarterFood of each of its biome's StarterFoods and StarterPickaxeCount of
		/// its biome's StarterPickaxes, each stack in one of a village's own chests, picked at random among those with
		/// room - the chests it was furnished or built with, not ones a player built
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		private static void StockChests(Village village)
		{
			List<Container> chests = new List<Container>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZNetView view = instance.Value;
				Container chest = ((view != null) ? view.GetComponent<Container>() : null);
				if (chest == null || !view.IsValid() || !village.OwnsPiece(view.transform.position) || chest.GetInventory() == null)
				{
					continue;
				}
				Piece piece = view.GetComponent<Piece>();
				bool ownChest = (piece != null && piece.GetCreator() == 0L && chest.m_privacy == Container.PrivacySetting.Public);
				if (ownChest && view.GetComponent<TombStone>() == null && view.GetComponentInParent<Vagon>() == null && view.GetComponentInParent<Ship>() == null)
				{
					chests.Add(chest);
				}
			}
			if (chests.Count == 0)
			{
				return;
			}
			List<KeyValuePair<string, int>> stock = new List<KeyValuePair<string, int>>();
			foreach (string name in Stock)
			{
				stock.Add(new KeyValuePair<string, int>(name, int.MaxValue));
			}
			int biome = VikingGear.IndexOf(WorldGenerator.instance.GetBiome(village.Farm));
			foreach (string name in StarterFoods[Mathf.Clamp(biome, 0, StarterFoods.Length - 1)])
			{
				stock.Add(new KeyValuePair<string, int>(name, StarterFood));
			}
			// A pickaxe doesn't stack, so each is a stack of one
			for (int i = 0; i < StarterPickaxeCount; i++)
			{
				stock.Add(new KeyValuePair<string, int>(StarterPickaxes[Mathf.Clamp(biome, 0, StarterPickaxes.Length - 1)], 1));
			}
			foreach (KeyValuePair<string, int> entry in stock)
			{
				string name = entry.Key;
				GameObject prefab = ((ObjectDB.instance != null) ? ObjectDB.instance.GetItemPrefab(name) : null);
				ItemDrop drop = ((prefab != null) ? prefab.GetComponent<ItemDrop>() : null);
				if (drop == null)
				{
					continue;
				}
				int stack = Mathf.Clamp(entry.Value, 1, Mathf.Max(1, drop.m_itemData.m_shared.m_maxStackSize));
				List<Container> room = chests.FindAll((Container chest) => chest.GetInventory().CanAddItem(drop.m_itemData, stack));
				if (room.Count == 0)
				{
					continue;
				}
				Container picked = room[Random.Range(0, room.Count)];
				if (!picked.m_nview.IsOwner())
				{
					picked.m_nview.ClaimOwnership();
				}
				picked.GetInventory().AddItem(name, stack, 1, 0, 0L, "", false);
				Plugin.Log.LogInfo("Stocked a chest of the village at " + village.Farm.ToString("F0") + " with " + stack + " " + name);
			}
		}

		/// <summary>
		/// Sets up the layers pieces and what they stand clear of are found on, and brings every collider up to date
		/// </summary>
		private static void Prepare()
		{
			if (solidMask == 0)
			{
				solidMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
				pieceMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "character", "character_net", "character_ghost");
			}
			Physics.SyncTransforms();
		}

		/// <summary>
		/// Lists the indexes of a village's houses in its core that are furnished, in a random order - those not built
		/// from a blueprint, which comes with its own furniture, or by a player, which the village took in for a bed
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The indexes</returns>
		private static List<int> ShuffledCore(Village village)
		{
			List<int> core = new List<int>();
			for (int i = 0; i < village.Houses.Count; i++)
			{
				if (village.IsCore(village.Houses[i]) && !VillageBlueprints.IsBlueprintHouse(village, village.Houses[i]) && !VillageBeds.IsAdded(village, village.Houses[i]))
				{
					core.Add(i);
				}
			}
			for (int i = core.Count - 1; i > 0; i--)
			{
				int other = Random.Range(0, i + 1);
				int swap = core[i];
				core[i] = core[other];
				core[other] = swap;
			}
			return(core);
		}

		/// <summary>
		/// Lists the crafting stations of StationPrefabs that stand nowhere in a village - in its houses or out on its
		/// ground, built by anyone
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The stations' prefabs</returns>
		private static List<string> MissingStations(Village village)
		{
			List<string> missing = new List<string>(StationPrefabs);
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				if (instance.Value == null)
				{
					continue;
				}
				Vector3 position = instance.Key.GetPosition();
				if (!village.Contains(position) && !village.InHouse(position, 0.5f))
				{
					continue;
				}
				int prefab = instance.Key.GetPrefab();
				missing.RemoveAll((string station) => station.GetStableHashCode() == prefab);
			}
			return(missing);
		}

		/// <summary>
		/// Furnishes a village's houses, giving it each crafting station it doesn't have in a house of its core - each
		/// in a different house while there are enough - and the beds only in its core, where the vikings live, not its
		/// outposts
		/// </summary>
		/// <param name="village">The village</param>
		private static void Furnish(Village village)
		{
			List<VillagePaths.House> houses = village.Houses;
			Prepare();
			List<string> missing = MissingStations(village);
			List<int> core = ShuffledCore(village);
			Dictionary<int, List<string>> assigned = new Dictionary<int, List<string>>();
			for (int s = 0; s < missing.Count && core.Count > 0; s++)
			{
				int house = core[s % core.Count];
				if (!assigned.TryGetValue(house, out List<string> stations))
				{
					stations = new List<string>();
					assigned[house] = stations;
				}
				stations.Add(missing[s]);
			}
			List<Layout> layouts = new List<Layout>();
			for (int i = 0; i < houses.Count; i++)
			{
				// A house built from a blueprint is left as it was designed, furniture and all
				if (VillageBlueprints.IsBlueprintHouse(village, houses[i]))
				{
					layouts.Add(null);
					continue;
				}
				Layout layout = Layout.Measure(houses[i]);
				layouts.Add(layout);
				if (layout == null)
				{
					Plugin.Log.LogWarning("Couldn't lay out the house at " + houses[i].Center.ToString("F0") + " to furnish it");
					continue;
				}
				int heads = (core.Contains(i) ? VillageRespawn.PlannedHeads(houses[i].Center) : 0);
				List<string> stations = (assigned.TryGetValue(i, out List<string> given) ? given : new List<string>());
				foreach (string placed in Furnish(layout, heads, stations))
				{
					missing.Remove(placed);
				}
			}
			// A station whose chosen house had no room for it goes in another house of the village's core
			PlaceAnywhere(core, layouts, missing);
			foreach (string station in missing)
			{
				Plugin.Log.LogWarning("Found no room for a " + station + " in the core of the village around " + houses[0].Center.ToString("F0"));
			}
			foreach (Layout layout in layouts)
			{
				if (layout != null)
				{
					layout.Build();
				}
			}
		}

		/// <summary>
		/// Gives a village each crafting station it's missing - against a wall in a furnished house of its core with room
		/// for it, or else in a house of its core built from a blueprint, or else under a shelter of its own out on the
		/// village's open ground (see PlaceSheltered), trying spots no player can see first - and notes them among its
		/// pieces so its vikings build them back too
		/// </summary>
		/// <param name="village">The village</param>
		private static void AddStations(Village village)
		{
			Prepare();
			List<string> missing = MissingStations(village);
			if (missing.Count == 0)
			{
				return;
			}
			List<int> core = HiddenFirst(village, ShuffledCore(village));
			List<Layout> layouts = new List<Layout>();
			for (int i = 0; i < village.Houses.Count; i++)
			{
				layouts.Add(core.Contains(i) ? Layout.Measure(village.Houses[i]) : null);
			}
			PlaceAnywhere(core, layouts, missing);
			if (missing.Count > 0)
			{
				List<int> designed = new List<int>();
				for (int i = 0; i < village.Houses.Count; i++)
				{
					VillagePaths.House house = village.Houses[i];
					if (village.IsCore(house) && VillageBlueprints.IsBlueprintHouse(village, house) && !VillageBeds.IsAdded(village, house))
					{
						designed.Add(i);
						layouts[i] = Layout.Measure(house);
					}
				}
				PlaceAnywhere(HiddenFirst(village, designed), layouts, missing);
			}
			List<PieceRecord> built = new List<PieceRecord>();
			foreach (Layout layout in layouts)
			{
				if (layout != null)
				{
					built.AddRange(layout.Build());
				}
			}
			foreach (string station in missing)
			{
				if (!PlaceSheltered(village, station, built))
				{
					Plugin.Log.LogWarning("Found no room for a " + station + " anywhere in the village around " + village.Houses[0].Center.ToString("F0") + ", even under a shelter of its own");
				}
			}
			VillageRecords.Add(village, built);
		}

		/// <summary>
		/// Upgrades each workbench, forge, black forge and galdr table of a village's own - in its houses or out on its
		/// ground, built by no player - to the level its biome needs to repair the gear of that biome and those before it
		/// (see StationUpgrades), putting each upgrade it's missing within the upgrade's reach of it: against a wall of
		/// the house it stands in, or on the open ground around it out of the way of its front when it stands outside -
		/// and notes them among the village's pieces so its vikings build them back too
		/// </summary>
		/// <param name="village">The village, whose generator this client owns</param>
		private static void AddUpgrades(Village village)
		{
			if (WorldGenerator.instance == null)
			{
				return;
			}
			Prepare();
			int biome = VikingGear.IndexOf(WorldGenerator.instance.GetBiome(village.Farm));
			List<CraftingStation> stations = new List<CraftingStation>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZNetView view = instance.Value;
				CraftingStation station = ((view != null) ? view.GetComponent<CraftingStation>() : null);
				if (station == null || !view.IsValid() || !StationUpgrades.Find(Utils.GetPrefabName(view.gameObject), out StationUpgrades.Station _))
				{
					continue;
				}
				Vector3 position = view.transform.position;
				Piece piece = view.GetComponent<Piece>();
				if ((village.Contains(position) || village.InHouse(position, 0.5f)) && piece != null && piece.GetCreator() == 0L)
				{
					stations.Add(station);
				}
			}
			List<PieceRecord> built = new List<PieceRecord>();
			foreach (CraftingStation station in stations)
			{
				Upgrade(village, station, stations, biome, built);
			}
			VillageRecords.Add(village, built);
		}

		/// <summary>
		/// Puts the upgrades a crafting station is missing for a biome within reach of it - against a wall of the house it
		/// stands in, as near it as fits, keeping the front of every station in the house clear, or else on the open
		/// ground around it
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="station">The station</param>
		/// <param name="stations">Every station of the village's being upgraded, this one included</param>
		/// <param name="biome">The village's biome's index into VikingGear.Biomes</param>
		/// <param name="built">The pieces put up, as they're noted among the village's pieces, added to</param>
		private static void Upgrade(Village village, CraftingStation station, List<CraftingStation> stations, int biome, List<PieceRecord> built)
		{
			string stationName = Utils.GetPrefabName(station.gameObject);
			StationUpgrades.Find(stationName, out StationUpgrades.Station kind);
			List<StationExtension> attached = new List<StationExtension>();
			StationExtension.FindExtensions(station, station.transform.position, attached);
			HashSet<string> present = new HashSet<string>();
			foreach (StationExtension extension in attached)
			{
				present.Add(Utils.GetPrefabName(extension.gameObject));
			}
			List<string> missing = StationUpgrades.Missing(kind, biome, present);
			if (missing.Count == 0)
			{
				return;
			}
			Vector3 position = station.transform.position;
			int houseIndex = village.Houses.FindIndex((VillagePaths.House house) => Village.InHouse(house, position, 0f));
			Layout layout = null;
			if (houseIndex >= 0)
			{
				layout = Layout.Measure(village.Houses[houseIndex]);
				if (layout == null)
				{
					Plugin.Log.LogWarning("Couldn't lay out the house at " + village.Houses[houseIndex].Center.ToString("F0") + " to upgrade its " + stationName);
					return;
				}
				foreach (CraftingStation other in stations)
				{
					if (other != null && Village.InHouse(village.Houses[houseIndex], other.transform.position, 0f))
					{
						layout.KeepFront(other);
					}
				}
			}
			int placed = 0;
			foreach (string extension in missing)
			{
				float reach = UpgradeReach(extension);
				bool fits = ((layout != null) ? layout.PlaceNear(extension, position, reach) : PlaceOutsideNear(village, station, extension, reach, built));
				if (fits)
				{
					placed++;
				}
				else
				{
					Plugin.Log.LogWarning("Found no room for a " + extension + " within " + reach.ToString("F1") + "m of the " + stationName + " at " + position.ToString("F1") + " in the village at " + village.Farm.ToString("F0"));
				}
			}
			if (layout != null)
			{
				built.AddRange(layout.Build());
				Physics.SyncTransforms();
			}
			Plugin.Log.LogInfo("Upgraded the " + stationName + " at " + position.ToString("F1") + " in the village at " + village.Farm.ToString("F0") + " with " + placed + " of the " + missing.Count + " upgrades it was missing, for level " + StationUpgrades.Level(kind, biome));
		}

		/// <summary>
		/// Works out how far from its station an upgrade is put at most - inside the reach the game counts it from, by
		/// UpgradeReachSlack
		/// </summary>
		/// <param name="prefabName">The upgrade's prefab</param>
		/// <returns>The distance, in meters, or 0 when the game has no such upgrade</returns>
		private static float UpgradeReach(string prefabName)
		{
			GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
			StationExtension extension = ((prefab != null) ? prefab.GetComponent<StationExtension>() : null);
			return((extension != null) ? Mathf.Max(0f, extension.m_maxStationDistance - UpgradeReachSlack) : 0f);
		}

		/// <summary>
		/// Puts a station's upgrade on the open ground around it, facing the way it does, at the nearest spot within reach
		/// that's level, outside every house and not in front of the station, with nothing in the way - off painted ground
		/// like a path if there's such a spot, or else on it
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="station">The station, standing outside the village's houses</param>
		/// <param name="prefabName">The upgrade's prefab</param>
		/// <param name="reach">The farthest from the station it can go, in meters</param>
		/// <param name="built">The pieces put up, added to</param>
		/// <returns>False when there's no such spot, or the game has no such piece</returns>
		private static bool PlaceOutsideNear(Village village, CraftingStation station, string prefabName, float reach, List<PieceRecord> built)
		{
			GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
			if (prefab == null || reach <= 0f || !Footprint(prefabName, out Bounds size))
			{
				return(false);
			}
			Transform at = station.transform;
			bool hasFront = Footprint(Utils.GetPrefabName(station.gameObject), out Bounds stationSize);
			List<Vector3> spots = new List<Vector3>();
			for (float x = -reach; x <= reach; x += Cell)
			{
				for (float z = -reach; z <= reach; z += Cell)
				{
					Vector3 local = new Vector3(x, 0f, z);
					if (local.magnitude > reach)
					{
						continue;
					}
					// The upgrade faces the way the station does, so its box in the station's frame is its own, moved
					bool inFront = (hasFront && x + size.max.x > stationSize.min.x && x + size.min.x < stationSize.max.x && z + size.max.z > stationSize.max.z && z + size.min.z < stationSize.max.z + StationFrontClear);
					if (!inFront)
					{
						spots.Add(local);
					}
				}
			}
			spots.Sort((Vector3 a, Vector3 b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));
			for (int pass = 0; pass < 2; pass++)
			{
				foreach (Vector3 local in spots)
				{
					Vector3 spot = at.TransformPoint(local);
					Room room = OutsideRoom(village, spot, at.rotation, size, false);
					if (room.InHouse || room.Rise() > OutsideFlatness || room.Blockers.Count > 0 || room.Clearable.Count > 0 || (pass == 0 && room.Painted))
					{
						continue;
					}
					spot.y = room.Highest;
					if (Vector3.Distance(spot, at.position) > reach)
					{
						continue;
					}
					Put(prefab, spot, at.rotation, built);
					Physics.SyncTransforms();
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Orders some of a village's houses so those no player can see come first
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="houses">The houses' indexes</param>
		/// <returns>The indexes, the hidden houses first</returns>
		private static List<int> HiddenFirst(Village village, List<int> houses)
		{
			List<int> ordered = houses.FindAll((int i) => VillageRespawn.Hidden(village.Houses[i].Center));
			ordered.AddRange(houses.FindAll((int i) => !ordered.Contains(i)));
			return(ordered);
		}

		/// <summary>
		/// Puts up a crafting station out on a village's open ground under a shelter of its own, when no house has room
		/// for it - a wall behind it and at each end and a sloped roof on top, open at the front, which covers it
		/// enough to be used - outside the houses and away from the fire, trying in turn:
		/// - fairly flat ground with nothing in the way, off the painted ground the paths are on, out of every player's
		///   sight for the first half of the tries
		/// - ground up to OutsideFlattenLimit from flat, which is flattened, painted or not, clearing what grows or lies
		///   there, with no building piece or anything that can't be cleared in the way
		/// - the flattest spot of those, whatever's in the way, clearing what can be cleared and raising the shelter on
		///   a wooden platform with posts and stairs (see PlaceRaised), or flattening it without the pieces for one
		/// writing why each try failed to the log when the first way finds no spot
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="prefabName">The station's prefab</param>
		/// <param name="built">The pieces put up, as they're noted among the village's pieces, added to</param>
		/// <returns>False when the game has no such piece, or there's no open ground in the village's core at all</returns>
		private static bool PlaceSheltered(Village village, string prefabName, List<PieceRecord> built)
		{
			GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
			GameObject wall = ZNetScene.instance.GetPrefab(ShelterWall);
			GameObject roof = ZNetScene.instance.GetPrefab(ShelterRoof);
			if (prefab == null || wall == null || roof == null)
			{
				Plugin.Log.LogWarning("Couldn't find the " + prefabName + ", " + ShelterWall + " or " + ShelterRoof + " prefab to put up a shelter");
				return(false);
			}
			float height = ShelterHeight + ShelterRoofRise;
			Bounds size = new Bounds(new Vector3(0f, height / 2f, 0f), new Vector3(ShelterWidth + 0.3f, height, ShelterDepth + 0.3f));
			Fireplace fire = village.Fire();
			ShelterTries tries = new ShelterTries();
			for (int i = 0; i < OutsideTries; i++)
			{
				if (!ShelterSpot(village, fire, tries, out Vector3 spot))
				{
					continue;
				}
				// The first half of the tries keep out of every player's sight
				if (i < OutsideTries / 2 && !VillageRespawn.Hidden(spot))
				{
					tries.Seen++;
					continue;
				}
				Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
				Room room = OutsideRoom(village, spot, rotation, size, false);
				tries.Note(room);
				if (!room.Fits(OutsideFlatness, false))
				{
					continue;
				}
				spot.y = room.Highest;
				BuildShelter(spot, rotation, false, prefab, wall, roof, built);
				Plugin.Log.LogInfo("Put up a " + prefabName + " under a shelter at " + spot.ToString("F1") + " in the village at " + village.Farm.ToString("F0") + ", with no room for it in its houses");
				return(true);
			}
			Plugin.Log.LogWarning("Shelter for a " + prefabName + " in the village at " + village.Farm.ToString("F0") + ": no flat open spot in " + OutsideTries + " tries - " + tries.Describe() + ". Trying again, flattening the ground and clearing what's in the way");
			Vector3 bestSpot = Vector3.zero;
			Quaternion bestRotation = Quaternion.identity;
			Room best = null;
			ShelterTries relaxed = new ShelterTries();
			for (int i = 0; i < OutsideTries; i++)
			{
				if (!ShelterSpot(village, fire, relaxed, out Vector3 spot))
				{
					continue;
				}
				Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
				Room room = OutsideRoom(village, spot, rotation, size, true);
				relaxed.Note(room);
				if (room.InHouse)
				{
					continue;
				}
				if (best == null || room.Rise() < best.Rise())
				{
					best = room;
					bestSpot = spot;
					bestRotation = rotation;
				}
				if (room.Fits(OutsideFlattenLimit, true))
				{
					PlaceFlattened(village, prefabName, spot, rotation, room, prefab, wall, roof, built, "flattening " + room.Rise().ToString("F1") + "m of slope and clearing " + room.Clearable.Count + " things");
					return(true);
				}
			}
			if (best == null)
			{
				Plugin.Log.LogWarning("Shelter for a " + prefabName + " in the village at " + village.Farm.ToString("F0") + ": no open spot outside its houses at all in " + OutsideTries + " more tries - " + relaxed.Describe());
				return(false);
			}
			if (!PlaceRaised(village, prefabName, bestSpot, bestRotation, best, prefab, wall, roof, built, relaxed))
			{
				PlaceFlattened(village, prefabName, bestSpot, bestRotation, best, prefab, wall, roof, built, "the flattest spot tried, flattening " + best.Rise().ToString("F1") + "m of slope, clearing " + best.Clearable.Count + " things and leaving " + Join(best.Blockers) + " in the way - " + relaxed.Describe());
			}
			return(true);
		}

		/// <summary>
		/// Puts up a station's shelter on a wooden platform at a spot no flatter than it has to be - floors PlatformLift
		/// above the highest ground under the shelter and the room to stand in front of it, on posts down to the ground at
		/// every corner where it's at least PlatformPostMin below, and stairs from the platform's front edge down to the
		/// ground - clearing what can be cleared there first
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="prefabName">The station's prefab name</param>
		/// <param name="spot">Where the station goes</param>
		/// <param name="rotation">Which way it faces - the stairs go down in front</param>
		/// <param name="room">What's there (see OutsideRoom)</param>
		/// <param name="prefab">The station's prefab</param>
		/// <param name="wall">The wall piece</param>
		/// <param name="roof">The roof piece</param>
		/// <param name="built">The pieces put up, added to</param>
		/// <param name="tries">Why the spots tried didn't fit, for the log</param>
		/// <returns>False when the game has no such floor, pole or stair piece</returns>
		private static bool PlaceRaised(Village village, string prefabName, Vector3 spot, Quaternion rotation, Room room, GameObject prefab, GameObject wall, GameObject roof, List<PieceRecord> built, ShelterTries tries)
		{
			GameObject floor = ZNetScene.instance.GetPrefab(PlatformFloor);
			GameObject pole = ZNetScene.instance.GetPrefab(PlatformPole);
			GameObject shortPole = ZNetScene.instance.GetPrefab(PlatformShortPole);
			GameObject stair = ZNetScene.instance.GetPrefab(PlatformStair);
			if (floor == null || pole == null || shortPole == null || stair == null)
			{
				Plugin.Log.LogWarning("Couldn't find the " + PlatformFloor + ", " + PlatformPole + ", " + PlatformShortPole + " or " + PlatformStair + " prefab to raise a shelter on");
				return(false);
			}
			foreach (ZNetView view in room.Clearable)
			{
				if (view != null && view.IsValid())
				{
					view.ClaimOwnership();
					ZNetScene.instance.Destroy(view.gameObject);
				}
			}
			// The shelter's floors, two across and one deep, and the room to stand at the station in front of it
			float half = ShelterPiece / 2f;
			float back = -ShelterDepth / 2f;
			float front = ShelterDepth / 2f + PlatformFront;
			float top = float.MinValue;
			for (float x = -ShelterWidth / 2f; x <= ShelterWidth / 2f; x += ShelterPiece)
			{
				for (float z = back; z <= front; z += ShelterPiece)
				{
					top = Mathf.Max(top, ZoneSystem.instance.GetGroundHeight(spot + rotation * new Vector3(x, 0f, z)));
				}
			}
			top += PlatformLift;
			int posts = 0;
			for (float x = -ShelterWidth / 2f; x <= ShelterWidth / 2f; x += ShelterPiece)
			{
				for (float z = back; z <= front; z += ShelterPiece)
				{
					Vector3 corner = spot + rotation * new Vector3(x, 0f, z);
					corner.y = top;
					posts += (Post(corner, pole, shortPole, built) ? 1 : 0);
				}
			}
			for (float x = -half; x <= half; x += ShelterPiece)
			{
				for (float z = back + half; z < front; z += ShelterPiece)
				{
					Vector3 at = spot + rotation * new Vector3(x, 0f, z);
					at.y = top;
					Put(floor, at, rotation, built);
				}
			}
			// The stairs go down from the platform's front edge, each a meter lower and two farther out, until one reaches
			// the ground, with a post under the foot of each that stands clear of it. A stair rises toward its back, so it
			// faces the way the shelter does, its back to the platform
			int stairs = 0;
			bool climb = (top - ZoneSystem.instance.GetGroundHeight(spot + rotation * new Vector3(0f, 0f, front)) > PlatformStepMax);
			for (int rise = 1; climb && rise <= PlatformStairsMax; rise++)
			{
				Vector3 foot = spot + rotation * new Vector3(0f, 0f, front + ShelterPiece * rise);
				Vector3 at = spot + rotation * new Vector3(0f, 0f, front + ShelterPiece * (rise - 0.5f));
				at.y = top - rise;
				Put(stair, at, rotation, built);
				stairs++;
				float ground = ZoneSystem.instance.GetGroundHeight(foot);
				if (at.y <= ground + PlatformPostMin)
				{
					break;
				}
				foot.y = at.y;
				foreach (float side in new[] { -half, half })
				{
					Vector3 post = foot + rotation * new Vector3(side, 0f, 0f);
					Post(post, pole, shortPole, built);
				}
			}
			spot.y = top;
			BuildShelter(spot, rotation, true, prefab, wall, roof, built);
			Plugin.Log.LogWarning("Put up a " + prefabName + " under a shelter at " + spot.ToString("F1") + " in the village at " + village.Farm.ToString("F0") + ", on a platform " + (top - room.Lowest).ToString("F1") + "m above the lowest ground under it with " + posts + " posts and " + stairs + " stairs, the flattest spot tried rising " + room.Rise().ToString("F1") + "m - clearing " + room.Clearable.Count + " things and leaving " + Join(room.Blockers) + " in the way - " + tries.Describe());
			return(true);
		}

		/// <summary>
		/// Puts a post of poles under a point of a platform, down from it to the ground - the 2 meter pole, and a 1 meter
		/// one for what's left under a meter, the lowest one sunk into the ground - when the ground is at least
		/// PlatformPostMin below it
		/// </summary>
		/// <param name="top">Where the post's top goes</param>
		/// <param name="pole">The 2 meter pole</param>
		/// <param name="shortPole">The 1 meter pole</param>
		/// <param name="built">The pieces put up, added to</param>
		/// <returns>True when a post was put up</returns>
		private static bool Post(Vector3 top, GameObject pole, GameObject shortPole, List<PieceRecord> built)
		{
			float ground = ZoneSystem.instance.GetGroundHeight(top);
			if (top.y - ground < PlatformPostMin)
			{
				return(false);
			}
			float bottom = top.y;
			while (bottom > ground)
			{
				bool full = (bottom - ground > 1f);
				float length = (full ? 2f : 1f);
				Vector3 at = new Vector3(top.x, bottom - length / 2f, top.z);
				Put((full ? pole : shortPole), at, Quaternion.identity, built);
				bottom -= length;
			}
			return(true);
		}

		/// <summary>
		/// Flattens the ground at a spot, clears what can be cleared there and puts up a station's shelter on it
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="prefabName">The station's prefab name</param>
		/// <param name="spot">Where it goes</param>
		/// <param name="rotation">Which way it faces</param>
		/// <param name="room">What's there (see OutsideRoom)</param>
		/// <param name="prefab">The station's prefab</param>
		/// <param name="wall">The wall piece</param>
		/// <param name="roof">The roof piece</param>
		/// <param name="built">The pieces put up, added to</param>
		/// <param name="how">How it was put there, for the log</param>
		private static void PlaceFlattened(Village village, string prefabName, Vector3 spot, Quaternion rotation, Room room, GameObject prefab, GameObject wall, GameObject roof, List<PieceRecord> built, string how)
		{
			foreach (ZNetView view in room.Clearable)
			{
				if (view != null && view.IsValid())
				{
					view.ClaimOwnership();
					ZNetScene.instance.Destroy(view.gameObject);
				}
			}
			// Levels the ground halfway between its highest and lowest points, so it's cut and filled evenly - though never
			// down into the water
			spot.y = Mathf.Max((room.Lowest + room.Highest) / 2f, Mathf.Min(room.Highest, ZoneSystem.instance.m_waterLevel + ShelterWaterClearance));
			bool flattened = VillagePaths.Flatten(spot);
			if (!flattened)
			{
				spot.y = room.Highest;
			}
			BuildShelter(spot, rotation, flattened, prefab, wall, roof, built);
			Plugin.Log.LogWarning("Put up a " + prefabName + " under a shelter at " + spot.ToString("F1") + " in the village at " + village.Farm.ToString("F0") + ", " + how + (flattened ? "" : ", with no terrain operation to flatten it"));
		}

		/// <summary>
		/// Picks a random spot on open ground in a village's core for a shelter, away from its fire
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="fire">Its fire, or null</param>
		/// <param name="tries">Why tries failed, added to</param>
		/// <param name="spot">The spot</param>
		/// <returns>False when there was none, or it was too near the fire</returns>
		private static bool ShelterSpot(Village village, Fireplace fire, ShelterTries tries, out Vector3 spot)
		{
			if (!village.RandomOpenSpot(out spot))
			{
				tries.NoSpot++;
				return(false);
			}
			if (fire != null && Utils.DistanceXZ(spot, fire.transform.position) < OutsideFireClearance)
			{
				tries.NearFire++;
				return(false);
			}
			return(true);
		}

		/// <summary>
		/// What's at a spot a piece might stand on out on the open ground - how high and low the ground under it is,
		/// whether any of it is in a house or on painted ground, and what's in its way: what grows or lies there that
		/// can be cleared, and what can't, like a building piece, by name
		/// </summary>
		private class Room
		{
			public float Lowest = float.MaxValue;
			public float Highest = float.MinValue;
			public bool InHouse;
			public bool Painted;
			public readonly List<ZNetView> Clearable = new List<ZNetView>();
			public readonly List<string> Blockers = new List<string>();

			/// <summary>
			/// How far the ground under it rises from its lowest point to its highest
			/// </summary>
			/// <returns>The rise, in meters</returns>
			public float Rise()
			{
				return(Highest - Lowest);
			}

			/// <summary>
			/// Checks whether a piece can go there
			/// </summary>
			/// <param name="flatness">How far the ground can rise across it</param>
			/// <param name="clearing">True when the ground is flattened and painted ground and what can be cleared are
			/// fine, or false when nothing at all can be in the way</param>
			/// <returns>True when it can</returns>
			public bool Fits(float flatness, bool clearing)
			{
				if (InHouse || Rise() > flatness || Blockers.Count > 0)
				{
					return(false);
				}
				return(clearing || (!Painted && Clearable.Count == 0));
			}
		}

		/// <summary>
		/// Why a station's shelter didn't go at the spots tried, counted for the log
		/// </summary>
		private class ShelterTries
		{
			public int NoSpot;
			public int NearFire;
			public int Seen;
			public int InHouse;
			public int Painted;
			public int Steep;
			public int Cleared;
			public int Blocked;
			public float Flattest = float.MaxValue;
			public readonly Dictionary<string, int> BlockedBy = new Dictionary<string, int>();

			/// <summary>
			/// Counts what kept a spot tried from fitting
			/// </summary>
			/// <param name="room">What's at the spot</param>
			public void Note(Room room)
			{
				if (room.InHouse)
				{
					InHouse++;
					return;
				}
				Flattest = Mathf.Min(Flattest, room.Rise());
				if (room.Rise() > OutsideFlatness)
				{
					Steep++;
				}
				if (room.Painted)
				{
					Painted++;
				}
				if (room.Clearable.Count > 0)
				{
					Cleared++;
				}
				if (room.Blockers.Count > 0)
				{
					Blocked++;
				}
				foreach (string blocker in room.Blockers)
				{
					BlockedBy[blocker] = (BlockedBy.TryGetValue(blocker, out int count) ? count : 0) + 1;
				}
			}

			/// <summary>
			/// Describes why the spots tried didn't fit
			/// </summary>
			/// <returns>The description</returns>
			public string Describe()
			{
				List<string> blockers = new List<string>();
				foreach (KeyValuePair<string, int> entry in BlockedBy)
				{
					blockers.Add(entry.Key + " x" + entry.Value);
				}
				return(NoSpot + " with no open ground found, " + NearFire + " near the fire, " + Seen + " in a player's sight, " + InHouse + " in a house, " + Steep + " too steep (flattest " + ((Flattest < float.MaxValue) ? Flattest.ToString("F2") + "m" : "none") + "), " + Painted + " on painted ground, " + Cleared + " with something growing or lying there, " + Blocked + " with something that can't be cleared in the way" + ((blockers.Count > 0) ? " (" + string.Join(", ", blockers) + ")" : ""));
			}
		}

		/// <summary>
		/// Joins names for the log
		/// </summary>
		/// <param name="names">The names</param>
		/// <returns>The names, or "nothing" when there are none</returns>
		private static string Join(List<string> names)
		{
			return((names.Count > 0) ? string.Join(", ", names) : "nothing");
		}

		/// <summary>
		/// Builds a station's shelter around it - two walls behind it, one at each end, each standing on the ground under
		/// it, and two roof pieces with their low edge on the back walls' tops, rising toward the open front - as pieces
		/// no player built
		/// </summary>
		/// <param name="spot">Where the station stands, on the ground</param>
		/// <param name="rotation">Which way it faces - the shelter is open in front</param>
		/// <param name="flattened">Whether the walls stand at the spot's height - on ground just flattened to it, which
		/// the ground under them may not show yet, or on a platform's floor</param>
		/// <param name="station">The station's prefab</param>
		/// <param name="wall">The wall piece</param>
		/// <param name="roof">The roof piece, whose pivot is under its middle, level with its low edge at its front, and which rises toward its back</param>
		/// <param name="built">The pieces put up, as they're noted among the village's pieces, added to</param>
		private static void BuildShelter(Vector3 spot, Quaternion rotation, bool flattened, GameObject station, GameObject wall, GameObject roof, List<PieceRecord> built)
		{
			Put(station, spot, rotation, built);
			float half = ShelterPiece / 2f;
			Quaternion side = rotation * Quaternion.Euler(0f, 90f, 0f);
			Vector3[] walls = { new Vector3(-half, 0f, -ShelterDepth / 2f), new Vector3(half, 0f, -ShelterDepth / 2f), new Vector3(-ShelterWidth / 2f, 0f, 0f), new Vector3(ShelterWidth / 2f, 0f, 0f) };
			float top = float.MinValue;
			for (int i = 0; i < walls.Length; i++)
			{
				Vector3 at = spot + rotation * walls[i];
				at.y = (flattened ? spot.y : ZoneSystem.instance.GetGroundHeight(at)) - ShelterSink + half;
				top = Mathf.Max(top, at.y + half);
				Put(wall, at, ((i < 2) ? rotation : side), built);
			}
			// Turned around, so its low edge lies along the back walls
			Quaternion slope = rotation * Quaternion.Euler(0f, 180f, 0f);
			foreach (float x in new[] { -half, half })
			{
				Vector3 at = spot + rotation * new Vector3(x, 0f, 0f);
				at.y = top;
				Put(roof, at, slope, built);
			}
		}

		/// <summary>
		/// Puts up a piece no player built, the way it's placed, noting it among the pieces put up
		/// </summary>
		/// <param name="prefab">The piece</param>
		/// <param name="position">Where it stands</param>
		/// <param name="rotation">Which way it faces</param>
		/// <param name="built">The pieces put up, added to</param>
		private static void Put(GameObject prefab, Vector3 position, Quaternion rotation, List<PieceRecord> built)
		{
			GameObject instance = Object.Instantiate(prefab, position, rotation);
			VillageRecords.MarkBuilt(instance);
			WearNTear wear = instance.GetComponent<WearNTear>();
			if (wear != null)
			{
				wear.OnPlaced();
			}
			int hash = prefab.name.GetStableHashCode();
			built.Add(new PieceRecord { Prefab = hash, Position = position, Rotation = rotation, Wood = VillageRecords.WoodCost(hash) });
		}

		/// <summary>
		/// Looks at what's at a spot a piece might stand on out on the open ground - the ground under it at its corners
		/// and middle, whether any of them is in a house or on painted ground like a path, and what's in the space it
		/// would take: what grows or lies there that can be cleared - anything with a ZDO that isn't a building piece or
		/// a creature - and what can't
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="spot">Where the piece's origin would be</param>
		/// <param name="rotation">Which way it would face</param>
		/// <param name="size">Its size, around its origin</param>
		/// <param name="flattening">True when the ground would be flattened, so the ground itself isn't in the way</param>
		/// <returns>What's there</returns>
		private static Room OutsideRoom(Village village, Vector3 spot, Quaternion rotation, Bounds size, bool flattening)
		{
			Room room = new Room();
			for (int corner = 0; corner < 5; corner++)
			{
				Vector3 local = ((corner == 4) ? size.center : size.center + new Vector3(((corner & 1) != 0) ? size.extents.x : -size.extents.x, 0f, ((corner & 2) != 0) ? size.extents.z : -size.extents.z));
				Vector3 point = spot + rotation * local;
				float height = ZoneSystem.instance.GetGroundHeight(point);
				room.Lowest = Mathf.Min(room.Lowest, height);
				room.Highest = Mathf.Max(room.Highest, height);
				Heightmap heightmap = Heightmap.FindHeightmap(point);
				room.InHouse |= village.InHouse(point, 0.5f);
				room.Painted |= (heightmap != null && heightmap.IsCleared(point));
			}
			if (room.InHouse)
			{
				return(room);
			}
			Vector3 center = spot + rotation * size.center;
			center.y = (flattening ? (room.Lowest + room.Highest) / 2f : room.Highest) + OutsideLift + size.extents.y;
			foreach (Collider hit in Physics.OverlapBox(center, size.extents, rotation, solidMask, QueryTriggerInteraction.Ignore))
			{
				if (hit == null || hit.GetComponentInParent<Heightmap>() != null)
				{
					// The ground rising into the space is what keeps the spot from being flat enough, unless it's flattened
					if (!flattening && hit != null)
					{
						AddOnce(room.Blockers, "the ground");
					}
					continue;
				}
				ZNetView view = hit.GetComponentInParent<ZNetView>();
				if (view != null && hit.GetComponentInParent<Character>() != null)
				{
					continue;
				}
				string name = ((view != null) ? Utils.GetPrefabName(view.gameObject) : hit.transform.root.name);
				if (view != null && view.IsValid() && Clearable(view))
				{
					if (!room.Clearable.Contains(view))
					{
						room.Clearable.Add(view);
					}
					continue;
				}
				AddOnce(room.Blockers, name);
			}
			return(room);
		}

		/// <summary>
		/// Checks whether something in a shelter's way can be cleared - what grows or lies on the ground, like a tree, a
		/// fallen log, a bush, a rock or a wild plant, but never a building piece, an item, a chest or a tombstone
		/// </summary>
		/// <param name="view">What's in the way</param>
		/// <returns>True when it can</returns>
		private static bool Clearable(ZNetView view)
		{
			if (view.GetComponent<Piece>() != null || view.GetComponent<ItemDrop>() != null || view.GetComponent<Container>() != null || view.GetComponent<TombStone>() != null)
			{
				return(false);
			}
			return(view.GetComponent<TreeBase>() != null || view.GetComponent<TreeLog>() != null || view.GetComponent<Destructible>() != null || view.GetComponent<MineRock>() != null || view.GetComponent<MineRock5>() != null || view.GetComponent<Pickable>() != null);
		}

		/// <summary>
		/// Adds a name to a list unless it's already there
		/// </summary>
		/// <param name="names">The list</param>
		/// <param name="name">The name</param>
		private static void AddOnce(List<string> names, string name)
		{
			if (!names.Contains(name))
			{
				names.Add(name);
			}
		}

		/// <summary>
		/// Places each of some crafting stations against a wall in the first house of a village's core with room for it,
		/// spreading them across different houses while there are enough
		/// </summary>
		/// <param name="core">The indexes of the village's core houses</param>
		/// <param name="layouts">Each house's layout, or null</param>
		/// <param name="missing">The stations' prefabs, with each placed one taken out</param>
		private static void PlaceAnywhere(List<int> core, List<Layout> layouts, List<string> missing)
		{
			HashSet<int> used = new HashSet<int>();
			foreach (string station in new List<string>(missing))
			{
				// Houses without a station come first, so each station gets a house of its own when it can
				List<int> order = new List<int>(core);
				order.Sort((int a, int b) => (used.Contains(a) ? 1 : 0).CompareTo(used.Contains(b) ? 1 : 0));
				foreach (int i in order)
				{
					if (layouts[i] != null && layouts[i].PlaceAgainstWall(station))
					{
						missing.Remove(station);
						used.Add(i);
						break;
					}
				}
			}
		}

		/// <summary>
		/// Lays out a house's pieces, most needed first - a bed for its first viking, a chest, the crafting stations it
		/// gets, a bed for its second viking, a table with stools, and another chest - each as fits
		/// </summary>
		/// <param name="layout">The house's layout</param>
		/// <param name="heads">How many vikings call the house home</param>
		/// <param name="stations">The crafting stations the house gets</param>
		/// <returns>The stations it had room for</returns>
		private static List<string> Furnish(Layout layout, int heads, List<string> stations)
		{
			List<string> placedStations = new List<string>();
			int beds = 0;
			if (heads > 0 && layout.PlaceAgainstWall(BedPrefab))
			{
				beds++;
			}
			int chests = (layout.PlaceAgainstWall(ChestPrefab) ? 1 : 0);
			foreach (string station in stations)
			{
				if (layout.PlaceAgainstWall(station))
				{
					placedStations.Add(station);
				}
			}
			for (int i = beds; i < heads; i++)
			{
				layout.PlaceAgainstWall(BedPrefab);
			}
			if (!layout.PlaceTable(TablePrefab))
			{
				layout.PlaceTable(RoundTablePrefab);
			}
			for (int i = chests; i < ChestsMax; i++)
			{
				layout.PlaceAgainstWall(ChestPrefab);
			}
			Plugin.Log.LogInfo("Furnished the house at " + layout.House.Center.ToString("F0") + " with " + layout.Placed.Count + " pieces");
			return(placedStations);
		}

		/// <summary>
		/// Works out a piece's size from its colliders, around its own origin
		/// </summary>
		/// <param name="prefabName">The piece's prefab</param>
		/// <param name="size">Its size</param>
		/// <returns>False when the game has no such piece or it has no colliders</returns>
		public static bool Footprint(string prefabName, out Bounds size)
		{
			if (footprints.TryGetValue(prefabName, out size))
			{
				return(size.size != Vector3.zero);
			}
			size = new Bounds();
			GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
			if (prefab != null)
			{
				Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
				bool found = false;
				foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
				{
					if (collider.isTrigger || !LocalBounds(collider, out Bounds local))
					{
						continue;
					}
					Matrix4x4 toPiece = toRoot * collider.transform.localToWorldMatrix;
					for (int corner = 0; corner < 8; corner++)
					{
						Vector3 point = local.center + Vector3.Scale(local.extents, new Vector3(((corner & 1) != 0) ? 1f : -1f, ((corner & 2) != 0) ? 1f : -1f, ((corner & 4) != 0) ? 1f : -1f));
						Vector3 moved = toPiece.MultiplyPoint3x4(point);
						if (found)
						{
							size.Encapsulate(moved);
						}
						else
						{
							size = new Bounds(moved, Vector3.zero);
							found = true;
						}
					}
				}
			}
			footprints[prefabName] = size;
			return(size.size != Vector3.zero);
		}

		/// <summary>
		/// Gets a collider's bounds around its own transform
		/// </summary>
		/// <param name="collider">The collider</param>
		/// <param name="local">Its bounds</param>
		/// <returns>False for a kind of collider that isn't measured</returns>
		private static bool LocalBounds(Collider collider, out Bounds local)
		{
			local = new Bounds();
			if (collider is BoxCollider box)
			{
				local = new Bounds(box.center, box.size);
				return(true);
			}
			if (collider is MeshCollider mesh && mesh.sharedMesh != null)
			{
				local = mesh.sharedMesh.bounds;
				return(true);
			}
			if (collider is CapsuleCollider capsule)
			{
				Vector3 extent = Vector3.one * capsule.radius * 2f;
				extent[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2f);
				local = new Bounds(capsule.center, extent);
				return(true);
			}
			if (collider is SphereCollider sphere)
			{
				local = new Bounds(sphere.center, Vector3.one * sphere.radius * 2f);
				return(true);
			}
			return(false);
		}

		/// <summary>
		/// Finds the floor under a point inside a house - the highest floor piece or ground below it, past anything
		/// standing on the floor, like a table
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="floor">Where the floor is</param>
		/// <returns>False when there's none</returns>
		private static bool Floor(Vector3 point, out Vector3 floor)
		{
			floor = point;
			Vector3 from = new Vector3(point.x, point.y + FloorSearchUp, point.z);
			RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, FloorSearchUp + FloorSearchDown, solidMask, QueryTriggerInteraction.Ignore);
			if (hits.Length == 0)
			{
				return(false);
			}
			System.Array.Sort(hits, (RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance));
			int terrain = LayerMask.NameToLayer("terrain");
			foreach (RaycastHit hit in hits)
			{
				WearNTear piece = hit.collider.GetComponentInParent<WearNTear>();
				bool floorPiece = (piece != null && Utils.GetPrefabName(piece.gameObject).IndexOf("floor", System.StringComparison.OrdinalIgnoreCase) >= 0);
				if ((floorPiece || hit.collider.gameObject.layer == terrain) && hit.normal.y > 0.9f)
				{
					floor = hit.point;
					return(true);
				}
			}
			floor = hits[hits.Length - 1].point;
			return(true);
		}

		/// <summary>
		/// A piece laid out in a house, to be built
		/// </summary>
		private struct Placement
		{
			public string Prefab;
			public Vector3 Position;
			public Quaternion Rotation;

			/// <summary>
			/// The cells it takes, as column and row
			/// </summary>
			public List<Vector2Int> Cells;
		}

		/// <summary>
		/// A house's floor as a grid of cells lined up with its walls, and the pieces laid out on it
		/// </summary>
		private class Layout
		{
			public VillagePaths.House House;
			public readonly List<Placement> Placed = new List<Placement>();

			/// <summary>
			/// The floor at the house's middle, and the directions along its walls
			/// </summary>
			private Vector3 origin;
			private Vector3 right;
			private Vector3 forward;
			private Quaternion facing;

			/// <summary>
			/// Where the grid starts from the middle, along right and forward, and its cells
			/// </summary>
			private float startX;
			private float startZ;
			private FloorGrid grid;

			/// <summary>
			/// Where the house's doors stand
			/// </summary>
			private readonly List<Vector3> doors = new List<Vector3>();

			/// <summary>
			/// The cells kept clear in front of each crafting station, to use it from (see KeepFront)
			/// </summary>
			private readonly List<List<Vector2Int>> stationFronts = new List<List<Vector2Int>>();

			/// <summary>
			/// Measures a house - its floor, which way its walls run and how far they are, and what already stands on
			/// each cell of its floor
			/// </summary>
			/// <param name="house">The house</param>
			/// <returns>The layout, or null when the house has no floor or walls to find</returns>
			public static Layout Measure(VillagePaths.House house)
			{
				if (!Floor(house.Center, out Vector3 middle))
				{
					return(null);
				}
				Layout layout = new Layout();
				layout.House = house;
				layout.origin = middle;
				if (!layout.FindWalls())
				{
					return(null);
				}
				layout.MarkCells();
				return((layout.grid.FreeAtStart > 0) ? layout : null);
			}

			/// <summary>
			/// Works out which way the house's walls run from the level rays that hit them, and how far each wall is
			/// from the middle
			/// </summary>
			/// <returns>False when no walls were found</returns>
			private bool FindWalls()
			{
				Vector3 rayOrigin = origin + Vector3.up * WallRayHeight;
				float reach = House.HalfSize + 1f;
				List<Vector3> normals = new List<Vector3>();
				for (int i = 0; i < WallRays; i++)
				{
					Vector3 direction = Quaternion.Euler(0f, i * 360f / WallRays, 0f) * Vector3.forward;
					if (!Physics.Raycast(rayOrigin, direction, out RaycastHit wall, reach, solidMask, QueryTriggerInteraction.Ignore) || wall.distance < House.HalfSize * 0.5f)
					{
						continue;
					}
					Vector3 normal = wall.normal;
					normal.y = 0f;
					if (normal.sqrMagnitude < 0.25f)
					{
						continue;
					}
					normals.Add(normal);
				}
				if (normals.Count == 0)
				{
					return(false);
				}
				facing = Quaternion.Euler(0f, WallYaw(normals), 0f);
				forward = facing * Vector3.forward;
				right = facing * Vector3.right;
				float east = WallDistance(rayOrigin, right, reach);
				float west = WallDistance(rayOrigin, -right, reach);
				float north = WallDistance(rayOrigin, forward, reach);
				float south = WallDistance(rayOrigin, -forward, reach);
				startX = -west;
				startZ = -south;
				grid = new FloorGrid(Mathf.FloorToInt((west + east) / Cell), Mathf.FloorToInt((south + north) / Cell));
				return(grid.Columns > 1 && grid.Rows > 1);
			}

			/// <summary>
			/// Finds how far a wall is from a point along a direction
			/// </summary>
			/// <param name="from">The point</param>
			/// <param name="direction">The direction</param>
			/// <param name="reach">How far to look</param>
			/// <returns>The distance, or the house's half size when nothing's that close</returns>
			private float WallDistance(Vector3 from, Vector3 direction, float reach)
			{
				// A post or something else narrow in the way isn't the wall, so the grid reaches past it to the wall
				// behind - what stands in the house is marked on its cells
				Vector3 across = Vector3.Cross(Vector3.up, direction);
				float found = float.MaxValue;
				foreach (RaycastHit wall in Physics.RaycastAll(from, direction, reach, solidMask, QueryTriggerInteraction.Ignore))
				{
					Vector3 extent = wall.collider.bounds.size;
					float width = Mathf.Abs(extent.x * across.x) + Mathf.Abs(extent.z * across.z);
					if (width >= WallMinWidth && wall.distance < found)
					{
						found = wall.distance;
					}
				}
				return((found < float.MaxValue) ? Mathf.Max(0f, found - FitSlack) : House.HalfSize);
			}

			/// <summary>
			/// Finds where a cell's middle is in the world, on the floor
			/// </summary>
			/// <param name="column">The cell's column</param>
			/// <param name="row">The cell's row</param>
			/// <returns>Its middle</returns>
			private Vector3 CellPoint(int column, int row)
			{
				return(origin + right * (startX + (column + 0.5f) * Cell) + forward * (startZ + (row + 0.5f) * Cell));
			}

			/// <summary>
			/// Marks each cell blocked where there's no floor or something stands, kept clear by a door, or free, and
			/// notes where the way in from each door starts
			/// </summary>
			private void MarkCells()
			{
				foreach (Collider collider in Physics.OverlapSphere(House.Center, House.HalfSize + 2f, solidMask, QueryTriggerInteraction.Collide))
				{
					Door door = collider.GetComponentInParent<Door>();
					if (door != null && !doors.Contains(door.transform.position))
					{
						doors.Add(door.transform.position);
					}
				}
				Vector3 halfCell = new Vector3(Cell * 0.4f, (CellCheckHeight - FloorClearance) / 2f, Cell * 0.4f);
				for (int column = 0; column < grid.Columns; column++)
				{
					for (int row = 0; row < grid.Rows; row++)
					{
						Vector3 point = CellPoint(column, row);
						bool floored = (Floor(point, out Vector3 floor) && Mathf.Abs(floor.y - origin.y) <= FloorTolerance);
						Vector3 checkCenter = new Vector3(point.x, origin.y + FloorClearance + halfCell.y, point.z);
						if (!floored || Physics.CheckBox(checkCenter, halfCell, facing, solidMask, QueryTriggerInteraction.Ignore))
						{
							grid.Cells[column, row] = FloorGrid.Blocked;
							continue;
						}
						grid.Cells[column, row] = FloorGrid.Free;
						foreach (Vector3 door in doors)
						{
							float distance = Utils.DistanceXZ(point, door);
							if (distance <= DoorKeepClear)
							{
								grid.Cells[column, row] = FloorGrid.KeepClear;
							}
							if (distance <= DoorEntry)
							{
								grid.Entries.Add(new Vector2Int(column, row));
							}
						}
						if (grid.Cells[column, row] == FloorGrid.Free)
						{
							grid.FreeAtStart++;
						}
					}
				}
				grid.EnterAtMiddle();
			}

			/// <summary>
			/// Places a piece against one of the house's walls, back to the wall and facing into the house, at the first
			/// spot along the walls that fits
			/// </summary>
			/// <param name="prefabName">The piece</param>
			/// <returns>False when it doesn't fit anywhere</returns>
			public bool PlaceAgainstWall(string prefabName)
			{
				if (!Footprint(prefabName, out Bounds size))
				{
					return(false);
				}
				int start = Random.Range(0, 4);
				// Every wall is tried right against it before any a cell out from one
				for (int gap = 0; gap <= 1; gap++)
				{
					for (int side = 0; side < 4; side++)
					{
						// Each side's inward direction, as a quarter turn from forward - the wall behind it is the grid's
						// edge
						int quarter = (start + side) % 4;
						Quaternion rotation = facing * Quaternion.Euler(0f, quarter * 90f, 0f);
						int width = CellSpan(size.size.x);
						int depth = CellSpan(size.size.z);
						int along = (((quarter % 2) == 0) ? grid.Columns : grid.Rows);
						int offsetStart = Random.Range(0, Mathf.Max(1, along));
						for (int step = 0; step < along; step++)
						{
							int offset = (offsetStart + step) % along;
							if (TryTake(prefabName, size, rotation, grid.WallCells(quarter, offset, gap, width, depth)))
							{
								if (gap == 0)
								{
									PushBack(Placed.Count - 1, size);
								}
								return(true);
							}
						}
					}
				}
				return(false);
			}

			/// <summary>
			/// Keeps the floor in front of a crafting station standing in the house clear, StationFrontClear out from it
			/// and as wide as it is, so nothing is laid out there and the way to it stays open
			/// </summary>
			/// <param name="station">The station</param>
			public void KeepFront(CraftingStation station)
			{
				if (!Footprint(Utils.GetPrefabName(station.gameObject), out Bounds size))
				{
					return;
				}
				Transform at = station.transform;
				List<Vector2Int> front = new List<Vector2Int>();
				for (int column = 0; column < grid.Columns; column++)
				{
					for (int row = 0; row < grid.Rows; row++)
					{
						if (grid.Cells[column, row] != FloorGrid.Free)
						{
							continue;
						}
						Vector3 local = at.InverseTransformPoint(CellPoint(column, row));
						if (local.x >= size.min.x && local.x <= size.max.x && local.z > size.max.z - FitSlack && local.z <= size.max.z + StationFrontClear)
						{
							grid.Cells[column, row] = FloorGrid.KeepClear;
							grid.FreeAtStart--;
							front.Add(new Vector2Int(column, row));
						}
					}
				}
				if (front.Count > 0)
				{
					stationFronts.Add(front);
				}
			}

			/// <summary>
			/// Places a piece against one of the house's walls within reach of a point, back to the wall and facing into
			/// the house - right against a wall before a cell out from one, and the nearest the point first
			/// </summary>
			/// <param name="prefabName">The piece</param>
			/// <param name="point">The point, like the crafting station it's an upgrade of</param>
			/// <param name="reach">How far from the point its origin can be, in meters</param>
			/// <returns>False when it doesn't fit anywhere in reach</returns>
			public bool PlaceNear(string prefabName, Vector3 point, float reach)
			{
				if (reach <= 0f || !Footprint(prefabName, out Bounds size))
				{
					return(false);
				}
				int width = CellSpan(size.size.x);
				int depth = CellSpan(size.size.z);
				List<WallSpot> spots = new List<WallSpot>();
				for (int gap = 0; gap <= 1; gap++)
				{
					for (int quarter = 0; quarter < 4; quarter++)
					{
						Quaternion rotation = facing * Quaternion.Euler(0f, quarter * 90f, 0f);
						int along = (((quarter % 2) == 0) ? grid.Columns : grid.Rows);
						for (int offset = 0; offset < along; offset++)
						{
							List<Vector2Int> cells = grid.WallCells(quarter, offset, gap, width, depth);
							if (!grid.AreFree(cells))
							{
								continue;
							}
							float distance = Vector3.Distance(Origin(size, rotation, cells), point);
							if (distance <= reach)
							{
								spots.Add(new WallSpot { Gap = gap, Quarter = quarter, Offset = offset, Distance = distance });
							}
						}
					}
				}
				spots.Sort((WallSpot a, WallSpot b) => ((a.Gap != b.Gap) ? a.Gap.CompareTo(b.Gap) : a.Distance.CompareTo(b.Distance)));
				foreach (WallSpot spot in spots)
				{
					Quaternion rotation = facing * Quaternion.Euler(0f, spot.Quarter * 90f, 0f);
					if (!TryTake(prefabName, size, rotation, grid.WallCells(spot.Quarter, spot.Offset, spot.Gap, width, depth)))
					{
						continue;
					}
					if (spot.Gap == 0)
					{
						PushBack(Placed.Count - 1, size);
					}
					// Pushed back against the wall, it can end up out of reach
					if (Vector3.Distance(Placed[Placed.Count - 1].Position, point) > reach)
					{
						Remove(Placed.Count - 1);
						continue;
					}
					return(true);
				}
				return(false);
			}

			/// <summary>
			/// A spot against a wall a piece might go - how many cells out from the wall, which wall as quarter turns from
			/// forward, how many cells along it, and how far its origin would be from where it's wanted, in meters
			/// </summary>
			private struct WallSpot
			{
				public int Gap;
				public int Quarter;
				public int Offset;
				public float Distance;
			}

			/// <summary>
			/// Moves a piece laid out at the edge of the floor's grid back until it's against what's behind it - the
			/// house's wall, which can stand farther out than the grid when something in the house was in the way of the
			/// ray that measured it - unless that would put it in front of a door
			/// </summary>
			/// <remarks>
			/// Nothing else is laid out behind the grid's edge, so only what's already standing can be in the way
			/// </remarks>
			/// <param name="index">The piece's place among the pieces laid out</param>
			/// <param name="size">Its size</param>
			private void PushBack(int index, Bounds size)
			{
				Placement placement = Placed[index];
				Vector3 back = placement.Rotation * Vector3.back;
				PieceBox(size, placement.Position, placement.Rotation, out Vector3 center, out Vector3 half);
				if (!Physics.BoxCast(center, half, back, out RaycastHit hit, placement.Rotation, MaxPushBack, solidMask, QueryTriggerInteraction.Ignore) || hit.distance <= FitSlack)
				{
					return;
				}
				Vector3 moved = placement.Position + back * (hit.distance - FitSlack);
				if (doors.Exists((Vector3 door) => Utils.DistanceXZ(moved + placement.Rotation * size.center, door) <= DoorKeepClear + Mathf.Max(size.extents.x, size.extents.z)))
				{
					return;
				}
				placement.Position = moved;
				Placed[index] = placement;
			}

			/// <summary>
			/// Works out the box a piece takes up, shrunk a little so it can touch a wall and lifted off the floor
			/// </summary>
			/// <param name="size">The piece's size</param>
			/// <param name="position">Where its origin goes</param>
			/// <param name="rotation">Which way it faces</param>
			/// <param name="center">The box's middle</param>
			/// <param name="half">Half the box's size</param>
			private static void PieceBox(Bounds size, Vector3 position, Quaternion rotation, out Vector3 center, out Vector3 half)
			{
				half = new Vector3(Mathf.Max(0.05f, size.extents.x - FitSlack), Mathf.Max(0.05f, size.extents.y - FloorClearance / 2f), Mathf.Max(0.05f, size.extents.z - FitSlack));
				center = position + rotation * size.center + Vector3.up * (FloorClearance / 2f);
			}

			/// <summary>
			/// Places a table in the open middle of the house, with stools around it, when it fits with at least
			/// StoolsMin stools
			/// </summary>
			/// <param name="prefabName">The table</param>
			/// <returns>False when it doesn't fit</returns>
			public bool PlaceTable(string prefabName)
			{
				if (!Footprint(prefabName, out Bounds size) || !Footprint(StoolPrefab, out Bounds stool))
				{
					return(false);
				}
				for (int turn = 0; turn < 2; turn++)
				{
					Quaternion rotation = facing * Quaternion.Euler(0f, turn * 90f, 0f);
					int width = CellSpan((turn == 0) ? size.size.x : size.size.z);
					int depth = CellSpan((turn == 0) ? size.size.z : size.size.x);
					foreach (Vector2Int spot in grid.TableSpots(width, depth))
					{
						List<Vector2Int> taken = FloorGrid.Block(spot, width, depth);
						int before = Placed.Count;
						if (!TryTake(prefabName, size, rotation, taken))
						{
							continue;
						}
						if (PlaceStools(spot, width, depth, stool) >= StoolsMin)
						{
							return(true);
						}
						// Too few stools fit, so the table goes too
						while (Placed.Count > before)
						{
							Remove(Placed.Count - 1);
						}
					}
				}
				return(false);
			}

			/// <summary>
			/// Places stools around a table just placed, one at the middle of each side, facing the table, long sides first
			/// </summary>
			/// <param name="spot">The table's first cell</param>
			/// <param name="width">How many cells it is along the columns</param>
			/// <param name="depth">How many cells it is along the rows</param>
			/// <param name="stool">The stool's size</param>
			/// <returns>How many stools were placed</returns>
			private int PlaceStools(Vector2Int spot, int width, int depth, Bounds stool)
			{
				Vector2Int[] sides = FloorGrid.StoolSides(spot, width, depth);
				Vector3 table = (CellPoint(spot.x, spot.y) + CellPoint(spot.x + width - 1, spot.y + depth - 1)) / 2f;
				int placed = 0;
				foreach (Vector2Int cell in sides)
				{
					if (placed >= StoolsMax)
					{
						break;
					}
					Vector3 toTable = table - CellPoint(cell.x, cell.y);
					toTable.y = 0f;
					if (toTable.sqrMagnitude < 0.01f)
					{
						continue;
					}
					if (TryTake(StoolPrefab, stool, Quaternion.LookRotation(toTable.normalized), new List<Vector2Int> { cell }))
					{
						placed++;
					}
				}
				return(placed);
			}

			/// <summary>
			/// Takes cells for a piece when they're all free, nothing already there is in its way, and the house stays
			/// easy to get around - every piece reachable from a door and at least FreeShareMin of its floor free
			/// </summary>
			/// <param name="prefabName">The piece</param>
			/// <param name="size">Its size</param>
			/// <param name="rotation">Which way it faces</param>
			/// <param name="taken">The cells it would take</param>
			/// <returns>True when it was placed</returns>
			private bool TryTake(string prefabName, Bounds size, Quaternion rotation, List<Vector2Int> taken)
			{
				if (!grid.AreFree(taken))
				{
					return(false);
				}
				Vector3 position = Origin(size, rotation, taken);
				PieceBox(size, position, rotation, out Vector3 boxCenter, out Vector3 half);
				if (Physics.CheckBox(boxCenter, half, rotation, pieceMask, QueryTriggerInteraction.Ignore))
				{
					return(false);
				}
				grid.Take(taken);
				Placement placement = new Placement();
				placement.Prefab = prefabName;
				placement.Position = position;
				placement.Rotation = rotation;
				placement.Cells = taken;
				Placed.Add(placement);
				List<List<Vector2Int>> pieces = Placed.ConvertAll((Placement laid) => laid.Cells);
				// The floor kept clear in front of each station has to stay reachable too, so the station can be used
				pieces.AddRange(stationFronts);
				if (!grid.Roomy(pieces))
				{
					Remove(Placed.Count - 1);
					return(false);
				}
				return(true);
			}

			/// <summary>
			/// Works out where a piece's origin goes for it to take some cells, on the floor
			/// </summary>
			/// <param name="size">The piece's size</param>
			/// <param name="rotation">Which way it faces</param>
			/// <param name="taken">The cells it takes</param>
			/// <returns>Where its origin goes</returns>
			private Vector3 Origin(Bounds size, Quaternion rotation, List<Vector2Int> taken)
			{
				Vector3 middle = Vector3.zero;
				foreach (Vector2Int cell in taken)
				{
					middle += CellPoint(cell.x, cell.y);
				}
				middle /= taken.Count;
				// The piece's own middle is off its origin, so the origin goes where it puts the middle on the cells
				Vector3 offset = rotation * new Vector3(size.center.x, 0f, size.center.z);
				return(new Vector3(middle.x - offset.x, origin.y - size.min.y, middle.z - offset.z));
			}

			/// <summary>
			/// Takes a laid out piece back out, freeing its cells
			/// </summary>
			/// <param name="index">Its place among the pieces laid out</param>
			private void Remove(int index)
			{
				grid.Release(Placed[index].Cells);
				Placed.RemoveAt(index);
			}

			/// <summary>
			/// Builds the pieces laid out, as pieces no player built
			/// </summary>
			/// <returns>Each piece built, as it would be noted among the village's pieces</returns>
			public List<PieceRecord> Build()
			{
				List<PieceRecord> built = new List<PieceRecord>();
				foreach (Placement placement in Placed)
				{
					GameObject prefab = ZNetScene.instance.GetPrefab(placement.Prefab);
					if (prefab != null)
					{
						VillageRecords.MarkBuilt(Object.Instantiate(prefab, placement.Position, placement.Rotation));
						int hash = placement.Prefab.GetStableHashCode();
						built.Add(new PieceRecord { Prefab = hash, Position = placement.Position, Rotation = placement.Rotation, Wood = VillageRecords.WoodCost(hash) });
					}
				}
				return(built);
			}
		}

		/// <summary>
		/// Works out how many cells of a house's floor a piece takes across one way, leaving FitSlack so a piece just
		/// over a whole number of cells doesn't take one more
		/// </summary>
		/// <param name="size">The piece's size that way, in meters</param>
		/// <returns>How many cells it takes</returns>
		internal static int CellSpan(float size)
		{
			return(Mathf.CeilToInt((size - FitSlack) / Cell));
		}

		/// <summary>
		/// Works out which way a house's walls run from the level normals of the walls hit - since walls run at right
		/// angles, each normal's angle is folded into a quarter turn by taking it four times over, and the folded angles
		/// are averaged around the circle
		/// </summary>
		/// <param name="normals">The walls' normals, level</param>
		/// <returns>How far the walls are turned from the world's axes, in degrees, from -45 to 45</returns>
		internal static float WallYaw(List<Vector3> normals)
		{
			float sumSin = 0f;
			float sumCos = 0f;
			foreach (Vector3 normal in normals)
			{
				float angle = Mathf.Atan2(normal.x, normal.z) * 4f;
				sumSin += Mathf.Sin(angle);
				sumCos += Mathf.Cos(angle);
			}
			return(Mathf.Atan2(sumSin, sumCos) / 4f * Mathf.Rad2Deg);
		}

		/// <summary>
		/// A house's floor as a grid of cells, its columns running along the walls' right and its rows along their
		/// forward - each cell free, blocked where there's no floor or something stands, kept clear by a door, or taken
		/// by a piece laid out - with the cells a way in starts from
		/// </summary>
		internal class FloorGrid
		{
			public const byte Free = 0;
			public const byte Blocked = 1;
			public const byte KeepClear = 2;
			public const byte Taken = 3;

			/// <summary>
			/// How many cells the grid has each way, and each cell's state, by column and row
			/// </summary>
			public readonly int Columns;
			public readonly int Rows;
			public readonly byte[,] Cells;

			/// <summary>
			/// The cells a way into the house starts from, by its doors, and how many cells were free to start with
			/// </summary>
			public readonly List<Vector2Int> Entries = new List<Vector2Int>();
			public int FreeAtStart;

			/// <summary>
			/// The four steps from a cell to the cells beside it
			/// </summary>
			private static readonly Vector2Int[] Steps = new Vector2Int[] { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(1, 0) };

			/// <summary>
			/// Makes a grid with every cell free
			/// </summary>
			/// <param name="columns">How many columns it has</param>
			/// <param name="rows">How many rows it has</param>
			public FloorGrid(int columns, int rows)
			{
				Columns = columns;
				Rows = rows;
				Cells = new byte[Mathf.Max(columns, 0), Mathf.Max(rows, 0)];
			}

			/// <summary>
			/// Checks whether a cell is on the grid
			/// </summary>
			/// <param name="cell">The cell</param>
			/// <returns>True when it is</returns>
			public bool Inside(Vector2Int cell)
			{
				return(cell.x >= 0 && cell.y >= 0 && cell.x < Columns && cell.y < Rows);
			}

			/// <summary>
			/// Checks whether a cell can be walked across - free or kept clear by a door
			/// </summary>
			/// <param name="cell">The cell</param>
			/// <returns>True when it can</returns>
			public bool Walkable(Vector2Int cell)
			{
				if (!Inside(cell))
				{
					return(false);
				}
				byte state = Cells[cell.x, cell.y];
				return(state == Free || state == KeepClear);
			}

			/// <summary>
			/// Checks whether every one of some cells is on the grid and free
			/// </summary>
			/// <param name="cells">The cells</param>
			/// <returns>True when they all are</returns>
			public bool AreFree(List<Vector2Int> cells)
			{
				foreach (Vector2Int cell in cells)
				{
					if (!Inside(cell) || Cells[cell.x, cell.y] != Free)
					{
						return(false);
					}
				}
				return(true);
			}

			/// <summary>
			/// Marks cells taken by a piece
			/// </summary>
			/// <param name="cells">The cells</param>
			public void Take(List<Vector2Int> cells)
			{
				foreach (Vector2Int cell in cells)
				{
					Cells[cell.x, cell.y] = Taken;
				}
			}

			/// <summary>
			/// Marks cells free again
			/// </summary>
			/// <param name="cells">The cells</param>
			public void Release(List<Vector2Int> cells)
			{
				foreach (Vector2Int cell in cells)
				{
					Cells[cell.x, cell.y] = Free;
				}
			}

			/// <summary>
			/// Makes the way into a house no door gave a way into start from its middlemost cell that isn't blocked
			/// </summary>
			public void EnterAtMiddle()
			{
				if (Entries.Count > 0)
				{
					return;
				}
				Vector2Int middle = new Vector2Int(Columns / 2, Rows / 2);
				Vector2Int best = new Vector2Int(-1, -1);
				float bestDistance = float.MaxValue;
				for (int column = 0; column < Columns; column++)
				{
					for (int row = 0; row < Rows; row++)
					{
						float distance = (new Vector2Int(column, row) - middle).sqrMagnitude;
						if (Cells[column, row] != Blocked && distance < bestDistance)
						{
							best = new Vector2Int(column, row);
							bestDistance = distance;
						}
					}
				}
				if (best.x >= 0)
				{
					Entries.Add(best);
				}
			}

			/// <summary>
			/// Lists the cells a piece takes against a wall, its back to the grid's edge
			/// </summary>
			/// <param name="quarter">Which way it faces, as quarter turns from forward - 0 backs onto the first row, 1
			/// the first column, 2 the last row and 3 the last column</param>
			/// <param name="offset">How many cells along the wall it starts, from the wall's left as seen facing into
			/// the house</param>
			/// <param name="gap">How many cells out from the wall it stands</param>
			/// <param name="width">How many cells wide it is, along the wall</param>
			/// <param name="depth">How many cells deep it is, out from the wall</param>
			/// <returns>The cells, which can be off the grid</returns>
			public List<Vector2Int> WallCells(int quarter, int offset, int gap, int width, int depth)
			{
				List<Vector2Int> taken = new List<Vector2Int>();
				for (int a = 0; a < width; a++)
				{
					for (int d = 0; d < depth; d++)
					{
						int along = offset + a;
						int outward = gap + d;
						Vector2Int cell;
						if (quarter == 0)
						{
							cell = new Vector2Int(along, outward);
						}
						else if (quarter == 1)
						{
							cell = new Vector2Int(outward, Rows - 1 - along);
						}
						else if (quarter == 2)
						{
							cell = new Vector2Int(Columns - 1 - along, Rows - 1 - outward);
						}
						else
						{
							cell = new Vector2Int(Columns - 1 - outward, along);
						}
						taken.Add(cell);
					}
				}
				return(taken);
			}

			/// <summary>
			/// Lists the cells of a block
			/// </summary>
			/// <param name="spot">Its first cell, the lowest column and row</param>
			/// <param name="width">How many cells it is along the columns</param>
			/// <param name="depth">How many cells it is along the rows</param>
			/// <returns>The cells</returns>
			public static List<Vector2Int> Block(Vector2Int spot, int width, int depth)
			{
				List<Vector2Int> cells = new List<Vector2Int>();
				for (int a = 0; a < width; a++)
				{
					for (int d = 0; d < depth; d++)
					{
						cells.Add(new Vector2Int(spot.x + a, spot.y + d));
					}
				}
				return(cells);
			}

			/// <summary>
			/// Lists the spots a table's first cell can go, the nearest the grid's middle first - two cells from the
			/// grid's edge on every side, room for a stool beside it and a cell between the stool and the wall
			/// </summary>
			/// <param name="width">How many cells the table is along the columns</param>
			/// <param name="depth">How many cells it is along the rows</param>
			/// <returns>The spots</returns>
			public List<Vector2Int> TableSpots(int width, int depth)
			{
				List<Vector2Int> spots = new List<Vector2Int>();
				for (int column = 2; column + width + 2 <= Columns; column++)
				{
					for (int row = 2; row + depth + 2 <= Rows; row++)
					{
						spots.Add(new Vector2Int(column, row));
					}
				}
				Vector2 middle = new Vector2(Columns / 2f, Rows / 2f);
				spots.Sort((Vector2Int a, Vector2Int b) => (a + new Vector2(width, depth) / 2f - middle).sqrMagnitude.CompareTo((b + new Vector2(width, depth) / 2f - middle).sqrMagnitude));
				return(spots);
			}

			/// <summary>
			/// Lists the cells for a table's stools, one at the middle of each side, its long sides first
			/// </summary>
			/// <param name="spot">The table's first cell</param>
			/// <param name="width">How many cells it is along the columns</param>
			/// <param name="depth">How many cells it is along the rows</param>
			/// <returns>The cells - below and above it along the rows, then left and right, or left and right first
			/// when it's deeper than it's wide</returns>
			public static Vector2Int[] StoolSides(Vector2Int spot, int width, int depth)
			{
				Vector2Int[] sides = new Vector2Int[]
				{
					new Vector2Int(spot.x + width / 2, spot.y - 1),
					new Vector2Int(spot.x + width / 2, spot.y + depth),
					new Vector2Int(spot.x - 1, spot.y + depth / 2),
					new Vector2Int(spot.x + width, spot.y + depth / 2)
				};
				if (depth > width)
				{
					sides = new Vector2Int[] { sides[2], sides[3], sides[0], sides[1] };
				}
				return(sides);
			}

			/// <summary>
			/// Checks whether the house is still easy to get around - at least FreeShareMin of the cells free to start
			/// with can be walked to from the way in, and every piece is next to a cell that can be walked to
			/// </summary>
			/// <param name="pieces">The cells each piece laid out takes</param>
			/// <returns>True when it is</returns>
			public bool Roomy(List<List<Vector2Int>> pieces)
			{
				bool[,] reached = new bool[Columns, Rows];
				Queue<Vector2Int> open = new Queue<Vector2Int>();
				int reachedFree = 0;
				foreach (Vector2Int entry in Entries)
				{
					if (Walkable(entry) && !reached[entry.x, entry.y])
					{
						reached[entry.x, entry.y] = true;
						open.Enqueue(entry);
					}
				}
				while (open.Count > 0)
				{
					Vector2Int cell = open.Dequeue();
					if (Cells[cell.x, cell.y] == Free)
					{
						reachedFree++;
					}
					foreach (Vector2Int step in Steps)
					{
						Vector2Int next = cell + step;
						if (Walkable(next) && !reached[next.x, next.y])
						{
							reached[next.x, next.y] = true;
							open.Enqueue(next);
						}
					}
				}
				if (reachedFree < FreeAtStart * FreeShareMin)
				{
					return(false);
				}
				foreach (List<Vector2Int> piece in pieces)
				{
					bool reachable = false;
					foreach (Vector2Int cell in piece)
					{
						foreach (Vector2Int step in Steps)
						{
							Vector2Int next = cell + step;
							if (Walkable(next) && reached[next.x, next.y])
							{
								reachable = true;
							}
						}
					}
					if (!reachable)
					{
						return(false);
					}
				}
				return(true);
			}
		}
	}
}
