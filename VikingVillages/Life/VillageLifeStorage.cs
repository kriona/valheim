using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// The village's chests - those loaded near the viking that it may use, finding one with room for what it carries,
	/// walking to it and opening it, putting things in, and dumping what no chest has room for
	/// </summary>
	internal partial class VillageLife
	{
		/// <summary>
		/// Lists the village's chests the viking may use - public ones in the village, not on a cart or ship, not a
		/// tombstone, not open, and not one a player keeps the vikings out of - leaving out those it couldn't get to lately
		/// </summary>
		/// <returns>The chests</returns>
		private List<Container> UsableChests()
		{
			List<Container> chests = new List<Container>();
			foreach (Container chest in LoadedChests())
			{
				if (chest != null && Usable(chest))
				{
					chests.Add(chest);
				}
			}
			return(chests);
		}

		/// <summary>
		/// How often the loaded chests, items on the ground, beehives and pickables are listed again for the vikings to
		/// look through, in seconds
		/// </summary>
		private const float LoadedListTime = 2f;

		/// <summary>
		/// The chests, items on the ground, beehives and pickables loaded when they were last listed, shared by every
		/// viking this client runs - some may have been destroyed since, so each is checked before it's used
		/// </summary>
		private static readonly List<Container> loadedChests = new List<Container>();
		private static readonly List<ItemDrop> loadedDrops = new List<ItemDrop>();
		private static readonly List<Beehive> loadedHives = new List<Beehive>();
		private static readonly List<Pickable> loadedPickables = new List<Pickable>();

		/// <summary>
		/// When the loaded objects were last listed, in Time.time seconds, or -1 before they have been
		/// </summary>
		private static float loadedListedAt = -1f;

		/// <summary>
		/// Lists the loaded chests, items on the ground, beehives and pickables in one pass over the scene, unless they
		/// were listed less than LoadedListTime seconds ago - so a list doesn't change within a frame, and can be gone
		/// through while what's called along the way looks at it too
		/// </summary>
		private static void ListLoaded()
		{
			if (loadedListedAt >= 0f && Time.time - loadedListedAt < LoadedListTime)
			{
				return;
			}
			loadedListedAt = Time.time;
			loadedChests.Clear();
			loadedDrops.Clear();
			loadedHives.Clear();
			loadedPickables.Clear();
			if (ZNetScene.instance == null)
			{
				return;
			}
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZNetView view = instance.Value;
				if (view == null)
				{
					continue;
				}
				if (view.TryGetComponent(out Container chest))
				{
					loadedChests.Add(chest);
				}
				if (view.TryGetComponent(out ItemDrop drop))
				{
					loadedDrops.Add(drop);
				}
				if (view.TryGetComponent(out Beehive hive))
				{
					loadedHives.Add(hive);
				}
				if (view.TryGetComponent(out Pickable pickable))
				{
					loadedPickables.Add(pickable);
				}
			}
		}

		/// <summary>
		/// The loaded chests (see ListLoaded)
		/// </summary>
		/// <returns>The chests, some of which may have been destroyed since they were listed</returns>
		private static List<Container> LoadedChests()
		{
			ListLoaded();
			return(loadedChests);
		}

		/// <summary>
		/// The loaded items on the ground (see ListLoaded)
		/// </summary>
		/// <returns>The items, some of which may have been picked up or destroyed since they were listed</returns>
		private static List<ItemDrop> LoadedDrops()
		{
			ListLoaded();
			return(loadedDrops);
		}

		/// <summary>
		/// The loaded beehives (see ListLoaded)
		/// </summary>
		/// <returns>The beehives, some of which may have been destroyed since they were listed</returns>
		private static List<Beehive> LoadedHives()
		{
			ListLoaded();
			return(loadedHives);
		}

		/// <summary>
		/// The loaded pickables (see ListLoaded)
		/// </summary>
		/// <returns>The pickables, some of which may have been destroyed since they were listed</returns>
		private static List<Pickable> LoadedPickables()
		{
			ListLoaded();
			return(loadedPickables);
		}

		/// <summary>
		/// Checks whether the viking may use a chest
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>True when it's a public chest of the village that's free to use</returns>
		private bool Usable(Container chest)
		{
			if (!IsVillageChest(village, chest))
			{
				return(false);
			}
			ZDO zdo = chest.m_nview.GetZDO();
			if (Avoided(zdo.m_uid) || HeldByPlayer(chest))
			{
				return(false);
			}
			// The chest this viking has open for itself is in use only by it
			bool inUse = (zdo.GetInt(ZDOVars.s_inUse) != 0 || chest.IsInUse());
			return(!inUse || chest == openChest);
		}

		/// <summary>
		/// Checks whether a chest is one of a village's, which its vikings use - one in the village (see ChestOfVillage)
		/// that no player keeps them out of (see VillageChestUse)
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="chest">The chest</param>
		/// <returns>True when it is</returns>
		private static bool IsVillageChest(Village village, Container chest)
		{
			return(ChestOfVillage(village, chest) && !VillageChestUse.KeptOut(chest));
		}

		/// <summary>
		/// Checks whether a chest is one a village's vikings would use unless a player keeps them out of it - a public one
		/// in the village or its houses, whoever built it, not on a cart or ship, not a tombstone and not one of the game's
		/// treasure chests
		/// </summary>
		/// <param name="village">The village, or null</param>
		/// <param name="chest">The chest</param>
		/// <returns>True when it is</returns>
		internal static bool ChestOfVillage(Village village, Container chest)
		{
			if (village == null || chest == null || chest.m_nview == null || !chest.m_nview.IsValid() || chest.m_privacy != Container.PrivacySetting.Public)
			{
				return(false);
			}
			if (chest.m_rootObjectOverride != null || chest.m_wagon != null || chest.m_autoDestroyEmpty || IsTreasureChest(chest) || chest.GetComponent<TombStone>() != null || chest.GetComponentInParent<Ship>() != null)
			{
				return(false);
			}
			Vector3 position = chest.transform.position;
			return(village.Contains(position) || village.InHouse(position, 1f));
		}

		/// <summary>
		/// On a chest of the game's treasure chests that a farm, or a house its vikings take in, was generated with, that
		/// it's the village's own (see FarmSpawns.OnChestAwake)
		/// </summary>
		public static readonly int OwnChestKey = "kriona.VikingVillages.ownChest".GetStableHashCode();

		/// <summary>
		/// Checks whether a chest is one of the game's treasure chests - buried in the ground, or standing in a ruin or
		/// a crypt - which fill themselves with loot the first time they load, unlike a chest built as a piece, which
		/// starts empty. One can lie inside a village, but it's the world's and not the village's - unless the village
		/// was generated with it (see OwnChestKey)
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>True when it is</returns>
		internal static bool IsTreasureChest(Container chest)
		{
			if (!HasDefaultLoot(chest))
			{
				return(false);
			}
			ZNetView view = ((chest.m_nview != null) ? chest.m_nview : chest.GetComponent<ZNetView>());
			ZDO zdo = ((view != null && view.IsValid()) ? view.GetZDO() : null);
			return(zdo == null || !zdo.GetBool(OwnChestKey));
		}

		/// <summary>
		/// Checks whether a chest's prefab fills it with loot the first time it loads, as the game's treasure chests do
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>True when it does</returns>
		internal static bool HasDefaultLoot(Container chest)
		{
			return(chest.m_defaultItems != null && chest.m_defaultItems.m_drops.Count > 0);
		}

		/// <summary>
		/// Lists a village's loaded chests, the ones its vikings use (see IsVillageChest)
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The chests</returns>
		public static List<Container> VillageChests(Village village)
		{
			return(LoadedChests().FindAll((Container chest) => IsVillageChest(village, chest)));
		}

		/// <summary>
		/// Checks whether another player's game owns a chest while that player stands at it - as it does once they've
		/// asked to open it, before their game marks it in use, and taking it then would lose what they move in it
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>True when it does</returns>
		private static bool HeldByPlayer(Container chest)
		{
			long owner = chest.m_nview.GetZDO().GetOwner();
			if (owner == 0L || owner == ZDOMan.GetSessionID())
			{
				return(false);
			}
			Vector3 position = chest.transform.position;
			foreach (Player player in Player.GetAllPlayers())
			{
				ZDO zdo = ((player != null && player.m_nview != null) ? player.m_nview.GetZDO() : null);
				if (zdo != null && zdo.GetOwner() == owner && Vector3.Distance(player.transform.position, position) <= PlayerChestRange)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether a point is inside an active ward
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>True when it is</returns>
		public static bool Warded(Vector3 point)
		{
			foreach (PrivateArea area in PrivateArea.m_allAreas)
			{
				if (area != null && area.IsEnabled() && area.IsInside(point, 0f))
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Finds the chest to put an item in, among the village's chests the viking may use that have room for all of it
		/// - the nearest whose unfinished stacks of the item take all of it, so no slot is used, or else the nearest with
		/// an unfinished stack of it to fill before the rest goes in a slot beside it, or else the nearest that already
		/// holds some, keeping the item together, or else the nearest at all
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>The chest, or null when none has room</returns>
		private Container FindChestFor(ItemDrop.ItemData item)
		{
			List<Container> chests = UsableChests();
			return(NearestStacking(chests.FindAll((Container chest) => HasRoom(chest, item, chests)), item, item.m_stack));
		}

		/// <summary>
		/// Picks the chest to put an amount of an item in, among chests with room for it - the nearest whose unfinished
		/// stacks of the item take all of it, so no slot is used, or else the nearest with an unfinished stack of it to
		/// fill before the rest goes in a slot beside it, or else the nearest that already holds some, keeping the item
		/// together, or else the nearest at all - so an item is topped up to full stacks before it's spread over new ones
		/// </summary>
		/// <param name="fits">The chests with room for it</param>
		/// <param name="item">The item</param>
		/// <param name="amount">How much of it goes in</param>
		/// <returns>The chest, or null when there's none</returns>
		private Container NearestStacking(List<Container> fits, ItemDrop.ItemData item, int amount)
		{
			Container chosen = Nearest(fits.FindAll((Container chest) => StackRoom(chest, item) >= amount));
			if (chosen == null)
			{
				chosen = Nearest(fits.FindAll((Container chest) => StackRoom(chest, item) > 0));
			}
			if (chosen == null)
			{
				chosen = Nearest(fits.FindAll((Container chest) => chest.GetInventory().GetAllItems().Exists((ItemDrop.ItemData stored) => SameStack(stored, item))));
			}
			return((chosen != null) ? chosen : Nearest(fits));
		}

		/// <summary>
		/// Works out how much more of an item a chest's unfinished stacks of it hold
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="item">The item</param>
		/// <returns>How many more fit on its stacks</returns>
		private static int StackRoom(Container chest, ItemDrop.ItemData item)
		{
			int maxStack = Mathf.Max(1, item.m_shared.m_maxStackSize);
			int room = 0;
			foreach (ItemDrop.ItemData stored in chest.GetInventory().GetAllItems())
			{
				if (SameStack(stored, item))
				{
					room += Mathf.Max(0, maxStack - stored.m_stack);
				}
			}
			return(room);
		}

		/// <summary>
		/// Checks whether an item stored in a chest is one an item goes onto when it's put in, the way the game stacks
		/// them - the same item, quality and world level
		/// </summary>
		/// <param name="stored">The item in the chest</param>
		/// <param name="item">The item put in</param>
		/// <returns>True when it stacks onto it</returns>
		private static bool SameStack(ItemDrop.ItemData stored, ItemDrop.ItemData item)
		{
			return(StackMerge.SameStack(stored, item));
		}

		/// <summary>
		/// Checks whether a chest has room for all of an item - see ChestRoom
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="item">The item</param>
		/// <param name="chests">The village's chests the vikings may use, for counting its stacks of an item they don't
		/// use</param>
		/// <returns>True when it has</returns>
		private static bool HasRoom(Container chest, ItemDrop.ItemData item, List<Container> chests)
		{
			return(ChestRoom(chest, item, chests) >= item.m_stack);
		}

		/// <summary>
		/// Checks whether a viking wants an item lying around the village - when a chest has room for it, or it's
		/// something the vikings don't use, which it drops outside the village when the chests hold enough of it and it
		/// may leave
		/// </summary>
		/// <param name="item">The item</param>
		/// <param name="chests">The village's chests the viking may use</param>
		/// <param name="mayLeave">Whether it may leave the village (see MayLeave)</param>
		/// <returns>True when it wants it</returns>
		private static bool WantsLoot(ItemDrop.ItemData item, List<Container> chests, bool mayLeave)
		{
			return(chests.Exists((Container chest) => HasRoom(chest, item, chests)) || (mayLeave && KindOf(item) == StoreKind.Other));
		}

		/// <summary>
		/// Counts the stacks of an item in some chests
		/// </summary>
		/// <param name="item">The item</param>
		/// <param name="chests">The chests</param>
		/// <returns>How many stacks hold it</returns>
		private static int StacksIn(ItemDrop.ItemData item, List<Container> chests)
		{
			int stacks = 0;
			foreach (Container chest in chests)
			{
				foreach (ItemDrop.ItemData stored in chest.GetInventory().GetAllItems())
				{
					if (stored.m_shared.m_name == item.m_shared.m_name && stored.m_worldLevel == item.m_worldLevel)
					{
						stacks++;
					}
				}
			}
			return(stacks);
		}

		/// <summary>
		/// What a chest holds, by what the vikings keep a slot free for
		/// </summary>
		internal enum StoreKind
		{
			Wood,
			Food,
			Other
		}

		/// <summary>
		/// Works out what kind of thing an item is, for storing it - raw meat and the rest a cooking station cooks count
		/// as food
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>Wood, food or anything else</returns>
		private static StoreKind KindOf(ItemDrop.ItemData item)
		{
			if (IsWoodItem(item))
			{
				return(StoreKind.Wood);
			}
			return((VikingFood.IsFood(item) || VillageCooking.IsRaw(item)) ? StoreKind.Food : StoreKind.Other);
		}

		/// <summary>
		/// Works out how many of an item the vikings may put in a chest - onto its stacks of the item, and into its empty
		/// slots, keeping one free for wood until it holds some wood and one for food until it holds some food, so loot
		/// never crowds out what they gather - for wood, while no more than ChestWoodShare of its slots hold wood, and for
		/// anything else, a new stack only when the chest has none of it and the village's chests have fewer than
		/// VillageStacksMax
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="item">The item</param>
		/// <param name="chests">The village's chests the vikings may use, for counting its stacks of an item they don't
		/// use, or null for wood and food</param>
		/// <returns>How many fit</returns>
		private static int ChestRoom(Container chest, ItemDrop.ItemData item, List<Container> chests)
		{
			Inventory store = chest.GetInventory();
			StoreKind kind = KindOf(item);
			int slots = store.m_width * store.m_height;
			int maxStack = Mathf.Max(1, item.m_shared.m_maxStackSize);
			int woodSlots = 0;
			bool holdsFood = false;
			bool holdsItem = false;
			int stackRoom = 0;
			foreach (ItemDrop.ItemData stored in store.GetAllItems())
			{
				StoreKind storedKind = KindOf(stored);
				if (storedKind == StoreKind.Wood)
				{
					woodSlots++;
				}
				else if (storedKind == StoreKind.Food)
				{
					holdsFood = true;
				}
				if (stored.m_shared.m_name == item.m_shared.m_name && stored.m_worldLevel == item.m_worldLevel)
				{
					stackRoom += Mathf.Max(0, maxStack - stored.m_stack);
					holdsItem = true;
				}
			}
			bool counted = (kind == StoreKind.Other && chests != null && (!holdsItem || maxStack == 1));
			int villageStacks = (counted ? StacksIn(item, chests) : 0);
			return(ChestRoomFor(kind, slots, store.m_inventory.Count, woodSlots, holdsFood, holdsItem, villageStacks, KeptStacks(item), stackRoom, maxStack));
		}

		/// <summary>
		/// Works out how many of an item the vikings may put in a chest from what the chest holds - the room on its
		/// stacks of the item, and its empty slots less one kept for wood until it holds wood and one for food until it
		/// holds food, for wood only while no more than ChestWoodShare of its slots hold wood, and for anything else
		/// one new stack only while the village has fewer stacks of it than it keeps - and for an item that stacks, only
		/// when the chest has none of it, while one that doesn't stack, like a tool, can go beside another
		/// </summary>
		/// <param name="kind">What kind of thing the item is</param>
		/// <param name="slots">How many slots the chest has</param>
		/// <param name="used">How many of its slots hold something</param>
		/// <param name="woodSlots">How many of its slots hold wood</param>
		/// <param name="holdsFood">Whether it holds any food</param>
		/// <param name="holdsItem">Whether it holds any of the item</param>
		/// <param name="villageStacks">How many stacks of the item the village's chests hold, counted for anything
		/// but wood and food</param>
		/// <param name="stacksKept">How many stacks of the item the village keeps (see KeptStacks)</param>
		/// <param name="stackRoom">How many more of the item its stacks of it take</param>
		/// <param name="maxStack">How many of the item make a full stack</param>
		/// <returns>How many fit, never less than none</returns>
		internal static int ChestRoomFor(StoreKind kind, int slots, int used, int woodSlots, bool holdsFood, bool holdsItem, int villageStacks, int stacksKept, int stackRoom, int maxStack)
		{
			int reserved = ((kind != StoreKind.Wood && woodSlots == 0) ? 1 : 0) + ((kind != StoreKind.Food && !holdsFood) ? 1 : 0);
			int newSlots = slots - used - reserved;
			if (kind == StoreKind.Wood)
			{
				newSlots = Mathf.Min(newSlots, Mathf.FloorToInt(slots * ChestWoodShare) - woodSlots);
			}
			else if (kind == StoreKind.Other)
			{
				bool enough = ((holdsItem && maxStack > 1) || villageStacks >= stacksKept);
				newSlots = (enough ? 0 : Mathf.Min(newSlots, 1));
			}
			return(stackRoom + Mathf.Max(0, newSlots) * maxStack);
		}

		/// <summary>
		/// Where the vikings stand to use each chest, by its ZDO, and when that was found, by Time.time - the nearest a
		/// viking can stand to it, which can be a little way off when what's around it, like a bed or the wall it stands
		/// against, keeps the floor right beside it out of reach
		/// </summary>
		private static readonly Dictionary<ZDOID, KeyValuePair<float, Vector3>> chestStands = new Dictionary<ZDOID, KeyValuePair<float, Vector3>>();

		/// <summary>
		/// How far from a chest the spot to stand at to use it is looked for, in meters, and how long a spot found is
		/// kept before it's looked for again, in seconds, since what stands around the chest can change
		/// </summary>
		private const float ChestStandSearch = 2.5f;
		private const float ChestStandLife = 30f;

		/// <summary>
		/// Checks whether the viking is near enough a chest to use it - within ChestReach of it, or at the spot to stand
		/// at to use it (see ChestStand)
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>True when it is</returns>
		internal bool AtChest(Container chest)
		{
			float toChest = Utils.DistanceXZ(chest.transform.position, transform.position);
			if (toChest <= ChestReach)
			{
				return(true);
			}
			return(ChestStand(chest, out Vector3 stand) && InReach(toChest, Utils.DistanceXZ(stand, transform.position), ChestReach));
		}

		/// <summary>
		/// Walks the viking to the spot to stand at to use a chest, near enough that it's within reach of the chest
		/// there, rather than to the chest itself, whose own spot can't be stood on
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="chest">The chest</param>
		internal void GoToChest(float dt, Container chest)
		{
			if (!ChestStand(chest, out Vector3 stand))
			{
				Go(dt, chest.transform.position, ChestReach, false);
				return;
			}
			Go(dt, stand, StandReach(Utils.DistanceXZ(stand, chest.transform.position), ChestReach), false);
		}

		/// <summary>
		/// Finds the spot to stand at to use a chest - the nearest point a viking can stand within ChestStandSearch of
		/// it, found again every ChestStandLife
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="stand">The spot</param>
		/// <returns>False when there's none</returns>
		private static bool ChestStand(Container chest, out Vector3 stand)
		{
			stand = chest.transform.position;
			if (chest.m_nview == null || !chest.m_nview.IsValid())
			{
				return(false);
			}
			ZDOID id = chest.m_nview.GetZDO().m_uid;
			if (chestStands.TryGetValue(id, out KeyValuePair<float, Vector3> known) && Time.time - known.Key < ChestStandLife)
			{
				stand = known.Value;
				return(true);
			}
			if (!NavmeshPoints.FindValidPoint(out Vector3 valid, chest.transform.position, ChestStandSearch, Pathfinding.AgentType.Humanoid))
			{
				return(false);
			}
			chestStands[id] = new KeyValuePair<float, Vector3>(Time.time, valid);
			stand = valid;
			return(true);
		}

		/// <summary>
		/// Takes a chest over from whoever owns it so the viking can change what's in it, with what's in it brought up
		/// to date and its unfinished stacks merged (see StackMerge), unless it's no longer free to use
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>False when it can't be used</returns>
		private bool OpenChest(Container chest)
		{
			if (!Usable(chest))
			{
				return(false);
			}
			NoteChestReached(chest);
			if (!chest.m_nview.IsOwner())
			{
				chest.m_nview.ClaimOwnership();
			}
			// The game doesn't load a chest marked in use, which ShowChestOpen has done, so the mark is lifted while it
			// loads - except while the local player has it open, when what's in it is already up to date and loading
			// would swap out the items under their cursor
			if (InventoryGui.instance == null || InventoryGui.instance.m_currentContainer != chest)
			{
				bool inUse = chest.m_inUse;
				chest.m_inUse = false;
				chest.Load();
				chest.m_inUse = inUse;
				// It tidies the chest as it opens it, merging unfinished stacks of an item so each item takes as few slots
				// as it fits in
				StackMerge.Merge(chest.GetInventory());
			}
			return(true);
		}

		/// <summary>
		/// Puts the loot the viking carries in a chest it has reached, when the chest still has room for all of it
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="loot">The loot, in the viking's inventory</param>
		/// <returns>True when it put it in</returns>
		private bool PutLoot(Container chest, ItemDrop.ItemData loot)
		{
			if (!OpenChest(chest))
			{
				return(false);
			}
			Inventory store = chest.GetInventory();
			if (!HasRoom(chest, loot, UsableChests()))
			{
				return(false);
			}
			ItemDrop.ItemData item = loot.Clone();
			item.m_customData.Remove(VikingItems.CarryKey);
			item.m_equipped = false;
			if (!store.AddItem(item))
			{
				return(false);
			}
			TestLog.Count(LoggedCraftLeftover(loot) ? "Put back after crafting" : "Put in chests", TestLog.ItemName(item), item.m_stack);
			humanoid.GetInventory().RemoveItem(loot);
			chest.m_closeEffects.Create(chest.transform.position, chest.transform.rotation);
			return(true);
		}

		/// <summary>
		/// Puts the loot the viking carries down on the ground in front of it - when there's no chest it can put it in,
		/// or where it drops what the chests hold enough of, marked so no viking stores it again
		/// </summary>
		/// <param name="discard">True to mark it as not to be stored</param>
		/// <param name="reason">Why it's put down, for the log</param>
		private void PutDownLoot(bool discard, string reason)
		{
			ItemDrop.ItemData loot = CarriedLoot();
			if (loot == null)
			{
				return;
			}
			humanoid.GetInventory().RemoveItem(loot);
			loot.m_customData.Remove(VikingItems.CarryKey);
			TestLog.Count(discard ? "Dropped outside, as the chests hold enough" : "Put down, with no chest for it", TestLog.ItemName(loot), loot.m_stack);
			Plugin.Log.LogInfo(LogName() + " put down " + ((loot.m_stack > 1) ? loot.m_stack + " " : "") + Localization.instance.Localize(loot.m_shared.m_name) + " " + (village.Contains(transform.position) ? "in" : "outside") + " the village - " + reason);
			ItemDrop dropped = ItemDrop.DropItem(loot, loot.m_stack, transform.position + transform.forward * 0.6f + Vector3.up * 0.5f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
			if (discard && dropped != null && dropped.m_nview != null && dropped.m_nview.IsValid())
			{
				dropped.m_nview.GetZDO().Set(DiscardKey, true);
			}
		}

		/// <summary>
		/// Finds where to drop an item the chests hold enough of - beside what the village's vikings dropped outside it
		/// before while any of it is still there (see FindDumpPile), so it all lies in one pile; or else outside the
		/// village, in the direction the viking is from its middle where it can - on ground a viking can stand on, away
		/// from enemies and far enough from every player's base that the item despawns as usual
		/// </summary>
		/// <param name="spot">The spot</param>
		/// <returns>False when there's none within DumpReach of the village's edge, or the pile has an enemy near it</returns>
		private bool FindDumpSpot(out Vector3 spot)
		{
			spot = Vector3.zero;
			if (FindDumpPile(out Vector3 pile))
			{
				if (Pathfinding.instance != null && NavmeshPoints.FindValidPoint(out Vector3 beside, pile, DumpPileReach, Pathfinding.AgentType.Humanoid))
				{
					if (EnemyNear(beside, ForageSafety))
					{
						return(false);
					}
					spot = beside;
					return(true);
				}
			}
			Vector3 away = transform.position - village.Middle;
			away.y = 0f;
			away = ((away.sqrMagnitude > 0.01f) ? away.normalized : Vector3.forward);
			for (int i = 0; i < DumpTries; i++)
			{
				// The first tries head straight out from the viking's side, the later ones fan out around the village
				Vector3 direction = Quaternion.Euler(0f, Random.Range(-1f, 1f) * 180f * i / DumpTries, 0f) * away;
				for (float distance = village.Radius + DumpBaseMargin; distance <= village.Radius + DumpReach; distance += 4f)
				{
					Vector3 point = village.Middle + direction * distance;
					if (EffectArea.IsPointInsideArea(point, EffectArea.Type.PlayerBase, DumpBaseMargin) != null)
					{
						continue;
					}
					if (Pathfinding.instance == null || !NavmeshPoints.FindValidPoint(out Vector3 valid, point, 2f, Pathfinding.AgentType.Humanoid))
					{
						continue;
					}
					if (EffectArea.IsPointInsideArea(valid, EffectArea.Type.PlayerBase, DumpBaseMargin) != null || EnemyNear(valid, ForageSafety))
					{
						continue;
					}
					spot = valid;
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Finds the pile of what the village's vikings dropped outside it because the chests hold enough of it - the
		/// dropped item nearest the village's middle that's still lying outside every player's base, within DumpReach of
		/// the village's edge give or take DumpPileSlack - so every viking of the village drops on the same pile
		/// </summary>
		/// <param name="pile">Where the item lies</param>
		/// <returns>True when there's one</returns>
		private bool FindDumpPile(out Vector3 pile)
		{
			pile = Vector3.zero;
			float nearest = village.Radius + DumpReach + DumpPileSlack;
			bool found = false;
			foreach (ItemDrop drop in LoadedDrops())
			{
				ZNetView view = ((drop != null) ? drop.m_nview : null);
				if (view == null || !view.IsValid() || !view.GetZDO().GetBool(DiscardKey))
				{
					continue;
				}
				Vector3 at = drop.transform.position;
				float distance = Utils.DistanceXZ(at, village.Middle);
				if (distance > nearest || EffectArea.IsPointInsideArea(at, EffectArea.Type.PlayerBase, DumpBaseMargin) != null)
				{
					continue;
				}
				nearest = distance;
				pile = at;
				found = true;
			}
			return(found);
		}

		/// <summary>
		/// Notes the viking finding nowhere outside the village to drop an item the chests hold enough of, so it holds
		/// on to it and tries again in DumpRetryTime seconds - for DumpGiveUpTime seconds in all
		/// </summary>
		/// <returns>True while it keeps trying, false once it has tried for long enough</returns>
		private bool DumpLater()
		{
			if (dumpNowhereSince < 0f)
			{
				dumpNowhereSince = Time.time;
			}
			if (Time.time - dumpNowhereSince >= DumpGiveUpTime)
			{
				dumpNowhereSince = -1f;
				return(false);
			}
			dumpAgainAt = Time.time + DumpRetryTime;
			return(true);
		}
	}
}
