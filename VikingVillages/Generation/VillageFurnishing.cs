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
	/// cells with room for a viking to stand on (see FloorGrid.RoomToStand), so every way through is three cells wide,
	/// and while at least FreeShareMin of the floor that was free stays free, so a small house gets fewer pieces.
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
		private const float UpgradeReachSlack = 0.1f;
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
		/// AddUpgrades) - then parks its cart beside a house, once (see VillageCarts.PlaceGenerated)
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
			VillageCarts.PlaceGenerated(village);
		}

		/// <summary>
		/// Notes a village a player started as furnished and given its crafting stations, their upgrades and its cart, so
		/// nothing is put in the buildings players put up for it - the vikings use the beds, chests and stations players
		/// build, and build their cart themselves
		/// </summary>
		/// <param name="anchor">The village's anchor's ZDO, owned by this client</param>
		public static void MarkFurnished(ZDO anchor)
		{
			anchor.Set(FurnishedKey, true);
			anchor.Set(StationsDoneKey, true);
			anchor.Set(UpgradesDoneKey, true);
			anchor.Set(VillageCarts.PlacedKey, true);
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
		/// VikingGear) - the best its biome's own ore and the biomes before it give, or null for none - and how many of
		/// them
		/// </summary>
		internal static readonly string[] StarterPickaxes =
		{
			// Meadows
			null,
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
		/// its biome's StarterPickaxes when it has one, each stack in one of a village's own chests, picked at random among
		/// those with room - the chests it was furnished or built with, not ones a player built or the game's treasure
		/// chests
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
				bool ownChest = (piece != null && piece.GetCreator() == 0L && chest.m_privacy == Container.PrivacySetting.Public && !VillageLife.IsTreasureChest(chest));
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
			int biome = VillageCooking.VillageBiome(village);
			foreach (string name in StarterFoods[Mathf.Clamp(biome, 0, StarterFoods.Length - 1)])
			{
				stock.Add(new KeyValuePair<string, int>(name, StarterFood));
			}
			// A pickaxe doesn't stack, so each is a stack of one
			string pickaxe = StarterPickaxes[Mathf.Clamp(biome, 0, StarterPickaxes.Length - 1)];
			for (int i = 0; pickaxe != null && i < StarterPickaxeCount; i++)
			{
				stock.Add(new KeyValuePair<string, int>(pickaxe, 1));
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
		/// Furnishes a village's houses in three passes - first a bed in each house of its core that has none, then each
		/// crafting station it doesn't have in a house of its core with room for it, each a different house while there
		/// are enough (see PlaceAnywhere), then the rest of each house (see FurnishRest) - with the beds only in its core,
		/// where the vikings live, not its outposts. A station no house has room for is left to AddStations, which
		/// writes why to the log and tries elsewhere
		/// </summary>
		/// <param name="village">The village</param>
		private static void Furnish(Village village)
		{
			List<VillagePaths.House> houses = village.Houses;
			Prepare();
			List<string> missing = MissingStations(village);
			List<int> core = ShuffledCore(village);
			List<Layout> layouts = new List<Layout>();
			int[] heads = new int[houses.Count];
			int[] beds = new int[houses.Count];
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
				heads[i] = (core.Contains(i) ? VillageRespawn.PlannedHeads(houses[i].Center) : 0);
				beds[i] = FurnishFirstBed(layout, heads[i]);
			}
			// The stations go in before the chests and the other beds take up the walls of every house
			PlaceAnywhere(core, layouts, missing, false);
			for (int i = 0; i < houses.Count; i++)
			{
				if (layouts[i] != null)
				{
					FurnishRest(layouts[i], heads[i], beds[i]);
				}
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
			PlaceAnywhere(core, layouts, missing, true);
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
				if (designed.Count > 0)
				{
					PlaceAnywhere(HiddenFirst(village, designed), layouts, missing, true);
				}
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
		/// (see StationUpgrades), by the village's biome its vikings are of (see VillageCooking.VillageBiome), so the
		/// stations repair their gear, putting each upgrade it's missing within the upgrade's reach of it: against a wall of
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
			int biome = VillageCooking.VillageBiome(village);
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
		/// ground around it - and with the test log on, draws the house's floor once they're all laid out
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
					Plugin.Log.LogWarning("Found no room for a " + extension + " within " + reach.ToString("F1") + "m of the " + stationName + " at " + position.ToString("F1") + " in the village at " + village.Farm.ToString("F0") + ((layout != null) ? " - " + layout.DescribeMisses() : ""));
				}
			}
			if (layout != null)
			{
				built.AddRange(layout.Build());
				Physics.SyncTransforms();
			}
			Plugin.Log.LogInfo("Upgraded the " + stationName + " at " + position.ToString("F1") + " in the village at " + village.Farm.ToString("F0") + " with " + placed + " of the " + missing.Count + " upgrades it was missing, for level " + StationUpgrades.Level(kind, biome));
			// Drawn once every upgrade is laid out, so the grid shows where each one went as well as the room left
			if (layout != null && (TestLog.On || Player.m_debugMode))
			{
				Plugin.Log.LogInfo("Station room floor: " + layout.DescribeGrid());
			}
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
			if (!village.RandomOpenSpot(out spot, tries.Ground))
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
			/// Why the points looked at for open ground weren't open, behind NoSpot (see Village.RandomOpenSpot)
			/// </summary>
			public readonly Village.OpenMisses Ground = new Village.OpenMisses();

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
				return(NoSpot + " with no open ground found" + ((NoSpot > 0) ? " (" + Ground.Describe() + ")" : "") + ", " + NearFire + " near the fire, " + Seen + " in a player's sight, " + InHouse + " in a house, " + Steep + " too steep (flattest " + ((Flattest < float.MaxValue) ? Flattest.ToString("F2") + "m" : "none") + "), " + Painted + " on painted ground, " + Cleared + " with something growing or lying there, " + Blocked + " with something that can't be cleared in the way" + ((blockers.Count > 0) ? " (" + string.Join(", ", blockers) + ")" : ""));
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
		/// Finds where another chest goes in a house - against a wall as near a point as fits, within ChestNearReach of it,
		/// keeping the way from a door to every bed and piece in the house clear, the way the house was furnished (see
		/// Layout.PlaceNear)
		/// </summary>
		/// <param name="house">The house</param>
		/// <param name="near">The point - a chest already there, or the house's middle</param>
		/// <param name="position">Set to where the chest stands</param>
		/// <param name="rotation">Set to which way it faces</param>
		/// <returns>False when the house has no room for one</returns>
		internal static bool ChestSpotNear(VillagePaths.House house, Vector3 near, out Vector3 position, out Quaternion rotation)
		{
			position = Vector3.zero;
			rotation = Quaternion.identity;
			Prepare();
			Layout layout = Layout.Measure(house);
			if (layout == null || !layout.PlaceNear(ChestPrefab, near, ChestNearReach))
			{
				return(false);
			}
			Placement placed = layout.Placed[layout.Placed.Count - 1];
			position = placed.Position;
			rotation = placed.Rotation;
			return(true);
		}

		/// <summary>
		/// How far from the chest it goes beside another chest is put at most, in meters
		/// </summary>
		private const float ChestNearReach = 4f;

		/// <summary>
		/// The chest the vikings build more of when their chests run out of room
		/// </summary>
		internal const string ChestPiece = ChestPrefab;

		/// <summary>
		/// Checks whether a piece can stand on the open ground at a spot as it is - outside every house, on ground that
		/// rises no more than a distance under it, with nothing in its way, not even what could be cleared
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="spot">Where the piece's origin would be</param>
		/// <param name="rotation">Which way it would face</param>
		/// <param name="size">Its size, around its origin</param>
		/// <param name="flatness">How far the ground under it can rise, in meters</param>
		/// <param name="painted">True when painted ground like a path is fine</param>
		/// <param name="ground">Set to the highest ground under it</param>
		/// <returns>True when it can</returns>
		internal static bool OpenGround(Village village, Vector3 spot, Quaternion rotation, Bounds size, float flatness, bool painted, out float ground)
		{
			Prepare();
			Room room = OutsideRoom(village, spot, rotation, size, false);
			ground = room.Highest;
			return(room.Fits(flatness, false) || (painted && !room.InHouse && room.Rise() <= flatness && room.Blockers.Count == 0 && room.Clearable.Count == 0));
		}

		/// <summary>
		/// Checks whether something in a shelter's way can be cleared - what grows or lies on the ground, like a tree, a
		/// fallen log, a bush, a rock or a wild plant, but never a building piece, an item, a chest, a tombstone or an
		/// oak, which the village is built around (see VillageOaks)
		/// </summary>
		/// <param name="view">What's in the way</param>
		/// <returns>True when it can</returns>
		private static bool Clearable(ZNetView view)
		{
			if (view.GetComponent<Piece>() != null || view.GetComponent<ItemDrop>() != null || view.GetComponent<Container>() != null || view.GetComponent<TombStone>() != null || VillageOaks.IsOak(view))
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
		/// spreading them across different houses while there are enough - writing to the log, when asked to, why each
		/// house tried had no room for a station none had room for, and in a test run or debug mode, each such house's
		/// floor
		/// </summary>
		/// <param name="core">The indexes of the village's core houses</param>
		/// <param name="layouts">Each house's layout, or null</param>
		/// <param name="missing">The stations' prefabs, with each placed one taken out</param>
		/// <param name="log">True to write why a station found no room to the log</param>
		private static void PlaceAnywhere(List<int> core, List<Layout> layouts, List<string> missing, bool log)
		{
			HashSet<int> used = new HashSet<int>();
			foreach (string station in new List<string>(missing))
			{
				// Houses without a station come first, so each station gets a house of its own when it can
				List<int> order = new List<int>(core);
				order.Sort((int a, int b) => (used.Contains(a) ? 1 : 0).CompareTo(used.Contains(b) ? 1 : 0));
				List<string> misses = new List<string>();
				List<Layout> missed = new List<Layout>();
				foreach (int i in order)
				{
					if (layouts[i] == null)
					{
						misses.Add("house " + i + " of the village, which couldn't be laid out");
						missed.Add(null);
						continue;
					}
					if (layouts[i].PlaceAgainstWall(station))
					{
						missing.Remove(station);
						used.Add(i);
						misses.Clear();
						missed.Clear();
						break;
					}
					misses.Add(layouts[i].DescribeMisses());
					missed.Add(layouts[i]);
				}
				if (!log)
				{
					continue;
				}
				if (core.Count == 0)
				{
					misses.Add("the village, which has no core houses");
					missed.Add(null);
				}
				for (int m = 0; m < misses.Count; m++)
				{
					Plugin.Log.LogWarning("Station room: no room for a " + station + " in " + misses[m]);
					if (missed[m] != null && (TestLog.On || Player.m_debugMode))
					{
						Plugin.Log.LogInfo("Station room floor: " + missed[m].DescribeGrid());
					}
				}
			}
		}

		/// <summary>
		/// Lays out a bed for a house's first viking when no bed the vikings can get to already stands in it, as fits
		/// </summary>
		/// <param name="layout">The house's layout</param>
		/// <param name="heads">How many vikings call the house home</param>
		/// <returns>How many of its vikings have a bed in it, up to its heads</returns>
		private static int FurnishFirstBed(Layout layout, int heads)
		{
			int beds = Mathf.Min(heads, layout.StandingBeds);
			if (beds == 0 && heads > 0 && layout.PlaceAgainstWall(BedPrefab))
			{
				beds++;
			}
			return(beds);
		}

		/// <summary>
		/// Lays out the rest of a house's pieces once its first bed and the village's crafting stations are in, most
		/// needed first - a chest, a bed for each of its vikings still without one, a table with stools, and another
		/// chest - each as fits
		/// </summary>
		/// <param name="layout">The house's layout</param>
		/// <param name="heads">How many vikings call the house home</param>
		/// <param name="beds">How many of its vikings already have a bed in it (see FurnishFirstBed)</param>
		private static void FurnishRest(Layout layout, int heads, int beds)
		{
			int chests = (layout.PlaceAgainstWall(ChestPrefab) ? 1 : 0);
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
			if (TestLog.On || Player.m_debugMode)
			{
				Plugin.Log.LogInfo("Furnished floor: " + layout.DescribeGrid());
			}
		}

		/// <summary>
		/// Works out a piece's size from its solid colliders that are turned on, around its own origin - a collider
		/// turned off, like the box the bellows carry beside their mesh, takes up no room
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
					if (!collider.enabled || collider.isTrigger || !LocalBounds(collider, out Bounds local))
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
		/// A bed or chest standing in a house before it's furnished, for the log - its prefab, where it stands, the
		/// cells it stands on, as column and row, and the mark it's drawn with on the grid
		/// </summary>
		private struct StandingPiece
		{
			public string Name;
			public Vector3 Position;
			public List<Vector2Int> Cells;
			public char Mark;
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
			/// The cells each bed already standing in the house takes, for each that could be walked up to before
			/// anything was laid out, so nothing laid out shuts it off
			/// </summary>
			private readonly List<List<Vector2Int>> standingBeds = new List<List<Vector2Int>>();

			/// <summary>
			/// How many beds already stand in the house that could be walked up to before anything was laid out
			/// </summary>
			public int StandingBeds
			{
				get
				{
					return(standingBeds.Count);
				}
			}

			/// <summary>
			/// The cells each chest already standing in the house takes, like the treasure chest it came with or one
			/// furnished before its station was upgraded, for each that could be walked up to before anything was laid
			/// out, so nothing laid out shuts it off
			/// </summary>
			private readonly List<List<Vector2Int>> standingChests = new List<List<Vector2Int>>();

			/// <summary>
			/// The beds and chests already standing in the house, for the log (see DescribeGrid)
			/// </summary>
			private readonly List<StandingPiece> standingPieces = new List<StandingPiece>();

			/// <summary>
			/// How many of the cells something stood on when the house was measured each thing stood on, by its prefab,
			/// for the log (see DescribeGrid)
			/// </summary>
			private readonly Dictionary<string, int> standingCells = new Dictionary<string, int>();

			/// <summary>
			/// The spots tried for the last piece placed against a wall or near a point, and why each that didn't fit
			/// didn't - its cells something stands on or with no floor, kept clear by a door or in front of a station,
			/// or taken by a piece laid out, something already standing in its way, the house left too hard to get
			/// around, or out of reach of the point once pushed back against the wall or slid along it - and for a piece
			/// placed near a point, how many spots with free cells were out of its reach, and how many cells the piece
			/// takes along the wall and out from it, for the log (see DescribeMisses)
			/// </summary>
			private int missTried;
			private int missStanding;
			private int missKeptClear;
			private int missTaken;
			private int missBlocked;
			private int missCramped;
			private int missPushedFar;
			private int missFar;
			private bool missNear;
			private bool missNoSize;
			private int missWidth;
			private int missDepth;

			/// <summary>
			/// Describes why the last piece placed against a wall or near a point didn't fit in the house, for the log
			/// </summary>
			/// <returns>The description</returns>
			public string DescribeMisses()
			{
				string floor = "the house at " + House.Center.ToString("F0") + " (" + grid.Columns + "x" + grid.Rows + " cells, " + grid.CountFree() + " free, " + Placed.Count + " pieces laid out)";
				if (missNoSize)
				{
					return(floor + ": the piece's size couldn't be measured");
				}
				string tried = floor + ": " + missTried + " spots tried for a piece " + missWidth + "x" + missDepth + " cells along the wall and out from it - " + missStanding + " on cells something stands on or with no floor, " + missKeptClear + " on cells kept clear by a door or in front of a station, " + missTaken + " on cells taken by pieces laid out, " + missBlocked + " with something already standing in the way, " + missCramped + " leaving the house too hard to get around";
				if (!missNear)
				{
					return(tried);
				}
				return(tried + ", " + missPushedFar + " out of reach once pushed back against the wall or slid along it - and " + missFar + " spots against the walls with free cells out of reach");
			}

			/// <summary>
			/// Starts counting why the spots tried for a piece don't fit (see DescribeMisses)
			/// </summary>
			private void ResetMisses()
			{
				missTried = 0;
				missStanding = 0;
				missKeptClear = 0;
				missTaken = 0;
				missBlocked = 0;
				missCramped = 0;
				missPushedFar = 0;
				missFar = 0;
				missNear = false;
				missNoSize = false;
				missWidth = 0;
				missDepth = 0;
			}

			/// <summary>
			/// Counts a spot tried whose cells aren't all free, by what keeps them from being free (see
			/// FloorGrid.Hindrance)
			/// </summary>
			/// <param name="hindrance">What keeps them from being free</param>
			private void CountHindrance(byte hindrance)
			{
				missTried++;
				if (hindrance == FloorGrid.Blocked)
				{
					missStanding++;
				}
				else if (hindrance == FloorGrid.KeepClear)
				{
					missKeptClear++;
				}
				else
				{
					missTaken++;
				}
			}

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
			/// Marks each cell blocked where there's no floor or something stands, kept clear by a door, or free, notes
			/// where the way in from each door starts, and notes the cells of the beds and chests already standing that
			/// can be walked up to, and of every bed and chest already standing, and which cells have no floor and what stands
			/// on the others that are blocked, for the log
			/// </summary>
			private void MarkCells()
			{
				Dictionary<Bed, List<Vector2Int>> beds = new Dictionary<Bed, List<Vector2Int>>();
				Dictionary<Container, List<Vector2Int>> chests = new Dictionary<Container, List<Vector2Int>>();
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
						if (!floored)
						{
							grid.Cells[column, row] = FloorGrid.Blocked;
							grid.Floorless[column, row] = true;
							continue;
						}
						Collider[] standing = Physics.OverlapBox(checkCenter, halfCell, facing, solidMask, QueryTriggerInteraction.Ignore);
						if (standing.Length > 0)
						{
							grid.Cells[column, row] = FloorGrid.Blocked;
							NoteStanding(standing, new Vector2Int(column, row), beds);
							NoteStanding(standing, new Vector2Int(column, row), chests);
							CountStanding(standing);
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
				bool[,] reached = grid.Reach(out int _);
				foreach (List<Vector2Int> cells in beds.Values)
				{
					if (grid.Reachable(cells, reached))
					{
						standingBeds.Add(cells);
					}
				}
				foreach (List<Vector2Int> cells in chests.Values)
				{
					if (grid.Reachable(cells, reached))
					{
						standingChests.Add(cells);
					}
				}
				foreach (KeyValuePair<Bed, List<Vector2Int>> bed in beds)
				{
					standingPieces.Add(new StandingPiece { Name = Utils.GetPrefabName(bed.Key.gameObject), Position = bed.Key.transform.position, Cells = bed.Value, Mark = 'B' });
				}
				foreach (KeyValuePair<Container, List<Vector2Int>> chest in chests)
				{
					standingPieces.Add(new StandingPiece { Name = Utils.GetPrefabName(chest.Key.gameObject), Position = chest.Key.transform.position, Cells = chest.Value, Mark = 'C' });
				}
			}

			/// <summary>
			/// Counts a cell for each thing standing on it, once each, by its prefab - or the collider's name for what
			/// isn't a networked object, and "terrain" for the ground - for the log
			/// </summary>
			/// <param name="standing">The colliders standing on the cell</param>
			private void CountStanding(Collider[] standing)
			{
				int terrain = LayerMask.NameToLayer("terrain");
				HashSet<string> names = new HashSet<string>();
				foreach (Collider collider in standing)
				{
					ZNetView view = collider.GetComponentInParent<ZNetView>();
					string name = ((collider.gameObject.layer == terrain) ? "terrain" : ((view != null) ? Utils.GetPrefabName(view.gameObject) : collider.gameObject.name));
					if (names.Add(name))
					{
						standingCells.TryGetValue(name, out int count);
						standingCells[name] = count + 1;
					}
				}
			}

			/// <summary>
			/// Notes a cell as one a piece of a kind stands on, for each piece of that kind among what stands on it
			/// </summary>
			/// <typeparam name="T">The kind of piece, like Bed</typeparam>
			/// <param name="standing">The colliders standing on the cell</param>
			/// <param name="cell">The cell</param>
			/// <param name="found">The cells each piece of the kind stands on, added to</param>
			private static void NoteStanding<T>(Collider[] standing, Vector2Int cell, Dictionary<T, List<Vector2Int>> found) where T : Component
			{
				foreach (Collider collider in standing)
				{
					T piece = collider.GetComponentInParent<T>();
					if (piece == null)
					{
						continue;
					}
					if (!found.TryGetValue(piece, out List<Vector2Int> cells))
					{
						cells = new List<Vector2Int>();
						found[piece] = cells;
					}
					if (!cells.Contains(cell))
					{
						cells.Add(cell);
					}
				}
			}

			/// <summary>
			/// Describes the house's floor for the log once it's laid out - which way the grid lies, how many of the cells
			/// left free are reached from the way in (see FloorGrid.Reach), the pieces laid out and the beds and chests
			/// already standing, each with whether it can still be walked up to (see
			/// FloorGrid.Reachable), what blocked its cells when it was measured (see DescribeBlocked), and the grid drawn
			/// a row a line (see FloorGrid.Draw), with each piece laid out marked by its number, a bed already standing by
			/// "B" and a chest by "C"
			/// </summary>
			/// <returns>The description, its grid on the lines after the first</returns>
			public string DescribeGrid()
			{
				bool[,] reached = grid.Reach(out int reachedFree);
				Dictionary<Vector2Int, char> marks = new Dictionary<Vector2Int, char>();
				List<string> pieces = new List<string>();
				for (int i = 0; i < Placed.Count; i++)
				{
					char mark = (char)((i < 9) ? '1' + i : 'a' + i - 9);
					foreach (Vector2Int cell in Placed[i].Cells)
					{
						marks[cell] = mark;
					}
					pieces.Add(mark + " " + Placed[i].Prefab + " at " + Placed[i].Position.ToString("F1") + " facing " + Placed[i].Rotation.eulerAngles.y.ToString("F0") + (grid.Reachable(Placed[i].Cells, reached) ? "" : ", out of reach"));
				}
				foreach (StandingPiece standing in standingPieces)
				{
					foreach (Vector2Int cell in standing.Cells)
					{
						if (!marks.ContainsKey(cell))
						{
							marks[cell] = standing.Mark;
						}
					}
					pieces.Add(standing.Mark + " " + standing.Name + " at " + standing.Position.ToString("F1") + ", already standing" + (grid.Reachable(standing.Cells, reached) ? "" : ", out of reach"));
				}
				string head = "the house at " + House.Center.ToString("F0") + ", " + grid.Columns + "x" + grid.Rows + " cells of " + Cell.ToString("F1") + "m around its floor's middle at " + origin.ToString("F1") + ", drawn with " + facing.eulerAngles.y.ToString("F0") + " degrees from north up, " + reachedFree + " of the " + grid.CountFree() + " cells left free reached from the way in (" + grid.FreeAtStart + " were free before anything was laid out), blocked cells " + DescribeBlocked(grid.CountFloorless(), standingCells) + " - " + ((pieces.Count > 0) ? string.Join(", ", pieces) : "no pieces");
				return(head + "\n" + string.Join("\n", grid.Draw(reached, marks)));
			}

			/// <summary>
			/// Places a piece against one of the house's walls, back to the wall and facing into the house, at the first
			/// spot along the walls that fits
			/// </summary>
			/// <param name="prefabName">The piece</param>
			/// <returns>False when it doesn't fit anywhere</returns>
			public bool PlaceAgainstWall(string prefabName)
			{
				ResetMisses();
				if (!Footprint(prefabName, out Bounds size))
				{
					missNoSize = true;
					return(false);
				}
				int start = Random.Range(0, 4);
				int width = CellSpan(size.size.x);
				int depth = CellSpan(size.size.z);
				missWidth = width;
				missDepth = depth;
				// Every wall is tried right against it before any a cell out from one
				for (int gap = 0; gap <= 1; gap++)
				{
					for (int side = 0; side < 4; side++)
					{
						// Each side's inward direction, as a quarter turn from forward - the wall behind it is the grid's
						// edge
						int quarter = (start + side) % 4;
						Quaternion rotation = facing * Quaternion.Euler(0f, quarter * 90f, 0f);
						int along = (((quarter % 2) == 0) ? grid.Columns : grid.Rows);
						int offsetStart = Random.Range(0, Mathf.Max(1, along));
						for (int step = 0; step < along; step++)
						{
							int offset = (offsetStart + step) % along;
							List<Vector2Int> cells = grid.WallCells(quarter, offset, gap, width, depth);
							// A piece running past the end of the wall isn't a spot along it
							if (!cells.TrueForAll(grid.Inside))
							{
								continue;
							}
							if (TryTake(prefabName, size, rotation, cells))
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
			/// the house - right against a wall before a cell out from one, and the nearest the point first - sliding it
			/// along the wall toward the point when its cells leave it out of reach (see SlideToward)
			/// </summary>
			/// <param name="prefabName">The piece</param>
			/// <param name="point">The point, like the crafting station it's an upgrade of</param>
			/// <param name="reach">How far from the point its origin can be, in meters</param>
			/// <returns>False when it doesn't fit anywhere in reach</returns>
			public bool PlaceNear(string prefabName, Vector3 point, float reach)
			{
				ResetMisses();
				if (reach <= 0f || !Footprint(prefabName, out Bounds size))
				{
					missNoSize = (reach > 0f);
					return(false);
				}
				missNear = true;
				int width = CellSpan(size.size.x);
				int depth = CellSpan(size.size.z);
				missWidth = width;
				missDepth = depth;
				// A spot a little out of reach can still be tried, since the piece can slide along the wall toward the point
				float slideMost = SlideRoom(width, size.size.x) + Cell;
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
							// A piece running past the end of the wall isn't a spot along it
							if (!cells.TrueForAll(grid.Inside))
							{
								continue;
							}
							float distance = Vector3.Distance(Origin(size, rotation, cells), point);
							byte hindrance = grid.Hindrance(cells);
							if (hindrance != FloorGrid.Free)
							{
								if (distance <= reach)
								{
									CountHindrance(hindrance);
								}
								continue;
							}
							if (distance > reach + slideMost)
							{
								missFar++;
								continue;
							}
							spots.Add(new WallSpot { Gap = gap, Quarter = quarter, Offset = offset, Distance = distance });
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
					if (Vector3.Distance(Placed[Placed.Count - 1].Position, point) > reach)
					{
						SlideToward(Placed.Count - 1, size, point, spot, width, depth);
					}
					// Pushed back against the wall, or slid along it no farther than it could, it can end up out of reach
					if (Vector3.Distance(Placed[Placed.Count - 1].Position, point) > reach)
					{
						missPushedFar++;
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
			/// Slides a piece laid out against a wall along it toward a point, until it meets something standing - like the
			/// crafting station it's an upgrade of - so it stands as near the point as it can: no farther than the room
			/// its cells leave beside it, or a cell more when the cells just past it are blocked by something standing,
			/// which is the room between it and what stands there
			/// </summary>
			/// <remarks>
			/// A piece takes whole cells, so two pieces side by side stand apart by the room their cells leave - enough
			/// to put a station's upgrade out of its reach
			/// </remarks>
			/// <param name="index">The piece's place among the pieces laid out</param>
			/// <param name="size">Its size</param>
			/// <param name="point">The point</param>
			/// <param name="spot">The spot against the wall its cells are at</param>
			/// <param name="width">How many cells it takes along the wall</param>
			/// <param name="depth">How many cells it takes out from the wall</param>
			private void SlideToward(int index, Bounds size, Vector3 point, WallSpot spot, int width, int depth)
			{
				Placement placement = Placed[index];
				// A higher offset along the wall is toward the piece's right
				Vector3 along = placement.Rotation * Vector3.right;
				float toPoint = Vector3.Dot(point - placement.Position, along);
				if (Mathf.Abs(toPoint) < 0.01f)
				{
					return;
				}
				Vector3 direction = ((toPoint > 0f) ? along : -along);
				float most = SlideRoom(width, size.size.x);
				List<Vector2Int> past = grid.WallCells(spot.Quarter, ((toPoint > 0f) ? spot.Offset + width : spot.Offset - 1), spot.Gap, 1, depth);
				if (past.TrueForAll((Vector2Int cell) => grid.Inside(cell) && grid.Cells[cell.x, cell.y] == FloorGrid.Blocked && !grid.Floorless[cell.x, cell.y]))
				{
					most += Cell;
				}
				most = Mathf.Min(most, Mathf.Abs(toPoint));
				PieceBox(size, placement.Position, placement.Rotation, out Vector3 center, out Vector3 half);
				float slide = most;
				if (Physics.BoxCast(center, half, direction, out RaycastHit hit, placement.Rotation, most + FitSlack, solidMask, QueryTriggerInteraction.Ignore))
				{
					slide = Mathf.Min(most, hit.distance - FitSlack);
				}
				if (slide <= 0f)
				{
					return;
				}
				placement.Position += direction * slide;
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
				byte hindrance = grid.Hindrance(taken);
				if (hindrance != FloorGrid.Free)
				{
					CountHindrance(hindrance);
					return(false);
				}
				missTried++;
				Vector3 position = Origin(size, rotation, taken);
				PieceBox(size, position, rotation, out Vector3 boxCenter, out Vector3 half);
				if (Physics.CheckBox(boxCenter, half, rotation, pieceMask, QueryTriggerInteraction.Ignore))
				{
					missBlocked++;
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
				// So do the beds and chests the house came with, so the vikings can get into them
				pieces.AddRange(standingBeds);
				pieces.AddRange(standingChests);
				if (!grid.Roomy(pieces))
				{
					missCramped++;
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
		/// Works out the room a piece's cells leave on each side of it one way, with the piece in their middle
		/// </summary>
		/// <param name="cells">How many cells it takes that way</param>
		/// <param name="size">The piece's size that way, in meters</param>
		/// <returns>The room, in meters, or 0 when it fills them</returns>
		internal static float SlideRoom(int cells, float size)
		{
			return(Mathf.Max(0f, (cells * Cell - size) / 2f));
		}

		/// <summary>
		/// Describes what blocked a house's cells for the log - how many have no floor, then how many each thing stood
		/// on, the most first and by name among those alike
		/// </summary>
		/// <param name="floorless">How many cells have no floor</param>
		/// <param name="standing">How many cells each thing stood on, by its name</param>
		/// <returns>The description, like "8 with no floor, 30 by woodwall, 12 by wood_pole"</returns>
		internal static string DescribeBlocked(int floorless, Dictionary<string, int> standing)
		{
			List<KeyValuePair<string, int>> counts = new List<KeyValuePair<string, int>>(standing);
			counts.Sort((KeyValuePair<string, int> a, KeyValuePair<string, int> b) => ((a.Value != b.Value) ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key)));
			List<string> parts = new List<string> { floorless + " with no floor" };
			foreach (KeyValuePair<string, int> count in counts)
			{
				parts.Add(count.Value + " by " + count.Key);
			}
			return(string.Join(", ", parts));
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
			/// Whether each blocked cell is blocked for having no floor, or a floor too far above or below the house's,
			/// rather than for something standing on it, by column and row
			/// </summary>
			public readonly bool[,] Floorless;

			/// <summary>
			/// The four steps from a cell to the cells beside it
			/// </summary>
			private static readonly Vector2Int[] Steps = new Vector2Int[] { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(1, 0) };

			/// <summary>
			/// The eight steps from a cell to the cells around it, corners included
			/// </summary>
			private static readonly Vector2Int[] Around = new Vector2Int[] { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(-1, -1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(1, 1) };

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
				Floorless = new bool[Mathf.Max(columns, 0), Mathf.Max(rows, 0)];
			}

			/// <summary>
			/// Counts the cells blocked for having no floor
			/// </summary>
			/// <returns>How many there are</returns>
			public int CountFloorless()
			{
				int count = 0;
				for (int column = 0; column < Columns; column++)
				{
					for (int row = 0; row < Rows; row++)
					{
						count += ((Floorless[column, row] && Cells[column, row] == Blocked) ? 1 : 0);
					}
				}
				return(count);
			}

			/// <summary>
			/// Counts the cells that are free
			/// </summary>
			/// <returns>How many there are</returns>
			public int CountFree()
			{
				int count = 0;
				for (int column = 0; column < Columns; column++)
				{
					for (int row = 0; row < Rows; row++)
					{
						count += ((Cells[column, row] == Free) ? 1 : 0);
					}
				}
				return(count);
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
			/// Checks whether a viking has room to stand on a cell - it and every cell around it, corners included, can be
			/// walked across, with the grid's edge counting as the wall it is. The game's navmesh keeps a viking 0.4
			/// meters from anything solid, more than the quarter meter from a cell's middle to its edge, so a way through
			/// has to be three cells wide - a gap of one or two cells, up to a meter, has no navmesh across it
			/// </summary>
			/// <param name="cell">The cell</param>
			/// <returns>True when it has</returns>
			public bool RoomToStand(Vector2Int cell)
			{
				if (!Walkable(cell))
				{
					return(false);
				}
				foreach (Vector2Int step in Around)
				{
					if (!Walkable(cell + step))
					{
						return(false);
					}
				}
				return(true);
			}

			/// <summary>
			/// Checks whether a cell is reached from the way in - stood on, or one a viking standing beside it reaches
			/// across, like the cells along a wall or beside a piece
			/// </summary>
			/// <param name="cell">The cell</param>
			/// <param name="reached">Whether a viking can stand on each cell and walk to it from the way in (see Reach)</param>
			/// <returns>True when it is</returns>
			public bool ReachedAcross(Vector2Int cell, bool[,] reached)
			{
				if (!Walkable(cell))
				{
					return(false);
				}
				if (reached[cell.x, cell.y])
				{
					return(true);
				}
				foreach (Vector2Int step in Around)
				{
					Vector2Int next = cell + step;
					if (Inside(next) && reached[next.x, next.y])
					{
						return(true);
					}
				}
				return(false);
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
			/// Works out what keeps some cells from all being free, the longest lasting first - a cell off the grid, with no
			/// floor or something standing on it, then one kept clear, then one taken by a piece laid out
			/// </summary>
			/// <param name="cells">The cells</param>
			/// <returns>Blocked, KeepClear or Taken, or Free when they're all free</returns>
			public byte Hindrance(List<Vector2Int> cells)
			{
				byte worst = Free;
				foreach (Vector2Int cell in cells)
				{
					byte state = (Inside(cell) ? Cells[cell.x, cell.y] : Blocked);
					if (state == Blocked)
					{
						return(Blocked);
					}
					if (state == KeepClear || (state == Taken && worst == Free))
					{
						worst = state;
					}
				}
				return(worst);
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
			/// Makes the way into a house no door gave a way into start from its middlemost cell with room to stand on (see
			/// RoomToStand), or else its middlemost cell that isn't blocked
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
				bool bestRoomy = false;
				for (int column = 0; column < Columns; column++)
				{
					for (int row = 0; row < Rows; row++)
					{
						Vector2Int cell = new Vector2Int(column, row);
						if (Cells[column, row] == Blocked)
						{
							continue;
						}
						float distance = (cell - middle).sqrMagnitude;
						bool roomy = RoomToStand(cell);
						if ((roomy && !bestRoomy) || (roomy == bestRoomy && distance < bestDistance))
						{
							best = cell;
							bestDistance = distance;
							bestRoomy = roomy;
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
			/// with are reached from the way in (see Reach), and every piece can be walked up to (see Reachable)
			/// </summary>
			/// <param name="pieces">The cells each piece that has to stay reachable takes</param>
			/// <returns>True when it is</returns>
			public bool Roomy(List<List<Vector2Int>> pieces)
			{
				bool[,] reached = Reach(out int reachedFree);
				if (reachedFree < FreeAtStart * FreeShareMin)
				{
					return(false);
				}
				foreach (List<Vector2Int> piece in pieces)
				{
					if (!Reachable(piece, reached))
					{
						return(false);
					}
				}
				return(true);
			}

			/// <summary>
			/// Finds the cells a viking can stand on and walk to from the way in, stepping only onto cells with room to
			/// stand on (see RoomToStand), so a gap narrower than three cells isn't a way through
			/// </summary>
			/// <param name="reachedFree">How many free cells are reached - stood on, or reached across from a cell
			/// beside them (see ReachedAcross)</param>
			/// <returns>Whether a viking can stand on each cell and walk to it from the way in, by column and row</returns>
			public bool[,] Reach(out int reachedFree)
			{
				bool[,] reached = new bool[Columns, Rows];
				Queue<Vector2Int> open = new Queue<Vector2Int>();
				foreach (Vector2Int entry in Entries)
				{
					if (RoomToStand(entry) && !reached[entry.x, entry.y])
					{
						reached[entry.x, entry.y] = true;
						open.Enqueue(entry);
					}
				}
				while (open.Count > 0)
				{
					Vector2Int cell = open.Dequeue();
					foreach (Vector2Int step in Steps)
					{
						Vector2Int next = cell + step;
						if (RoomToStand(next) && !reached[next.x, next.y])
						{
							reached[next.x, next.y] = true;
							open.Enqueue(next);
						}
					}
				}
				reachedFree = 0;
				for (int column = 0; column < Columns; column++)
				{
					for (int row = 0; row < Rows; row++)
					{
						if (Cells[column, row] == Free && ReachedAcross(new Vector2Int(column, row), reached))
						{
							reachedFree++;
						}
					}
				}
				return(reached);
			}

			/// <summary>
			/// Checks whether a piece can be walked up to - one of its sides has a gap at least two cells deep out from
			/// it, a meter, whose cell farther out a viking can stand on and walk to from the way in (see Reach), since
			/// the cell beside the piece is too near it to stand on
			/// </summary>
			/// <param name="piece">The cells the piece takes</param>
			/// <param name="reached">Whether a viking can stand on each cell and walk to it from the way in (see
			/// Reach)</param>
			/// <returns>True when it can</returns>
			public bool Reachable(List<Vector2Int> piece, bool[,] reached)
			{
				foreach (Vector2Int cell in piece)
				{
					foreach (Vector2Int step in Steps)
					{
						Vector2Int next = cell + step;
						Vector2Int beyond = next + step;
						if (Walkable(next) && Walkable(beyond) && reached[beyond.x, beyond.y])
						{
							return(true);
						}
					}
				}
				return(false);
			}

			/// <summary>
			/// Draws the grid for the log, a character a cell, its last row first - a cell marked with a piece shows the
			/// piece's mark, and the others show their state: "#" for something standing on it, "_" for no floor, "E" for
			/// a way in that's reached, "." for a free cell a viking can stand on and walk to from the way in, ":" for one
			/// it only reaches across from a cell beside it (see ReachedAcross), "," for one it doesn't reach, "+" and "-"
			/// for cells kept clear that are reached and aren't, and "x" for a cell taken by a piece with no mark
			/// </summary>
			/// <param name="reached">Whether a viking can stand on each cell and walk to it from the way in (see
			/// Reach)</param>
			/// <param name="marks">The mark of each cell a piece stands on, by its column and row</param>
			/// <returns>The rows, the last first</returns>
			public List<string> Draw(bool[,] reached, Dictionary<Vector2Int, char> marks)
			{
				List<string> lines = new List<string>();
				for (int row = Rows - 1; row >= 0; row--)
				{
					char[] line = new char[Columns];
					for (int column = 0; column < Columns; column++)
					{
						Vector2Int cell = new Vector2Int(column, row);
						bool walked = ReachedAcross(cell, reached);
						byte state = Cells[column, row];
						if (marks.TryGetValue(cell, out char mark))
						{
							line[column] = mark;
						}
						else if (state == Blocked)
						{
							line[column] = (Floorless[column, row] ? '_' : '#');
						}
						else if (state == Taken)
						{
							line[column] = 'x';
						}
						else if (walked && Entries.Contains(cell))
						{
							line[column] = 'E';
						}
						else if (state == KeepClear)
						{
							line[column] = (walked ? '+' : '-');
						}
						else
						{
							line[column] = (reached[column, row] ? '.' : (walked ? ':' : ','));
						}
					}
					lines.Add(new string(line));
				}
				return(lines);
			}
		}
	}
}
