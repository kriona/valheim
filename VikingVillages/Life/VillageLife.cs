using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A living viking's life in its village - what it does with nothing to fight, chosen by the time of day, on the
	/// client that owns it
	/// </summary>
	/// <remarks>
	/// By day it strolls, stands about, talks with the others, pets the village's boars, repairs the village's houses,
	/// rebuilds them and keeps the fire stacked with wood from the village's chests or the ground, crafts arrows at the
	/// village's workbench when it has a bow, puts loot lying around the village away in a chest with room for it, and
	/// now and then stocks a chest that's less than half wood with wood from the ground. At dusk it gathers around the fire, at night it sleeps in a free bed in the village or sits in its house -
	/// except the night watch, which patrols the village - and in the morning it gets up and may stretch. Whatever it's
	/// doing, a fight comes first: it stops as soon as its AI has an enemy, and it rushes to help a viking or player
	/// near it that's attacked. It doesn't chase an enemy far past the village's edge, and walks back when it ends up
	/// far outside. Its AI fights as it always does - this only takes over while it has nothing to fight, and dulls its
	/// senses while it rests at night
	/// </remarks>
	internal partial class VillageLife : MonoBehaviour, Interactable, TextReceiver, IVikingLife
	{
		/// <summary>
		/// The RPCs a player sends a viking's owner when they talk to it, with the player's ZDO, and when they name it,
		/// with the name and who named it
		/// </summary>
		private const string TalkRpc = "kriona.VikingVillages.VikingTalk";
		private const string NameRpc = "kriona.VikingVillages.VikingName";

		/// <summary>
		/// The RPC a player sends a viking's owner while the box to name it is open, with the player's ZDO and whether it's
		/// still open, so the viking stands facing them
		/// </summary>
		private const string NamingRpc = "kriona.VikingVillages.VikingNaming";

		/// <summary>
		/// How a viking waits while a player names it - how often the player's client says the box is still open and how
		/// long the viking keeps facing them after it last heard, in seconds, how long it keeps facing them once the box
		/// closes, and how far from the player it can get before the box closes, in meters
		/// </summary>
		private const float NamingPingInterval = 1f;
		private const float NamingHold = 2.5f;
		private const float NamingLinger = 1.5f;
		private const float NamingRange = 8f;

		/// <summary>
		/// The viking the local player has the box to name open for, and when this client next tells its owner the box is
		/// still open
		/// </summary>
		private static VillageLife naming;
		private static float nextNamingPing;

		/// <summary>
		/// On a living viking, that it came to its village from outside the fence and has yet to walk in
		/// </summary>
		public static readonly int ArrivingKey = "kriona.VikingVillages.vikingArriving".GetStableHashCode();

		/// <summary>
		/// On a living viking, when it was last found away from its village, in ZNet time ticks, or 0 while it's there
		/// </summary>
		private static readonly int AwaySinceKey = "kriona.VikingVillages.vikingAwaySince".GetStableHashCode();

		/// <summary>
		/// The least time between a viking's answers when a player talks to it, in seconds
		/// </summary>
		private const float TalkAnswerInterval = 1.5f;

		/// <summary>
		/// When the viking can next answer a player talking to it
		/// </summary>
		private float nextAnswer;

		/// <summary>
		/// Whether the viking has its ID, so its owner stops checking (see VikingIdentity)
		/// </summary>
		private bool hasId;

		/// <summary>
		/// On a bed or fire, the viking that has claimed it, and until when in ZNet time ticks
		/// </summary>
		private const string ClaimKey = "kriona.VikingVillages.vikingClaim";
		private static readonly int ClaimUntilKey = "kriona.VikingVillages.vikingClaimUntil".GetStableHashCode();

		/// <summary>
		/// What a viking carries an item for (see VikingItems.CarryKey)
		/// </summary>
		private const string CarryWood = "wood";
		private const string CarryLoot = "loot";
		private const string CarryCraft = "craft";

		private const string HammerPrefab = "Hammer";
		private static readonly int BranchHash = "Pickable_Branch".GetStableHashCode();

		/// <summary>
		/// On a living viking, until when in ZNet time ticks it's stocking its village's chests with wood, until when
		/// it's foraging, until when it's hunting, and until when it's petting a boar, each renewed every ClaimRenew
		/// seconds as it goes, so only a few vikings of the village do each at once (see VikingsPerJob)
		/// </summary>
		private static readonly int StockingKey = "kriona.VikingVillages.vikingStocking".GetStableHashCode();
		private static readonly int ForagingKey = "kriona.VikingVillages.vikingForaging".GetStableHashCode();
		private static readonly int HuntingKey = "kriona.VikingVillages.vikingHunting".GetStableHashCode();
		private static readonly int PettingKey = "kriona.VikingVillages.vikingPetting".GetStableHashCode();
		private static readonly int HiveKey = "kriona.VikingVillages.vikingBuildingHive".GetStableHashCode();
		private static readonly int PathingKey = "kriona.VikingVillages.vikingMakingPaths".GetStableHashCode();
		private static readonly int ArmingKey = "kriona.VikingVillages.vikingArming".GetStableHashCode();
		private static readonly int LumberKey = "kriona.VikingVillages.vikingLumbering".GetStableHashCode();
		private static readonly int UpgradingKey = "kriona.VikingVillages.vikingUpgrading".GetStableHashCode();
		private static readonly int SmeltingKey = "kriona.VikingVillages.vikingSmelting".GetStableHashCode();

		/// <summary>
		/// For each job only a few vikings do at once, how many of the village's vikings it takes for one more to do it
		/// - a village of 4 or fewer has one stocking the chests, one of 5-8 two, and so on
		/// </summary>
		private static readonly Dictionary<int, int> VikingsPerJob = new Dictionary<int, int>
		{
			{ StockingKey, 4 },
			{ ForagingKey, 4 },
			{ HuntingKey, 5 },
			{ PettingKey, 6 },
			// One viking builds a beehive at a time, one makes paths, one arms itself, one cuts brush trees, one
			// upgrades its gear and one smelts, however big the village
			{ HiveKey, int.MaxValue },
			{ PathingKey, int.MaxValue },
			{ ArmingKey, int.MaxValue },
			{ LumberKey, int.MaxValue },
			{ UpgradingKey, int.MaxValue },
			{ SmeltingKey, int.MaxValue }
		};

		/// <summary>
		/// How many brush trees a viking cuts in one go when the village has no wood, the chopping damage of a punch,
		/// for a viking with no axe, and how many swings and punches it takes at most before giving up on a tree
		/// </summary>
		private const int LumberTrees = 4;
		private const float PunchChop = 4f;
		private const int LumberSwingsMax = 20;
		private const int LumberPunchesMax = 50;

		/// <summary>
		/// The bow a viking crafts when it becomes an archer with no bow in the village's chests - the crude bow
		/// </summary>
		private const string CrudeBowPrefab = "Bow";

		/// <summary>
		/// The hoe a viking makes paths with, and how often a village's path for a new house is looked for, in seconds -
		/// planning one reads the ground between the houses
		/// </summary>
		private const string HoePrefab = "Hoe";
		private const float PathPlanInterval = 30f;

		/// <summary>
		/// When each village, by its generator's ZDO, next has its path for a new house looked for, in Time.time seconds
		/// </summary>
		private static readonly Dictionary<ZDOID, float> nextPathPlan = new Dictionary<ZDOID, float>();

		/// <summary>
		/// The path each village's vikings are partway through making, by its generator's ZDO - kept when the viking
		/// making it is called away, so whoever takes it up next carries on from where it left off (see FindPathWork)
		/// </summary>
		private static readonly Dictionary<ZDOID, PathProgress> pathProgress = new Dictionary<ZDOID, PathProgress>();

		/// <summary>
		/// A house's path partway made - its plan, the next of its spots to hoe and the next of its bridges to build
		/// </summary>
		private class PathProgress
		{
			public VillagePaths.HousePath Plan;
			public int Next;
			public int BridgeNext;
		}

		/// <summary>
		/// The prey each hunting viking on this client has gone for, which its AI treats as an enemy even when it isn't
		/// one, like a deer
		/// </summary>
		private static readonly Dictionary<Character, Character> hunts = new Dictionary<Character, Character>();

		/// <summary>
		/// On a living viking, until when in ZNet time ticks it's out of its village or heading out, renewed every
		/// RestCheckInterval while it is, so a village with more than HomeGuardFrom vikings always keeps one at home
		/// </summary>
		private static readonly int OutingKey = "kriona.VikingVillages.vikingOuting".GetStableHashCode();

		/// <summary>
		/// How long the mark that a viking is out lasts unless it's renewed, in seconds, and how many vikings a village
		/// has to have before one of them has to stay home
		/// </summary>
		private const float OutingTime = 5f;
		private const int HomeGuardFrom = 2;

		/// <summary>
		/// How long a claim on a bed or fire lasts before it's renewed, in seconds
		/// </summary>
		private const float ClaimTime = 90f;
		private const float ClaimRenew = 30f;

		/// <summary>
		/// How near a viking or player has to be attacked for the viking to rush to help, in meters
		/// </summary>
		private const float DefendRange = 30f;

		/// <summary>
		/// How far past the village's edge a fellow's fight or call for help reaches every viking ready for a raid on
		/// the village, wherever in it they stand, in meters
		/// </summary>
		private const float RaidCallReach = 20f;

		/// <summary>
		/// How far past the village's edge the viking chases an enemy, and one that hurt it or someone it's defending
		/// within GrudgeTime seconds, and how far past it the viking can end up before it walks back, in meters
		/// </summary>
		private const float LeashRange = 40f;
		private const float GrudgeRange = 70f;
		private const float GrudgeTime = 15f;
		private const float StrayRange = 75f;

		/// <summary>
		/// How far past the village's edge an enemy sets off the night watch's alarm, in meters, and the least time
		/// between its alarms, in seconds
		/// </summary>
		private const float AlarmRange = 30f;
		private const float AlarmInterval = 20f;
		private const float AlarmPose = 1.5f;

		/// <summary>
		/// How near a living viking a monster outside the village has to come to be a threat it goes after, when the
		/// monster isn't alerted or after anyone, in meters
		/// </summary>
		private const float ThreatRange = 10f;

		/// <summary>
		/// What a viking archer counts an enemy archer's distance as, as a fraction of it, when picking which threat to
		/// go after - so it slightly prefers shooting back at one over a nearer enemy that has to come to it
		/// </summary>
		internal const float ArcherTargetWeight = 0.8f;

		/// <summary>
		/// How far a viking resting at night hears, in meters - asleep it sees nothing, so it only wakes to noise
		/// </summary>
		private const float RestingHearRange = 25f;

		/// <summary>
		/// The part of the day dusk starts at, when the vikings gather around the fire
		/// </summary>
		public const float DuskStart = 0.71f;

		/// <summary>
		/// The part of the day the vikings pull out their torches at, as the sun goes down - it sets at about 0.74 - and
		/// how far from the fire a viking pulls one out, in meters
		/// </summary>
		private const float TorchStart = 0.72f;
		private const float TorchFireRange = 8f;

		/// <summary>
		/// How far from the fire the vikings gather around a bonfire and a campfire, in meters
		/// </summary>
		private const float BonfireRingMin = 4f;
		private const float BonfireRingMax = 5.5f;
		private const float CampfireRingMin = 2.5f;
		private const float CampfireRingMax = 3.5f;

		/// <summary>
		/// How much of the ring around a fire each viking gathered there takes up, in meters around it, which gives how
		/// many a fire has room for before the vikings go to another or its ring widens to fit them
		/// </summary>
		private const float GatherSpacing = 1.75f;

		/// <summary>
		/// The chance a viking sits down at the fire rather than standing, and the part of the day the sitters sit down
		/// between, after the torches are out
		/// </summary>
		private const float GatherSitChance = 0.35f;
		private const float GatherSitStart = 0.725f;
		private const float GatherSitEnd = 0.74f;

		/// <summary>
		/// How near another viking sitting at the fire has to be for a sitting viking to turn to it, in meters
		/// </summary>
		private const float GatherNeighbor = 2f;

		/// <summary>
		/// How near a seat by the fire a viking gets before sitting on it, since the piece itself stands in the way, how
		/// far past the ring a seat's piece can stand and still be looked at, how far past the ring its seat can be, and
		/// how near a player has to be to a seat to be sitting on it, in meters
		/// </summary>
		private const float GatherSeatReach = 1.5f;
		private const float GatherSeatSearch = 3f;
		private const float GatherSeatSlack = 0.5f;
		private const float GatherSeatTaken = 0.6f;

		/// <summary>
		/// The share of its health below which a viking rests by the fire before anything else by day, how far inside
		/// the fire's warmth and outside its flames it sits, in meters, how often it checks whether it's resting, and how
		/// long it leaves resting by the fire after it couldn't get a seat there, in seconds
		/// </summary>
		private const float WoundedShare = 0.75f;
		private const float WarmthMargin = 0.5f;
		private const float RestCheckInterval = 1f;
		private const float RestRetryTime = 60f;

		/// <summary>
		/// How long a viking keeping warm by a fire can still be freezing where it got to before it goes inside its house
		/// instead, and how long it keeps warm inside before trying the fire again, in seconds
		/// </summary>
		private const float WarmFireTimeout = 10f;
		private const float WarmInsideTime = 60f;

		/// <summary>
		/// How often a viking resting or gathering by the fire checks whether the rain has it go inside, in seconds
		/// </summary>
		private const float RainCheckInterval = 5f;

		/// <summary>
		/// How far a viking gathered at the fire or sitting on its floor can be pushed before it goes back to where it
		/// was, in meters
		/// </summary>
		private const float PushedDistance = 1f;

		/// <summary>
		/// How many spots on its floor a viking walks for before waiting, when it can't get into its house for the night,
		/// how long it waits before trying again, in seconds, and how many points it samples for each spot
		/// </summary>
		private const int RestFloorTries = 4;
		private const float RestRetryWait = 15f;
		private const int RestFloorSamples = 6;

		/// <summary>
		/// How far from a house's doors a viking stands or rests inside it, so it isn't in the way of the doorway, in
		/// meters
		/// </summary>
		private const float DoorClearance = 2f;

		/// <summary>
		/// How many more times a viking held up on its way to its bed tries again before resting on the floor instead,
		/// and how long it waits between tries, in seconds
		/// </summary>
		private const int BedRetries = 2;
		private const float BedRetryWait = 3f;

		/// <summary>
		/// How near the edge of its bed a viking has to stand, seen from above, to lie down in it from there - its foot
		/// included, which is too far from the bed's middle when its long sides are blocked - how far above or below the
		/// bed it can stand, and how high the check that nothing stands between them looks, in meters
		/// </summary>
		private const float BedEdgeReach = 0.8f;
		private const float BedEdgeHeight = 1f;
		private const float BedEdgeKnee = 0.3f;

		/// <summary>
		/// How many places in the ring a viking tries before heading for the fire itself, and how far from a place on the
		/// navmesh the ring's point can be, in meters
		/// </summary>
		private const int GatherTries = 4;
		private const float GatherSnap = 2f;

		/// <summary>
		/// How far past the village's edge the viking looks for branches and wood on the ground, in meters, and how long
		/// it leaves work that needs wood after finding none, in seconds
		/// </summary>
		private const float WoodReach = 60f;
		private const float NoWoodWait = 60f;

		/// <summary>
		/// The most wood the viking carries
		/// </summary>
		private const int CarryMax = 20;

		/// <summary>
		/// How far a viking carrying more than it can walks to put things away in a chest - with none that near, it
		/// drops them where it stands - in meters
		/// </summary>
		private const float UnburdenReach = 20f;

		/// <summary>
		/// Finds the heaviest thing the viking can do without - anything it carries for the village, or else something
		/// of its own it doesn't hang on to (see VikingCore.Keeps)
		/// </summary>
		/// <returns>The item, or null when there's nothing it can do without</returns>
		private ItemDrop.ItemData HeaviestSpare()
		{
			ItemDrop.ItemData heaviest = null;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (VikingItems.IsProp(item) || body.GearQueued(item) || item.m_shared.m_weight <= 0f)
				{
					continue;
				}
				if (!VikingItems.IsCarried(item) && (viking == null || viking.Keeps(item)))
				{
					continue;
				}
				if (heaviest == null || item.m_shared.m_weight * item.m_stack > heaviest.m_shared.m_weight * heaviest.m_stack)
				{
					heaviest = item;
				}
			}
			return(heaviest);
		}

		/// <summary>
		/// Works out how many of an item the viking has to shed to carry no more than it can
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>How many, at least one and at most its stack</returns>
		private int OverBy(ItemDrop.ItemData item)
		{
			float over = humanoid.GetInventory().GetTotalWeight() - body.GetMaxCarryWeight();
			return(Mathf.Clamp(Mathf.CeilToInt(over / Mathf.Max(0.01f, item.m_shared.m_weight)), 1, item.m_stack));
		}

		/// <summary>
		/// Finds the nearest of the village's chests within UnburdenReach of the viking with room for an item
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>The chest, or null when there's none</returns>
		private Container UnburdenChest(ItemDrop.ItemData item)
		{
			return(Nearest(UsableChests().FindAll((Container chest) => !Avoided(chest.m_nview.GetZDO().m_uid) && Utils.DistanceXZ(chest.transform.position, transform.position) <= UnburdenReach && chest.GetInventory().CanAddItem(item, 1))));
		}

		/// <summary>
		/// Makes a copy of as many of an item as the viking has to shed, as its own rather than carried
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>The copy</returns>
		private ItemDrop.ItemData SpareCopy(ItemDrop.ItemData item)
		{
			ItemDrop.ItemData copy = item.Clone();
			copy.m_stack = OverBy(item);
			copy.m_equipped = false;
			copy.m_customData.Remove(VikingItems.CarryKey);
			return(copy);
		}

		/// <summary>
		/// Puts as many of an item as the viking has to shed in a chest it has reached
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="item">The item</param>
		/// <returns>False when the chest couldn't take them</returns>
		private bool StoreSpare(Container chest, ItemDrop.ItemData item)
		{
			if (!OpenChest(chest))
			{
				return(false);
			}
			ItemDrop.ItemData copy = SpareCopy(item);
			int amount = copy.m_stack;
			if (!chest.GetInventory().AddItem(copy))
			{
				return(false);
			}
			humanoid.GetInventory().RemoveItem(item, amount);
			chest.m_closeEffects.Create(chest.transform.position, chest.transform.rotation);
			return(true);
		}

		/// <summary>
		/// Drops what the viking can do without where it stands, the heaviest first, until it's no longer held back by
		/// its load - in a fight, or running from one, when there's no time to put it in a chest
		/// </summary>
		private void ShedForFight()
		{
			ItemDrop.ItemData spare = HeaviestSpare();
			// At most a drop for each slot, so a stack that won't come out of the inventory can't hold it here
			int drops = 0;
			Inventory inventory = humanoid.GetInventory();
			while (spare != null && drops < inventory.m_width * inventory.m_height && inventory.GetTotalWeight() > body.GetMaxCarryWeight())
			{
				DropSpare(spare);
				drops++;
				spare = HeaviestSpare();
			}
			body.UpdateEncumbered();
		}

		/// <summary>
		/// Drops as many of an item as the viking has to shed where it stands, for a fellow with room to carry off
		/// </summary>
		/// <param name="item">The item</param>
		private void DropSpare(ItemDrop.ItemData item)
		{
			ItemDrop.ItemData copy = SpareCopy(item);
			int amount = copy.m_stack;
			humanoid.GetInventory().RemoveItem(item, amount);
			Plugin.Log.LogInfo(LogName() + " put down " + ((amount > 1) ? amount + " " : "") + Localization.instance.Localize(copy.m_shared.m_name) + " - too heavily loaded for a fight");
			ItemDrop.DropItem(copy, amount, transform.position + transform.forward * 0.6f + Vector3.up * 0.5f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
		}

		/// <summary>
		/// The wood lying on the ground the viking picks up, as well as branches, and takes from chests for its work -
		/// plain wood only, so fine wood and core wood, which are harder to come by, are kept for the recipes that ask for
		/// them by name, and are picked up and put away as loot
		/// </summary>
		private static readonly HashSet<string> WoodItems = new HashSet<string> { "Wood" };

		/// <summary>
		/// The arrows a viking with a bow crafts, and how many it carries when its quiver is full - as many as it spawns
		/// with
		/// </summary>
		private const string ArrowPrefab = "ArrowWood";
		private const int QuiverFull = 100;

		/// <summary>
		/// How many arrows a viking can't do without, which it crafts whatever the village's wood - cutting brush trees
		/// for it when there's none (see FindLumberWork) - and how many it needs, which it crafts as long as there's wood
		/// to get. Past that it only crafts arrows up to QuiverFull while the village has SpareWood more than a batch
		/// takes, so its quiver never eats into the wood the rest of the village needs
		/// </summary>
		private const int QuiverCritical = 20;
		private const int QuiverImportant = 40;
		private const int SpareWood = 40;

		/// <summary>
		/// The batch of arrows a craft makes and the wood it takes, when the game's recipe can't be read, and the wood the
		/// recipe's wood is counted from
		/// </summary>
		private const int ArrowBatchDefault = 20;
		private const int ArrowWoodDefault = 8;
		private const string RecipeWood = "Wood";

		/// <summary>
		/// The workbench a viking crafts arrows at, how near it the viking works, in meters, and how long a batch takes,
		/// in seconds
		/// </summary>
		private static readonly int WorkbenchHash = "piece_workbench".GetStableHashCode();
		private const float CraftReach = 1.8f;

		/// <summary>
		/// The forge a viking repairs the gear the workbench can't at
		/// </summary>
		private static readonly int ForgeHash = "forge".GetStableHashCode();
		private const float CraftTime = 3f;

		/// <summary>
		/// The least share of a crafting station's roof check point that has to be covered for a viking to use it, the
		/// same as for a player, how long whether it can be used is kept before it's checked again, in seconds, and the
		/// most stations kept
		/// </summary>
		private const float StationMinCover = 0.7f;
		private const float StationCheckTime = 10f;
		private const int StationChecksMax = 256;

		/// <summary>
		/// Whether each crafting station could be used when it was last checked, and until when that holds, by its ZDO
		/// </summary>
		private static readonly Dictionary<ZDOID, KeyValuePair<float, bool>> stationChecks = new Dictionary<ZDOID, KeyValuePair<float, bool>>();

		/// <summary>
		/// The torch a viking carries at night and crafts a new one of, and how burnt down its only torch is before it
		/// does, as a share of its strength
		/// </summary>
		private const string TorchPrefab = "Torch";
		private const float TorchReplaceShare = 0.5f;

		/// <summary>
		/// How long a viking leaves crafting a torch at dusk or night after the village's chests couldn't give it what one
		/// takes, in seconds, and how often the watch checks its torch will last its shift
		/// </summary>
		private const float NightTorchRetry = 60f;
		private const float WatchTorchCheck = 5f;

		/// <summary>
		/// The share of its durability a piece of a viking's gear wears down to before it takes its gear to the
		/// workbench, and how long it works at the bench on each piece it repairs there, in seconds
		/// </summary>
		private const float GearWornShare = 0.75f;
		private const float GearRepairTime = 1.5f;

		/// <summary>
		/// How far past the village's edge the viking picks up loot, in meters, how long an item a player dropped has to
		/// have lain there first, so it doesn't take what a player just put down, and how long any other has, like what
		/// drops from a kill, in seconds
		/// </summary>
		private const float LootReach = 10f;
		private const float LootSettle = 30f;
		private const float DropSettle = 2f;

		/// <summary>
		/// How fast an item can still be moving and count as lying at rest, in meters a second
		/// </summary>
		private const float LootRestSpeed = 0.1f;

		/// <summary>
		/// How long after a fight a viking drops what it's doing to clear up the loot left lying around, in seconds
		/// </summary>
		private const float LootRushTime = 120f;

		/// <summary>
		/// How long after a kill a viking looks for what its enemy dropped, in seconds - long enough for a corpse to drop
		/// its loot - and how far from where it died it looks, in meters
		/// </summary>
		private const float KillLootTime = 45f;
		private const float KillLootRadius = 8f;

		/// <summary>
		/// The most stacks of an item the vikings don't use - anything but wood and food - they keep in the village's
		/// chests, one to a chest at most, and of each trophy, which only piles up
		/// </summary>
		private const int VillageStacksMax = 2;
		private const int TrophyStacks = 1;

		/// <summary>
		/// On an item a viking dropped outside the village because the chests hold enough of it, that no viking stores
		/// it again
		/// </summary>
		private static readonly int DiscardKey = "kriona.VikingVillages.vikingDiscard".GetStableHashCode();

		/// <summary>
		/// Where a viking drops an item the chests hold enough of - how far past the village's edge it looks, how far
		/// from a player's base it keeps, so the item despawns as usual, in meters, how many directions it tries, how
		/// long it holds on to the item before trying again when it may not leave the village or finds nowhere to drop
		/// it, and how long it keeps trying before it puts it down where it stands, in seconds
		/// </summary>
		private const float DumpReach = 50f;
		private const float DumpBaseMargin = 3f;
		private const int DumpTries = 12;
		private const float DumpRetryTime = 15f;
		private const float DumpGiveUpTime = 180f;
		private const float DumpArrive = 1.5f;

		/// <summary>
		/// How near what the village's vikings dropped outside it before an item is dropped, so it all lies in one pile,
		/// and how much farther than DumpReach from the village's edge a pile can lie and still be the village's, in
		/// meters
		/// </summary>
		private const float DumpPileReach = 2f;
		private const float DumpPileSlack = 5f;

		/// <summary>
		/// How far from a viking gathering loot the next item can lie for it to pick that up too before taking them to
		/// the chests, in meters
		/// </summary>
		private const float LootBatchReach = 15f;

		/// <summary>
		/// How far past where the vikings pick up loot the debug hover text counts what's lying around, in meters
		/// </summary>
		private const float DebugLootMargin = 20f;

		/// <summary>
		/// How often a viking strolling, standing about or petting a boar looks for work it could be doing instead, in
		/// seconds
		/// </summary>
		private const float WorkCheckInterval = 5f;

		/// <summary>
		/// How often the viking looks at whether to use a forsaken power for its job, and for how long after its job
		/// starts, or first takes it out of the village, it may use one for it, in seconds
		/// </summary>
		private const float JobPowerInterval = 1f;
		private const float JobPowerWindow = 5f;

		/// <summary>
		/// How a viking teaches a forsaken power at dusk - how near it gets to the viking it teaches, how long it stands
		/// by them once it has used the power, and how long it tries before giving up, in meters and seconds
		/// </summary>
		private const float TeachReach = 3f;
		private const float TeachLinger = 2f;
		private const float TeachTimeout = 60f;

		/// <summary>
		/// How near the viking gets to a chest and to an item on the ground to reach it, in meters, and how long it
		/// reaches out before it takes or puts something, in seconds
		/// </summary>
		private const float ChestReach = 1.5f;
		private const float ItemReach = 1.2f;
		private const float ReachTime = 0.8f;

		/// <summary>
		/// How long a chest the viking reaches into stays open, in seconds - a little past when it takes or puts
		/// something
		/// </summary>
		private const float ChestOpenTime = ReachTime + 0.7f;

		/// <summary>
		/// How near a chest a player has to stand for the vikings to leave it alone while that player's game owns it,
		/// in meters - a little past how far a player opens a chest from
		/// </summary>
		private const float PlayerChestRange = 6f;

		/// <summary>
		/// How long the viking leaves a chest or loot alone after it couldn't get to it, in seconds
		/// </summary>
		private const float AvoidTime = 60f;

		/// <summary>
		/// How often a viking on its way to loot checks that a chest still has room for it, in seconds
		/// </summary>
		private const float RoomCheckInterval = 2f;

		/// <summary>
		/// How much wood the viking adds to the fire in one go
		/// </summary>
		private const int FuelTripsMax = 12;

		/// <summary>
		/// How far a viking looks for another to talk with, in meters, how near they stand, how long they talk and how
		/// long each takes a turn, in seconds, and how likely a turn starts with a gesture
		/// </summary>
		private const float TalkSearch = 30f;
		private const float TalkDistance = 1.8f;
		private const float TalkTimeMin = 10f;
		private const float TalkTimeMax = 30f;
		private const float TurnTimeMin = 3f;
		private const float TurnTimeMax = 6f;
		private const float GestureChance = 0.3f;

		/// <summary>
		/// How long two vikings have to get inside a house to talk out of the rain, in seconds, before the talk is given up
		/// </summary>
		private const float TalkWalkTime = 30f;

		/// <summary>
		/// How long after a fight vikings may talk about it, in seconds
		/// </summary>
		private const float FightTalkTime = 300f;

		/// <summary>
		/// How long the viking stands about, in seconds
		/// </summary>
		private const float IdleTimeMin = 4f;
		private const float IdleTimeMax = 12f;

		/// <summary>
		/// How likely each day job is when the viking picks what to do next with no work to do - talking, petting a
		/// boar, gathering wood for a chest, foraging food for a chest, getting rested again by the fire, strolling and
		/// standing about
		/// </summary>
		private const float TalkWeight = 25f;
		private const float PetWeight = 10f;
		private const float StockWeight = 15f;
		private const float ForageWeight = 15f;
		private const float RefreshWeight = 15f;
		private const float HuntWeight = 15f;

		/// <summary>
		/// How many times as likely foraging and hunting are while the village's chests hold too little food for its
		/// vikings to keep their foods up (see FoodLow)
		/// </summary>
		private const float FoodShortBoost = 4f;

		/// <summary>
		/// How many times as likely a viking from a harder biome than its village's is to pick gathering wood, foraging
		/// and hunting, which take it out of the village, where it's safer than the others
		/// </summary>
		private const float StrongOutingBoost = 3f;

		/// <summary>
		/// How near a cooking station a viking cooks at it, and how near the station it sets out to cook at the others it
		/// cooks at too have to be, as at the same fire, in meters
		/// </summary>
		private const float CookReach = 2f;
		private const float CookTogether = 4f;

		/// <summary>
		/// How much putting food on a cooking station and taking cooked food off raise the skill it uses, the same as a
		/// player's
		/// </summary>
		private const float CookAddSkill = 0.4f;
		private const float CookTakeSkill = 0.6f;

		/// <summary>
		/// How a viking hunts - how far past the village's edge it looks for prey and how much farther it chases, how
		/// near it gets before its AI goes for the prey as an archer (once it's done shooting from afar, see HuntJob.Snipe)
		/// and with a weapon in hand, in meters, the longest it
		/// hunts, in seconds, the share of its health it gives up and runs home below, and how far around the kill it
		/// gathers what dropped, in meters, for how long at most, in seconds
		/// </summary>
		private const float HuntReach = 60f;
		private const float HuntChase = 20f;
		private const float ArcherEngage = 25f;
		private const float MeleeEngage = 8f;
		private const float HuntTime = 180f;
		private const float HuntRetreatShare = 0.5f;
		private const float CollectRadius = 8f;
		private const float CollectTime = 20f;

		/// <summary>
		/// How an archer shoots a bird for its feathers - how near it gets, in meters, how long it waits between arrows,
		/// in seconds, how many it looses before giving up on the bird, how far off facing it it can be and still loose,
		/// in degrees, and how long it waits for the bird to land again once it takes off, in seconds
		/// </summary>
		private const float BirdRange = 20f;
		private const float BirdShotInterval = 2.5f;
		private const int BirdShotsMax = 6;
		private const float BirdAimAngle = 8f;
		private const float BirdLandWait = 20f;

		/// <summary>
		/// Whether the viking is aiming at a bird or at prey it's shooting from afar, which keeps its bow drawn and in
		/// its hands
		/// </summary>
		private bool aimingBow;

		/// <summary>
		/// How vikings from the Plains on hunt as a party (see GroupHunts) - how many go out together, at least and at
		/// most, the leader among them, how far from the leader it looks for free vikings to bring, how near the leader
		/// they gather before they set out and how long it waits for them at most, in seconds, how near the leader the
		/// rest keep as it leads them out, and how near the prey the leader gets before they all go for it, in meters
		/// </summary>
		private const int PartyMin = 3;
		private const int PartyMax = 4;
		private const float PartyRecruitRange = 30f;
		private const float PartyGatherRange = 6f;
		private const float PartyGatherTime = 30f;
		private const float PartyFollow = 5f;
		private const float PartyEngage = 20f;

		/// <summary>
		/// How an archer shoots prey from afar - how near the prey can come before its AI takes the fight over, and how
		/// many arrows it looses before its AI takes over to close in, as for prey it keeps missing
		/// </summary>
		private const float SnipeHandOver = 10f;
		private const int SnipeShotsMax = 8;

		/// <summary>
		/// How near an archer gets to prey before it stops to shoot it from afar, by its bow (see SnipeRangeFor) - as far
		/// as its arrows fly in SnipeFlightTime seconds, so prey has little time to move off, and no farther than its
		/// arrows stray SnipeSpreadTolerance meters from where it aims, then no nearer than SnipeRangeMin and no farther
		/// than it sees, in meters
		/// </summary>
		private const float SnipeFlightTime = 0.7f;
		private const float SnipeSpreadTolerance = 0.52f;
		private const float SnipeRangeMin = 15f;

		/// <summary>
		/// How a hunter stalks wild prey (see HuntJob.Stalk) - how near the prey it starts stalking instead of hurrying,
		/// how far around the prey's back reaches from straight behind it, the most it swings around the prey from the
		/// way it's coming to come at its back, in degrees, how near the spot it stalks toward it gets, in meters, and
		/// how often it works the spot out again, in seconds
		/// </summary>
		private const float StalkRange = 60f;
		private const float StalkBehind = 30f;
		private const float StalkSwingMax = 60f;
		private const float StalkSpotReach = 1.5f;
		private const float StalkReplan = 2f;

		/// <summary>
		/// Works out how far a hunter swings around its prey to come at its back (see HuntJob.Stalk) - none when it's
		/// already within StalkBehind of straight behind it, just far enough to get there when that's no more than
		/// StalkSwingMax, and none when it's farther, rather than circling all the way around
		/// </summary>
		/// <param name="around">How far around the prey its back is from the way the hunter is coming, in degrees, either way</param>
		/// <returns>How far it swings, in degrees, the same way</returns>
		internal static float StalkSwing(float around)
		{
			float needed = Mathf.Abs(around) - StalkBehind;
			if (needed <= 0f || needed > StalkSwingMax)
			{
				return(0f);
			}
			return(Mathf.Sign(around) * needed);
		}

		/// <summary>
		/// The part of the day after which a viking no longer goes to the fire to get rested again, since the gathering
		/// at dusk soon rests it
		/// </summary>
		private const float RefreshDayEnd = 0.6f;

		/// <summary>
		/// How far past the village's edge a viking forages for berries and mushrooms, how far from an enemy it keeps
		/// while it's out of the village - foraging, hunting, or getting wood or loot - so it heads home before most
		/// monsters see it rather than bringing them back with it, in meters, and how many it gathers before taking them
		/// home
		/// </summary>
		private const float ForageReach = 60f;
		private const float ForageSafety = 50f;
		private const int ForageLoad = 10;

		/// <summary>
		/// How far a living viking sees, in meters
		/// </summary>
		private const float SightRange = 50f;

		/// <summary>
		/// How long a viking leaves eating after it found no food it could eat, in seconds
		/// </summary>
		private const float EatRetryTime = 60f;

		/// <summary>
		/// How many foods the village's chests have to hold for each of its vikings before they keep their foods up
		/// rather than eating only when wounded - a full set of foods each, as many as a viking eats at once
		/// </summary>
		private const int SpareMeals = 3;

		/// <summary>
		/// The most of a chest's slots the vikings fill with wood
		/// </summary>
		private const float ChestWoodShare = 0.5f;
		private const float StrollWeight = 40f;
		private const float IdleWeight = 25f;

		/// <summary>
		/// How many swings the viking repairs and rebuilds a piece with - one, as a player does - the seconds between
		/// them, and the seconds into the last one its hammer lands, when the work is done
		/// </summary>
		private const int RepairSwings = 1;
		private const int RebuildSwings = 1;
		private const float SwingInterval = 1.2f;
		private const float SwingHitTime = 0.4f;

		/// <summary>
		/// How near the viking gets to a piece to work on it, in meters
		/// </summary>
		private const float WorkReach = 2.5f;

		/// <summary>
		/// How long the viking has to get within reach of a damaged piece before it gives up on repairing it, and how
		/// long it then leaves the piece alone, in seconds - long enough to cross the village, since it can keep moving
		/// without ever getting there, like when the nearest it can stand is still out of reach
		/// </summary>
		private const float RepairGiveUpTime = 90f;
		private const float RepairAvoidTime = 300f;

		/// <summary>
		/// How long the viking waits after the night ends before it gets up - at least WakeDelayMin seconds, and up to
		/// SleepSpread seconds more, most of them early, so the village doesn't all get up at once - how likely it
		/// stretches, and how long it stands still for its stretch - a little longer than the stretch, which waits until
		/// it's up. Going to bed is spread out over SleepSpread seconds the same way (see Bedtime)
		/// </summary>
		private const float WakeDelayMin = 1f;
		private const float SleepSpread = 60f;
		private const float StretchChance = 0.5f;
		private const float StretchTime = VikingPoses.StretchDuration + 0.5f;

		/// <summary>
		/// How long the watch stops at each spot of its patrol, in seconds
		/// </summary>
		private const float PatrolPauseMin = 3f;
		private const float PatrolPauseMax = 6f;

		/// <summary>
		/// How near the watch walks to the viking it wakes at midnight to hand the watch over to, in meters, and how long
		/// it stands there after waking it before handing over, in seconds
		/// </summary>
		private const float WakeReach = 2f;
		private const float WakeTime = 2.5f;

		/// <summary>
		/// How near a viking awake has to be to see one keeping watch die, and take its place, in meters, and what it
		/// can't see through
		/// </summary>
		private const float WitnessRange = 30f;
		private static int witnessMask;

		/// <summary>
		/// How the watch looks into a player's noise - how often it listens, how long after looking into a player's
		/// noise any watch on this client leaves that player's noises alone, how near it walks to where it heard them, how
		/// near it has to see them to know them, in meters, how long it looks before giving up and how long it looks
		/// around once it has found them or nothing, in seconds
		/// </summary>
		private const float ListenInterval = 1f;
		private const float InvestigateCooldown = 180f;
		private const float NoiseReach = 2f;
		private const float RecognizeRange = 10f;
		private const float InvestigateTime = 30f;
		private const float InvestigateLook = 3f;

		/// <summary>
		/// When the watch last looked into each player's noise, by the player's ZDO
		/// </summary>
		private static readonly Dictionary<ZDOID, float> investigated = new Dictionary<ZDOID, float>();

		/// <summary>
		/// Finds a player the watch hears and goes to look into - within the player's noise of it, not hostile, not
		/// already in plain sight close by, and whose noise no watch has looked into in the last InvestigateCooldown
		/// seconds - noting it's being looked into
		/// </summary>
		/// <returns>The player, or null when it hears none</returns>
		private Player HeardPlayer()
		{
			foreach (Player player in Player.GetAllPlayers())
			{
				if (player == null || player.IsDead() || BaseAI.IsEnemy(humanoid, player))
				{
					continue;
				}
				ZDOID id = player.GetZDOID();
				if (investigated.TryGetValue(id, out float last) && Time.time - last < InvestigateCooldown)
				{
					continue;
				}
				float distance = Vector3.Distance(player.transform.position, transform.position);
				if (distance > player.GetNoiseRange() || (distance <= RecognizeRange && ai.CanSeeTarget(player)))
				{
					continue;
				}
				investigated[id] = Time.time;
				return(player);
			}
			return(null);
		}

		/// <summary>
		/// How far outside a house with no doors found the watch stops on its patrol, in meters
		/// </summary>
		private const float DoorstepDistance = 2f;

		/// <summary>
		/// How long the viking can be on its way to a spot without getting half a meter nearer or moving StuckMove
		/// meters before it gives up, in seconds
		/// </summary>
		private const float StuckTime = 10f;
		private const float StuckMove = 1f;

		/// <summary>
		/// The nearest a spot has to be for the viking to count as there, in meters - Go stops it once it's this near,
		/// whatever reach it asks for, so a job that checks for itself it's there allows no less
		/// </summary>
		private const float ArriveReach = 1f;

		/// <summary>
		/// How the viking jumps over something low in its way when there's no way around, like a pen's fence - how long
		/// it goes without getting nearer before it tries, how far ahead it looks for the obstacle, the highest the
		/// obstacle can be, all in seconds and meters, and how many times it tries before it gives up on where it's going
		/// </summary>
		private const float HopAfter = 3f;
		private const float HopReach = 1.5f;
		private const float HopHeight = 1.4f;
		private const int HopsMax = 3;

		/// <summary>
		/// How far past what it jumps over the viking looks for where it lands, in meters
		/// </summary>
		private const float HopLanding = 1f;

		/// <summary>
		/// The viking's run-up to a jump over a fence, made with its own jump - how far back from the fence it starts its
		/// run, in meters, the longest it backs up, runs at the fence and is in the air for, in seconds, how far over the
		/// top of the fence its feet keep, and how far it has to carry past the fence beyond its own width, in meters
		/// </summary>
		private const float RunUpDistance = 1.5f;
		private const float BackUpTime = 1.5f;
		private const float RunUpTime = 2f;
		private const float HopAirTime = 1.5f;
		private const float HopClearance = 0.1f;
		private const float HopDepth = 0.25f;

		/// <summary>
		/// How far past where its run-up would take it the viking keeps from a fire's flames to back up for one, in meters
		/// </summary>
		private const float RunUpFireMargin = 1f;

		/// <summary>
		/// How a viking fighting hand to hand goes over a pen's fence between it and its enemy rather than swinging
		/// through it - how often it looks for the fence, how far off the enemy can be, and how long it keeps trying
		/// before it fights as it would, in seconds and meters
		/// </summary>
		private const float FenceCheckInterval = 0.5f;
		private const float FenceReach = 8f;

		/// <summary>
		/// How high above the ground a fence is looked for between two spots, in meters - low, in the middle and high on
		/// a fence, so the gaps between its rails don't hide it
		/// </summary>
		private static readonly float[] FenceHeights = { 0.25f, 0.5f, 0.8f };
		private const float FenceChaseTime = 10f;

		/// <summary>
		/// How long a viking slaughtering livestock keeps trying to get over the fence between it and the animal - it
		/// never strikes through it - before it gives up the slaughter, and how often it looks whether the animal is in a
		/// pen it isn't in, in seconds, and how near the animal it walks to get into its pen, in meters - nearer than it
		/// could get from outside the fence, so it has to go in
		/// </summary>
		private const float SlaughterFenceTime = 30f;
		private const float SlaughterPenCheck = 1f;
		private const float SlaughterReach = 0.5f;

		/// <summary>
		/// Seconds between looks for the viking's village
		/// </summary>
		private const float VillageCheckInterval = 5f;

		/// <summary>
		/// How long a viking has to be away from its village before it joins another village it's in, in seconds of game
		/// time
		/// </summary>
		private const float SwitchAwayTime = 300f;

		/// <summary>
		/// How the viking weighs up a fight - how far it counts vikings with it and enemies on it, in meters, how many
		/// enemies outnumber it, the share of its health below which it's badly hurt and above which it's recovered,
		/// the share a healthy viking has, how long after it was hurt it counts as attacked, and seconds between looks
		/// </summary>
		private const float AllyRange = 15f;
		private const float EnemyRange = 15f;
		private const int FightOutnumbered = 4;
		private const float BadlyHurtShare = 0.25f;
		private const float RecoveredShare = 0.4f;
		private const float HealthyShare = 0.5f;
		private const float AttackedTime = 5f;
		private const float TacticsInterval = 0.5f;

		/// <summary>
		/// How far a badly hurt viking hanging back keeps from the nearest enemy, and how far it steps back at a time, in
		/// meters
		/// </summary>
		private const float HangBackDistance = 5f;
		private const float HangBackStep = 3f;

		/// <summary>
		/// How a badly hurt viking strikes an enemy busy with someone else from behind - how far out it circles the
		/// enemy, how far around it goes in one step, in degrees, how wide the arc behind the enemy is that counts as
		/// behind it, how near behind it walks in to, and how near it has to be to strike, in meters
		/// </summary>
		private const float FlankRadius = 4f;
		private const float FlankStep = 60f;
		private const float BehindAngle = 50f;
		private const float FlankClose = 1.5f;
		private const float FlankStrike = 3f;

		/// <summary>
		/// How long a badly hurt viking keeps running from an enemy after it stops coming after it, in seconds, so it
		/// doesn't turn straight back
		/// </summary>
		private const float RunTime = 3f;

		/// <summary>
		/// How often a badly hurt viking shouts for help while enemies are near, in seconds
		/// </summary>
		private const float ShoutInterval = 6f;

		/// <summary>
		/// How a viking calls out an enemy it spots - the least time between its own calls, between any two vikings'
		/// calls, and before the same enemy is called out again, in seconds
		/// </summary>
		private const float SpotInterval = 15f;
		private const float SpotGap = 2f;
		private const float SpotRepeat = 60f;

		/// <summary>
		/// How a viking greets a player arriving - how near the player has to be, in meters, how long the player has to
		/// have been away from every viking first, how often it looks for one, and how long it stands facing them, in
		/// seconds
		/// </summary>
		private const float GreetRange = 10f;
		private const float GreetAwayTime = 180f;
		private const float GreetCheckInterval = 1f;
		private const float GreetFaceTime = 2f;

		/// <summary>
		/// How long a viking stands facing a player who talked to it, in seconds - longer than for a greeting, so it
		/// doesn't walk off while they read what it says
		/// </summary>
		private const float TalkFaceTime = GreetFaceTime + 2f;

		/// <summary>
		/// How far a badly hurt viking hanging back keeps from an enemy that shoots or throws, in meters - about as far as
		/// a greydwarf throws
		/// </summary>
		private const float RangedHangBackDistance = 20f;

		/// <summary>
		/// Whether each enemy has an attack that shoots or throws, which doesn't change, by its ZDO - all forgotten once
		/// there are more than RangedCacheMax
		/// </summary>
		private static readonly Dictionary<ZDOID, bool> rangedCache = new Dictionary<ZDOID, bool>();
		private const int RangedCacheMax = 500;

		/// <summary>
		/// How the viking steers around those in its way - how far ahead it looks, how far it keeps beyond their sides
		/// and its own, in meters, and how hard it bends its way at the nearest
		/// </summary>
		private const float AvoidRange = 2.5f;
		private const float AvoidClearance = 0.3f;
		private const float AvoidStrength = 1.5f;

		/// <summary>
		/// The least it bends its way around one in its way, as a share of the hardest, so one nearly straight ahead
		/// still has it pass to a side; how many seconds it keeps the side it passes each one on; and the most it keeps
		/// before forgetting them all
		/// </summary>
		private const float AvoidMinPush = 0.25f;

		/// <summary>
		/// How far from a zombie still getting up a melee fighter stands ready, and how much farther it can be before it
		/// walks back up to it, in meters
		/// </summary>
		private const float RiseReadyDistance = 2.5f;
		private const float RiseReadySlack = 1.5f;

		/// <summary>
		/// The creatures and players near the viking as it looks for an enemy it can hurt, kept to save making a list
		/// </summary>
		private static readonly List<Character> enemiesNear = new List<Character>();
		private const float AvoidMemory = 1.5f;
		private const int AvoidSidesMax = 32;

		/// <summary>
		/// The creatures and players near the viking as it steers around them, kept to save making a list each update
		/// </summary>
		private static readonly List<Character> avoidNear = new List<Character>();

		/// <summary>
		/// The side the viking passes each one it steers around on, 1 or -1, with when it last did, and the last one it
		/// steered around
		/// </summary>
		private readonly Dictionary<Character, KeyValuePair<float, float>> avoidSides = new Dictionary<Character, KeyValuePair<float, float>>();
		private Character steeredAround;
		private float steeredAt;

		/// <summary>
		/// How many seconds after steering around one the viking's debug text still shows it
		/// </summary>
		private const float SteeredShowTime = 0.5f;

		/// <summary>
		/// How the viking moved one AI update - when, whether it moved, which way it faced, and what moved it
		/// </summary>
		private struct MoveSample
		{
			public float Time;
			public bool Moving;
			public float Yaw;
			public string Mover;
		}

		/// <summary>
		/// How the viking moved over the last TwitchWindow seconds, what moved it this update, and when its twitching is
		/// next written to the log
		/// </summary>
		private readonly Queue<MoveSample> moveSamples = new Queue<MoveSample>();
		private string mover = "game AI";
		private float nextTwitchLog;

		/// <summary>
		/// How twitching is told - over the last TwitchWindow seconds, starting to move after standing still at least
		/// TwitchStarts times, or turning back the other way at least TwitchReversals times after turning at least
		/// TwitchTurn degrees - and the fewest seconds between its reports in the log
		/// </summary>
		private const float TwitchWindow = 3f;
		private const int TwitchStarts = 6;
		private const int TwitchReversals = 8;
		private const float TwitchTurn = 3f;
		private const float TwitchLogInterval = 10f;

		/// <summary>
		/// Whether each thing lying on the ground is in a pen, and until when that's known, by its ZDO - with how long an
		/// answer is kept, in seconds, and the most kept before they're all forgotten
		/// </summary>
		private static readonly Dictionary<ZDOID, KeyValuePair<bool, float>> penChecks = new Dictionary<ZDOID, KeyValuePair<bool, float>>();
		private const float PenCheckTime = 60f;
		private const int PenChecksMax = 2000;


		/// <summary>
		/// How far the viking reaches to pick something up from where it can stand, in meters, and how many things it
		/// tries before it gives up looking for one it can reach
		/// </summary>
		private const float PickReach = 2f;

		/// <summary>
		/// How many of the nearest things a viking is after - loot, food or wood - it tries to find a way to in one look
		/// before giving up for now
		/// </summary>
		private const int ReachTries = 4;

		/// <summary>
		/// How the viking waits for a corpse to rise as a zombie - how far from it the corpse can be, and how near and
		/// how far from the corpse it stands while it waits, in meters
		/// </summary>
		private const float WaryRange = 25f;
		private const float WaitNear = 3f;
		private const float WaitFar = 6f;

		/// <summary>
		/// How the vikings mourn one of their village who died - how far from its corpse a viking comes to mourn it, how
		/// far from the corpse no enemy can be, and how far from it they stand, in meters, the most who mourn it at once,
		/// how long each stands there, how long between its emotes of grief and how long before it speaks, in seconds,
		/// and how likely each one after the first speaks at all
		/// </summary>
		private const float MournRange = 25f;
		private const float MournClear = 20f;
		private const float MournDistance = 1.8f;
		private const int MournersMax = 5;
		private const float MournTimeMin = 15f;
		private const float MournTimeMax = 25f;
		private const float GrieveIntervalMin = 5f;
		private const float GrieveIntervalMax = 9f;
		private const float MournSpeakDelayMin = 1.5f;
		private const float MournSpeakDelayMax = 5f;
		private const float MournSpeakChance = 0.5f;

		/// <summary>
		/// How often a viking looks for a corpse to mourn, in seconds
		/// </summary>
		private const float MournCheckInterval = 2f;

		/// <summary>
		/// How a viking leaving a village that was disbanded says goodbye - how near a player has to be for it to, in
		/// meters, the soonest and latest it speaks, so the vikings around don't all speak at once, and how long it stands
		/// facing the player after, in seconds
		/// </summary>
		private const float GoodbyeRange = 20f;
		private const float GoodbyeDelayMin = 0.3f;
		private const float GoodbyeDelayMax = 3f;
		private const float GoodbyeStand = 3f;

		/// <summary>
		/// How a viking keeps guard through a raid on its village - the least and most time it stands at one spot before
		/// it moves to another, and the least time between two calls a village's vikings on this client make as a raid
		/// comes on by day, in seconds
		/// </summary>
		private const float GuardTimeMin = 20f;
		private const float GuardTimeMax = 40f;
		private const float RaidCallInterval = 300f;

		/// <summary>
		/// Where a viking keeps guard around the fire - how far past the archers' ring the ring of those fighting hand to
		/// hand starts, in meters, how many spots it tries, how many degrees around the fire each try turns farther from
		/// its own side and how many it shifts at random, how far above the water a spot has to be, in meters, and how
		/// many degrees either side of straight out from the fire it looks
		/// </summary>
		private const float GuardRingGap = 3f;
		private const int GuardTries = 12;
		private const float GuardStep = 25f;
		private const float GuardJitter = 10f;
		private const float GuardWaterClearance = 0.3f;
		private const float GuardLookSpread = 60f;

		/// <summary>
		/// Whether the viking has noticed the raid on its village - awake, or woken to it - and when a viking of each
		/// village on this client last called one out, by its generator's or anchor's ZDO
		/// </summary>
		private bool raidNoticed;
		private static readonly Dictionary<ZDOID, float> lastRaidCall = new Dictionary<ZDOID, float>();

		/// <summary>
		/// Where the viking has stood since when, for the debug hover text to tell how long it's been standing still -
		/// it's still while it keeps within StillReach of there, in meters
		/// </summary>
		private Vector3 stillAt;
		private float stillSince;
		private const float StillReach = 1f;

		/// <summary>
		/// When the viking's stillness was last checked, and how long it can go unchecked before it starts over, in
		/// seconds - longer means its life wasn't running here
		/// </summary>
		private float stillCheckedAt;
		private const float StillGap = 5f;

		/// <summary>
		/// How long the viking has to stand still for the debug hover text to say so, and to be logged as stuck at a job
		/// that shouldn't keep it still that long (see LogIfStuck), in seconds
		/// </summary>
		private const float StillShowTime = 10f;
		private const float StuckLogTime = 180f;

		/// <summary>
		/// The job the viking was last logged as stuck at, so each is only logged once
		/// </summary>
		private Job loggedStuck;

		/// <summary>
		/// When the viking was last resting, sitting, lying, in bed, on something or gathering at a fire, which keep it
		/// still without it being stuck (see LogIfStuck)
		/// </summary>
		private float stillRestedAt;

		/// <summary>
		/// Whether the viking tried to walk somewhere this AI update (see Go), and for how many seconds it has tried
		/// while standing still, for FreeIfTrapped
		/// </summary>
		private bool walkedThisUpdate;
		private float walkingStill;

		/// <summary>
		/// How many of the viking's moves were stopped short of a ledge or dangerous ground (see VikingDangerStepPatch)
		/// since it last moved from where it stands, for the log when it's freed
		/// </summary>
		private int stepsStopped;

		/// <summary>
		/// How many seconds a viking tries to walk somewhere without getting anywhere before it's freed (see
		/// FreeIfTrapped), and how far around it a spot to put it is looked for, in steps of FreeStep, in meters, with
		/// FreeSides spots at each step
		/// </summary>
		private const float FreeAfter = 90f;
		private const float FreeReach = 9f;
		private const float FreeStep = 1.5f;
		private const int FreeSides = 8;

		/// <summary>
		/// Writes to the log when the viking has stood still for StuckLogTime at a job that shouldn't keep it still that
		/// long - anything but resting, sitting, lying, in bed or sitting on something, or gathering at a fire, counting
		/// only from when it last stopped doing those, so a night in bed isn't taken for being stuck the morning after -
		/// with what the job says it's doing and what moved the viking last, once for each job, so a job that never
		/// finishes can be found
		/// </summary>
		private void LogIfStuck()
		{
			if (job == null)
			{
				return;
			}
			if (attachPoint != null || inBed || job.Resting() || job.Sitting() || job is GatherJob)
			{
				stillRestedAt = Time.time;
				return;
			}
			float still = Time.time - Mathf.Max(stillSince, stillRestedAt);
			if (job == loggedStuck || still < StuckLogTime)
			{
				return;
			}
			loggedStuck = job;
			TestLog.Count("Troubles", "stuck at a job");
			TestLog.Count("Stuck, by job", LogJobName());
			Plugin.Log.LogWarning("Stuck: " + LogName() + " of the village at " + ((village != null) ? village.Farm.ToString("F0") : "none") + " has stood still for " + still.ToString("F0") + "s at its job: " + job.Describe() + ", for " + (Time.time - job.StartedAt).ToString("F0") + "s, moved by " + mover + (VillageRaids.On(village) ? ", with a raid on" : "") + (IsNight() ? ", at night" : ""));
		}

		/// <summary>
		/// Frees a viking in its village that has tried to walk somewhere for FreeAfter seconds without getting
		/// anywhere - shut in beside its bed by the furniture, at the far side of a pen too big to tell from open ground,
		/// or anywhere else it has no way out of - putting it at the nearest spot with a way to the village's open ground
		/// (see FreeSpot), rather than leaving it there to go hungry. When the spot it stands on has a way there already,
		/// what holds it is something the navmesh doesn't show, which the spots around it share, so it's put at the
		/// open ground itself
		/// </summary>
		private void FreeIfTrapped()
		{
			if (walkingStill < FreeAfter)
			{
				return;
			}
			walkingStill = 0f;
			if (village == null || attachPoint != null || inBed || road != Road.None || !village.Contains(transform.position) || Pathfinding.instance == null)
			{
				return;
			}
			Vector3 from = transform.position;
			Vector3 open = village.RallyPoint();
			bool wayOut = WayToOpenGround(from, open, out string own);
			// What it was doing is written down before it's moved, which starts its path and its moves over
			string report = ((TestLog.On || Player.m_debugMode) ? FreeReport(own) : null);
			Vector3 spot;
			bool found = (wayOut ? NavmeshPoints.FindValidPoint(out spot, open, FreeStep, Pathfinding.AgentType.Humanoid) : FreeSpot(from, open, FreeReach, out spot));
			if (!found)
			{
				return;
			}
			Vector3 freed = spot + Vector3.up * 0.1f;
			transform.position = freed;
			humanoid.m_body.position = freed;
			humanoid.m_body.linearVelocity = Vector3.zero;
			ai.StopMoving();
			ResetStuck();
			TestLog.Count("Troubles", "freed after walking without getting anywhere");
			TestLog.Count("Freed, by where to", (wayOut ? "the open ground, its own spot having a way there" : "the nearest spot with a way to the open ground"));
			if (report != null)
			{
				Plugin.Log.LogInfo("Freed: " + TestLog.Label(humanoid) + " at " + from.ToString("F1") + " tried to walk for " + FreeAfter.ToString("F0") + "s without getting anywhere, moved by " + mover + ", at its job: " + ((job != null) ? job.Describe() : "none") + ", so it's put at " + freed.ToString("F1") + ", " + Utils.DistanceXZ(from, freed).ToString("F1") + "m away" + (wayOut ? ", on the village's open ground" : "") + "\n" + report);
			}
		}

		/// <summary>
		/// Checks whether the spot a viking stands on has a whole way to the village's open ground - on the navmesh
		/// within FreeStep of it, out of any pen and off the roofs, the way FreeSpot checks the spots around it
		/// </summary>
		/// <param name="from">Where the viking stands</param>
		/// <param name="open">The spot on the village's open ground (see Village.RallyPoint)</param>
		/// <param name="state">What was found, for the log</param>
		/// <returns>True when it has</returns>
		private static bool WayToOpenGround(Vector3 from, Vector3 open, out string state)
		{
			if (!NavmeshPoints.FindValidPoint(out Vector3 own, from, FreeStep, Pathfinding.AgentType.Humanoid))
			{
				state = "no navmesh within " + FreeStep.ToString("F1") + "m";
				return(false);
			}
			string at = "its navmesh at " + own.ToString("F1") + ", " + Utils.DistanceXZ(from, own).ToString("F1") + "m away";
			if (Village.FencedIn(own))
			{
				state = at + ", in a pen";
				return(false);
			}
			if (NavmeshRoofs.OnRoof(own))
			{
				state = at + ", on a roof";
				return(false);
			}
			if (!Pathfinding.instance.HavePath(own, open, Pathfinding.AgentType.Humanoid))
			{
				state = at + ", with no whole way to the open ground at " + open.ToString("F1");
				return(false);
			}
			state = at + ", with a whole way to the open ground at " + open.ToString("F1");
			return(true);
		}

		/// <summary>
		/// Describes what a viking about to be freed was doing, for the log - the spot it stands on, the path its AI
		/// follows, whether it was trying to move and how fast it went, how many of its moves stopped short of a ledge
		/// or dangerous ground, and its last moves
		/// </summary>
		/// <param name="own">What WayToOpenGround found for the spot it stands on</param>
		/// <returns>The report's lines</returns>
		private string FreeReport(string own)
		{
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			text.Append("  Own spot: " + own + "\n");
			text.Append("  Path: " + PathText() + ", the last search " + ((ai != null && ai.m_lastFindPathResult) ? "found a way" : "found none") + "\n");
			Vector3 move = humanoid.GetMoveDir();
			Vector3 velocity = humanoid.m_body.linearVelocity;
			text.Append("  Moving: " + ((new Vector3(move.x, 0f, move.z).sqrMagnitude > 0.01f) ? "trying to" : "not trying to") + ", at " + new Vector3(velocity.x, 0f, velocity.z).magnitude.ToString("F2") + "m/s\n");
			text.Append("  Steps stopped at a ledge or dangerous ground: " + stepsStopped + " since it last got anywhere\n");
			text.Append("  Trace (M moving, . still, then facing): " + MoveTrace());
			return(text.ToString());
		}

		/// <summary>
		/// Finds the nearest spot around a trapped viking it can walk to the village's open ground from - on the navmesh,
		/// out of any pen, off the roofs and clear of fires, looking further out a FreeStep at a time, up to a reach -
		/// or else the open ground itself
		/// </summary>
		/// <param name="from">Where the viking stands</param>
		/// <param name="open">The spot on the village's open ground (see Village.RallyPoint)</param>
		/// <param name="reach">How far out it looks, in meters</param>
		/// <param name="spot">The spot found</param>
		/// <returns>False when the open ground isn't on the navmesh either</returns>
		private static bool FreeSpot(Vector3 from, Vector3 open, float reach, out Vector3 spot)
		{
			for (float step = FreeStep; step <= reach; step += FreeStep)
			{
				for (int i = 0; i < FreeSides; i++)
				{
					Vector3 point = from + Quaternion.Euler(0f, i * 360f / FreeSides, 0f) * Vector3.forward * step;
					if (!NavmeshPoints.FindValidPoint(out Vector3 valid, point, FreeStep, Pathfinding.AgentType.Humanoid))
					{
						continue;
					}
					if (Village.FencedIn(valid) || NavmeshRoofs.OnRoof(valid) || Village.NearFlames(valid))
					{
						continue;
					}
					if (Pathfinding.instance.HavePath(valid, open, Pathfinding.AgentType.Humanoid))
					{
						spot = valid;
						return(true);
					}
				}
			}
			return(NavmeshPoints.FindValidPoint(out spot, open, FreeStep, Pathfinding.AgentType.Humanoid));
		}

		/// <summary>
		/// The most corpses a viking remembers having mourned, and the vikings on this client remember having said
		/// farewell to, before they forget them all - a corpse lies no more than a minute or so
		/// </summary>
		private const int CorpseMemory = 64;

		/// <summary>
		/// How far the viking keeps from a poison cloud or a zombie's corpse that might leave one, in meters
		/// </summary>
		private const float PoisonAvoid = 3f;

		/// <summary>
		/// How far past PoisonAvoid the viking stops on its way to a spot within PoisonAvoid of poison, in meters, how long
		/// it waits there for the poison to go before giving up on the spot, and how long a break in the wait starts it
		/// over, in seconds
		/// </summary>
		private const float PoisonWaitReach = 1f;
		private const float PoisonWaitTime = 10f;
		private const float PoisonWaitBreak = 1f;

		/// <summary>
		/// Whether the viking is stepping away from poison, and when it started waiting for poison to go from where it's
		/// walking and last waited, or -1 while it isn't waiting
		/// </summary>
		private bool steppingFromPoison;
		private float poisonWaitSince = -1f;
		private float poisonWaitLast;

		/// <summary>
		/// How near a fire's flames the viking's body can get before it runs out, how far clear of them it runs before
		/// it stops, in meters, and seconds between looks for flames while it isn't running from them
		/// </summary>
		private const float FlameStep = 0.5f;
		private const float FlameClear = 1f;
		private const float FlameCheckInterval = 0.2f;

		/// <summary>
		/// Whether the viking is running out of a fire's flames, and when it next looks for flames
		/// </summary>
		private bool escapingFlames;
		private float nextFlameCheck;

		/// <summary>
		/// How a viking fighting hand to hand deals with a root, or any monster that can't move - how far it keeps from
		/// one, past its 4 meter lash, how near one it calls an archer over, in meters, and how often it calls, in
		/// seconds
		/// </summary>
		private const float RootKeepAway = 6f;
		private const float RootCallRange = 15f;
		private const float ArcherCallInterval = 15f;

		/// <summary>
		/// What a viking calls out when it finds a root, with an archer in the village to shoot it and without
		/// </summary>
		private static readonly string[] ArcherCalls = { "Archer! There's a root here!", "Bring a bow - a root!", "Shoot this root, I can't get near it!" };
		private static readonly string[] RootWarnings = { "Roots! Keep clear of them!", "A root - stay out of its reach!" };

		/// <summary>
		/// How the viking gets off a roof it finds itself on - seconds between checks, how far it looks for ground that
		/// isn't a roof, in meters, and seconds it leaves it after finding none
		/// </summary>
		private const float RoofCheckInterval = 1f;

		/// <summary>
		/// Seconds between checks for whether the viking is in a pen it has no business in
		/// </summary>
		private const float PenCheckInterval = 2f;
		private const float RoofExitSearch = 10f;
		private const float RoofGiveUpTime = 15f;

		/// <summary>
		/// How the viking gets out of water it stands about in - seconds between checks, the least it moves between two
		/// checks to count as on its way somewhere, in meters, how far it looks for dry ground, how far apart the rings
		/// and how many directions it looks in, how far above the water level ground is dry, in meters, and seconds it
		/// leaves it after it can't get out
		/// </summary>
		private const float WaterCheckInterval = 3f;

		/// <summary>
		/// How a viking held up by others standing about gets them to make way - seconds it gets no nearer before it
		/// asks, and between asks, how far ahead of it and to either side of its way they can stand, how far they step
		/// aside, in meters, and the most seconds they take to
		/// </summary>
		private const float MakeWayAfter = 1f;
		private const float MakeWayReach = 2.5f;
		private const float MakeWayWidth = 1.2f;
		private const float MakeWayStep = 2f;
		private const float MakeWayTime = 4f;
		private const float WaterStillDistance = 1f;
		private const float WaterExitSearch = 16f;
		private const float WaterExitStep = 2f;
		private const int WaterExitDirections = 16;
		private const float DryHeight = 0.3f;
		private const float WaterGiveUpTime = 20f;

		/// <summary>
		/// How a viking on fire puts itself out - how far it looks for water, how far below the water level the ground
		/// has to be for the water to soak it, in meters, and seconds between looks when there's none near
		/// </summary>
		private const float DouseReach = 24f;
		private const float DouseDepth = 0.4f;

		/// <summary>
		/// How far under the water the ground under an enemy can be before a viking fighting hand to hand leaves it alone,
		/// in meters
		/// </summary>
		private const float WadeDepth = 1f;
		private const float DouseRetryTime = 2f;

		/// <summary>
		/// The layers of what the viking can jump over - buildings, fences, trees and rocks
		/// </summary>
		private static int hopMask;

		/// <summary>
		/// The living vikings this client has loaded
		/// </summary>
		public static readonly HashSet<VillageLife> All = new HashSet<VillageLife>();

		/// <summary>
		/// What each viking working on a piece or a branch has claimed, by the piece's ZDO or where a missing piece
		/// stood, so two vikings this client owns don't work on the same one
		/// </summary>
		private static readonly Dictionary<object, VillageLife> workClaims = new Dictionary<object, VillageLife>();

		private Humanoid humanoid;
		private ZNetView nview;
		private MonsterAI ai;
		private VikingCore viking;

		/// <summary>
		/// The viking's body, the same component as humanoid, for what it eats, its rest, its load and its gear changes
		/// </summary>
		private LivingVikingBody body;

		private Village village;
		private float nextVillageCheck;

		/// <summary>
		/// Until when the viking leaves work that needs wood, after finding none to gather
		/// </summary>
		private float noWoodUntil;

		/// <summary>
		/// When the viking next looks through the game's recipes for gear better than its own to craft, and how often, in
		/// seconds, since it weighs every weapon, armor and shield there is
		/// </summary>
		private float nextGearCraftCheck;
		private const float GearCraftCheckInterval = 15f;

		/// <summary>
		/// Whether the viking was out from under a roof when it last looked, when it next looks, and how often, in
		/// seconds, for hurrying through the rain (see RunsInRain)
		/// </summary>
		private bool roofless;
		private float nextRainRoofCheck;
		private const float RainRoofCheck = 1f;

		/// <summary>
		/// Until when the viking leaves crafting a torch at dusk or night, after the village's chests couldn't give it
		/// what one takes
		/// </summary>
		private float nightTorchAgainAt;

		/// <summary>
		/// When the viking next checks whether it's resting by a fire, and until when it leaves resting by the fire after
		/// it couldn't get a seat there
		/// </summary>
		private float nextRestCheck;
		private float restAgainAt;

		/// <summary>
		/// Until when the viking keeps warm inside its house rather than by the fire, after it couldn't get warm there
		/// </summary>
		private float warmInsideUntil;

		/// <summary>
		/// When a viking strolling, standing about or petting a boar next looks for work it could be doing instead
		/// </summary>
		private float nextWorkCheck;

		/// <summary>
		/// The job the viking last looked at using a forsaken power for, when it started, when the job first took it out
		/// of the village, or -1 while it hasn't, and when it next looks
		/// </summary>
		private Job powerJob;
		private float powerJobAt;
		private float powerOutingAt = -1f;
		private float nextJobPowerCheck;

		/// <summary>
		/// When the viking last had an enemy to fight
		/// </summary>
		private float lastFightAt = float.MinValue;

		/// <summary>
		/// When the viking goes to bed tonight, or -1 before it's picked (see Bedtime)
		/// </summary>
		private float bedAt = -1f;

		/// <summary>
		/// When the viking last called an archer over to a root (see CallArcher)
		/// </summary>
		private float lastArcherCall = float.MinValue;

		/// <summary>
		/// Until when a wounded viking leaves eating after it found no food it could eat
		/// </summary>
		private float eatAgainAt;

		/// <summary>
		/// Until when the viking holds on to an item the chests hold enough of, when it may not leave the village to drop
		/// it outside or found nowhere to drop it, and since when it has found nowhere, or a negative number while it
		/// hasn't
		/// </summary>
		private float dumpAgainAt;
		private float dumpNowhereSince = -1f;

		/// <summary>
		/// Whether the viking is running back to its village's core for help in a fight, and why - set by a foraging
		/// viking's job as an enemy comes near, with no reason given
		/// </summary>
		private bool fleeing;
		private FleeReason fleeReason;

		/// <summary>
		/// The viking of its village it runs to once it's home with no one beside it, or null while it runs for the
		/// village's rally point
		/// </summary>
		private Character fleeTo;

		/// <summary>
		/// How near another viking of its village has to be for a viking that ran home to turn and fight, in meters
		/// </summary>
		private const float RegroupRange = 6f;

		/// <summary>
		/// Whether the viking, badly hurt at home, is keeping out of the fight to let the others fight, and when it next
		/// weighs up the fight
		/// </summary>
		private bool hangingBack;
		private float nextTactics;

		/// <summary>
		/// The enemy a viking hanging back is working its way behind to strike, until when it keeps running from an
		/// enemy that came after it, and when it next shouts for help
		/// </summary>
		private Character flanked;
		private float runUntil;
		private float nextShout;

		/// <summary>
		/// The enemy the viking last went after and when it can next call out an enemy it spots, and when any viking on
		/// this client last called out an enemy, by the enemy's ZDO, and when one last did at all
		/// </summary>
		private Character lastTarget;

		/// <summary>
		/// The enemy the viking is fighting and where it last stood, and where and when the last one it fought died, for
		/// picking up what it drops (see FindKillLoot)
		/// </summary>
		private Character fought;
		private Vector3 foughtAt;
		private Vector3 killSpot;
		private float killAt = -1f;
		private float nextCallOut;
		private static readonly Dictionary<ZDOID, float> calledOut = new Dictionary<ZDOID, float>();
		private static float lastCallOut = -SpotGap;

		/// <summary>
		/// The player the viking is greeting and until when it stands facing them, when it next looks for a player to
		/// greet, and when any viking on this client last had each player near, by the player's ZDO
		/// </summary>
		private Player greeting;
		private float greetUntil;
		private float nextGreetCheck;
		private static readonly Dictionary<ZDOID, float> playerLastNear = new Dictionary<ZDOID, float>();

		/// <summary>
		/// Why a viking runs home in a fight - it was foraging, it's alone and was attacked, it's outnumbered, it's badly
		/// hurt, or a viking with it is
		/// </summary>
		internal enum FleeReason
		{
			None,
			Forage,
			Alone,
			Outnumbered,
			Wounded,
			Escort
		}

		/// <summary>
		/// The chest the viking has open, and when it closes it
		/// </summary>
		private Container openChest;
		private float closeChestAt;

		/// <summary>
		/// The chests and loot the viking couldn't get to, and until when it leaves each alone
		/// </summary>
		private readonly Dictionary<ZDOID, float> avoided = new Dictionary<ZDOID, float>();

		/// <summary>
		/// The pieces the viking couldn't get to to repair or clear snow off, and until when it leaves each to the others
		/// - kept apart from avoided, so a chest it gave up repairing is still one it eats from and stores in
		/// </summary>
		private readonly Dictionary<ZDOID, float> hammerAvoided = new Dictionary<ZDOID, float>();

		/// <summary>
		/// What the vikings this client runs have all given up on reaching, and when it's next pruned of what's
		/// forgotten, every GiveUpPruneInterval seconds
		/// </summary>
		private static readonly VillageGiveUps giveUps = new VillageGiveUps();
		private static float nextGiveUpPrune;
		private const float GiveUpPruneInterval = 60f;

		/// <summary>
		/// What the viking alone has given up on reaching, after failing at it Tries times itself within Window (see
		/// VillageGiveUps), whether or not any other viking tried - so one left on its own stops going back to it
		/// </summary>
		private readonly VillageGiveUps ownGiveUps = new VillageGiveUps(1);

		/// <summary>
		/// What the viking is doing, or null when it has nothing to do
		/// </summary>
		private Job job;

		/// <summary>
		/// Whether the viking's life is running - in its village
		/// </summary>
		private bool active;

		/// <summary>
		/// The AI's sight and hearing, put back when the viking stops resting
		/// </summary>
		private float viewRange;
		private float hearRange;
		private bool dulled;

		/// <summary>
		/// The bed or chair the viking lies or sits on, and its colliders, which the viking's own ignores
		/// </summary>
		private Transform attachPoint;
		private Collider[] attachColliders;

		/// <summary>
		/// Whether the viking is lying in a bed, asleep
		/// </summary>
		private bool inBed;

		/// <summary>
		/// The enemy the viking is defending someone from or that hurt it, and until when it chases it past its leash
		/// </summary>
		private Character grudge;
		private float grudgeUntil;

		/// <summary>
		/// Whether the viking is walking back into its village from far outside it
		/// </summary>
		private bool returning;

		/// <summary>
		/// When the night watch last raised the alarm, and until when it stands roaring
		/// </summary>
		private float lastAlarm = -AlarmInterval;
		private float alarmUntil = -1f;

		/// <summary>
		/// Whether the last walk was given up, because there's no way there or the viking got stuck
		/// </summary>
		private bool moveFailed;
		private Vector3 stuckTarget;
		private float stuckBest;
		private Vector3 stuckFrom;
		private float stuckSince;

		/// <summary>
		/// Whether the viking has opened or closed a door beside it since it last got anywhere (see CheckStuck), so a
		/// door that didn't free it doesn't hold off giving up
		/// </summary>
		private bool doorTried;

		/// <summary>
		/// How many times the viking has jumped toward where it's walking, and its jump over a fence - what it's doing
		/// for it, which way it jumps, where the fence it's jumping is and how high above where it started its top is,
		/// until when it keeps at what it's doing, and whether it has left the ground yet
		/// </summary>
		private int hops;
		private HopPhase hopPhase = HopPhase.None;
		private Vector3 hopDirection;
		private Vector3 hopPoint;
		private float hopTop;
		private float hopPhaseUntil;
		private bool hopLeftGround;

		/// <summary>
		/// For the log of how the viking's jump over a fence ended (see LogHopEnd) - the piece it jumps, how high its top
		/// stands, where the viking left the ground and how far in front of the fence, and the highest its feet got
		/// </summary>
		private string hopPiece;
		private float hopFenceTop;
		private Vector3 hopTakeoff;
		private float hopTakeoffAhead;
		private float hopPeak;

		/// <summary>
		/// How a jump over a fence ended, for the log (see HopResult) - with the viking past the fence's face by at least
		/// HopOverPast, in meters, or with its feet within HopOnTopDrop of the fence's top short of that
		/// </summary>
		private const float HopOverPast = 0.4f;
		private const float HopOnTopDrop = 0.3f;

		/// <summary>
		/// What the viking is doing to jump a fence - nothing, backing up for its run-up, running at the fence, or in
		/// the air over it
		/// </summary>
		private enum HopPhase
		{
			None,
			BackUp,
			RunUp,
			Air
		}

		/// <summary>
		/// Where a jump over a fence left the viking - past the fence, on top of it, or back on the side it jumped from
		/// </summary>
		internal enum HopOutcome
		{
			Over,
			OnTop,
			FellBack
		}

		/// <summary>
		/// The corpse the viking is waiting for to rise as a zombie, or ZDOID.None while it isn't
		/// </summary>
		private ZDOID waitingFor = ZDOID.None;

		/// <summary>
		/// The corpses of the viking's village it has mourned, by ZDO, and when it next looks for one to mourn
		/// </summary>
		private readonly HashSet<ZDOID> mourned = new HashSet<ZDOID>();
		private float nextMournCheck;

		/// <summary>
		/// The corpses about to rise that a viking on this client has said farewell to as it stood ready by them, by ZDO,
		/// so only one does for each
		/// </summary>
		private static readonly HashSet<ZDOID> farewelled = new HashSet<ZDOID>();

		/// <summary>
		/// Whether the viking has started leaving a village that was disbanded, the player it says goodbye to, or null
		/// when none was near, when it says it, or -1 once it has, and when it runs off
		/// </summary>
		private bool leaving;
		private Player goodbyeTo;
		private float goodbyeAt;
		private float runOffAt;

		/// <summary>
		/// Whether the viking is walking off a roof it found itself on, where to, and when it next checks for one
		/// </summary>
		private bool gettingOffRoof;

		/// <summary>
		/// Whether the viking is getting out of a pen it found itself in, and when it next checks for one
		/// </summary>
		private bool gettingOutOfPen;
		private float nextPenCheck;

		/// <summary>
		/// Whether the wood the viking is after lies in a pen, and whether its job took it into one for wood, which
		/// holds until it's back out of the pen
		/// </summary>
		private bool woodInPen;
		private bool inPenForWood;

		/// <summary>
		/// Where the viking steps aside to while making way for another and until when, and when it next asks others
		/// in its way to make way for it
		/// </summary>
		private Vector3 makeWaySpot;
		private float makeWayUntil;
		private float nextAskWay;
		private Vector3 roofExit;
		private float nextRoofCheck;

		/// <summary>
		/// Whether the viking is getting out of the water, where it's headed, when it next checks, whether it was in the
		/// water at the last check and where it stood then
		/// </summary>
		private bool gettingOutOfWater;
		private Vector3 waterExit;
		private float nextWaterCheck;
		private bool wasInWater;
		private Vector3 waterCheckAt;

		/// <summary>
		/// Whether the viking is running to the water to put itself out, where it's headed, and when it next looks for
		/// water
		/// </summary>
		private bool dousing;
		private Vector3 douseSpot;
		private float nextDouseCheck;

		/// <summary>
		/// How near the viking has got to the enemy it's after, and since when it hasn't got nearer, for jumping a fence
		/// between them
		/// </summary>
		private Character chased;
		private float chaseBest;
		private float chaseSince;

		/// <summary>
		/// Whether a pen's fence stands between the viking and the enemy it's after, whether there's a way around it,
		/// when it's next looked for, and since when the viking has been after that enemy
		/// </summary>
		private bool fenceBetween;
		private bool fencePath;
		private float nextFenceCheck;
		private float fenceSince;

		/// <summary>
		/// Finds the viking's parts and notes it among the loaded vikings
		/// </summary>
		private void Awake()
		{
			humanoid = GetComponent<Humanoid>();
			nview = GetComponent<ZNetView>();
			ai = GetComponent<MonsterAI>();
			viking = GetComponent<VikingCore>();
			body = GetComponent<LivingVikingBody>();
			if (ai != null)
			{
				// A living viking keeps a sharp lookout, so it spots monsters before they're on it
				ai.m_viewRange = Mathf.Max(ai.m_viewRange, SightRange);
				viewRange = ai.m_viewRange;
				hearRange = ai.m_hearRange;
			}
			if (nview != null && nview.GetZDO() != null)
			{
				nview.Register<ZDOID>(TalkRpc, RPC_Talk);
				nview.Register<string, string>(NameRpc, RPC_Name);
				nview.Register<ZDOID, bool>(NamingRpc, RPC_Naming);
			}
			All.Add(this);
		}

		/// <summary>
		/// Has a player talk to the viking, or with the alternate key down name it, when it's a living viking that isn't
		/// their enemy - its owner answers
		/// </summary>
		/// <param name="user">Who's talking to it</param>
		/// <param name="hold">Whether the key is held</param>
		/// <param name="alt">Whether the alternate key is down</param>
		/// <returns>False, so the player doesn't reach out to it</returns>
		public bool Interact(Humanoid user, bool hold, bool alt)
		{
			if (hold || !CanTalkTo(user as Player))
			{
				return(false);
			}
			if (alt)
			{
				if (VikingNames.CanName() && TextInput.instance != null)
				{
					TextInput.instance.RequestText(this, "$hud_rename", VikingNames.MaxLength);
					naming = this;
					nextNamingPing = Time.time + NamingPingInterval;
					nview.InvokeRPC(NamingRpc, user.GetZDOID(), true);
				}
				return(false);
			}
			nview.InvokeRPC(TalkRpc, user.GetZDOID());
			return(false);
		}

		/// <summary>
		/// Keeps the viking the local player is naming facing them while the box to name it is open, telling its owner
		/// every NamingPingInterval seconds, and closes the box once the viking gets more than NamingRange from the
		/// player - in a fight, say - call it from the plugin's Update
		/// </summary>
		public static void UpdateNaming()
		{
			if (ReferenceEquals(naming, null))
			{
				return;
			}
			TextInput input = TextInput.instance;
			// A viking that unloaded has no one to give the name to
			if (naming == null)
			{
				if (input != null && ReferenceEquals(input.m_queuedSign, naming))
				{
					input.Hide();
					input.m_queuedSign = null;
				}
				naming = null;
				return;
			}
			bool open = (input != null && input.m_panel != null && input.m_panel.activeSelf && ReferenceEquals(input.m_queuedSign, naming));
			Player player = Player.m_localPlayer;
			bool valid = (naming.nview != null && naming.nview.IsValid());
			if (open && player != null && valid && Vector3.Distance(player.transform.position, naming.transform.position) > NamingRange)
			{
				input.Hide();
				input.m_queuedSign = null;
				open = false;
			}
			if (!open || player == null || !valid)
			{
				if (player != null && valid)
				{
					naming.nview.InvokeRPC(NamingRpc, player.GetZDOID(), false);
				}
				naming = null;
				return;
			}
			if (Time.time >= nextNamingPing)
			{
				nextNamingPing = Time.time + NamingPingInterval;
				naming.nview.InvokeRPC(NamingRpc, player.GetZDOID(), true);
			}
		}

		/// <summary>
		/// Has the viking stand facing the player naming it while the box to name it is open, on its owner, unless it has
		/// a fight to go to - for NamingHold seconds after each word that it's still open, and NamingLinger once it closes
		/// </summary>
		/// <param name="sender">The client that sent it</param>
		/// <param name="playerId">The player's ZDO</param>
		/// <param name="open">Whether the box is still open</param>
		private void RPC_Naming(long sender, ZDOID playerId, bool open)
		{
			if (!nview.IsOwner())
			{
				return;
			}
			GameObject found = ((ZNetScene.instance != null) ? ZNetScene.instance.FindInstance(playerId) : null);
			Player player = ((found != null) ? found.GetComponent<Player>() : null);
			if (!CanTalkTo(player))
			{
				return;
			}
			if (open)
			{
				// A viking that doesn't know the player greets them as they go to name it, or gets to know them quietly when
				// it can't, so they see the name they give it
				if (!GreetIfStranger(player))
				{
					VikingSpeech.Meet(humanoid, player.GetPlayerID());
				}
				greeting = player;
				greetUntil = Time.time + NamingHold;
				playerLastNear[player.GetZDOID()] = Time.time;
			}
			else if (greeting == player)
			{
				greetUntil = Mathf.Min(greetUntil, Time.time + NamingLinger);
			}
		}

		/// <summary>
		/// Until when the viking leaves its gear as it is, while a player has its inventory open, in Time.time seconds
		/// </summary>
		private float gearHeldUntil;

		/// <summary>
		/// Since when each piece of its own gear the viking would wear has sat in its inventory unworn, in Time.time
		/// seconds, and when it next looks for any
		/// </summary>
		private readonly Dictionary<ItemDrop.ItemData, float> unwornSince = new Dictionary<ItemDrop.ItemData, float>();
		private float nextGearCheck;

		/// <summary>
		/// How long the viking leaves gear of its own it would wear in its inventory before putting it on, and how often
		/// it looks for any, in seconds
		/// </summary>
		private const float PutOnDelay = 10f;
		private const float GearCheckInterval = 2f;

		/// <summary>
		/// Puts a piece of gear on as its change in the body's queue is done (see LivingVikingBody.UpdateGearChanges), the
		/// way the viking takes up gear it found (see VikingCore.TakeUpGear), or takes it off - from its back too, when
		/// it's sheathed there
		/// </summary>
		/// <param name="change">The change</param>
		/// <param name="item">The piece it's for - its item, or the one in the other hand</param>
		private void ChangeGear(LivingVikingBody.GearChange change, ItemDrop.ItemData item)
		{
			if (change.PutOn)
			{
				if (viking != null && change.Chosen)
				{
					viking.Wear(item);
				}
				else if (viking != null && !GearHeld())
				{
					bool tookUp = viking.TakeUpGear(item, out ItemDrop.ItemData replaced);
					if (change.Found)
					{
						FoundGearDone(item, tookUp, replaced);
					}
				}
				return;
			}
			// Takes a sheathed piece off the viking's back too
			humanoid.UnequipItem(item, true);
		}

		/// <summary>
		/// Has the viking switch to gear of its own that's better than what it has - armor for a slot it has nothing in
		/// or better than what it wears there, or a better shield, like gear a player took off it, gave it or had it swap
		/// for something worse - once it has sat in its inventory unused for PutOnDelay seconds, while no player has its
		/// inventory open, on its owner
		/// </summary>
		private void UpdatePutOn()
		{
			if (Time.time < nextGearCheck)
			{
				return;
			}
			nextGearCheck = Time.time + GearCheckInterval;
			if (viking == null || GearHeld() || humanoid.IsDead())
			{
				unwornSince.Clear();
				return;
			}
			// Weapons are left out - the viking picks the best of its own as each fight calls for (see
			// VikingCore.ChooseWeapon) - and so are its dverger circlet and helmets, while it puts those on and takes them
			// off as it wants light (see VikingCore.UpdateTorch)
			List<ItemDrop.ItemData> unworn = humanoid.GetInventory().GetAllItems().FindAll((ItemDrop.ItemData item) => IsGear(item) && !item.IsWeapon() && !VikingItems.IsCarried(item) && !VikingItems.IsProp(item) && !humanoid.IsItemEquiped(item) && !viking.SwapsForLight(item) && viking.WouldTakeUp(item));
			List<ItemDrop.ItemData> forgotten = new List<ItemDrop.ItemData>();
			foreach (ItemDrop.ItemData item in unwornSince.Keys)
			{
				if (!unworn.Contains(item))
				{
					forgotten.Add(item);
				}
			}
			foreach (ItemDrop.ItemData item in forgotten)
			{
				unwornSince.Remove(item);
			}
			foreach (ItemDrop.ItemData item in unworn)
			{
				if (!unwornSince.TryGetValue(item, out float since))
				{
					unwornSince[item] = Time.time;
				}
				else if (Time.time - since >= PutOnDelay)
				{
					body.QueueGear(item, true, false);
				}
			}
		}

		/// <summary>
		/// Has the viking stand facing a player who has its inventory open and leave its gear as it is, for NamingHold
		/// seconds - called every frame the inventory is open, on the viking's owner, which the player's client is
		/// </summary>
		/// <param name="player">The player</param>
		public void HoldForInventory(Player player)
		{
			if (player == null)
			{
				return;
			}
			greeting = player;
			greetUntil = Time.time + NamingHold;
			gearHeldUntil = Time.time + NamingHold;
			playerLastNear[player.GetZDOID()] = Time.time;
		}

		/// <summary>
		/// Lets the viking go back to what it was doing and look after its gear again, once a player has closed its
		/// inventory, after facing them NamingLinger seconds more
		/// </summary>
		public void ReleaseInventory()
		{
			gearHeldUntil = 0f;
			greetUntil = Mathf.Min(greetUntil, Time.time + NamingLinger);
		}

		/// <summary>
		/// Checks whether the viking leaves its gear as it is - not equipping, drawing, sheathing or swapping anything -
		/// while a player has its inventory open
		/// </summary>
		/// <returns>True while it does</returns>
		public bool GearHeld()
		{
			return(Time.time < gearHeldUntil);
		}

		/// <summary>
		/// Gets the viking's name for the box to rename it
		/// </summary>
		/// <returns>The name, or ""</returns>
		public string GetText()
		{
			return(VikingNames.GetRaw(humanoid));
		}

		/// <summary>
		/// Names the viking what the player typed - its owner names it
		/// </summary>
		/// <param name="text">The name</param>
		public void SetText(string text)
		{
			if (nview != null && nview.IsValid())
			{
				nview.InvokeRPC(NameRpc, (text ?? "").Trim(), VikingNames.LocalAuthor());
			}
		}

		/// <summary>
		/// Names the viking, on its owner, and has it answer to its new name - turning down "Viking" or nothing, which
		/// would take its name away, and a creature's, a legend's, a player's or a fellow viking's name (see
		/// VikingNames.Judge)
		/// </summary>
		/// <param name="sender">The client that sent it</param>
		/// <param name="name">The name</param>
		/// <param name="author">Who named it</param>
		private void RPC_Name(long sender, string name, string author)
		{
			if (!nview.IsOwner() || !VikingCore.IsLivingViking(humanoid))
			{
				return;
			}
			bool renamed = (VikingNames.Get(humanoid).Length > 0);
			string plain = VikingNames.Plain(name);
			VikingNames.Verdict verdict = VikingNames.Judge(humanoid, village, plain);
			if (verdict != VikingNames.Verdict.Taken)
			{
				// Markup is said as it was typed, so the line shows what was wrong with it. It's said even in its sleep, so the
				// player isn't left wondering why the name didn't take
				string filtered = VikingNames.Filter(plain, author);
				string shown = ((verdict == VikingNames.Verdict.Markup) ? VikingNames.Literal(filtered) : filtered.RemoveRichTextTags());
				VikingSpeech.Say(humanoid, VikingSpeech.Refused(verdict, shown), VikingSpeech.TalkTime, VikingSpeech.TalkCull, true);
				return;
			}
			VikingNames.Set(humanoid, plain, author);
			string named = VikingNames.Get(humanoid);
			if (named.Length > 0)
			{
				VikingSpeech.Say(humanoid, VikingSpeech.Named(named, renamed), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
			}
		}

		/// <summary>
		/// Leaves items used on the viking to the game
		/// </summary>
		/// <param name="user">Who used it</param>
		/// <param name="item">The item</param>
		/// <returns>False</returns>
		public bool UseItem(Humanoid user, ItemDrop.ItemData item)
		{
			return(false);
		}

		/// <summary>
		/// Checks whether a player can talk to the viking - it's a living viking, alive, and not their enemy
		/// </summary>
		/// <param name="player">The player, or null</param>
		/// <returns>True when they can</returns>
		public bool CanTalkTo(Player player)
		{
			return(player != null && humanoid != null && !humanoid.IsDead() && nview != null && nview.IsValid() && VikingCore.IsLivingViking(humanoid) && !BaseAI.IsEnemy(humanoid, player));
		}

		/// <summary>
		/// Answers a player talking to the viking, on its owner - in a fight or swimming it's busy and asleep it's sleepy, it greets
		/// a player it has never met as a stranger with a wave and remembers them, and chats with one it knows (see
		/// VillageAnswer) or tells them its news of them (see VikingNewsStore), turning to them for TalkFaceTime seconds
		/// either way, and noting the talk once it has said its piece (see VikingIdentity.Answered)
		/// </summary>
		/// <param name="sender">The client that sent it</param>
		/// <param name="playerId">The player's ZDO</param>
		private void RPC_Talk(long sender, ZDOID playerId)
		{
			if (!nview.IsOwner() || Time.time < nextAnswer)
			{
				return;
			}
			GameObject found = ((ZNetScene.instance != null) ? ZNetScene.instance.FindInstance(playerId) : null);
			Player player = ((found != null) ? found.GetComponent<Player>() : null);
			if (!CanTalkTo(player))
			{
				return;
			}
			nextAnswer = Time.time + TalkAnswerInterval;
			if (ai.m_targetCreature != null)
			{
				VikingSpeech.Say(humanoid, VikingSpeech.Busy(), VikingSpeech.QuickTime, VikingSpeech.TalkCull);
				return;
			}
			if (humanoid.IsSwimming())
			{
				VikingSpeech.Say(humanoid, VikingSpeech.Swimming(), VikingSpeech.QuickTime, VikingSpeech.TalkCull);
				return;
			}
			if (inBed)
			{
				VikingSpeech.Say(humanoid, VikingSpeech.Sleepy(), VikingSpeech.TalkTime, VikingSpeech.TalkCull, true);
				return;
			}
			playerLastNear[player.GetZDOID()] = Time.time;
			if (VikingSpeech.Knows(humanoid, player.GetPlayerID()))
			{
				string news = VikingNewsStore.Tell(humanoid, player, NewsBiome());
				string answer = (news ?? (active ? VillageAnswer(player) : RoadAnswer(player)));
				VikingSpeech.Say(humanoid, answer, VikingSpeech.TalkTime, VikingSpeech.TalkCull);
				greeting = player;
				greetUntil = Time.time + TalkFaceTime;
				VikingIdentity.Answered(nview, player.GetPlayerID(), true);
				return;
			}
			Greet(player, TalkFaceTime, true);
		}

		/// <summary>
		/// Picks what a viking at home says when a player it knows talks to it - now and then about a quest of its
		/// village's (see QuestAnswer), the village on the other side of its village's portal (see VillagePortalSpeech),
		/// being old friends or how the player is doing (see VikingFriendSpeech), the one of Frozen's folk it's named
		/// after (see VikingFrozenSpeech), or a runestone of the biome it stands in or of someone with its name (see
		/// VikingLoreSpeech)
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>The line</returns>
		private string VillageAnswer(Player player)
		{
			string name = player.GetPlayerName();
			bool friends = VikingIdentity.OldFriends(nview, player.GetPlayerID());
			return(QuestAnswer(player) ?? VillagePortalSpeech.Chat(village, name) ?? VikingFriendSpeech.Chat(friends, name) ?? VikingFrozenSpeech.Chat(name, VikingNames.Get(humanoid)) ?? VikingLoreSpeech.Chat(transform.position, name, VikingNames.Get(humanoid)) ?? VikingSpeech.Answer(player, IsNight(), VillageNames.Get(village)));
		}

		/// <summary>
		/// Picks what a viking away from any village says when a player it knows talks to it - lost, on the road,
		/// wandering, or gathering at the fire of a village a player disbanded
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>The line</returns>
		private string RoadAnswer(Player player)
		{
			ZDO zdo = nview.GetZDO();
			bool onRoad = VillageTravel.OnRoad(zdo);
			if (zdo.GetBool(VillageFounding.LeavingKey) && !onRoad)
			{
				return(VikingRoadSpeech.Disbanded(VillageNames.AtFire(VillageDeparted.LeftFire(zdo))));
			}
			return(VikingRoadSpeech.Answer(player, road == Road.Confused, onRoad, VillageTravel.DestinationName(zdo)));
		}

		/// <summary>
		/// Greets a player it doesn't know yet as they open its inventory or the box to name it, so it gets to know them
		/// - unless it's asleep, in a fight or swimming, the same as it wouldn't greet them on sight
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>True when it greeted them</returns>
		public bool GreetIfStranger(Player player)
		{
			if (player == null || VikingSpeech.Knows(humanoid, player.GetPlayerID()) || inBed || (job != null && job.Resting()) || ai.m_targetCreature != null || humanoid.IsSwimming())
			{
				return(false);
			}
			Greet(player, GreetFaceTime, false);
			return(true);
		}

		/// <summary>
		/// Greets a player - turning to them with a wave and a word, by name for a player it knows, now and then as an old
		/// friend at home (see VikingFriendSpeech), and as a stranger otherwise - and remembers them, noting the talk once
		/// it has said its piece (see VikingIdentity.Answered). A player it knows may hear its news of them in place of
		/// the word (see VikingNewsStore)
		/// </summary>
		/// <param name="player">The player</param>
		/// <param name="faceTime">How long it faces them, in seconds</param>
		/// <param name="talked">Whether the player talked to it, rather than it seeing them arrive</param>
		private void Greet(Player player, float faceTime, bool talked)
		{
			long playerId = player.GetPlayerID();
			bool known = VikingSpeech.Knows(humanoid, playerId);
			greeting = player;
			greetUntil = Time.time + faceTime;
			VikingPoses.Wave(humanoid);
			string news = (known ? VikingNewsStore.Tell(humanoid, player, NewsBiome()) : null);
			string friend = ((known && active) ? VikingFriendSpeech.Greeting(VikingIdentity.OldFriends(nview, playerId), player.GetPlayerName()) : null);
			// One away from any village greets them the way a traveler does, or a lost one asks where it is
			string line = friend ?? (active ? VikingSpeech.Greeting(player, IsNight(), known, VikingNames.Get(humanoid), VillageNames.Get(village)) : VikingRoadSpeech.Greeting(player, IsNight(), known, VikingNames.Get(humanoid), road == Road.Confused));
			VikingSpeech.Say(humanoid, news ?? line, VikingSpeech.TalkTime, VikingSpeech.TalkCull);
			VikingSpeech.Meet(humanoid, playerId);
			VikingIdentity.Answered(nview, playerId, talked);
		}

		/// <summary>
		/// Gets the index of the viking's biome, for how impressed it is by news of a boss or biome beyond it (see
		/// VikingNewsLines.ImpressedKey)
		/// </summary>
		/// <returns>The index into VikingGear's tables</returns>
		private int NewsBiome()
		{
			return((viking != null) ? viking.BiomeIndex() : 0);
		}

		/// <summary>
		/// Stops what the viking is doing and forgets it as it unloads
		/// </summary>
		private void OnDestroy()
		{
			All.Remove(this);
			StopJob();
			ReleaseWork();
			if (openChest != null)
			{
				ShowChestClosed();
			}
			if (onAnswer != null)
			{
				TravelPlanner.Forget(onAnswer);
			}
		}

		/// <summary>
		/// Keeps a viking lying in bed or sitting on a chair where it lies or sits, puts on and takes off its gear, and
		/// closes the chest it opened once it's done with it, on its owner - and gives it an ID of its own the first
		/// time (see VikingIdentity)
		/// </summary>
		private void FixedUpdate()
		{
			bool owner = (nview != null && nview.IsValid() && nview.IsOwner());
			if (owner && !hasId)
			{
				hasId = (VikingIdentity.Ensure(nview).Length > 0);
			}
			if (attachPoint != null && owner)
			{
				HoldAttached();
			}
			if (owner && body != null && VikingCore.IsLivingViking(body))
			{
				body.UpdateGearChanges(ChangeGear);
				UpdatePutOn();
				body.UpdateEncumbered();
			}
			if (openChest != null && Time.time >= closeChestAt)
			{
				ShowChestClosed();
			}
		}

		/// <summary>
		/// Shows a chest the viking is reaching into as open, for every client to see and hear, and closes it again
		/// after ChestOpenTime seconds
		/// </summary>
		/// <remarks>
		/// The chest's own way of opening counts it as used by the local player, who a dedicated server doesn't have,
		/// so it's marked in use the way that does, which shows it open and keeps players and other vikings out of it
		/// meanwhile - Usable lets this viking keep using it. A chest that's no longer free to use is left as it is, and
		/// OpenChest then finds it can't be used
		/// </remarks>
		/// <param name="chest">The chest</param>
		private void ShowChestOpen(Container chest)
		{
			if (!Usable(chest))
			{
				return;
			}
			if (openChest != null && openChest != chest)
			{
				ShowChestClosed();
			}
			chest.m_nview.ClaimOwnership();
			// What a player moved in or out of it before it was handed over is loaded first, since the game doesn't load a
			// chest marked in use
			chest.Load();
			chest.m_inUse = true;
			chest.UpdateUseVisual();
			if (openChest != chest)
			{
				VillageSignals.SendEffect(VillageSignals.ChestOpenEffect, chest.m_nview.GetZDO().m_uid);
			}
			openChest = chest;
			closeChestAt = Time.time + ChestOpenTime;
		}

		/// <summary>
		/// Shows the chest the viking opened as closed again
		/// </summary>
		private void ShowChestClosed()
		{
			Container chest = openChest;
			openChest = null;
			if (chest == null || chest.m_nview == null || !chest.m_nview.IsValid())
			{
				return;
			}
			if (chest.m_nview.IsOwner())
			{
				chest.m_inUse = false;
				chest.UpdateUseVisual();
			}
			VillageSignals.SendEffect(VillageSignals.ChestCloseEffect, chest.m_nview.GetZDO().m_uid);
		}

		/// <summary>
		/// Checks whether the viking's life is running and it isn't fighting
		/// </summary>
		/// <returns>True when its village life decides what it does</returns>
		public bool IsActive()
		{
			return(active);
		}

		/// <summary>
		/// Checks whether the viking is asleep - in bed, or resting for the night on a chair or the floor
		/// </summary>
		/// <remarks>
		/// Its job is only known on the client that owns it, which runs its life
		/// </remarks>
		/// <returns>True when it is</returns>
		public bool Asleep()
		{
			return(active && (inBed || (job != null && job.Resting())));
		}

		/// <summary>
		/// Has a viking that was leaving a village turn back, as the village is started again at its bonfire (see
		/// VillageFounding.TurnBack) - stopping where it is and telling the nearest player within GoodbyeRange so, before
		/// its village life takes it home
		/// </summary>
		private void TurnBack()
		{
			leaving = false;
			ForgetRoad();
			if (ai == null || nview == null || !nview.IsValid() || !nview.IsOwner() || humanoid.IsDead())
			{
				return;
			}
			VillageTravel.Clear(nview.GetZDO());
			ai.StopMoving();
			Player near = Player.GetClosestPlayer(transform.position, GoodbyeRange);
			if (near != null)
			{
				VikingSpeech.Say(humanoid, VikingSpeech.TurnBack(near, VillageNames.Get(Village.Of(nview.GetZDO()))), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
			}
		}

		/// <summary>
		/// Has a viking leaving a village that was disbanded gather at its fire with the rest and set out with them for
		/// another village (see UpdateGathering), travelling there like any viking with no village (see UpdateRoad). With
		/// no village to go to, or no gathering noted at its bonfire (see VillageDisband), it says goodbye to the nearest player
		/// within GoodbyeRange, facing them with a wave, then runs off the way night monsters do at dawn - away from the
		/// nearest player within 40 meters until none is left that near, when it's gone (see BaseAI.MoveAwayAndDespawn),
		/// kept at its bonfire to come back to a village started there again (see VillageDeparted) - fighting whatever it
		/// has to on the way, on its owner
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True to skip the AI's own update, false while it fights or this client doesn't own it</returns>
		private bool UpdateLeaving(float dt)
		{
			if (!nview.IsOwner() || humanoid.IsDead() || ai == null)
			{
				return(false);
			}
			ZDO zdo = nview.GetZDO();
			if (!leaving)
			{
				leaving = true;
				if (active)
				{
					Interrupt("its village disbanded");
					SetDulled(false);
				}
				active = false;
				village = null;
				ForgetRoad();
				goodbyeTo = null;
				goodbyeAt = -1f;
				runOffAt = Time.time;
				nextGatherLine = Time.time + Random.Range(0f, GatherLineMin);
			}
			if (VillageTravel.OnRoad(zdo))
			{
				return(UpdateRoad(dt));
			}
			if (ai.m_targetCreature != null)
			{
				return(false);
			}
			ZDO fire = GatherFire(zdo);
			if (VillageDisband.Gathers(fire) && UpdateGathering(dt, zdo, fire))
			{
				return(true);
			}
			if (!goodbyeStarted)
			{
				goodbyeStarted = true;
				StartGoodbye();
			}
			if (goodbyeTo != null && Time.time < runOffAt)
			{
				SayGoodbye();
				return(true);
			}
			mover = "leaving a disbanded village";
			// The game's own call takes it away when no player is that near, so it's kept at its bonfire first
			if (Player.GetClosestPlayer(transform.position, VillageDeparted.GoneReach) == null)
			{
				VillageDeparted.Depart(humanoid);
			}
			ai.MoveAwayAndDespawn(dt, true);
			return(true);
		}

		/// <summary>
		/// Runs before the viking's AI on its owner - has one leaving a disbanded village say goodbye and run off (see
		/// UpdateLeaving), stops what it's doing when a fight starts, keeps it from chasing far
		/// out of the village, brings it back when it has strayed, has the night watch raise the alarm, uses a forsaken
		/// power for a fight worth one (see VikingPowers.UpdateFight), and dulls its senses while it rests
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True to skip the AI's own update</returns>
		public bool BeforeAI(float dt)
		{
			// One leaving a village that was disbanded belongs to no village again, even one started where it stood
			if (nview != null && nview.IsValid() && nview.GetZDO().GetBool(VillageFounding.LeavingKey))
			{
				return(UpdateLeaving(dt));
			}
			if (leaving)
			{
				TurnBack();
			}
			if (!UpdateActive())
			{
				return(UpdateRoad(dt));
			}
			if (road != Road.None)
			{
				ForgetRoad();
			}
			// Getting on a quest's boat, riding it or swimming in from it, its crossing takes the update over (see
			// UpdateBoat)
			if (UpdateBoat(dt))
			{
				return(true);
			}
			if (ZombieState.IsRising(humanoid))
			{
				return(false);
			}
			UpdateRaid();
			VikingDoors.Update(humanoid);
			if (UpdateHop())
			{
				return(true);
			}
			if (UpdateFlames() || UpdatePoison() || UpdateRoots())
			{
				return(true);
			}
			UpdateRisen();
			if (UpdateDousing(dt))
			{
				return(true);
			}
			LeashTarget();
			Character target = ai.m_targetCreature;
			// Held back by its load, it can't run or fight well, so it drops what it can do without first
			if (body.IsEncumbered() && (target != null || fleeing))
			{
				ShedForFight();
			}
			if (UpdateFlee(dt, target))
			{
				return(true);
			}
			// A badly hurt viking about to strike from behind has just picked the enemy it strikes
			target = ai.m_targetCreature;
			NoteFought(target);
			if (target != null)
			{
				lastFightAt = Time.time;
				// A hunter keeps its hunt through the fight with its prey and the greylings it meets
				if (!(job is HuntJob hunt && hunt.Allows(target)))
				{
					Interrupt("a fight");
				}
				SetDulled(false);
				returning = false;
				if (UpdateChase(dt, target))
				{
					return(true);
				}
				if (IsWatch() && Time.time - lastAlarm >= AlarmInterval && village.DistanceOutside(target.transform.position) <= AlarmRange)
				{
					RaiseAlarm(target);
				}
				CallOut(target);
				body.Powers.UpdateFight();
				if (Time.time < alarmUntil)
				{
					ai.StopMoving();
					ai.LookAt(target.GetCenterPoint());
					return(true);
				}
				// A zombie still getting up can't be hurt, so the viking fights another enemy it can, or else stands
				// ready by the zombie and strikes once it's up
				if (ZombieState.IsRising(target))
				{
					Character other = NearestHurtableEnemy();
					if (other == null)
					{
						WaitForRise(dt, target);
						return(true);
					}
					ai.m_targetCreature = other;
				}
				return(UpdateKite(dt, ai.m_targetCreature));
			}
			if (UpdateMakeWay(dt) || UpdateOutOfPen(dt) || UpdateOffRoof(dt) || UpdateOutOfWater(dt))
			{
				return(true);
			}
			// A viking that just came to the village from outside the fence walks in before anything else
			if (nview.GetZDO().GetBool(ArrivingKey))
			{
				nview.GetZDO().Set(ArrivingKey, false);
				if (!village.Contains(transform.position))
				{
					Interrupt("walking in from outside the village");
					returning = true;
				}
			}
			if (!returning && village.DistanceOutside(transform.position) > StrayRange)
			{
				Interrupt("straying out of the village");
				returning = true;
			}
			if (returning)
			{
				if (village.Contains(transform.position))
				{
					returning = false;
				}
				else
				{
					Go(dt, village.RallyPoint(), 1f, true);
					if (moveFailed)
					{
						returning = false;
					}
					return(true);
				}
			}
			lastTarget = null;
			SetDulled(job != null && job.Resting());
			return(UpdateGreeting());
		}

		/// <summary>
		/// Has the viking call out an enemy it has just gone after - by name, every SpotInterval seconds at most, unless
		/// another viking called it out in the last SpotRepeat seconds or any viking called one out in the last SpotGap.
		/// A hunter doesn't call out its prey
		/// </summary>
		/// <param name="enemy">The enemy</param>
		private void CallOut(Character enemy)
		{
			if (enemy == lastTarget)
			{
				return;
			}
			lastTarget = enemy;
			if (Time.time < nextCallOut || Time.time - lastCallOut < SpotGap || enemy.IsPlayer() || Hunts(humanoid, enemy) || (job != null && job.Resting()))
			{
				return;
			}
			ZDOID id = enemy.GetZDOID();
			if (calledOut.TryGetValue(id, out float last) && Time.time - last < SpotRepeat)
			{
				return;
			}
			if (calledOut.Count > 64)
			{
				calledOut.Clear();
			}
			calledOut[id] = Time.time;
			lastCallOut = Time.time;
			nextCallOut = Time.time + SpotInterval;
			VikingSpeech.SayFirst(humanoid, VikingSpeech.Spotted(enemy), VikingSpeech.CallTime, VikingSpeech.CallCull);
		}

		/// <summary>
		/// Has the viking greet a player arriving - one within GreetRange it can see, who hadn't been near any viking on
		/// this client for GreetAwayTime seconds - so only the first viking to see them greets them (see Greet). A
		/// viking resting at night doesn't, nor one swimming, which still notes the players near it, so it doesn't greet
		/// them once it's out of the water either. Turns it to the player it greeted for GreetFaceTime seconds, or was
		/// talked to by for TalkFaceTime
		/// </summary>
		/// <returns>True while it stands facing the player it greeted, so the AI's own update is skipped</returns>
		private bool UpdateGreeting()
		{
			bool swimming = humanoid.IsSwimming();
			if (greeting != null && Time.time < greetUntil && !swimming)
			{
				// A cook doesn't let its food burn while it faces a player - it takes done food off where it stands, or
				// leaves them to go and take it off
				if (job is CookJob cook && cook.TendWhileHeld())
				{
					greeting = null;
					return(false);
				}
				mover = "greeting " + greeting.GetPlayerName();
				Face(greeting.GetEyePoint());
				return(true);
			}
			greeting = null;
			if (Time.time < nextGreetCheck || (job != null && job.Resting()))
			{
				return(false);
			}
			nextGreetCheck = Time.time + GreetCheckInterval;
			foreach (Player player in Player.GetAllPlayers())
			{
				if (player == null || player.IsDead() || BaseAI.IsEnemy(humanoid, player) || Vector3.Distance(player.transform.position, transform.position) > GreetRange)
				{
					continue;
				}
				ZDOID id = player.GetZDOID();
				bool away = (!playerLastNear.TryGetValue(id, out float last) || Time.time - last > GreetAwayTime);
				// A viking that can't see a player arriving leaves them for one that can
				if (away && !ai.CanSeeTarget(player))
				{
					continue;
				}
				playerLastNear[id] = Time.time;
				if (!away || swimming)
				{
					continue;
				}
				Greet(player, GreetFaceTime, false);
				return(true);
			}
			return(false);
		}

		/// <summary>
		/// Weighs up a fight and keeps the viking out of one it shouldn't take on where it is - away from the village's
		/// core it runs back to the village's middle for help, and at home, badly hurt, it hangs back and lets the others
		/// fight
		/// </summary>
		/// <remarks>
		/// Away from home it runs back when it's foraging or getting wood or loot and an enemy comes for it, when it's
		/// alone and attacked, when FightOutnumbered or more enemies are on it, when it's below BadlyHurtShare of its
		/// health, or when a viking with it is - though a single greyling with no other enemy near it stays and fights. A viking that ran back for being alone turns to fight as soon as another viking is with it, and every
		/// other once it's home with another viking of its village within RegroupRange - home with no one beside it, it
		/// runs on to the nearest of them, and only turns to fight alone when none of them is left. At home, a viking below BadlyHurtShare hangs back while its fellows can take the fight -
		/// unless there are more enemies than healthy vikings near, or none of them are healthy - until it's back above
		/// RecoveredShare. Hanging back, it runs from any enemy coming after it, and otherwise circles behind an enemy busy
		/// with someone else to strike it there. A viking running home, hanging back badly hurt or fighting a monster too
		/// tough for it shouts for help, and one with no enemy of its own joins a fight it sees its fellows in (see
		/// TryJoinFight)
		/// </remarks>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="target">The enemy its AI is after, or null</param>
		/// <returns>True while it runs or hangs back, so the AI's own update is skipped</returns>
		private bool UpdateFlee(float dt, Character target)
		{
			bool home = AtHome(transform.position);
			if (fleeing && fleeReason == FleeReason.None)
			{
				fleeReason = FleeReason.Forage;
			}
			// A single greyling with nothing else near is no danger, so it stays and fights that
			bool loneGreyling = LoneGreyling(target);
			if (!fleeing && job is ForageJob && !home && (target != null || ai.m_timeSinceHurt < 1f) && !loneGreyling)
			{
				fleeing = true;
				fleeReason = FleeReason.Forage;
			}
			// Out of the village getting wood or loot, it heads home rather than take on a fight out there alone
			if (!fleeing && !home && target != null && job != null && !(job is HuntJob) && job.Outing() && !loneGreyling)
			{
				Flee(FleeReason.Forage);
			}
			// A hunter runs from any enemy but its prey and greylings, and gives up the hunt below HuntRetreatShare
			if (job is HuntJob hunt)
			{
				bool foe = (target != null && !hunt.Allows(target));
				if (humanoid.GetHealthPercentage() < HuntRetreatShare || (foe && !home))
				{
					if (target != null && hunt.Allows(target))
					{
						ai.m_targetCreature = null;
					}
					Interrupt("giving up a hunt");
					if (!home)
					{
						Flee(FleeReason.Wounded);
					}
				}
			}
			bool fighting = (target != null || ai.m_timeSinceHurt < AttackedTime || fleeing);
			if (!fighting)
			{
				hangingBack = false;
				TryJoinFight();
				return(false);
			}
			if (Time.time >= nextTactics)
			{
				nextTactics = Time.time + TacticsInterval;
				WeighFight(home);
			}
			// A monster too tough for it to bring down alone has it calling the others in
			if (!fleeing && target != null && Tough(target))
			{
				ShoutForHelp(target);
			}
			if (fleeing)
			{
				if (job != null)
				{
					Interrupt("fleeing");
				}
				SetDulled(false);
				mover = "running home";
				// Running from a fight it won't take on alone, it shouts for the others, whatever it's running for
				ShoutForHelp(target);
				if (fleeTo != null && !fleeTo.IsDead())
				{
					Go(dt, fleeTo.transform.position, RegroupRange / 2f, true);
				}
				else
				{
					Go(dt, village.RallyPoint(), 2f, true);
				}
				if (moveFailed)
				{
					// With no way home it stands and fights
					moveFailed = false;
					StopFleeing();
				}
				return(fleeing);
			}
			if (!hangingBack)
			{
				flanked = null;
				return(false);
			}
			// Badly hurt, it runs from any enemy coming after it, and otherwise works its way behind one busy with
			// someone else to strike it there
			Character pursuer = Pursuer();
			if (pursuer != null)
			{
				runUntil = Time.time + RunTime;
			}
			ShoutForHelp(pursuer ?? target);
			if (Time.time >= runUntil)
			{
				Character mark = FindFlankMark();
				if (mark != null)
				{
					return(Flank(dt, mark));
				}
			}
			flanked = null;
			mover = "hanging back";
			HangBack(dt);
			return(true);
		}

		/// <summary>
		/// Finds the nearest enemy within RangedHangBackDistance coming after the viking
		/// </summary>
		/// <returns>The enemy, or null when there's none</returns>
		private Character Pursuer()
		{
			enemiesNear.Clear();
			Character.GetCharactersInRange(transform.position, RangedHangBackDistance, enemiesNear);
			Character nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (Character other in enemiesNear)
			{
				if (other == null || other == humanoid || other.IsDead() || !BaseAI.IsEnemy(humanoid, other))
				{
					continue;
				}
				BaseAI otherAI = other.GetBaseAI();
				if (otherAI == null || otherAI.GetTargetCreature() != humanoid)
				{
					continue;
				}
				float distance = Vector3.Distance(other.transform.position, transform.position);
				if (distance < nearestDistance)
				{
					nearest = other;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Finds an enemy in the village's core for a badly hurt viking to strike from behind - one within EnemyRange busy with
		/// someone else, that it can hurt and reach - keeping to the one it's already working its way behind
		/// </summary>
		/// <returns>The enemy, or null when there's none</returns>
		private Character FindFlankMark()
		{
			if (flanked != null && Flankable(flanked) && Vector3.Distance(flanked.transform.position, transform.position) <= EnemyRange)
			{
				return(flanked);
			}
			enemiesNear.Clear();
			Character.GetCharactersInRange(transform.position, EnemyRange, enemiesNear);
			Character nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (Character other in enemiesNear)
			{
				if (other == null || other == humanoid || !Flankable(other))
				{
					continue;
				}
				float distance = Vector3.Distance(other.transform.position, transform.position);
				if (distance < nearestDistance)
				{
					nearest = other;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Checks whether a badly hurt viking can strike an enemy from behind - it's alive, an enemy, in the village's core,
		/// can be hurt and reached, and is after someone other than the viking
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>True when it can</returns>
		private bool Flankable(Character enemy)
		{
			if (enemy.IsDead() || !BaseAI.IsEnemy(humanoid, enemy) || ZombieState.IsRising(enemy) || OutOfReach(enemy) || !AtHome(enemy.transform.position))
			{
				return(false);
			}
			BaseAI enemyAI = enemy.GetBaseAI();
			Character busyWith = ((enemyAI != null) ? enemyAI.GetTargetCreature() : null);
			return(busyWith != null && busyWith != humanoid);
		}

		/// <summary>
		/// Works a badly hurt viking around behind an enemy busy with someone else - circling it FlankRadius out, on the
		/// side it's already on, so it never crosses in front of it - then walks in and strikes once it's behind it
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="mark">The enemy</param>
		/// <returns>True while it's working its way around, so the AI's own update is skipped, and false once it's
		/// behind the enemy and near enough for the AI to strike</returns>
		private bool Flank(float dt, Character mark)
		{
			flanked = mark;
			ai.m_targetCreature = mark;
			ai.SetAlerted(true);
			Vector3 from = mark.transform.position;
			Vector3 back = -mark.transform.forward;
			back.y = 0f;
			back = ((back.sqrMagnitude < 0.01f) ? Vector3.back : back.normalized);
			Vector3 to = transform.position - from;
			to.y = 0f;
			float distance = to.magnitude;
			float bearing = ((distance < 0.01f) ? 0f : Vector3.SignedAngle(back, to, Vector3.up));
			if (Mathf.Abs(bearing) <= BehindAngle)
			{
				if (distance <= FlankStrike)
				{
					mover = "striking " + mark.GetHoverName() + " from behind";
					return(false);
				}
				mover = "closing in behind " + mark.GetHoverName();
				Go(dt, from + back * FlankClose, 0.5f, true);
				moveFailed = false;
				return(true);
			}
			// Each step takes it up to FlankStep further around the enemy toward its back
			float step = Mathf.Sign(bearing) * Mathf.Min(Mathf.Abs(bearing), FlankStep);
			Vector3 spot = from + Quaternion.Euler(0f, bearing - step, 0f) * back * FlankRadius;
			mover = "circling behind " + mark.GetHoverName();
			Go(dt, spot, 1f, true);
			moveFailed = false;
			return(true);
		}

		/// <summary>
		/// Has a viking shout for help every ShoutInterval seconds while there's an enemy near that fights back (see
		/// FightsBack) - badly hurt and hanging back, running home from a fight, or fighting a monster too tough for it
		/// (see Tough) - words over its head on every client, and a call that brings the vikings within DefendRange of
		/// it to help against the enemy. Prey that only runs, like a deer, is no reason to shout
		/// </summary>
		/// <param name="enemy">The enemy it's shouting about, or null for the nearest one it can hurt that fights back</param>
		private void ShoutForHelp(Character enemy)
		{
			if (Time.time < nextShout)
			{
				return;
			}
			if (enemy == null || enemy.IsDead() || !FightsBack(enemy))
			{
				enemy = NearestHurtableEnemy(true);
			}
			if (enemy == null)
			{
				return;
			}
			nextShout = Time.time + ShoutInterval;
			VillageSignals.SendShout(humanoid, enemy);
		}

		/// <summary>
		/// Decides whether the viking runs home, stops running, or hangs back, from who's around it
		/// </summary>
		/// <param name="home">Whether it's in its village's core</param>
		private void WeighFight(bool home)
		{
			int allies = 0;
			int healthyAllies = 0;
			bool woundedAlly = false;
			int enemies = 0;
			foreach (Character character in Character.GetAllCharacters())
			{
				if (character == null || character == humanoid || character.IsDead())
				{
					continue;
				}
				float distance = Vector3.Distance(character.transform.position, transform.position);
				if (VikingCore.IsLivingViking(character) && distance <= AllyRange)
				{
					allies++;
					float share = character.GetHealthPercentage();
					if (share >= HealthyShare)
					{
						healthyAllies++;
					}
					if (share < BadlyHurtShare && !AtHome(character.transform.position))
					{
						woundedAlly = true;
					}
				}
				else if (distance <= EnemyRange && BaseAI.IsEnemy(humanoid, character))
				{
					enemies++;
				}
			}
			float health = humanoid.GetHealthPercentage();
			fleeTo = null;
			if (home)
			{
				// Home with no one beside it, it keeps running to the nearest of its village's vikings rather than
				// turning to fight alone at the edge of the village
				if (fleeing)
				{
					Character fellow = NearestFellow(out float fellowDistance);
					if (fellow != null && fellowDistance > RegroupRange)
					{
						fleeTo = fellow;
						hangingBack = false;
						return;
					}
				}
				StopFleeing();
				hangingBack = HangsBack(health, hangingBack, enemies, healthyAllies);
				return;
			}
			hangingBack = false;
			if (fleeing)
			{
				if (StopsFleeing(fleeReason, allies))
				{
					StopFleeing();
				}
				return;
			}
			bool hunting = (job is HuntJob);
			bool attackedAlone = (allies == 0 && ai.m_timeSinceHurt < AttackedTime && !hunting && !LoneGreyling(ai.m_targetCreature));
			FleeReason reason = FleeFor(health, hunting, woundedAlly, enemies, attackedAlone);
			if (reason != FleeReason.None)
			{
				Flee(reason);
			}
		}

		/// <summary>
		/// Checks whether a viking at home hangs back from a fight - when it's below BadlyHurtShare of its health, or
		/// still below RecoveredShare once hanging back, and its fellows can take the fight without it, with at least
		/// one healthy ally and no more enemies than healthy allies
		/// </summary>
		/// <param name="health">Its share of its health</param>
		/// <param name="hangingBack">Whether it's hanging back already</param>
		/// <param name="enemies">How many enemies are near it</param>
		/// <param name="healthyAllies">How many of the vikings beside it have at least HealthyShare of their
		/// health</param>
		/// <returns>True when it hangs back</returns>
		internal static bool HangsBack(float health, bool hangingBack, int enemies, int healthyAllies)
		{
			bool low = (health < (hangingBack ? RecoveredShare : BadlyHurtShare));
			bool needed = (enemies > healthyAllies || healthyAllies == 0);
			return(low && !needed);
		}

		/// <summary>
		/// Checks whether a viking running home away from it stops running - only one that ran for being alone, once
		/// another viking is beside it
		/// </summary>
		/// <param name="reason">Why it's running</param>
		/// <param name="allies">How many vikings are beside it</param>
		/// <returns>True when it stops</returns>
		internal static bool StopsFleeing(FleeReason reason, int allies)
		{
			return(reason == FleeReason.Alone && allies > 0);
		}

		/// <summary>
		/// Works out why a viking away from home runs home, if it does - first for being below BadlyHurtShare of its
		/// health, or HuntRetreatShare for a hunter, who is out on its own to fight its prey and gives up sooner, then
		/// to escort a badly hurt viking, then for FightOutnumbered or more enemies, then for being attacked alone
		/// </summary>
		/// <param name="health">Its share of its health</param>
		/// <param name="hunting">Whether it's hunting</param>
		/// <param name="woundedAlly">Whether a viking beside it, away from home, is below BadlyHurtShare</param>
		/// <param name="enemies">How many enemies are near it</param>
		/// <param name="attackedAlone">Whether it was just attacked with no viking beside it, while not hunting and not
		/// fighting a lone greyling</param>
		/// <returns>Why it runs, or None when it stays</returns>
		internal static FleeReason FleeFor(float health, bool hunting, bool woundedAlly, int enemies, bool attackedAlone)
		{
			if (health < (hunting ? HuntRetreatShare : BadlyHurtShare))
			{
				return(FleeReason.Wounded);
			}
			else if (woundedAlly)
			{
				return(FleeReason.Escort);
			}
			else if (enemies >= FightOutnumbered)
			{
				return(FleeReason.Outnumbered);
			}
			else if (attackedAlone)
			{
				return(FleeReason.Alone);
			}
			return(FleeReason.None);
		}

		/// <summary>
		/// Finds the nearest other living viking of the viking's village that's loaded
		/// </summary>
		/// <param name="distance">How far it is, in meters</param>
		/// <returns>The viking, or null when there's none</returns>
		private Character NearestFellow(out float distance)
		{
			Character nearest = null;
			distance = float.MaxValue;
			foreach (Character other in village.Vikings())
			{
				if (other == null || other == humanoid || other.IsDead())
				{
					continue;
				}
				float away = Vector3.Distance(other.transform.position, transform.position);
				if (away < distance)
				{
					nearest = other;
					distance = away;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Starts the viking running home for help
		/// </summary>
		/// <param name="reason">Why</param>
		private void Flee(FleeReason reason)
		{
			fleeing = true;
			fleeReason = reason;
			ResetStuck();
		}

		/// <summary>
		/// Stops the viking running home, so it turns and fights
		/// </summary>
		private void StopFleeing()
		{
			fleeing = false;
			fleeReason = FleeReason.None;
			fleeTo = null;
		}

		/// <summary>
		/// Keeps a badly hurt viking out of the fight at home - at HangBackDistance from each enemy, or
		/// RangedHangBackDistance from one that shoots or throws, staying in the village's core, facing the fight
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		private void HangBack(float dt)
		{
			// It backs away from the enemy it's furthest inside the reach of, ranged or not
			Character enemy = null;
			float worst = 0f;
			Character nearest = null;
			float nearestDistance = float.MaxValue;
			avoidNear.Clear();
			Character.GetCharactersInRange(transform.position, RangedHangBackDistance + HangBackStep, avoidNear);
			foreach (Character other in avoidNear)
			{
				if (other == null || other == humanoid || other.IsDead() || !BaseAI.IsEnemy(humanoid, other))
				{
					continue;
				}
				float distance = Utils.DistanceXZ(other.transform.position, transform.position);
				if (distance < nearestDistance)
				{
					nearest = other;
					nearestDistance = distance;
				}
				float keep = (HasRangedAttack(other) ? RangedHangBackDistance : HangBackDistance);
				float inside = keep - distance;
				if (inside > worst)
				{
					worst = inside;
					enemy = other;
				}
			}
			if (enemy == null)
			{
				ai.StopMoving();
				if (nearest != null)
				{
					ai.LookAt(nearest.GetCenterPoint());
				}
				return;
			}
			Vector3 away = transform.position - enemy.transform.position;
			away.y = 0f;
			Vector3 spot = transform.position + ((away.sqrMagnitude < 0.01f) ? -transform.forward : away.normalized) * HangBackStep;
			if (!AtHome(spot))
			{
				// Cornered at the edge of the core, it backs off across the core instead
				Vector3 across = village.CoreMiddle - enemy.transform.position;
				across.y = 0f;
				spot = village.CoreMiddle + ((across.sqrMagnitude < 0.01f) ? Vector3.forward : across.normalized) * village.CoreRadius * 0.5f;
			}
			Go(dt, spot, 1f, true);
			moveFailed = false;
		}

		/// <summary>
		/// Checks whether an enemy can hit from afar - it has an attack that shoots or throws, like a greydwarf's stone,
		/// a shaman's bolt or a viking's bow - remembering the answer for each enemy
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>True when it can</returns>
		private static bool HasRangedAttack(Character enemy)
		{
			ZDOID id = enemy.GetZDOID();
			if (rangedCache.TryGetValue(id, out bool known))
			{
				return(known);
			}
			if (rangedCache.Count > RangedCacheMax)
			{
				rangedCache.Clear();
			}
			bool ranged = false;
			Humanoid armed = enemy as Humanoid;
			if (armed != null && armed.GetInventory() != null)
			{
				foreach (ItemDrop.ItemData item in armed.GetInventory().GetAllItems())
				{
					if (item.IsWeapon() && (Shoots(item.m_shared.m_attack) || Shoots(item.m_shared.m_secondaryAttack)))
					{
						ranged = true;
						break;
					}
				}
			}
			rangedCache[id] = ranged;
			return(ranged);
		}

		/// <summary>
		/// Checks whether an attack shoots or throws something
		/// </summary>
		/// <param name="attack">The attack, or null</param>
		/// <returns>True when it does</returns>
		private static bool Shoots(Attack attack)
		{
			return(attack != null && (attack.m_attackType == Attack.AttackType.Projectile || attack.m_attackType == Attack.AttackType.TriggerProjectile));
		}

		/// <summary>
		/// Checks whether a point is in the village's core, its vikings' home
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>True when it is</returns>
		private bool AtHome(Vector3 point)
		{
			return(Utils.DistanceXZ(point, village.CoreMiddle) <= village.CoreRadius);
		}

		/// <summary>
		/// Bends the way the viking's AI is about to move around the creatures and players in its way rather than into
		/// them - to the side of each one ahead of it, harder the nearer it is. The enemy it's after and who or what its
		/// job is about are left out, so it still walks right up to them
		/// </summary>
		/// <remarks>
		/// The navmesh only knows the ground and what's built on it, not creatures, so the AI's way runs straight
		/// through anyone standing on it. The bend goes into the AI's own move, before it turns, since the AI only moves
		/// once it faces its way - bending the move after it would turn the viking off its way, so the AI would stop to
		/// turn back, again and again. The side it passes each one on is kept for AvoidMemory seconds, so one straight
		/// ahead doesn't have it weaving from side to side
		/// </remarks>
		/// <param name="dir">The way the AI is moving, normalized</param>
		/// <returns>The way to move</returns>
		public Vector3 Steer(Vector3 dir)
		{
			// Walking straight through a doorway, any steering would only push it into the door frame, and a jump goes
			// where it was aimed
			if (humanoid == null || nview == null || !nview.IsValid() || !nview.IsOwner() || humanoid.IsDead() || attachPoint != null || humanoid.InAttack() || !humanoid.IsOnGround() || hopPhase != HopPhase.None || VikingDoors.IsCrossing(humanoid))
			{
				return(dir);
			}
			Vector3 heading = new Vector3(dir.x, 0f, dir.z);
			if (heading.sqrMagnitude < 0.01f)
			{
				return(dir);
			}
			heading.Normalize();
			Vector3 side = Vector3.Cross(Vector3.up, heading);
			Vector3 steer = Vector3.zero;
			avoidNear.Clear();
			Character.GetCharactersInRange(transform.position, AvoidRange, avoidNear);
			Character target = ((ai != null) ? ai.m_targetCreature : null);
			Character focus = ((job != null) ? job.Focus() : null);
			steeredAround = null;
			foreach (Character other in avoidNear)
			{
				if (other == null || other == humanoid || other == target || other == focus || other.IsDead())
				{
					continue;
				}
				Vector3 toOther = other.transform.position - transform.position;
				toOther.y = 0f;
				float ahead = Vector3.Dot(toOther, heading);
				float across = Vector3.Dot(toOther, side);
				float clearance = humanoid.GetRadius() + other.GetRadius() + AvoidClearance;
				if (ahead <= 0f || Mathf.Abs(across) > clearance)
				{
					continue;
				}
				// One on the other side of a wall or fence isn't in the way
				if (WallBetween(other))
				{
					continue;
				}
				// It passes on the side it picked for this one before, or else the side the other is already off to
				float pass = ((across >= 0f) ? -1f : 1f);
				if (avoidSides.TryGetValue(other, out KeyValuePair<float, float> kept) && Time.time - kept.Key < AvoidMemory)
				{
					pass = kept.Value;
				}
				avoidSides[other] = new KeyValuePair<float, float>(Time.time, pass);
				float push = (1f - Mathf.Clamp01(ahead / AvoidRange)) * (1f - Mathf.Abs(across) / clearance);
				steer += side * pass * Mathf.Max(push, AvoidMinPush) * AvoidStrength;
				steeredAround = other;
				steeredAt = Time.time;
			}
			if (avoidSides.Count > AvoidSidesMax)
			{
				avoidSides.Clear();
			}
			if (steer == Vector3.zero)
			{
				return(dir);
			}
			Vector3 bent = (heading + steer).normalized;
			return(new Vector3(bent.x, dir.y, bent.z));
		}

		/// <summary>
		/// Finds the nearest enemy within EnemyRange the viking can hurt - not a zombie still getting up
		/// </summary>
		/// <param name="fightingBack">True to leave out prey that only runs, like a deer (see FightsBack)</param>
		/// <returns>The enemy, or null when there's none</returns>
		private Character NearestHurtableEnemy(bool fightingBack = false)
		{
			enemiesNear.Clear();
			Character.GetCharactersInRange(transform.position, EnemyRange, enemiesNear);
			Character nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (Character other in enemiesNear)
			{
				if (other == null || other == humanoid || other.IsDead() || !BaseAI.IsEnemy(humanoid, other) || ZombieState.IsRising(other) || (fightingBack && !FightsBack(other)))
				{
					continue;
				}
				float distance = Vector3.Distance(other.transform.position, transform.position);
				if (distance < nearestDistance)
				{
					nearest = other;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Has the viking stand ready by a zombie still getting up, facing it with its weapon drawn - a melee fighter
		/// just out of its reach, an archer where it stands - so it strikes as soon as the zombie can be hurt
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="zombie">The zombie</param>
		private void WaitForRise(float dt, Character zombie)
		{
			mover = "waiting for a zombie to get up";
			float distance = Utils.DistanceXZ(zombie.transform.position, transform.position);
			if (viking != null && !viking.IsArcher() && distance > RiseReadyDistance + RiseReadySlack)
			{
				Go(dt, zombie.transform.position, RiseReadyDistance, true);
				moveFailed = false;
				return;
			}
			ai.StopMoving();
			ai.LookAt(zombie.GetCenterPoint());
		}

		/// <summary>
		/// Notes how the viking moved this AI update, after its AI and village life have both had their say, so its
		/// twitching - stopping and starting, or turning one way then the other, over and over - shows in its debug
		/// hover text and, with debug mode on, is written to the log with what it was doing, and a viking twitching on
		/// top of something it's stuck on climbs down (see ClimbDown) - frees a viking that has tried to walk somewhere
		/// for a long time without getting anywhere (see FreeIfTrapped), writes it getting onto a roof (see LogOnRoof),
		/// and notes which forsaken powers it's under, for other clients' vikings to tell
		/// </summary>
		public void AfterAI()
		{
			if (humanoid == null || nview == null || !nview.IsValid() || !nview.IsOwner() || humanoid.IsDead())
			{
				return;
			}
			body.Powers.Update();
			// The game's AI wanders an idle creature about, which a viking on a seat doesn't do
			if (attachPoint != null)
			{
				ai.StopMoving();
			}
			// A viking whose life didn't run here for a while - run by another client, or by none while it was out of
			// every player's simulated area - starts standing still over, rather than counting the time it wasn't run
			if (Utils.DistanceXZ(transform.position, stillAt) > StillReach || Time.time - stillCheckedAt > StillGap)
			{
				stillAt = transform.position;
				stillSince = Time.time;
				walkingStill = 0f;
				stepsStopped = 0;
			}
			else if (walkedThisUpdate)
			{
				walkingStill += Time.time - stillCheckedAt;
			}
			walkedThisUpdate = false;
			stillCheckedAt = Time.time;
			LogIfStuck();
			FreeIfTrapped();
			LogOnRoof(mover);
			Vector3 move = humanoid.GetMoveDir();
			MoveSample sample = new MoveSample();
			sample.Time = Time.time;
			sample.Moving = (new Vector3(move.x, 0f, move.z).sqrMagnitude > 0.01f);
			sample.Yaw = transform.eulerAngles.y;
			sample.Mover = mover;
			moveSamples.Enqueue(sample);
			while (moveSamples.Count > 0 && Time.time - moveSamples.Peek().Time > TwitchWindow)
			{
				moveSamples.Dequeue();
			}
			mover = "game AI";
			CountTwitches(out int starts, out int reversals);
			if (starts < TwitchStarts && reversals < TwitchReversals)
			{
				return;
			}
			if (Player.m_debugMode && Time.time >= nextTwitchLog)
			{
				nextTwitchLog = Time.time + TwitchLogInterval;
				Plugin.Log.LogInfo(TwitchReport(starts, reversals));
			}
			if (ClimbDown())
			{
				moveSamples.Clear();
			}
		}

		/// <summary>
		/// How far above the next point of its path a twitching viking has to stand to count as stuck on top of
		/// something, and how near that point has to be, seen from above, for it to climb down to it, in meters
		/// </summary>
		private const float StrandHeight = 1.5f;
		private const float StrandReach = 3f;

		/// <summary>
		/// Climbs a twitching viking down from on top of something it's stuck on - a roof, or furniture it got onto - to
		/// the next point of its path, when that's well below it and close by, rather than leaving it turning back and
		/// forth at the edge, unable to step down to the way it's going
		/// </summary>
		/// <returns>True when it climbed down</returns>
		private bool ClimbDown()
		{
			if (ai == null || ai.m_path == null || ai.m_path.Count == 0 || attachPoint != null || !humanoid.IsOnGround())
			{
				return(false);
			}
			Vector3 next = ai.m_path[0];
			if (!Stranded(transform.position, next))
			{
				return(false);
			}
			Vector3 down = next + Vector3.up * 0.1f;
			TestLog.Count("Troubles", "climbed down from on top of something");
			if (TestLog.On || Player.m_debugMode)
			{
				Plugin.Log.LogInfo("Climb: " + TestLog.Name(humanoid, "F1") + " was stuck " + (transform.position.y - next.y).ToString("F1") + "m above its path, so it climbs down to " + down.ToString("F1"));
			}
			transform.position = down;
			humanoid.m_body.position = down;
			humanoid.m_body.linearVelocity = Vector3.zero;
			return(true);
		}

		/// <summary>
		/// Checks whether a viking stands stuck above the next point of its path - at least StrandHeight above it, and
		/// within StrandReach of it seen from above
		/// </summary>
		/// <param name="at">Where it stands</param>
		/// <param name="next">The next point of its path</param>
		/// <returns>True when it does</returns>
		internal static bool Stranded(Vector3 at, Vector3 next)
		{
			float dx = at.x - next.x;
			float dz = at.z - next.z;
			return(at.y - next.y >= StrandHeight && dx * dx + dz * dz <= StrandReach * StrandReach);
		}

		/// <summary>
		/// Counts the times in the last TwitchWindow seconds the viking started moving after standing still, and turned
		/// back the other way after turning one way
		/// </summary>
		/// <param name="starts">How many times it started moving</param>
		/// <param name="reversals">How many times it turned back</param>
		private void CountTwitches(out int starts, out int reversals)
		{
			starts = 0;
			reversals = 0;
			bool first = true;
			bool wasMoving = false;
			float lastYaw = 0f;
			float lastTurn = 0f;
			foreach (MoveSample sample in moveSamples)
			{
				if (!first)
				{
					if (sample.Moving && !wasMoving)
					{
						starts++;
					}
					float turn = Mathf.DeltaAngle(lastYaw, sample.Yaw);
					if (Mathf.Abs(turn) >= TwitchTurn)
					{
						if (lastTurn != 0f && Mathf.Sign(turn) != Mathf.Sign(lastTurn))
						{
							reversals++;
						}
						lastTurn = turn;
					}
				}
				first = false;
				wasMoving = sample.Moving;
				lastYaw = sample.Yaw;
			}
		}

		/// <summary>
		/// Describes the viking's twitching and what it was doing, for the log
		/// </summary>
		/// <param name="starts">How many times it started moving in the last TwitchWindow seconds</param>
		/// <param name="reversals">How many times it turned back</param>
		/// <returns>The report</returns>
		private string TwitchReport(int starts, int reversals)
		{
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			text.Append("Twitching: " + TestLog.Name(humanoid, "F1") + " started moving " + starts + " times and turned back " + reversals + " times in " + TwitchWindow.ToString("F0") + "s\n");
			text.Append("  Job: " + ((job != null) ? job.Describe() : "none") + "\n");
			Character target = ((ai != null) ? ai.m_targetCreature : null);
			text.Append("  Target: " + ((target != null) ? TestLog.Label(target) + " at " + Vector3.Distance(target.transform.position, transform.position).ToString("F1") + "m" : "none") + ", alerted " + (ai != null && ai.IsAlerted()) + "\n");
			text.Append("  Path: " + PathText() + "\n");
			text.Append("  Stuck: " + ((stuckSince > 0f) ? (Time.time - stuckSince).ToString("F1") + "s without progress" : "no") + ", hops " + hops + ", door " + (VikingDoors.IsWaiting(humanoid) ? "waiting" : (VikingDoors.IsCrossing(humanoid) ? "crossing" : "none")) + "\n");
			text.Append("  Steering around: " + ((steeredAround != null && Time.time - steeredAt < SteeredShowTime) ? TestLog.Label(steeredAround) + " at " + Vector3.Distance(steeredAround.transform.position, transform.position).ToString("F1") + "m" : "no one") + "\n");
			Dictionary<string, int> movers = new Dictionary<string, int>();
			foreach (MoveSample sample in moveSamples)
			{
				movers[sample.Mover] = (movers.TryGetValue(sample.Mover, out int count) ? count : 0) + 1;
			}
			List<string> moverText = new List<string>();
			foreach (KeyValuePair<string, int> entry in movers)
			{
				moverText.Add(entry.Key + " x" + entry.Value);
			}
			text.Append("  Moved by: " + string.Join(", ", moverText) + "\n");
			text.Append("  Trace (M moving, . still, then facing): " + MoveTrace());
			return(text.ToString());
		}

		/// <summary>
		/// Describes the path the viking's AI is following, for the log
		/// </summary>
		/// <returns>How many points it has left and where the next is, or "none"</returns>
		private string PathText()
		{
			if (ai != null && ai.m_path != null && ai.m_path.Count > 0)
			{
				return(ai.m_path.Count + " points left, next " + Utils.DistanceXZ(ai.m_path[0], transform.position).ToString("F1") + "m away at " + ai.m_path[0].ToString("F1"));
			}
			return("none");
		}

		/// <summary>
		/// Lists the viking's moves over the last TwitchWindow seconds, for the log - M when it was moving or . when it
		/// was still, then the way it faced
		/// </summary>
		/// <returns>The moves, oldest first</returns>
		private string MoveTrace()
		{
			System.Text.StringBuilder trace = new System.Text.StringBuilder();
			foreach (MoveSample sample in moveSamples)
			{
				trace.Append((sample.Moving ? "M" : ".") + Mathf.RoundToInt(sample.Yaw) + " ");
			}
			return(trace.ToString().TrimEnd());
		}

		/// <summary>
		/// Notes that the viking's move was stopped short of a ledge or dangerous ground (see VikingDangerStepPatch), for
		/// the log when it's freed
		/// </summary>
		public void NoteStepStopped()
		{
			stepsStopped++;
		}

		/// <summary>
		/// Checks whether a wall, fence or other solid thing stands between the viking and a creature, at knee height
		/// </summary>
		/// <param name="other">The creature</param>
		/// <returns>True when one does</returns>
		private bool WallBetween(Character other)
		{
			if (hopMask == 0)
			{
				hopMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece");
			}
			Vector3 knee = Vector3.up * 0.5f;
			return(Physics.Linecast(transform.position + knee, other.transform.position + knee, hopMask, QueryTriggerInteraction.Ignore));
		}

		/// <summary>
		/// Checks whether the viking hurries through the rain, running rather than walking - while it's raining on it,
		/// out from under a roof, looked at again every RainRoofCheck seconds since it casts for the roof
		/// </summary>
		/// <returns>True when it does</returns>
		private bool RunsInRain()
		{
			// The Swamp's own drizzle, which hardly lets up, it walks through like any other day
			if (!VikingWeather.KeepsIndoors(transform.position))
			{
				return(false);
			}
			if (Time.time >= nextRainRoofCheck)
			{
				nextRainRoofCheck = Time.time + RainRoofCheck;
				roofless = !Cover.IsUnderRoof(humanoid.GetCenterPoint());
			}
			return(roofless);
		}

		/// <summary>
		/// Checks whether the viking is out of its village's core, or on the road to a village with none of its own yet,
		/// where it runs everywhere rather than walks, so it's out in the open for as little time as it can be
		/// </summary>
		/// <returns>True when it's away from home</returns>
		public bool AwayFromHome()
		{
			if (road == Road.Travelling || road == Road.Waiting)
			{
				return(true);
			}
			return(active && village != null && !AtHome(transform.position));
		}

		/// <summary>
		/// Checks whether an enemy of the viking's is within a range of a point, leaving out those a check passes
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="range">The range, in meters</param>
		/// <param name="ignored">True for an enemy that doesn't count</param>
		/// <returns>True when one is</returns>
		private bool EnemyNear(Vector3 point, float range, System.Predicate<Character> ignored)
		{
			List<Character> near = new List<Character>();
			Character.GetCharactersInRange(point, range, near);
			return(near.Exists((Character other) => other != humanoid && !other.IsDead() && BaseAI.IsEnemy(humanoid, other) && !ignored(other)));
		}

		/// <summary>
		/// Checks whether an enemy of the viking's is within a range of a point
		/// </summary>
		/// <param name="point">The point</param>
		/// <param name="range">The range, in meters</param>
		/// <returns>True when one is</returns>
		private bool EnemyNear(Vector3 point, float range)
		{
			List<Character> near = new List<Character>();
			Character.GetCharactersInRange(point, range, near);
			return(near.Exists((Character other) => other != humanoid && !other.IsDead() && BaseAI.IsEnemy(humanoid, other)));
		}

		/// <summary>
		/// Checks whether there's danger within ForageSafety of a point for a viking out of the village - any enemy of
		/// its, except a single greyling with no other enemy near, which it can take on alone
		/// </summary>
		/// <param name="point">The point</param>
		/// <returns>True when there is</returns>
		private bool DangerNear(Vector3 point)
		{
			List<Character> near = new List<Character>();
			Character.GetCharactersInRange(point, ForageSafety, near);
			int enemies = 0;
			bool greyling = false;
			foreach (Character other in near)
			{
				if (other == humanoid || other.IsDead() || !BaseAI.IsEnemy(humanoid, other))
				{
					continue;
				}
				enemies++;
				greyling = IsHuntedMonster(other);
			}
			return(enemies > 1 || (enemies == 1 && !greyling));
		}

		/// <summary>
		/// Checks whether an enemy is a single greyling with no other enemy near the viking, which it stays and fights
		/// rather than running home from
		/// </summary>
		/// <param name="enemy">The enemy, or null</param>
		/// <returns>True when it is</returns>
		private bool LoneGreyling(Character enemy)
		{
			return(IsHuntedMonster(enemy) && !DangerNear(transform.position));
		}

		/// <summary>
		/// Forgets an enemy the viking has picked that's too far past the village's edge to go after - beyond its
		/// leash, beyond GrudgeRange for one that hurt it or someone it's defending, or for a hunter's prey, beyond where
		/// it hunts and chases - one out in water too deep to fight it in (see OutOfReach), and the village's livestock
		/// unless it's the animal the viking is slaughtering
		/// </summary>
		public void LeashTarget()
		{
			Character target = ai.m_targetCreature;
			if (target == null || village == null)
			{
				return;
			}
			if (FarmSpawns.IsLivestock(target) && !Hunts(humanoid, target))
			{
				ai.m_targetCreature = null;
				return;
			}
			float leash = (HoldsGrudge(target) ? GrudgeRange : LeashRange);
			if (job is HuntJob hunt && hunt.Allows(target))
			{
				leash = Mathf.Max(leash, HuntReach + HuntChase);
			}
			if (village.DistanceOutside(target.transform.position) > leash || OutOfReach(target))
			{
				ai.m_targetCreature = null;
			}
		}

		/// <summary>
		/// Checks whether an enemy is out in water too deep for the viking to fight it hand to hand - swimming, or below
		/// the water level where the ground is more than WadeDepth under it - so it stays on dry land rather than swim
		/// after it. An archer still shoots at it from the shore
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>True when it is</returns>
		public bool OutOfReach(Character enemy)
		{
			return(!HasBow(humanoid) && InDeepWater(enemy));
		}

		/// <summary>
		/// Checks whether an enemy is out in water too deep to fight it in hand to hand - swimming, or below the water
		/// level where the ground is more than WadeDepth under it
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>True when it is</returns>
		private static bool InDeepWater(Character enemy)
		{
			if (enemy == null || ZoneSystem.instance == null)
			{
				return(false);
			}
			if (enemy.IsSwimming())
			{
				return(true);
			}
			float water = ZoneSystem.instance.m_waterLevel;
			Vector3 position = enemy.transform.position;
			return(position.y < water && ZoneSystem.instance.GetGroundHeight(position) < water - WadeDepth);
		}

		/// <summary>
		/// How many of its fellows already fighting an enemy hand to hand make it too crowded for another to get at it,
		/// and how close to the enemy, past its size, they count as at it, in meters
		/// </summary>
		private const int CrowdCount = 3;
		private const float CrowdReach = 2.5f;

		/// <summary>
		/// Checks whether the viking would rather shoot an enemy than fight it hand to hand - one it can't reach, out in
		/// deep water, flying or with no way to it, one too many of its fellows are already at, or a tough one it keeps
		/// away from (see UpdateKite)
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>True when it would</returns>
		public bool WantsBow(Character enemy)
		{
			if (enemy == null)
			{
				return(false);
			}
			if (InDeepWater(enemy) || enemy.IsFlying() || Kites(enemy))
			{
				return(true);
			}
			return(Crowded(enemy) || (ai != null && !ai.HavePath(enemy.transform.position)));
		}

		/// <summary>
		/// Checks whether CrowdCount of the viking's fellows are already at an enemy, while it isn't one of them
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>True when they are</returns>
		private bool Crowded(Character enemy)
		{
			float reach = enemy.GetRadius() + CrowdReach;
			if (Utils.DistanceXZ(enemy.transform.position, transform.position) <= reach)
			{
				return(false);
			}
			List<Character> near = new List<Character>();
			Character.GetCharactersInRange(enemy.transform.position, reach, near);
			int at = 0;
			foreach (Character other in near)
			{
				if (other != humanoid && !other.IsDead() && VikingCore.IsLivingViking(other) && Utils.DistanceXZ(other.transform.position, enemy.transform.position) <= reach)
				{
					at++;
				}
			}
			return(at >= CrowdCount);
		}

		/// <summary>
		/// How far past a tough monster's reach the viking it's after lures it - backing away once the monster is within
		/// LureNear, and standing its ground again once it's past LureFar - in meters
		/// </summary>
		private const float LureNear = 1.5f;
		private const float LureFar = 4f;

		/// <summary>
		/// How far past a tough monster's reach the rest keep while they shoot it, how much farther from it than the
		/// viking luring it, so it stays after the lure, which it takes as the nearest, and the farthest out that keeps
		/// them, so they're still in bowshot, in meters
		/// </summary>
		private const float SupportKeep = 8f;
		private const float SupportBehindLure = 3f;
		private const float SupportKeepMax = 22f;

		/// <summary>
		/// How far each step a viking keeping away from a tough monster takes, in meters, how far it turns off straight
		/// away when it's cornered, in degrees, and how far from a tough monster a fellow who can shoot counts as there
		/// to shoot it, in meters
		/// </summary>
		private const float KiteStep = 4f;
		private const float KiteTurn = 70f;
		private const float ShooterRange = 30f;

		/// <summary>
		/// How often a monster the viking fights is weighed up again for being tough, in seconds
		/// </summary>
		private const float ToughCheckInterval = 2f;

		/// <summary>
		/// The monster the viking last weighed up, whether it's tough, and when that was, in Time.time seconds
		/// </summary>
		private Character toughChecked;
		private bool toughResult;
		private float toughCheckedAt;

		/// <summary>
		/// Whether the viking is backing away from a tough monster, and how far off straight away it backs, in degrees
		/// </summary>
		private bool kiteBacking;
		private float kiteTurn;

		/// <summary>
		/// Whether the viking keeps clear of the tough monster it last weighed up while its fellows gather against it
		/// (see KeepsClear)
		/// </summary>
		private bool gathering;

		/// <summary>
		/// How far past a tough monster's reach a fellow counts as gathered against it, in meters - as far out as the
		/// vikings keeping clear of it stand, and a little more
		/// </summary>
		private const float GatherReach = SupportKeepMax + LureFar;

		/// <summary>
		/// Checks whether the viking keeps away from a monster rather than fight it hand to hand - it's tough for the
		/// viking (see VikingCore.IsTough), and it isn't charging it with its fellows (see KeepsClear). A hunting party's
		/// prey is kited only by the one it's after (see HuntParty)
		/// </summary>
		/// <param name="monster">The monster</param>
		/// <returns>True when it does</returns>
		private bool Kites(Character monster)
		{
			if (monster == null || viking == null || MonsterThreat.IsRoot(monster) || monster.IsDead())
			{
				return(false);
			}
			// A hunting party's prey is kited by whichever of them it's after, while the rest fight it (see HuntParty)
			if (job is HuntJob hunt && hunt.PartyFights(monster))
			{
				BaseAI hunted = monster.GetBaseAI();
				return(hunted != null && hunted.GetTargetCreature() == humanoid);
			}
			if (monster != toughChecked || Time.time - toughCheckedAt >= ToughCheckInterval)
			{
				toughChecked = monster;
				toughCheckedAt = Time.time;
				toughResult = (Tough(monster) && KeepsClear(monster));
			}
			return(toughResult);
		}

		/// <summary>
		/// Weighs up who's with the viking against a tough monster - how many of its village's vikings it takes to fight
		/// it hand to hand together (see VikingCore.FightersNeeded), how many have gathered within GatherReach past its
		/// reach, and how many more are within DefendRange of the viking, where its shouts for help call them in - and
		/// checks whether it keeps clear of the monster for now (see KeepsClear(int, int, int, bool)). A fellow below
		/// BadlyHurtShare of its health hangs back, so it isn't counted, and a viking with no village counts only itself
		/// </summary>
		/// <param name="monster">The tough monster</param>
		/// <returns>True when it keeps clear</returns>
		private bool KeepsClear(Character monster)
		{
			int needed = viking.FightersNeeded(monster);
			float reach = MonsterThreat.Reach(monster) + GatherReach;
			int gathered = 0;
			int called = 0;
			List<Character> fellows = ((village != null) ? village.Vikings() : new List<Character> { humanoid });
			foreach (Character fellow in fellows)
			{
				if (fellow != humanoid && fellow.GetHealthPercentage() < BadlyHurtShare)
				{
					continue;
				}
				bool near = (Utils.DistanceXZ(fellow.transform.position, monster.transform.position) <= reach);
				if (near)
				{
					gathered++;
				}
				if (near || Vector3.Distance(fellow.transform.position, transform.position) <= DefendRange)
				{
					called++;
				}
			}
			gathering = (gathered < needed && called >= needed);
			bool shooters = (gathered < needed && !gathering && ShootersNear(monster));
			return(KeepsClear(needed, gathered, called, shooters));
		}

		/// <summary>
		/// Checks whether a viking keeps clear of a tough monster for now - not once enough of its fellows have gathered
		/// against it, when they all charge it together, but while enough are within call to gather, and otherwise
		/// while someone near can shoot it, so it can be brought down while it's kept busy. With too few to take it on
		/// and no one to shoot it, the viking fights it anyway
		/// </summary>
		/// <param name="needed">How many vikings it takes to fight the monster hand to hand together</param>
		/// <param name="gathered">How many of them have gathered against it, the viking among them</param>
		/// <param name="called">How many are gathered or within call, the viking among them</param>
		/// <param name="shooters">Whether the viking or a fellow near the monster can shoot it</param>
		/// <returns>True when it keeps clear</returns>
		internal static bool KeepsClear(int needed, int gathered, int called, bool shooters)
		{
			if (gathered >= needed)
			{
				return(false);
			}
			return(called >= needed || shooters);
		}

		/// <summary>
		/// Checks whether a monster is too tough for the viking to bring down alone (see VikingCore.IsTough), weighed
		/// again every ToughCheckInterval seconds
		/// </summary>
		/// <param name="monster">The monster</param>
		/// <returns>True when it is</returns>
		private bool Tough(Character monster)
		{
			if (monster == null || viking == null || MonsterThreat.IsRoot(monster) || monster.IsDead())
			{
				return(false);
			}
			if (monster != hardChecked || Time.time - hardCheckedAt >= ToughCheckInterval)
			{
				hardChecked = monster;
				hardCheckedAt = Time.time;
				hardResult = viking.IsTough(monster);
			}
			return(hardResult);
		}

		/// <summary>
		/// The monster last weighed for how tough it is for the viking, whether it was, and when
		/// </summary>
		private Character hardChecked;
		private bool hardResult;
		private float hardCheckedAt;

		/// <summary>
		/// Joins a fight the viking sees one of its fellows in - while it has no enemy of its own, isn't in bed, resting
		/// or badly hurt - every JoinCheckInterval seconds: the nearest enemy within DefendRange of it that a living viking
		/// within JoinRange it can see is fighting, the way it comes to a call for help (see OnDistress). Through a raid
		/// it's ready for, it's the nearest enemy any of its village's vikings is fighting where its fight reaches it (see
		/// Hears), seen or not. A fellow's prey isn't its enemy, so it leaves a hunt to the hunter
		/// </summary>
		/// <remarks>
		/// Only the vikings this client runs are known to be fighting, by the enemy their AI is after - those near it
		/// mostly are, since the clients near a place run what's there
		/// </remarks>
		private void TryJoinFight()
		{
			if (Time.time < nextJoinCheck || ai == null || humanoid == null)
			{
				return;
			}
			nextJoinCheck = Time.time + JoinCheckInterval;
			if (inBed || (job != null && job.Resting()) || Wounded())
			{
				return;
			}
			// Through a raid it joins any fellow fighting in the village, seen or not
			bool raid = RaidReady();
			List<Character> near = new List<Character>();
			if (raid)
			{
				near = village.Vikings();
			}
			else
			{
				Character.GetCharactersInRange(transform.position, JoinRange, near);
			}
			Character joined = null;
			float nearest = float.MaxValue;
			foreach (Character other in near)
			{
				if (other == humanoid || other.IsDead() || !VikingCore.IsLivingViking(other) || !other.m_nview.IsOwner())
				{
					continue;
				}
				Character enemy = ((other.GetBaseAI() is MonsterAI theirs) ? theirs.GetTargetCreature() : null);
				if (enemy == null || enemy.IsDead() || !BaseAI.IsEnemy(humanoid, enemy))
				{
					continue;
				}
				float distance = Vector3.Distance(enemy.transform.position, transform.position);
				bool reaches = (raid ? Hears(other.transform.position) : (distance <= DefendRange && ai.CanSeeTarget(other)));
				if (reaches && distance < nearest)
				{
					joined = enemy;
					nearest = distance;
				}
			}
			if (joined == null)
			{
				return;
			}
			Interrupt("joining a fight");
			grudge = joined;
			grudgeUntil = Time.time + GrudgeTime;
			Engage(joined);
		}

		/// <summary>
		/// How far a viking sees its fellows fighting from to join them, in meters, and how often it looks, in seconds
		/// </summary>
		private const float JoinRange = 20f;
		private const float JoinCheckInterval = 1f;

		/// <summary>
		/// When the viking next looks for a fight its fellows are in
		/// </summary>
		private float nextJoinCheck;

		/// <summary>
		/// Checks whether the viking or one of its fellows within ShooterRange of a monster can shoot it
		/// </summary>
		/// <param name="monster">The monster</param>
		/// <returns>True when one can</returns>
		private bool ShootersNear(Character monster)
		{
			if (HasBow(humanoid))
			{
				return(true);
			}
			List<Character> near = new List<Character>();
			Character.GetCharactersInRange(monster.transform.position, ShooterRange, near);
			return(near.Exists((Character other) => other != humanoid && !other.IsDead() && VikingCore.IsLivingViking(other) && HasBow(other)));
		}

		/// <summary>
		/// Kites a tough monster (see Kites) - the viking it's after lures it, staying just out of its reach, past
		/// LureNear and backing off whenever it gets closer, while the rest keep SupportKeep past its reach and farther
		/// from it than the lure, so it stays after the lure, and shoot it. One who can't shoot holds its ground out of
		/// reach rather than walk in
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="target">The viking's target</param>
		/// <returns>True while it's moving or holding off, so the AI's own update is skipped</returns>
		private bool UpdateKite(float dt, Character target)
		{
			if (!Kites(target))
			{
				kiteBacking = false;
				kiteTurn = 0f;
				return(false);
			}
			BaseAI enemyAI = target.GetBaseAI();
			Character chasing = ((enemyAI != null) ? enemyAI.GetTargetCreature() : null);
			bool lure = (chasing == humanoid);
			float reach = MonsterThreat.Reach(target);
			float near = reach + LureNear;
			if (!lure)
			{
				float behindLure = ((chasing != null) ? Utils.DistanceXZ(chasing.transform.position, target.transform.position) + SupportBehindLure : 0f);
				near = Mathf.Max(reach + SupportKeep, Mathf.Min(behindLure, SupportKeepMax));
			}
			float far = near + (LureFar - LureNear);
			float distance = Utils.DistanceXZ(target.transform.position, transform.position);
			mover = (lure ? "luring " : (gathering ? "gathering against " : "keeping clear of ")) + target.GetHoverName();
			if (distance < near || (kiteBacking && distance < far))
			{
				kiteBacking = true;
				BackAway(dt, target);
				return(true);
			}
			kiteBacking = false;
			if (!HasBow(humanoid))
			{
				ai.StopMoving();
				ai.LookAt(target.GetCenterPoint());
				return(true);
			}
			return(false);
		}

		/// <summary>
		/// Backs the viking away from a monster at a run, turning off to the side when it's cornered and to the other
		/// side the next time
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="monster">The monster</param>
		private void BackAway(float dt, Character monster)
		{
			Vector3 away = transform.position - monster.transform.position;
			away.y = 0f;
			away = ((away.sqrMagnitude < 0.01f) ? -transform.forward : away.normalized);
			Vector3 heading = Quaternion.Euler(0f, kiteTurn, 0f) * away;
			Go(dt, transform.position + heading * KiteStep, 1f, true);
			if (moveFailed)
			{
				moveFailed = false;
				kiteTurn = ((kiteTurn > 0f) ? -KiteTurn : KiteTurn);
			}
		}

		/// <summary>
		/// Checks whether a viking on this client is hunting a creature, which its AI treats as an enemy
		/// </summary>
		/// <param name="hunter">The viking</param>
		/// <param name="prey">The creature</param>
		/// <returns>True when it is</returns>
		public static bool Hunts(Character hunter, Character prey)
		{
			return(hunter != null && prey != null && hunts.TryGetValue(hunter, out Character hunted) && hunted == prey);
		}

		/// <summary>
		/// Checks whether a viking is an archer, fighting with a bow or crossbow (see VikingCore.IsArcher)
		/// </summary>
		/// <param name="viking">The viking</param>
		/// <returns>True when it is</returns>
		public static bool HasBow(Character viking)
		{
			VikingCore archer = ((viking != null) ? viking.GetComponent<VikingCore>() : null);
			return(archer != null && archer.IsArcher());
		}

		/// <summary>
		/// Checks whether the viking may hunt - an archer always, or anyone when none of its village's vikings is one
		/// </summary>
		/// <param name="archer">Set to whether it's an archer, which also hunts the prey only archers hunt (see
		/// HuntPrey)</param>
		/// <returns>True when it may</returns>
		private bool MayHunt(out bool archer)
		{
			archer = HasBow(humanoid);
			return(archer || !village.Vikings().Exists((Character other) => HasBow(other)));
		}

		/// <summary>
		/// Checks whether a creature is prey the viking hunts - untamed prey of a biome within its reach (see
		/// HuntPrey.Hunts), living or risen as a zombie, that's alive and not one of the village's livestock
		/// </summary>
		/// <param name="creature">The creature</param>
		/// <param name="archer">Whether the viking is an archer</param>
		/// <returns>True when it is</returns>
		private bool IsPrey(Character creature, bool archer)
		{
			if (creature == null || creature.IsDead() || creature.IsTamed() || FarmSpawns.IsLivestock(creature))
			{
				return(false);
			}
			int biome = ((viking != null) ? viking.BiomeIndex() : 0);
			return(HuntPrey.Hunts(Utils.GetPrefabName(creature.gameObject), archer, biome));
		}

		/// <summary>
		/// Checks whether the viking is aiming its bow at a bird or at prey it's shooting from afar, so it keeps its bow
		/// in its hands
		/// </summary>
		/// <returns>True while it is</returns>
		public bool AimingBow()
		{
			return(aimingBow);
		}

		/// <summary>
		/// Checks whether the viking hunts wild prey as a party - from the Plains on, by the biome its gear comes from,
		/// where the prey fights back hard enough that one viking alone shouldn't take it on (see HuntParty)
		/// </summary>
		/// <returns>True when it does</returns>
		private bool GroupHunts()
		{
			return(viking != null && viking.BiomeIndex() >= VikingGear.IndexOf(Heightmap.Biome.Plains));
		}

		/// <summary>
		/// Lists the other vikings of the village this client owns that are free to join a hunting party - strolling
		/// or standing about, with nothing to fight and not hurt, within PartyRecruitRange - nearest first
		/// </summary>
		/// <returns>The vikings</returns>
		private List<VillageLife> FreeHunters()
		{
			List<VillageLife> free = new List<VillageLife>();
			foreach (VillageLife other in All)
			{
				if (other == this || !other.active || other.village != village || other.ai == null || other.ai.m_targetCreature != null || other.inBed || other.Wounded())
				{
					continue;
				}
				if (other.job != null && !(other.job is StrollJob) && !(other.job is IdleJob))
				{
					continue;
				}
				if (Vector3.Distance(other.transform.position, transform.position) <= PartyRecruitRange)
				{
					free.Add(other);
				}
			}
			free.Sort((VillageLife a, VillageLife b) => Vector3.Distance(a.transform.position, transform.position).CompareTo(Vector3.Distance(b.transform.position, transform.position)));
			return(free);
		}

		/// <summary>
		/// Checks whether the viking could lead a hunting party out now - there's prey for a party (see FindPrey) and
		/// enough free vikings to go with it (see FreeHunters)
		/// </summary>
		/// <param name="wanted">The prefab name of what the prey has to drop, or null for any prey</param>
		/// <returns>True when it could</returns>
		private bool CanLeadParty(string wanted)
		{
			return(GroupHunts() && FreeHunters().Count >= PartyMin - 1 && FindPrey(false, wanted, false, true) != null);
		}

		/// <summary>
		/// Leads a hunting party out - picking the prey and bringing the nearest free vikings, up to PartyMax with the
		/// leader, each of them taking up the party's hunt (see HuntParty)
		/// </summary>
		/// <param name="wanted">The prefab name of what the prey has to drop, or null for any prey</param>
		/// <returns>The leader's job, or null when there's no prey or too few to go</returns>
		private Job LeadParty(string wanted)
		{
			List<VillageLife> free = FreeHunters();
			Character target = ((free.Count >= PartyMin - 1) ? FindPrey(false, wanted, false, true) : null);
			if (target == null)
			{
				return(null);
			}
			HuntParty party = new HuntParty(this, target, Time.time + PartyGatherTime);
			for (int i = 0; i < free.Count && party.Members.Count < PartyMax; i++)
			{
				VillageLife member = free[i];
				party.Members.Add(member);
				member.StopJob();
				member.job = new HuntJob(member, HasBow(member.humanoid), wanted, false, party);
				member.job.Begin();
			}
			return(new HuntJob(this, HasBow(humanoid), wanted, false, party));
		}

		/// <summary>
		/// Checks whether the viking is out hunting or slaughtering livestock
		/// </summary>
		/// <returns>True when it is</returns>
		public bool Hunting()
		{
			return(job is HuntJob);
		}

		/// <summary>
		/// Checks whether a creature is prey that isn't dangerous (see HuntPrey.IsHarmless), which a hunter doesn't
		/// mind near the prey it's after or near itself - while a dangerous one, like a lox, means a fight with one
		/// could become a fight with all of them
		/// </summary>
		/// <param name="creature">The creature</param>
		/// <returns>True when it is</returns>
		private static bool IsHarmlessPrey(Character creature)
		{
			return(creature != null && HuntPrey.IsHarmless(Utils.GetPrefabName(creature.gameObject)));
		}

		/// <summary>
		/// Checks whether a hunter doesn't mind a creature near the prey it's after or near itself (see
		/// HuntPrey.Ignores) - prey that isn't dangerous, or another of its prey's kind when that kind doesn't herd
		/// </summary>
		/// <param name="prey">The prey, or null with none picked yet</param>
		/// <param name="creature">The other creature</param>
		/// <returns>True when it doesn't mind it</returns>
		private static bool IgnoredByHunter(Character prey, Character creature)
		{
			if (creature == null)
			{
				return(false);
			}
			string preyName = ((prey != null) ? Utils.GetPrefabName(prey.gameObject) : null);
			return(HuntPrey.Ignores(preyName, Utils.GetPrefabName(creature.gameObject)));
		}

		/// <summary>
		/// Checks whether a creature is a monster a hunter fights rather than runs from - a greyling
		/// </summary>
		/// <param name="creature">The creature</param>
		/// <returns>True when it is</returns>
		private static bool IsHuntedMonster(Character creature)
		{
			return(creature != null && Utils.GetPrefabName(creature.gameObject) == HuntPrey.FoughtOnTheWay);
		}

		/// <summary>
		/// Finds the nearest prey within HuntReach of the village that no other viking is after and with no enemy near it
		/// but what a hunter doesn't mind (see IgnoredByHunter), so dangerous prey is only hunted alone - only in the
		/// village while the viking may not leave it (see MayLeave)
		/// </summary>
		/// <param name="archer">Whether the viking is an archer, which also hunts the prey only archers hunt (see
		/// HuntPrey)</param>
		/// <param name="wanted">The prefab name of what the prey has to drop, like resin from a greyling, or null for any
		/// prey</param>
		/// <param name="livestock">True to take only the village's own livestock, which it slaughters when nothing wild
		/// drops what it's after (see GatherFor)</param>
		/// <param name="party">True for prey a hunting party goes after - from the Plains on, a viking hunts wild prey
		/// that isn't only for archers only as a party, and alone only what is (see GroupHunts)</param>
		/// <param name="food">True to take only prey that drops food, like meat (see QuestLoot.DropsFood)</param>
		/// <returns>The prey, or null when there's none</returns>
		private Character FindPrey(bool archer, string wanted, bool livestock, bool party = false, bool food = false)
		{
			float reach = (MayLeave() ? HuntReach : 0f);
			bool grouped = (!livestock && GroupHunts());
			List<Character> found = new List<Character>();
			foreach (Character creature in Character.GetAllCharacters())
			{
				bool prey = (livestock ? (creature != null && !creature.IsDead() && FarmSpawns.IsLivestock(creature) && !ZombieState.IsZombie(creature)) : IsPrey(creature, archer));
				if (!prey || (wanted != null && !Drops(creature.gameObject, wanted)) || village.DistanceOutside(creature.transform.position) > reach || hunts.ContainsValue(creature))
				{
					continue;
				}
				if (food && !QuestLoot.DropsFood(Utils.GetPrefabName(creature.gameObject)))
				{
					continue;
				}
				// Wild prey is only hunted on ground no harder than the hunter's own biome
				if (!livestock && TooHardToHunt(creature.transform.position))
				{
					continue;
				}
				if (grouped && HuntPrey.ForParty(Utils.GetPrefabName(creature.gameObject)) != party)
				{
					continue;
				}
				if (creature.m_nview != null && creature.m_nview.IsValid() && Avoided(creature.m_nview.GetZDO().m_uid))
				{
					continue;
				}
				found.Add(creature);
			}
			found.Sort((Character a, Character b) => Vector3.Distance(a.transform.position, transform.position).CompareTo(Vector3.Distance(b.transform.position, transform.position)));
			foreach (Character prey in found)
			{
				if (!EnemyNear(prey.transform.position, ForageSafety, (Character other) => other == prey || IgnoredByHunter(prey, other)))
				{
					return(prey);
				}
			}
			return(null);
		}

		/// <summary>
		/// Checks whether there's wild prey about that drops food for the village's hunters - the prey only archers hunt
		/// too when the village has an archer, hunted alone or by a party (see FindPrey)
		/// </summary>
		/// <returns>True when there is</returns>
		private bool WildFoodNear()
		{
			bool archers = village.Vikings().Exists((Character other) => HasBow(other));
			return(FindPrey(archers, null, false, false, true) != null || FindPrey(archers, null, false, true, true) != null);
		}

		/// <summary>
		/// Checks whether a spot is on ground harder than the viking's own biome - the one its gear comes from - which it
		/// doesn't go hunting on, so a hunter of the Meadows doesn't go after prey out on the Plains
		/// </summary>
		/// <param name="point">The spot</param>
		/// <returns>True when it's harder</returns>
		private bool TooHardToHunt(Vector3 point)
		{
			if (WorldGenerator.instance == null || viking == null)
			{
				return(false);
			}
			return(VikingGear.IndexOf(WorldGenerator.instance.GetBiome(point)) > viking.BiomeIndex());
		}

		/// <summary>
		/// Finds the nearest bird within HuntReach of the village for an archer to shoot for its feathers - a seagull,
		/// crow or the like, landed on the ground, since the vikings never shoot at one in the air - that no other viking
		/// is after and with no enemy near it but prey that isn't dangerous (see IsHarmlessPrey), only in the village while
		/// the viking may not leave it (see MayLeave)
		/// </summary>
		/// <param name="wanted">The prefab name of what the bird has to drop, like feathers, or null for any bird</param>
		/// <returns>The bird, or null when there's none</returns>
		private RandomFlyingBird FindBird(string wanted)
		{
			float reach = (MayLeave() ? HuntReach : 0f);
			List<KeyValuePair<float, RandomFlyingBird>> found = new List<KeyValuePair<float, RandomFlyingBird>>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZNetView view = instance.Value;
				if (view == null || !view.IsValid() || village.DistanceOutside(view.transform.position) > reach || Avoided(instance.Key.m_uid))
				{
					continue;
				}
				if (wanted != null && !Drops(view.gameObject, wanted))
				{
					continue;
				}
				RandomFlyingBird bird = view.GetComponent<RandomFlyingBird>();
				if (bird == null || !Landed(bird) || view.GetComponent<Destructible>() == null || TooHardToHunt(view.transform.position))
				{
					continue;
				}
				if (workClaims.TryGetValue(instance.Key.m_uid, out VillageLife holder) && holder != this && holder != null && holder.job != null)
				{
					continue;
				}
				found.Add(new KeyValuePair<float, RandomFlyingBird>(Vector3.Distance(view.transform.position, transform.position), bird));
			}
			found.Sort((KeyValuePair<float, RandomFlyingBird> a, KeyValuePair<float, RandomFlyingBird> b) => a.Key.CompareTo(b.Key));
			foreach (KeyValuePair<float, RandomFlyingBird> entry in found)
			{
				if (!EnemyNear(entry.Value.transform.position, ForageSafety, (Character other) => IsHarmlessPrey(other)))
				{
					return(entry.Value);
				}
			}
			return(null);
		}

		/// <summary>
		/// Checks whether a bird has landed on the ground, as the game notes it for every client (see
		/// RandomFlyingBird.CustomFixedUpdate)
		/// </summary>
		/// <param name="bird">The bird</param>
		/// <returns>True while it's landed</returns>
		private static bool Landed(RandomFlyingBird bird)
		{
			return(bird.m_nview != null && bird.m_nview.IsValid() && bird.m_nview.GetZDO().GetBool(ZDOVars.s_landed));
		}

		/// <summary>
		/// Works out where an archer aims to hit a bird - ahead of it by as far as it flies while the arrow is on its
		/// way, and above it by as far as the arrow drops on the way there
		/// </summary>
		/// <param name="target">Where the bird is</param>
		/// <param name="velocity">How fast it's flying, in meters a second</param>
		/// <returns>The point to aim at</returns>
		private Vector3 BirdAim(Vector3 target, Vector3 velocity)
		{
			ItemDrop.ItemData weapon = humanoid.GetCurrentWeapon();
			if (weapon == null || string.IsNullOrEmpty(weapon.m_shared.m_ammoType))
			{
				return(target);
			}
			float speed = weapon.m_shared.m_attack.m_projectileVel;
			GameObject shot = weapon.m_shared.m_attack.m_attackProjectile;
			ItemDrop.ItemData ammo = humanoid.GetAmmoItem();
			if (ammo == null || !humanoid.GetInventory().ContainsItem(ammo))
			{
				ammo = humanoid.GetInventory().GetAmmoItem(weapon.m_shared.m_ammoType);
			}
			if (ammo != null && ammo.m_shared.m_attack.m_attackProjectile != null)
			{
				speed += ammo.m_shared.m_attack.m_projectileVel;
				shot = ammo.m_shared.m_attack.m_attackProjectile;
			}
			Projectile projectile = ((shot != null) ? shot.GetComponent<Projectile>() : null);
			float gravity = ((projectile != null) ? projectile.m_gravity : 0f);
			float time = Vector3.Distance(humanoid.m_eye.position, target) / Mathf.Max(1f, speed);
			return(target + velocity * time + Vector3.up * (0.5f * gravity * time * time));
		}

		/// <summary>
		/// Works out how near the viking gets to prey before it stops to shoot it from afar, from the bow it shoots
		/// with and the ammo it has for it (see SnipeRangeFor) - SnipeRangeMin when it has none
		/// </summary>
		/// <returns>The range, in meters</returns>
		private float SnipeRange()
		{
			ItemDrop.ItemData bow = ((viking != null) ? viking.Bow() : null);
			Attack attack = ((bow != null) ? bow.m_shared.m_attack : null);
			if (attack == null)
			{
				return(SnipeRangeMin);
			}
			float speed = attack.m_projectileVel;
			float spread = attack.m_projectileAccuracy;
			ItemDrop.ItemData ammo = humanoid.GetInventory().GetAmmoItem(bow.m_shared.m_ammoType);
			if (ammo != null && ammo.m_shared.m_attack != null)
			{
				speed += ammo.m_shared.m_attack.m_projectileVel;
				spread += ammo.m_shared.m_attack.m_projectileAccuracy;
			}
			return(SnipeRangeFor(speed, spread));
		}

		/// <summary>
		/// Works out how near an archer gets to prey before it stops to shoot it from afar - as far as its arrows fly in
		/// SnipeFlightTime, and no farther than they stray SnipeSpreadTolerance from where it aims, between
		/// SnipeRangeMin and SightRange. A viking always draws its bow fully, so its arrows fly at the bow's full speed
		/// and spread as little as the bow lets them (see VikingDrawPatch)
		/// </summary>
		/// <param name="speed">How fast its arrows fly, in meters a second</param>
		/// <param name="spread">How far its arrows stray from where it aims, either way, in degrees</param>
		/// <returns>The range, in meters</returns>
		internal static float SnipeRangeFor(float speed, float spread)
		{
			float range = speed * SnipeFlightTime;
			if (spread > 0f)
			{
				range = Mathf.Min(range, SnipeSpreadTolerance / Mathf.Tan(spread * Mathf.Deg2Rad));
			}
			return(Mathf.Clamp(range, SnipeRangeMin, SightRange));
		}

		/// <summary>
		/// Checks whether an archer is ready to loose an arrow at a point - its bow is in its hands with arrows for it,
		/// and it faces the point
		/// </summary>
		/// <param name="aim">The point</param>
		/// <returns>True when it is</returns>
		private bool ReadyToShoot(Vector3 aim)
		{
			ItemDrop.ItemData weapon = humanoid.GetCurrentWeapon();
			if (weapon == null || string.IsNullOrEmpty(weapon.m_shared.m_ammoType) || humanoid.GetInventory().GetAmmoItem(weapon.m_shared.m_ammoType) == null || humanoid.InAttack())
			{
				return(false);
			}
			Vector3 toward = aim - transform.position;
			toward.y = 0f;
			Vector3 facing = transform.forward;
			facing.y = 0f;
			return(toward.sqrMagnitude < 0.01f || Vector3.Angle(toward, facing) <= BirdAimAngle);
		}

		/// <summary>
		/// Runs the viking's job in place of the game's wandering while it has nothing to fight, picking a new one when
		/// it has none - every MournCheckInterval, dropping it to mourn one of its village whose corpse lies near (see
		/// FindMourning) - and every WorkCheckInterval, dropping a job that has it freezing out in the cold by day to warm
		/// up (see FreezesAtWork), dropping looting, strolling, standing about or petting a boar for
		/// done food on a cooking station before it burns, dropping strolling, standing about or petting a boar for work
		/// that needs doing, like loot to put away, after a fight dropping more for the loot it left (see
		/// YieldsToLoot), and at dusk getting up from the gathering to teach a forsaken power (see FindTeachWork) - and
		/// uses a forsaken power for its job or for being hurt (see UpdateJobPower)
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True when its life handled it, so the wandering is skipped</returns>
		public bool Idle(float dt)
		{
			if (!active)
			{
				return(RoadIdle(dt));
			}
			if (!questChecked)
			{
				questChecked = true;
				ReturnFromQuest();
			}
			UpdateHomecoming();
			if (UpdateWary(dt))
			{
				return(true);
			}
			if (Time.time >= nextMournCheck)
			{
				nextMournCheck = Time.time + MournCheckInterval;
				Job mourn = FindMourning();
				if (mourn != null)
				{
					Interrupt("mourning");
					job = mourn;
					job.Begin();
				}
			}
			if (Time.time >= nextWorkCheck && FreezesAtWork())
			{
				// Freezing out in the cold, it drops what it's doing to warm up
				nextWorkCheck = Time.time + WorkCheckInterval;
				Interrupt("freezing");
				job = new GatherJob(this, false, true);
				job.Begin();
			}
			if (Time.time >= nextWorkCheck)
			{
				Job work = null;
				// Done food burns before long, so it's taken off ahead of loot, even partway through putting loot away - and
				// by a viking at most other work, which only takes it off and goes back to its work
				if ((job is LootJob || job is IdleJob || job is StrollJob || job is PetJob) && !IsNight())
				{
					nextWorkCheck = Time.time + WorkCheckInterval;
					work = FindTendWork(true);
				}
				else if (TakesOffDoneFood())
				{
					nextWorkCheck = Time.time + WorkCheckInterval;
					work = FindTendWork(true, true);
				}
				// What an enemy it killed dropped is picked up first, wherever it lies
				if (work == null && PicksUpKillLoot())
				{
					nextWorkCheck = Time.time + WorkCheckInterval;
					work = FindKillLoot();
				}
				if (work == null && AfterFight() && YieldsToLoot())
				{
					nextWorkCheck = Time.time + WorkCheckInterval;
					work = FindLootWork(true);
				}
				if (work == null && (job is IdleJob || job is StrollJob || job is PetJob) && !IsNight() && !IsDusk())
				{
					nextWorkCheck = Time.time + WorkCheckInterval;
					work = FindWork();
				}
				// Gathered at the fire at dusk, it gets up to teach a forsaken power to one of the village who lacks it
				if (work == null && job is GatherJob gathering && gathering.Evening() && IsDusk())
				{
					nextWorkCheck = Time.time + WorkCheckInterval;
					work = FindTeachWork();
				}
				if (work != null)
				{
					StopJob();
					job = work;
					job.Begin();
				}
			}
			if (job == null)
			{
				job = ChooseJob();
				job.Begin();
			}
			LogJobTick();
			UpdateJobPower();
			bool firstTick = (job != tickedJob);
			tickedJob = job;
			if (job.Tick(dt))
			{
				string kind = JobName(job);
				string ended = job.Describe();
				StopJob(true);
				if (Churning(firstTick, kind, ended))
				{
					job = new IdleJob(this, ChurnPause);
					job.Begin();
				}
			}
			if (Time.time >= nextRestCheck)
			{
				nextRestCheck = Time.time + RestCheckInterval;
				body.Rest.Check(job != null && job.Sitting());
				ReadyBestArrows();
				UpdateOuting(false);
			}
			return(true);
		}

		/// <summary>
		/// The job the viking last carried on, so a job that ends on its first update is told apart, and how many jobs in a
		/// row have ended on their first update
		/// </summary>
		private Job tickedJob;
		private int instantEnds;

		/// <summary>
		/// How many jobs in a row ending on their first update count as the viking picking a job it can't do over and
		/// over, and how long it stands about then before picking again, in seconds
		/// </summary>
		private const int ChurnLimit = 20;
		private const float ChurnPause = 10f;

		/// <summary>
		/// Counts a job ending, and checks whether the viking keeps picking jobs that end as soon as they start - a job
		/// whose choice and whose own checks don't agree, picked again every update - writing it to the log with the last
		/// of them, so it stands about for ChurnPause instead of picking again at once
		/// </summary>
		/// <param name="firstTick">Whether the job ended on its first update</param>
		/// <param name="kind">The kind of job, for the totals</param>
		/// <param name="ended">What the job said it was doing</param>
		/// <returns>True when it keeps doing so</returns>
		private bool Churning(bool firstTick, string kind, string ended)
		{
			instantEnds = (firstTick ? instantEnds + 1 : 0);
			if (instantEnds < ChurnLimit)
			{
				return(false);
			}
			instantEnds = 0;
			TestLog.Count("Troubles", "jobs ending as they start");
			TestLog.Count("Jobs ending as they start, by job", kind);
			if (TestLog.On || Player.m_debugMode)
			{
				Plugin.Log.LogWarning("Churn: " + LogName() + " started and ended " + ChurnLimit + " jobs in a row at once, the last " + ended + " - standing about for " + ChurnPause.ToString("F0") + "s");
			}
			return(true);
		}

		/// <summary>
		/// Checks whether the viking comes from a harder biome than its village's (see VillageCooking.VillageBiome), so
		/// its gear makes it safer out of the village than the others
		/// </summary>
		/// <returns>True when it does</returns>
		private bool OutclassesVillage()
		{
			return(viking != null && village != null && viking.BiomeIndex() > VillageCooking.VillageBiome(village));
		}

		/// <summary>
		/// Checks whether the viking is hurt enough to rest by the fire first
		/// </summary>
		/// <returns>True when it's below WoundedShare of its health</returns>
		private bool Wounded()
		{
			return(humanoid != null && humanoid.GetHealth() < humanoid.GetMaxHealth() * WoundedShare);
		}

		/// <summary>
		/// Looks at using a forsaken power for the viking's job every JobPowerInterval seconds - Yagluth's in the first
		/// JobPowerWindow seconds of foraging, which raises its Farming skill, and Eikthyr's in the first JobPowerWindow
		/// seconds after its job takes it out of the village - or for being hurt (see VikingPowers.UpdateWork)
		/// </summary>
		private void UpdateJobPower()
		{
			if (job != powerJob)
			{
				powerJob = job;
				powerJobAt = Time.time;
				powerOutingAt = -1f;
			}
			if (Time.time < nextJobPowerCheck)
			{
				return;
			}
			nextJobPowerCheck = Time.time + JobPowerInterval;
			if (job != null && powerOutingAt < 0f && job.Outing())
			{
				powerOutingAt = Time.time;
			}
			bool farming = (job is ForageJob && Time.time - powerJobAt <= JobPowerWindow);
			bool outing = (powerOutingAt >= 0f && Time.time - powerOutingAt <= JobPowerWindow);
			body.Powers.UpdateWork(farming, outing);
		}

		/// <summary>
		/// Finds a forsaken power the viking knows to teach at the village's gathering at dusk, and one of the village who
		/// doesn't know it to teach it to (see VikingPowers.FindPupil) - none through a raid
		/// </summary>
		/// <returns>The job, or null when there's no one to teach or the viking can't use a power yet</returns>
		private Job FindTeachWork()
		{
			if (village == null || RaidReady())
			{
				return(null);
			}
			Character pupil = body.Powers.FindPupil(village, out int power);
			return((pupil != null) ? new TeachJob(this, pupil, power) : null);
		}

		/// <summary>
		/// Checks whether the viking is freezing by day at a job that leaves it out in the cold - anything but gathering
		/// at or resting by a fire, keeping warm, getting up from bed, keeping watch, lightening its load or eating,
		/// which don't keep it out long
		/// </summary>
		/// <returns>True when it should drop its job to warm up</returns>
		private bool FreezesAtWork()
		{
			if (IsNight() || IsDusk() || job is GatherJob || job is RestJob || job is WatchJob || job is UnburdenJob || job is EatJob)
			{
				return(false);
			}
			return(VikingWeather.IsFreezing(humanoid));
		}

		/// <summary>
		/// Notes an enemy that hurt the viking, so it stops what it's doing and chases them past its leash
		/// </summary>
		/// <param name="attacker">Who hurt it, or null</param>
		public void OnHurt(Character attacker)
		{
			if (!active)
			{
				return;
			}
			Interrupt("being hurt");
			if (attacker != null && !attacker.IsDead() && BaseAI.IsEnemy(humanoid, attacker))
			{
				grudge = attacker;
				grudgeUntil = Time.time + GrudgeTime;
			}
		}

		/// <summary>
		/// Rushes the viking to help when someone near it is attacked - through a raid, anywhere in its village (see
		/// Hears)
		/// </summary>
		/// <param name="where">Where they were attacked</param>
		/// <param name="attacker">Who attacked them</param>
		public void OnDistress(Vector3 where, Character attacker)
		{
			if (!active || nview == null || !nview.IsValid() || !nview.IsOwner() || humanoid.IsDead())
			{
				return;
			}
			if (attacker == humanoid || !Hears(where) || !BaseAI.IsEnemy(humanoid, attacker))
			{
				return;
			}
			Character current = ai.m_targetCreature;
			if (current != null && current != attacker && Vector3.Distance(current.transform.position, transform.position) < Vector3.Distance(attacker.transform.position, transform.position))
			{
				return;
			}
			Interrupt("helping one attacked");
			grudge = attacker;
			grudgeUntil = Time.time + GrudgeTime;
			Engage(attacker);
		}

		/// <summary>
		/// Checks whether a fight or a call for help at a place reaches the viking - within DefendRange of it, or through
		/// a raid it's ready for, anywhere in its village or within RaidCallReach past its edge, so the whole village
		/// fights a raid together rather than leaving one of its own to it at the edge
		/// </summary>
		/// <param name="where">Where the fight or call is</param>
		/// <returns>True when it reaches the viking</returns>
		private bool Hears(Vector3 where)
		{
			if (Vector3.Distance(where, transform.position) <= DefendRange)
			{
				return(true);
			}
			return(RaidReady() && village.DistanceOutside(where) <= RaidCallReach);
		}

		/// <summary>
		/// Wakes the viking when its village's watch raises the alarm, and sends it after the enemy nearest the village
		/// </summary>
		/// <param name="farm">The village's farm</param>
		public void OnAlarm(Vector3 farm)
		{
			if (!active || village == null || Utils.DistanceXZ(village.Farm, farm) > 1f || nview == null || !nview.IsOwner())
			{
				return;
			}
			Interrupt("an alarm");
			SetDulled(false);
			Character enemy = NearestEnemy(AlarmRange);
			if (enemy != null)
			{
				Engage(enemy);
			}
			else
			{
				ai.SetAlerted(true);
			}
		}

		/// <summary>
		/// Checks whether the viking wants a torch out - from just before sunset until dawn while it isn't near one of
		/// the village's gathering fires, always then while it's on watch, never while it rests, and never by day
		/// </summary>
		/// <returns>True when it does</returns>
		public bool WantsTorch()
		{
			if (!IsNight() && (EnvMan.instance == null || EnvMan.instance.GetDayFraction() < TorchStart))
			{
				return(false);
			}
			if (job != null && job.Resting())
			{
				return(false);
			}
			if (IsWatch())
			{
				return(true);
			}
			List<Fireplace> fires = ((village != null) ? village.GatherFires() : new List<Fireplace>());
			return(!fires.Exists((Fireplace fire) => fire != null && Utils.DistanceXZ(fire.transform.position, transform.position) <= TorchFireRange));
		}

		/// <summary>
		/// Works out whether the viking's life runs - with a village it belongs to loaded, and on its owner - finding its
		/// village and noting it as the village's when it has none yet
		/// </summary>
		/// <returns>True when it runs</returns>
		private bool UpdateActive()
		{
			bool wanted = (ServerCheck.Active && nview != null && nview.IsValid() && nview.IsOwner() && ai != null && !humanoid.IsDead());
			if (wanted && (village == null || Time.time >= nextVillageCheck))
			{
				nextVillageCheck = Time.time + VillageCheckInterval;
				Village found = FindVillage();
				// A viking moving to another village drops what it was doing for its old one
				if (village != null && found != null && found != village)
				{
					Interrupt("moving to another village");
				}
				village = found;
			}
			wanted = (wanted && village != null);
			if (!wanted && active)
			{
				Interrupt("leaving village life");
				SetDulled(false);
			}
			active = wanted;
			return(active);
		}

		/// <summary>
		/// Finds the village the viking belongs to, or the one it stands in when it hasn't noted one - one spawned or
		/// brought in from outside any village - noting it as the village's, at home in the house nearest it. A viking
		/// away from its own village for SwitchAwayTime joins the village it's in the same way
		/// </summary>
		/// <remarks>
		/// It's away whenever it's farther than Village.FindReach outside its own village, or its village isn't loaded,
		/// and how long it has been is kept in its ZDO in game time, so it carries over a restart
		/// </remarks>
		/// <returns>The village, or null</returns>
		private Village FindVillage()
		{
			ZDO zdo = nview.GetZDO();
			Village here = Village.Find(transform.position);
			if (zdo.GetVec3(Village.VillageKey, Vector3.zero) != Vector3.zero)
			{
				Village own = Village.Of(zdo);
				if (own != null && own.DistanceOutside(transform.position) <= Village.FindReach)
				{
					zdo.Set(AwaySinceKey, 0L);
					return(own);
				}
				long now = NetTime.Now();
				long since = zdo.GetLong(AwaySinceKey, 0L);
				if (since == 0L)
				{
					zdo.Set(AwaySinceKey, now);
					return(own);
				}
				if (here == null || now - since < NetTime.Ticks(SwitchAwayTime) || !here.NearestCoreHouse(transform.position, out VillagePaths.House newHome))
				{
					return(own);
				}
				here.Adopt(zdo, newHome);
				zdo.Set(AwaySinceKey, 0L);
				Plugin.Log.LogInfo(LogName() + " was away from its village long enough, and joined the village at " + here.Farm.ToString("F0"));
				return(here);
			}
			// One on the road joins only the village it's going to, none joins one while sitting on a boat, and none joins
			// a village before it has every viking it starts with, so it doesn't take a new viking's place
			if (here != null && VikingBoat.BoardOf(zdo) != Crossing.Aboard && VillageTravel.Bound(zdo, here) && VillageTravel.Takes(here) && here.NearestCoreHouse(transform.position, out VillagePaths.House house))
			{
				here.Adopt(zdo, house);
				return(here);
			}
			return(null);
		}

		/// <summary>
		/// Finds the house the viking calls home - always one of the village's core that still stands, so a viking that
		/// called an outpost home moves to the core house nearest it, and one whose home was razed moves to the standing
		/// house nearest it
		/// </summary>
		/// <remarks>
		/// While none of the core stands, the viking has no home, though it keeps the one it called home, so it goes
		/// back there once it's rebuilt
		/// </remarks>
		/// <param name="house">The house</param>
		/// <returns>False when its village has no house standing</returns>
		private bool Home(out VillagePaths.House house)
		{
			Vector3 center = nview.GetZDO().GetVec3(Village.HomeKey, transform.position);
			if (!village.NearestHouse(center, out house))
			{
				return(false);
			}
			if (!village.IsCore(house) && village.NearestCoreHouse(house.Center, out VillagePaths.House core))
			{
				house = core;
				village.Adopt(nview.GetZDO(), house);
			}
			Vector3 home = house.Center;
			List<VillagePaths.House> standing = VillageRecords.StandingCore(village);
			if (standing.Exists((VillagePaths.House other) => Utils.DistanceXZ(other.Center, home) <= 1f))
			{
				return(true);
			}
			if (standing.Count == 0)
			{
				return(false);
			}
			List<VillagePaths.House> nearest = new List<VillagePaths.House>(standing);
			nearest.Sort((VillagePaths.House a, VillagePaths.House b) => Utils.DistanceXZ(a.Center, home).CompareTo(Utils.DistanceXZ(b.Center, home)));
			house = nearest[0];
			village.Adopt(nview.GetZDO(), house);
			return(true);
		}

		/// <summary>
		/// Picks what the viking does next - lightening its load first when it carries more than it can, then putting
		/// away what it brought home from a quest, and otherwise by
		/// the time of day: resting or keeping watch at night, getting up in the morning, gathering at the fire at dusk, and by day working whenever there's work, or else talking, petting,
		/// gathering wood or foraging food for a chest with room for it, getting rested again by the fire before late in
		/// the day, hunting, strolling or standing about - in the rain leaving out strolling, petting and getting rested
		/// again, and standing about inside its house
		/// </summary>
		/// <returns>The job</returns>
		private Job ChooseJob()
		{
			// Carrying more than it can, it lightens its load before anything else, by day or night, so it doesn't gather
			// at the fire or go to bed held back by it
			if (body.IsEncumbered() && HeaviestSpare() != null)
			{
				return(new UnburdenJob(this));
			}
			if (IsNight() || IsDusk())
			{
				// The watch is picked before anyone heads off, so the one picked goes straight to keeping watch
				VillageWatch.EnsureWatch(village);
			}
			// Through a raid it only keeps watch, eats or rests by the fire while wounded, or keeps guard
			if (RaidReady())
			{
				return(RaidJob());
			}
			// What an enemy it killed dropped, or the gear of one of its village it has just mourned, is picked up before
			// it takes up anything else
			if (PicksUpKillLoot())
			{
				Job loot = FindKillLoot();
				if (loot != null)
				{
					return(loot);
				}
			}
			// What it brought home from a quest is put away first, even before bed
			Job questLoot = FindQuestLootWork();
			if (questLoot != null)
			{
				return(questLoot);
			}
			if (IsNight())
			{
				// Food left cooking into the night is taken off once it's done, by the next viking up and about
				Job tend = FindTendWork(true, true);
				if (tend != null)
				{
					return(tend);
				}
				// A viking whose torches won't see it through the dark crafts one first - the watch, one to last its shift
				Job torch = FindNightTorchWork();
				if (torch != null)
				{
					return(torch);
				}
				if (IsWatch())
				{
					return(new WatchJob(this));
				}
				// One that isn't ready for bed yet stays by the fire a while
				return(Bedtime() ? (Job)new RestJob(this) : new GatherJob(this, false));
			}
			// Tonight's bedtime is picked again when the night comes
			bedAt = -1f;
			if (IsDusk())
			{
				// Food left on a station is taken off before the village gathers, so it doesn't burn in the night, a viking
				// whose torches won't see it through the dark crafts one, and one with a forsaken power another of the
				// village doesn't know teaches it to them
				Job tend = FindTendWork();
				return(tend ?? FindNightTorchWork() ?? FindTeachWork() ?? new GatherJob(this, false));
			}
			Job work = FindWork();
			if (work != null)
			{
				return(work);
			}
			// In the rain it doesn't stroll about or pet the boars - it stands about out of the rain instead. The Swamp's
			// own drizzle, which hardly lets up, doesn't keep it in
			bool raining = VikingWeather.KeepsIndoors(transform.position);
			VillageLife partner = FindPartner();
			Character boar = ((raining || OtherDoing(PettingKey)) ? null : FindBoar());
			Container store = ((Time.time >= noWoodUntil && !OtherDoing(StockingKey)) ? FindStockChest() : null);
			bool forage = (!OtherDoing(ForagingKey) && (FindHive() != null || FindForage() != null));
			// Any rain, the Swamp's drizzle too, keeps it from getting rested out by the fire, where it would only get wet
			bool refresh = (!VikingWeather.RainsOn(transform.position) && !body.Rest.IsRested() && EnvMan.instance.GetDayFraction() < RefreshDayEnd && Time.time >= restAgainAt && village.GatherFires().Count > 0);
			bool archer = false;
			bool mayHunt = (!Wounded() && !OtherDoing(HuntingKey));
			bool solo = (mayHunt && MayHunt(out archer) && (FindPrey(archer, null, false) != null || (archer && FindBird(null) != null)));
			// From the Plains on, wild prey that isn't only for archers is hunted by a party (see HuntParty)
			bool party = (mayHunt && CanLeadParty(null));
			bool hunt = (solo || party);
			float talk = ((partner != null) ? TalkWeight : 0f);
			float pet = ((boar != null) ? PetWeight : 0f);
			float strong = (OutclassesVillage() ? StrongOutingBoost : 1f);
			float stock = ((store != null) ? StockWeight * strong : 0f);
			// While the chests hold too little food for the village to keep its foods up, getting more comes up far more often
			bool foodLow = FoodLow(village);
			float need = (foodLow ? FoodShortBoost : 1f);
			float gather = (forage ? ForageWeight * need * strong : 0f);
			float rest = (refresh ? RefreshWeight : 0f);
			float hunting = (hunt ? HuntWeight * need * strong : 0f);
			// Short of food with no wild prey about that drops any for the village's hunters, it slaughters one of the
			// village's livestock, which its pen brings back
			bool slaughter = (foodLow && mayHunt && !WildFoodNear() && FindPrey(HasBow(humanoid), null, true, false, true) != null);
			float butcher = (slaughter ? HuntWeight * need : 0f);
			float stroll = (raining ? 0f : StrollWeight);
			float roll = Random.value * (talk + pet + stock + gather + rest + hunting + butcher + stroll + IdleWeight);
			if (roll < talk)
			{
				TalkSession session = new TalkSession(this, partner, Random.Range(TalkTimeMin, TalkTimeMax));
				partner.JoinTalk(session);
				return(new TalkJob(this, session));
			}
			roll -= talk;
			if (roll < pet)
			{
				return(new PetJob(this, boar));
			}
			roll -= pet;
			if (roll < stock)
			{
				return(new StockJob(this, store));
			}
			roll -= stock;
			if (roll < gather)
			{
				return(new ForageJob(this));
			}
			roll -= gather;
			if (roll < rest)
			{
				return(new GatherJob(this, true));
			}
			roll -= rest;
			if (roll < hunting)
			{
				Job led = (party ? LeadParty(null) : null);
				return(led ?? new HuntJob(this, archer, null, false));
			}
			roll -= hunting;
			if (roll < butcher)
			{
				return(new HuntJob(this, HasBow(humanoid), null, true));
			}
			roll -= butcher;
			if (raining)
			{
				return(new IdleJob(this, Random.Range(IdleTimeMin, IdleTimeMax), true));
			}
			// It doesn't stand about inside a house by day, where it'd be in the way of the chests and the door - it
			// strolls out instead
			if (roll < stroll || village.InHouse(transform.position, 0f))
			{
				return(new StrollJob(this));
			}
			return(new IdleJob(this, Random.Range(IdleTimeMin, IdleTimeMax)));
		}

		/// <summary>
		/// What a viking does as day work to get food for a village short of it (see PickFoodWork)
		/// </summary>
		internal enum FoodWork
		{
			None,
			Hunt,
			Slaughter
		}

		/// <summary>
		/// Picks what a viking does to get food for its village - nothing when the chests hold food enough or it may not
		/// hunt, hunting wild prey near the village that drops food when it can go after some, or else slaughtering one
		/// of the village's livestock, only when no wild prey that drops food is about for any of the village's hunters
		/// </summary>
		/// <param name="foodLow">Whether the chests hold too little food for the vikings to keep their foods up</param>
		/// <param name="mayHunt">Whether the viking may hunt now - not wounded, with few enough of the village
		/// hunting</param>
		/// <param name="preyNear">Whether there's wild prey near that drops food that the viking itself hunts</param>
		/// <param name="wildFoodNear">Whether there's wild prey near that drops food for any of the village's
		/// hunters</param>
		/// <param name="livestock">Whether the village has livestock that drops food to slaughter</param>
		/// <returns>What it does</returns>
		internal static FoodWork PickFoodWork(bool foodLow, bool mayHunt, bool preyNear, bool wildFoodNear, bool livestock)
		{
			if (!foodLow || !mayHunt)
			{
				return(FoodWork.None);
			}
			if (preyNear)
			{
				return(FoodWork.Hunt);
			}
			return((!wildFoodNear && livestock) ? FoodWork.Slaughter : FoodWork.None);
		}

		/// <summary>
		/// Finds work getting food while the village's chests hold too little for its vikings to keep their foods up (see
		/// FoodLow), ahead of the village's upkeep - hunting wild prey near it that drops food, or slaughtering one of its
		/// livestock (see PickFoodWork). With neither, the viking goes on to the rest of its work, and getting food is
		/// left to the rolled jobs, which favor foraging and hunting while food is low
		/// </summary>
		/// <returns>The job, or null when there's food enough, the viking may not hunt, or there's nothing to hunt or
		/// slaughter</returns>
		private Job FindFoodWork()
		{
			bool foodLow = FoodLow(village);
			bool mayHunt = (foodLow && !Wounded() && !OtherDoing(HuntingKey));
			bool archer = false;
			bool preyNear = (mayHunt && MayHunt(out archer) && FindPrey(archer, null, false, false, true) != null);
			bool wildFood = (mayHunt && !preyNear && WildFoodNear());
			bool livestock = (mayHunt && !preyNear && !wildFood && FindPrey(HasBow(humanoid), null, true, false, true) != null);
			FoodWork work = PickFoodWork(foodLow, mayHunt, preyNear, wildFood, livestock);
			if (work == FoodWork.Hunt)
			{
				return(new HuntJob(this, archer, null, false, null, true));
			}
			if (work == FoodWork.Slaughter)
			{
				return(new HuntJob(this, HasBow(humanoid), null, true));
			}
			return(null);
		}

		/// <summary>
		/// Finds work for the viking, most needed first - leaving on a quest of its village it's on (see
		/// FindQuestWork), then when it's wounded, eating from a chest and then resting by the
		/// fire until it's rested, then eating to keep its foods up while there's food enough to go around (see
		/// FoodToSpare), then in freezing weather keeping warm by a lit fire, or inside its house when none is
		/// burning, until the weather clears, then putting away loot it already carries, then loot lying around the
		/// village to put in a chest, then cooking the raw food it carries or the chests hold at a cooking station, then a
		/// meal at the cauldron (see FindMealWork), then gear in the
		/// village's chests better than its own to take up, then for an archer arrows in a chest while it's missing a
		/// batch or better ones than its best, then while the village is short of food prey to hunt or livestock to
		/// slaughter for it (see FindFoodWork), then a path for a house added for a player's bed (see FindPathWork), then
		/// the nearest damaged piece of the village to repair, then
		/// its own gear to repair at the
		/// workbench or forge when a piece of it has worn down to GearWornShare, then the village's bonfire or campfire to
		/// build back when it's been destroyed, which takes nothing, then a sitting log left by the village's old fire to
		/// take down for its wood, then the village's cart to take down an old one of, fetch back or build (see
		/// FindCartWork), then another chest to build when the chests run low on room (see FindChestWork), then a missing
		/// piece to rebuild, then the fire to
		/// stack when it isn't full and no other viking is on it, then arrows to craft at the workbench when it has a bow
		/// and is missing a batch, then a torch or a missing weapon to craft, then a weapon, armor or a shield better than
		/// its own to craft (see FindGearCraftWork), then a piece of its gear to upgrade, each when the village has
		/// everything it takes (see FindUpgradeWork), then the next step toward the village's goal - smelting or crafting
		/// what it takes (see FindGoalWork) - then tools for the chests (see FindToolStockWork), then with Zombie
		/// Apocalypse a weapon of its own to line with silver (see FindLiningWork) and silver arrows to restock (see
		/// FindSilverArrowWork), then beehives -
		/// leaving out work that needs wood for a while after it found none to gather, unless there's wood in the
		/// village's chests
		/// </summary>
		/// <returns>The job, or null when there's nothing to do</returns>
		private Job FindWork()
		{
			// Carrying more than it can holds it back from everything else, so it lightens its load first
			if (body.IsEncumbered() && HeaviestSpare() != null)
			{
				return(new UnburdenJob(this));
			}
			// A quest setting out takes its vikings before any other work
			Job quest = FindQuestWork();
			if (quest != null)
			{
				return(quest);
			}
			if (Wounded())
			{
				Container pantry = ((Time.time >= eatAgainAt) ? FindFoodChest(false) : null);
				if (pantry != null)
				{
					return(new EatJob(this, pantry, false));
				}
				if (!body.Rest.IsRested() && Time.time >= restAgainAt && village.GatherFires().Count > 0)
				{
					return(new GatherJob(this, true));
				}
			}
			// With food enough in the chests to go around, it keeps its foods up for the health they add, eating each again
			// once it has nearly run out, on the weakest food that does it good - otherwise the food is left for the wounded
			if (Time.time >= eatAgainAt && FoodToSpare())
			{
				Container pantry = FindFoodChest(true);
				if (pantry != null)
				{
					return(new EatJob(this, pantry, true));
				}
			}
			// In freezing weather it keeps out of the cold, rather than working out in it until it freezes
			if (VikingWeather.FreezingFor(humanoid))
			{
				return(new GatherJob(this, false, true));
			}
			// Loot comes ahead of cooking, so it doesn't lie about while the cooks are busy - done food on a station is
			// still taken off before it burns, by a viking at loot work too (see Idle), and raw meat picked up as loot goes
			// straight to a cooking station
			Job lootWork = FindLootWork(false);
			if (lootWork != null)
			{
				return(lootWork);
			}
			Job cook = FindCookWork();
			if (cook != null)
			{
				return(cook);
			}
			// Then a meal at the cauldron, from what the chests hold
			Job meal = FindMealWork();
			if (meal != null)
			{
				return(meal);
			}
			bool carrying = (CarriedLoot() != null);
			Container armory = (carrying ? null : FindUpgradeChest());
			if (armory != null)
			{
				return(new UpgradeJob(this, armory));
			}
			Container quiver = (carrying ? null : FindArrowChest());
			if (quiver != null)
			{
				return(new AmmoJob(this, quiver));
			}
			// Short of food, getting more comes ahead of the village's upkeep, so a village always being attacked and
			// repaired still gets meat
			Job food = FindFoodWork();
			if (food != null)
			{
				return(food);
			}
			// A new house's path comes ahead of the village's upkeep - one viking at a time makes it, leaving the rest to
			// the upkeep - and waits out a thunderstorm, while light rain doesn't put it off
			if (!VikingWeather.Storming())
			{
				Job paths = FindPathWork();
				if (paths != null)
				{
					return(paths);
				}
			}
			List<WearNTear> damaged = VillageRecords.Damaged(village);
			damaged.Sort((WearNTear a, WearNTear b) => Vector3.Distance(a.transform.position, transform.position).CompareTo(Vector3.Distance(b.transform.position, transform.position)));
			foreach (WearNTear piece in damaged)
			{
				ZNetView view = piece.GetComponent<ZNetView>();
				if (view != null && view.IsValid() && !HammerAvoided(view.GetZDO().m_uid) && ClaimWork(view.GetZDO().m_uid))
				{
					return(new RepairJob(this, piece, view.GetZDO().m_uid));
				}
			}
			// In the Deep North, heavy snow on a piece wears it down, so it's cleared off once it builds up
			foreach (WearNTear piece in VillageSnow.Snowy(village, transform.position))
			{
				ZNetView view = piece.m_nview;
				if (!Warded(piece.transform.position) && !HammerAvoided(view.GetZDO().m_uid) && ClaimWork(view.GetZDO().m_uid))
				{
					return(new SnowJob(this, piece, view.GetZDO().m_uid));
				}
			}
			// Then the deep snow on the village's ground, which slows whoever walks through it
			if (VillageSnow.ClaimDeepSpot(village, transform.position, out Vector3 deep))
			{
				if (!Warded(deep))
				{
					return(new SnowGroundJob(this, deep));
				}
				VillageSnow.ReleaseDeepSpot(village, deep, false);
			}
			Job gear = FindGearWork();
			if (gear != null)
			{
				return(gear);
			}
			// The village's fire is built back with nothing, so it doesn't wait on wood. A ward keeps the vikings from
			// building in it, the same as it keeps out players it doesn't permit
			if (village.FireMissing(out PieceRecord fireRecord) && !Warded(fireRecord.Position) && ClaimWork(RecordKey(fireRecord)))
			{
				return(new RebuildJob(this, fireRecord, true));
			}
			// The sitting logs left by an old fire come down for their wood, which builds them around the new one
			foreach (Vector3 spot in VillageSeats.TakeDowns(village))
			{
				ZNetView log = (Warded(spot) ? null : VillageSeats.StandingLog(spot));
				if (log != null && ClaimWork(log.GetZDO().m_uid))
				{
					return(new TakeDownJob(this, log));
				}
			}
			Job cart = FindCartWork();
			if (cart != null)
			{
				return(cart);
			}
			Job chest = FindChestWork();
			if (chest != null)
			{
				return(chest);
			}
			Job lumber = FindLumberWork();
			if (lumber != null)
			{
				return(lumber);
			}
			if (Time.time >= noWoodUntil || CarriedWood() > 0 || FindWoodChest() != null)
			{
				Job wooden = FindWoodWork();
				if (wooden != null)
				{
					return(wooden);
				}
			}
			Job arrows = FindArrowWork();
			if (arrows != null)
			{
				return(arrows);
			}
			Job torch = FindTorchWork();
			if (torch != null)
			{
				return(torch);
			}
			Job sidearm = FindSidearmWork();
			if (sidearm != null)
			{
				return(sidearm);
			}
			// Better gear is crafted before what it would replace is upgraded
			Job better = FindGearCraftWork();
			if (better != null)
			{
				return(better);
			}
			Job upgrade = FindUpgradeWork();
			if (upgrade != null)
			{
				return(upgrade);
			}
			// Then the next step toward the village's goal, and only after that the tools for its chests, so they never
			// take what the goal's gear needs
			Job goal = FindGoalWork();
			if (goal != null)
			{
				return(goal);
			}
			Job tools = FindToolStockWork();
			if (tools != null)
			{
				return(tools);
			}
			Job lining = FindLiningWork();
			if (lining != null)
			{
				return(lining);
			}
			Job silverArrows = FindSilverArrowWork();
			if (silverArrows != null)
			{
				return(silverArrows);
			}
			// Beehives wait out a thunderstorm, while light rain doesn't put them off
			if (VikingWeather.Storming())
			{
				return(null);
			}
			Job hive = FindHiveWork();
			if (hive != null)
			{
				return(hive);
			}
			return(null);
		}

		/// <summary>
		/// Finds brush trees for the viking to cut down for their wood (see LumberJob) - when the village is out of wood,
		/// none carried, none in the chests and none found lying about lately, while it needs some badly (see
		/// VillageNeedsWood) - one viking at a time, and none while it's badly hurt. A viking with no axe takes one from
		/// the chests; or with none there, leaves the job to a fellow with an axe who's doing nothing that matters, for up
		/// to LumberHandOffWait seconds; or else crafts a stone axe, punching the trees for the wood it takes
		/// </summary>
		/// <returns>The job, or null when there's wood or no pressing need for it, no brush tree to cut, or a fellow with
		/// an axe is taking it on</returns>
		private Job FindLumberWork()
		{
			if (Time.time >= noWoodUntil || Wounded() || OtherDoing(LumberKey) || CarriedWood() > 0 || FindWoodChest() != null)
			{
				return(null);
			}
			if (!VillageNeedsWood())
			{
				return(null);
			}
			TreeBase tree = FindBrushTree();
			if (tree == null)
			{
				return(null);
			}
			Container axeChest = null;
			ArrowPlan axeCraft = null;
			if (OwnAxe() == null)
			{
				axeChest = Nearest(UsableChests().FindAll((Container chest) => !Avoided(chest.m_nview.GetZDO().m_uid) && chest.GetInventory().GetAllItems().Exists((ItemDrop.ItemData item) => !VikingItems.IsCarried(item) && IsAxe(item))));
				if (axeChest == null)
				{
					VillageLife holder = FreeAxeHolder();
					if (holder != null && HandOffLumber(holder))
					{
						return(null);
					}
					axeCraft = PlanCraft(StoneAxePrefab, out string _);
				}
			}
			lumberHandOffSince.Remove(village.Zdo.m_uid);
			return(new LumberJob(this, tree, axeChest, axeCraft));
		}

		/// <summary>
		/// Checks whether the village needs wood badly - one of its vikings on this client is down to fewer than
		/// QuiverCritical arrows, or its workbench or forge needs building back
		/// </summary>
		/// <returns>True when it does</returns>
		private bool VillageNeedsWood()
		{
			if (StationMissing())
			{
				return(true);
			}
			foreach (VillageLife other in All)
			{
				if (other != null && other.village == village && other.humanoid != null && !other.humanoid.IsDead() && other.CriticalArrows())
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// How long a viking with no axe leaves cutting trees to a fellow with one before it takes the job on itself, in
		/// seconds
		/// </summary>
		private const float LumberHandOffWait = 30f;

		/// <summary>
		/// When each village, by its generator's ZDO, started waiting for a viking with an axe to cut its trees, in
		/// Time.time seconds
		/// </summary>
		private static readonly Dictionary<ZDOID, float> lumberHandOffSince = new Dictionary<ZDOID, float>();

		/// <summary>
		/// Finds a fellow of the viking's village on this client with an axe of its own who's free to cut trees - not
		/// fighting, not badly hurt, and only standing about, strolling, talking or petting a boar
		/// </summary>
		/// <returns>The fellow, or null when there's none</returns>
		private VillageLife FreeAxeHolder()
		{
			foreach (VillageLife other in All)
			{
				if (other == null || other == this || other.village != village || !other.active || other.humanoid == null || other.humanoid.IsDead())
				{
					continue;
				}
				bool idle = (other.job == null || other.job is IdleJob || other.job is StrollJob || other.job is TalkJob || other.job is PetJob);
				if (idle && !other.Wounded() && other.OwnAxe() != null)
				{
					return(other);
				}
			}
			return(null);
		}

		/// <summary>
		/// Leaves cutting trees to a fellow with an axe - stopping what it's idly doing, so it looks for work and finds
		/// the job, knowing the village is out of wood - until the village has waited LumberHandOffWait seconds for one
		/// </summary>
		/// <param name="holder">The fellow</param>
		/// <returns>False once the village has waited long enough, so the viking takes the job on itself</returns>
		private bool HandOffLumber(VillageLife holder)
		{
			ZDOID id = village.Zdo.m_uid;
			// A wait long past is a new shortage, waited for afresh
			if (!lumberHandOffSince.TryGetValue(id, out float since) || Time.time - since > LumberHandOffWait * 2f)
			{
				since = Time.time;
				lumberHandOffSince[id] = since;
			}
			if (Time.time - since >= LumberHandOffWait)
			{
				return(false);
			}
			holder.noWoodUntil = Mathf.Max(holder.noWoodUntil, noWoodUntil);
			holder.Interrupt("cutting trees handed to it");
			return(true);
		}

		/// <summary>
		/// Checks whether an item is an axe - a weapon that chops, not held for show
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it is</returns>
		private static bool IsAxe(ItemDrop.ItemData item)
		{
			return(item != null && item.IsWeapon() && !VikingItems.IsProp(item) && item.GetDamage().m_chop > 0f);
		}

		/// <summary>
		/// Checks whether the village's workbench or forge has been destroyed and is waiting to be built back
		/// </summary>
		/// <returns>True when one is</returns>
		private bool StationMissing()
		{
			System.Predicate<PieceRecord> station = (PieceRecord record) => record.Prefab == WorkbenchHash || record.Prefab == ForgeHash;
			return(VillageRecords.Missing(village).Exists(station) || VillageRecords.MissingInRuins(village).Exists(station));
		}

		/// <summary>
		/// Checks whether a tree is a brush tree - a small one that drops its wood when it falls rather than a log to
		/// split, which any tool cuts
		/// </summary>
		/// <param name="tree">The tree, or null</param>
		/// <returns>True when it is</returns>
		private static bool IsBrush(TreeBase tree)
		{
			if (tree == null || tree.m_logPrefab != null || tree.m_minToolTier > 0 || tree.m_dropWhenDestroyed == null)
			{
				return(false);
			}
			return(tree.m_dropWhenDestroyed.m_drops.Exists((DropTable.DropData drop) => drop.m_item != null && WoodItems.Contains(drop.m_item.name)));
		}

		/// <summary>
		/// Finds the nearest brush tree within ForageReach of the village - one no other viking is after or the viking
		/// couldn't get to lately, with no danger near it - only in the village while the viking may not leave it (see
		/// MayLeave)
		/// </summary>
		/// <returns>The tree, or null when there's none</returns>
		private TreeBase FindBrushTree()
		{
			float reach = (MayLeave() ? ForageReach : 0f);
			List<KeyValuePair<float, TreeBase>> found = new List<KeyValuePair<float, TreeBase>>();
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZNetView view = instance.Value;
				if (view == null || village.DistanceOutside(view.transform.position) > reach || Avoided(instance.Key.m_uid))
				{
					continue;
				}
				TreeBase tree = view.GetComponent<TreeBase>();
				if (!IsBrush(tree))
				{
					continue;
				}
				if (workClaims.TryGetValue(instance.Key.m_uid, out VillageLife holder) && holder != this && holder != null && holder.job != null)
				{
					continue;
				}
				found.Add(new KeyValuePair<float, TreeBase>(Vector3.Distance(view.transform.position, transform.position), tree));
			}
			found.Sort((KeyValuePair<float, TreeBase> a, KeyValuePair<float, TreeBase> b) => a.Key.CompareTo(b.Key));
			foreach (KeyValuePair<float, TreeBase> entry in found)
			{
				if (!DangerNear(entry.Value.transform.position))
				{
					return(entry.Value);
				}
			}
			return(null);
		}

		/// <summary>
		/// Finds the viking's own axe - a weapon of its own that chops
		/// </summary>
		/// <returns>The axe, or null when it has none</returns>
		private ItemDrop.ItemData OwnAxe()
		{
			return(humanoid.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => item.IsWeapon() && !VikingItems.IsCarried(item) && !VikingItems.IsProp(item) && item.GetDamage().m_chop > 0f));
		}

		/// <summary>
		/// Swings at a tree the way a player chops one - its own axe's chopping damage and tier, or a punch's PunchChop
		/// with no axe - raising its WoodCutting skill the way a player's rises
		/// </summary>
		/// <param name="tree">The tree</param>
		private void ChopTree(TreeBase tree)
		{
			ItemDrop.ItemData axe = OwnAxe();
			HitData hit = new HitData();
			hit.m_damage.m_chop = ((axe != null) ? axe.GetDamage().m_chop : PunchChop);
			hit.m_toolTier = (short)((axe != null) ? axe.m_shared.m_toolTier : 0);
			hit.m_point = tree.transform.position + Vector3.up;
			hit.m_dir = (tree.transform.position - transform.position).normalized;
			hit.m_skill = Skills.SkillType.WoodCutting;
			hit.SetAttacker(humanoid);
			tree.Damage(hit);
			RaiseSkill(Skills.SkillType.WoodCutting, 1f);
			TestLog.Count("Work done", "axe swings at trees");
		}

		/// <summary>
		/// The melee weapons a viking with only a bow crafts to fight up close with, in the order it prefers them - a flint axe while
		/// the village has any flint, a stone axe while there's stone to be had, or else a club, which takes only wood
		/// </summary>
		private const string FlintAxePrefab = "AxeFlint";
		private const string StoneAxePrefab = "AxeStone";
		private const string ClubPrefab = "Club";
		private const string FlintPrefab = "Flint";

		/// <summary>
		/// Finds the weapon the viking is missing of the two every viking carries, for it to craft - for one with no bow,
		/// a crude bow, once the village has the wood for it and a first batch of arrows, which its quiver then fills
		/// with (see FindArrowWork); for one with a bow and no melee weapon, a flint axe, stone axe or club (see
		/// FlintAxePrefab) - or else the job that gets what the village is short of for it, like a hunt for leather or
		/// foraging for flint (see GatherFor). One viking arms itself at a time
		/// </summary>
		/// <returns>The job, or null when it has both, or can't get what it's missing</returns>
		private Job FindSidearmWork()
		{
			if (viking == null || OtherDoing(ArmingKey))
			{
				return(null);
			}
			if (BowAmmo() != null)
			{
				if (viking.MeleeDamage() > 0f)
				{
					return(null);
				}
				List<Container> chests = UsableChests();
				List<string> picks = new List<string>();
				if (CraftStock(FlintPrefab, chests) > 0)
				{
					picks.Add(FlintAxePrefab);
				}
				if (CraftStock(StonePrefab, chests) > 0 || FindForage(StonePrefab) != null)
				{
					picks.Add(StoneAxePrefab);
				}
				picks.Add(ClubPrefab);
				foreach (string pick in picks)
				{
					Job job = CraftWeaponWork(pick, "Crafting a melee weapon", false);
					if (job != null)
					{
						return(job);
					}
				}
				return(null);
			}
			ArrowRecipe(out int _, out int arrowWood);
			if (VillageWood() < WoodToCraft(CrudeBowPrefab) + arrowWood)
			{
				return(null);
			}
			return(CraftWeaponWork(CrudeBowPrefab, "Crafting a bow", false));
		}

		/// <summary>
		/// Finds the job crafting a weapon the viking keeps to fall back on, or puts away in a chest - or the job that
		/// gets what the village is short of for it (see GatherFor)
		/// </summary>
		/// <param name="prefab">The weapon's prefab name</param>
		/// <param name="label">What the job shows as in the debug hover text</param>
		/// <param name="store">True to put it away in a chest for the village, rather than keep it</param>
		/// <returns>The job, or null when it can't get what the weapon takes</returns>
		private Job CraftWeaponWork(string prefab, string label, bool store)
		{
			ArrowPlan plan = PlanCraft(prefab, out string missing);
			if (plan == null)
			{
				return(GatherFor(missing));
			}
			bool woodLeft = (Time.time >= noWoodUntil || CarriedWood() >= plan.Wood || FindWoodChest() != null);
			if (!woodLeft || (plan.Station != null && !ClaimObject(plan.Station.GetComponent<ZNetView>())))
			{
				return(null);
			}
			return(new WeaponJob(this, plan, prefab, label, store));
		}

		/// <summary>
		/// Counts the wood the viking carries and the village's chests hold, of every kind
		/// </summary>
		/// <returns>The wood</returns>
		private int VillageWood()
		{
			int wood = CarriedWood();
			foreach (Container chest in UsableChests())
			{
				wood += ChestWood(chest);
			}
			return(wood);
		}

		/// <summary>
		/// Reads the wood the game's recipe for an item takes
		/// </summary>
		/// <param name="prefab">The item's prefab name</param>
		/// <returns>The wood, or 0 when the game has no recipe for it</returns>
		private static int WoodToCraft(string prefab)
		{
			Recipe recipe = ((ObjectDB.instance != null) ? ObjectDB.instance.m_recipes.Find((Recipe candidate) => Crafts(candidate) && candidate.m_item.gameObject.name == prefab) : null);
			int wood = 0;
			if (recipe != null)
			{
				foreach (Piece.Requirement requirement in recipe.m_resources)
				{
					if (requirement != null && requirement.m_resItem != null && WoodItems.Contains(requirement.m_resItem.gameObject.name))
					{
						wood += CraftAmount(requirement, 1);
					}
				}
			}
			return(wood);
		}


		/// <summary>
		/// Describes a village's path for the debug hover text of its fire, while a house added to it for a player's bed
		/// has none yet - who's making it, or what it's waiting on (see FindPathWork)
		/// </summary>
		/// <param name="village">The village</param>
		/// <returns>The line, ending in a line break, or "" when no house needs a path</returns>
		public static string DescribePaths(Village village)
		{
			List<VillagePaths.House> added = VillageBeds.AddedHouses(village);
			if (added.Count == 0 || !VillagePaths.NeedsPath(village, added))
			{
				return("");
			}
			VillageLife maker = null;
			VillageLife any = null;
			foreach (VillageLife life in All)
			{
				if (life == null || life.village != village)
				{
					continue;
				}
				if (life.job is PathJob)
				{
					maker = life;
				}
				if (life.active)
				{
					any = life;
				}
			}
			string why;
			if (maker != null)
			{
				string name = VikingNames.Get(maker.humanoid);
				why = ((name.Length > 0) ? name : "a viking") + " is making it - " + maker.job.Describe();
			}
			else if (any == null)
			{
				why = "for the client that runs the village's vikings";
			}
			else if (any.FindStation(WorkbenchHash) == null)
			{
				why = "waiting for a workbench";
			}
			else if (VikingWeather.Storming())
			{
				why = "waiting out the thunderstorm";
			}
			else if (any.OwnTool(HoePrefab) == null && any.ChestWithCraft(HoePrefab) == null && any.PlanCraft(HoePrefab, out string missing) == null)
			{
				why = "no hoe, and short of " + (missing ?? "what one takes") + " to craft one";
			}
			else
			{
				why = "waiting for a viking free of other work";
			}
			if (maker == null && pathProgress.TryGetValue(village.Zdo.m_uid, out PathProgress started))
			{
				why += " - " + started.Next + "/" + started.Plan.Spots.Count + " made so far";
			}
			return("Paths: a house added for a player's bed has none yet, " + why + "\n");
		}

		/// <summary>
		/// Finds a path for the viking to make with a hoe - from a house added to the village for a player's bed to the
		/// rest of the village, when no other viking is making one and the village has a workbench, with a hoe of its own
		/// or in a chest, or what one takes to craft. A path partway made is carried on from where it was left, without
		/// waiting to plan one (see pathProgress). When the village is short of something a hoe takes, like stone, it
		/// goes out for it instead (see GatherFor)
		/// </summary>
		/// <returns>The job, or null when there's no path to make or no way to get a hoe</returns>
		private Job FindPathWork()
		{
			if (OtherDoing(PathingKey) || FindStation(WorkbenchHash) == null)
			{
				return(null);
			}
			List<VillagePaths.House> added = VillageBeds.AddedHouses(village);
			ZDOID id = village.Zdo.m_uid;
			if (added.Count == 0 || !VillagePaths.NeedsPath(village, added))
			{
				pathProgress.Remove(id);
				return(null);
			}
			if (pathProgress.TryGetValue(id, out PathProgress started) && !VillagePaths.StillNeedsPath(village, added, started.Plan.House))
			{
				pathProgress.Remove(id);
				started = null;
			}
			if (started == null && nextPathPlan.TryGetValue(id, out float next) && Time.time < next)
			{
				return(null);
			}
			ArrowPlan craft = null;
			Container hoeChest = null;
			if (OwnTool(HoePrefab) == null)
			{
				hoeChest = ChestWithCraft(HoePrefab);
				if (hoeChest == null)
				{
					craft = PlanCraft(HoePrefab, out string missing);
					if (craft == null)
					{
						return(GatherFor(missing));
					}
					bool woodLeft = (Time.time >= noWoodUntil || CarriedWood() >= craft.Wood || FindWoodChest() != null);
					if (!woodLeft)
					{
						return(null);
					}
				}
			}
			if (started == null)
			{
				nextPathPlan[id] = Time.time + PathPlanInterval;
				VillagePaths.HousePath plan = VillagePaths.PlanHousePath(village, added);
				if (plan == null)
				{
					return(null);
				}
				if (plan.Spots.Count == 0)
				{
					VillagePaths.Pathed(village, plan.House);
					return(null);
				}
				started = new PathProgress();
				started.Plan = plan;
				pathProgress[id] = started;
			}
			if (craft != null && craft.Station != null && !ClaimObject(craft.Station.GetComponent<ZNetView>()))
			{
				return(null);
			}
			return(new PathJob(this, started, hoeChest, craft));
		}

		/// <summary>
		/// Finds a tool of the viking's own in its inventory - not carried for a chest or held for show
		/// </summary>
		/// <param name="prefab">The tool's prefab name</param>
		/// <returns>The tool, or null when it has none</returns>
		private ItemDrop.ItemData OwnTool(string prefab)
		{
			return(humanoid.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => item.m_dropPrefab != null && item.m_dropPrefab.name == prefab && !VikingItems.IsCarried(item) && !VikingItems.IsProp(item)));
		}

		/// <summary>
		/// Takes a tool or weapon out of a chest the viking has reached as its own, to work or fight with
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="wanted">Which items it takes</param>
		/// <returns>The item, or null when the chest has none or the viking has no room for it</returns>
		private ItemDrop.ItemData TakeTool(Container chest, System.Predicate<ItemDrop.ItemData> wanted)
		{
			if (!OpenChest(chest))
			{
				return(null);
			}
			Inventory store = chest.GetInventory();
			ItemDrop.ItemData stored = store.GetAllItems().Find((ItemDrop.ItemData item) => !VikingItems.IsCarried(item) && wanted(item));
			Inventory inventory = humanoid.GetInventory();
			Vector2i slot = inventory.FindEmptySlot(false);
			if (stored == null || slot.x < 0 || WeightRoom(stored) < 1)
			{
				return(null);
			}
			ItemDrop.ItemData tool = stored.Clone();
			tool.m_stack = 1;
			tool.m_equipped = false;
			tool.m_gridPos = slot;
			inventory.m_inventory.Add(tool);
			inventory.Changed();
			store.RemoveItem(stored, 1);
			chest.m_closeEffects.Create(chest.transform.position, chest.transform.rotation);
			return(tool);
		}

		/// <summary>
		/// Puts a tool the viking worked with away - out of its hands, and carried for a chest, so it goes back in one
		/// </summary>
		/// <param name="prefab">The tool's prefab name</param>
		private void PutToolAway(string prefab)
		{
			ItemDrop.ItemData tool = OwnTool(prefab);
			if (tool == null)
			{
				return;
			}
			if (humanoid.IsItemEquiped(tool))
			{
				humanoid.UnequipItem(tool, false);
			}
			tool.m_customData[VikingItems.CarryKey] = CarryLoot;
			humanoid.GetInventory().Changed();
		}

		/// <summary>
		/// Finds a beehive for the viking to build - when the village has fewer than VillageHives.HivesMax, no other
		/// viking is building one, there's a queen bee it carries or can take from a chest, wood it can get, and a spot
		/// for one
		/// </summary>
		/// <returns>The job, or null when there's no hive to build</returns>
		private Job FindHiveWork()
		{
			if (OtherDoing(HiveKey) || VillageHives.Count(village) >= VillageHives.HivesMax)
			{
				return(null);
			}
			int wood = VillageHives.WoodCost();
			ItemDrop.ItemData queen = ItemOf(VillageHives.QueenBeePrefab);
			if (wood < 0 || queen == null || (CarriedLootOf(queen) <= 0 && FindQueenChest() == null))
			{
				return(null);
			}
			if (Time.time < noWoodUntil && CarriedWood() < wood && FindWoodChest() == null)
			{
				return(null);
			}
			if (!VillageHives.FindSpot(village, out Vector3 spot, out Quaternion rotation))
			{
				return(null);
			}
			return(new HiveJob(this, spot, rotation, wood));
		}

		/// <summary>
		/// Finds the nearest of the village's chests with a queen bee in it, leaving out those the viking couldn't get to
		/// </summary>
		/// <returns>The chest, or null when there's none</returns>
		private Container FindQueenChest()
		{
			Container nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (Container chest in UsableChests())
			{
				if (Avoided(chest.m_nview.GetZDO().m_uid) || QueenIn(chest) == null)
				{
					continue;
				}
				float distance = Vector3.Distance(chest.transform.position, transform.position);
				if (distance < nearestDistance)
				{
					nearest = chest;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Finds a queen bee in a chest
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>The queen bee's stack, or null when there's none</returns>
		private static ItemDrop.ItemData QueenIn(Container chest)
		{
			return(chest.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => item.m_dropPrefab != null && item.m_dropPrefab.name == VillageHives.QueenBeePrefab));
		}

		/// <summary>
		/// Takes a queen bee out of a chest the viking has reached, to carry
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>False when there was none, or it couldn't be used or carried</returns>
		private bool TakeQueen(Container chest)
		{
			if (!OpenChest(chest))
			{
				return(false);
			}
			ItemDrop.ItemData queen = QueenIn(chest);
			if (queen == null || Carry(queen, 1, CarryLoot) <= 0)
			{
				return(false);
			}
			chest.GetInventory().RemoveItem(queen, 1);
			chest.m_closeEffects.Create(chest.transform.position, chest.transform.rotation);
			return(true);
		}

		/// <summary>
		/// Uses up a queen bee the viking carries
		/// </summary>
		/// <returns>False when it carries none</returns>
		private bool UseQueen()
		{
			ItemDrop.ItemData carried = humanoid.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => IsCarriedFor(item, CarryLoot) && item.m_dropPrefab != null && item.m_dropPrefab.name == VillageHives.QueenBeePrefab);
			if (carried == null)
			{
				return(false);
			}
			UseCarried(carried, 1);
			return(true);
		}

		/// <summary>
		/// Building a beehive by day - taking a queen bee from a chest when it carries none, getting the wood, then
		/// walking to the hive's spot and hammering it together, using up the queen bee and the wood. One viking builds a
		/// hive at a time
		/// </summary>
		private class HiveJob : Job
		{
			private readonly Vector3 spot;
			private readonly Quaternion rotation;
			private readonly int wood;
			private Container chest;
			private float takeAt = -1f;
			private WoodRun run;
			private HammerWork work;
			private float nextRenew;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="where">Where the hive goes, on the ground</param>
			/// <param name="facing">Which way it faces</param>
			/// <param name="cost">The wood it takes</param>
			public HiveJob(VillageLife owner, Vector3 where, Quaternion facing, int cost) : base(owner)
			{
				spot = where;
				rotation = facing;
				wood = cost;
				work = new HammerWork(owner, RebuildSwings);
			}

			/// <summary>
			/// Marks the viking as building a hive, so no other viking starts one
			/// </summary>
			public override void Begin()
			{
				life.SetDoing(HiveKey, true);
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>What it's doing</returns>
			public override string Describe()
			{
				return("Build beehive at " + spot.ToString("F0"));
			}

			/// <summary>
			/// Gets the queen bee and the wood, then builds the hive
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's built, or it can't be - there's no queen bee, wood or room left, or it's dusk</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk())
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.SetDoing(HiveKey, true);
				}
				ItemDrop.ItemData queen = ItemOf(VillageHives.QueenBeePrefab);
				if (queen == null)
				{
					return(true);
				}
				if (life.CarriedLootOf(queen) <= 0)
				{
					return(FetchQueen(dt));
				}
				if (!life.HaveWood(wood, ref run, dt, out bool done))
				{
					return(done);
				}
				if (!work.Tick(dt, spot))
				{
					if (life.moveFailed)
					{
						life.moveFailed = false;
						return(true);
					}
					return(false);
				}
				// Someone may have built or dropped something there while it was getting the wood
				if (VillageHives.HasRoom(life.village, spot, rotation) && life.CarriedWood() >= wood && life.UseQueen())
				{
					life.UseWood(wood);
					VillageHives.Build(life.village, spot, rotation);
				}
				return(true);
			}

			/// <summary>
			/// Walks to the nearest chest with a queen bee and takes one
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True when there's no queen bee to be had, which ends the job</returns>
			private bool FetchQueen(float dt)
			{
				if (chest == null || !life.Usable(chest) || QueenIn(chest) == null)
				{
					chest = life.FindQueenChest();
					takeAt = -1f;
					if (chest == null)
					{
						return(true);
					}
				}
				if (takeAt >= 0f)
				{
					if (Time.time < takeAt)
					{
						return(false);
					}
					takeAt = -1f;
					if (!life.TakeQueen(chest))
					{
						chest = null;
					}
					return(false);
				}
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						chest = null;
					}
					return(false);
				}
				life.Face(chest.transform.position);
				VikingPoses.Interact(life.humanoid);
				chest.m_openEffects.Create(chest.transform.position, chest.transform.rotation);
				takeAt = Time.time + ReachTime;
				return(false);
			}

			/// <summary>
			/// Checks whether the wood the viking is after lies outside the village
			/// </summary>
			/// <returns>True when it does</returns>
			public override bool Outing()
			{
				return(run != null && run.Outing());
			}

			/// <summary>
			/// Lets go of the wood it was after and its mark as the one building a hive
			/// </summary>
			public override void End()
			{
				if (run != null)
				{
					run.Release();
				}
				life.SetDoing(HiveKey, false);
			}
		}

		/// <summary>
		/// Finds work that takes wood - the lowest missing piece, then the village's fire or one of its gathering fires
		/// to stack
		/// </summary>
		/// <returns>The job, or null when nothing needs it</returns>
		private Job FindWoodWork()
		{
			// A ward keeps the vikings from building in it, the same as it keeps out players it doesn't permit. The lowest
			// pieces go back first, so walls and roofs have what holds them up
			List<PieceRecord> missing = VillageRecords.Missing(village);
			missing.Sort((PieceRecord a, PieceRecord b) => a.Position.y.CompareTo(b.Position.y));
			// With no more wood to get for now, it only builds back what the wood it carries is enough for, rather than
			// starting a piece it gives up on as soon as it looks for the rest of the wood
			bool moreWood = (Time.time >= noWoodUntil || FindWoodChest() != null);
			int carried = CarriedWood();
			foreach (PieceRecord record in missing)
			{
				if ((moreWood || carried >= Mathf.Min(record.Wood, CarryMax)) && !Warded(record.Position) && ClaimWork(RecordKey(record)))
				{
					return(new RebuildJob(this, record, false));
				}
			}
			// A house razed to the ground goes back up once the rest of the village is whole, lowest pieces first - once
			// one of its pieces stands it's no longer in ruins, and the rest of it is built back with the others
			List<PieceRecord> ruins = VillageRecords.MissingInRuins(village);
			ruins.Sort((PieceRecord a, PieceRecord b) => a.Position.y.CompareTo(b.Position.y));
			foreach (PieceRecord record in ruins)
			{
				if ((moreWood || carried >= Mathf.Min(record.Wood, CarryMax)) && !Warded(record.Position) && ClaimWork(RecordKey(record)))
				{
					return(new RebuildJob(this, record, false));
				}
			}
			foreach (Fireplace fire in village.GatherFires())
			{
				if (fire != null && !IsFull(fire) && ClaimObject(fire.GetComponent<ZNetView>()))
				{
					return(new FuelJob(this, fire));
				}
			}
			return(null);
		}

		/// <summary>
		/// Picks the gathering fire the viking goes to - the one nearest its home with room for it, outside the pens, or
		/// else the one with the most room for its size (see FireRoom)
		/// </summary>
		/// <returns>The fire, or null when the village has none to gather at</returns>
		private Fireplace ChooseGatherFire()
		{
			List<Fireplace> fires = village.GatherFires().FindAll((Fireplace fire) => fire != null && !Village.FencedIn(fire.transform.position));
			if (fires.Count == 0)
			{
				return(null);
			}
			Vector3 from = (Home(out VillagePaths.House house) ? house.Center : transform.position);
			fires.Sort((Fireplace a, Fireplace b) => Utils.DistanceXZ(a.transform.position, from).CompareTo(Utils.DistanceXZ(b.transform.position, from)));
			Fireplace roomiest = null;
			float lightest = float.MaxValue;
			foreach (Fireplace fire in fires)
			{
				int gathered = Gatherers(fire);
				int room = FireRoom(fire);
				if (gathered < room)
				{
					return(fire);
				}
				float load = (float)gathered / room;
				if (load < lightest)
				{
					roomiest = fire;
					lightest = load;
				}
			}
			return(roomiest);
		}

		/// <summary>
		/// Picks a spot on a house's floor, on the navmesh at least a meter inside the walls, not on the roof and
		/// DoorClearance from its doors, so the viking isn't in the way of anyone coming in or out - or, when it finds
		/// none in twice RestFloorSamples tries, the first such spot it found however near a door, or else the middle of
		/// the house
		/// </summary>
		/// <param name="house">The house</param>
		/// <returns>The spot</returns>
		private static Vector3 FloorSpot(VillagePaths.House house)
		{
			List<Vector3> doors = Village.Doors(house);
			Vector3 nearDoor = house.Center;
			bool hasNearDoor = false;
			for (int i = 0; i < RestFloorSamples * 2; i++)
			{
				Vector2 offset = Random.insideUnitCircle * Mathf.Max(0.5f, house.HalfSize - 1.5f);
				Vector3 point = house.Center + new Vector3(offset.x, 0f, offset.y);
				Vector3 valid = point;
				if (Pathfinding.instance != null && (!NavmeshPoints.FindValidPoint(out valid, point, house.HalfSize, Pathfinding.AgentType.Humanoid) || !Village.InHouse(house, valid, -1f) || NavmeshRoofs.OnRoof(valid)))
				{
					continue;
				}
				if (!doors.Exists((Vector3 door) => Utils.DistanceXZ(door, valid) < DoorClearance))
				{
					return(valid);
				}
				if (!hasNearDoor)
				{
					nearDoor = valid;
					hasNearDoor = true;
				}
			}
			return(nearDoor);
		}

		/// <summary>
		/// Counts the other loaded vikings of the village gathered or gathering at a fire
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <returns>How many</returns>
		private int Gatherers(Fireplace fire)
		{
			int count = 0;
			foreach (VillageLife other in All)
			{
				if (other != this && other != null && other.village == village && other.job is GatherJob gather && gather.Fire() == fire)
				{
					count++;
				}
			}
			return(count);
		}

		/// <summary>
		/// Works out how many vikings a fire has room for in the ring around it, GatherSpacing apart
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <returns>How many, at least 1</returns>
		private static int FireRoom(Fireplace fire)
		{
			return(RingRoom(FireRing(fire, 0.5f)));
		}

		/// <summary>
		/// Works out how many vikings a circle around a fire has room for, GatherSpacing apart
		/// </summary>
		/// <param name="radius">The circle's radius, in meters</param>
		/// <returns>How many, at least 1</returns>
		internal static int RingRoom(float radius)
		{
			return(Mathf.Max(1, Mathf.FloorToInt(2f * Mathf.PI * radius / GatherSpacing)));
		}

		/// <summary>
		/// Widens the ring the vikings gather in around a fire when more gather than it has room for, keeping its inside
		/// and growing its area in step with how many more there are
		/// </summary>
		/// <param name="min">The ring's inside, in meters from the fire</param>
		/// <param name="max">The ring's outside</param>
		/// <param name="crowd">How many vikings gather there</param>
		/// <param name="room">How many the fire has room for (see FireRoom)</param>
		/// <returns>The ring's outside, unchanged while there's room for them all</returns>
		internal static float WidenRing(float min, float max, int crowd, int room)
		{
			if (crowd <= room)
			{
				return(max);
			}
			return(Mathf.Sqrt(min * min + (max * max - min * min) * crowd / room));
		}

		/// <summary>
		/// Narrows a ring around a fire to within its warmth, less WarmthMargin, and clear of its flames by
		/// WarmthMargin, keeping the ring at least a meter deep where the warmth leaves room and its inside never past
		/// its outside
		/// </summary>
		/// <param name="warmth">How far the fire's warmth reaches, in meters, or 0 when it has none</param>
		/// <param name="flames">How far its flames reach, in meters, or 0 when it has none</param>
		/// <param name="min">The ring's inside, in meters from the fire</param>
		/// <param name="max">The ring's outside</param>
		internal static void WarmRingWithin(float warmth, float flames, ref float min, ref float max)
		{
			if (warmth > 0f)
			{
				max = Mathf.Min(max, warmth - WarmthMargin);
			}
			min = Mathf.Min(min, max - 1f);
			min = Mathf.Max(min, flames + WarmthMargin);
			max = Mathf.Max(max, min);
		}

		/// <summary>
		/// Works out how far around to turn the next of several vikings spread out on either side of a line - none for
		/// the first, then a step one way, a step the other, two steps the first way, and so on
		/// </summary>
		/// <param name="i">Which of them it is, from 0</param>
		/// <param name="step">How far apart they stand, in degrees</param>
		/// <returns>The turn, in degrees</returns>
		internal static float Fan(int i, float step)
		{
			return(((i + 1) / 2) * step * ((i % 2 == 1) ? -1f : 1f));
		}

		/// <summary>
		/// Checks whether an item in a viking's inventory is one it carries for a purpose
		/// </summary>
		/// <param name="item">The item</param>
		/// <param name="purpose">CarryWood or CarryLoot</param>
		/// <returns>True when it carries it for that</returns>
		private static bool IsCarriedFor(ItemDrop.ItemData item, string purpose)
		{
			return(item != null && item.m_customData.TryGetValue(VikingItems.CarryKey, out string carried) && carried == purpose);
		}

		/// <summary>
		/// Gets how much wood the viking carries, of every kind
		/// </summary>
		/// <returns>The wood</returns>
		private int CarriedWood()
		{
			int wood = 0;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (IsCarriedFor(item, CarryWood))
				{
					wood += item.m_stack;
				}
			}
			return(wood);
		}

		/// <summary>
		/// Works out how much more wood of a kind the viking can carry - up to CarryMax in all, and as much as its
		/// inventory has room for
		/// </summary>
		/// <param name="wood">The kind of wood</param>
		/// <returns>How much it can take</returns>
		private int WoodRoom(ItemDrop.ItemData wood)
		{
			return(Mathf.Min(CarryMax - CarriedWood(), CarryRoom(wood, CarryWood)));
		}

		/// <summary>
		/// Checks whether the viking can carry any more wood
		/// </summary>
		/// <returns>True when it's below CarryMax and its inventory has room</returns>
		private bool CanCarryWood()
		{
			ItemDrop.ItemData plain = ItemOf("Wood");
			return(plain != null && WoodRoom(plain) > 0);
		}

		/// <summary>
		/// Adds wood the viking gathered to what it carries
		/// </summary>
		/// <param name="wood">The kind of wood, whose stack is ignored</param>
		/// <param name="amount">How much</param>
		/// <returns>How much it took, up to WoodRoom</returns>
		private int AddWood(ItemDrop.ItemData wood, int amount)
		{
			return(Carry(wood, Mathf.Min(amount, WoodRoom(wood)), CarryWood));
		}

		/// <summary>
		/// Uses up wood the viking carries, of any kind
		/// </summary>
		/// <param name="amount">How much</param>
		private void UseWood(int amount)
		{
			Inventory inventory = humanoid.GetInventory();
			foreach (ItemDrop.ItemData item in inventory.m_inventory.FindAll((ItemDrop.ItemData carried) => IsCarriedFor(carried, CarryWood)))
			{
				if (amount <= 0)
				{
					break;
				}
				int used = Mathf.Min(amount, item.m_stack);
				inventory.RemoveItem(item, used);
				amount -= used;
			}
		}

		/// <summary>
		/// Finds the loot the viking carries for a chest
		/// </summary>
		/// <returns>The loot, or null when it carries none</returns>
		private ItemDrop.ItemData CarriedLoot()
		{
			return(humanoid.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => IsCarriedFor(item, CarryLoot)));
		}

		/// <summary>
		/// Works out how many of an item the viking's inventory has room for, carried for a purpose - on its carried
		/// stacks of the item and in its empty slots, never on its own items
		/// </summary>
		/// <param name="template">The item</param>
		/// <param name="purpose">CarryWood or CarryLoot</param>
		/// <returns>How many it has room for</returns>
		private int CarryRoom(ItemDrop.ItemData template, string purpose)
		{
			Inventory inventory = humanoid.GetInventory();
			int maxStack = Mathf.Max(1, template.m_shared.m_maxStackSize);
			int room = (inventory.m_width * inventory.m_height - inventory.m_inventory.Count) * maxStack;
			foreach (ItemDrop.ItemData item in inventory.GetAllItems())
			{
				if (StacksWith(item, template, purpose))
				{
					room += Mathf.Max(0, maxStack - item.m_stack);
				}
			}
			return(Mathf.Min(room, WeightRoom(template)));
		}

		/// <summary>
		/// Works out how many of an item the viking can carry before it's as laden as a player can be - its whole
		/// inventory counts, what it wears included, the way a player's does
		/// </summary>
		/// <param name="template">The item</param>
		/// <returns>How many</returns>
		private int WeightRoom(ItemDrop.ItemData template)
		{
			float each = template.m_shared.m_weight;
			if (each <= 0f)
			{
				return(int.MaxValue);
			}
			float left = body.GetMaxCarryWeight() - humanoid.GetInventory().GetTotalWeight();
			return(Mathf.Max(0, Mathf.FloorToInt(left / each)));
		}

		/// <summary>
		/// Checks whether more of an item can go on a stack the viking carries
		/// </summary>
		/// <param name="item">The stack in its inventory</param>
		/// <param name="template">The item</param>
		/// <param name="purpose">CarryWood or CarryLoot</param>
		/// <returns>True when it's a carried stack of the same item</returns>
		private static bool StacksWith(ItemDrop.ItemData item, ItemDrop.ItemData template, string purpose)
		{
			return(IsCarriedFor(item, purpose) && item.m_shared.m_name == template.m_shared.m_name && item.m_quality == template.m_quality && item.m_variant == template.m_variant && item.m_worldLevel == template.m_worldLevel);
		}

		/// <summary>
		/// Puts items in the viking's inventory as carried for a purpose - onto its carried stacks of the item first,
		/// then into empty slots, never onto its own items
		/// </summary>
		/// <param name="template">The item, whose stack is ignored</param>
		/// <param name="amount">How many</param>
		/// <param name="purpose">CarryWood or CarryLoot</param>
		/// <returns>How many it had room for</returns>
		private int Carry(ItemDrop.ItemData template, int amount, string purpose)
		{
			if (amount <= 0 || template == null || template.m_dropPrefab == null)
			{
				return(0);
			}
			Inventory inventory = humanoid.GetInventory();
			int maxStack = Mathf.Max(1, template.m_shared.m_maxStackSize);
			amount = Mathf.Min(amount, WeightRoom(template));
			int placed = 0;
			foreach (ItemDrop.ItemData item in inventory.GetAllItems())
			{
				if (placed >= amount)
				{
					break;
				}
				if (StacksWith(item, template, purpose) && item.m_stack < maxStack)
				{
					int added = Mathf.Min(amount - placed, maxStack - item.m_stack);
					item.m_stack += added;
					placed += added;
				}
			}
			while (placed < amount)
			{
				Vector2i slot = inventory.FindEmptySlot(false);
				if (slot.x < 0)
				{
					break;
				}
				ItemDrop.ItemData copy = template.Clone();
				copy.m_stack = Mathf.Min(amount - placed, maxStack);
				copy.m_gridPos = slot;
				copy.m_equipped = false;
				copy.m_customData[VikingItems.CarryKey] = purpose;
				inventory.m_inventory.Add(copy);
				placed += copy.m_stack;
			}
			if (placed > 0)
			{
				inventory.Changed();
			}
			return(placed);
		}

		/// <summary>
		/// Makes an item of a prefab to carry, the way the game adds one to an inventory
		/// </summary>
		/// <param name="prefabName">The item's prefab</param>
		/// <returns>The item, or null when there's no such prefab</returns>
		private static ItemDrop.ItemData ItemOf(string prefabName)
		{
			GameObject prefab = ((ObjectDB.instance != null) ? ObjectDB.instance.GetItemPrefab(prefabName) : null);
			ItemDrop drop = ((prefab != null) ? prefab.GetComponent<ItemDrop>() : null);
			if (drop == null)
			{
				return(null);
			}
			ItemDrop.ItemData item = drop.m_itemData.Clone();
			item.m_dropPrefab = prefab;
			item.m_stack = 1;
			item.m_worldLevel = (byte)Game.m_worldLevel;
			return(item);
		}

		/// <summary>
		/// Gets the item lying on the ground as an item to carry, with its prefab set
		/// </summary>
		/// <param name="drop">The item on the ground</param>
		/// <returns>The item, or null when its prefab is unknown</returns>
		private static ItemDrop.ItemData ItemOf(ItemDrop drop)
		{
			ItemDrop.ItemData item = drop.m_itemData;
			if (item.m_dropPrefab == null && ObjectDB.instance != null)
			{
				item.m_dropPrefab = ObjectDB.instance.GetItemPrefab(Utils.GetPrefabName(drop.gameObject));
			}
			return((item.m_dropPrefab != null) ? item : null);
		}

		/// <summary>
		/// Lists the village's chests the viking may use - public ones in the village, not on a cart or ship, not a
		/// tombstone, not open, and not a chest a player built inside a ward - leaving out those it couldn't get to lately
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
		/// Checks whether a chest is one of a village's, which its vikings use - a public one in the village or its
		/// houses, not on a cart or ship, not a tombstone, not one of the game's treasure chests, and not a chest a
		/// player built inside a ward, which keeps the vikings out of it but not out of the village's own
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="chest">The chest</param>
		/// <returns>True when it is</returns>
		private static bool IsVillageChest(Village village, Container chest)
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
			if (!village.Contains(position) && !village.InHouse(position, 1f))
			{
				return(false);
			}
			bool playerBuilt = (chest.m_nview.GetZDO().GetLong(ZDOVars.s_creator, 0L) != 0L);
			return(!(playerBuilt && Warded(position)));
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
		/// Leaves a chest, loot or piece the viking couldn't get to alone for a while, and notes the failed try at
		/// anything but a chest for the viking itself and for the village, whose vikings all leave it alone for longer
		/// once enough of them have failed (see VillageGiveUps) - unless the viking stands off its navmesh (see
		/// OffItsNavmesh)
		/// </summary>
		/// <param name="view">The chest, loot or piece</param>
		/// <param name="seconds">How long it's left alone</param>
		private void Avoid(ZNetView view, float seconds = AvoidTime)
		{
			Avoid(avoided, view, seconds);
		}

		/// <summary>
		/// Leaves a piece the viking couldn't get to to repair or clear snow off to the others for RepairAvoidTime - for
		/// that work alone (see HammerAvoided), so a chest it gave up on is still one it uses - noting the failed try for
		/// the village the way Avoid does
		/// </summary>
		/// <param name="view">The piece</param>
		private void AvoidHammerWork(ZNetView view)
		{
			Avoid(hammerAvoided, view, RepairAvoidTime);
		}

		/// <summary>
		/// Notes in a list of the things the viking leaves alone that it couldn't get to a chest, loot or piece (see
		/// Avoid)
		/// </summary>
		/// <param name="list">The list, by ZDO, with until when each is left alone</param>
		/// <param name="view">The chest, loot or piece</param>
		/// <param name="seconds">How long it's left alone</param>
		private void Avoid(Dictionary<ZDOID, float> list, ZNetView view, float seconds)
		{
			if (view == null || !view.IsValid())
			{
				return;
			}
			List<ZDOID> expired = new List<ZDOID>();
			foreach (KeyValuePair<ZDOID, float> entry in list)
			{
				if (Time.time >= entry.Value)
				{
					expired.Add(entry.Key);
				}
			}
			foreach (ZDOID id in expired)
			{
				list.Remove(id);
			}
			ZDOID thing = view.GetZDO().m_uid;
			list[thing] = Time.time + seconds;
			string name = Utils.GetPrefabName(view.gameObject);
			bool chest = (view.GetComponent<Container>() != null);
			// With no job, it's looking for one - like FindLootWork leaving loot it can't reach for the next nearest
			TestLog.Count("Couldn't reach, by job", ((job != null) ? LogJobName() : "choosing a job"));
			// A chest is counted with its place, since a village has several and the one out of reach is worth knowing
			TestLog.Count("Couldn't reach, by thing", (chest ? name + " at " + view.transform.position.ToString("F0") : name));
			if (Time.time >= nextGiveUpPrune)
			{
				nextGiveUpPrune = Time.time + GiveUpPruneInterval;
				giveUps.Prune(Time.time);
			}
			// A chest is never given up on by the whole village - without it the vikings would have nowhere to put away
			// what they find or eat from - only by the viking that couldn't get to it, for a while
			if (chest)
			{
				Container store = view.GetComponent<Container>();
				// A chest it got to but found nothing in is still within reach, and a viking off its navmesh fails to get to
				// everything, so neither leaves the chest's food out of the village's (see ChestFoodWithinReach)
				if (!AtChest(store) && !OffItsNavmesh())
				{
					NoteChestMissed(store);
				}
				LogChestMiss(store);
				return;
			}
			// A viking stranded where it can't walk from - up on a roof, or anywhere else off its navmesh - fails to get
			// to everything, so its try says nothing about the thing and only it leaves it alone
			if (OffItsNavmesh())
			{
				TestLog.Count("Tries not counted for the village, the viking off the navmesh", name);
				return;
			}
			ownGiveUps.Prune(Time.time);
			float own = ownGiveUps.Fail(thing, nview.GetZDO().m_uid, Time.time);
			if (own > 0f)
			{
				TestLog.Count("Given up on by a viking alone", name);
				if (TestLog.On || Player.m_debugMode)
				{
					Plugin.Log.LogInfo("Given up: " + LogName() + " (" + LogJobName() + ") leaves " + name + " at " + view.transform.position.ToString("F1") + " alone for " + (own / 60f).ToString("F0") + " minutes, after " + VillageGiveUps.Tries + " tries of its own failed to reach it");
				}
			}
			float shared = giveUps.Fail(thing, nview.GetZDO().m_uid, Time.time);
			if (shared <= 0f)
			{
				return;
			}
			TestLog.Count("Given up on by every viking", name);
			if (TestLog.On || Player.m_debugMode)
			{
				Plugin.Log.LogInfo("Given up: the vikings leave " + name + " at " + view.transform.position.ToString("F1") + " alone for " + (shared / 60f).ToString("F0") + " minutes, after " + VillageGiveUps.Tries + " tries by " + VillageGiveUps.Vikings + " or more of them failed to reach it - the last by " + LogName() + " (" + LogJobName() + ")");
			}
		}

		/// <summary>
		/// How near the viking its navmesh has to be for it to count as standing on it, in meters
		/// </summary>
		private const float OwnNavmeshReach = 1f;

		/// <summary>
		/// Checks whether the viking stands off its navmesh - on a roof, or with none within OwnNavmeshReach - where it
		/// can't walk anywhere from
		/// </summary>
		/// <returns>True when it does</returns>
		private bool OffItsNavmesh()
		{
			if (NavmeshRoofs.OnRoof(transform.position))
			{
				return(true);
			}
			return(Pathfinding.instance != null && !NavmeshPoints.FindValidPoint(out Vector3 _, transform.position, OwnNavmeshReach, Pathfinding.AgentType.Humanoid));
		}

		/// <summary>
		/// Checks whether the viking can get within reach of something lying on the ground - the nearest point it can
		/// stand at is near enough, and it has a whole way there, or a way through a door, which can't be told until
		/// it's walked
		/// </summary>
		/// <remarks>
		/// A thing tucked against a rock or a wall can be further from where the viking can stand than it reaches, so
		/// walking at it would only press it against what's in the way
		/// </remarks>
		/// <param name="point">Where it lies</param>
		/// <param name="stand">Where the viking can stand to reach it</param>
		/// <returns>False when it can't get within reach</returns>
		private bool CanReach(Vector3 point, out Vector3 stand)
		{
			return(CanReach(point, out stand, out string _, out bool _));
		}

		/// <summary>
		/// Checks whether the viking can get within reach of something lying on the ground, saying why not when it can't
		/// (see CanReach)
		/// </summary>
		/// <param name="point">Where it lies</param>
		/// <param name="stand">Where the viking can stand to reach it</param>
		/// <param name="why">Why it can't, for the log, or null when it can</param>
		/// <param name="noPath">True when there's a spot near enough to stand at, and only the whole path there is
		/// missing</param>
		/// <returns>False when it can't get within reach</returns>
		private bool CanReach(Vector3 point, out Vector3 stand, out string why, out bool noPath)
		{
			stand = point;
			why = null;
			noPath = false;
			if (Pathfinding.instance == null)
			{
				return(true);
			}
			if (!NavmeshPoints.FindValidPoint(out stand, point, PickReach, Pathfinding.AgentType.Humanoid))
			{
				why = "no navmesh within " + PickReach.ToString("F1") + "m of it";
				return(false);
			}
			if (Utils.DistanceXZ(stand, point) > PickReach)
			{
				why = "the nearest navmesh, at " + stand.ToString("F1") + ", is " + Utils.DistanceXZ(stand, point).ToString("F1") + "m from it";
				return(false);
			}
			if (VikingDoors.WayThroughDoor(humanoid, stand))
			{
				return(true);
			}
			if (!Pathfinding.instance.HavePath(transform.position, stand, Pathfinding.AgentType.Humanoid))
			{
				bool onMesh = NavmeshPoints.FindValidPoint(out Vector3 from, transform.position, 1f, Pathfinding.AgentType.Humanoid);
				why = "no whole path from the viking to " + stand.ToString("F1") + (onMesh ? "" : " - the viking isn't on the navmesh itself");
				noPath = true;
				return(false);
			}
			return(true);
		}

		/// <summary>
		/// Notes in the log, in debug mode, that the viking leaves loot alone for AvoidTime and why
		/// </summary>
		/// <param name="view">The loot</param>
		/// <param name="why">Why</param>
		private void DebugAvoid(ZNetView view, string why)
		{
			if (!Player.m_debugMode || view == null || !view.IsValid())
			{
				return;
			}
			Plugin.Log.LogInfo("Loot: " + TestLog.Name(humanoid, "F1") + " (" + ((job != null) ? job.Describe() : "no job") + ") leaves " + Utils.GetPrefabName(view.gameObject) + " at " + view.transform.position.ToString("F1") + " alone for " + AvoidTime.ToString("F0") + "s - " + why);
		}

		/// <summary>
		/// Checks whether the viking's job takes it into pens - foraging, petting a boar, slaughtering one, putting loot
		/// away, feeding a fire that stands in one, or getting wood lying in one, the only things it goes into one for
		/// </summary>
		/// <returns>True when it does</returns>
		private bool GoesIntoPens()
		{
			return(job is ForageJob || job is PetJob || job is LootJob || job is FuelJob || (job is HuntJob hunt && hunt.Slaughters()) || (job != null && inPenForWood));
		}

		/// <summary>
		/// Checks whether something lies in a pen, which the vikings keep out of - remembering the answer for
		/// PenCheckTime, since what lies on the ground doesn't move
		/// </summary>
		/// <param name="id">Its ZDO</param>
		/// <param name="point">Where it lies</param>
		/// <returns>True when it's in a pen</returns>
		private static bool InPen(ZDOID id, Vector3 point)
		{
			if (penChecks.TryGetValue(id, out KeyValuePair<bool, float> known) && Time.time < known.Value)
			{
				return(known.Key);
			}
			if (penChecks.Count > PenChecksMax)
			{
				penChecks.Clear();
			}
			bool inPen = Village.FencedIn(point);
			penChecks[id] = new KeyValuePair<bool, float>(inPen, Time.time + PenCheckTime);
			return(inPen);
		}

		/// <summary>
		/// Checks whether the viking is leaving a chest or loot alone after it couldn't get to it or gave up on it after
		/// failing again and again, or every viking is after they all gave up on it (see VillageGiveUps)
		/// </summary>
		/// <param name="id">The chest's or loot's ZDO</param>
		/// <returns>True while it is</returns>
		private bool Avoided(ZDOID id)
		{
			return((avoided.TryGetValue(id, out float until) && Time.time < until) || ownGiveUps.GivenUp(id, Time.time) || giveUps.GivenUp(id, Time.time));
		}

		/// <summary>
		/// Checks whether the viking is leaving a piece alone for repairing and clearing snow off, after it couldn't get
		/// to it for that work or it couldn't get to it at all (see Avoided)
		/// </summary>
		/// <param name="id">The piece's ZDO</param>
		/// <returns>True while it is</returns>
		private bool HammerAvoided(ZDOID id)
		{
			return((hammerAvoided.TryGetValue(id, out float until) && Time.time < until) || Avoided(id));
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
		/// Notes on the viking whether it's out of its village, heading out for something outside it or on an outing
		/// its job has it on, for OutingTime seconds
		/// </summary>
		/// <param name="heading">True when it has just picked something outside the village to go to</param>
		private void UpdateOuting(bool heading)
		{
			if (nview == null || !nview.IsValid() || !nview.IsOwner() || village == null)
			{
				return;
			}
			bool outing = (heading || !village.Contains(transform.position) || (job != null && job.Outing()));
			nview.GetZDO().Set(OutingKey, (outing ? NetTime.Now() + NetTime.Ticks(OutingTime) : 0L));
		}

		/// <summary>
		/// Checks whether the viking may leave the village for wood, food or loot - always in a village of
		/// HomeGuardFrom vikings or fewer, and otherwise only while another viking is at home, in the village and not
		/// heading out, on any client
		/// </summary>
		/// <returns>True when it may</returns>
		private bool MayLeave()
		{
			List<Character> vikings = village.Vikings();
			if (vikings.Count <= HomeGuardFrom)
			{
				return(true);
			}
			long now = NetTime.Now();
			foreach (Character other in vikings)
			{
				if (other != humanoid && other.m_nview != null && other.m_nview.IsValid() && village.Contains(other.transform.position) && other.m_nview.GetZDO().GetLong(OutingKey) <= now)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Checks whether as many of the village's vikings as may are already doing a job only a few do at once, on any
		/// client - one for every so many of the village's vikings (see VikingsPerJob), and always at least one
		/// </summary>
		/// <param name="key">The job's mark - StockingKey, ForagingKey, HuntingKey or PettingKey</param>
		/// <returns>True when no more may</returns>
		private bool OtherDoing(int key)
		{
			List<Character> vikings = village.Vikings();
			int perJob = (VikingsPerJob.TryGetValue(key, out int per) ? per : 1);
			int allowed = AllowedDoing(vikings.Count, perJob);
			long now = NetTime.Now();
			int doing = 0;
			foreach (Character other in vikings)
			{
				if (other != humanoid && other.m_nview != null && other.m_nview.IsValid() && other.m_nview.GetZDO().GetLong(key) > now)
				{
					doing++;
				}
			}
			return(doing >= allowed);
		}

		/// <summary>
		/// Works out how many of a village's vikings may do a job only a few do at once - one for every perJob of them,
		/// rounded up, and always at least one
		/// </summary>
		/// <param name="vikings">How many vikings the village has</param>
		/// <param name="perJob">How many vikings there are for each one doing the job</param>
		/// <returns>How many may do it at once</returns>
		internal static int AllowedDoing(int vikings, int perJob)
		{
			return(Mathf.Max(1, Mathf.CeilToInt(vikings / (float)perJob)));
		}

		/// <summary>
		/// Notes on the viking that it's doing a job only one viking does at a time for the next ClaimTime seconds, or
		/// that it's done
		/// </summary>
		/// <param name="key">The job's mark - StockingKey, ForagingKey, HuntingKey or PettingKey</param>
		/// <param name="doing">True while it's doing it</param>
		private void SetDoing(int key, bool doing)
		{
			if (nview != null && nview.IsValid() && nview.IsOwner())
			{
				nview.GetZDO().Set(key, (doing ? NetTime.Now() + NetTime.Ticks(ClaimTime) : 0L));
			}
		}

		/// <summary>
		/// Finds the chest to stock with wood among the village's chests the viking may use with room for more - one with
		/// an unfinished stack of wood to top up first, so the wood fills a stack before it's spread over new ones (see
		/// NearestStacking)
		/// </summary>
		/// <returns>The chest, or null when none has room</returns>
		private Container FindStockChest()
		{
			ItemDrop.ItemData plain = ItemOf("Wood");
			if (plain == null)
			{
				return(null);
			}
			return(NearestStacking(UsableChests().FindAll((Container chest) => ChestRoom(chest, plain, null) > 0), plain, CarryMax));
		}

		/// <summary>
		/// Puts the wood the viking carries in a chest it has reached, as much as the chest may hold
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>How much it put in</returns>
		private int PutWood(Container chest)
		{
			if (!OpenChest(chest))
			{
				return(0);
			}
			Inventory store = chest.GetInventory();
			Inventory inventory = humanoid.GetInventory();
			int put = 0;
			foreach (ItemDrop.ItemData carried in inventory.m_inventory.FindAll((ItemDrop.ItemData held) => IsCarriedFor(held, CarryWood)))
			{
				int amount = Mathf.Min(carried.m_stack, ChestRoom(chest, carried, null));
				if (amount <= 0)
				{
					continue;
				}
				ItemDrop.ItemData item = carried.Clone();
				item.m_customData.Remove(VikingItems.CarryKey);
				item.m_equipped = false;
				item.m_stack = amount;
				if (!store.AddItem(item))
				{
					break;
				}
				inventory.RemoveItem(carried, amount);
				put += amount;
			}
			if (put > 0)
			{
				chest.m_closeEffects.Create(chest.transform.position, chest.transform.rotation);
			}
			return(put);
		}

		/// <summary>
		/// Finds the nearest of the village's chests the viking may use that has wood in it - one with an unfinished stack
		/// of wood first, so the wood left stays in full stacks
		/// </summary>
		/// <returns>The chest, or null when none has</returns>
		private Container FindWoodChest()
		{
			List<Container> stocked = UsableChests().FindAll((Container chest) => ChestWood(chest) > 0);
			Container unfinished = Nearest(stocked.FindAll((Container chest) => chest.GetInventory().GetAllItems().Exists((ItemDrop.ItemData stored) => IsWoodItem(stored) && stored.m_stack < stored.m_shared.m_maxStackSize)));
			return((unfinished != null) ? unfinished : Nearest(stocked));
		}

		/// <summary>
		/// Finds the chest nearest the viking
		/// </summary>
		/// <param name="chests">The chests</param>
		/// <returns>The nearest, or null when there are none</returns>
		private Container Nearest(List<Container> chests)
		{
			Container nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (Container chest in chests)
			{
				float distance = Vector3.Distance(chest.transform.position, transform.position);
				if (distance < nearestDistance)
				{
					nearest = chest;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Checks whether an item in a chest is wood
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it is</returns>
		private static bool IsWoodItem(ItemDrop.ItemData item)
		{
			return(item != null && item.m_dropPrefab != null && WoodItems.Contains(item.m_dropPrefab.name));
		}

		/// <summary>
		/// Counts the wood in a chest
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>The wood, of every kind</returns>
		private static int ChestWood(Container chest)
		{
			int wood = 0;
			foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
			{
				if (IsWoodItem(item))
				{
					wood += item.m_stack;
				}
			}
			return(wood);
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
		/// Takes wood from a chest the viking has reached, as much as it needs and can carry, from its smallest stack first
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="wanted">How much it needs</param>
		/// <returns>How much it took</returns>
		private int TakeWood(Container chest, int wanted)
		{
			if (wanted <= 0 || !OpenChest(chest))
			{
				return(0);
			}
			Inventory store = chest.GetInventory();
			int taken = 0;
			// The smallest stack is used up first, so the wood left stays in full stacks
			List<ItemDrop.ItemData> stacks = store.m_inventory.FindAll((ItemDrop.ItemData stored) => IsWoodItem(stored));
			stacks.Sort((ItemDrop.ItemData a, ItemDrop.ItemData b) => a.m_stack.CompareTo(b.m_stack));
			foreach (ItemDrop.ItemData item in stacks)
			{
				if (taken >= wanted)
				{
					break;
				}
				int took = AddWood(item, Mathf.Min(wanted - taken, item.m_stack));
				if (took <= 0)
				{
					break;
				}
				store.RemoveItem(item, took);
				taken += took;
			}
			if (taken > 0)
			{
				chest.m_closeEffects.Create(chest.transform.position, chest.transform.rotation);
			}
			return(taken);
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

		/// <summary>
		/// Finds loot work for the viking - putting away the loot it carries, or else picking up the nearest loot lying
		/// around the village that it wants
		/// </summary>
		/// <param name="fight">True for clearing up after a fight, which goes on through dusk and night</param>
		/// <returns>The job, or null when there's no loot work</returns>
		private Job FindLootWork(bool fight)
		{
			bool carrying = (CarriedLoot() != null);
			if (carrying)
			{
				// A viking holding on to something it can't drop outside yet doesn't pick up more
				return((Time.time >= dumpAgainAt) ? new LootJob(this, null, fight) : null);
			}
			dumpNowhereSince = -1f;
			ZNetView loot = FindLoot();
			if (loot != null && ClaimWork(loot.GetZDO().m_uid))
			{
				UpdateOuting(!village.Contains(loot.transform.position));
				return(new LootJob(this, loot, fight));
			}
			return(null);
		}

		/// <summary>
		/// Notes the enemy the viking fights now, and where and when the one it fought before died, when it did - its
		/// corpse gone or dead - so it can pick up what that drops (see FindKillLoot)
		/// </summary>
		/// <param name="target">The enemy it fights now, or null</param>
		private void NoteFought(Character target)
		{
			if (!ReferenceEquals(fought, null) && !ReferenceEquals(fought, target) && (fought == null || fought.IsDead()))
			{
				killSpot = foughtAt;
				killAt = Time.time;
			}
			fought = target;
			if (target != null)
			{
				foughtAt = target.transform.position;
			}
		}

		/// <summary>
		/// Checks whether the viking picks up what an enemy it fought dropped - within KillLootTime of the kill, while
		/// it isn't badly hurt or held back by its load, and with nothing to do it wouldn't drop for loot after a fight
		/// (see YieldsToLoot) - the night watch included, which goes back to its watch after
		/// </summary>
		/// <returns>True when it does</returns>
		private bool PicksUpKillLoot()
		{
			if (killAt < 0f || Time.time - killAt >= KillLootTime || Wounded() || body.IsEncumbered())
			{
				return(false);
			}
			return(job == null || job is WatchJob || YieldsToLoot());
		}

		/// <summary>
		/// Finds what an enemy the viking fought dropped - the nearest loot within KillLootRadius of where it died, however
		/// far from the village, once there's no danger there - to pick up and put away with the loot near it
		/// </summary>
		/// <returns>The job, or null when there's none, or danger near</returns>
		private Job FindKillLoot()
		{
			List<Container> chests = UsableChests();
			if (chests.Count == 0 || DangerNear(killSpot))
			{
				return(null);
			}
			ZNetView best = null;
			float bestDistance = float.MaxValue;
			foreach (ItemDrop drop in LoadedDrops())
			{
				ZNetView view = ((drop != null) ? drop.m_nview : null);
				if (view == null || !IsLoot(view) || Utils.DistanceXZ(view.transform.position, killSpot) > KillLootRadius)
				{
					continue;
				}
				ZDOID id = view.GetZDO().m_uid;
				if (Avoided(id) || (workClaims.TryGetValue(id, out VillageLife holder) && holder != this && holder != null && holder.job != null))
				{
					continue;
				}
				if (CarryRoom(drop.m_itemData, CarryLoot) <= 0 || !WantsLoot(drop.m_itemData, chests, true))
				{
					continue;
				}
				float distance = Vector3.Distance(view.transform.position, transform.position);
				if (distance < bestDistance)
				{
					best = view;
					bestDistance = distance;
				}
			}
			if (best == null || !ClaimWork(best.GetZDO().m_uid))
			{
				return(null);
			}
			UpdateOuting(!village.Contains(best.transform.position));
			return(new LootJob(this, best, true, true));
		}

		/// <summary>
		/// Checks whether the viking fought within LootRushTime, so it clears up the loot before going back to what it
		/// was doing
		/// </summary>
		/// <returns>True when it did</returns>
		private bool AfterFight()
		{
			return(Time.time - lastFightAt < LootRushTime);
		}

		/// <summary>
		/// Checks whether the viking drops its job to clear up loot after a fight - standing about, strolling, petting a
		/// boar, talking, gathering at the fire or resting for the night, and the work loot comes ahead of (repairing,
		/// rebuilding, keeping the fire stacked, stocking the chests with wood, crafting arrows and repairing its gear),
		/// which it picks up again after. Not keeping watch, cooking, eating or resting while wounded, or going out to
		/// hunt or forage
		/// </summary>
		/// <remarks>
		/// A fight interrupts the viking's job, and the loot it leaves is still coming to rest when the viking picks the
		/// next one, so the work it picks over the loot gives way to it once it's there
		/// </remarks>
		/// <returns>True when it does</returns>
		private bool YieldsToLoot()
		{
			if (Wounded() || IsWatch())
			{
				return(false);
			}
			if (job is RepairJob || job is RebuildJob || job is FuelJob || job is StockJob || job is ArrowJob || job is GearJob)
			{
				return(true);
			}
			return(job is IdleJob || job is StrollJob || job is PetJob || job is TalkJob || job is RestJob || (job is GatherJob gather && !gather.Heals() && !gather.Warms()));
		}

		/// <summary>
		/// Finds a corpse of the viking's village for it to mourn, when it would drop what it's doing for that (see
		/// YieldsToMourning) - the nearest within MournRange that stays dead and that it hasn't mourned yet, with fewer
		/// than MournersMax mourning it and no enemy within MournClear of it
		/// </summary>
		/// <returns>The job, or null when there's none</returns>
		private Job FindMourning()
		{
			if (!YieldsToMourning())
			{
				return(null);
			}
			Ragdoll corpse = VillageFallen.NearestCorpse(village, transform.position, MournRange, (Ragdoll candidate) => mourned.Contains(candidate.m_nview.GetZDO().m_uid) || Mourners(candidate) >= MournersMax);
			if (corpse == null || Village.FindEnemyNear(corpse.GetAverageBodyPosition(), MournClear) != null)
			{
				return(null);
			}
			return(new MournJob(this, corpse));
		}

		/// <summary>
		/// Checks whether the viking drops its job to mourn one of its village - anything but mourning already, hunting,
		/// cooking or lightening its load, and never while it keeps watch, lies in bed or rests with its senses dulled
		/// </summary>
		/// <returns>True when it does</returns>
		private bool YieldsToMourning()
		{
			if (IsWatch() || inBed || dulled || (job != null && job.Resting()) || VillageRaids.On(village))
			{
				return(false);
			}
			return(!(job is MournJob || job is HuntJob || job is CookJob || job is UnburdenJob));
		}

		/// <summary>
		/// Counts the vikings on this client mourning a corpse
		/// </summary>
		/// <param name="corpse">The corpse</param>
		/// <returns>How many are</returns>
		private static int Mourners(Ragdoll corpse)
		{
			int count = 0;
			foreach (VillageLife life in All)
			{
				if (life.job is MournJob mourning && mourning.Corpse == corpse)
				{
					count++;
				}
			}
			return(count);
		}

		/// <summary>
		/// Finds the nearest loot lying around the village that no other viking is after and that the viking wants -
		/// one of the village's chests has room for it, or it's something the vikings don't use to drop outside the
		/// village (see WantsLoot) - only in the village while the viking may not leave it (see MayLeave)
		/// </summary>
		/// <returns>The loot, or null when there's none</returns>
		private ZNetView FindLoot()
		{
			List<Container> chests = UsableChests();
			if (chests.Count == 0)
			{
				return(null);
			}
			bool mayLeave = MayLeave();
			float reach = (mayLeave ? LootReach : 0f);
			List<KeyValuePair<float, ZNetView>> found = new List<KeyValuePair<float, ZNetView>>();
			foreach (ItemDrop drop in LoadedDrops())
			{
				ZNetView view = ((drop != null) ? drop.m_nview : null);
				if (view == null || !IsLoot(view) || village.DistanceOutside(view.transform.position) > reach)
				{
					continue;
				}
				ZDOID id = view.GetZDO().m_uid;
				if (Avoided(id))
				{
					continue;
				}
				if (workClaims.TryGetValue(id, out VillageLife holder) && holder != this && holder != null && holder.job != null)
				{
					continue;
				}
				ItemDrop.ItemData item = drop.m_itemData;
				if (CarryRoom(item, CarryLoot) > 0 && WantsLoot(item, chests, mayLeave))
				{
					found.Add(new KeyValuePair<float, ZNetView>(Vector3.Distance(view.transform.position, transform.position), view));
				}
			}
			found.Sort((KeyValuePair<float, ZNetView> a, KeyValuePair<float, ZNetView> b) => a.Key.CompareTo(b.Key));
			// Loot it can't get within reach of is left alone for a while, and the next nearest tried - except in a pen,
			// which has no way in but over its fence, which the viking jumps. Loot lying up on a pen's fence is told by the
			// ground it's reached from, which lies in the pen, since its own spot is above the fence, and loot at the far
			// side of a pen too big to see as fenced in by the way there (see PennedByPaths)
			int tries = 0;
			foreach (KeyValuePair<float, ZNetView> entry in found)
			{
				// Loot out of the village with a monster near it is left for now
				if (!village.Contains(entry.Value.transform.position) && DangerNear(entry.Value.transform.position))
				{
					continue;
				}
				if (InPen(entry.Value.GetZDO().m_uid, entry.Value.transform.position) || CanReach(entry.Value.transform.position, out Vector3 stand, out string why, out bool noPath) || (noPath && (Village.FencedIn(stand) || PennedByPaths(stand))))
				{
					return(entry.Value);
				}
				DebugAvoid(entry.Value, why);
				Avoid(entry.Value);
				if (++tries >= ReachTries)
				{
					break;
				}
			}
			return(null);
		}

		/// <summary>
		/// Checks whether an object is loot the viking puts away - an item at rest that has lain on the ground for
		/// DropSettle, or LootSettle for one a player dropped, so what a player just put down is left a while, and not a
		/// fish, ammo, which
		/// the viking would shoot, food set out as a piece, or one a viking dropped outside the village because the
		/// chests hold enough of it
		/// </summary>
		/// <param name="view">The object</param>
		/// <returns>True when it is</returns>
		private static bool IsLoot(ZNetView view)
		{
			if (!view.IsValid() || view.GetZDO().GetBool(DiscardKey))
			{
				return(false);
			}
			ItemDrop drop = view.GetComponent<ItemDrop>();
			if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null || drop.IsPiece() || view.GetComponent<Fish>() != null)
			{
				return(false);
			}
			ItemDrop.ItemData.ItemType type = drop.m_itemData.m_shared.m_itemType;
			if (type == ItemDrop.ItemData.ItemType.Ammo || type == ItemDrop.ItemData.ItemType.AmmoNonEquipable)
			{
				return(false);
			}
			// What's still flying or rolling isn't loot until it comes to rest, so where it can be reached from is judged
			// where it ends up
			Rigidbody body = drop.m_body;
			if (body != null && !body.isKinematic && body.linearVelocity.sqrMagnitude > LootRestSpeed * LootRestSpeed)
			{
				return(false);
			}
			// A player's drop no longer picks itself up, which tells it from what drops from a kill
			return(drop.GetTimeSinceSpawned() >= (drop.m_autoPickup ? DropSettle : LootSettle) && ItemOf(drop) != null);
		}

		/// <summary>
		/// Picks up loot the viking has reached, as much of it as its inventory has room for
		/// </summary>
		/// <param name="view">The loot</param>
		/// <returns>False when it was gone or there was no room</returns>
		private bool PickUpLoot(ZNetView view)
		{
			if (view == null || !IsLoot(view))
			{
				return(false);
			}
			ItemDrop drop = view.GetComponent<ItemDrop>();
			if (!OwnItem(view))
			{
				return(false);
			}
			drop.Load();
			ItemDrop.ItemData item = ItemOf(drop);
			int stack = item.m_stack;
			int taken = Carry(item, stack, CarryLoot);
			if (taken <= 0)
			{
				return(false);
			}
			if (taken >= stack)
			{
				ZNetScene.instance.Destroy(drop.gameObject);
			}
			else
			{
				drop.SetStack(stack - taken);
			}
			TestLog.Count("Looted", TestLog.ItemName(item), taken);
			TakeUpGear();
			return(true);
		}

		/// <summary>
		/// Asks the game that owns an item on the ground to hand it over, the way a player's game does as they pick it
		/// up, so it's this client's by the time the viking reaching for it takes it (see OwnItem)
		/// </summary>
		/// <param name="view">The item, or a branch, which needs no asking</param>
		private static void RequestItem(ZNetView view)
		{
			ItemDrop drop = ((view != null && view.IsValid()) ? view.GetComponent<ItemDrop>() : null);
			if (drop != null)
			{
				drop.RequestOwn();
			}
		}

		/// <summary>
		/// Makes an item on the ground the viking has reached this client's to take, the way only its owner may - left
		/// alone while another game owns it, which hasn't handed it over yet (see RequestItem) or is picking it up
		/// </summary>
		/// <param name="view">The item</param>
		/// <returns>True when this client owns it</returns>
		private static bool OwnItem(ZNetView view)
		{
			if (view.IsOwner())
			{
				return(true);
			}
			if (view.GetZDO().HasOwner())
			{
				return(false);
			}
			view.ClaimOwnership();
			return(true);
		}

		/// <summary>
		/// Has the viking take up gear among the loot it carries that's better than its own - armor, a shield or a
		/// weapon - so it wears or wields it rather than putting it in a chest, putting it on in the piece's equip time
		/// the way a player does (see FoundGearDone)
		/// </summary>
		private void TakeUpGear()
		{
			if (viking == null || GearHeld())
			{
				return;
			}
			Inventory inventory = humanoid.GetInventory();
			bool changed = false;
			foreach (ItemDrop.ItemData item in inventory.m_inventory.FindAll((ItemDrop.ItemData carried) => IsCarriedFor(carried, CarryLoot) && IsGear(carried) && !body.GearQueued(carried)))
			{
				// It's its own while it's weighed up, since a viking never wields what it carries for a chest
				item.m_customData.Remove(VikingItems.CarryKey);
				if (!viking.WouldTakeUp(item))
				{
					item.m_customData[VikingItems.CarryKey] = CarryLoot;
					continue;
				}
				body.QueueGear(item, true, false, true);
				changed = true;
			}
			if (changed)
			{
				inventory.Changed();
			}
		}

		/// <summary>
		/// Finishes taking up a piece of gear the viking found as loot or in a chest - carrying what it replaced to a
		/// chest, or the piece itself when it turned out not to be better by the time it was put on
		/// </summary>
		/// <param name="item">The piece</param>
		/// <param name="tookUp">Whether the viking took it up</param>
		/// <param name="replaced">The armor, shield or weapon it replaced, or null</param>
		private void FoundGearDone(ItemDrop.ItemData item, bool tookUp, ItemDrop.ItemData replaced)
		{
			if (!tookUp)
			{
				item.m_customData[VikingItems.CarryKey] = CarryLoot;
				humanoid.GetInventory().Changed();
				return;
			}
			if (replaced != null)
			{
				replaced.m_equipped = false;
				replaced.m_customData[VikingItems.CarryKey] = CarryLoot;
				humanoid.GetInventory().Changed();
			}
			TestLog.Count("Gear taken up", TestLog.ItemName(item));
			Plugin.Log.LogInfo(LogName() + " took up " + Localization.instance.Localize(item.m_shared.m_name) + ((replaced != null) ? " in place of " + Localization.instance.Localize(replaced.m_shared.m_name) : ""));
		}

		/// <summary>
		/// Finds the nearest of the village's chests holding gear the viking would take up over its own (see
		/// VikingCore.WouldTakeUp)
		/// </summary>
		/// <returns>The chest, or null when none holds any</returns>
		private Container FindUpgradeChest()
		{
			if (viking == null)
			{
				return(null);
			}
			return(Nearest(UsableChests().FindAll((Container chest) => UpgradeIn(chest) != null)));
		}

		/// <summary>
		/// Finds gear in a chest the viking would take up over its own
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>The gear, or null when there's none</returns>
		private ItemDrop.ItemData UpgradeIn(Container chest)
		{
			return(chest.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => IsGear(item) && viking.WouldTakeUp(item)));
		}

		/// <summary>
		/// Takes gear the viking would take up out of a chest it has reached and takes it up, carrying what it replaces
		/// to put back
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>False when there was no such gear left in it, no room to carry it, or the chest couldn't be used</returns>
		private bool TakeUpgrade(Container chest)
		{
			if (!OpenChest(chest))
			{
				return(false);
			}
			ItemDrop.ItemData item = UpgradeIn(chest);
			if (item == null || Carry(item, 1, CarryLoot) <= 0)
			{
				return(false);
			}
			chest.GetInventory().RemoveItem(item);
			TakeUpGear();
			return(true);
		}

		/// <summary>
		/// Checks whether an item is gear a viking could take up - armor, a shield, a utility item like a strength belt,
		/// or a weapon other than a torch
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>True when it is</returns>
		private static bool IsGear(ItemDrop.ItemData item)
		{
			ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
			bool armor = (type == ItemDrop.ItemData.ItemType.Helmet || type == ItemDrop.ItemData.ItemType.Chest || type == ItemDrop.ItemData.ItemType.Legs || type == ItemDrop.ItemData.ItemType.Shoulder || type == ItemDrop.ItemData.ItemType.Shield || type == ItemDrop.ItemData.ItemType.Utility);
			return(armor || (item.IsWeapon() && type != ItemDrop.ItemData.ItemType.Torch));
		}

		/// <summary>
		/// Finds a cooking station for the viking to cook at and claims it
		/// </summary>
		/// <returns>The job, or null when there's nothing to cook or nowhere to cook it</returns>
		private Job FindCookWork()
		{
			CookingStation station = FindCookStation(false);
			if (station != null && ClaimObject(station.GetComponent<ZNetView>()))
			{
				return(new CookJob(this, station));
			}
			return(null);
		}

		/// <summary>
		/// Finds the nearest cooking station the viking could cook at, with no other viking cooking there - unless only
		/// what it carries counts, one with food on it to tend first, done food before food still cooking, so it's taken
		/// off before it burns, whoever put it on; then one lit, with an empty slot, and raw food it cooks carried by the
		/// viking or, unless only what it carries counts, in one of the village's chests
		/// </summary>
		/// <param name="carriedOnly">True to only count the raw food the viking carries</param>
		/// <returns>The station, or null when there's none</returns>
		private CookingStation FindCookStation(bool carriedOnly)
		{
			List<CookingStation> stations = VillageCooking.Stations(village, transform.position);
			if (!carriedOnly)
			{
				CookingStation tend = FindTendStation(stations);
				if (tend != null)
				{
					return(tend);
				}
			}
			List<Container> chests = null;
			foreach (CookingStation station in stations)
			{
				if (!VillageCooking.IsLit(station) || VillageCooking.FreeSlots(station) == 0 || !IsFree(station.GetComponent<ZNetView>()))
				{
					continue;
				}
				if (CarriedRaw(station) == null)
				{
					if (carriedOnly)
					{
						continue;
					}
					if (chests == null)
					{
						chests = UsableChests();
					}
					if (!chests.Exists((Container chest) => RawIn(chest, station, chests) != null))
					{
						continue;
					}
				}
				return(station);
			}
			return(null);
		}

		/// <summary>
		/// Finds the nearest cooking station with food on it to tend and no other viking at it - done food first, then
		/// food still cooking over a lit fire
		/// </summary>
		/// <param name="stations">The village's stations, nearest first</param>
		/// <param name="doneOnly">True to only count done food, which burns next</param>
		/// <returns>The station, or null when none needs tending</returns>
		private CookingStation FindTendStation(List<CookingStation> stations, bool doneOnly = false)
		{
			CookingStation cooking = null;
			foreach (CookingStation station in stations)
			{
				if (!IsFree(station.GetComponent<ZNetView>()))
				{
					continue;
				}
				if (station.HaveDoneItem())
				{
					return(station);
				}
				if (!doneOnly && cooking == null && VillageCooking.IsLit(station) && station.HaveUncookedItem())
				{
					cooking = station;
				}
			}
			return(cooking);
		}

		/// <summary>
		/// Finds work tending food already on a cooking station, so it's taken off before it burns
		/// </summary>
		/// <param name="doneOnly">True to only tend done food, which burns next</param>
		/// <param name="takeOffOnly">True to only take off the done food, rather than cook on after (see
		/// CookJob)</param>
		/// <returns>The job, or null when no station needs tending</returns>
		private Job FindTendWork(bool doneOnly = false, bool takeOffOnly = false)
		{
			CookingStation station = FindTendStation(VillageCooking.Stations(village, transform.position), doneOnly);
			if (station != null && ClaimObject(station.GetComponent<ZNetView>()))
			{
				return(new CookJob(this, station, takeOffOnly));
			}
			return(null);
		}

		/// <summary>
		/// Checks whether the viking leaves its job a moment to take done food off a cooking station before it burns -
		/// resting or gathering by the fire, talking, repairing, building back, stocking the chests with wood, or keeping
		/// guard through a raid, all of which it picks up again after
		/// </summary>
		/// <returns>True when it does</returns>
		private bool TakesOffDoneFood()
		{
			return(job is GatherJob || job is TalkJob || job is RepairJob || job is RebuildJob || job is StockJob || job is GuardJob);
		}

		/// <summary>
		/// Finds raw food the viking carries that a cooking station cooks - not what the village's chests are short of
		/// for meals at the cauldron (see MealShort), which goes into a chest instead
		/// </summary>
		/// <param name="station">The station</param>
		/// <returns>The food, or null when it carries none</returns>
		private ItemDrop.ItemData CarriedRaw(CookingStation station)
		{
			return(humanoid.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => IsCarriedFor(item, CarryLoot) && VillageCooking.Cooks(station, item) && !MealShort(item.m_dropPrefab.name)));
		}

		/// <summary>
		/// Finds raw food in a chest that a cooking station cooks, of which the village's chests hold more than they hold
		/// back for meals at the cauldron (see MealHeld)
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="station">The station</param>
		/// <param name="chests">The village's chests the viking may use</param>
		/// <returns>The food, or null when there's none</returns>
		private ItemDrop.ItemData RawIn(Container chest, CookingStation station, List<Container> chests)
		{
			return(chest.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => VillageCooking.Cooks(station, item) && SpareRaw(item.m_dropPrefab.name, chests) > 0));
		}

		/// <summary>
		/// Counts how much of a raw food the village's chests hold beyond what they hold back for meals at the cauldron
		/// (see MealHeld)
		/// </summary>
		/// <param name="name">The food's prefab name</param>
		/// <param name="chests">The village's chests the viking may use</param>
		/// <returns>How much, which may be 0 or less</returns>
		private int SpareRaw(string name, List<Container> chests)
		{
			int held = MealHeld(name);
			return((held > 0) ? Stored(name, chests) - held : int.MaxValue);
		}

		/// <summary>
		/// Takes some of the carried loot out of the viking's inventory
		/// </summary>
		/// <param name="item">The carried stack</param>
		/// <param name="amount">How many</param>
		private void UseCarried(ItemDrop.ItemData item, int amount)
		{
			humanoid.GetInventory().RemoveItem(item, amount);
		}

		/// <summary>
		/// Takes raw food a cooking station cooks out of a chest the viking has reached, up to a number of pieces -
		/// leaving the village's chests what they hold back for meals at the cauldron (see MealHeld)
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="station">The station</param>
		/// <param name="wanted">How many pieces at most</param>
		/// <returns>How many it took</returns>
		private int TakeRaw(Container chest, CookingStation station, int wanted)
		{
			if (!OpenChest(chest))
			{
				return(0);
			}
			Inventory store = chest.GetInventory();
			List<Container> chests = UsableChests();
			int taken = 0;
			foreach (ItemDrop.ItemData item in store.m_inventory.FindAll((ItemDrop.ItemData stored) => VillageCooking.Cooks(station, stored)))
			{
				if (taken >= wanted)
				{
					break;
				}
				int spare = SpareRaw(item.m_dropPrefab.name, chests);
				int took = ((spare > 0) ? Carry(item, Mathf.Min(wanted - taken, Mathf.Min(item.m_stack, spare)), CarryLoot) : 0);
				if (took <= 0)
				{
					continue;
				}
				store.RemoveItem(item, took);
				taken += took;
			}
			// What the chests hold changed, so the next look at what they're short of counts it afresh
			nextMealReserve = 0f;
			return(taken);
		}

		/// <summary>
		/// Takes what's done off a cooking station the viking has reached and carries it, to put away - putting down at
		/// its feet whatever it has no room for - raising its Cooking skill for each piece and getting a bonus piece of
		/// cooked food as often as a player of its skill does, the way a player's comes
		/// </summary>
		/// <param name="station">The station</param>
		private void TakeCooked(CookingStation station)
		{
			Skills.SkillType skill = StationSkill(station.m_skill);
			string burnt = ((station.m_overCookedItem != null) ? station.m_overCookedItem.gameObject.name : "");
			foreach (string name in VillageCooking.TakeDone(station))
			{
				ItemDrop.ItemData item = ItemOf(name);
				if (item == null)
				{
					continue;
				}
				RaiseSkill(skill, CookTakeSkill);
				// Food cooked well comes off with a bonus as often as a player's skill gives one, never burnt food
				int count = 1;
				if (station.m_canGiveBonusYield && name != burnt && InventoryGui.instance != null && Random.value < humanoid.GetSkillFactor(skill) * InventoryGui.instance.m_craftBonusChance)
				{
					count += InventoryGui.instance.m_craftBonusAmount;
					VillageSignals.SendCraftBonus(station.m_nview.GetZDO().m_uid, nview.GetZDO().m_uid, InventoryGui.instance.m_craftBonusAmount);
				}
				int carried = Carry(item, count, CarryLoot);
				if (carried < count)
				{
					ItemDrop.DropItem(item, count - carried, transform.position + transform.forward * 0.6f + Vector3.up * 0.5f, Quaternion.identity);
				}
				TestLog.Count("Cooked", TestLog.ItemName(item) + ((name == burnt) ? " (burnt)" : ""), count);
			}
		}

		/// <summary>
		/// Raises the viking's skill for a batch it crafted at a station - the station's crafting skill, or Crafting when
		/// it names none - and works out the bonus the batch comes with, as often as a player of its skill gets one
		/// </summary>
		/// <param name="bench">The station</param>
		/// <returns>How many more it got, shown on every client when it's any</returns>
		private int CraftBonus(CraftingStation bench)
		{
			Skills.SkillType skill = ((bench.m_craftingSkill != Skills.SkillType.None) ? bench.m_craftingSkill : Skills.SkillType.Crafting);
			RaiseSkill(skill, 1f);
			if (InventoryGui.instance == null || Random.value >= humanoid.GetSkillFactor(skill) * InventoryGui.instance.m_craftBonusChance)
			{
				return(0);
			}
			int bonus = InventoryGui.instance.m_craftBonusAmount;
			VillageSignals.SendCraftBonus(bench.GetComponent<ZNetView>().GetZDO().m_uid, nview.GetZDO().m_uid, bonus);
			return(bonus);
		}

		/// <summary>
		/// Gets the skill a cooking station raises, or Cooking when it names none
		/// </summary>
		/// <param name="skill">The station's skill</param>
		/// <returns>The skill</returns>
		private static Skills.SkillType StationSkill(Skills.SkillType skill)
		{
			return((skill != Skills.SkillType.None) ? skill : Skills.SkillType.Cooking);
		}

		/// <summary>
		/// Raises one of the viking's skills by an amount, the way a player's rises (see VikingCore.RaiseSkill)
		/// </summary>
		/// <param name="skill">The skill</param>
		/// <param name="factor">How much the action raises it</param>
		private void RaiseSkill(Skills.SkillType skill, float factor)
		{
			if (viking != null)
			{
				viking.RaiseSkill(skill, factor);
			}
		}

		/// <summary>
		/// Finds the nearest item lying within CollectRadius of a hunter's kill - its meat, hide and the rest, dropped as
		/// it or its zombie died
		/// </summary>
		/// <param name="kill">Where the kill was made</param>
		/// <returns>The item, or null when there's none</returns>
		private ZNetView FindDrop(Vector3 kill)
		{
			ZNetView nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (ItemDrop drop in LoadedDrops())
			{
				ZNetView view = ((drop != null) ? drop.m_nview : null);
				if (view == null || !view.IsValid() || Utils.DistanceXZ(view.transform.position, kill) > CollectRadius || !IsKillDrop(view))
				{
					continue;
				}
				ItemDrop.ItemData item = drop.m_itemData;
				float distance = Vector3.Distance(view.transform.position, transform.position);
				if (distance < nearestDistance && CarryRoom(item, CarryLoot) > 0 && !Avoided(view.GetZDO().m_uid))
				{
					nearest = view;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Checks whether an object is an item a hunter gathers from its kill - one lying on the ground that isn't set
		/// out as a piece, a fish, or one a viking dropped outside the village, however long it has lain there
		/// </summary>
		/// <param name="view">The object</param>
		/// <returns>True when it is</returns>
		private static bool IsKillDrop(ZNetView view)
		{
			if (!view.IsValid() || view.GetZDO().GetBool(DiscardKey))
			{
				return(false);
			}
			ItemDrop drop = view.GetComponent<ItemDrop>();
			return(drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null && !drop.IsPiece() && view.GetComponent<Fish>() == null && ItemOf(drop) != null);
		}

		/// <summary>
		/// Picks up an item from a hunter's kill, as much of it as the viking has room for
		/// </summary>
		/// <param name="view">The item</param>
		/// <returns>False when it was gone or there was no room</returns>
		private bool PickUpDrop(ZNetView view)
		{
			if (view == null || !IsKillDrop(view))
			{
				return(false);
			}
			ItemDrop drop = view.GetComponent<ItemDrop>();
			if (!OwnItem(view))
			{
				return(false);
			}
			drop.Load();
			ItemDrop.ItemData item = ItemOf(drop);
			int stack = item.m_stack;
			int taken = Carry(item, stack, CarryLoot);
			if (taken <= 0)
			{
				return(false);
			}
			if (taken >= stack)
			{
				ZNetScene.instance.Destroy(drop.gameObject);
			}
			else
			{
				drop.SetStack(stack - taken);
			}
			TestLog.Count("Looted from kills", TestLog.ItemName(item), taken);
			return(true);
		}

		/// <summary>
		/// Finds the nearest beehive built in the village with honey in it that no other viking is after, that the
		/// viking has room to carry the honey of and one of the village's chests has room for
		/// </summary>
		/// <returns>The beehive, or null when there's none</returns>
		private Beehive FindHive()
		{
			List<Container> chests = UsableChests();
			if (chests.Count == 0)
			{
				return(null);
			}
			Beehive nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (Beehive hive in LoadedHives())
			{
				ZNetView view = ((hive != null) ? hive.m_nview : null);
				if (view == null || !view.IsValid() || !village.OwnsPiece(view.transform.position))
				{
					continue;
				}
				ZDOID id = view.GetZDO().m_uid;
				if (Avoided(id) || hive.m_honeyItem == null || hive.GetHoneyLevel() <= 0)
				{
					continue;
				}
				if (workClaims.TryGetValue(id, out VillageLife holder) && holder != this && holder != null && holder.job != null)
				{
					continue;
				}
				float distance = Vector3.Distance(view.transform.position, transform.position);
				if (distance >= nearestDistance)
				{
					continue;
				}
				ItemDrop.ItemData honey = ItemOf(hive.m_honeyItem.gameObject.name);
				if (honey == null)
				{
					continue;
				}
				int amount = HoneyIn(hive, honey);
				honey.m_stack = CarriedLootOf(honey) + amount;
				if (CarryRoom(honey, CarryLoot) >= amount && chests.Exists((Container chest) => HasRoom(chest, honey, chests)))
				{
					nearest = hive;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Works out how much honey a beehive gives, the way it gives a player - one for each of its honey, scaled by the
		/// world's resource rate
		/// </summary>
		/// <param name="hive">The beehive</param>
		/// <param name="honey">Its honey</param>
		/// <returns>How much</returns>
		private static int HoneyIn(Beehive hive, ItemDrop.ItemData honey)
		{
			int amount = 0;
			for (int i = hive.GetHoneyLevel(); i > 0; i--)
			{
				amount += ((Game.instance != null) ? Game.instance.ScaleDrops(honey, 1) : 1);
			}
			return(amount);
		}

		/// <summary>
		/// Takes the honey out of a beehive the viking has reached, the same as a player, carried for a chest rather than
		/// dropped
		/// </summary>
		/// <param name="hive">The beehive</param>
		/// <returns>How much it took</returns>
		private int TakeHoney(Beehive hive)
		{
			if (hive == null || hive.m_nview == null || !hive.m_nview.IsValid() || hive.m_honeyItem == null)
			{
				return(0);
			}
			if (!hive.m_nview.IsOwner())
			{
				hive.m_nview.ClaimOwnership();
			}
			ItemDrop.ItemData honey = ItemOf(hive.m_honeyItem.gameObject.name);
			int amount = ((honey != null) ? HoneyIn(hive, honey) : 0);
			if (amount <= 0 || CarryRoom(honey, CarryLoot) < amount)
			{
				return(0);
			}
			int taken = Carry(honey, amount, CarryLoot);
			if (taken > 0)
			{
				TestLog.Count("Gathered", TestLog.ItemName(honey), taken);
				NoteFoodFound();
				hive.ResetLevel();
				hive.m_spawnEffect.Create(hive.m_spawnPoint.position, Quaternion.identity);
			}
			return(taken);
		}

		/// <summary>
		/// The food each pickable's prefab gives, by the prefab's name, or null for one that gives no food
		/// </summary>
		private static readonly Dictionary<string, ItemDrop.ItemData> forageFoods = new Dictionary<string, ItemDrop.ItemData>();

		/// <summary>
		/// What each creature's or bird's prefab drops when it's killed, by the prefab's name (see Drops)
		/// </summary>
		private static readonly Dictionary<string, HashSet<string>> dropsByPrefab = new Dictionary<string, HashSet<string>>();

		/// <summary>
		/// What the vikings gather besides food, by prefab name, whether it grows back or not - flint, for arrows, and
		/// stone, only gathered for a hoe (see FindPathWork)
		/// </summary>
		private static readonly HashSet<string> ForageMaterials = new HashSet<string> { "Flint", StonePrefab };
		private const string StonePrefab = "Stone";

		/// <summary>
		/// Finds what a pickable gives that the viking gathers - food that grows back, like berries and mushrooms, or what
		/// a cauldron's recipe takes that grows back, like thistle (see FindForage), but not crops a player planted, or
		/// flint (see ForageMaterials)
		/// </summary>
		/// <param name="pickable">The pickable</param>
		/// <returns>The item, or null when it gives nothing the viking gathers</returns>
		private static ItemDrop.ItemData ForageFood(Pickable pickable)
		{
			if (pickable.m_itemPrefab == null || ObjectDB.instance == null)
			{
				return(null);
			}
			string name = pickable.m_itemPrefab.name;
			bool material = ForageMaterials.Contains(name);
			if (!material && pickable.m_respawnTimeMinutes <= 0f)
			{
				return(null);
			}
			if (!forageFoods.TryGetValue(name, out ItemDrop.ItemData food))
			{
				food = ItemOf(name);
				if (!material && !VikingFood.IsFood(food) && !VillageCooking.IsCauldronIngredient(name))
				{
					food = null;
				}
				forageFoods[name] = food;
			}
			return(food);
		}

		/// <summary>
		/// Checks whether a pickable is something the viking can forage now - grown back, and giving food or flint
		/// </summary>
		/// <param name="pickable">The pickable</param>
		/// <returns>True when it is</returns>
		private static bool IsForage(Pickable pickable)
		{
			return(pickable != null && pickable.m_nview != null && pickable.m_nview.IsValid() && pickable.CanBePicked() && ForageFood(pickable) != null);
		}

		/// <summary>
		/// Counts how many of an item the viking carries for a chest
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>How many</returns>
		private int CarriedLootOf(ItemDrop.ItemData item)
		{
			int count = 0;
			foreach (ItemDrop.ItemData carried in humanoid.GetInventory().GetAllItems())
			{
				if (StacksWith(carried, item, CarryLoot))
				{
					count += carried.m_stack;
				}
			}
			return(count);
		}

		/// <summary>
		/// Finds the nearest food to forage within ForageReach of the village - one no other viking is after, with no
		/// enemy within ForageSafety of it, that the viking has room to carry and one of the village's chests has room
		/// for along with what it already carries of it - only in the village while the viking may not leave it (see
		/// MayLeave). Stone is only gathered when it's what the viking is after, and what only a cauldron's recipe takes,
		/// like thistle, only by a village with a cauldron
		/// </summary>
		/// <param name="only">The prefab name of the one thing the viking is after, or null for any food or flint</param>
		/// <returns>The pickable, or null when there's none</returns>
		private Pickable FindForage(string only = null)
		{
			List<Container> chests = UsableChests();
			if (chests.Count == 0)
			{
				return(null);
			}
			bool cauldron = (FindStation(VillageCooking.CauldronHash) != null);
			float reach = (MayLeave() ? ForageReach : 0f);
			List<KeyValuePair<float, Pickable>> found = new List<KeyValuePair<float, Pickable>>();
			foreach (Pickable pickable in LoadedPickables())
			{
				ZNetView view = ((pickable != null) ? pickable.m_nview : null);
				if (view == null || !view.IsValid() || village.DistanceOutside(view.transform.position) > reach)
				{
					continue;
				}
				ZDOID id = view.GetZDO().m_uid;
				if (Avoided(id) || !IsForage(pickable))
				{
					continue;
				}
				string gives = pickable.m_itemPrefab.name;
				if ((only != null) ? (gives != only) : (gives == StonePrefab))
				{
					continue;
				}
				if (!cauldron && !ForageMaterials.Contains(gives) && !VikingFood.IsFood(ForageFood(pickable)))
				{
					continue;
				}
				if (workClaims.TryGetValue(id, out VillageLife holder) && holder != this && holder != null && holder.job != null)
				{
					continue;
				}
				found.Add(new KeyValuePair<float, Pickable>(Vector3.Distance(view.transform.position, transform.position), pickable));
			}
			found.Sort((KeyValuePair<float, Pickable> a, KeyValuePair<float, Pickable> b) => a.Key.CompareTo(b.Key));
			int unreachable = 0;
			foreach (KeyValuePair<float, Pickable> entry in found)
			{
				Pickable pickable = entry.Value;
				ItemDrop.ItemData food = ForageFood(pickable).Clone();
				int amount = ForageAmount(pickable);
				food.m_stack = CarriedLootOf(food) + amount;
				if (CarryRoom(food, CarryLoot) < amount || !chests.Exists((Container chest) => HasRoom(chest, food, chests)))
				{
					continue;
				}
				if (DangerNear(pickable.transform.position))
				{
					continue;
				}
				// Food it can't get within reach of is left alone for a while - except in a pen, which has no way in but
				// over its fence, which a forager jumps
				if (!InPen(pickable.m_nview.GetZDO().m_uid, pickable.transform.position) && !CanReach(pickable.transform.position, out Vector3 _))
				{
					Avoid(pickable.m_nview);
					unreachable++;
					if (unreachable >= ReachTries)
					{
						return(null);
					}
					continue;
				}
				return(pickable);
			}
			return(null);
		}

		/// <summary>
		/// Picks food the viking has reached, the same as a player picking it, so it grows back as usual - carried for a
		/// chest rather than dropped
		/// </summary>
		/// <remarks>
		/// It picks as much as a player would - scaled by the world's resource rate, unless the pickable isn't scaled -
		/// and gets the pickable's bonus the way a player does from the skill picking it raises, Farming when it raises
		/// none: as often as the pickable's chance at full skill, scaled by how far up the skill the viking is. Every
		/// client shows the bonus above the viking with the pickable's bonus sound, and the skill rises for each pick,
		/// the same as a player's
		/// </remarks>
		/// <param name="pickable">The pickable</param>
		/// <returns>How many it picked</returns>
		private int PickForage(Pickable pickable)
		{
			if (!IsForage(pickable))
			{
				return(0);
			}
			ItemDrop.ItemData food = ForageFood(pickable);
			int amount = ForageAmount(pickable);
			Skills.SkillType skill = ((pickable.m_pickRaiseSkill != Skills.SkillType.None) ? pickable.m_pickRaiseSkill : Skills.SkillType.Farming);
			bool lucky = (Random.value < humanoid.GetSkillFactor(skill) * pickable.m_maxLevelBonusChance);
			int bonus = (lucky ? Mathf.Max(0, pickable.m_bonusYieldAmount) : 0);
			int room = CarryRoom(food, CarryLoot);
			if (room < amount)
			{
				return(0);
			}
			bonus = Mathf.Min(bonus, room - amount);
			pickable.m_pickEffector.Create(pickable.transform.position, Quaternion.identity);
			pickable.m_nview.InvokeRPC(ZNetView.Everybody, "RPC_SetPicked", true);
			if (viking != null)
			{
				viking.RaiseSkill(skill, 1f);
			}
			if (bonus > 0)
			{
				VillageSignals.SendForageBonus(pickable.m_nview.GetZDO().m_uid, nview.GetZDO().m_uid, bonus);
			}
			int carried = Carry(food, amount + bonus, CarryLoot);
			TestLog.Count("Gathered", TestLog.ItemName(food), carried);
			return(carried);
		}

		/// <summary>
		/// Works out how much food picking a pickable gives before any bonus - its amount scaled by the world's resource
		/// rate, the same as for a player, unless it isn't scaled
		/// </summary>
		/// <param name="pickable">The pickable</param>
		/// <returns>How many</returns>
		private static int ForageAmount(Pickable pickable)
		{
			int amount = Mathf.Max(1, pickable.m_amount);
			if (pickable.m_dontScale || Game.instance == null || pickable.m_itemPrefab == null)
			{
				return(amount);
			}
			return(Mathf.Max(1, Mathf.Max(pickable.m_minAmountScaled, Game.instance.ScaleDrops(pickable.m_itemPrefab, amount))));
		}

		/// <summary>
		/// Finds the food in a chest the viking eats now - wounded, the one that heals it fastest, and keeping its foods
		/// up, the weakest that raises it above its floor, or with none, the strongest of the rest (see
		/// VikingFood.TopUpOrder), so the strongest food is left for the wounded
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="topUp">True when it's keeping its foods up rather than eating for its wounds</param>
		/// <returns>The food, or null when there's none</returns>
		private ItemDrop.ItemData FoodIn(Container chest, bool topUp)
		{
			ItemDrop.ItemData best = null;
			float floor = body.Food.Floor();
			foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
			{
				if (!(topUp ? body.Food.CanTopUp(item) : body.Food.CanEat(item)))
				{
					continue;
				}
				if (best == null || (topUp ? EatsBefore(item, best, floor) : (item.m_shared.m_foodRegen > best.m_shared.m_foodRegen || (item.m_shared.m_foodRegen == best.m_shared.m_foodRegen && item.m_shared.m_food > best.m_shared.m_food))))
				{
					best = item;
				}
			}
			return(best);
		}

		/// <summary>
		/// Checks whether a viking keeping its foods up eats one food before another (see VikingFood.TopUpOrder)
		/// </summary>
		/// <param name="item">The food</param>
		/// <param name="other">The other food</param>
		/// <param name="floor">The viking's floor</param>
		/// <returns>True when it eats the first first</returns>
		private static bool EatsBefore(ItemDrop.ItemData item, ItemDrop.ItemData other, float floor)
		{
			return(VikingFood.TopUpOrder(item.m_shared.m_food, item.m_shared.m_foodRegen, other.m_shared.m_food, other.m_shared.m_foodRegen, floor) < 0);
		}

		/// <summary>
		/// Checks whether the chests the viking may use hold food enough to go around the village (see FoodEnough)
		/// </summary>
		/// <returns>True when they do</returns>
		private bool FoodToSpare()
		{
			int food = 0;
			foreach (Container chest in UsableChests())
			{
				foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
				{
					if (VikingFood.IsFood(item))
					{
						food += item.m_stack;
					}
				}
			}
			return(FoodEnough(food, village.Vikings().Count));
		}

		/// <summary>
		/// Checks whether there's food enough to go around a village - SpareMeals of it for each of its vikings, enough
		/// for every one to eat a full set of foods
		/// </summary>
		/// <param name="food">How much food the chests hold</param>
		/// <param name="vikings">How many vikings the village has loaded</param>
		/// <returns>True when there is</returns>
		internal static bool FoodEnough(int food, int vikings)
		{
			return(food >= Mathf.Max(1, vikings) * SpareMeals);
		}

		/// <summary>
		/// Finds the chest the viking eats from - wounded, the nearest of the village's chests it may use with food in it
		/// it can eat now, and keeping its foods up, the one holding the food it eats first of all of them (see FoodIn),
		/// the nearest of those alike
		/// </summary>
		/// <param name="topUp">True when it's keeping its foods up rather than eating for its wounds</param>
		/// <returns>The chest, or null when there's none</returns>
		private Container FindFoodChest(bool topUp)
		{
			List<Container> pantries = UsableChests().FindAll((Container chest) => FoodIn(chest, topUp) != null);
			if (!topUp || pantries.Count == 0)
			{
				return(Nearest(pantries));
			}
			float floor = body.Food.Floor();
			ItemDrop.ItemData first = null;
			foreach (Container chest in pantries)
			{
				ItemDrop.ItemData food = FoodIn(chest, true);
				if (first == null || EatsBefore(food, first, floor))
				{
					first = food;
				}
			}
			return(Nearest(pantries.FindAll((Container chest) => !EatsBefore(first, FoodIn(chest, true), floor))));
		}

		/// <summary>
		/// Eats the food in a chest the viking has reached that it eats now (see FoodIn), the way a player eats
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="topUp">True when it's keeping its foods up rather than eating for its wounds</param>
		/// <returns>False when there was no food in it it could eat, or it couldn't be used</returns>
		private bool EatFrom(Container chest, bool topUp)
		{
			if (!OpenChest(chest))
			{
				return(false);
			}
			ItemDrop.ItemData food = FoodIn(chest, topUp);
			if (food == null || !body.Food.Eat(food))
			{
				return(false);
			}
			TestLog.Count("Eaten", TestLog.ItemName(food));
			chest.GetInventory().RemoveItem(food, 1);
			chest.m_closeEffects.Create(chest.transform.position, chest.transform.rotation);
			humanoid.m_consumeItemEffects.Create(transform.position, Quaternion.identity);
			humanoid.m_zanim.SetTrigger("eat");
			return(true);
		}

		/// <summary>
		/// Finds another viking of the village this client owns that's free to talk - strolling or standing about. In the
		/// rain it's one under a roof when there is one, or else one to bring inside its own house to talk (see
		/// TalkSession), and none when it has no house to talk in
		/// </summary>
		/// <returns>The viking, or null</returns>
		private VillageLife FindPartner()
		{
			List<VillageLife> free = FreePartners();
			if (free.Count == 0 || !VikingWeather.KeepsIndoors(transform.position))
			{
				return((free.Count > 0) ? free[Random.Range(0, free.Count)] : null);
			}
			List<VillageLife> sheltered = free.FindAll((VillageLife other) => VikingWeather.UnderRoof(other.humanoid.GetCenterPoint()));
			if (sheltered.Count > 0)
			{
				return(sheltered[Random.Range(0, sheltered.Count)]);
			}
			return(Home(out VillagePaths.House _) ? free[Random.Range(0, free.Count)] : null);
		}

		/// <summary>
		/// Lists the other vikings of the village this client owns that are free to talk - strolling or standing about,
		/// within TalkSearch
		/// </summary>
		/// <returns>The vikings</returns>
		private List<VillageLife> FreePartners()
		{
			List<VillageLife> free = new List<VillageLife>();
			foreach (VillageLife other in All)
			{
				if (other == this || !other.active || other.village != village || other.ai.m_targetCreature != null)
				{
					continue;
				}
				if (other.job != null && !(other.job is StrollJob) && !(other.job is IdleJob))
				{
					continue;
				}
				if (Vector3.Distance(other.transform.position, transform.position) <= TalkSearch)
				{
					free.Add(other);
				}
			}
			return(free);
		}

		/// <summary>
		/// Starts the viking talking with another that has come to talk to it
		/// </summary>
		/// <param name="session">The talk</param>
		private void JoinTalk(TalkSession session)
		{
			StopJob();
			job = new TalkJob(this, session);
			job.Begin();
		}

		/// <summary>
		/// Finds one of the village's boars to pet - in a pen or out of one, since petting is one of the few things a
		/// viking goes into a pen for
		/// </summary>
		/// <returns>The boar, or null</returns>
		private Character FindBoar()
		{
			List<Character> boars = new List<Character>();
			foreach (Character character in Character.GetAllCharacters())
			{
				if (character == null || character.IsDead() || !character.IsTamed() || !FarmSpawns.IsLivestock(character) || !village.Contains(character.transform.position))
				{
					continue;
				}
				boars.Add(character);
			}
			return((boars.Count > 0) ? boars[Random.Range(0, boars.Count)] : null);
		}

		/// <summary>
		/// Checks whether a fire holds as much wood as it can
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <returns>True when it's full</returns>
		private static bool IsFull(Fireplace fire)
		{
			return(FuelNeeded(fire) <= 0);
		}

		/// <summary>
		/// Works out how much wood a fire takes to fill
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <returns>The wood, or 0 when it's full, burns forever or isn't loaded</returns>
		private static int FuelNeeded(Fireplace fire)
		{
			ZNetView view = fire.GetComponent<ZNetView>();
			if (view == null || !view.IsValid() || fire.m_infiniteFuel)
			{
				return(0);
			}
			return(Mathf.Max(0, Mathf.FloorToInt(fire.m_maxFuel - Mathf.CeilToInt(view.GetZDO().GetFloat(ZDOVars.s_fuel)))));
		}

		/// <summary>
		/// Stops what the viking is doing for something that comes first, like a fight or getting out of a pen - it gets
		/// up, puts away what it holds and lets go of its claims - counting why for the test log (see LogInterrupt)
		/// </summary>
		/// <param name="why">Why it stops, for the test log, like "a fight"</param>
		private void Interrupt(string why)
		{
			if (job != null || attachPoint != null)
			{
				LogInterrupt(why);
				StopJob();
				VikingPoses.StopAll(humanoid);
			}
		}

		/// <summary>
		/// Ends the viking's job
		/// </summary>
		/// <param name="done">Whether the job finished, rather than being interrupted, for the test log</param>
		private void StopJob(bool done = false)
		{
			Job ending = job;
			job = null;
			if (ending != null)
			{
				LogJobStopped(ending, done);
				ending.End();
			}
			Detach();
			HoldHammer(false);
			moveFailed = false;
			woodInPen = false;
			inPenForWood = false;
		}

		/// <summary>
		/// Sends the viking after an enemy
		/// </summary>
		/// <param name="enemy">The enemy</param>
		private void Engage(Character enemy)
		{
			ai.m_targetCreature = enemy;
			ai.m_lastKnownTargetPos = enemy.transform.position;
			ai.m_beenAtLastPos = false;
			ai.m_targetStatic = null;
			ai.SetAlerted(true);
		}

		/// <summary>
		/// Has the night watch stop and roar at an enemy it has spotted near the village, calling out to wake the others
		/// </summary>
		/// <param name="enemy">The enemy</param>
		private void RaiseAlarm(Character enemy)
		{
			lastAlarm = Time.time;
			alarmUntil = Time.time + AlarmPose;
			ai.StopMoving();
			ai.LookAt(enemy.GetCenterPoint());
			VikingPoses.Roar(humanoid);
			VikingSpeech.SayFirst(humanoid, VikingSpeech.Alarm(enemy, VillageNames.Get(village)), VikingSpeech.AlarmTime, VikingSpeech.AlarmCull);
			calledOut[enemy.GetZDOID()] = Time.time;
			VillageSignals.SendAlarm(village);
		}

		/// <summary>
		/// Checks whether the viking is still after an enemy that hurt it or someone it's defending
		/// </summary>
		/// <param name="target">The enemy</param>
		/// <returns>True when it is</returns>
		private bool HoldsGrudge(Character target)
		{
			return(target != null && target == grudge && Time.time <= grudgeUntil);
		}

		/// <summary>
		/// Finds the nearest enemy of the viking within a distance of its village's edge
		/// </summary>
		/// <param name="reach">How far past the edge to look, in meters</param>
		/// <returns>The enemy, or null</returns>
		private Character NearestEnemy(float reach)
		{
			Character nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (Character character in Character.GetAllCharacters())
			{
				if (character == null || character == humanoid || character.IsDead() || !BaseAI.IsEnemy(humanoid, character) || character.IsPlayer())
				{
					continue;
				}
				if (village.DistanceOutside(character.transform.position) > reach)
				{
					continue;
				}
				float distance = Vector3.Distance(character.transform.position, transform.position);
				if (distance < nearestDistance)
				{
					nearest = character;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Finds the viking this client owns nearest where one keeping watch for its village fell that saw it - awake,
		/// within WitnessRange with nothing in the way, not keeping watch and not having kept it tonight - to take its
		/// place
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="where">Where the viking keeping watch fell</param>
		/// <param name="dead">The dead viking's ZDO</param>
		/// <returns>The viking, or null when none saw it</returns>
		public static Character WatchWitness(Village village, Vector3 where, ZDOID dead)
		{
			if (witnessMask == 0)
			{
				witnessMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
			}
			Character nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (VillageLife life in All)
			{
				if (!life.active || life.village != village || life.humanoid == null || life.humanoid.IsDead() || !life.nview.IsOwner())
				{
					continue;
				}
				ZDOID id = life.nview.GetZDO().m_uid;
				if (id == dead || VillageWatch.IsWatch(village, id) || VillageWatch.HasWatched(village, id))
				{
					continue;
				}
				// One asleep, or resting with its senses dulled, sees nothing
				if (life.inBed || life.dulled || (life.job != null && life.job.Resting()))
				{
					continue;
				}
				Vector3 eye = life.humanoid.m_eye.position;
				float distance = Vector3.Distance(eye, where);
				if (distance > WitnessRange || distance >= nearestDistance || Physics.Linecast(eye, where, witnessMask, QueryTriggerInteraction.Ignore))
				{
					continue;
				}
				nearest = life.humanoid;
				nearestDistance = distance;
			}
			return(nearest);
		}

		/// <summary>
		/// Checks whether this viking is its village's night watch tonight
		/// </summary>
		/// <returns>True when it is</returns>
		private bool IsWatch()
		{
			return(village != null && (IsNight() || IsDusk()) && VillageWatch.IsWatch(village, nview.GetZDO().m_uid));
		}

		/// <summary>
		/// Checks whether the viking only picks fights with monsters that are a threat - any living viking
		/// </summary>
		/// <returns>True when it does</returns>
		public bool PicksFightsWithThreats()
		{
			return(humanoid != null && VikingCore.IsLivingViking(humanoid));
		}

		/// <summary>
		/// Checks whether a monster is a threat a living viking goes after - one in its village, within ThreatRange of
		/// it, alerted, or after someone or something. A living animal that only ever runs, like a deer or a hare, is
		/// never one, even alerted as it runs from a hunter's arrows - only the prey the viking is hunting itself, or a
		/// zombie one, which comes after people instead. Neither is a leech that hasn't hurt anyone (see QuietMonsters)
		/// </summary>
		/// <param name="monster">The monster</param>
		/// <returns>True when it is</returns>
		public bool IsThreat(Character monster)
		{
			// Prey an archer shoots from afar is left to its hunt until the AI takes the fight over
			if (job is HuntJob hunt && hunt.Allows(monster))
			{
				return(!hunt.Snipes(monster));
			}
			if (!FightsBack(monster) || QuietMonsters.LeftAlone(monster))
			{
				return(false);
			}
			Vector3 position = monster.transform.position;
			if ((village != null && village.Contains(position)) || Utils.DistanceXZ(position, transform.position) <= ThreatRange)
			{
				return(true);
			}
			BaseAI other = monster.GetBaseAI();
			return(other != null && (other.IsAlerted() || other.GetTargetCreature() != null || (other is MonsterAI hunter && hunter.m_targetStatic != null)));
		}

		/// <summary>
		/// Checks whether a creature fights back - anything but a living animal that only ever runs, like a deer, a hare
		/// or a bird, whose AI never attacks. A zombie one does, since it comes after people instead
		/// </summary>
		/// <param name="creature">The creature</param>
		/// <returns>True when it does</returns>
		private static bool FightsBack(Character creature)
		{
			return(!(creature.GetBaseAI() is AnimalAI) || ZombieState.IsZombie(creature));
		}

		/// <summary>
		/// Checks whether the viking is an archer, which slightly prefers enemy archers when picking which threat to go
		/// after (see NearestThreat)
		/// </summary>
		/// <returns>True when it is</returns>
		public bool PrefersArchers()
		{
			return(HasBow(humanoid));
		}

		/// <summary>
		/// Finds the nearest enemy the viking's AI can sense that's a threat, the way the AI finds its nearest enemy - for
		/// an archer, with enemy archers counted a little nearer than they are (see TargetDistance)
		/// </summary>
		/// <returns>The enemy, or null when there's none</returns>
		public Character NearestThreat()
		{
			Character nearest = null;
			float nearestDistance = float.MaxValue;
			bool archer = PrefersArchers();
			foreach (Character other in Character.GetAllCharacters())
			{
				if (other == null || other.IsDead() || other.m_aiSkipTarget || !BaseAI.IsEnemy(humanoid, other))
				{
					continue;
				}
				BaseAI otherAI = other.GetBaseAI();
				if ((otherAI != null && otherAI.IsSleeping()) || !ai.CanSenseTarget(other) || !IsThreat(other) || OutOfReach(other))
				{
					continue;
				}
				float distance = Vector3.Distance(other.transform.position, transform.position);
				distance = TargetDistance(distance, archer && IsEnemyArcher(other));
				if (distance < nearestDistance)
				{
					nearest = other;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Works out how near a viking counts an enemy as when picking which to go after - an archer counts an enemy
		/// archer as ArcherTargetWeight of its distance, and every other enemy as its distance
		/// </summary>
		/// <param name="distance">How far the enemy is, in meters</param>
		/// <param name="preferred">Whether it's an enemy archer and the viking an archer</param>
		/// <returns>The distance it counts as, in meters</returns>
		internal static float TargetDistance(float distance, bool preferred)
		{
			return(preferred ? (distance * ArcherTargetWeight) : distance);
		}

		/// <summary>
		/// Checks whether an enemy is an archer - one holding a bow or crossbow that shoots, like a draugr or skeleton
		/// archer, or a zombie viking with a bow
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>True when it is</returns>
		private static bool IsEnemyArcher(Character enemy)
		{
			ItemDrop.ItemData weapon = ((enemy is Humanoid armed) ? armed.GetCurrentWeapon() : null);
			if (weapon == null || weapon.m_shared.m_attack == null || weapon.m_shared.m_attack.m_attackType != Attack.AttackType.Projectile)
			{
				return(false);
			}
			Skills.SkillType skill = weapon.m_shared.m_skillType;
			return(weapon.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow || skill == Skills.SkillType.Bows || skill == Skills.SkillType.Crossbows);
		}

		/// <summary>
		/// Checks whether it's night
		/// </summary>
		/// <returns>True when it is</returns>
		private static bool IsNight()
		{
			return(EnvMan.IsNight());
		}

		/// <summary>
		/// Checks whether it's time for the viking to go to bed - once it's night and its own delay has passed, picked
		/// the first time it's asked each night (see SpreadDelay), so the village doesn't all head to bed at once. The
		/// pick is forgotten as the viking picks its jobs by day (see ChooseJob). Once the players have slept through the
		/// night, it never is until the next night, so no viking goes to bed in the morning they woke to (see SleepSkip)
		/// </summary>
		/// <returns>True when it is</returns>
		private bool Bedtime()
		{
			if (!IsNight() || SleepSkip.SleptThroughNight())
			{
				return(false);
			}
			if (bedAt < 0f)
			{
				bedAt = Time.time + SpreadDelay();
			}
			return(Time.time >= bedAt);
		}

		/// <summary>
		/// Picks how long a viking waits to go to bed or get up, up to SleepSpread seconds, most of them short
		/// </summary>
		/// <returns>The wait, in seconds</returns>
		private static float SpreadDelay()
		{
			float late = Random.value;
			return(late * late * SleepSpread);
		}

		/// <summary>
		/// Checks whether it's dusk - the end of the day, before night falls
		/// </summary>
		/// <returns>True when it is</returns>
		private static bool IsDusk()
		{
			return(EnvMan.instance != null && !EnvMan.IsNight() && EnvMan.instance.GetDayFraction() >= DuskStart);
		}

		/// <summary>
		/// Dulls the viking's senses while it rests, so it sees nothing and only hears noise, and puts them back after
		/// </summary>
		/// <param name="dull">True to dull them</param>
		private void SetDulled(bool dull)
		{
			if (dull == dulled || ai == null)
			{
				return;
			}
			dulled = dull;
			ai.m_viewRange = (dull ? 0f : viewRange);
			ai.m_hearRange = (dull ? Mathf.Min(hearRange, RestingHearRange) : hearRange);
		}

		/// <summary>
		/// Walks the viking toward a spot, through the doors in its way - running when it asks to, when it's away from
		/// home or on a trip to another village (see AwayFromHome), or when it's climbing a slope
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="target">The spot</param>
		/// <param name="reach">How near it has to get</param>
		/// <param name="run">True to run</param>
		/// <returns>True once it's there, or when it gave up, which sets moveFailed</returns>
		private bool Go(float dt, Vector3 target, float reach, bool run)
		{
			if (WaitForPoison(target, out bool poisonGaveUp))
			{
				return(poisonGaveUp);
			}
			run = (run || AwayFromHome() || RunsInRain() || (viking != null && viking.Climbing()));
			bool viaDoor = VikingDoors.Steer(humanoid, target, out Vector3 waypoint, out bool direct);
			mover = (direct ? "straight through a door" : (viaDoor ? "path to a door" : "path")) + " for " + ((job != null) ? job.GetType().Name : "village life");
			if (direct)
			{
				walkedThisUpdate = true;
				ai.MoveTowards(waypoint - transform.position, run);
				bool gaveUp = CheckStuck(target);
				AskWayWhenHeldUp(waypoint);
				return(gaveUp);
			}
			// MoveTo also stops when it finds no way there, which CheckStuck gives up on once no progress is made
			ai.MoveTo(dt, waypoint, (viaDoor ? 0.5f : reach), run);
			if (!viaDoor && Utils.DistanceXZ(transform.position, target) <= Mathf.Max(reach, ArriveReach))
			{
				ai.StopMoving();
				ResetStuck();
				return(true);
			}
			walkedThisUpdate = true;
			// Waiting its turn at a door isn't being stuck, though it asks whoever stands about in the doorway to make way
			if (viaDoor && VikingDoors.IsWaiting(humanoid))
			{
				ResetStuck();
				Character standing = VikingDoors.DoorwayBlocker(humanoid);
				if (standing != null && Time.time >= nextAskWay && standing.TryGetComponent(out VillageLife other))
				{
					nextAskWay = Time.time + MakeWayAfter;
					other.MakeWay(transform.position, standing.transform.position - transform.position);
				}
				return(false);
			}
			bool stuck = CheckStuck(target);
			AskWayWhenHeldUp(waypoint);
			return(stuck);
		}

		/// <summary>
		/// Holds the viking back from a spot within PoisonAvoid of poison once it's within PoisonWaitReach of where it
		/// keeps clear of it, standing there until the poison is gone - a cloud lasts moments - and giving up on the spot
		/// after PoisonWaitTime seconds, the way it gives up on a spot it can't reach, so its job moves on
		/// </summary>
		/// <param name="target">Where the viking is walking</param>
		/// <param name="gaveUp">Set to true when it gives up on the spot, which sets moveFailed</param>
		/// <returns>True while it holds back, so it doesn't walk on</returns>
		private bool WaitForPoison(Vector3 target, out bool gaveUp)
		{
			gaveUp = false;
			if (!VillageHazards.NearestPoison(target, PoisonAvoid, out Vector3 poison) || Utils.DistanceXZ(transform.position, poison) > PoisonAvoid + PoisonWaitReach)
			{
				poisonWaitSince = -1f;
				return(false);
			}
			mover = "waiting for poison to go for " + ((job != null) ? job.GetType().Name : "village life");
			ai.StopMoving();
			ResetStuck();
			// A wait broken off for longer than PoisonWaitBreak - for a fight, or a job that doesn't walk - starts over
			if (poisonWaitSince < 0f || Time.time - poisonWaitLast > PoisonWaitBreak)
			{
				poisonWaitSince = Time.time;
			}
			poisonWaitLast = Time.time;
			if (Time.time - poisonWaitSince >= PoisonWaitTime)
			{
				poisonWaitSince = -1f;
				moveFailed = true;
				gaveUp = true;
				TestLog.Count("Walks given up for poison, by job", ((job != null) ? LogJobName() : "village life"));
			}
			return(true);
		}

		/// <summary>
		/// Asks the vikings standing about in front of the viking to make way, once it has got no nearer where it's
		/// going for MakeWayAfter seconds, and no more often than that
		/// </summary>
		/// <param name="toward">Where it's walking next</param>
		private void AskWayWhenHeldUp(Vector3 toward)
		{
			if (Time.time - stuckSince < MakeWayAfter || Time.time < nextAskWay)
			{
				return;
			}
			nextAskWay = Time.time + MakeWayAfter;
			Vector3 heading = toward - transform.position;
			heading.y = 0f;
			if (heading.sqrMagnitude < 0.01f)
			{
				return;
			}
			heading.Normalize();
			foreach (VillageLife other in All)
			{
				if (other == this || !other.active)
				{
					continue;
				}
				Vector3 offset = other.transform.position - transform.position;
				offset.y = 0f;
				float ahead = Vector3.Dot(offset, heading);
				if (ahead < 0f || ahead > MakeWayReach || Mathf.Abs(heading.x * offset.z - heading.z * offset.x) > MakeWayWidth)
				{
					continue;
				}
				other.MakeWay(transform.position, heading);
			}
		}

		/// <summary>
		/// Has the viking step aside for another walking past, when it's only standing about, strolling, talking,
		/// petting a boar or standing in the ring at the fire - out to the side of the other's way, to the side it's on
		/// already, onto open ground
		/// </summary>
		/// <param name="from">Where the other viking is</param>
		/// <param name="heading">The way the other is walking</param>
		public void MakeWay(Vector3 from, Vector3 heading)
		{
			if (!active || attachPoint != null || ai.GetTargetCreature() != null || Time.time < makeWayUntil)
			{
				return;
			}
			bool idle = (job is IdleJob || job is StrollJob || job is TalkJob || job is PetJob || (job is GatherJob gather && !gather.Sitting()));
			if (!idle)
			{
				return;
			}
			heading.y = 0f;
			if (heading.sqrMagnitude < 0.01f)
			{
				return;
			}
			heading.Normalize();
			Vector3 offset = transform.position - from;
			Vector3 side = new Vector3(-heading.z, 0f, heading.x);
			if (Vector3.Dot(offset, side) < 0f)
			{
				side = -side;
			}
			// To its own side of the other's way first, then the other side, then on ahead out of the way
			Vector3[] tries = new[] { transform.position + side * MakeWayStep, transform.position - side * MakeWayStep, transform.position + heading * MakeWayStep * 1.5f };
			foreach (Vector3 point in tries)
			{
				if (NavmeshPoints.FindValidPoint(out Vector3 spot, point, 1f, Pathfinding.AgentType.Humanoid) && Utils.DistanceXZ(spot, from) > Utils.DistanceXZ(transform.position, from) - 0.5f)
				{
					makeWaySpot = spot;
					makeWayUntil = Time.time + MakeWayTime;
					return;
				}
			}
		}

		/// <summary>
		/// Walks the viking aside while it's making way for another (see MakeWay), putting its job off
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True while it's stepping aside, so the AI's own update is skipped</returns>
		private bool UpdateMakeWay(float dt)
		{
			if (Time.time >= makeWayUntil)
			{
				return(false);
			}
			if (Go(dt, makeWaySpot, 0.4f, false))
			{
				makeWayUntil = 0f;
				moveFailed = false;
				ai.StopMoving();
				return(false);
			}
			return(true);
		}

		/// <summary>
		/// Notes whether the viking is getting anywhere on its way to a spot - nearer it, or at least moving, since the
		/// way can lead away from the spot first, like around a house to its door - jumping whatever low thing is in its
		/// way when it has stood still for HopAfter seconds and there's no way around, and giving up when it has for
		/// StuckTime seconds
		/// </summary>
		/// <param name="target">Where it's walking</param>
		/// <returns>True when it gave up</returns>
		private bool CheckStuck(Vector3 target)
		{
			float distance = Utils.DistanceXZ(transform.position, target);
			if (Vector3.Distance(target, stuckTarget) > 0.5f)
			{
				hops = 0;
			}
			bool moved = (Utils.DistanceXZ(transform.position, stuckFrom) >= StuckMove);
			if (Vector3.Distance(target, stuckTarget) > 0.5f || distance < stuckBest - 0.5f || moved)
			{
				stuckTarget = target;
				stuckBest = distance;
				stuckFrom = transform.position;
				stuckSince = Time.time;
				doorTried = false;
				return(false);
			}
			// A door beside it, standing open across its way or shut in front of it, is opened or closed once before it
			// tries jumping anything
			if (!doorTried && Time.time - stuckSince >= HopAfter && VikingDoors.Unstick(humanoid))
			{
				doorTried = true;
				stuckSince = Time.time;
				return(false);
			}
			if (Time.time - stuckSince >= HopAfter && hops < HopsMax && TryHop(target, false))
			{
				hops++;
				stuckSince = Time.time;
				return(false);
			}
			if (Time.time - stuckSince < StuckTime)
			{
				return(false);
			}
			ai.StopMoving();
			moveFailed = true;
			TestLog.Count("Walks given up, by job", LogJobName());
			ResetStuck();
			return(true);
		}

		/// <summary>
		/// Forgets how near the viking got to where it was walking
		/// </summary>
		private void ResetStuck()
		{
			stuckTarget = Vector3.positiveInfinity;
			stuckBest = float.MaxValue;
			stuckFrom = transform.position;
			stuckSince = Time.time;
			hops = 0;
			doorTried = false;
		}

		/// <summary>
		/// Keeps a viking out of a root's reach, stepping back out when it's within RootKeepAway, even in a fight - one
		/// fighting hand to hand calls an archer over when a root is within RootCallRange, every ArcherCallInterval
		/// seconds, and an archer keeps the root as its target as it backs off, so it shoots it from out of its reach
		/// </summary>
		/// <returns>True while it's stepping away, so the AI's own update is skipped</returns>
		private bool UpdateRoots()
		{
			bool archer = HasBow(humanoid);
			Character root = NearestRoot(archer ? RootKeepAway : RootCallRange);
			if (root == null)
			{
				return(false);
			}
			if (!archer)
			{
				CallArcher(root);
			}
			Vector3 away = transform.position - root.transform.position;
			away.y = 0f;
			if (away.magnitude > RootKeepAway)
			{
				return(false);
			}
			if (away.sqrMagnitude < 0.01f)
			{
				away = -transform.forward;
			}
			if (!archer && ai.m_targetCreature == root)
			{
				ai.m_targetCreature = null;
			}
			Interrupt("keeping clear of a root");
			mover = (archer ? "backing off from a root to shoot it" : "keeping clear of a root");
			ai.MoveTowards(away.normalized, true);
			return(true);
		}

		/// <summary>
		/// Finds the nearest root hostile to the viking within a range of it
		/// </summary>
		/// <param name="range">The range, in meters</param>
		/// <returns>The root, or null when there's none</returns>
		private Character NearestRoot(float range)
		{
			List<Character> near = new List<Character>();
			Character.GetCharactersInRange(transform.position, range, near);
			Character nearest = null;
			float best = float.MaxValue;
			foreach (Character other in near)
			{
				if (other == humanoid || other.IsDead() || !MonsterThreat.IsRoot(other) || !BaseAI.IsEnemy(other, humanoid))
				{
					continue;
				}
				float distance = Utils.DistanceXZ(other.transform.position, transform.position);
				if (distance < best)
				{
					nearest = other;
					best = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Calls the village's nearest archer this client runs over to shoot a root, at most every ArcherCallInterval
		/// seconds - giving it the root as its target - with a shout, or a warning to keep clear when there's no archer
		/// </summary>
		/// <param name="root">The root</param>
		private void CallArcher(Character root)
		{
			if (Time.time - lastArcherCall < ArcherCallInterval)
			{
				return;
			}
			lastArcherCall = Time.time;
			VillageLife archer = null;
			float best = float.MaxValue;
			foreach (VillageLife other in All)
			{
				if (other == null || other == this || other.village != village || !other.active || other.humanoid.IsDead() || !HasBow(other.humanoid))
				{
					continue;
				}
				float distance = Utils.DistanceXZ(other.transform.position, root.transform.position);
				if (distance < best)
				{
					archer = other;
					best = distance;
				}
			}
			string[] lines = ((archer != null) ? ArcherCalls : RootWarnings);
			VikingSpeech.Chatter(humanoid, lines[Random.Range(0, lines.Length)], VikingSpeech.TalkTime);
			if (archer != null && archer.ai.m_targetCreature != root)
			{
				archer.ai.m_targetCreature = root;
				archer.ai.SetAlerted(true);
			}
		}

		/// <summary>
		/// Has the viking run out of a fire's flames when its body gets within FlameStep of them, straight away from the
		/// fire's middle, even in a fight, until it's FlameClear clear of them
		/// </summary>
		/// <returns>True while it's running out, so the AI's own update is skipped</returns>
		private bool UpdateFlames()
		{
			if (attachPoint != null)
			{
				escapingFlames = false;
				return(false);
			}
			if (!escapingFlames && Time.time < nextFlameCheck)
			{
				return(false);
			}
			nextFlameCheck = Time.time + FlameCheckInterval;
			float radius = humanoid.GetRadius() + (escapingFlames ? FlameClear : FlameStep);
			if (!FireSafety.FlamesNear(humanoid.GetCenterPoint(), radius, out Vector3 middle, out float _))
			{
				escapingFlames = false;
				return(false);
			}
			Vector3 away = transform.position - middle;
			away.y = 0f;
			if (away.sqrMagnitude < 0.01f)
			{
				away = -transform.forward;
			}
			escapingFlames = true;
			Interrupt("flames");
			mover = "running out of the flames";
			ai.MoveTowards(away.normalized, true);
			return(true);
		}

		/// <summary>
		/// Has the viking step away from a poison cloud or a zombie's corpse that might leave one when it's too near,
		/// running straight away from it, even in a fight. It keeps its job, picking it up again once it's clear, unless
		/// it's sitting, lying or on a seat, which it has to get up from
		/// </summary>
		/// <returns>True while it's stepping away, so the AI's own update is skipped</returns>
		private bool UpdatePoison()
		{
			if (!VillageHazards.NearestPoison(transform.position, PoisonAvoid, out Vector3 poison))
			{
				steppingFromPoison = false;
				return(false);
			}
			Vector3 away = transform.position - poison;
			away.y = 0f;
			if (away.sqrMagnitude < 0.01f)
			{
				away = -transform.forward;
			}
			if (attachPoint != null || VikingPoses.SettledDown(humanoid, out bool _))
			{
				Interrupt("poison");
			}
			if (!steppingFromPoison)
			{
				steppingFromPoison = true;
				TestLog.Count("Stepped away from poison, by job", ((job != null) ? LogJobName() : "none"));
			}
			mover = "stepping away from poison";
			ai.MoveTowards(away.normalized, true);
			return(true);
		}

		/// <summary>
		/// Describes the viking's village life for the debug hover text - its village and home, what it's doing, its
		/// bedtime at night once it's picked, what has it on edge, and the loot lying around the village
		/// </summary>
		/// <remarks>
		/// Its job is only known on the client that owns it, which runs its life
		/// </remarks>
		/// <returns>The lines, each ending in a line break</returns>
		public string DebugSummary()
		{
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			if (!active)
			{
				text.Append("Village life: off" + ((village == null) ? ", no village" : "") + "\n");
				DescribeRoad(text);
				return(text.ToString());
			}
			ZDO zdo = nview.GetZDO();
			text.Append("Village: " + village.Farm.ToString("F0") + ", home " + zdo.GetVec3(Village.HomeKey, Vector3.zero).ToString("F0") + "\n");
			if (job != null)
			{
				text.Append("Job: " + job.Describe() + ", for " + (Time.time - job.StartedAt).ToString("F0") + "s\n");
			}
			else
			{
				text.Append("Job: none\n");
			}
			// Tonight's bedtime is picked the first time it's asked for at night (see Bedtime)
			if (IsNight() && SleepSkip.SleptThroughNight())
			{
				text.Append("Bedtime: none, the night was slept through\n");
			}
			else if (IsNight() && bedAt >= 0f)
			{
				float left = bedAt - Time.time;
				text.Append("Bedtime: " + ((left > 0f) ? "in " + left.ToString("F0") + "s" : "passed " + (-left).ToString("F0") + "s ago") + "\n");
			}
			if (Time.time - stillSince >= StillShowTime)
			{
				text.Append("Standing still for " + (Time.time - stillSince).ToString("F0") + "s" + (inBed ? ", in bed" : ((attachPoint != null) ? ", seated" : "")) + "\n");
			}
			text.Append(VillageBeds.DescribeFor(zdo, village));
			CountTwitches(out int starts, out int reversals);
			string lastMover = ((moveSamples.Count > 0) ? moveSamples.ToArray()[moveSamples.Count - 1].Mover : "nothing");
			text.Append("Moved by: " + lastMover + ((starts >= TwitchStarts || reversals >= TwitchReversals) ? ", twitching" : "") + " (" + starts + " starts, " + reversals + " turn-backs in " + TwitchWindow.ToString("F0") + "s)" + ((steeredAround != null && Time.time - steeredAt < SteeredShowTime) ? ", steering around " + steeredAround.GetHoverName() : "") + "\n");
			text.Append("Eaten: " + body.Food.Describe() + "\n");
			text.Append("Rest: " + body.Rest.Describe() + "\n");
			text.Append("Powers: " + body.Powers.Describe() + "\n");
			List<string> flags = new List<string>();
			if (IsWatch())
			{
				flags.Add("night watch");
			}
			if (waitingFor != ZDOID.None)
			{
				flags.Add("waiting for a corpse to rise");
			}
			if (RaidReady())
			{
				flags.Add("ready for a raid");
			}
			else if (VillageRaids.On(village))
			{
				flags.Add("raid on, not noticed yet");
			}
			if (returning)
			{
				flags.Add("walking back to the village");
			}
			if (fleeing)
			{
				flags.Add("running home (" + fleeReason.ToString().ToLowerInvariant() + ")");
			}
			if (hangingBack)
			{
				flags.Add("hanging back, badly hurt" + ((Time.time < runUntil) ? ", running from an enemy" : ((flanked != null) ? ", working behind " + flanked.GetHoverName() : "")));
			}
			if (gettingOffRoof)
			{
				flags.Add("getting off a roof");
			}
			if (dulled)
			{
				flags.Add("senses dulled");
			}
			if (Time.time < noWoodUntil)
			{
				flags.Add("no wood for " + Mathf.CeilToInt(noWoodUntil - Time.time) + "s");
			}
			if (grudge != null && Time.time <= grudgeUntil)
			{
				flags.Add("grudge against " + grudge.GetHoverName());
			}
			if (flags.Count > 0)
			{
				text.Append(string.Join(", ", flags) + "\n");
			}
			text.Append(DebugLoot());
			return(text.ToString());
		}

		/// <summary>
		/// Names what the viking is doing, for its name in debug mode
		/// </summary>
		/// <remarks>
		/// Its job is only known on the client that owns it, which runs its life
		/// </remarks>
		/// <returns>Its job, or null when it has none or its life doesn't run here</returns>
		public string DebugJob()
		{
			return((active && job != null) ? job.Describe() : null);
		}

		/// <summary>
		/// Describes the loot lying around the village for the debug hover text - how much there is and why the viking
		/// leaves what it leaves
		/// </summary>
		/// <returns>The line, ending in a line break</returns>
		private string DebugLoot()
		{
			List<Container> chests = UsableChests();
			bool mayLeave = MayLeave();
			int lying = 0;
			int settling = 0;
			int far = 0;
			int avoided = 0;
			int claimed = 0;
			int noRoom = 0;
			int wanted = 0;
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				ZNetView view = instance.Value;
				ItemDrop drop = ((view != null && view.IsValid()) ? view.GetComponent<ItemDrop>() : null);
				if (drop == null || village.DistanceOutside(view.transform.position) > LootReach + DebugLootMargin)
				{
					continue;
				}
				lying++;
				if (!IsLoot(view))
				{
					settling++;
				}
				else if (village.DistanceOutside(view.transform.position) > (mayLeave ? LootReach : 0f))
				{
					far++;
				}
				else if (Avoided(instance.Key.m_uid))
				{
					avoided++;
				}
				else if (workClaims.TryGetValue(instance.Key.m_uid, out VillageLife holder) && holder != this && holder != null && holder.job != null)
				{
					claimed++;
				}
				else if (!WantsLoot(drop.m_itemData, chests, mayLeave) || CarryRoom(drop.m_itemData, CarryLoot) <= 0)
				{
					noRoom++;
				}
				else
				{
					wanted++;
				}
			}
			if (lying == 0)
			{
				return("Loot: none, " + chests.Count + " usable chests\n");
			}
			return("Loot: " + lying + " lying, " + wanted + " to take, " + chests.Count + " usable chests - left: " + settling + " not loot or settling, " + far + " too far, " + avoided + " unreachable, " + claimed + " taken by others, " + noRoom + " no room\n");
		}

		/// <summary>
		/// Checks whether the viking is waiting for a corpse to rise, aiming at a bird or prey, or ready for a raid on its
		/// village, which keeps its weapon drawn
		/// </summary>
		/// <returns>True while it is</returns>
		public bool IsWary()
		{
			return(waitingFor != ZDOID.None || aimingBow || RaidReady());
		}

		/// <summary>
		/// Checks whether the viking is ready for a raid on its village - one is on, and it has noticed it, awake
		/// </summary>
		/// <returns>True while it is</returns>
		private bool RaidReady()
		{
			return(active && raidNoticed && !inBed && !dulled && VillageRaids.On(village));
		}

		/// <summary>
		/// Has the viking notice a raid on its village, once it's awake - dropping what it's doing unless it's something
		/// that can't wait (see RaidCritical), so it keeps guard instead (see ChooseJob), with the night watch roaring
		/// the alarm that wakes the village at night, and a viking calling it out by day, at most every
		/// RaidCallInterval for each village - and forget it once the raid is over
		/// </summary>
		private void UpdateRaid()
		{
			if (!VillageRaids.On(village))
			{
				raidNoticed = false;
				return;
			}
			if (raidNoticed || inBed || dulled)
			{
				return;
			}
			raidNoticed = true;
			string name = VillageNames.Get(village);
			if (IsWatch() && (IsNight() || IsDusk()))
			{
				lastAlarm = Time.time;
				ai.StopMoving();
				VikingPoses.Roar(humanoid);
				VikingSpeech.SayFirst(humanoid, VikingSpeech.RaidAlarm(name), VikingSpeech.AlarmTime, VikingSpeech.AlarmCull);
				VillageSignals.SendAlarm(village);
			}
			else if (!(lastRaidCall.TryGetValue(village.Zdo.m_uid, out float last) && Time.time - last < RaidCallInterval))
			{
				lastRaidCall[village.Zdo.m_uid] = Time.time;
				VikingSpeech.SayFirst(humanoid, VikingSpeech.RaidCall(name), VikingSpeech.CallTime, VikingSpeech.CallCull);
			}
			if (job != null && !RaidCritical())
			{
				Interrupt("a raid");
			}
		}

		/// <summary>
		/// Picks what the viking does through a raid on its village - keeping watch at night when it's the watch, taking
		/// done food off a cooking station, eating, or resting by the fire, while it's wounded, or else keeping guard
		/// </summary>
		/// <returns>The job</returns>
		private Job RaidJob()
		{
			if (IsNight() && IsWatch())
			{
				return(new WatchJob(this));
			}
			// The stations stand over the fire the village keeps guard around, so done food is taken off before it burns
			Job tend = FindTendWork(true, true);
			if (tend != null)
			{
				return(tend);
			}
			if (Wounded())
			{
				Container pantry = ((Time.time >= eatAgainAt) ? FindFoodChest(false) : null);
				if (pantry != null)
				{
					return(new EatJob(this, pantry, false));
				}
				if (!body.Rest.IsRested() && Time.time >= restAgainAt && village.GatherFires().Count > 0)
				{
					return(new GatherJob(this, true));
				}
			}
			return(new GuardJob(this));
		}

		/// <summary>
		/// Checks whether the viking's job is one it keeps at through a raid - keeping guard or watch, eating or resting
		/// by the fire while it's wounded, or lightening its load
		/// </summary>
		/// <returns>True when it is</returns>
		private bool RaidCritical()
		{
			return(job is GuardJob || job is WatchJob || job is EatJob || job is UnburdenJob || (job is GatherJob gather && gather.Heals()));
		}

		/// <summary>
		/// Has the viking wait for a corpse near it to rise as a zombie, rather than go about its day, when it has
		/// nothing to fight and isn't resting - it walks up to the corpse, stands a few meters off facing it, and keeps
		/// its weapon drawn, saying farewell when it's the corpse of one of its village (see SayFarewell)
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True while it waits, so its job is put off</returns>
		private bool UpdateWary(float dt)
		{
			if (job != null && job.Resting())
			{
				return(false);
			}
			if (!VillageHazards.NearestRising(transform.position, WaryRange, out VillageHazards.Rising corpse) || village.DistanceOutside(corpse.Position) > LeashRange)
			{
				waitingFor = ZDOID.None;
				return(false);
			}
			if (job != null || attachPoint != null)
			{
				Interrupt("a corpse about to rise");
			}
			waitingFor = corpse.Id;
			float distance = Utils.DistanceXZ(transform.position, corpse.Position);
			if (distance > WaitFar)
			{
				Go(dt, corpse.Position, (WaitNear + WaitFar) / 2f, false);
				moveFailed = false;
				return(true);
			}
			if (distance < WaitNear)
			{
				Vector3 away = transform.position - corpse.Position;
				away.y = 0f;
				mover = "backing away from a corpse";
				ai.MoveTowards(((away.sqrMagnitude < 0.01f) ? -transform.forward : away.normalized), false);
				return(true);
			}
			Face(corpse.Position);
			SayFarewell(corpse.Id);
			return(true);
		}

		/// <summary>
		/// Has the viking say farewell to one of its village whose corpse it stands ready by, about to rise - the first
		/// viking on this client to stand ready by it
		/// </summary>
		/// <param name="corpse">The corpse's ZDO</param>
		private void SayFarewell(ZDOID corpse)
		{
			if (farewelled.Contains(corpse))
			{
				return;
			}
			ZDO zdo = ((ZDOMan.instance != null) ? ZDOMan.instance.GetZDO(corpse) : null);
			if (!VillageFallen.IsOf(zdo, village))
			{
				return;
			}
			if (farewelled.Count >= CorpseMemory)
			{
				farewelled.Clear();
			}
			farewelled.Add(corpse);
			VikingSpeech.Chatter(humanoid, VikingSpeech.RisingFarewell(VikingNames.Get(zdo)), VikingSpeech.TalkTime);
		}

		/// <summary>
		/// Sends the viking after the zombie of the corpse it was waiting for, as soon as it has risen
		/// </summary>
		private void UpdateRisen()
		{
			if (waitingFor == ZDOID.None || !VillageHazards.HasRisen(waitingFor, out Character zombie))
			{
				return;
			}
			waitingFor = ZDOID.None;
			if (zombie != null && !zombie.IsDead() && BaseAI.IsEnemy(humanoid, zombie))
			{
				Engage(zombie);
			}
		}

		/// <summary>
		/// Gets the viking out of a pen it finds itself in with no danger near and nothing to do there - not foraging
		/// or petting a boar - by walking for the village's rally point, which jumps the fence when there's no way out
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True while it's getting out, so the AI's own update is skipped</returns>
		private bool UpdateOutOfPen(float dt)
		{
			// A viking that went into a pen for wood has its reason to be there until it's back out
			if (inPenForWood && !woodInPen && Time.time >= nextPenCheck)
			{
				nextPenCheck = Time.time + PenCheckInterval;
				if (!Village.FencedIn(transform.position))
				{
					inPenForWood = false;
				}
			}
			if (viking == null || !viking.NoDangerNear() || GoesIntoPens() || attachPoint != null)
			{
				gettingOutOfPen = false;
				return(false);
			}
			if (!gettingOutOfPen)
			{
				if (Time.time < nextPenCheck)
				{
					return(false);
				}
				nextPenCheck = Time.time + PenCheckInterval;
				if (!Village.FencedIn(transform.position))
				{
					return(false);
				}
				LogEscape("out of a pen", village.RallyPoint());
				Interrupt("a pen");
				gettingOutOfPen = true;
				ResetStuck();
			}
			Go(dt, village.RallyPoint(), 1f, false);
			if (moveFailed || !Village.FencedIn(transform.position))
			{
				moveFailed = false;
				gettingOutOfPen = false;
				return(false);
			}
			return(true);
		}

		/// <summary>
		/// Gets the viking off a roof it finds itself on out of a fight, to the nearest ground within RoofExitSearch
		/// with a way to the village's open ground (see FreeSpot) - climbing straight down when that's right below it or
		/// beside it, or else stopping what it's doing and walking there, and climbing down to it when walking doesn't
		/// get it off. With no such ground, it gives up for a while
		/// </summary>
		/// <remarks>
		/// A roof that comes down to the ground, like a farmhouse's, can be walked up, but the ground a viking stands
		/// over up there - the house's floor under the roof, or the ground at its edge - often can't be walked down to
		/// </remarks>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True while it's getting off, so the AI's own update is skipped</returns>
		private bool UpdateOffRoof(float dt)
		{
			if (viking == null || !viking.AvoidsRoofs())
			{
				gettingOffRoof = false;
				return(false);
			}
			if (!gettingOffRoof)
			{
				if (Time.time < nextRoofCheck || attachPoint != null)
				{
					return(false);
				}
				nextRoofCheck = Time.time + RoofCheckInterval;
				if (!NavmeshRoofs.OnRoof(transform.position) || Pathfinding.instance == null)
				{
					return(false);
				}
				// Roofs are left out of the viking's navmesh here, so the spots found are off the roof
				if (!FreeSpot(transform.position, village.RallyPoint(), RoofExitSearch, out roofExit) || Utils.DistanceXZ(roofExit, transform.position) > RoofExitSearch)
				{
					nextRoofCheck = Time.time + RoofGiveUpTime;
					return(false);
				}
				if (ClimbsOffRoof(transform.position, roofExit))
				{
					ClimbOffRoof("stood right over it");
					return(false);
				}
				LogEscape("off " + RoofPiece(transform.position), roofExit);
				Interrupt("a roof");
				gettingOffRoof = true;
				ResetStuck();
			}
			if (!NavmeshRoofs.OnRoof(transform.position))
			{
				gettingOffRoof = false;
				return(false);
			}
			// Go is done when the viking is above the spot or gave up walking, either way still up on the roof
			if (Go(dt, roofExit, 0.5f, false))
			{
				gettingOffRoof = false;
				moveFailed = false;
				ClimbOffRoof("couldn't walk there");
				return(false);
			}
			return(true);
		}

		/// <summary>
		/// Climbs the viking down from the roof it's on to the ground found for it (see UpdateOffRoof)
		/// </summary>
		/// <param name="why">Why it climbs rather than walks, for the log</param>
		private void ClimbOffRoof(string why)
		{
			Vector3 from = transform.position;
			Vector3 down = roofExit + Vector3.up * 0.1f;
			transform.position = down;
			humanoid.m_body.position = down;
			humanoid.m_body.linearVelocity = Vector3.zero;
			ai.StopMoving();
			ResetStuck();
			TestLog.Count("Troubles", "climbed down off a roof");
			if (TestLog.On || Player.m_debugMode)
			{
				Plugin.Log.LogInfo("Climb: " + TestLog.Name(humanoid, "F1") + " was on a roof " + (from.y - roofExit.y).ToString("F1") + "m above the ground found for it and " + why + ", so it climbs down to " + down.ToString("F1") + ", " + Utils.DistanceXZ(from, down).ToString("F1") + "m away, at its job: " + ((job != null) ? job.Describe() : "none"));
			}
		}

		/// <summary>
		/// Checks whether a viking on a roof climbs straight down to the ground found for it rather than walking there -
		/// when the ground is within ArriveReach of it seen from above, which walking would take for there already, or
		/// well below it close by (see Stranded)
		/// </summary>
		/// <param name="at">Where it stands</param>
		/// <param name="exit">The ground found for it</param>
		/// <returns>True when it climbs</returns>
		internal static bool ClimbsOffRoof(Vector3 at, Vector3 exit)
		{
			float dx = at.x - exit.x;
			float dz = at.z - exit.z;
			return(dx * dx + dz * dz <= ArriveReach * ArriveReach || Stranded(at, exit));
		}

		/// <summary>
		/// Gets the viking out of the water when it stands about in it with no danger near - in the water at two checks
		/// WaterCheckInterval apart, having moved less than WaterStillDistance between them, so one wading across a
		/// stream on its way somewhere keeps going. It stops what it's doing and walks to the nearest dry ground, giving
		/// up for a while when it finds none or can't get there
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True while it's getting out, so the AI's own update is skipped</returns>
		private bool UpdateOutOfWater(float dt)
		{
			if (attachPoint != null)
			{
				gettingOutOfWater = false;
				return(false);
			}
			if (!gettingOutOfWater)
			{
				if (Time.time < nextWaterCheck)
				{
					return(false);
				}
				nextWaterCheck = Time.time + WaterCheckInterval;
				bool inWater = humanoid.InWater();
				bool still = (wasInWater && inWater && Utils.DistanceXZ(transform.position, waterCheckAt) < WaterStillDistance);
				wasInWater = inWater;
				waterCheckAt = transform.position;
				if (!still)
				{
					return(false);
				}
				wasInWater = false;
				if (!FindDryGround(out waterExit))
				{
					nextWaterCheck = Time.time + WaterGiveUpTime;
					return(false);
				}
				Interrupt("standing in water");
				gettingOutOfWater = true;
				ResetStuck();
			}
			if (Go(dt, waterExit, 0.5f, false))
			{
				gettingOutOfWater = false;
				if (moveFailed)
				{
					moveFailed = false;
					nextWaterCheck = Time.time + WaterGiveUpTime;
				}
				return(false);
			}
			return(true);
		}

		/// <summary>
		/// Runs the viking to the water to put itself out when it's on fire and there's water within DouseReach - even in
		/// a fight, which waits until it's out. In the water it's soaked, which puts the fire out
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <returns>True while it runs for the water, so the AI's own update is skipped</returns>
		private bool UpdateDousing(float dt)
		{
			if (attachPoint != null || !humanoid.GetSEMan().HaveStatusEffect(SEMan.s_statusEffectBurning) || humanoid.InWater())
			{
				dousing = false;
				return(false);
			}
			if (!dousing)
			{
				if (Time.time < nextDouseCheck)
				{
					return(false);
				}
				nextDouseCheck = Time.time + DouseRetryTime;
				if (!FindWater(out douseSpot))
				{
					return(false);
				}
				Interrupt("on fire");
				dousing = true;
				ResetStuck();
			}
			if (Go(dt, douseSpot, 0.5f, true))
			{
				dousing = false;
				moveFailed = false;
				return(false);
			}
			return(true);
		}

		/// <summary>
		/// Finds the nearest water within DouseReach of the viking deep enough to soak it, that it can get to on the
		/// navmesh - never the boiling sea around the Ashlands (see DangerousGround)
		/// </summary>
		/// <param name="water">The spot found</param>
		/// <returns>False when there's none</returns>
		private bool FindWater(out Vector3 water)
		{
			water = Vector3.zero;
			if (Pathfinding.instance == null || ZoneSystem.instance == null)
			{
				return(false);
			}
			float deep = ZoneSystem.instance.m_waterLevel - DouseDepth;
			for (float distance = WaterExitStep; distance <= DouseReach; distance += WaterExitStep)
			{
				for (int i = 0; i < WaterExitDirections; i++)
				{
					Vector3 point = transform.position + Quaternion.Euler(0f, i * 360f / WaterExitDirections, 0f) * Vector3.forward * distance;
					if (ZoneSystem.instance.GetGroundHeight(point) > deep || DangerousGround.IsDangerous(point) || !NavmeshPoints.FindValidPoint(out Vector3 valid, point, WaterExitStep / 2f, Pathfinding.AgentType.Humanoid))
					{
						continue;
					}
					if (ZoneSystem.instance.GetGroundHeight(valid) <= deep)
					{
						water = valid;
						return(true);
					}
				}
			}
			return(false);
		}

		/// <summary>
		/// Finds the nearest dry ground within WaterExitSearch of the viking that it can walk to - on the navmesh, outside
		/// the pens and above the water level by DryHeight
		/// </summary>
		/// <param name="dry">The spot found</param>
		/// <returns>False when there's none</returns>
		private bool FindDryGround(out Vector3 dry)
		{
			dry = Vector3.zero;
			if (Pathfinding.instance == null || ZoneSystem.instance == null)
			{
				return(false);
			}
			float water = ZoneSystem.instance.m_waterLevel + DryHeight;
			for (float distance = WaterExitStep; distance <= WaterExitSearch; distance += WaterExitStep)
			{
				for (int i = 0; i < WaterExitDirections; i++)
				{
					Vector3 point = transform.position + Quaternion.Euler(0f, i * 360f / WaterExitDirections, 0f) * Vector3.forward * distance;
					if (ZoneSystem.instance.GetGroundHeight(point) < water || !NavmeshPoints.FindValidPoint(out Vector3 valid, point, WaterExitStep / 2f, Pathfinding.AgentType.Humanoid))
					{
						continue;
					}
					if (valid.y >= water && !Village.FencedIn(valid))
					{
						dry = valid;
						return(true);
					}
				}
			}
			return(false);
		}

		/// <summary>
		/// Notes how near the viking has got to the enemy it's after, and jumps whatever low thing is between them when
		/// it hasn't got nearer for HopAfter seconds and there's no way around - so a viking in a pen gets out to fight.
		/// A viking fighting hand to hand with a pen's fence between it and the enemy goes over the fence for up to
		/// FenceChaseTime seconds rather than swinging through it (see ChaseOverFence). One slaughtering livestock never
		/// strikes it through a fence, with a bow or not - it keeps going over for SlaughterFenceTime seconds, then gives
		/// the animal up
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="target">The enemy</param>
		/// <returns>True when it jumped, is on its way over the fence or gave up the slaughter, so the AI's own update is
		/// skipped</returns>
		private bool UpdateChase(float dt, Character target)
		{
			float distance = Utils.DistanceXZ(transform.position, target.transform.position);
			if (target != chased)
			{
				fenceSince = Time.time;
				nextFenceCheck = 0f;
			}
			HuntJob slaughter = ((job is HuntJob hunt && hunt.Slaughters() && hunt.IsPreyOf(target)) ? hunt : null);
			if ((slaughter != null || Time.time - fenceSince < FenceChaseTime) && FenceBetween(target, (slaughter != null)))
			{
				chased = target;
				if (slaughter != null && Time.time - fenceSince >= SlaughterFenceTime)
				{
					slaughter.GiveUp();
					ai.StopMoving();
					return(true);
				}
				return(ChaseOverFence(dt, target));
			}
			if (target != chased || distance < chaseBest - 0.5f || humanoid.InAttack())
			{
				chased = target;
				chaseBest = distance;
				chaseSince = Time.time;
				return(false);
			}
			if (Time.time - chaseSince < HopAfter)
			{
				return(false);
			}
			chaseSince = Time.time;
			return(TryHop(target.transform.position, false));
		}

		/// <summary>
		/// Checks whether a pen's fence stands between the viking and an enemy it would fight hand to hand - close by,
		/// with a building piece in the way at a fence's height (see PieceAcross) and one of them in a pen and the other
		/// not, or for livestock it slaughters, the animal in a pen - looking again every FenceCheckInterval seconds, and
		/// noting whether there's a way around it
		/// </summary>
		/// <remarks>
		/// A spot just outside a pen, between its fence and a house or the village's fence, can count as fenced in too,
		/// so livestock in a pen is taken to be apart from the viking whenever a piece stands between them
		/// </remarks>
		/// <param name="target">The enemy</param>
		/// <param name="slaughter">True for livestock the viking slaughters, which it looks for the fence for even when it
		/// can shoot over it</param>
		/// <returns>True when there is</returns>
		private bool FenceBetween(Character target, bool slaughter)
		{
			if (Time.time < nextFenceCheck)
			{
				return(fenceBetween);
			}
			nextFenceCheck = Time.time + FenceCheckInterval;
			fenceBetween = false;
			if (!slaughter && HasBow(humanoid))
			{
				return(false);
			}
			float length = Vector3.Distance(transform.position, target.transform.position);
			if (length < 0.1f || length > FenceReach || !PieceAcross(transform.position, target.transform.position))
			{
				return(false);
			}
			bool targetPenned = Village.FencedIn(target.transform.position);
			fenceBetween = ((slaughter && targetPenned) || targetPenned != Village.FencedIn(transform.position));
			fencePath = (fenceBetween && Pathfinding.instance != null && Pathfinding.instance.HavePath(transform.position, target.transform.position, Pathfinding.AgentType.Humanoid));
			return(fenceBetween);
		}

		/// <summary>
		/// Checks whether a building piece stands between two spots on the ground at a fence's height - looking low, in
		/// the middle and high on a fence, so the gaps between its rails don't hide it
		/// </summary>
		/// <param name="from">One spot, on the ground</param>
		/// <param name="to">The other, on the ground</param>
		/// <returns>True when one does</returns>
		private static bool PieceAcross(Vector3 from, Vector3 to)
		{
			EnsureHopMask();
			foreach (float height in FenceHeights)
			{
				Vector3 start = from + Vector3.up * height;
				Vector3 line = to + Vector3.up * height - start;
				float length = line.magnitude;
				if (length < 0.1f)
				{
					return(false);
				}
				if (Physics.Raycast(start, line / length, out RaycastHit hit, length, hopMask, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<WearNTear>() != null)
				{
					return(true);
				}
			}
			return(false);
		}

		/// <summary>
		/// Takes the viking over a pen's fence to the enemy on the other side - by the way around when there is one, like
		/// a gate, or else straight at the fence and over it once it's right in front
		/// </summary>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="target">The enemy</param>
		/// <returns>True, so the AI's own update is skipped</returns>
		private bool ChaseOverFence(float dt, Character target)
		{
			if (fencePath)
			{
				mover = "going around a fence";
				ai.MoveTo(dt, target.transform.position, 0f, true);
				return(true);
			}
			if (TryHop(target.transform.position, true))
			{
				return(true);
			}
			mover = "going for a fence";
			Vector3 direction = target.transform.position - transform.position;
			direction.y = 0f;
			if (direction.sqrMagnitude > 0.01f)
			{
				ai.MoveTowards(direction.normalized, true);
			}
			return(true);
		}

		/// <summary>
		/// Sets up the layers a jump looks for obstacles on, the first time it's wanted
		/// </summary>
		private static void EnsureHopMask()
		{
			if (hopMask == 0)
			{
				hopMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece");
			}
		}

		/// <summary>
		/// How many times a viking shut in a pen tries to jump out before it climbs over instead - a pen whose ground
		/// inside lies lower than its fence's foot outside can have a fence too high to jump
		/// </summary>
		private const int PenHopTries = 3;

		/// <summary>
		/// The viking's jumps out of the pen it's shut in, for ClimbOutOfPen
		/// </summary>
		private int penHops;

		/// <summary>
		/// Checks whether something in the viking's way is a building piece it jumps when there's no way around it - like
		/// a fence or a low wall, but never a crafting station or its extension, a chest, bed, chair, fire or other
		/// furniture, or a roof or beam, any of which would only leave it stuck on top
		/// </summary>
		/// <param name="collider">What's in its way</param>
		/// <returns>True when it jumps it</returns>
		private static bool Hoppable(Collider collider)
		{
			WearNTear piece = collider.GetComponentInParent<WearNTear>();
			if (piece == null)
			{
				return(false);
			}
			if (piece.GetComponentInChildren<Container>() != null || piece.GetComponentInChildren<CraftingStation>() != null || piece.GetComponentInChildren<StationExtension>() != null || piece.GetComponentInChildren<Bed>() != null || piece.GetComponentInChildren<Chair>() != null || piece.GetComponentInChildren<Fireplace>() != null)
			{
				return(false);
			}
			Piece info = piece.GetComponent<Piece>();
			if (info != null && (info.m_category == Piece.PieceCategory.Furniture || info.m_category == Piece.PieceCategory.Crafting))
			{
				return(false);
			}
			return(HoppableName(Utils.GetPrefabName(piece.gameObject)));
		}

		/// <summary>
		/// Checks whether a building piece's name leaves it one the viking jumps - anything but a roof or a beam, which
		/// slope or stand up off the ground for it to end up stuck on
		/// </summary>
		/// <param name="name">The piece's prefab name</param>
		/// <returns>True when it does</returns>
		internal static bool HoppableName(string name)
		{
			return(!name.Contains("roof") && !name.Contains("beam"));
		}

		/// <summary>
		/// Climbs the viking over a pen's fence it's shut in by (see ShutIn), once it has tried to jump out more than
		/// PenHopTries times without getting out - putting it down on the far side, where its jump would land - rather
		/// than leaving it shut in, jumping and falling back, for as long as the fence is too high for it
		/// </summary>
		/// <param name="feet">Where it stands</param>
		/// <param name="landing">Where its jump would land, past the fence, on the ground</param>
		/// <param name="obstacle">The fence in its way</param>
		/// <returns>True when it climbed over</returns>
		private bool ClimbOutOfPen(Vector3 feet, Vector3 landing, RaycastHit obstacle)
		{
			bool shutIn = ShutIn(feet, landing, out Vector3 outside);
			if (!PenHopDue(ref penHops, shutIn))
			{
				return(false);
			}
			Vector3 over = outside + Vector3.up * 0.1f;
			transform.position = over;
			humanoid.m_body.position = over;
			humanoid.m_body.linearVelocity = Vector3.zero;
			ai.StopMoving();
			TestLog.Count("Troubles", "climbed out of a pen");
			if (TestLog.On || Player.m_debugMode)
			{
				WearNTear piece = obstacle.collider.GetComponentInParent<WearNTear>();
				Plugin.Log.LogInfo("Climb: " + TestLog.Name(humanoid, "F1") + " couldn't jump the " + ((piece != null) ? Utils.GetPrefabName(piece.gameObject) : "fence") + " at " + obstacle.point.ToString("F1") + " out of a pen, so it climbs over to " + over.ToString("F1"));
			}
			return(true);
		}

		/// <summary>
		/// Counts a viking's jump out of a pen toward climbing over its fence (see ClimbOutOfPen) - counting each jump it
		/// makes while it's shut in, however far apart, and starting over once it isn't
		/// </summary>
		/// <param name="hops">Its jumps out so far, counted on, or set back to none</param>
		/// <param name="shutIn">True when it's shut in as it jumps</param>
		/// <returns>True when it has jumped more than PenHopTries times without getting out, so it climbs over</returns>
		internal static bool PenHopDue(ref int hops, bool shutIn)
		{
			if (!shutIn)
			{
				hops = 0;
				return(false);
			}
			hops++;
			if (hops <= PenHopTries)
			{
				return(false);
			}
			hops = 0;
			return(true);
		}

		/// <summary>
		/// Checks whether the viking is shut in where it stands, with its jump the way out - in its village, when it has no
		/// whole path to the village's open ground (see Village.RallyPoint) and the navmesh where its jump would land
		/// does, which tells a pen too big to see as fenced in from its far side as well as a small one, and never takes
		/// it into one. Out of its village, when it stands fenced in and its jump would land outside (see
		/// Village.FencedIn)
		/// </summary>
		/// <param name="feet">Where it stands</param>
		/// <param name="landing">Where its jump would land, past the fence, on the ground</param>
		/// <param name="outside">Where it would be put down out of it - on the navmesh near the landing, when there's
		/// that</param>
		/// <returns>True when it's shut in</returns>
		private bool ShutIn(Vector3 feet, Vector3 landing, out Vector3 outside)
		{
			outside = landing;
			if (village == null || Pathfinding.instance == null || !village.Contains(feet))
			{
				return(Village.FencedIn(feet) && !Village.FencedIn(landing));
			}
			Vector3 open = village.RallyPoint();
			if (WayToOpenGround(feet, open))
			{
				return(false);
			}
			if (!NavmeshPoints.FindValidPoint(out Vector3 ground, landing, HopLanding, Pathfinding.AgentType.Humanoid) || !WayToOpenGround(ground, open))
			{
				return(false);
			}
			outside = ground;
			return(true);
		}

		/// <summary>
		/// Checks whether a spot loot is reached from lies in a pen by the way there - in the viking's village, open to the
		/// sky, with no whole path from it to the village's open ground (see Village.RallyPoint) while there is one from
		/// the viking - which tells the ground inside a pen too big to see as fenced in from its far side (see
		/// Village.FencedIn)
		/// </summary>
		/// <param name="stand">Where the viking would stand to reach the loot, on the navmesh</param>
		/// <returns>True when it lies in a pen</returns>
		private bool PennedByPaths(Vector3 stand)
		{
			if (village == null || Pathfinding.instance == null || !village.Contains(stand) || Village.Roofed(stand))
			{
				return(false);
			}
			Vector3 open = village.RallyPoint();
			return(WayToOpenGround(transform.position, open) && !WayToOpenGround(stand, open));
		}

		/// <summary>
		/// Checks whether there's a whole path from a spot to the village's open ground, which there isn't from inside a
		/// pen, however big
		/// </summary>
		/// <param name="from">The spot</param>
		/// <param name="open">The spot on the village's open ground (see Village.RallyPoint)</param>
		/// <returns>True when there is</returns>
		private static bool WayToOpenGround(Vector3 from, Vector3 open)
		{
			return(Pathfinding.instance.HavePath(from, open, Pathfinding.AgentType.Humanoid));
		}

		/// <summary>
		/// Starts the viking's jump over something low right in front of it on its way to a spot, like a fence, when
		/// there's no way around it - backing up and running at it, then jumping with its own jump (see UpdateHop) - or
		/// climbs out of a pen it can't jump out of (see ClimbOutOfPen)
		/// </summary>
		/// <param name="toward">Where it's going</param>
		/// <param name="fighting">True when it's going over a pen's fence to an enemy, which it jumps into the pen for,
		/// knowing there's no way around</param>
		/// <returns>True when it started</returns>
		private bool TryHop(Vector3 toward, bool fighting)
		{
			if (!fighting && Pathfinding.instance != null && Pathfinding.instance.HavePath(transform.position, toward, Pathfinding.AgentType.Humanoid))
			{
				return(false);
			}
			Vector3 direction = toward - transform.position;
			direction.y = 0f;
			if (direction.sqrMagnitude < 0.01f || !humanoid.IsOnGround())
			{
				return(false);
			}
			direction.Normalize();
			EnsureHopMask();
			Vector3 feet = transform.position;
			// Only a building piece like a fence is jumped - a rock is walked around, never climbed onto
			bool low = (Physics.Raycast(feet + Vector3.up * 0.4f, direction, out RaycastHit obstacle, HopReach, hopMask, QueryTriggerInteraction.Ignore) && Hoppable(obstacle.collider));
			bool high = Physics.Raycast(feet + Vector3.up * HopHeight, direction, HopReach + 0.5f, hopMask, QueryTriggerInteraction.Ignore);
			if (!low || high)
			{
				return(false);
			}
			// It jumps out of a pen it's shut in, but only into one for an enemy or its job
			Vector3 landing = feet + direction * (HopReach + HopLanding);
			landing.y = ZoneSystem.instance.GetSolidHeight(landing);
			if (!fighting && !GoesIntoPens() && !Village.FencedIn(feet) && Village.FencedIn(landing))
			{
				return(false);
			}
			if (ClimbOutOfPen(feet, landing, obstacle))
			{
				return(true);
			}
			// How high the fence's top stands, found from above just inside its face
			Vector3 probe = obstacle.point + direction * 0.1f;
			probe.y = feet.y + HopHeight + 0.5f;
			float top = HopHeight;
			if (Physics.Raycast(probe, Vector3.down, out RaycastHit topHit, HopHeight + 0.5f, hopMask, QueryTriggerInteraction.Ignore))
			{
				top = Mathf.Clamp(topHit.point.y - feet.y, 0.4f, HopHeight);
			}
			hopDirection = direction;
			hopPoint = obstacle.point;
			hopTop = top;
			hopLeftGround = false;
			hopPiece = Utils.GetPrefabName(obstacle.collider.GetComponentInParent<WearNTear>().gameObject);
			hopFenceTop = feet.y + top;
			// It only backs up for a run-up with no fire's flames within reach behind it, since it backs up without looking -
			// beside a fire it jumps from where it stands
			bool backUp = (Ahead() < RunUpDistance && !FireSafety.FlamesNear(feet, RunUpDistance + RunUpFireMargin, out Vector3 _, out float _));
			hopPhase = (backUp ? HopPhase.BackUp : HopPhase.RunUp);
			hopPhaseUntil = Time.time + (backUp ? BackUpTime : RunUpTime);
			if (Player.m_debugMode)
			{
				Plugin.Log.LogInfo("Hop: " + TestLog.Label(humanoid) + " at " + feet.ToString("F1") + " (" + ((job != null) ? job.Describe() : "no job") + ") takes a run-up to jump " + hopPiece + ", " + top.ToString("F2") + "m high at " + obstacle.point.ToString("F1") + ", toward " + toward.ToString("F1") + ", finding no path there" + (VikingDoors.IsCrossing(humanoid) ? ", while crossing a doorway" : ""));
			}
			return(true);
		}

		/// <summary>
		/// Carries on the viking's jump over a fence (see TryHop) - backing up to RunUpDistance from it, running at it,
		/// jumping when it's as far from it as it needs to be to clear it (see JumpDistance), and keeping on over it
		/// until it lands - giving up on a step that takes too long
		/// </summary>
		/// <returns>True while it's jumping, so the AI's own update is skipped</returns>
		private bool UpdateHop()
		{
			if (hopPhase == HopPhase.None)
			{
				return(false);
			}
			if (Time.time >= hopPhaseUntil && hopPhase != HopPhase.BackUp)
			{
				LogHopEnd((hopPhase == HopPhase.Air) ? ", still off the ground after " + HopAirTime.ToString("F1") + "s" : null);
				hopPhase = HopPhase.None;
				return(false);
			}
			if (hopPhase == HopPhase.BackUp)
			{
				mover = "backing up to jump a fence";
				if (Ahead() >= RunUpDistance || Time.time >= hopPhaseUntil)
				{
					hopPhase = HopPhase.RunUp;
					hopPhaseUntil = Time.time + RunUpTime;
				}
				else
				{
					ai.MoveTowards(-hopDirection, false);
				}
				return(true);
			}
			if (hopPhase == HopPhase.RunUp)
			{
				mover = "running at a fence";
				ai.MoveTowards(hopDirection, true);
				if (humanoid.IsOnGround() && Ahead() <= JumpDistance())
				{
					// A creature's jump carries it the way it faces, so it's turned to the fence first
					transform.rotation = Quaternion.LookRotation(hopDirection);
					humanoid.m_body.rotation = transform.rotation;
					hopTakeoff = transform.position;
					hopTakeoffAhead = Ahead();
					hopPeak = hopTakeoff.y;
					humanoid.Jump(true);
					hopPhase = HopPhase.Air;
					hopPhaseUntil = Time.time + HopAirTime;
				}
				return(true);
			}
			mover = "jumping a fence";
			ai.MoveTowards(hopDirection, true);
			hopPeak = Mathf.Max(hopPeak, transform.position.y);
			if (!humanoid.IsOnGround())
			{
				hopLeftGround = true;
			}
			else if (hopLeftGround)
			{
				LogHopEnd(null);
				hopPhase = HopPhase.None;
			}
			return(true);
		}

		/// <summary>
		/// Writes to the log, in test or debug mode, how the viking's jump over a fence ended - where it landed, whether
		/// that's past the fence, on top of it or back where it jumped from (see HopResult), how high its feet got
		/// against the fence's top and how far in front of the fence it left the ground - or that its run-up ran out
		/// before it jumped, counting each outcome
		/// </summary>
		/// <param name="note">What to add about how the jump ended, like still being in the air, or null</param>
		private void LogHopEnd(string note)
		{
			if (hopPhase == HopPhase.RunUp)
			{
				TestLog.Count("Fence jumps", "never jumped");
				if (TestLog.On || Player.m_debugMode)
				{
					Plugin.Log.LogInfo("Hop: " + TestLog.Name(humanoid, "F1") + " ran at the " + hopPiece + " at " + hopPoint.ToString("F1") + " for " + RunUpTime.ToString("F0") + "s without jumping, " + Ahead().ToString("F2") + "m in front of it");
				}
				return;
			}
			if (hopPhase != HopPhase.Air)
			{
				return;
			}
			HopOutcome outcome = HopResult(hopPoint, hopDirection, transform.position, hopFenceTop);
			string how = HopOutcomeName(outcome);
			TestLog.Count("Fence jumps", how);
			if (TestLog.On || Player.m_debugMode)
			{
				float clearance = hopPeak - hopFenceTop;
				Plugin.Log.LogInfo("Hop: " + TestLog.Name(humanoid, "F1") + " jumped the " + hopPiece + " at " + hopPoint.ToString("F1") + " and " + how + " - its feet rose " + (hopPeak - hopTakeoff.y).ToString("F2") + "m from " + hopTakeoff.ToString("F1") + ", " + Mathf.Abs(clearance).ToString("F2") + "m " + ((clearance >= 0f) ? "over" : "short of") + " the fence's top at " + hopFenceTop.ToString("F2") + ", leaving the ground " + hopTakeoffAhead.ToString("F2") + "m in front of it" + note);
			}
		}

		/// <summary>
		/// Works out where a jump over a fence left the viking - past the fence once it's at least HopOverPast beyond the
		/// fence's face the way it jumped, on top of it short of that with its feet no more than HopOnTopDrop below the
		/// fence's top, and otherwise back on the side it jumped from
		/// </summary>
		/// <param name="face">Where it found the fence's face, before jumping</param>
		/// <param name="direction">The way it jumped, level</param>
		/// <param name="landed">Where its feet are once it's down</param>
		/// <param name="top">How high the fence's top stands</param>
		/// <returns>Where it ended up</returns>
		internal static HopOutcome HopResult(Vector3 face, Vector3 direction, Vector3 landed, float top)
		{
			Vector3 offset = landed - face;
			offset.y = 0f;
			float past = Vector3.Dot(offset, direction);
			if (past >= HopOverPast)
			{
				return(HopOutcome.Over);
			}
			if (landed.y >= top - HopOnTopDrop)
			{
				return(HopOutcome.OnTop);
			}
			return(HopOutcome.FellBack);
		}

		/// <summary>
		/// Names how a jump over a fence ended, for the log
		/// </summary>
		/// <param name="outcome">How it ended</param>
		/// <returns>The words for it</returns>
		internal static string HopOutcomeName(HopOutcome outcome)
		{
			if (outcome == HopOutcome.Over)
			{
				return("got over");
			}
			else if (outcome == HopOutcome.OnTop)
			{
				return("ended up on top of it");
			}
			return("fell back");
		}

		/// <summary>
		/// Checks whether the viking is backing up, running at or jumping a fence (see UpdateHop), when it runs whatever
		/// else it's doing
		/// </summary>
		/// <returns>True when it is</returns>
		public bool Hopping()
		{
			return(hopPhase != HopPhase.None);
		}

		/// <summary>
		/// Works out how far in front of the viking the face of the fence it's jumping is, from the front of its body
		/// </summary>
		/// <returns>The distance, in meters, less than 0 once it's past</returns>
		private float Ahead()
		{
			Vector3 offset = hopPoint - transform.position;
			offset.y = 0f;
			return(Vector3.Dot(offset, hopDirection) - humanoid.GetRadius());
		}

		/// <summary>
		/// Works out how far from the fence the viking jumps to clear it at the speed it's going - its own jump made
		/// stronger by its Jump skill (see VikingCore.JumpSkillBonus), against the game's gravity - far enough out to
		/// be above the fence's top by the time it gets there, and as far again as it can spare to carry its body past
		/// </summary>
		/// <returns>The distance, in meters</returns>
		private float JumpDistance()
		{
			float boost = ((viking != null) ? VikingCore.SkillBoost(viking.SkillLevel(Skills.SkillType.Jump), VikingCore.JumpSkillBonus) : 1f);
			float up = humanoid.m_jumpForce * boost;
			float gravity = Mathf.Max(1f, -Physics.gravity.y);
			float height = hopTop + HopClearance;
			Vector3 velocity = humanoid.m_body.linearVelocity;
			velocity.y = 0f;
			float speed = Mathf.Max(0f, Vector3.Dot(velocity, hopDirection)) + humanoid.m_jumpForceForward * boost;
			float past = humanoid.GetRadius() * 2f + HopDepth;
			return(JumpFrom(up, gravity, height, speed, past));
		}

		/// <summary>
		/// Works out how far from a fence a jump clears it - far enough out to be above the fence's top by the time it
		/// gets there, and half of how much farther it carries above that height than it has to go to get past
		/// </summary>
		/// <param name="up">How fast the jump goes up, in meters a second</param>
		/// <param name="gravity">How fast gravity pulls down, in meters a second each second</param>
		/// <param name="height">How high it has to get</param>
		/// <param name="speed">How fast it goes toward the fence, in meters a second</param>
		/// <param name="past">How far it has to go above that height to get past the fence, in meters</param>
		/// <returns>The distance, in meters</returns>
		internal static float JumpFrom(float up, float gravity, float height, float speed, float past)
		{
			float spare = up * up - 2f * gravity * height;
			if (spare <= 0f)
			{
				// It can't get that high, so it jumps from right in front, which is as near as it comes
				return(0.1f);
			}
			float root = Mathf.Sqrt(spare);
			float rising = (up - root) / gravity;
			float over = 2f * root / gravity;
			return(speed * rising + Mathf.Max(0f, speed * over - past) / 2f);
		}

		/// <summary>
		/// Stops the viking and turns it to face a point
		/// </summary>
		/// <param name="point">The point</param>
		private void Face(Vector3 point)
		{
			ai.StopMoving();
			Vector3 look = point;
			look.y = humanoid.m_eye.position.y;
			ai.LookAt(look);
		}

		/// <summary>
		/// Lays the viking on a bed or sits it on a chair, ignoring the piece's colliders while it's there
		/// </summary>
		/// <param name="point">Where it lies or sits</param>
		/// <param name="piece">The bed or chair</param>
		private void Attach(Transform point, GameObject piece)
		{
			Detach();
			attachPoint = point;
			inBed = (piece.GetComponent<Bed>() != null);
			attachColliders = piece.GetComponentsInChildren<Collider>();
			foreach (Collider collider in attachColliders)
			{
				Physics.IgnoreCollision(humanoid.m_collider, collider, true);
			}
			humanoid.m_body.useGravity = false;
			body.SetSeated(true);
			HoldAttached();
		}

		/// <summary>
		/// Gets the viking off the bed or chair it's on, a little above it
		/// </summary>
		private void Detach()
		{
			if (attachPoint == null && attachColliders == null)
			{
				return;
			}
			Vector3 standing = ((attachPoint != null) ? attachPoint.position + Vector3.up * 0.5f : transform.position);
			if (attachColliders != null)
			{
				foreach (Collider collider in attachColliders)
				{
					if (collider != null && humanoid.m_collider != null)
					{
						Physics.IgnoreCollision(humanoid.m_collider, collider, false);
					}
				}
			}
			attachPoint = null;
			attachColliders = null;
			inBed = false;
			if (body != null)
			{
				body.SetSeated(false);
			}
			if (humanoid != null && humanoid.m_body != null)
			{
				humanoid.m_body.useGravity = true;
				humanoid.m_body.position = standing;
				transform.position = standing;
			}
		}

		/// <summary>
		/// Holds the viking where it lies or sits, still - with its gravity off, which the game's own motion update turns
		/// back on every physics update, the way it does for a player sitting down - moving with what it sits on when that
		/// moves, like a ship, so it keeps up with it between physics updates rather than jumping after it (see
		/// Player.UpdateAttach)
		/// </summary>
		private void HoldAttached()
		{
			transform.position = attachPoint.position;
			transform.rotation = attachPoint.rotation;
			humanoid.m_body.position = attachPoint.position;
			humanoid.m_body.rotation = attachPoint.rotation;
			humanoid.m_body.useGravity = false;
			Rigidbody carrier = attachPoint.GetComponentInParent<Rigidbody>();
			humanoid.m_body.linearVelocity = ((carrier != null) ? carrier.GetPointVelocity(attachPoint.position) : Vector3.zero);
			humanoid.m_body.angularVelocity = Vector3.zero;
			humanoid.m_maxAirAltitude = attachPoint.position.y;
			// Its seat's colliders are ignored, so it touches no ground - it's kept counted as on the ground, and still,
			// or its animation flips between falling, walking and sitting
			humanoid.m_lastGroundTouch = 0f;
			humanoid.m_currentVel = Vector3.zero;
			humanoid.SetMoveDir(Vector3.zero);
		}

		/// <summary>
		/// Holds the viking on the bed or chair it's on right after the game's motion update has moved it, so the physics
		/// that follows has nothing to move
		/// </summary>
		public void AfterMotion()
		{
			if (attachPoint != null && nview != null && nview.IsValid() && nview.IsOwner())
			{
				HoldAttached();
			}
		}

		/// <summary>
		/// Gives the viking a hammer to hold for show, or takes away whatever it holds for show
		/// </summary>
		/// <param name="hold">True to hold a hammer</param>
		private void HoldHammer(bool hold)
		{
			if (hold)
			{
				HoldProp(HammerPrefab);
				return;
			}
			if (humanoid == null)
			{
				return;
			}
			Inventory inventory = humanoid.GetInventory();
			ItemDrop.ItemData prop = inventory.GetAllItems().Find((ItemDrop.ItemData item) => VikingItems.IsProp(item));
			if (prop == null)
			{
				return;
			}
			if (humanoid.IsItemEquiped(prop))
			{
				humanoid.UnequipItem(prop, false);
			}
			inventory.RemoveItem(prop);
		}

		/// <summary>
		/// Gives the viking something to hold for show while it works, like a hammer or an axe, in place of any other it
		/// holds - taken away again when its job ends (see HoldHammer)
		/// </summary>
		/// <param name="prefab">The item's prefab name</param>
		private void HoldProp(string prefab)
		{
			if (humanoid == null || GearHeld())
			{
				return;
			}
			Inventory inventory = humanoid.GetInventory();
			ItemDrop.ItemData prop = inventory.GetAllItems().Find((ItemDrop.ItemData item) => VikingItems.IsProp(item));
			if (prop != null && (prop.m_dropPrefab == null || prop.m_dropPrefab.name != prefab))
			{
				HoldHammer(false);
				prop = null;
			}
			if (prop == null)
			{
				prop = inventory.AddItem(prefab, 1, 1, 0, 0L, "", false);
				if (prop == null)
				{
					return;
				}
				prop.m_customData[VikingItems.PropKey] = "1";
			}
			if (!humanoid.IsItemEquiped(prop))
			{
				humanoid.EquipItem(prop, false);
			}
		}

		/// <summary>
		/// Claims a bed or fire for the viking in the object's ZDO, so no other viking takes it - renewing a claim it
		/// already holds
		/// </summary>
		/// <param name="view">The object</param>
		/// <returns>False when another viking holds it</returns>
		private bool ClaimObject(ZNetView view)
		{
			if (view == null || !view.IsValid())
			{
				return(false);
			}
			ZDO zdo = view.GetZDO();
			ZDOID me = nview.GetZDO().m_uid;
			ZDOID holder = zdo.GetZDOID(ClaimKey);
			long now = NetTime.Now();
			if (!ClaimFree(holder, me, zdo.GetLong(ClaimUntilKey), now, ClaimantAlive(holder)))
			{
				return(false);
			}
			view.ClaimOwnership();
			zdo.Set(ClaimKey, me);
			zdo.Set(ClaimUntilKey, now + NetTime.Ticks(ClaimTime));
			return(true);
		}

		/// <summary>
		/// Checks whether no other viking holds a bed, chair or fire
		/// </summary>
		/// <param name="view">The object</param>
		/// <returns>True when it's free</returns>
		private bool IsFree(ZNetView view)
		{
			if (view == null || !view.IsValid())
			{
				return(false);
			}
			ZDO zdo = view.GetZDO();
			ZDOID holder = zdo.GetZDOID(ClaimKey);
			return(ClaimFree(holder, nview.GetZDO().m_uid, zdo.GetLong(ClaimUntilKey), NetTime.Now(), ClaimantAlive(holder)));
		}

		/// <summary>
		/// Checks whether a viking may take a bed, chair or fire from who holds its claim - when no one does, the
		/// viking does itself, the claim has run out or its holder has died
		/// </summary>
		/// <param name="holder">The ZDO of the viking holding the claim, or ZDOID.None</param>
		/// <param name="me">The viking's own ZDO</param>
		/// <param name="until">When the claim runs out, in world ticks</param>
		/// <param name="now">The world time, in ticks</param>
		/// <param name="alive">Whether the holder is still in the world</param>
		/// <returns>True when it's free for the viking</returns>
		internal static bool ClaimFree(ZDOID holder, ZDOID me, long until, long now, bool alive)
		{
			return(holder == ZDOID.None || holder == me || until <= now || !alive);
		}

		/// <summary>
		/// Lets go of the viking's claim on a bed or fire
		/// </summary>
		/// <param name="view">The object</param>
		private void ReleaseObject(ZNetView view)
		{
			if (view == null || !view.IsValid() || nview == null || !nview.IsValid())
			{
				return;
			}
			ZDO zdo = view.GetZDO();
			if (zdo.GetZDOID(ClaimKey) == nview.GetZDO().m_uid)
			{
				view.ClaimOwnership();
				zdo.Set(ClaimKey, ZDOID.None);
				zdo.Set(ClaimUntilKey, 0L);
			}
		}

		/// <summary>
		/// Checks whether a viking holding a claim is still in the world - a viking whose ZDO is gone has died
		/// </summary>
		/// <param name="holder">The viking's ZDO</param>
		/// <returns>True when it's still there</returns>
		private static bool ClaimantAlive(ZDOID holder)
		{
			return(ZDOMan.instance == null || ZDOMan.instance.GetZDO(holder) != null);
		}

		/// <summary>
		/// Claims a piece or branch for the viking among those this client's vikings work on
		/// </summary>
		/// <param name="key">The piece's or branch's ZDO, or where a missing piece stood</param>
		/// <returns>False when another viking has claimed it</returns>
		private bool ClaimWork(object key)
		{
			if (workClaims.TryGetValue(key, out VillageLife holder) && holder != null && holder != this && holder.job != null)
			{
				return(false);
			}
			workClaims[key] = this;
			return(true);
		}

		/// <summary>
		/// Lets go of a piece or branch the viking claimed
		/// </summary>
		/// <param name="key">The piece's or branch's ZDO, or where a missing piece stood</param>
		private void ReleaseWork(object key)
		{
			if (workClaims.TryGetValue(key, out VillageLife holder) && holder == this)
			{
				workClaims.Remove(key);
			}
		}

		/// <summary>
		/// Lets go of everything the viking claimed among the pieces and branches
		/// </summary>
		private void ReleaseWork()
		{
			List<object> held = new List<object>();
			foreach (KeyValuePair<object, VillageLife> claim in workClaims)
			{
				if (claim.Value == this || claim.Value == null)
				{
					held.Add(claim.Key);
				}
			}
			foreach (object key in held)
			{
				workClaims.Remove(key);
			}
		}

		/// <summary>
		/// The key a missing piece is claimed by - where it stood, to the nearest 10 centimeters
		/// </summary>
		/// <param name="record">The piece</param>
		/// <returns>The key</returns>
		private static Vector3Int RecordKey(PieceRecord record)
		{
			return(Vector3Int.RoundToInt(record.Position * 10f));
		}

		/// <summary>
		/// Finds the nearest wood near the village that no other viking is after - a branch, or wood lying on the
		/// ground - only in the village while the viking may not leave it (see MayLeave)
		/// </summary>
		/// <returns>The branch or wood, or null when there's none</returns>
		private ZNetView FindWood()
		{
			float reach = (MayLeave() ? WoodReach : 0f);
			ZNetView nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (ZNetView view in LoadedWood())
			{
				if (!IsWood(view) || village.DistanceOutside(view.transform.position) > reach)
				{
					continue;
				}
				ZDOID id = view.GetZDO().m_uid;
				if (Avoided(id))
				{
					continue;
				}
				if (workClaims.TryGetValue(id, out VillageLife holder) && holder != this && holder != null && holder.job != null)
				{
					continue;
				}
				float distance = Vector3.Distance(view.transform.position, transform.position);
				if (distance < nearestDistance && (village.Contains(view.transform.position) || !DangerNear(view.transform.position)))
				{
					nearest = view;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Goes through the loaded objects that can be wood the viking gathers (see IsWood) - the items on the ground
		/// and the branches
		/// </summary>
		/// <returns>Their objects, leaving out those destroyed since they were listed (see ListLoaded)</returns>
		private static IEnumerable<ZNetView> LoadedWood()
		{
			foreach (ItemDrop drop in LoadedDrops())
			{
				ZNetView view = ((drop != null) ? drop.m_nview : null);
				if (view != null && view.IsValid() && view.GetZDO().GetPrefab() != BranchHash)
				{
					yield return(view);
				}
			}
			foreach (Pickable pickable in LoadedPickables())
			{
				ZNetView view = ((pickable != null) ? pickable.m_nview : null);
				if (view != null && view.IsValid() && view.GetZDO().GetPrefab() == BranchHash)
				{
					yield return(view);
				}
			}
		}

		/// <summary>
		/// Checks whether an object is wood the viking can gather - a branch that hasn't been picked, or wood lying on
		/// the ground
		/// </summary>
		/// <param name="view">The object</param>
		/// <returns>True when it is</returns>
		private static bool IsWood(ZNetView view)
		{
			if (!view.IsValid())
			{
				return(false);
			}
			if (view.GetZDO().GetPrefab() == BranchHash)
			{
				Pickable branch = view.GetComponent<Pickable>();
				return(branch != null && !branch.GetPicked());
			}
			ItemDrop drop = view.GetComponent<ItemDrop>();
			return(drop != null && drop.m_itemData != null && drop.m_itemData.m_dropPrefab != null && WoodItems.Contains(drop.m_itemData.m_dropPrefab.name));
		}

		/// <summary>
		/// Gathers wood the viking has reached into its inventory - picking a branch, or picking up as much of the wood
		/// on the ground as it can carry, leaving the rest
		/// </summary>
		/// <param name="view">The branch or wood</param>
		/// <returns>How much wood it gathered</returns>
		private int GatherWood(ZNetView view)
		{
			if (view == null || !IsWood(view))
			{
				return(0);
			}
			Pickable branch = view.GetComponent<Pickable>();
			if (branch != null)
			{
				ItemDrop.ItemData picked = ItemOf((branch.m_itemPrefab != null) ? branch.m_itemPrefab.name : "Wood");
				if (picked == null || WoodRoom(picked) <= 0 || !PickBranch(branch))
				{
					return(0);
				}
				return(AddWood(picked, 1));
			}
			ItemDrop drop = view.GetComponent<ItemDrop>();
			if (drop == null || !OwnItem(view))
			{
				return(0);
			}
			drop.Load();
			ItemDrop.ItemData wood = ItemOf(drop);
			if (wood == null)
			{
				return(0);
			}
			int stack = wood.m_stack;
			int taken = AddWood(wood, stack);
			if (taken <= 0)
			{
				return(0);
			}
			if (taken >= stack)
			{
				ZNetScene.instance.Destroy(drop.gameObject);
			}
			else
			{
				drop.SetStack(stack - taken);
			}
			return(taken);
		}

		/// <summary>
		/// Picks up a branch the viking has reached, the same as a player picking it, so it grows back as usual - the
		/// viking keeps the wood rather than dropping it
		/// </summary>
		/// <param name="branch">The branch</param>
		/// <returns>False when it was already gone</returns>
		private static bool PickBranch(Pickable branch)
		{
			if (branch == null || branch.GetPicked() || branch.m_nview == null || !branch.m_nview.IsValid())
			{
				return(false);
			}
			branch.m_pickEffector.Create(branch.transform.position, Quaternion.identity);
			branch.m_nview.InvokeRPC(ZNetView.Everybody, "RPC_SetPicked", true);
			TestLog.Count("Work done", "branches picked up");
			return(true);
		}

		/// <summary>
		/// Something the viking does with nothing to fight, one AI update at a time
		/// </summary>
		private abstract class Job
		{
			protected readonly VillageLife life;

			/// <summary>
			/// Makes the job for a viking
			/// </summary>
			/// <param name="owner">The viking's life</param>
			protected Job(VillageLife owner)
			{
				life = owner;
				StartedAt = Time.time;
			}

			/// <summary>
			/// When the job was made, for the debug hover text
			/// </summary>
			public readonly float StartedAt;

			/// <summary>
			/// Starts the job
			/// </summary>
			public virtual void Begin()
			{
			}

			/// <summary>
			/// Carries the job on
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's done</returns>
			public abstract bool Tick(float dt);

			/// <summary>
			/// Ends the job, whether it's done or was interrupted, letting go of what it claimed and ending its poses
			/// </summary>
			public virtual void End()
			{
			}

			/// <summary>
			/// Checks whether the viking is resting at night in this job, with its senses dulled
			/// </summary>
			/// <returns>True while it rests</returns>
			public virtual bool Resting()
			{
				return(false);
			}

			/// <summary>
			/// Checks whether the viking sits in this job, on the floor or a chair, which rests it by a fire
			/// </summary>
			/// <returns>True while it sits</returns>
			public virtual bool Sitting()
			{
				return(false);
			}

			/// <summary>
			/// Checks whether the job has the viking heading out of the village - for wood, food or loot outside it
			/// </summary>
			/// <returns>True while it does</returns>
			public virtual bool Outing()
			{
				return(false);
			}

			/// <summary>
			/// Finds the creature the job is about, which the viking walks right up to rather than around - the viking
			/// it's talking with, or the boar it's petting
			/// </summary>
			/// <returns>The creature, or null for a job that's about none</returns>
			public virtual Character Focus()
			{
				return(null);
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name, and how far along it is for a job that says</returns>
			public virtual string Describe()
			{
				string name = GetType().Name;
				return(name.EndsWith("Job") ? name.Substring(0, name.Length - 3) : name);
			}
		}

		/// <summary>
		/// Keeping guard through a raid on the village - standing in a ring around the fire it gathers at (see
		/// ChooseGatherFire) with its weapon drawn (see IsWary), facing out and looking around: an archer in the ring the
		/// village gathers in at dusk (see FireRing), and one fighting hand to hand GuardRingGap farther out, in front of
		/// the archers - each viking on its own side of the fire, shifting a little along it every GuardTimeMin to
		/// GuardTimeMax, until the raid is over. With no fire to gather at, it keeps guard at an open spot in the core
		/// </summary>
		private class GuardJob : Job
		{
			private Vector3 post;
			private bool hasPost;
			private Vector3 middle;
			private bool aroundFire;
			private float until;
			private float nextLook;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			public GuardJob(VillageLife owner) : base(owner)
			{
			}

			/// <summary>
			/// Picks where it keeps guard - in its ring around the fire, or else an open spot in the core - and for how
			/// long
			/// </summary>
			public override void Begin()
			{
				until = Time.time + Random.Range(GuardTimeMin, GuardTimeMax);
				Fireplace fire = life.ChooseGatherFire();
				if (fire != null)
				{
					middle = fire.transform.position;
					float inner = FireRing(fire, 0f);
					float outer = FireRing(fire, 1f);
					if (life.viking == null || !life.viking.IsArcher())
					{
						float width = outer - inner;
						inner = outer + GuardRingGap;
						outer = inner + width;
					}
					aroundFire = RingPost(inner, outer);
					hasPost = aroundFire;
				}
				if (!hasPost)
				{
					hasPost = life.village.RandomOpenSpot(out post);
				}
			}

			/// <summary>
			/// Picks a spot in its ring around the fire, on its own side of it - the side its ZDO picks for it, so it keeps
			/// to the same one, shifted a little each time, or else turned farther around for a spot inside a house or a
			/// pen, or in the water
			/// </summary>
			/// <param name="inner">The inside of the ring, from the fire's middle, in meters</param>
			/// <param name="outer">The outside of it, in meters</param>
			/// <returns>False when none of GuardTries spots would do</returns>
			private bool RingPost(float inner, float outer)
			{
				float side = (life.nview.GetZDO().m_uid.GetHashCode() & 0xFFFF) / 65536f * 360f;
				for (int i = 0; i < GuardTries; i++)
				{
					float turn = Fan(i, GuardStep) + Random.Range(-GuardJitter, GuardJitter);
					Vector3 point = middle + Quaternion.Euler(0f, side + turn, 0f) * Vector3.forward * Random.Range(inner, outer);
					if (ZoneSystem.instance.GetGroundHeight(point, out float ground))
					{
						point.y = ground;
					}
					if (point.y < ZoneSystem.instance.m_waterLevel + GuardWaterClearance || life.village.InHouse(point, 0.5f) || Village.FencedIn(point))
					{
						continue;
					}
					post = point;
					return(true);
				}
				return(false);
			}

			/// <summary>
			/// Describes the job for the debug hover text - where it keeps guard, how far off it is and how long it stays
			/// </summary>
			/// <returns>The description</returns>
			public override string Describe()
			{
				string where = (!hasPost ? "with nowhere to stand" : (aroundFire ? "around the fire" : "in the core") + " at " + post.ToString("F0") + ", " + Utils.DistanceXZ(life.transform.position, post).ToString("F1") + "m off");
				return("Guard " + where + ", " + Mathf.Max(0f, until - Time.time).ToString("F0") + "s left" + (VillageRaids.On(life.village) ? "" : ", raid over"));
			}

			/// <summary>
			/// Walks to its spot, then stands there looking around every few seconds - out from the fire, in its ring
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has stood there long enough, it can't get there, or the raid is over</returns>
			public override bool Tick(float dt)
			{
				if (!VillageRaids.On(life.village) || Time.time >= until)
				{
					return(true);
				}
				if (hasPost && Utils.DistanceXZ(life.transform.position, post) > 1.5f)
				{
					life.Go(dt, post, 1f, false);
					return(life.moveFailed);
				}
				life.ai.StopMoving();
				if (Time.time >= nextLook)
				{
					nextLook = Time.time + Random.Range(2f, 4f);
					Vector3 outward = life.transform.position - middle;
					outward.y = 0f;
					Vector2 look = Random.insideUnitCircle.normalized;
					Vector3 around = new Vector3(look.x, 0f, look.y);
					life.ai.LookTowards((aroundFire && outward.sqrMagnitude > 0.01f) ? Quaternion.Euler(0f, Random.Range(-GuardLookSpread, GuardLookSpread), 0f) * outward.normalized : around);
				}
				return(false);
			}
		}

		/// <summary>
		/// Standing about for a while, looking around now and then - in the rain, inside its house, clear of the doors
		/// </summary>
		private class IdleJob : Job
		{
			private readonly float duration;
			private readonly bool shelter;
			private float until = -1f;
			private float nextLook;
			private Vector3 spot;
			private bool walking;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="duration">How long it stands, in seconds</param>
			/// <param name="shelter">True to stand inside its house, out of the rain</param>
			public IdleJob(VillageLife owner, float duration, bool shelter = false) : base(owner)
			{
				this.duration = duration;
				this.shelter = shelter;
			}

			/// <summary>
			/// Picks a spot on its house's floor clear of the doors (see FloorSpot) to stand out of the rain, unless it
			/// already stands inside its house clear of them
			/// </summary>
			public override void Begin()
			{
				if (!shelter || !life.Home(out VillagePaths.House house))
				{
					return;
				}
				Vector3 position = life.transform.position;
				if (Village.InHouse(house, position, -1f) && !Village.Doors(house).Exists((Vector3 door) => Utils.DistanceXZ(door, position) < DoorClearance))
				{
					return;
				}
				spot = FloorSpot(house);
				walking = true;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name, and whether it's standing about inside</returns>
			public override string Describe()
			{
				return(shelter ? "Idle inside" + (walking ? ", on the way" : "") : "Idle");
			}

			/// <summary>
			/// Walks inside first when it stands about out of the rain, then stands still, turning to look somewhere else
			/// every few seconds
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has stood long enough</returns>
			public override bool Tick(float dt)
			{
				if (walking)
				{
					if (!life.Go(dt, spot, 0.75f, false))
					{
						return(false);
					}
					life.moveFailed = false;
					walking = false;
				}
				if (until < 0f)
				{
					until = Time.time + duration;
				}
				life.ai.StopMoving();
				if (Time.time >= nextLook)
				{
					nextLook = Time.time + Random.Range(3f, 6f);
					Vector2 look = Random.insideUnitCircle.normalized;
					life.ai.LookTowards(new Vector3(look.x, 0f, look.y));
				}
				return(Time.time >= until);
			}
		}

		/// <summary>
		/// Strolling to a random spot in the village
		/// </summary>
		private class StrollJob : Job
		{
			private Vector3 target;
			private bool hasTarget;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			public StrollJob(VillageLife owner) : base(owner)
			{
			}

			/// <summary>
			/// Picks where it strolls to
			/// </summary>
			public override void Begin()
			{
				hasTarget = life.village.RandomOpenSpot(out target);
			}

			/// <summary>
			/// Walks there
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's there, or when it can't get there</returns>
			public override bool Tick(float dt)
			{
				return(!hasTarget || life.Go(dt, target, 1f, false));
			}
		}

		/// <summary>
		/// Two vikings talking, which both of their jobs share - who's speaking, and when the talk ends
		/// </summary>
		private class TalkSession
		{
			public readonly VillageLife Leader;
			public readonly VillageLife Partner;
			public float Until;
			public VillageLife Speaker;
			public float NextTurn;
			public bool Over;

			/// <summary>
			/// How long they talk once they've met, in seconds
			/// </summary>
			public readonly float Duration;

			/// <summary>
			/// In the rain, when neither is under a roof, the spot inside the leader's house they talk at, whether there is
			/// one, and whether the leader has got there - the partner follows the leader in
			/// </summary>
			public readonly Vector3 Place;
			public readonly bool HasPlace;
			public bool AtPlace;

			/// <summary>
			/// What they say, a line a turn, how many lines have been said, and whether the first has
			/// </summary>
			public readonly string[] Lines;
			public int Said;
			public bool Started;

			/// <summary>
			/// Starts a talk, with the viking that started it speaking first, about what's going on around it - or now and
			/// then about one of the village's dead (see VillageFallen.Remember), a newcomer, or a quest the one who started
			/// it just came back from or that's out (see QuestTalk). In the rain, when the partner isn't under
			/// a roof, they talk inside the leader's house, clear of its doors (see FloorSpot), with TalkWalkTime more to get
			/// there
			/// </summary>
			/// <param name="leader">The viking that started it</param>
			/// <param name="partner">The viking it talks to</param>
			/// <param name="duration">How long they talk, in seconds</param>
			public TalkSession(VillageLife leader, VillageLife partner, float duration)
			{
				Leader = leader;
				Partner = partner;
				Duration = duration;
				if (VikingWeather.KeepsIndoors(leader.transform.position) && !VikingWeather.UnderRoof(partner.humanoid.GetCenterPoint()) && leader.Home(out VillagePaths.House house))
				{
					Place = FloorSpot(house);
					HasPlace = true;
				}
				Until = Time.time + duration + (HasPlace ? TalkWalkTime : 0f);
				Speaker = leader;
				OtherName = NamedFellow(leader, partner);
				ItemName = leader.VillageMaterial();
				FallenName = VillageFallen.Remember(leader.village);
				VillageName = VillageNames.Get(leader.village);
				LinkedName = "";
				LinkedBiome = "";
				string[][] linkedTalks = null;
				if (VillagePortals.Linked(leader.village, out string linked, out Heightmap.Biome biome))
				{
					LinkedName = linked;
					LinkedBiome = VillagePortalSpeech.BiomeName(biome);
					linkedTalks = VillagePortalSpeech.Talks(biome);
				}
				string[] newcomerTalk = NewcomerTalk(leader, partner, VillageName.Length > 0, out NewcomerName, out NewcomerFrom);
				string[] questTalk = ((newcomerTalk == null) ? QuestTalk(leader) : null);
				Lines = (newcomerTalk ?? questTalk ?? VikingSpeech.Talk(IsNight() || IsDusk(), EnvMan.IsWet(), Time.time - leader.lastFightAt < FightTalkTime, leader.Wounded(), OtherName.Length > 0, ItemName.Length > 0, FallenName.Length > 0, leader.body.HasBeard(), VillageName.Length > 0, linkedTalks));
			}

			/// <summary>
			/// Picks a talk about a viking new to the village, now and then, for a game day after it joined (see
			/// VillageNewcomers) - welcoming it when it's the one talked to, or about one of the newcomers with a name
			/// otherwise, mentioning the village its group came from when there's one. A newcomer doesn't start one
			/// </summary>
			/// <param name="leader">The viking that started the talk</param>
			/// <param name="partner">The viking it talks to</param>
			/// <param name="named">Whether the village has a name</param>
			/// <param name="newcomer">Set to the name of the newcomer talked about, or ""</param>
			/// <param name="from">Set to the village its group came from, or ""</param>
			/// <returns>The talk's lines, or null for a talk about anything else</returns>
			private static string[] NewcomerTalk(VillageLife leader, VillageLife partner, bool named, out string newcomer, out string from)
			{
				newcomer = "";
				from = "";
				if (leader.village == null || VillageNewcomers.IsNew(leader.nview.GetZDO()))
				{
					return(null);
				}
				ZDO partnerZdo = partner.nview.GetZDO();
				if (VillageNewcomers.IsNew(partnerZdo) && Random.value < VillageNewcomers.WelcomeChance)
				{
					from = VillageNewcomers.From(partnerZdo);
					return(PickTalk(VillageNewcomers.Candidates(VillageNewcomers.WelcomeTalks, VillageNewcomers.WelcomeFromTalks, from.Length > 0, named)));
				}
				List<Character> others = leader.village.Vikings().FindAll((Character other) => other != null && other != leader.humanoid && other != partner.humanoid && other.m_nview != null && other.m_nview.IsValid() && VillageNewcomers.IsNew(other.m_nview.GetZDO()) && VikingNames.Get(other).Length > 0);
				if (others.Count == 0 || Random.value >= VillageNewcomers.AboutChance)
				{
					return(null);
				}
				Character talkedAbout = others[Random.Range(0, others.Count)];
				newcomer = VikingNames.Get(talkedAbout);
				from = VillageNewcomers.From(talkedAbout.m_nview.GetZDO());
				return(PickTalk(VillageNewcomers.Candidates(VillageNewcomers.AboutTalks, VillageNewcomers.AboutFromTalks, from.Length > 0, named)));
			}

			/// <summary>
			/// Picks one of a list of talks at random
			/// </summary>
			/// <param name="talks">The talks</param>
			/// <returns>One of them, or null when there are none</returns>
			private static string[] PickTalk(List<string[]> talks)
			{
				return((talks.Count > 0) ? talks[Random.Range(0, talks.Count)] : null);
			}

			/// <summary>
			/// The name of another viking of the village they might talk about, of a material in the village's chests they
			/// might talk about, of one of the village's dead they talk about, of the village itself, and of the village on
			/// the other side of its portal and the biome that stands in, or ""
			/// </summary>
			public readonly string OtherName;
			public readonly string ItemName;
			public readonly string FallenName;
			public readonly string VillageName;
			public readonly string LinkedName;
			public readonly string LinkedBiome;

			/// <summary>
			/// The name of a viking new to the village they talk about, and of the village its group came from, or "" (see
			/// VillageNewcomers)
			/// </summary>
			public readonly string NewcomerName;
			public readonly string NewcomerFrom;

			/// <summary>
			/// Has the viking whose turn it is say the talk's next line, while it has lines left - calling the one it speaks
			/// to by name when it has one
			/// </summary>
			public void SpeakTurn()
			{
				if (Said < Lines.Length && Speaker != null)
				{
					string listener = VikingNames.Get(Other(Speaker).humanoid);
					VikingSpeech.Chatter(Speaker.humanoid, VikingSpeech.Address(Lines[Said], listener, OtherName, ItemName, FallenName, VillageName, LinkedName, LinkedBiome, NewcomerName, NewcomerFrom), VikingSpeech.TalkTime);
					Said++;
				}
			}

			/// <summary>
			/// Picks another loaded viking of the leader's village with a name, at random, to talk about
			/// </summary>
			/// <param name="leader">The viking that started the talk</param>
			/// <param name="partner">The viking it talks to</param>
			/// <returns>Its name, or "" when there's none</returns>
			private static string NamedFellow(VillageLife leader, VillageLife partner)
			{
				List<string> names = new List<string>();
				if (leader.village != null)
				{
					foreach (Character fellow in leader.village.Vikings())
					{
						string name = ((fellow != leader.humanoid && fellow != partner.humanoid) ? VikingNames.Get(fellow) : "");
						if (name.Length > 0)
						{
							names.Add(name);
						}
					}
				}
				return((names.Count > 0) ? names[Random.Range(0, names.Count)] : "");
			}

			/// <summary>
			/// Finds the other viking of the talk
			/// </summary>
			/// <param name="one">One of them</param>
			/// <returns>The other</returns>
			public VillageLife Other(VillageLife one)
			{
				return((one == Leader) ? Partner : Leader);
			}
		}

		/// <summary>
		/// Walking up to another viking and talking with it, taking turns to speak
		/// </summary>
		private class TalkJob : Job
		{
			private readonly TalkSession session;
			private VikingPoses.TalkMode mode = VikingPoses.TalkMode.None;
			private bool met;

			/// <summary>
			/// Finds the viking it's talking with
			/// </summary>
			/// <returns>The other viking, or null when it's gone</returns>
			public override Character Focus()
			{
				VillageLife other = session.Other(life);
				return((other != null) ? other.humanoid : null);
			}

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="talk">The talk it's part of</param>
			public TalkJob(VillageLife owner, TalkSession talk) : base(owner)
			{
				session = talk;
			}

			/// <summary>
			/// Walks to the other viking, then faces it and speaks or listens in turn - in the rain, when they talk inside
			/// (see TalkSession), the leader walks to the spot in its house and waits there, and the other follows it in
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once the talk is over</returns>
			public override bool Tick(float dt)
			{
				VillageLife other = session.Other(life);
				if (session.Over || other == null || !other.active || other.job == null || !(other.job is TalkJob) || Time.time >= session.Until)
				{
					return(true);
				}
				float distance = Utils.DistanceXZ(other.transform.position, life.transform.position);
				if (!met)
				{
					if (session.HasPlace && !session.AtPlace)
					{
						if (session.Leader != life)
						{
							// It follows the leader in, rather than meeting it out in the rain
							if (distance > TalkDistance)
							{
								life.Go(dt, other.transform.position, TalkDistance, false);
								if (life.moveFailed)
								{
									return(true);
								}
							}
							else
							{
								life.ai.StopMoving();
							}
							return(false);
						}
						if (!life.Go(dt, session.Place, 0.75f, false))
						{
							return(false);
						}
						life.moveFailed = false;
						session.AtPlace = true;
					}
					if (distance > TalkDistance && session.HasPlace && session.Leader == life)
					{
						// It waits at its spot for the other to come in
						life.ai.StopMoving();
						life.Face(other.transform.position);
						return(false);
					}
					if (distance > TalkDistance)
					{
						life.Go(dt, other.transform.position, TalkDistance, false);
						if (life.moveFailed)
						{
							return(true);
						}
						return(false);
					}
					met = true;
					if (session.Leader == life)
					{
						session.NextTurn = Time.time + Random.Range(TurnTimeMin, TurnTimeMax);
					}
				}
				life.Face(other.transform.position);
				if (!other.TalkMet())
				{
					SetMode(VikingPoses.TalkMode.None);
					return(false);
				}
				if (session.Leader == life && !session.Started)
				{
					session.Started = true;
					if (session.HasPlace)
					{
						// The walk inside doesn't count against the talk
						session.Until = Time.time + session.Duration;
					}
					session.NextTurn = Time.time + Random.Range(TurnTimeMin, TurnTimeMax);
					session.SpeakTurn();
				}
				if (session.Leader == life && Time.time >= session.NextTurn)
				{
					session.Speaker = session.Other(session.Speaker);
					session.NextTurn = Time.time + Random.Range(TurnTimeMin, TurnTimeMax);
					if (Random.value < GestureChance)
					{
						VikingPoses.Gesture(session.Speaker.humanoid);
					}
					session.SpeakTurn();
				}
				SetMode((session.Speaker == life) ? VikingPoses.TalkMode.Speaking : VikingPoses.TalkMode.Listening);
				return(false);
			}

			/// <summary>
			/// Checks whether this viking has reached the other
			/// </summary>
			/// <returns>True once it has</returns>
			public bool Met()
			{
				return(met);
			}

			/// <summary>
			/// Sets how the viking's head moves as it talks, when it has changed
			/// </summary>
			/// <param name="next">Speaking, listening or neither</param>
			private void SetMode(VikingPoses.TalkMode next)
			{
				if (next != mode)
				{
					mode = next;
					VikingPoses.SetTalk(life.humanoid, mode);
				}
			}

			/// <summary>
			/// Ends the talk for both vikings
			/// </summary>
			public override void End()
			{
				session.Over = true;
				VikingPoses.SetTalk(life.humanoid, VikingPoses.TalkMode.None);
			}
		}

		/// <summary>
		/// Checks whether the viking has reached the viking it's talking with
		/// </summary>
		/// <returns>True when it's talking and has</returns>
		private bool TalkMet()
		{
			TalkJob talk = job as TalkJob;
			return(talk != null && talk.Met());
		}

		/// <summary>
		/// Walking up to one of the village's boars and petting it, one viking of the village at a time
		/// </summary>
		private class PetJob : Job
		{
			private readonly Character boar;
			private float pettedAt = -1f;
			private float nextRenew;

			/// <summary>
			/// Finds the boar it's petting
			/// </summary>
			/// <returns>The boar</returns>
			public override Character Focus()
			{
				return(boar);
			}

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="animal">The boar</param>
			public PetJob(VillageLife owner, Character animal) : base(owner)
			{
				boar = animal;
			}

			/// <summary>
			/// Notes that the viking is petting a boar, so no other viking of the village starts to
			/// </summary>
			public override void Begin()
			{
				life.SetDoing(PettingKey, true);
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Walks to the boar, reaches out to pet it and shows the hearts, then stands a moment
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's done, or the boar has gone</returns>
			public override bool Tick(float dt)
			{
				if (boar == null || boar.IsDead())
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					life.SetDoing(PettingKey, true);
					nextRenew = Time.time + ClaimRenew;
				}
				if (pettedAt >= 0f)
				{
					life.Face(boar.transform.position);
					return(Time.time - pettedAt >= 2.5f);
				}
				if (Utils.DistanceXZ(boar.transform.position, life.transform.position) > 1.6f)
				{
					life.Go(dt, boar.transform.position, 1.6f, false);
					return(life.moveFailed);
				}
				life.Face(boar.transform.position);
				VikingPoses.Interact(life.humanoid);
				VillageSignals.SendEffect(VillageSignals.PetEffect, boar.GetZDOID());
				pettedAt = Time.time;
				return(false);
			}

			/// <summary>
			/// Notes it's no longer petting a boar
			/// </summary>
			public override void End()
			{
				life.SetDoing(PettingKey, false);
			}
		}

		/// <summary>
		/// Walking over to the corpse of one of the village's vikings and mourning it - standing by it, facing it, with
		/// the player's emotes of grief now and then, and maybe a word of farewell
		/// </summary>
		private class MournJob : Job
		{
			/// <summary>
			/// The corpse it mourns
			/// </summary>
			public readonly Ragdoll Corpse;

			/// <summary>
			/// Whether the viking says a word of farewell, so the others mourning the corpse know one will
			/// </summary>
			public bool Speaks;

			/// <summary>
			/// Where by the corpse it stands, until when it stands there - or -1 until it's there - when it next grieves,
			/// and when it speaks, or -1 when it doesn't or already has
			/// </summary>
			private Vector3 spot;
			private float until = -1f;
			private float nextGrieve;
			private float speakAt = -1f;

			/// <summary>
			/// Where the corpse lay when the viking last saw it, so it can pick up the gear the dead dropped there once it
			/// has mourned, even when the corpse is gone by then
			/// </summary>
			private Vector3 lay;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="corpse">The corpse</param>
			public MournJob(VillageLife owner, Ragdoll corpse) : base(owner)
			{
				Corpse = corpse;
			}

			/// <summary>
			/// Notes the corpse as mourned, so the viking doesn't come back to it, and picks where it stands - MournDistance
			/// from the corpse, on the side it comes from, turned farther around it for each viking already mourning it
			/// </summary>
			public override void Begin()
			{
				if (life.mourned.Count >= CorpseMemory)
				{
					life.mourned.Clear();
				}
				life.mourned.Add(Corpse.m_nview.GetZDO().m_uid);
				Vector3 body = Corpse.GetAverageBodyPosition();
				Vector3 from = life.transform.position - body;
				from.y = 0f;
				float angle = ((from.sqrMagnitude < 0.01f) ? Random.Range(0f, 360f) : Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg);
				int index = Mathf.Max(0, Mourners(Corpse) - 1);
				angle += Fan(index, 360f / MournersMax);
				spot = body + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * MournDistance;
			}

			/// <summary>
			/// Walks to its spot by the corpse - or mourns from where it got to when it can't get there but is within
			/// WaitFar of the corpse - then faces the corpse, grieving every so often and saying its farewell once. Once
			/// it has mourned, it goes on to pick up the gear the dead dropped, the way it does what an enemy it killed
			/// dropped (see FindKillLoot)
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has mourned long enough, or the corpse is gone or out of reach</returns>
			public override bool Tick(float dt)
			{
				if (Corpse == null || Corpse.m_nview == null || !Corpse.m_nview.IsValid())
				{
					if (until >= 0f)
					{
						Mourned();
					}
					return(true);
				}
				Vector3 body = Corpse.GetAverageBodyPosition();
				lay = body;
				if (until < 0f)
				{
					if (Utils.DistanceXZ(life.transform.position, spot) > 0.5f && Utils.DistanceXZ(life.transform.position, body) > MournDistance + 0.5f)
					{
						life.Go(dt, spot, 0.5f, false);
						if (!life.moveFailed)
						{
							return(false);
						}
						if (Utils.DistanceXZ(life.transform.position, body) > WaitFar)
						{
							return(true);
						}
						life.moveFailed = false;
					}
					until = Time.time + Random.Range(MournTimeMin, MournTimeMax);
					nextGrieve = Time.time + Random.Range(0.5f, 1.5f);
					if (!OtherSpeaks() || Random.value < MournSpeakChance)
					{
						Speaks = true;
						speakAt = Time.time + Random.Range(MournSpeakDelayMin, MournSpeakDelayMax);
					}
				}
				life.Face(body);
				if (Time.time >= nextGrieve)
				{
					VikingPoses.Grieve(life.humanoid);
					nextGrieve = Time.time + Random.Range(GrieveIntervalMin, GrieveIntervalMax);
				}
				if (speakAt >= 0f && Time.time >= speakAt)
				{
					speakAt = -1f;
					VikingSpeech.Chatter(life.humanoid, VikingSpeech.Mourn(VikingNames.Get(Corpse.m_nview.GetZDO())), VikingSpeech.TalkTime);
				}
				if (Time.time < until)
				{
					return(false);
				}
				Mourned();
				return(true);
			}

			/// <summary>
			/// Notes where the corpse lay as the spot of a kill, so the viking picks up what lies around it next (see
			/// PicksUpKillLoot)
			/// </summary>
			private void Mourned()
			{
				life.killSpot = lay;
				life.killAt = Time.time;
			}

			/// <summary>
			/// Describes the job for the debug hover text - walking to the corpse, or standing by it and how long it stays
			/// </summary>
			/// <returns>The description</returns>
			public override string Describe()
			{
				if (Corpse == null)
				{
					return("Mourn, the corpse is gone");
				}
				return((until < 0f) ? "Mourn, walking to the corpse, " + Utils.DistanceXZ(life.transform.position, spot).ToString("F1") + "m off" : "Mourn by the corpse, " + Mathf.Max(0f, until - Time.time).ToString("F0") + "s left");
			}

			/// <summary>
			/// Checks whether another viking on this client mourning the same corpse says a word of farewell
			/// </summary>
			/// <returns>True when one does</returns>
			private bool OtherSpeaks()
			{
				foreach (VillageLife other in All)
				{
					if (other != life && other.job is MournJob mourning && mourning.Corpse == Corpse && mourning.Speaks)
					{
						return(true);
					}
				}
				return(false);
			}
		}

		/// <summary>
		/// Getting wood for a job that needs it, step by step - from the nearest of the village's chests with wood in it
		/// when the job takes stored wood, or else the nearest branch or wood lying on the ground near the village -
		/// added to the wood the viking carries
		/// </summary>
		private class WoodRun
		{
			private readonly VillageLife life;
			private readonly int wanted;
			private bool fromChests;
			private Container chest;
			private ZNetView wood;
			private ZDOID woodId;
			private Vector3 stand;
			private float takeAt = -1f;

			/// <summary>
			/// Finds the nearest wood on the ground the viking can get within reach of, leaving alone what it can't for a
			/// while - or in a pen, which has no way in but over its fence, which the viking jumps
			/// </summary>
			/// <returns>The wood, or null when there's none it can reach</returns>
			private ZNetView FindReachable()
			{
				for (int i = 0; i < ReachTries; i++)
				{
					ZNetView found = life.FindWood();
					if (found == null)
					{
						return(null);
					}
					if (InPen(found.GetZDO().m_uid, found.transform.position))
					{
						stand = found.transform.position;
						return(found);
					}
					if (life.CanReach(found.transform.position, out stand))
					{
						return(found);
					}
					life.Avoid(found);
				}
				return(null);
			}

			/// <summary>
			/// Whether no wood could be found, which keeps the viking from work that needs it for a while
			/// </summary>
			public bool None;

			/// <summary>
			/// Checks whether the wood the viking is after lies outside the village
			/// </summary>
			/// <returns>True when it does</returns>
			public bool Outing()
			{
				return(wood != null && !life.village.Contains(wood.transform.position));
			}

			/// <summary>
			/// Starts a run for a viking
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="needed">How much wood the job needs in all</param>
			/// <param name="chests">True to take stored wood from the village's chests first</param>
			public WoodRun(VillageLife owner, int needed, bool chests)
			{
				life = owner;
				wanted = needed;
				fromChests = chests;
			}

			/// <summary>
			/// Finds the nearest chest with wood or wood on the ground, walks to it and takes or gathers some
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has some, or None is set when there's none to get</returns>
			public bool Tick(float dt)
			{
				if (fromChests)
				{
					if (chest == null || !life.Usable(chest) || ChestWood(chest) <= 0)
					{
						chest = life.FindWoodChest();
						takeAt = -1f;
					}
					if (chest != null)
					{
						return(TakeFromChest(dt));
					}
					fromChests = false;
				}
				if (wood == null || !IsWood(wood))
				{
					Release();
					wood = (life.CanCarryWood() ? FindReachable() : null);
					if (wood == null)
					{
						GiveUp();
						return(false);
					}
					woodId = wood.GetZDO().m_uid;
					life.ClaimWork(woodId);
					life.UpdateOuting(Outing());
					life.woodInPen = InPen(woodId, wood.transform.position);
					if (life.woodInPen)
					{
						life.inPenForWood = true;
					}
					takeAt = -1f;
				}
				if (takeAt >= 0f)
				{
					if (Time.time < takeAt)
					{
						return(false);
					}
					int gathered = life.GatherWood(wood);
					Release();
					wood = null;
					return(gathered > 0);
				}
				// Out of the village with a monster in sight, it leaves the wood and heads home before it's seen
				if (Outing() && life.DangerNear(life.transform.position))
				{
					Release();
					wood = null;
					life.Flee(FleeReason.Forage);
					return(false);
				}
				if (Utils.DistanceXZ(wood.transform.position, life.transform.position) > PickReach && !life.Go(dt, stand, 0.5f, false))
				{
					return(false);
				}
				// Go counts it there within a meter of where it stands to reach
				if (life.moveFailed || Utils.DistanceXZ(wood.transform.position, life.transform.position) > PickReach + 1f)
				{
					// Stuck on the way, or it can't get near enough after all - it leaves this wood and tries another
					life.moveFailed = false;
					life.Avoid(wood);
					Release();
					wood = null;
					return(false);
				}
				life.Face(wood.transform.position);
				VikingPoses.Interact(life.humanoid);
				RequestItem(wood);
				takeAt = Time.time + ReachTime;
				return(false);
			}

			/// <summary>
			/// Walks to the chest, opens it and takes as much wood as the job still needs - leaving a chest it can't get
			/// to alone and trying another
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has taken some</returns>
			private bool TakeFromChest(float dt)
			{
				if (takeAt >= 0f)
				{
					if (Time.time < takeAt)
					{
						return(false);
					}
					takeAt = -1f;
					int taken = life.TakeWood(chest, Mathf.Max(1, wanted - life.CarriedWood()));
					if (taken <= 0)
					{
						// Its inventory is full, or the chest was emptied or opened meanwhile
						if (!life.CanCarryWood())
						{
							fromChests = false;
						}
						chest = null;
						return(false);
					}
					return(true);
				}
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						chest = null;
					}
					return(false);
				}
				life.Face(chest.transform.position);
				VikingPoses.Interact(life.humanoid);
				life.ShowChestOpen(chest);
				takeAt = Time.time + ReachTime;
				return(false);
			}

			/// <summary>
			/// Notes that there's no wood to gather, so the viking leaves work that needs it for a while
			/// </summary>
			private void GiveUp()
			{
				None = true;
				life.noWoodUntil = Time.time + NoWoodWait;
			}

			/// <summary>
			/// Lets go of the wood it was after
			/// </summary>
			public void Release()
			{
				if (woodId != ZDOID.None)
				{
					life.ReleaseWork(woodId);
					woodId = ZDOID.None;
				}
				life.woodInPen = false;
			}
		}

		/// <summary>
		/// Keeping the village's fire stacked - getting wood when it carries none, from the village's chests or the
		/// ground, and adding it a piece at a time, until the fire is full
		/// </summary>
		private class FuelJob : Job
		{
			private readonly Fireplace fire;
			private WoodRun run;
			private float addAt = -1f;
			private int trips;
			private float nextRenew;

			/// <summary>
			/// Makes the job, with the fire already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="target">The fire</param>
			public FuelJob(VillageLife owner, Fireplace target) : base(owner)
			{
				fire = target;
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Gathers wood when it has none and adds it to the fire a piece at a time
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once the fire is full, there's no wood left to gather, or it has added enough</returns>
			public override bool Tick(float dt)
			{
				if (fire == null || IsFull(fire) || trips >= FuelTripsMax)
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.ClaimObject(fire.GetComponent<ZNetView>());
				}
				if (life.CarriedWood() <= 0)
				{
					if (run == null)
					{
						run = new WoodRun(life, Mathf.Min(FuelTripsMax - trips, FuelNeeded(fire)), true);
					}
					if (run.Tick(dt))
					{
						run = null;
					}
					return(run != null && run.None);
				}
				if (addAt >= 0f)
				{
					if (Time.time < addAt)
					{
						return(false);
					}
					ZNetView view = fire.GetComponent<ZNetView>();
					if (view != null && view.IsValid())
					{
						view.InvokeRPC("RPC_AddFuel");
						life.UseWood(1);
						TestLog.Count("Work done", "wood put on fires");
					}
					addAt = -1f;
					trips++;
					return(false);
				}
				// Fuel goes on from outside the flames and the fire's no-walk zone
				float reach = Mathf.Max(FireRing(fire, 0f) - 0.5f, FireSafety.StandOff(fire));
				if (Utils.DistanceXZ(fire.transform.position, life.transform.position) > reach)
				{
					life.Go(dt, fire.transform.position, reach, false);
					return(life.moveFailed);
				}
				life.Face(fire.transform.position);
				VikingPoses.Interact(life.humanoid);
				addAt = Time.time + 0.8f;
				return(false);
			}

			/// <summary>
			/// Checks whether the wood the viking is after lies outside the village
			/// </summary>
			/// <returns>True when it does</returns>
			public override bool Outing()
			{
				return(run != null && run.Outing());
			}

			/// <summary>
			/// Lets go of the fire and the wood it was after
			/// </summary>
			public override void End()
			{
				if (run != null)
				{
					run.Release();
				}
				if (fire != null)
				{
					life.ReleaseObject(fire.GetComponent<ZNetView>());
				}
			}
		}

		/// <summary>
		/// How far from a fire the vikings gather, by how big it is - from the inside of the ring to the outside
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <param name="along">How far across the ring, from 0 to 1</param>
		/// <returns>The distance, in meters</returns>
		public static float FireRing(Fireplace fire, float along)
		{
			return(FireRingFor(Utils.GetPrefabName(fire.gameObject), along));
		}

		/// <summary>
		/// How far from a fire the vikings gather, by its prefab - farther around a bonfire than any other fire
		/// </summary>
		/// <param name="prefab">The fire's prefab name</param>
		/// <param name="along">How far across the ring, from 0 to 1</param>
		/// <returns>The distance, in meters</returns>
		internal static float FireRingFor(string prefab, float along)
		{
			bool bonfire = prefab.StartsWith("bonfire", System.StringComparison.OrdinalIgnoreCase);
			return(bonfire ? Mathf.Lerp(BonfireRingMin, BonfireRingMax, along) : Mathf.Lerp(CampfireRingMin, CampfireRingMax, along));
		}

		/// <summary>
		/// Measures how far a fire's effect areas of some types reach from it
		/// </summary>
		/// <param name="fire">The fire</param>
		/// <param name="types">The types</param>
		/// <returns>The farthest reach, in meters, or 0 when it has none</returns>
		public static float AreaReach(Fireplace fire, EffectArea.Type types)
		{
			float reach = 0f;
			foreach (EffectArea area in fire.GetComponentsInChildren<EffectArea>(true))
			{
				if ((area.m_type & types) == 0)
				{
					continue;
				}
				Vector3 offset = area.transform.position - fire.transform.position;
				offset.y = 0f;
				Vector3 scale = area.transform.lossyScale;
				float size = 0f;
				// Measured from the collider's own size rather than its bounds, which are empty while the area is switched
				// off, as a fire's flames are while it's unlit
				SphereCollider sphere = area.GetComponent<SphereCollider>();
				CapsuleCollider capsule = area.GetComponent<CapsuleCollider>();
				BoxCollider box = area.GetComponent<BoxCollider>();
				if (sphere != null)
				{
					size = sphere.radius * Mathf.Max(scale.x, scale.z);
				}
				else if (capsule != null)
				{
					// A capsule standing up reaches its radius around, and one lying down half its height along it
					float across = capsule.radius * Mathf.Max(scale.x, scale.z);
					size = ((capsule.direction == 1) ? across : Mathf.Max(across, capsule.height / 2f * Mathf.Max(scale.x, scale.z)));
				}
				else if (box != null)
				{
					size = Mathf.Max(box.size.x * scale.x, box.size.z * scale.z) / 2f;
				}
				else
				{
					Collider collider = area.GetComponent<Collider>();
					if (collider != null)
					{
						size = Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z);
					}
				}
				reach = Mathf.Max(reach, offset.magnitude + size);
			}
			return(reach);
		}

		/// <summary>
		/// Works out how near the viking walks to the spot it stands at to work on a piece - near enough that it's within
		/// WorkReach of the piece once there, or right up to the spot when it's too far from the piece for that, as one
		/// beside a piece whose own ground can't be stood on can be
		/// </summary>
		/// <param name="offset">How far the spot is from the part of the piece it works on, seen from above, in meters</param>
		/// <returns>How near it walks to the spot, in meters</returns>
		internal static float StandReach(float offset)
		{
			return(StandReach(offset, WorkReach));
		}

		/// <summary>
		/// Works out how near the viking walks to the spot it stands at to reach something - near enough that it's within
		/// reach of it once there, or right up to the spot when it's too far from it for that
		/// </summary>
		/// <param name="offset">How far the spot is from what it reaches, seen from above, in meters</param>
		/// <param name="reach">How near it has to be to what it reaches, in meters</param>
		/// <returns>How near it walks to the spot, in meters</returns>
		internal static float StandReach(float offset, float reach)
		{
			return(Mathf.Max(ArriveReach, reach - offset));
		}

		/// <summary>
		/// Checks whether the viking is within reach of a piece to work on it - within WorkReach of it, or at the spot it
		/// walks to to reach it
		/// </summary>
		/// <param name="toPoint">How far it is from the part of the piece it works on, seen from above, in meters</param>
		/// <param name="toStand">How far it is from the spot it walks to, seen from above, in meters</param>
		/// <returns>True when it is</returns>
		internal static bool InWorkReach(float toPoint, float toStand)
		{
			return(InReach(toPoint, toStand, WorkReach));
		}

		/// <summary>
		/// Checks whether the viking is within reach of something - within reach of it, or at the spot it walks to to
		/// reach it
		/// </summary>
		/// <param name="toPoint">How far it is from what it reaches, seen from above, in meters</param>
		/// <param name="toStand">How far it is from the spot it walks to, seen from above, in meters</param>
		/// <param name="reach">How near it has to be to what it reaches, in meters</param>
		/// <returns>True when it is</returns>
		internal static bool InReach(float toPoint, float toStand, float reach)
		{
			return(toPoint <= reach || toStand <= ArriveReach);
		}

		/// <summary>
		/// Swinging a hammer at a piece of the village, standing within reach of it
		/// </summary>
		private class HammerWork
		{
			private readonly VillageLife life;
			private readonly int count;
			private int swings;
			private float nextSwing;

			/// <summary>
			/// Whether the viking has got within reach and started swinging
			/// </summary>
			public bool Started
			{
				get
				{
					return(swings > 0);
				}
			}

			/// <summary>
			/// Starts the work for a viking
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="swingCount">How many times it swings</param>
			public HammerWork(VillageLife owner, int swingCount)
			{
				life = owner;
				count = swingCount;
			}

			/// <summary>
			/// Walks within reach of the piece, then faces it and swings the hammer
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="point">Where the piece stands</param>
			/// <returns>True once the last swing's hammer has landed</returns>
			public bool Tick(float dt, Vector3 point)
			{
				return(Tick(dt, point, point));
			}

			/// <summary>
			/// Walks toward a spot until it's within reach of the piece, then faces the piece and swings the hammer
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="point">The part of the piece it works on</param>
			/// <param name="stand">Where it walks to reach it</param>
			/// <returns>True once the last swing's hammer has landed</returns>
			public bool Tick(float dt, Vector3 point, Vector3 stand)
			{
				Vector3 at = life.transform.position;
				if (swings == 0 && !InWorkReach(Utils.DistanceXZ(point, at), Utils.DistanceXZ(stand, at)))
				{
					life.Go(dt, stand, StandReach(Utils.DistanceXZ(stand, point)), false);
					return(false);
				}
				life.Face(point);
				life.HoldHammer(true);
				if (Time.time < nextSwing)
				{
					return(false);
				}
				if (swings >= count)
				{
					return(true);
				}
				VikingPoses.Swing(life.humanoid);
				swings++;
				nextSwing = Time.time + ((swings >= count) ? SwingHitTime : SwingInterval);
				return(false);
			}
		}

		/// <summary>
		/// Finds the point of a piece nearest a spot - on its solid colliders, or where it stands when it has none
		/// </summary>
		/// <param name="piece">The piece</param>
		/// <param name="from">The spot</param>
		/// <returns>The point</returns>
		private static Vector3 NearestPoint(WearNTear piece, Vector3 from)
		{
			Vector3 nearest = piece.transform.position;
			float nearestDistance = float.MaxValue;
			foreach (Collider collider in piece.GetComponentsInChildren<Collider>())
			{
				if (collider == null || collider.isTrigger || !collider.enabled)
				{
					continue;
				}
				// The physics can't find the nearest point of a mesh that isn't convex, so its bounds stand in for it
				MeshCollider mesh = collider as MeshCollider;
				Vector3 point = ((mesh != null && !mesh.convex) ? collider.bounds.ClosestPoint(from) : collider.ClosestPoint(from));
				float distance = Utils.DistanceXZ(point, from);
				if (distance < nearestDistance)
				{
					nearest = point;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// How far from a viking giving up on hammer work flames are looked for, for the log, in meters
		/// </summary>
		private const float GiveUpFlameReach = 10f;

		/// <summary>
		/// Counts the viking giving up on a piece it was to repair or clear the snow off, and in a test run or debug mode
		/// logs why - its walk there stuck or its time up - where it aimed and stood to work, how far it is from each,
		/// how near a fire's flames it is and what moved it last
		/// </summary>
		/// <param name="work">The kind of work, like "Repair"</param>
		/// <param name="piece">The piece</param>
		/// <param name="stuck">True when its walk there got stuck, false when its time ran out</param>
		/// <param name="hasAim">Whether it had picked where to aim and stand</param>
		/// <param name="aim">The part of the piece it worked on</param>
		/// <param name="stand">Where it walked to reach it</param>
		/// <param name="onMesh">Whether that spot was found on the navmesh, rather than being the ground under the aim</param>
		private void LogHammerGiveUp(string work, WearNTear piece, bool stuck, bool hasAim, Vector3 aim, Vector3 stand, bool onMesh)
		{
			TestLog.Count("Hammer work given up", work + ", " + (stuck ? "stuck" : "out of time"));
			if ((!TestLog.On && !Player.m_debugMode) || piece == null)
			{
				return;
			}
			string why = (stuck ? "its walk there got stuck" : "it didn't get within reach in " + RepairGiveUpTime.ToString("F0") + "s");
			string spots = "no spot picked to work from";
			if (hasAim)
			{
				spots = "aiming at " + aim.ToString("F1") + ", " + Utils.DistanceXZ(aim, transform.position).ToString("F1") + "m off, from " + stand.ToString("F1") + (onMesh ? " on the navmesh" : " off the navmesh") + ", " + Utils.DistanceXZ(stand, transform.position).ToString("F1") + "m off";
			}
			string flames = "no flames within " + GiveUpFlameReach.ToString("F0") + "m";
			if (FireSafety.FlamesNear(humanoid.GetCenterPoint(), GiveUpFlameReach, out Vector3 middle, out float reach))
			{
				flames = "the flames of the fire at " + middle.ToString("F1") + " " + Mathf.Max(0f, Utils.DistanceXZ(middle, transform.position) - reach).ToString("F1") + "m off";
				if (hasAim)
				{
					flames += ", " + Mathf.Max(0f, Utils.DistanceXZ(middle, aim) - reach).ToString("F1") + "m from the aim";
				}
			}
			Plugin.Log.LogInfo(work + ": " + TestLog.Name(humanoid, "F1") + " gives up on " + Utils.GetPrefabName(piece.gameObject) + " at " + piece.transform.position.ToString("F1") + " as " + why + ", leaving it alone for " + RepairAvoidTime.ToString("F0") + "s - " + spots + ", " + flames + ", moved by " + mover);
		}

		/// <summary>
		/// Repairing a damaged piece of the village - walking to it and swinging at it, which takes no wood
		/// </summary>
		private class RepairJob : Job
		{
			private readonly WearNTear piece;
			private readonly ZDOID pieceId;
			private HammerWork work;

			/// <summary>
			/// The part of the piece it works on, where it walks to reach it, and whether they've been picked
			/// </summary>
			private Vector3 aim;
			private Vector3 stand;
			private bool hasAim;

			/// <summary>
			/// Whether the spot it walks to was found on the navmesh, rather than being the ground under the aim
			/// </summary>
			private bool standOnMesh;

			/// <summary>
			/// When it gives up on the piece if it hasn't got within reach of it
			/// </summary>
			private readonly float giveUpAt;

			/// <summary>
			/// Makes the job, with the piece already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="target">The piece</param>
			/// <param name="id">Its ZDO</param>
			public RepairJob(VillageLife owner, WearNTear target, ZDOID id) : base(owner)
			{
				piece = target;
				pieceId = id;
				work = new HammerWork(owner, RepairSwings);
				giveUpAt = Time.time + RepairGiveUpTime;
			}

			/// <summary>
			/// Walks to the piece, swings at it and repairs it
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's repaired, or gone, or it can't get there</returns>
			public override bool Tick(float dt)
			{
				if (piece == null || piece.GetHealthPercentage() >= 1f)
				{
					return(true);
				}
				// It can keep moving without ever getting within reach, which never counts as stuck, so it gives up in time
				if (!work.Started && Time.time >= giveUpAt)
				{
					life.LogHammerGiveUp("Repair", piece, false, hasAim, aim, stand, standOnMesh);
					life.ai.StopMoving();
					life.AvoidHammerWork(piece.m_nview);
					return(true);
				}
				// A long piece like a fence is worked on from wherever along it is nearest where the viking started, picked
				// once so the way there doesn't change as it walks - a point up on a roof would have its way to the door
				// flip between inside and out - and walked to on the ground under it
				if (!hasAim)
				{
					aim = NearestPoint(piece, life.transform.position);
					Vector3 ground = aim;
					ground.y = ZoneSystem.instance.GetGroundHeight(aim);
					standOnMesh = NavmeshPoints.FindValidPoint(out Vector3 valid, ground, WorkReach, Pathfinding.AgentType.Humanoid);
					stand = (standOnMesh ? valid : ground);
					hasAim = true;
				}
				if (!work.Tick(dt, aim, stand))
				{
					if (life.moveFailed)
					{
						// It can't get to this piece, so it leaves it alone for a while rather than trying it again
						life.LogHammerGiveUp("Repair", piece, true, hasAim, aim, stand, standOnMesh);
						life.AvoidHammerWork(piece.m_nview);
						return(true);
					}
					return(false);
				}
				if (piece.Repair())
				{
					VillageSignals.SendEffect(VillageSignals.RepairEffect, pieceId);
					TestLog.Count("Work done", "pieces repaired");
				}
				return(true);
			}

			/// <summary>
			/// Lets go of the piece
			/// </summary>
			public override void End()
			{
				life.ReleaseWork(pieceId);
			}
		}

		/// <summary>
		/// Clearing the snow built up on a piece of the village in the Deep North - walking to it and swinging at it, the
		/// way it repairs one, which knocks all of its snow off
		/// </summary>
		private class SnowJob : Job
		{
			private readonly WearNTear piece;
			private readonly ZDOID pieceId;
			private readonly HammerWork work;

			/// <summary>
			/// The part of the piece it works on, where it walks to reach it, and whether they've been picked
			/// </summary>
			private Vector3 aim;
			private Vector3 stand;
			private bool hasAim;

			/// <summary>
			/// Whether the spot it walks to was found on the navmesh, rather than being the ground under the aim
			/// </summary>
			private bool standOnMesh;

			/// <summary>
			/// When it gives up on the piece if it hasn't got within reach of it
			/// </summary>
			private readonly float giveUpAt;

			/// <summary>
			/// Makes the job, with the piece already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="target">The piece</param>
			/// <param name="id">Its ZDO</param>
			public SnowJob(VillageLife owner, WearNTear target, ZDOID id) : base(owner)
			{
				piece = target;
				pieceId = id;
				work = new HammerWork(owner, RepairSwings);
				giveUpAt = Time.time + RepairGiveUpTime;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>The description</returns>
			public override string Describe()
			{
				return("Clear snow" + ((piece != null) ? " off " + Utils.GetPrefabName(piece.gameObject) + ", " + Utils.DistanceXZ(piece.transform.position, life.transform.position).ToString("F1") + "m off" : ""));
			}

			/// <summary>
			/// Walks to the piece, swings at it and clears its snow off
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once its snow is off, or it's gone, or it can't get there</returns>
			public override bool Tick(float dt)
			{
				if (piece == null || VillageSnow.Snow(piece) < VillageSnow.ClearAt)
				{
					return(true);
				}
				if (!work.Started && Time.time >= giveUpAt)
				{
					life.LogHammerGiveUp("Snow", piece, false, hasAim, aim, stand, standOnMesh);
					life.ai.StopMoving();
					life.AvoidHammerWork(piece.m_nview);
					return(true);
				}
				// Picked once and walked to on the ground under it, the way a piece is repaired
				if (!hasAim)
				{
					aim = NearestPoint(piece, life.transform.position);
					Vector3 ground = aim;
					ground.y = ZoneSystem.instance.GetGroundHeight(aim);
					standOnMesh = NavmeshPoints.FindValidPoint(out Vector3 valid, ground, WorkReach, Pathfinding.AgentType.Humanoid);
					stand = (standOnMesh ? valid : ground);
					hasAim = true;
				}
				if (!work.Tick(dt, aim, stand))
				{
					if (life.moveFailed)
					{
						life.LogHammerGiveUp("Snow", piece, true, hasAim, aim, stand, standOnMesh);
						life.AvoidHammerWork(piece.m_nview);
						return(true);
					}
					return(false);
				}
				piece.ChangeSnow(-1f);
				return(true);
			}

			/// <summary>
			/// Lets go of the piece
			/// </summary>
			public override void End()
			{
				life.ReleaseWork(pieceId);
			}
		}

		/// <summary>
		/// Clearing a spot of deep snow off the village's ground in the Deep North - walking to it and swinging at the
		/// ground, which clears it within VillagePaths.SnowReach the way the village's ground is cleared as it's set up
		/// </summary>
		private class SnowGroundJob : Job
		{
			private readonly Vector3 spot;
			private readonly HammerWork work;
			private bool cleared;

			/// <summary>
			/// Where it walks to reach the spot, and whether that's been picked
			/// </summary>
			private Vector3 stand;
			private bool hasStand;

			/// <summary>
			/// When it gives up on the spot if it hasn't got there
			/// </summary>
			private readonly float giveUpAt;

			/// <summary>
			/// Makes the job, with the spot already claimed (see VillageSnow.ClaimDeepSpot)
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="deep">The spot, on the ground</param>
			public SnowGroundJob(VillageLife owner, Vector3 deep) : base(owner)
			{
				spot = deep;
				work = new HammerWork(owner, RepairSwings);
				giveUpAt = Time.time + RepairGiveUpTime;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>The description</returns>
			public override string Describe()
			{
				return("Clear snow off the ground, " + Utils.DistanceXZ(spot, life.transform.position).ToString("F1") + "m off");
			}

			/// <summary>
			/// Walks to the spot, swings at the ground and clears its snow
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's cleared, or it can't get there</returns>
			public override bool Tick(float dt)
			{
				if (!work.Started && Time.time >= giveUpAt)
				{
					life.ai.StopMoving();
					return(true);
				}
				// Where it stands to reach the spot is picked once, so the way there doesn't change as it walks
				if (!hasStand)
				{
					stand = (NavmeshPoints.FindValidPoint(out Vector3 valid, spot, WorkReach, Pathfinding.AgentType.Humanoid) ? valid : spot);
					hasStand = true;
				}
				if (!work.Tick(dt, spot, stand))
				{
					if (life.moveFailed)
					{
						return(true);
					}
					return(false);
				}
				cleared = VillagePaths.ClearSnowAt(spot);
				return(true);
			}

			/// <summary>
			/// Lets go of the spot, taking it off the village's deep snow once it's cleared
			/// </summary>
			public override void End()
			{
				VillageSnow.ReleaseDeepSpot(life.village, spot, cleared);
			}
		}

		/// <summary>
		/// Makes sure the viking carries enough wood to rebuild, getting more when it doesn't - from the village's chests
		/// first, then the ground
		/// </summary>
		/// <param name="needed">The wood the job takes, at most CarryMax</param>
		/// <param name="run">The job's wood run, started and finished here</param>
		/// <param name="dt">Seconds since the AI's last update</param>
		/// <param name="done">Set to true when there's no wood left to get, so the job is over</param>
		/// <returns>True once it carries enough</returns>
		private bool HaveWood(int needed, ref WoodRun run, float dt, out bool done)
		{
			done = false;
			int enough = Mathf.Min(needed, CarryMax);
			if (CarriedWood() >= enough)
			{
				return(true);
			}
			if (run == null)
			{
				run = new WoodRun(this, enough, true);
			}
			if (run.Tick(dt))
			{
				run = null;
				return(false);
			}
			done = run.None;
			return(false);
		}

		/// <summary>
		/// Rebuilding a piece of the village that was destroyed - getting wood from the village's chests or the ground
		/// until it carries the piece's wood, then swinging at where it stood and putting it back
		/// </summary>
		private class RebuildJob : Job
		{
			private readonly PieceRecord record;
			private readonly bool isFire;
			private WoodRun run;
			private HammerWork work;

			/// <summary>
			/// Makes the job, with the piece already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="missing">The piece</param>
			/// <param name="fire">True when it's the village's bonfire or campfire, which is built back as the village's</param>
			public RebuildJob(VillageLife owner, PieceRecord missing, bool fire) : base(owner)
			{
				record = missing;
				isFire = fire;
				work = new HammerWork(owner, RebuildSwings);
			}

			/// <summary>
			/// Gathers the wood, then builds the piece
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's built, there's no more wood to gather, or it can't get there</returns>
			public override bool Tick(float dt)
			{
				if (!life.HaveWood(record.Wood, ref run, dt, out bool done))
				{
					return(done);
				}
				if (!work.Tick(dt, record.Position))
				{
					return(life.moveFailed);
				}
				bool built = (isFire ? life.village.RebuildFire(record) : VillageRecords.Rebuild(life.village, record));
				if (built)
				{
					life.UseWood(record.Wood);
				}
				return(true);
			}

			/// <summary>
			/// Checks whether the wood the viking is after lies outside the village
			/// </summary>
			/// <returns>True when it does</returns>
			public override bool Outing()
			{
				return(run != null && run.Outing());
			}

			/// <summary>
			/// Lets go of the piece and the wood it was after
			/// </summary>
			public override void End()
			{
				if (run != null)
				{
					run.Release();
				}
				life.ReleaseWork(RecordKey(record));
			}
		}

		/// <summary>
		/// Taking down a sitting log left by the village's old fire - walking to it, swinging at it and taking it down,
		/// carrying its wood away to build it back around the new fire (see VillageSeats.TryMove)
		/// </summary>
		private class TakeDownJob : Job
		{
			private readonly ZNetView log;
			private readonly ZDOID id;
			private readonly HammerWork work;

			/// <summary>
			/// Makes the job, with the log already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="standing">The log</param>
			public TakeDownJob(VillageLife owner, ZNetView standing) : base(owner)
			{
				log = standing;
				id = standing.GetZDO().m_uid;
				work = new HammerWork(owner, RebuildSwings);
			}

			/// <summary>
			/// Walks to the log, swings at it and takes it down, keeping its wood
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's down, gone or the viking can't get to it</returns>
			public override bool Tick(float dt)
			{
				if (log == null || !log.IsValid())
				{
					return(true);
				}
				if (!work.Tick(dt, log.transform.position))
				{
					return(life.moveFailed);
				}
				int wood = VillageSeats.TakeDown(log);
				ItemDrop.ItemData plain = ItemOf("Wood");
				if (plain != null)
				{
					life.AddWood(plain, wood);
				}
				return(true);
			}

			/// <summary>
			/// Lets go of the log
			/// </summary>
			public override void End()
			{
				life.ReleaseWork(id);
			}
		}

		/// <summary>
		/// An archer filling its quiver from one of the village's chests - walking to it, opening it, taking the best
		/// arrows for its bow and putting back its own that don't make the cut (see TakeArrows) - or restocking its
		/// silver arrows from it (see TakeSilverArrows)
		/// </summary>
		private class AmmoJob : Job
		{
			private readonly Container chest;
			private readonly bool silver;
			private float actAt = -1f;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="store">The chest, holding arrows for its bow</param>
			/// <param name="silverArrows">True to restock its silver arrows, false to fill its quiver</param>
			public AmmoJob(VillageLife owner, Container store, bool silverArrows = false) : base(owner)
			{
				chest = store;
				silver = silverArrows;
			}

			/// <summary>
			/// Walks to the chest and fills its quiver from it - leaving a chest it can't get to, or that has nothing for
			/// it after all, alone for a while
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's done, or it can't get to the chest</returns>
			public override bool Tick(float dt)
			{
				if (chest == null || !life.Usable(chest))
				{
					return(true);
				}
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						return(true);
					}
					return(false);
				}
				if (actAt < 0f)
				{
					life.Face(chest.transform.position);
					VikingPoses.Interact(life.humanoid);
					life.ShowChestOpen(chest);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (Time.time < actAt)
				{
					return(false);
				}
				if (!(silver ? life.TakeSilverArrows(chest) : life.TakeArrows(chest)))
				{
					life.Avoid(chest.m_nview);
				}
				return(true);
			}
		}

		/// <summary>
		/// Taking up better gear from one of the village's chests - walking to it, opening it and taking out armor, a
		/// shield or a weapon better than its own to wear or wield, carrying what it replaces to put back
		/// </summary>
		private class UpgradeJob : Job
		{
			private Container chest;
			private float actAt = -1f;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="armory">The chest, holding gear the viking would take up</param>
			public UpgradeJob(VillageLife owner, Container armory) : base(owner)
			{
				chest = armory;
			}

			/// <summary>
			/// Walks to the chest and takes up the gear in it - leaving a chest it can't get to alone
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's taken the gear up, the gear's gone, or it can't get to the chest</returns>
			public override bool Tick(float dt)
			{
				if (chest == null || !life.Usable(chest))
				{
					return(true);
				}
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						return(true);
					}
					return(false);
				}
				if (actAt < 0f)
				{
					life.Face(chest.transform.position);
					VikingPoses.Interact(life.humanoid);
					life.ShowChestOpen(chest);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (Time.time < actAt)
				{
					return(false);
				}
				life.TakeUpgrade(chest);
				return(true);
			}
		}

		/// <summary>
		/// Cooking at the village's cooking stations - at the one it set out for and the others at the same fire no other
		/// viking is at, so it cooks all it can at once - putting the raw food it carries on, fetching more from the
		/// village's chests while they have room, waiting by them while it cooks, and taking off what's done, coal
		/// included, to put away - or only taking off what's done, for a viking pulled off other work
		/// </summary>
		private class CookJob : Job
		{
			private readonly List<CookingStation> stations = new List<CookingStation>();
			private Container chest;
			private CookingStation fetchFor;
			private bool chestTried;
			private float actAt = -1f;
			private float nextRenew;
			private float nextHeldTake;

			/// <summary>
			/// What it's doing at its stations, for the debug hover text
			/// </summary>
			private string doing = "starting";

			/// <summary>
			/// Whether it only takes off done food before it burns, and leaves cooking more to others - for a viking pulled
			/// off other work, or keeping guard through a raid
			/// </summary>
			private readonly bool takeOffOnly;

			/// <summary>
			/// Makes the job, with the station already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="cooker">The cooking station</param>
			/// <param name="takeOff">True to only take off done food (see takeOffOnly)</param>
			public CookJob(VillageLife owner, CookingStation cooker, bool takeOff = false) : base(owner)
			{
				stations.Add(cooker);
				nextRenew = Time.time + ClaimRenew;
				takeOffOnly = takeOff;
			}

			/// <summary>
			/// Claims the other stations at the same fire as the one it set out for (see CookTogether) that no other viking
			/// is at, to cook at them too
			/// </summary>
			public override void Begin()
			{
				CookingStation first = stations[0];
				if (first == null)
				{
					return;
				}
				foreach (CookingStation other in VillageCooking.Stations(life.village, first.transform.position))
				{
					ZNetView view = ((other != null) ? other.GetComponent<ZNetView>() : null);
					if (other == first || view == null || Utils.DistanceXZ(other.transform.position, first.transform.position) > CookTogether)
					{
						continue;
					}
					if (life.IsFree(view) && life.ClaimObject(view))
					{
						stations.Add(other);
					}
				}
			}

			/// <summary>
			/// Fetches raw food from a chest when it carries none, as much as its stations have room for, puts it on them a
			/// piece at a time, waits while it cooks and takes off what's done - done food first, before it burns
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once nothing is left to cook or take off, or it can't get to a station</returns>
			public override bool Tick(float dt)
			{
				stations.RemoveAll((CookingStation cooker) => cooker == null || cooker.m_nview == null || !cooker.m_nview.IsValid());
				if (stations.Count == 0)
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					foreach (CookingStation cooker in stations)
					{
						life.ClaimObject(cooker.m_nview);
					}
				}
				if (chest != null)
				{
					return(Fetch(dt));
				}
				CookingStation done = stations.Find((CookingStation cooker) => cooker.HaveDoneItem());
				if (takeOffOnly && done == null)
				{
					return(true);
				}
				int room = 0;
				CookingStation roomy = null;
				foreach (CookingStation cooker in stations)
				{
					int free = (VillageCooking.IsLit(cooker) ? VillageCooking.FreeSlots(cooker) : 0);
					room += free;
					if (free > 0 && roomy == null)
					{
						roomy = cooker;
					}
				}
				ItemDrop.ItemData raw = ((roomy != null) ? life.CarriedRaw(roomy) : null);
				// From dusk on it only finishes what's on the stations, rather than fetching more to cook into the night
				if (done == null && raw == null && roomy != null && !chestTried && !IsDusk() && !IsNight())
				{
					chestTried = true;
					List<Container> chests = life.UsableChests();
					chest = life.Nearest(chests.FindAll((Container candidate) => life.RawIn(candidate, roomy, chests) != null));
					if (chest != null)
					{
						fetchFor = roomy;
						actAt = -1f;
						return(false);
					}
				}
				// Food over a fire that has gone out - burned down, or put out by the rain - would never cook, so it isn't
				// waited on, and is left for when the fire is lit again (see FindTendStation)
				CookingStation cooking = stations.Find((CookingStation cooker) => cooker.HaveUncookedItem() && VillageCooking.IsLit(cooker));
				if (done == null && raw == null && cooking == null)
				{
					return(true);
				}
				CookingStation target = done ?? ((raw != null) ? roomy : cooking);
				if (!InReach(target))
				{
					FireSafety.CookSpot(target.transform.position, life.transform.position, CookReach, out Vector3 spot, out float within);
					doing = "walking to a station, " + Utils.DistanceXZ(spot, life.transform.position).ToString("F1") + "m off";
					life.Go(dt, spot, within, false);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						return(true);
					}
					return(false);
				}
				life.ai.StopMoving();
				life.Face(target.transform.position);
				if (done == null && raw == null)
				{
					// What's on the stations is still cooking
					doing = "waiting for the food to cook";
					return(false);
				}
				doing = ((done != null) ? "taking off done food" : "putting food on");
				if (actAt < 0f)
				{
					VikingPoses.Interact(life.humanoid);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (Time.time < actAt)
				{
					return(false);
				}
				actAt = -1f;
				if (done != null)
				{
					life.TakeCooked(done);
					chestTried = false;
				}
				else
				{
					VillageCooking.Add(roomy, raw.m_dropPrefab.name);
					TestLog.Count("Put on to cook", TestLog.ItemName(raw));
					life.UseCarried(raw, 1);
					life.RaiseSkill(StationSkill(roomy.m_skill), CookAddSkill);
				}
				return(false);
			}

			/// <summary>
			/// Describes the job for the debug hover text - fetching raw food from a chest, or what it's doing at its
			/// stations and how many it has
			/// </summary>
			/// <returns>The description</returns>
			public override string Describe()
			{
				if (chest != null)
				{
					return("Cook, fetching raw food from a chest " + Utils.DistanceXZ(chest.transform.position, life.transform.position).ToString("F1") + "m off");
				}
				return("Cook, " + doing + ", at " + stations.Count + ((stations.Count == 1) ? " station" : " stations"));
			}

			/// <summary>
			/// Checks whether the viking stands where it cooks at a station - near it and outside the flames of the fire
			/// under it (see FireSafety.CookSpot)
			/// </summary>
			/// <param name="cooker">The station</param>
			/// <returns>True when it does</returns>
			private bool InReach(CookingStation cooker)
			{
				FireSafety.CookSpot(cooker.transform.position, life.transform.position, CookReach, out Vector3 spot, out float within);
				return(Utils.DistanceXZ(spot, life.transform.position) <= Mathf.Max(within, ArriveReach));
			}

			/// <summary>
			/// Keeps done food on the viking's stations from burning while a player holds it - with its inventory open, or
			/// naming it - taking it off a station within its reach where it stands
			/// </summary>
			/// <returns>True when done food is on a station out of its reach, so it leaves the player to take it off</returns>
			public bool TendWhileHeld()
			{
				CookingStation done = stations.Find((CookingStation cooker) => cooker != null && cooker.m_nview != null && cooker.m_nview.IsValid() && cooker.HaveDoneItem());
				if (done == null)
				{
					return(false);
				}
				if (!InReach(done))
				{
					return(true);
				}
				if (Time.time >= nextHeldTake)
				{
					nextHeldTake = Time.time + ReachTime;
					VikingPoses.Interact(life.humanoid);
					life.TakeCooked(done);
					chestTried = false;
				}
				return(false);
			}

			/// <summary>
			/// Walks to the chest, opens it and takes as much raw food as its stations have room for - leaving a chest it
			/// can't get to alone
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>False, so the job goes on</returns>
			private bool Fetch(float dt)
			{
				if (!life.Usable(chest))
				{
					chest = null;
					return(false);
				}
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						chest = null;
					}
					return(false);
				}
				if (actAt < 0f)
				{
					life.Face(chest.transform.position);
					VikingPoses.Interact(life.humanoid);
					life.ShowChestOpen(chest);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (Time.time < actAt)
				{
					return(false);
				}
				actAt = -1f;
				int room = 0;
				foreach (CookingStation cooker in stations)
				{
					room += ((cooker != null && VillageCooking.IsLit(cooker)) ? VillageCooking.FreeSlots(cooker) : 0);
				}
				if (fetchFor != null)
				{
					life.TakeRaw(chest, fetchFor, room);
				}
				chest = null;
				return(false);
			}

			/// <summary>
			/// Lets go of its stations
			/// </summary>
			public override void End()
			{
				foreach (CookingStation cooker in stations)
				{
					if (cooker != null)
					{
						life.ReleaseObject(cooker.m_nview);
					}
				}
			}
		}

		/// <summary>
		/// A party of vikings from the Plains on hunting together (see GroupHunts) - its leader, everyone in it, the
		/// leader among them, the prey they're after, and how far along they are: gathering by the leader until every one
		/// of them is there or the time's up, then following the leader out until it's within PartyEngage of the prey,
		/// then all going for it at once
		/// </summary>
		/// <remarks>
		/// In the fight whichever of them the prey is after keeps it busy, kiting it just out of its reach, while the rest
		/// fight it (see Kites) - so when it turns on another of them, that one takes over the kiting. Everyone in a
		/// party is run by the client that owns its leader, since a party only takes vikings that client owns
		/// </remarks>
		private class HuntParty
		{
			public VillageLife Leader;
			public readonly List<VillageLife> Members = new List<VillageLife>();
			public Character Prey;
			public bool Setting;
			public bool Fighting;
			public readonly float GatherUntil;

			/// <summary>
			/// Makes a party with its leader in it
			/// </summary>
			/// <param name="leader">The viking leading it</param>
			/// <param name="prey">The prey it's after</param>
			/// <param name="gatherUntil">Until when the leader waits for the rest to gather, in Time.time seconds</param>
			public HuntParty(VillageLife leader, Character prey, float gatherUntil)
			{
				Leader = leader;
				Members.Add(leader);
				Prey = prey;
				GatherUntil = gatherUntil;
			}

			/// <summary>
			/// Drops whoever is no longer hunting with the party, and has another of them lead it when its leader has
			/// dropped out
			/// </summary>
			public void Prune()
			{
				Members.RemoveAll((VillageLife member) => member == null || !(member.job is HuntJob hunt) || !hunt.InParty(this));
				if (!Members.Contains(Leader) && Members.Count > 0)
				{
					Leader = Members[0];
				}
			}
		}

		/// <summary>
		/// Hunting by day - going out for the nearest prey within HuntReach of the village, from its biome's prey and
		/// those before it and right after it (see HuntPrey) - a boar, neck or greyling for a viking of the Meadows, and
		/// for an archer a deer too - or only prey and birds that drop something it's short of to craft with (see
		/// GatherFor), fighting it and the greylings it meets, and gathering what drops from the kill and from the
		/// zombie it rises as, to cook or put away back home
		/// </summary>
		/// <remarks>
		/// An archer hunting alone shoots wild prey from afar itself (see Snipe), and otherwise its AI does the fighting,
		/// with the prey treated as an enemy (see Hunts). From the Plains on, wild prey that isn't only for archers is
		/// hunted by a party, each of them with a job of its own (see HuntParty and PartyHunt). When any other enemy
		/// comes near or goes for it, or it's below HuntRetreatShare of its health, it gives up and runs home (see
		/// UpdateFlee)
		/// </remarks>
		private class HuntJob : Job
		{
			private readonly bool archer;
			private readonly string wanted;
			private readonly bool livestock;

			/// <summary>
			/// Whether it only hunts wild prey that drops food, for a village short of it (see FindFoodWork)
			/// </summary>
			private readonly bool foodOnly;
			private Character prey;
			private Vector3 kill;
			private bool engaged;
			private bool collecting;

			/// <summary>
			/// Whether an archer's AI has taken over the fight with its prey from shooting it from afar (see Snipe), and
			/// how many arrows it has loosed at it from afar
			/// </summary>
			private bool handedOver;
			private int snipeShots;

			/// <summary>
			/// Whether the viking stalked its prey this update, where it's stalking toward, the prey it worked that out for
			/// and when it works it out again (see Stalk)
			/// </summary>
			private bool stalked;
			private Vector3 stalkSpot;
			private Character stalkPrey;
			private float nextStalkPlan;
			private float collectUntil;
			private float giveUpAt;
			private ZNetView drop;
			private ZDOID dropId;
			private float actAt = -1f;
			private float nextRenew;

			/// <summary>
			/// Whether the livestock it's slaughtering stands in a pen the viking isn't in, and when it next looks
			/// </summary>
			private bool penApart;
			private float nextPenCheck;

			/// <summary>
			/// The bird an archer is shooting for its feathers, where it last saw it, when it took off, or below 0 while
			/// it's landed, and how many arrows it has loosed at it and when it can loose the next
			/// </summary>
			private RandomFlyingBird bird;
			private ZDOID birdId;
			private Vector3 birdLast;
			private float birdFlyingSince;
			private int shots;
			private float nextShot;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="isArcher">Whether the viking is an archer, which also hunts the prey only archers hunt and
			/// shoots birds</param>
			/// <param name="item">The prefab name of what it hunts for something to craft with, so only prey or birds that
			/// drop it are hunted (see GatherFor), or null for any prey</param>
			/// <param name="slaughter">True to take one of the village's livestock instead of wild prey</param>
			/// <param name="hunters">The hunting party the viking hunts with, or null to hunt alone</param>
			/// <param name="forFood">True to only hunt wild prey that drops food, leaving birds alone</param>
			public HuntJob(VillageLife owner, bool isArcher, string item, bool slaughter, HuntParty hunters = null, bool forFood = false) : base(owner)
			{
				archer = isArcher;
				wanted = item;
				livestock = slaughter;
				foodOnly = forFood;
				party = hunters;
				prey = ((hunters != null) ? hunters.Prey : null);
				NoteFood(prey);
			}

			/// <summary>
			/// Notes that the viking found food around the village when the wild prey it goes for drops some (see
			/// VillageLife.NoteFoodFound) - not livestock, or prey that drops none, like a greyling
			/// </summary>
			/// <param name="target">The prey, or null</param>
			private void NoteFood(Character target)
			{
				if (!livestock && target != null && QuestLoot.DropsFood(Utils.GetPrefabName(target.gameObject)))
				{
					life.NoteFoodFound();
				}
			}

			/// <summary>
			/// The hunting party the viking hunts with, or null when it hunts alone
			/// </summary>
			private readonly HuntParty party;

			/// <summary>
			/// Checks whether the viking hunts with a party
			/// </summary>
			/// <param name="hunters">The party</param>
			/// <returns>True when it does</returns>
			public bool InParty(HuntParty hunters)
			{
				return(party != null && party == hunters);
			}

			/// <summary>
			/// Checks whether the viking is fighting a creature as the prey of its hunting party
			/// </summary>
			/// <param name="creature">The creature</param>
			/// <returns>True when it is</returns>
			public bool PartyFights(Character creature)
			{
				return(party != null && party.Fighting && creature != null && creature == party.Prey);
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name, and what it's out for when it's after something to craft with or only food</returns>
			public override string Describe()
			{
				string name = (livestock ? "Slaughter" : ((party != null) ? "Group hunt" : "Hunt"));
				if (wanted != null)
				{
					return(name + " (" + wanted + ")");
				}
				return(foodOnly ? name + " (food)" : name);
			}

			/// <summary>
			/// Notes that the viking is hunting, so no other viking of the village starts to
			/// </summary>
			public override void Begin()
			{
				life.SetDoing(HuntingKey, true);
				nextRenew = Time.time + ClaimRenew;
				giveUpAt = Time.time + HuntTime;
			}

			/// <summary>
			/// Checks whether the hunter fights a creature rather than running from it - its prey, a greyling, or the
			/// zombie its kill rises as
			/// </summary>
			/// <param name="creature">The creature</param>
			/// <returns>True when it does</returns>
			public bool Allows(Character creature)
			{
				if (creature == null)
				{
					return(false);
				}
				if (creature == prey || IsHuntedMonster(creature))
				{
					return(true);
				}
				return((engaged || collecting) && ZombieState.IsZombie(creature) && Utils.DistanceXZ(creature.transform.position, kill) <= CollectRadius * 2f);
			}

			/// <summary>
			/// Checks whether the viking is shooting a creature from afar as its prey (see Snipe), which its AI leaves to
			/// it until it hands the fight over
			/// </summary>
			/// <param name="creature">The creature</param>
			/// <returns>True when it is</returns>
			public bool Snipes(Character creature)
			{
				return(creature != null && creature == prey && archer && !livestock && party == null && !handedOver && life.ai.m_targetCreature != creature);
			}

			/// <summary>
			/// Checks whether the viking is slaughtering one of the village's livestock rather than hunting wild prey
			/// </summary>
			/// <returns>True when it is</returns>
			public bool Slaughters()
			{
				return(livestock);
			}

			/// <summary>
			/// Checks whether a creature is the prey the viking is after
			/// </summary>
			/// <param name="creature">The creature</param>
			/// <returns>True when it is</returns>
			public bool IsPreyOf(Character creature)
			{
				return(creature != null && creature == prey);
			}

			/// <summary>
			/// Gives up on the prey, leaving it alone for a while (see LetGo) - when a fence keeps the viking from it, say
			/// </summary>
			public void GiveUp()
			{
				LetGo();
			}

			/// <summary>
			/// Checks whether the hunt has the viking out of the village
			/// </summary>
			/// <returns>True when its prey or kill is outside it</returns>
			public override bool Outing()
			{
				return((prey != null && !life.village.Contains(prey.transform.position)) || (bird != null && !life.village.Contains(bird.transform.position)) || ((engaged || collecting) && !life.village.Contains(kill)));
			}

			/// <summary>
			/// Finds prey, stalks it and goes for it, then gathers what drops from the kill - crouched only while it stalks,
			/// so it stands up once it's shooting, fighting or done
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's gathered what dropped, there's no prey, it's hunted for HuntTime, or it's dusk</returns>
			public override bool Tick(float dt)
			{
				stalked = false;
				try
				{
					return(Hunt(dt));
				}
				finally
				{
					life.body.SetStalking(stalked);
				}
			}

			/// <summary>
			/// Finds prey, closes in on it and goes for it, then gathers what drops from the kill
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's gathered what dropped, there's no prey, it's hunted for HuntTime, or it's dusk</returns>
			private bool Hunt(float dt)
			{
				if (IsNight() || IsDusk())
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					life.SetDoing(HuntingKey, true);
					nextRenew = Time.time + ClaimRenew;
				}
				if (collecting)
				{
					return(Collect(dt));
				}
				if (Time.time >= giveUpAt)
				{
					return(true);
				}
				Vector3 position = life.transform.position;
				if (!life.village.Contains(position) && life.EnemyNear(position, ForageSafety, (Character other) => Allows(other) || IgnoredByHunter(prey, other)))
				{
					life.Flee(FleeReason.Forage);
					return(true);
				}
				if (birdId != ZDOID.None)
				{
					return(ShootBird(dt));
				}
				if (party != null)
				{
					return(PartyHunt(dt, position));
				}
				if (prey == null || prey.IsDead())
				{
					if (engaged)
					{
						collecting = true;
						collectUntil = Time.time + CollectTime;
						hunts.Remove(life.humanoid);
						return(false);
					}
					prey = life.FindPrey(archer, wanted, livestock, false, foodOnly);
					// An archer goes for a bird when it's nearer than any prey, when it drops what it's after, unless it's
					// slaughtering livestock or only after food, which birds don't drop
					RandomFlyingBird nearBird = ((archer && !livestock && !foodOnly) ? life.FindBird(wanted) : null);
					if (nearBird != null && (prey == null || Vector3.Distance(nearBird.transform.position, position) < Vector3.Distance(prey.transform.position, position)))
					{
						prey = null;
						StartBird(nearBird);
						return(false);
					}
					if (prey == null)
					{
						return(true);
					}
					NoteFood(prey);
					hunts[life.humanoid] = prey;
					handedOver = false;
					snipeShots = 0;
					life.UpdateOuting(Outing());
				}
				kill = prey.transform.position;
				// Prey that runs off too far, or onto ground harder than the hunter's own biome, is let go
				if (life.village.DistanceOutside(kill) > HuntReach + HuntChase || (!livestock && life.TooHardToHunt(kill)))
				{
					LetGo();
					return(false);
				}
				// Livestock in a pen is slaughtered inside it, never through its fence, so the viking gets into the pen first.
				// A spot just outside a pen can count as fenced in too, so a piece between them means it's outside
				if (livestock && Time.time >= nextPenCheck)
				{
					nextPenCheck = Time.time + SlaughterPenCheck;
					penApart = (Village.FencedIn(kill) && (!Village.FencedIn(position) || PieceAcross(position, kill)));
				}
				if (livestock && penApart)
				{
					life.mover = "getting into the pen";
					life.Go(dt, kill, SlaughterReach, true);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						LetGo();
					}
					return(false);
				}
				// An archer shoots wild prey from afar until it comes close, or it's missed it too often
				if (archer && !livestock && party == null && !handedOver)
				{
					return(Snipe(dt, position));
				}
				float range = (archer ? ArcherEngage : MeleeEngage);
				if (Utils.DistanceXZ(kill, position) > range)
				{
					// Wild prey is stalked, and livestock in its pen just walked up to
					if (livestock)
					{
						life.Go(dt, kill, range * 0.8f, true);
					}
					else
					{
						Stalk(dt, position, range * 0.8f);
					}
					if (life.moveFailed)
					{
						life.moveFailed = false;
						LetGo();
					}
					return(false);
				}
				// Its AI takes it from here, and its job carries on once the fight is over
				engaged = true;
				life.ai.m_targetCreature = prey;
				life.ai.SetAlerted(true);
				return(false);
			}

			/// <summary>
			/// Hunts with the party (see HuntParty) - gathering by the leader, following it out while it stalks the prey,
			/// then going for the prey along with the rest once the leader is within PartyEngage of it, which the AI takes
			/// from there. Once the prey is down the leader gathers what dropped and the rest go back to their day. A party
			/// that's lost too many before the fight starts, or whose prey got away or out of reach, breaks up
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="position">Where the viking stands</param>
			/// <returns>True once the viking's part in the hunt is over</returns>
			private bool PartyHunt(float dt, Vector3 position)
			{
				party.Prune();
				Character target = party.Prey;
				if (target == null || target.IsDead())
				{
					// The kill is where the prey was last seen
					if (engaged && life == party.Leader)
					{
						collecting = true;
						collectUntil = Time.time + CollectTime;
						hunts.Remove(life.humanoid);
						return(false);
					}
					return(true);
				}
				if (!party.Fighting && party.Members.Count < PartyMin)
				{
					return(true);
				}
				if (prey != target)
				{
					NoteFood(target);
				}
				prey = target;
				kill = prey.transform.position;
				hunts[life.humanoid] = prey;
				if (life.village.DistanceOutside(kill) > HuntReach + HuntChase)
				{
					party.Prey = null;
					LetGo();
					return(true);
				}
				if (party.Fighting)
				{
					engaged = true;
					life.ai.m_targetCreature = prey;
					life.ai.SetAlerted(true);
					return(false);
				}
				VillageLife leader = party.Leader;
				if (!party.Setting)
				{
					bool gathered = party.Members.TrueForAll((VillageLife member) => Utils.DistanceXZ(member.transform.position, leader.transform.position) <= PartyGatherRange);
					if (gathered || Time.time >= party.GatherUntil)
					{
						party.Setting = true;
					}
					else if (life != leader)
					{
						life.mover = "joining the hunt";
						life.Go(dt, leader.transform.position, PartyGatherRange / 2f, true);
						return(false);
					}
					else
					{
						life.mover = "gathering the hunt";
						life.ai.StopMoving();
						return(false);
					}
				}
				if (life == leader)
				{
					if (Utils.DistanceXZ(kill, position) <= PartyEngage)
					{
						party.Fighting = true;
						return(false);
					}
					life.mover = "leading the hunt after " + prey.GetHoverName();
					Stalk(dt, position, PartyEngage * 0.8f);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						party.Prey = null;
						LetGo();
						return(true);
					}
					return(false);
				}
				life.mover = "following the hunt";
				float behind = Utils.DistanceXZ(leader.transform.position, position);
				if (behind > PartyFollow)
				{
					life.Go(dt, leader.transform.position, PartyFollow / 2f, behind > PartyGatherRange * 2f);
				}
				else
				{
					life.ai.StopMoving();
				}
				return(false);
			}

			/// <summary>
			/// Shoots the prey from afar - closing in to within its bow's range of it (see VillageLife.SnipeRange), and
			/// nearer while something stands
			/// between them, then standing with its bow drawn, aiming ahead of it and above it for the arrow's drop the
			/// way it aims at a bird, and loosing an arrow every BirdShotInterval seconds. Once the prey comes within
			/// SnipeHandOver, or it has loosed SnipeShotsMax arrows, its AI takes the fight over (see HandOver)
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="position">Where the viking stands</param>
			/// <returns>False, so the hunt goes on</returns>
			private bool Snipe(float dt, Vector3 position)
			{
				float range = life.SnipeRange();
				float distance = Utils.DistanceXZ(kill, position);
				if (distance <= SnipeHandOver || snipeShots >= SnipeShotsMax || life.ai.m_targetCreature == prey)
				{
					HandOver();
					return(false);
				}
				Vector3 center = prey.GetCenterPoint();
				if (distance > range || !ClearShot(center))
				{
					life.aimingBow = false;
					life.mover = "stalking " + prey.GetHoverName();
					Stalk(dt, position, ((distance > range) ? range * 0.8f : SnipeHandOver));
					if (life.moveFailed)
					{
						life.moveFailed = false;
						LetGo();
					}
					return(false);
				}
				life.mover = "shooting at " + prey.GetHoverName();
				life.aimingBow = true;
				life.ai.StopMoving();
				Vector3 aim = life.BirdAim(center, prey.GetVelocity());
				life.ai.LookAt(aim);
				if (Time.time >= nextShot && life.ReadyToShoot(aim) && life.humanoid.StartAttack(null, false))
				{
					snipeShots++;
					nextShot = Time.time + BirdShotInterval;
					engaged = true;
				}
				return(false);
			}

			/// <summary>
			/// Closes in on the prey - hurrying while it's farther off than StalkRange, and from there stalking it, crouched
			/// and quiet at a walk (see LivingVikingBody.SetStalking), toward a spot standOff from it. The spot is off the
			/// prey's back when the viking can get there by swinging no more than StalkSwingMax around it from the way it's
			/// coming, on ground it can walk to - and straight from the way it's coming otherwise, rather than circling all
			/// the way around. Worked out again every StalkReplan seconds as the prey moves and turns
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="position">Where the viking stands</param>
			/// <param name="standOff">How near the prey it goes</param>
			private void Stalk(float dt, Vector3 position, float standOff)
			{
				if (Utils.DistanceXZ(kill, position) > StalkRange)
				{
					life.Go(dt, kill, standOff, true);
					return;
				}
				stalked = true;
				if (Time.time >= nextStalkPlan || stalkPrey != prey)
				{
					nextStalkPlan = Time.time + StalkReplan;
					stalkPrey = prey;
					stalkSpot = StalkSpot(position, standOff);
				}
				life.Go(dt, stalkSpot, StalkSpotReach, false);
			}

			/// <summary>
			/// Works out where the viking stalks toward (see Stalk)
			/// </summary>
			/// <param name="position">Where the viking stands</param>
			/// <param name="standOff">How far from the prey the spot is</param>
			/// <returns>The spot</returns>
			private Vector3 StalkSpot(Vector3 position, float standOff)
			{
				Vector3 from = position - kill;
				from.y = 0f;
				if (from.sqrMagnitude < 0.01f)
				{
					return(kill);
				}
				from.Normalize();
				Vector3 straight = kill + from * standOff;
				Vector3 behind = -prey.transform.forward;
				behind.y = 0f;
				if (behind.sqrMagnitude < 0.01f)
				{
					return(straight);
				}
				// How far around the prey it would have to go to come at its back, kept to StalkSwingMax so it never circles
				// all the way around to get there
				float around = Vector3.SignedAngle(from, behind.normalized, Vector3.up);
				float swing = StalkSwing(around);
				if (swing == 0f)
				{
					return(straight);
				}
				Vector3 spot = kill + Quaternion.Euler(0f, swing, 0f) * from * standOff;
				// Only where the ground lets it - a spot it can stand on and walk to
				if (!NavmeshPoints.FindValidPoint(out Vector3 valid, spot, StalkSpotReach, Pathfinding.AgentType.Humanoid) || !life.ai.HavePath(valid))
				{
					return(straight);
				}
				return(valid);
			}

			/// <summary>
			/// Checks whether nothing stands between the viking's eye and a point on the prey
			/// </summary>
			/// <param name="point">The point</param>
			/// <returns>True when the way is clear</returns>
			private bool ClearShot(Vector3 point)
			{
				Vector3 eye = life.humanoid.m_eye.position;
				return(!Physics.Linecast(eye, point, BaseAI.m_viewBlockMask, QueryTriggerInteraction.Ignore));
			}

			/// <summary>
			/// Hands the fight with the prey over to the viking's AI, which closes in and fights it with whatever suits
			/// the fight - for prey that came close, or that the viking kept missing
			/// </summary>
			private void HandOver()
			{
				handedOver = true;
				engaged = true;
				life.aimingBow = false;
				life.ai.m_targetCreature = prey;
				life.ai.SetAlerted(true);
			}

			/// <summary>
			/// Sets out after a bird, claiming it so no other archer goes for it
			/// </summary>
			/// <param name="target">The bird</param>
			private void StartBird(RandomFlyingBird target)
			{
				bird = target;
				birdId = target.m_nview.GetZDO().m_uid;
				birdLast = target.transform.position;
				birdFlyingSince = -1f;
				shots = 0;
				nextShot = 0f;
				life.ClaimWork(birdId);
				life.UpdateOuting(Outing());
			}

			/// <summary>
			/// Closes in on the bird to within BirdRange, then stands with its bow drawn and looses an arrow at it every
			/// BirdShotInterval seconds while it's landed - never while it's in the air, when it waits for it to land again,
			/// following it, for up to BirdLandWait seconds - giving up after BirdShotsMax, when it flies too far out or
			/// doesn't land again in time, and once it's down, gathers its feathers where it fell
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>False, so the hunt goes on</returns>
			private bool ShootBird(float dt)
			{
				// A bird struck by an arrow breaks apart into its feathers
				if (bird == null)
				{
					bool hit = (shots > 0);
					ReleaseBird();
					if (hit)
					{
						kill = birdLast;
						kill.y = ZoneSystem.instance.GetGroundHeight(kill);
						engaged = true;
						collecting = true;
						collectUntil = Time.time + CollectTime;
					}
					return(false);
				}
				Vector3 position = bird.transform.position;
				birdLast = position;
				if (shots >= BirdShotsMax || life.village.DistanceOutside(position) > HuntReach + HuntChase || life.TooHardToHunt(position))
				{
					LetGoBird();
					return(false);
				}
				bool landed = Landed(bird);
				if (landed)
				{
					birdFlyingSince = -1f;
				}
				else if (birdFlyingSince < 0f)
				{
					birdFlyingSince = Time.time;
				}
				else if (Time.time - birdFlyingSince > BirdLandWait)
				{
					LetGoBird();
					return(false);
				}
				if (Utils.DistanceXZ(position, life.transform.position) > BirdRange)
				{
					life.aimingBow = false;
					Vector3 under = position;
					under.y = ZoneSystem.instance.GetGroundHeight(under);
					life.Go(dt, under, BirdRange * 0.8f, true);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						LetGoBird();
					}
					return(false);
				}
				life.mover = (landed ? "shooting at a bird" : "waiting for a bird to land");
				life.aimingBow = true;
				life.ai.StopMoving();
				Vector3 aim = (landed ? life.BirdAim(position, Vector3.zero) : position);
				life.ai.LookAt(aim);
				if (landed && Time.time >= nextShot && life.ReadyToShoot(aim) && life.humanoid.StartAttack(null, false))
				{
					shots++;
					nextShot = Time.time + BirdShotInterval;
				}
				return(false);
			}

			/// <summary>
			/// Gives up on a bird it couldn't hit or get to, leaving it alone for a while
			/// </summary>
			private void LetGoBird()
			{
				if (bird != null && bird.m_nview != null)
				{
					life.Avoid(bird.m_nview);
				}
				ReleaseBird();
			}

			/// <summary>
			/// Lets go of the bird it was after and puts its bow away
			/// </summary>
			private void ReleaseBird()
			{
				if (birdId != ZDOID.None)
				{
					life.ReleaseWork(birdId);
					birdId = ZDOID.None;
				}
				bird = null;
				shots = 0;
				life.aimingBow = false;
			}

			/// <summary>
			/// Gives up on prey that got away or that it can't get to, leaving it alone for a while - and its AI lets go of
			/// it too, since it's no longer an enemy once it's no longer hunted
			/// </summary>
			private void LetGo()
			{
				if (prey != null && prey.m_nview != null)
				{
					life.Avoid(prey.m_nview);
				}
				if (prey != null && life.ai.m_targetCreature == prey)
				{
					life.ai.m_targetCreature = null;
				}
				hunts.Remove(life.humanoid);
				prey = null;
				engaged = false;
				handedOver = false;
				snipeShots = 0;
				life.aimingBow = false;
			}

			/// <summary>
			/// Gathers what dropped around the kill - waiting while the kill rises as a zombie and its AI fights it, and a
			/// little longer after each thing it picks up
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once nothing more has dropped for a while</returns>
			private bool Collect(float dt)
			{
				if (actAt >= 0f)
				{
					if (Time.time < actAt)
					{
						return(false);
					}
					actAt = -1f;
					life.PickUpDrop(drop);
					ReleaseDrop();
					collectUntil = Mathf.Max(collectUntil, Time.time + ReachTime * 4f);
					return(false);
				}
				if (drop == null || !drop.IsValid())
				{
					ReleaseDrop();
					drop = life.FindDrop(kill);
					if (drop != null)
					{
						dropId = drop.GetZDO().m_uid;
						life.ClaimWork(dropId);
					}
				}
				if (drop != null)
				{
					if (Utils.DistanceXZ(drop.transform.position, life.transform.position) > ItemReach)
					{
						life.Go(dt, drop.transform.position, ItemReach, false);
						if (life.moveFailed)
						{
							life.moveFailed = false;
							life.Avoid(drop);
							ReleaseDrop();
						}
						return(false);
					}
					life.Face(drop.transform.position);
					VikingPoses.Interact(life.humanoid);
					RequestItem(drop);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (VillageHazards.NearestRising(kill, CollectRadius, out VillageHazards.Rising _))
				{
					collectUntil = Mathf.Max(collectUntil, Time.time + ReachTime * 4f);
				}
				life.ai.StopMoving();
				return(Time.time >= collectUntil);
			}

			/// <summary>
			/// Lets go of the dropped item it was after
			/// </summary>
			private void ReleaseDrop()
			{
				if (dropId != ZDOID.None)
				{
					life.ReleaseWork(dropId);
					dropId = ZDOID.None;
				}
				drop = null;
			}

			/// <summary>
			/// Stops hunting - letting go of its prey and what it was gathering, standing up from stalking, and noting it's
			/// no longer hunting
			/// </summary>
			public override void End()
			{
				life.body.SetStalking(false);
				ReleaseDrop();
				ReleaseBird();
				hunts.Remove(life.humanoid);
				if (prey != null && life.ai.m_targetCreature == prey)
				{
					life.ai.m_targetCreature = null;
				}
				life.SetDoing(HuntingKey, false);
			}
		}

		/// <summary>
		/// Foraging by day - taking the honey from the beehives built in the village, and picking berries, mushrooms and
		/// other food growing within ForageReach of the village, and flint, away from enemies, until it carries ForageLoad or
		/// there's no more a chest has room for, then going home, where it puts what it gathered away as loot
		/// </summary>
		/// <remarks>
		/// When an enemy comes for it or it's hurt outside the village, UpdateFlee runs it home for help
		/// </remarks>
		private class ForageJob : Job
		{
			private Pickable target;
			private Beehive hive;
			private ZDOID targetId;
			private int picked;
			private float pickAt = -1f;
			private float nextRenew;

			/// <summary>
			/// The prefab name of the one thing the viking gathers, like stone for a hoe, or null for any food or flint
			/// </summary>
			private readonly string only;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="onlyItem">The prefab name of the one thing it gathers, leaving the beehives alone, or null
			/// for any food or flint</param>
			public ForageJob(VillageLife owner, string onlyItem = null) : base(owner)
			{
				only = onlyItem;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name, and the one thing it gathers</returns>
			public override string Describe()
			{
				return((only != null) ? "Forage (" + only + ")" : "Forage");
			}

			/// <summary>
			/// Notes that the viking is foraging, so no other viking of the village starts to
			/// </summary>
			public override void Begin()
			{
				life.SetDoing(ForagingKey, true);
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Checks whether the food the viking is after is outside the village
			/// </summary>
			/// <returns>True when it is</returns>
			public override bool Outing()
			{
				return(target != null && !life.village.Contains(target.transform.position));
			}

			/// <summary>
			/// Finds the nearest food, walks to it and picks it, until it has gathered enough - heading home as soon as
			/// an enemy comes near
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has gathered enough or there's no more to gather, or it's dusk</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk() || picked >= ForageLoad)
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					life.SetDoing(ForagingKey, true);
					nextRenew = Time.time + ClaimRenew;
				}
				if (!life.village.Contains(life.transform.position) && life.DangerNear(life.transform.position))
				{
					life.fleeing = true;
					return(true);
				}
				if (hive != null)
				{
					return(Harvest(dt));
				}
				if (target == null || !IsForage(target))
				{
					Release();
					// The village's beehives come first, since they're close by
					hive = ((only == null) ? life.FindHive() : null);
					if (hive != null)
					{
						targetId = hive.m_nview.GetZDO().m_uid;
						life.ClaimWork(targetId);
						pickAt = -1f;
						return(false);
					}
					target = life.FindForage(only);
					if (target == null)
					{
						return(true);
					}
					targetId = target.m_nview.GetZDO().m_uid;
					life.ClaimWork(targetId);
					life.UpdateOuting(Outing());
					pickAt = -1f;
				}
				if (pickAt >= 0f)
				{
					if (Time.time < pickAt)
					{
						return(false);
					}
					int got = life.PickForage(target);
					// Only food picked counts as food found around the village, not flint, stone or what the cauldron takes
					if (got > 0 && VikingFood.IsFood(ForageFood(target)))
					{
						life.NoteFoodFound();
					}
					picked += got;
					Release();
					target = null;
					return(false);
				}
				if (Utils.DistanceXZ(target.transform.position, life.transform.position) > ItemReach)
				{
					life.Go(dt, target.transform.position, ItemReach, false);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(target.m_nview);
						Release();
						target = null;
					}
					return(false);
				}
				life.Face(target.transform.position);
				VikingPoses.Interact(life.humanoid);
				pickAt = Time.time + ReachTime;
				return(false);
			}

			/// <summary>
			/// Walks to the beehive and takes its honey - leaving a hive it can't get to alone
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>False, so the job goes on</returns>
			private bool Harvest(float dt)
			{
				if (hive == null || hive.GetHoneyLevel() <= 0)
				{
					Release();
					hive = null;
					return(false);
				}
				if (pickAt >= 0f)
				{
					if (Time.time < pickAt)
					{
						return(false);
					}
					picked += life.TakeHoney(hive);
					Release();
					hive = null;
					return(false);
				}
				if (Utils.DistanceXZ(hive.transform.position, life.transform.position) > ChestReach)
				{
					life.Go(dt, hive.transform.position, ChestReach, false);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(hive.m_nview);
						Release();
						hive = null;
					}
					return(false);
				}
				life.Face(hive.transform.position);
				VikingPoses.Interact(life.humanoid);
				pickAt = Time.time + ReachTime;
				return(false);
			}

			/// <summary>
			/// Lets go of the food or beehive it was after
			/// </summary>
			private void Release()
			{
				if (targetId != ZDOID.None)
				{
					life.ReleaseWork(targetId);
					targetId = ZDOID.None;
				}
			}

			/// <summary>
			/// Lets go of the food it was after, keeping what it gathered to put away, and notes it's no longer foraging
			/// </summary>
			public override void End()
			{
				Release();
				life.SetDoing(ForagingKey, false);
			}
		}

		/// <summary>
		/// Eating from a chest when wounded, or to keep its foods up while there's food enough to go around (see
		/// FoodToSpare) - walking to the chest it eats from (see FindFoodChest), opening it and eating the food there it
		/// eats now (see FoodIn)
		/// </summary>
		private class EatJob : Job
		{
			private Container chest;
			private float eatAt = -1f;
			private readonly bool topUp;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="pantry">The chest, with food in it the viking can eat</param>
			/// <param name="topUp">True when it's keeping its foods up rather than eating for its wounds</param>
			public EatJob(VillageLife owner, Container pantry, bool topUp) : base(owner)
			{
				chest = pantry;
				this.topUp = topUp;
			}

			/// <summary>
			/// Walks to the chest and eats - finding another when this one's food is gone or it can't be used, and
			/// leaving eating for EatRetryTime when there's none, or leaving a chest it can't get to alone
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has eaten, or there's no food it can eat</returns>
			public override bool Tick(float dt)
			{
				if (chest == null || !life.Usable(chest) || life.FoodIn(chest, topUp) == null)
				{
					chest = life.FindFoodChest(topUp);
					eatAt = -1f;
					if (chest == null)
					{
						life.eatAgainAt = Time.time + EatRetryTime;
						return(true);
					}
				}
				if (eatAt >= 0f)
				{
					if (Time.time < eatAt)
					{
						return(false);
					}
					eatAt = -1f;
					if (life.EatFrom(chest, topUp))
					{
						return(true);
					}
					chest = null;
					return(false);
				}
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						chest = null;
					}
					return(false);
				}
				life.Face(chest.transform.position);
				VikingPoses.Interact(life.humanoid);
				chest.m_openEffects.Create(chest.transform.position, chest.transform.rotation);
				eatAt = Time.time + ReachTime;
				return(false);
			}
		}

		/// <summary>
		/// Stocking the village's chests with wood by day - gathering branches and wood from the ground until it carries
		/// as much as the chest it picked has room for, or as much as it can carry, then putting it in that chest or the
		/// nearest other with room
		/// </summary>
		private class StockJob : Job
		{
			private Container chest;
			private WoodRun run;
			private bool loaded;
			private float actAt = -1f;
			private float nextRenew;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="store">The chest to stock, with room for wood</param>
			public StockJob(VillageLife owner, Container store) : base(owner)
			{
				chest = store;
			}

			/// <summary>
			/// Notes that the viking is stocking the chests, so no other viking of the village starts to
			/// </summary>
			public override void Begin()
			{
				life.SetDoing(StockingKey, true);
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Gathers wood, then takes it to the chest
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's put the wood away, there's no wood to gather, no chest has room, or it's dusk</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk())
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					life.SetDoing(StockingKey, true);
					nextRenew = Time.time + ClaimRenew;
				}
				if (!loaded)
				{
					return(Gather(dt));
				}
				return(PutAway(dt));
			}

			/// <summary>
			/// Gathers wood from the ground until it carries enough
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True when there was no wood to gather and it carries none</returns>
			private bool Gather(float dt)
			{
				ItemDrop.ItemData plain = ItemOf("Wood");
				if (plain == null || chest == null)
				{
					return(true);
				}
				int wanted = Mathf.Min(CarryMax, ChestRoom(chest, plain, null));
				if (life.CarriedWood() >= wanted || !life.CanCarryWood())
				{
					loaded = true;
					return(false);
				}
				if (run == null)
				{
					run = new WoodRun(life, wanted, false);
				}
				if (run.Tick(dt))
				{
					run = null;
					return(false);
				}
				if (run.None)
				{
					run.Release();
					run = null;
					if (life.CarriedWood() <= 0)
					{
						return(true);
					}
					loaded = true;
				}
				return(false);
			}

			/// <summary>
			/// Walks to the chest, or the nearest other with room when it's full or can't be used, and puts the wood in -
			/// keeping the wood for its work when no chest has room, and leaving a chest it can't get to alone
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's put the wood in, or no chest has room</returns>
			private bool PutAway(float dt)
			{
				if (life.CarriedWood() <= 0)
				{
					return(true);
				}
				ItemDrop.ItemData plain = ItemOf("Wood");
				if (chest == null || !life.Usable(chest) || (plain != null && ChestRoom(chest, plain, null) <= 0))
				{
					chest = life.FindStockChest();
					actAt = -1f;
					if (chest == null)
					{
						return(true);
					}
				}
				if (actAt >= 0f)
				{
					if (Time.time < actAt)
					{
						return(false);
					}
					actAt = -1f;
					if (life.PutWood(chest) > 0)
					{
						return(true);
					}
					// It was opened meanwhile, or has no room for the kinds of wood it carries
					life.Avoid(chest.m_nview);
					chest = null;
					return(false);
				}
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						chest = null;
					}
					return(false);
				}
				life.Face(chest.transform.position);
				VikingPoses.Interact(life.humanoid);
				life.ShowChestOpen(chest);
				actAt = Time.time + ReachTime;
				return(false);
			}

			/// <summary>
			/// Checks whether the wood the viking is after lies outside the village
			/// </summary>
			/// <returns>True when it does</returns>
			public override bool Outing()
			{
				return(run != null && run.Outing());
			}

			/// <summary>
			/// Lets go of the wood it was after, and notes it's no longer stocking the chests
			/// </summary>
			public override void End()
			{
				if (run != null)
				{
					run.Release();
				}
				life.SetDoing(StockingKey, false);
			}
		}

		/// <summary>
		/// Putting loot away - by day, or at any hour to clear up after a fight - picking up an item lying around the
		/// village, when one of its chests has room for it, and carrying it to the nearest chest with room, or putting
		/// away what it already carries - and carrying what the vikings don't use out of the village when the chests hold
		/// enough of it
		/// </summary>
		private class LootJob : Job
		{
			private readonly bool afterFight;
			private readonly bool takeOutside;
			private readonly bool cookFirst;
			private ZNetView loot;
			private ZDOID lootId;
			private Container chest;
			private float actAt = -1f;
			private float nextRoomCheck;
			private bool dumping;
			private Vector3 dumpSpot;

			/// <summary>
			/// Makes the job, with the loot already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="target">The loot to pick up, or null to put away what it carries</param>
			/// <param name="fight">True for clearing up after a fight, which goes on through dusk and night</param>
			/// <param name="outside">True when the loot was picked as something the chests hold enough of to carry out of
			/// the village, as what an enemy dropped is, so it's still wanted once the viking gets to it, whether or not
			/// it may leave the village then</param>
			/// <param name="cook">True to leave raw food it carries for a cooking station with room by day (see
			/// FindWork), false when no cooking work could be had for it, so the job doesn't end only to be picked
			/// again</param>
			public LootJob(VillageLife owner, ZNetView target, bool fight, bool outside = false, bool cook = true) : base(owner)
			{
				loot = target;
				lootId = ((target != null) ? target.GetZDO().m_uid : ZDOID.None);
				afterFight = fight;
				takeOutside = outside;
				cookFirst = cook;
			}

			/// <summary>
			/// Picks up the loot, and more lying near it as long as it can carry more without being held back by its load -
			/// part of a stack when that's all it has room for - then takes it to the chests and puts it in, putting down
			/// where it stands what no chest has room for anymore
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's all put away, gone, or it's dusk, unless it's clearing up after a fight</returns>
			public override bool Tick(float dt)
			{
				if (!afterFight && (IsNight() || IsDusk()))
				{
					return(true);
				}
				if (loot != null)
				{
					return(PickUp(dt));
				}
				// Raw meat it picked up goes straight to a cooking station with room by day, and the rest is put away
				// along with what it cooked (see FindWork)
				if (cookFirst && !dumping && !IsNight() && !IsDusk() && life.FindCookStation(true) != null)
				{
					return(true);
				}
				ItemDrop.ItemData carried = life.CarriedLoot();
				return((carried != null) ? PutAway(dt, carried) : true);
			}

			/// <summary>
			/// Goes for more loot lying near the viking after it picked some up, while it can carry more without being
			/// held back by its load (see FindLoot)
			/// </summary>
			private void PickNext()
			{
				ZNetView next = life.FindLoot();
				if (next == null || Utils.DistanceXZ(next.transform.position, life.transform.position) > LootBatchReach || !life.ClaimWork(next.GetZDO().m_uid))
				{
					return;
				}
				loot = next;
				lootId = next.GetZDO().m_uid;
				life.UpdateOuting(Outing());
			}

			/// <summary>
			/// Walks to the loot and picks it up, while a chest still has room for it - checked every RoomCheckInterval
			/// on the way and again once it's there - then goes for the next lying near it
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True when it's gone, there's no room for it, or there's no way to it, and it carries nothing to
			/// put away</returns>
			private bool PickUp(float dt)
			{
				if (loot == null || !IsLoot(loot))
				{
					Release();
					loot = null;
					return(life.CarriedLoot() == null);
				}
				if (actAt >= 0f)
				{
					if (Time.time < actAt)
					{
						return(false);
					}
					actAt = -1f;
					life.PickUpLoot(loot);
					Release();
					loot = null;
					PickNext();
					return(loot == null && life.CarriedLoot() == null);
				}
				// Out of the village with a monster in sight, it leaves the loot and heads home before it's seen
				if (!life.village.Contains(loot.transform.position) && life.DangerNear(life.transform.position))
				{
					Release();
					loot = null;
					life.Flee(FleeReason.Forage);
					return(life.CarriedLoot() == null);
				}
				bool reached = (Utils.DistanceXZ(loot.transform.position, life.transform.position) <= ItemReach);
				if (reached || Time.time >= nextRoomCheck)
				{
					nextRoomCheck = Time.time + RoomCheckInterval;
					ItemDrop.ItemData item = loot.GetComponent<ItemDrop>().m_itemData;
					if (life.CarryRoom(item, CarryLoot) <= 0 || !WantsLoot(item, life.UsableChests(), takeOutside || life.MayLeave()))
					{
						Release();
						loot = null;
						return(life.CarriedLoot() == null);
					}
				}
				if (!reached)
				{
					life.Go(dt, loot.transform.position, ItemReach, false);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.DebugAvoid(loot, "it got no nearer on its way there, " + Utils.DistanceXZ(loot.transform.position, life.transform.position).ToString("F1") + "m from it");
						life.Avoid(loot);
						Release();
						loot = null;
						return(life.CarriedLoot() == null);
					}
					return(false);
				}
				life.Face(loot.transform.position);
				VikingPoses.Interact(life.humanoid);
				RequestItem(loot);
				actAt = Time.time + ReachTime;
				return(false);
			}

			/// <summary>
			/// Walks to the nearest chest with room for what it carries, opens it and puts it in - leaving a chest it
			/// can't get to alone and trying another. Something the vikings don't use that no chest takes, because the
			/// chests hold enough of it, it carries out of the village and drops on the village's pile there instead (see
			/// FindDumpSpot) - holding on to it while there's nowhere to drop it, for DumpGiveUpTime seconds before it
			/// puts it down where it stands - and anything else no chest has room for it puts down where it stands
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="carried">The loot it carries</param>
			/// <returns>True once it's put away, put down or dropped outside, or it's holding on to it for now</returns>
			private bool PutAway(float dt, ItemDrop.ItemData carried)
			{
				if (dumping)
				{
					return(Dump(dt));
				}
				if (chest == null || !life.Usable(chest) || Time.time >= nextRoomCheck)
				{
					nextRoomCheck = Time.time + RoomCheckInterval;
					if (chest == null || !life.Usable(chest) || !HasRoom(chest, carried, life.UsableChests()))
					{
						chest = life.FindChestFor(carried);
						actAt = -1f;
					}
					if (chest == null)
					{
						life.ai.StopMoving();
						if (KindOf(carried) != StoreKind.Other)
						{
							life.PutDownLoot(false, "no chest has room for it");
							return(life.CarriedLoot() == null);
						}
						if (!life.MayLeave())
						{
							life.dumpAgainAt = Time.time + DumpRetryTime;
							return(true);
						}
						if (!life.FindDumpSpot(out dumpSpot))
						{
							if (!life.DumpLater())
							{
								life.PutDownLoot(true, "the chests hold enough of it, and there's been nowhere outside the village to drop it for " + DumpGiveUpTime.ToString("F0") + "s");
							}
							return(true);
						}
						life.dumpNowhereSince = -1f;
						dumping = true;
						life.UpdateOuting(true);
						return(false);
					}
				}
				if (actAt >= 0f)
				{
					if (Time.time < actAt)
					{
						return(false);
					}
					actAt = -1f;
					if (life.PutLoot(chest, carried))
					{
						// The rest it carries goes in the same chest when it fits
						nextRoomCheck = 0f;
						return(life.CarriedLoot() == null);
					}
					chest = null;
					return(false);
				}
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						chest = null;
					}
					return(false);
				}
				life.Face(chest.transform.position);
				VikingPoses.Interact(life.humanoid);
				life.ShowChestOpen(chest);
				actAt = Time.time + ReachTime;
				return(false);
			}

			/// <summary>
			/// Lets go of the loot it was after
			/// </summary>
			private void Release()
			{
				if (lootId != ZDOID.None)
				{
					life.ReleaseWork(lootId);
					lootId = ZDOID.None;
				}
			}

			/// <summary>
			/// Walks out of the village to where it drops what the chests hold enough of, and drops it there, marked so no
			/// viking stores it again - dropping it where it got to when it can't get there
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's dropped it and carries nothing else to put away</returns>
			private bool Dump(float dt)
			{
				if (Utils.DistanceXZ(dumpSpot, life.transform.position) > DumpArrive && !life.Go(dt, dumpSpot, DumpArrive, false))
				{
					return(false);
				}
				life.moveFailed = false;
				life.ai.StopMoving();
				VikingPoses.Interact(life.humanoid);
				life.PutDownLoot(true, "the chests hold enough of it");
				dumping = false;
				chest = null;
				return(life.CarriedLoot() == null);
			}

			/// <summary>
			/// Checks whether the loot the viking is after lies outside the village, or it's taking what the chests hold
			/// enough of out of the village
			/// </summary>
			/// <returns>True when it does</returns>
			public override bool Outing()
			{
				return(dumping || (loot != null && !life.village.Contains(loot.transform.position)));
			}

			/// <summary>
			/// Lets go of the loot it was after, keeping what it carries for the next time it puts loot away
			/// </summary>
			public override void End()
			{
				Release();
			}
		}

		/// <summary>
		/// Gathering around the village's fire at dusk, standing in a ring facing it, with some sitting down once the
		/// torches are out and facing whoever sits next to them - and a wounded viking sitting by it straight away, in
		/// its warmth, to rest, which by day a wounded viking or one getting rested again does until it's rested. By day
		/// in freezing weather, a viking sits in a lit fire's warmth the same way to keep from freezing until the weather
		/// clears, or stands inside its house when there's no fire burning. In the rain a wounded viking whose place by the
		/// fire isn't under a roof sits on its house's floor instead, dry, until it's no longer badly hurt or the rain
		/// stops, and in a thunderstorm the village gathers inside its houses
		/// </summary>
		private class GatherJob : Job
		{
			private readonly bool healing;
			private readonly bool warming;
			private Fireplace fire;
			private System.Random rolls;
			private Vector3 spot;
			private float reach = 0.75f;
			private float ringMin;
			private float ringMax;
			private bool hasSpot;
			private bool towardFire;
			private int tries;
			private bool sits;
			private float sitAt;
			private bool sitting;
			private float satAt;
			private bool settled;
			private Vector3 settledAt;
			private float settledTime;
			private bool goingBack;

			/// <summary>
			/// Whether the viking went inside its house instead of to the fire (see GoInside), and when it next checks
			/// whether the rain has it go inside
			/// </summary>
			private bool indoors;
			private float nextRainCheck;

			/// <summary>
			/// The seat by the fire it sits on, the piece it's part of, whether it's on it yet, and when its claim on it is
			/// renewed
			/// </summary>
			private Chair seat;
			private ZNetView seatView;
			private bool seated;
			private float nextRenew;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="rest">True for resting by the fire by day until it's rested, sitting at once in its warmth</param>
			/// <param name="warm">True for keeping warm by day while the weather is freezing, sitting at once in a lit
			/// fire's warmth</param>
			public GatherJob(VillageLife owner, bool rest, bool warm = false) : base(owner)
			{
				healing = rest;
				warming = warm;
			}

			/// <summary>
			/// Gets the fire the viking gathers at
			/// </summary>
			/// <returns>The fire, or null when it gathers at its doorstep</returns>
			public Fireplace Fire()
			{
				return(fire);
			}

			/// <summary>
			/// Checks whether this is the village's gathering at the end of the day, rather than resting or keeping warm
			/// </summary>
			/// <returns>True when it is</returns>
			public bool Evening()
			{
				return(!healing && !warming);
			}

			/// <summary>
			/// Picks the fire it gathers at (see ChooseGatherFire), whether and when the viking sits - a wounded or cold one
			/// at once - and its place in the ring, a free seat by the fire for one that sits when there is one, each viking
			/// keeping its own side of the fire and a wounded or cold one keeping within the fire's warmth, or its doorstep
			/// when there's no fire. The ring widens past the fire's room for as many as gather there, keeping as much
			/// ground for each. It goes inside its house instead (see GoInside) to keep warm when the fire isn't burning, to
			/// gather in a thunderstorm, and to rest in the rain when its place by the fire isn't under a roof, since a wet
			/// viking can't rest
			/// </summary>
			public override void Begin()
			{
				// The whole village doesn't crowd into a pen around a fire that stands in one, so it gathers at home
				fire = life.ChooseGatherFire();
				rolls = new System.Random(life.nview.GetZDO().m_uid.GetHashCode());
				if (warming && (fire == null || !fire.IsBurning() || Time.time < life.warmInsideUntil))
				{
					GoInside();
					return;
				}
				if (!healing && !warming && VikingWeather.Storming() && VikingWeather.RainsOn(life.transform.position))
				{
					GoInside();
					return;
				}
				bool wounded = (healing || warming || life.Wounded());
				sits = (fire != null && (rolls.NextDouble() < GatherSitChance || wounded));
				sitAt = (wounded ? 0f : Mathf.Lerp(GatherSitStart, GatherSitEnd, (float)rolls.NextDouble()));
				if (fire != null)
				{
					ringMin = FireRing(fire, 0f);
					ringMax = FireRing(fire, 1f);
					ringMax = WidenRing(ringMin, ringMax, life.Gatherers(fire) + 1, FireRoom(fire));
					if (wounded)
					{
						WarmRing(fire, ref ringMin, ref ringMax);
					}
					if (sits && FindSeat())
					{
						spot = seat.m_attachPoint.position;
						reach = GatherSeatReach;
						hasSpot = true;
					}
					else
					{
						hasSpot = PickSpot();
					}
					if (healing && hasSpot && Exposed(spot + Vector3.up))
					{
						LeaveSeat();
						GoInside();
					}
					return;
				}
				if (life.Home(out VillagePaths.House house))
				{
					List<Vector3> steps = Village.Doorsteps(house);
					spot = ((steps.Count > 0) ? steps[0] : house.Center);
					hasSpot = true;
				}
			}

			/// <summary>
			/// Has the viking go inside its house instead of to the fire - to a spot on its floor clear of the doors (see
			/// FloorSpot), or nowhere when it has no house standing
			/// </summary>
			private void GoInside()
			{
				fire = null;
				indoors = true;
				reach = 0.75f;
				hasSpot = false;
				if (life.Home(out VillagePaths.House shelter))
				{
					spot = FloorSpot(shelter);
					hasSpot = true;
				}
			}

			/// <summary>
			/// Checks whether a spot is out in the rain - raining on it, and not under a roof
			/// </summary>
			/// <param name="point">The spot, at about a body's middle</param>
			/// <returns>True when it is</returns>
			private static bool Exposed(Vector3 point)
			{
				return(VikingWeather.RainsOn(point) && !VikingWeather.UnderRoof(point));
			}

			/// <summary>
			/// Picks the next place in the ring that's on the navmesh and outside the houses, or once GatherTries
			/// places have failed, the fire itself to get as near to as it can
			/// </summary>
			/// <returns>False when there's nowhere left to try</returns>
			private bool PickSpot()
			{
				while (tries < GatherTries)
				{
					tries++;
					float angle = (float)(rolls.NextDouble() * 360.0);
					float distance = Mathf.Lerp(ringMin, ringMax, (float)rolls.NextDouble());
					Vector3 point = fire.transform.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
					if (Pathfinding.instance == null)
					{
						spot = point;
						return(true);
					}
					if (!NavmeshPoints.FindValidPoint(out Vector3 valid, point, GatherSnap, Pathfinding.AgentType.Humanoid))
					{
						continue;
					}
					float fromFire = Utils.DistanceXZ(valid, fire.transform.position);
					if (fromFire < ringMin - 0.5f || fromFire > ringMax + 1f || life.village.InHouse(valid, 0.5f))
					{
						continue;
					}
					spot = valid;
					return(true);
				}
				if (towardFire)
				{
					return(false);
				}
				towardFire = true;
				spot = fire.transform.position;
				reach = ringMax;
				return(true);
			}

			/// <summary>
			/// Finds the free seat by the fire nearest the viking and claims it - a sitting log, bench or chair outside the
			/// houses, its seat clear of the flames and no farther from the fire than the ring, with no player on it
			/// </summary>
			/// <returns>False when there's none</returns>
			private bool FindSeat()
			{
				Vector3 middle = fire.transform.position;
				float flames = AreaReach(fire, EffectArea.Type.Burning);
				Chair best = null;
				ZNetView bestView = null;
				float bestDistance = float.MaxValue;
				foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
				{
					ZNetView view = instance.Value;
					if (view == null || !view.IsValid() || Utils.DistanceXZ(view.transform.position, middle) > ringMax + GatherSeatSearch)
					{
						continue;
					}
					Chair candidate = view.GetComponentInChildren<Chair>();
					if (candidate == null || candidate.m_attachPoint == null || candidate.m_inShip)
					{
						continue;
					}
					Vector3 point = candidate.m_attachPoint.position;
					float fromFire = Utils.DistanceXZ(point, middle);
					if (fromFire > ringMax + GatherSeatSlack || fromFire < flames || life.village.InHouse(point, 0.5f) || PlayerOn(candidate) || !life.IsFree(view))
					{
						continue;
					}
					float distance = Utils.DistanceXZ(point, life.transform.position);
					if (distance < bestDistance)
					{
						best = candidate;
						bestView = view;
						bestDistance = distance;
					}
				}
				if (best == null || !life.ClaimObject(bestView))
				{
					return(false);
				}
				seat = best;
				seatView = bestView;
				nextRenew = Time.time + ClaimRenew;
				return(true);
			}

			/// <summary>
			/// Checks whether a player sits on a seat
			/// </summary>
			/// <param name="chair">The seat</param>
			/// <returns>True when one does</returns>
			private static bool PlayerOn(Chair chair)
			{
				return(Player.IsPlayerInRange(chair.m_attachPoint.position, GatherSeatTaken));
			}

			/// <summary>
			/// Lets go of its seat, getting off it first when it's on it
			/// </summary>
			private void LeaveSeat()
			{
				if (seated)
				{
					VikingPoses.SetChair(life.humanoid, false);
					life.Detach();
					seated = false;
					sitting = false;
				}
				if (seatView != null)
				{
					life.ReleaseObject(seatView);
				}
				seat = null;
				seatView = null;
			}

			/// <summary>
			/// Narrows the ring to within the fire's warmth, where a viking rests, and clear of its flames
			/// </summary>
			/// <param name="fire">The fire</param>
			/// <param name="min">The ring's inside, in meters from the fire</param>
			/// <param name="max">The ring's outside</param>
			public static void WarmRing(Fireplace fire, ref float min, ref float max)
			{
				WarmRingWithin(AreaReach(fire, EffectArea.Type.Heat), AreaReach(fire, EffectArea.Type.Burning), ref min, ref max);
			}

			/// <summary>
			/// Checks whether the viking sits by the fire
			/// </summary>
			/// <returns>True while it does</returns>
			public override bool Sitting()
			{
				return(sitting);
			}

			/// <summary>
			/// Checks whether the viking rests by the fire by day, rather than gathering at dusk
			/// </summary>
			/// <returns>True when it does</returns>
			public bool Heals()
			{
				return(healing);
			}

			/// <summary>
			/// Checks whether the viking keeps warm in freezing weather by day, rather than gathering at dusk
			/// </summary>
			/// <returns>True when it does</returns>
			public bool Warms()
			{
				return(warming);
			}

			/// <summary>
			/// Describes the job for the debug hover text - gathering at the fire at dusk, resting by it by day, or keeping
			/// warm by it or inside in freezing weather
			/// </summary>
			/// <returns>What it's doing, and whether it's sitting yet</returns>
			public override string Describe()
			{
				string name = (warming ? "Warm up" : (healing ? "Rest" : "Gather")) + (indoors ? " inside" : " by fire");
				return(name + (seated ? ", on a seat" : (sitting ? ", sitting" : (settled ? ", standing" : ", on the way"))));
			}

			/// <summary>
			/// Walks to its place and faces the fire, sitting down at its time if it's one that sits - on its seat, or
			/// else on the ground, turning to the nearest viking sitting next to it - until night falls, or by day until
			/// it's rested. It sits in the ring when it finds no way to its seat or a player takes it, tries another place
			/// when it finds no way to its own, stands where it got to when it finds none, and goes back to its place
			/// when it's pushed off it
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's the viking's bedtime (see Bedtime) or day, or for a wounded viking by day, once it's
			/// rested or it's dusk - or resting inside, once it's no longer badly hurt or the rain stops - and for a cold
			/// one, once the weather clears, it's dusk or its fire goes out. Out in the rain where it would be better off
			/// inside (see GoInside), it's done too, so it's picked again for inside</returns>
			public override bool Tick(float dt)
			{
				if (warming)
				{
					if (IsNight() || IsDusk() || !VikingWeather.FreezingFor(life.humanoid) || (fire != null && !fire.IsBurning()))
					{
						return(true);
					}
				}
				else if (healing ? (IsNight() || IsDusk() || life.body.Rest.IsRested() || (indoors && (!life.Wounded() || !VikingWeather.RainsOn(life.transform.position)))) : (!IsDusk() && (!IsNight() || life.Bedtime())))
				{
					return(true);
				}
				if (settled && !indoors && !warming && Time.time >= nextRainCheck)
				{
					nextRainCheck = Time.time + RainCheckInterval;
					if ((healing || VikingWeather.Storming()) && Exposed(life.humanoid.GetCenterPoint()))
					{
						return(true);
					}
				}
				if (!hasSpot)
				{
					life.ai.StopMoving();
					return(false);
				}
				if (!settled)
				{
					if (!life.Go(dt, spot, reach, false))
					{
						return(false);
					}
					if (life.moveFailed)
					{
						life.moveFailed = false;
						if (Utils.DistanceXZ(life.transform.position, spot) > reach + 1f)
						{
							if (seat != null)
							{
								// It found no way to its seat, so it sits in the ring instead
								LeaveSeat();
								reach = 0.75f;
							}
							if (!goingBack && fire != null && PickSpot())
							{
								return(false);
							}
							sits = false;
						}
					}
					settled = true;
					goingBack = false;
					settledAt = life.transform.position;
					settledTime = Time.time;
					if (healing && !sits && !indoors)
					{
						// It found no way to a seat by the fire
						life.restAgainAt = Time.time + RestRetryTime;
						return(true);
					}
				}
				if (warming && fire != null && Time.time - settledTime > WarmFireTimeout && VikingWeather.IsFreezing(life.humanoid))
				{
					// It couldn't get into the fire's warmth, so it goes inside instead
					life.warmInsideUntil = Time.time + WarmInsideTime;
					return(true);
				}
				if (healing && sitting && !indoors && Time.time - satAt > RestRetryTime && !life.body.Rest.IsResting())
				{
					// Its seat isn't warm enough to rest at
					life.restAgainAt = Time.time + RestRetryTime;
					return(true);
				}
				// Resting inside, it sits down on the floor
				if (healing && indoors && !sitting)
				{
					VikingPoses.SetSitting(life.humanoid, true);
					sitting = true;
					satAt = Time.time;
				}
				life.ai.StopMoving();
				if (seatView != null && (seat == null || !seatView.IsValid()))
				{
					// Its seat was broken
					LeaveSeat();
				}
				if (seatView != null && Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					if (!life.ClaimObject(seatView))
					{
						LeaveSeat();
					}
				}
				if (!seated && Utils.DistanceXZ(life.transform.position, settledAt) > PushedDistance)
				{
					if (sitting)
					{
						VikingPoses.SetSitting(life.humanoid, false);
						sitting = false;
					}
					settled = false;
					goingBack = true;
					spot = settledAt;
					reach = 0.75f;
					return(false);
				}
				if (fire == null)
				{
					return(false);
				}
				if (sits && !sitting && EnvMan.instance.GetDayFraction() >= sitAt)
				{
					if (seat != null && PlayerOn(seat))
					{
						LeaveSeat();
					}
					if (seat != null)
					{
						life.Attach(seat.m_attachPoint, seatView.gameObject);
						VikingPoses.SetChair(life.humanoid, true);
						seated = true;
					}
					else
					{
						VikingPoses.SetSitting(life.humanoid, true);
					}
					sitting = true;
					satAt = Time.time;
				}
				// A seat faces the way it was built
				if (seated)
				{
					return(false);
				}
				VillageLife neighbor = (sitting ? SittingNeighbor() : null);
				life.Face((neighbor != null) ? neighbor.transform.position : fire.transform.position);
				return(false);
			}

			/// <summary>
			/// Finds the nearest other viking sitting at the fire within GatherNeighbor of this one
			/// </summary>
			/// <returns>The viking's life, or null</returns>
			private VillageLife SittingNeighbor()
			{
				VillageLife nearest = null;
				float nearestDistance = GatherNeighbor;
				foreach (VillageLife other in All)
				{
					if (other == life || !(other.job is GatherJob gather) || !gather.sitting)
					{
						continue;
					}
					float distance = Utils.DistanceXZ(other.transform.position, life.transform.position);
					if (distance <= nearestDistance)
					{
						nearest = other;
						nearestDistance = distance;
					}
				}
				return(nearest);
			}

			/// <summary>
			/// Gets up, and lets go of its seat
			/// </summary>
			public override void End()
			{
				if (sitting && !seated)
				{
					VikingPoses.SetSitting(life.humanoid, false);
				}
				LeaveSeat();
			}
		}

		/// <summary>
		/// Teaching a forsaken power at the village's gathering at dusk - walking up to one of the village who doesn't
		/// know a power the viking does and using it beside them, so they learn it, along with everyone else it reaches,
		/// while no other viking sets out to teach them (see VikingPowers.ClaimPupil)
		/// </summary>
		private class TeachJob : Job
		{
			private readonly Character pupil;
			private readonly int power;
			private float usedAt = -1f;
			private float nextRenew;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="learner">The viking it teaches</param>
			/// <param name="taught">The power it teaches, by index in VikingPowers.Names</param>
			public TeachJob(VillageLife owner, Character learner, int taught) : base(owner)
			{
				pupil = learner;
				power = taught;
			}

			/// <summary>
			/// Finds the viking it teaches, which it walks right up to rather than around
			/// </summary>
			/// <returns>The viking</returns>
			public override Character Focus()
			{
				return(pupil);
			}

			/// <summary>
			/// Notes the viking it teaches, so no other viking sets out to teach them too
			/// </summary>
			public override void Begin()
			{
				life.body.Powers.ClaimPupil(pupil);
				nextRenew = Time.time + VikingPowers.PupilClaimTime * 0.5f;
			}

			/// <summary>
			/// Walks up to the viking it teaches and uses the power beside them, waiting while another viking near is in
			/// the act of using it, then stands by them a moment - giving up once dusk is over, after TeachTimeout
			/// seconds, or when they've died or learned it meanwhile
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's done</returns>
			public override bool Tick(float dt)
			{
				if (usedAt >= 0f)
				{
					if (pupil != null)
					{
						life.Face(pupil.transform.position);
					}
					return(Time.time - usedAt >= TeachLinger);
				}
				if (pupil == null || pupil.IsDead() || !IsDusk() || Time.time - StartedAt > TeachTimeout || (VikingPowers.KnownBy(pupil) & (1 << power)) != 0)
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					life.body.Powers.ClaimPupil(pupil);
					nextRenew = Time.time + VikingPowers.PupilClaimTime * 0.5f;
				}
				if (Utils.DistanceXZ(pupil.transform.position, life.transform.position) > TeachReach)
				{
					life.Go(dt, pupil.transform.position, TeachReach, false);
					return(life.moveFailed);
				}
				life.ai.StopMoving();
				life.Face(pupil.transform.position);
				if (life.body.Powers.Use(power, pupil))
				{
					usedAt = Time.time;
					VikingPowers.LogTeaching(life.humanoid, pupil, power);
					return(false);
				}
				// Another viking near using the same power has it wait, while a cooldown that has started since ends it
				return(!life.body.Powers.Ready());
			}

			/// <summary>
			/// Lets go of the viking it was teaching
			/// </summary>
			public override void End()
			{
				life.body.Powers.ClaimPupil(null);
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>The power it teaches and to whom</returns>
			public override string Describe()
			{
				string name = ((pupil != null) ? VikingNames.Get(pupil) : "");
				return("Teach " + VikingPowers.Names[power].Substring(3) + " to " + ((name.Length > 0) ? name : "a viking"));
			}
		}

		/// <summary>
		/// Resting at night - lying in a free bed in the village, or else sitting on a chair or the floor of its house -
		/// and getting up in the morning, after a moment and maybe with a stretch
		/// </summary>
		private class RestJob : Job
		{
			private Bed bed;
			private Chair chair;
			private VillagePaths.House house;
			private bool hasHouse;
			private Vector3 spot;
			private bool hasSpot;
			private bool settled;
			private Vector3 settledAt;
			private int floorTries;
			private int bedTries;
			private float retryAt;
			private float nextRenew;
			private float wakeAt = -1f;
			private float stretchUntil = -1f;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			public RestJob(VillageLife owner) : base(owner)
			{
			}

			/// <summary>
			/// Describes where the viking rests and whether it's settled down there
			/// </summary>
			/// <returns>The description</returns>
			public override string Describe()
			{
				string where = ((bed != null) ? "bed" : ((chair != null) ? "chair" : (hasSpot ? "floor" : "nowhere")));
				return("Rest on " + where + (settled ? ", settled" : ", on the way") + ((wakeAt >= 0f) ? ", waking" : ""));
			}

			/// <summary>
			/// Picks where the viking rests - a free bed, its home's first, then a chair in its home, then a spot on its
			/// home's floor, or by the fire when it has no home standing
			/// </summary>
			public override void Begin()
			{
				hasHouse = life.Home(out house);
				bed = FindBed();
				if (bed != null)
				{
					spot = bed.m_spawnPoint.position;
					hasSpot = true;
					return;
				}
				// With no house of the village standing, it rests out by the fire
				if (!hasHouse)
				{
					spot = life.village.RallyPoint();
					hasSpot = true;
					return;
				}
				chair = FindChair(house);
				if (chair != null)
				{
					spot = chair.m_attachPoint.position;
					hasSpot = true;
					return;
				}
				PickFloorSpot();
			}

			/// <summary>
			/// Picks a spot on its home's floor to rest on (see FloorSpot)
			/// </summary>
			private void PickFloorSpot()
			{
				spot = FloorSpot(house);
				hasSpot = true;
			}

			/// <summary>
			/// Checks whether the viking stands inside its home, within its walls and not on its roof
			/// </summary>
			/// <returns>True when it does</returns>
			private bool Inside()
			{
				Vector3 position = life.transform.position;
				return(hasHouse && Village.InHouse(house, position, -0.5f) && !NavmeshRoofs.OnRoof(position));
			}

			/// <summary>
			/// Checks whether the viking stands beside its bed - within BedEdgeReach of its nearest edge seen from above,
			/// level with it, and with nothing solid like a wall between them
			/// </summary>
			/// <returns>True when it does</returns>
			private bool BesideBed()
			{
				WearNTear piece = bed.GetComponent<WearNTear>();
				if (piece == null)
				{
					return(false);
				}
				Vector3 feet = life.transform.position;
				if (Mathf.Abs(feet.y - bed.transform.position.y) > BedEdgeHeight)
				{
					return(false);
				}
				Vector3 knee = feet + Vector3.up * BedEdgeKnee;
				Vector3 edge = NearestPoint(piece, knee);
				if (Utils.DistanceXZ(edge, feet) > BedEdgeReach)
				{
					return(false);
				}
				Vector3 toEdge = edge - knee;
				float distance = toEdge.magnitude;
				if (distance < 0.01f)
				{
					return(true);
				}
				EnsureHopMask();
				foreach (RaycastHit hit in Physics.RaycastAll(knee, toEdge / distance, distance, hopMask, QueryTriggerInteraction.Ignore))
				{
					if (hit.collider.GetComponentInParent<Bed>() != bed)
					{
						return(false);
					}
				}
				return(true);
			}

			/// <summary>
			/// Walks to where it rests and lies or sits down, keeps its bed claimed through the night, and gets up once
			/// the night is over - sitting on its home's floor only once it's inside, and going back to its spot when
			/// it's pushed off it
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's up in the morning</returns>
			public override bool Tick(float dt)
			{
				if (!IsNight())
				{
					return(WakeUp());
				}
				// Once the players have slept through the night, one still on its way to bed stays up (see Bedtime)
				if (!settled && SleepSkip.SleptThroughNight())
				{
					return(true);
				}
				// A viking picked as the watch after it settled down gets up to keep watch, with a word when it was asleep in
				// bed - woken by the one handing it over, most likely
				if (life.IsWatch())
				{
					if (life.inBed)
					{
						VikingSpeech.Chatter(life.humanoid, VikingSpeech.Woken(), VikingSpeech.TalkTime, true);
					}
					return(true);
				}
				if (!hasSpot || Time.time < retryAt)
				{
					life.ai.StopMoving();
					return(false);
				}
				if (!settled)
				{
					if (bed != null && bed.GetOwner() != 0L)
					{
						// A player claimed the bed
						return(true);
					}
					if (bed != null && Time.time >= nextRenew)
					{
						nextRenew = Time.time + ClaimRenew;
						if (!life.ClaimObject(bed.GetComponent<ZNetView>()))
						{
							// Another viking got there first
							return(true);
						}
					}
					// A bed or chair is lain or sat on from within reach of it, since the piece itself stands in the way, and a
					// bed from beside any edge of it
					float reach = ((bed != null || chair != null) ? 1.5f : 0.6f);
					if (bed != null && BesideBed())
					{
						life.ai.StopMoving();
						life.ResetStuck();
						life.moveFailed = false;
					}
					else if (!life.Go(dt, spot, reach, false))
					{
						return(false);
					}
					bool failed = life.moveFailed;
					life.moveFailed = false;
					// Held up on the way, like in a crowded doorway, it keeps its bed and tries again after a moment
					if (failed && bed != null && bedTries < BedRetries)
					{
						bedTries++;
						retryAt = Time.time + BedRetryWait;
						return(false);
					}
					if (failed && (bed != null || chair != null))
					{
						if (bed != null)
						{
							Plugin.Log.LogInfo(life.LogName() + " couldn't reach the bed at " + bed.transform.position.ToString("F0") + ", so it rests on the floor instead" + life.DescribeBedMiss(bed, spot));
							life.ReleaseObject(bed.GetComponent<ZNetView>());
						}
						if (chair != null)
						{
							life.ReleaseObject(chair.GetComponentInParent<ZNetView>());
						}
						bed = null;
						chair = null;
						if (!hasHouse)
						{
							hasSpot = false;
							return(false);
						}
						if (!Inside())
						{
							PickFloorSpot();
							return(false);
						}
					}
					if (bed == null && chair == null && !Inside())
					{
						// Its way in was blocked, or its spot is just past a wall from where it stands
						floorTries++;
						if (floorTries % RestFloorTries == 0)
						{
							retryAt = Time.time + RestRetryWait;
						}
						PickFloorSpot();
						return(false);
					}
					SettleDown();
					return(false);
				}
				// A player who claims the bed has it at once - the viking gets straight up and finds another
				if (bed != null && bed.GetOwner() != 0L)
				{
					return(true);
				}
				if (bed != null && Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.ClaimObject(bed.GetComponent<ZNetView>());
				}
				if (life.attachPoint == null)
				{
					life.ai.StopMoving();
					if (Utils.DistanceXZ(life.transform.position, settledAt) > PushedDistance)
					{
						StandUp();
						spot = settledAt;
					}
				}
				return(false);
			}

			/// <summary>
			/// Lies down on the bed, sits on the chair, or sits on the floor, putting away its torch
			/// </summary>
			private void SettleDown()
			{
				settled = true;
				settledAt = life.transform.position;
				nextRenew = Time.time + ClaimRenew;
				life.ai.StopMoving();
				if (life.viking != null)
				{
					life.viking.UpdateTorch();
				}
				if (bed != null)
				{
					life.Attach(bed.m_spawnPoint, bed.gameObject);
					VikingPoses.SetLying(life.humanoid, true);
				}
				else if (chair != null)
				{
					life.Attach(chair.m_attachPoint, chair.gameObject);
					VikingPoses.SetChair(life.humanoid, true);
				}
				else
				{
					VikingPoses.SetSitting(life.humanoid, true);
				}
			}

			/// <summary>
			/// Gets up a while after the night ends (see WakeDelayMin), stretching half the time
			/// </summary>
			/// <returns>True once it's up and done stretching</returns>
			private bool WakeUp()
			{
				life.ai.StopMoving();
				if (!settled)
				{
					return(true);
				}
				if (wakeAt < 0f)
				{
					wakeAt = Time.time + WakeDelayMin + SpreadDelay();
				}
				if (Time.time < wakeAt)
				{
					return(false);
				}
				if (stretchUntil < 0f)
				{
					if (bed != null)
					{
						life.body.Rest.Woke(bed.m_spawnPoint.position);
					}
					StandUp();
					if (Random.value >= StretchChance)
					{
						return(true);
					}
					VikingPoses.Stretch(life.humanoid);
					stretchUntil = Time.time + StretchTime;
				}
				return(Time.time >= stretchUntil);
			}

			/// <summary>
			/// Checks whether the viking sits for the night, on a chair or the floor
			/// </summary>
			/// <returns>True while it does</returns>
			public override bool Sitting()
			{
				return(settled && bed == null);
			}

			/// <summary>
			/// Gets off the bed, chair or floor
			/// </summary>
			private void StandUp()
			{
				if (!settled)
				{
					return;
				}
				settled = false;
				if (bed != null)
				{
					VikingPoses.SetLying(life.humanoid, false);
				}
				else if (chair != null)
				{
					VikingPoses.SetChair(life.humanoid, false);
				}
				else
				{
					VikingPoses.SetSitting(life.humanoid, false);
				}
				life.Detach();
			}

			/// <summary>
			/// Gets up at once and lets go of its bed or chair
			/// </summary>
			public override void End()
			{
				StandUp();
				if (bed != null)
				{
					life.ReleaseObject(bed.GetComponent<ZNetView>());
				}
				if (chair != null)
				{
					life.ReleaseObject(chair.GetComponentInParent<ZNetView>());
				}
			}

			/// <summary>
			/// Checks whether the viking is lying or sitting down for the night - from settling down until it stands up in
			/// the morning, which can be a while after dawn (see WakeUp)
			/// </summary>
			/// <returns>True while it is</returns>
			public override bool Resting()
			{
				return(settled && (IsNight() || stretchUntil < 0f));
			}

			/// <summary>
			/// Finds a free bed in the village's core, or one a player put up that brought a viking (see VillageBeds) -
			/// one no player has claimed, no other viking holds tonight and no other viking of the village calls its own
			/// - its own bed first, then one in its home, then the nearest, and notes it as the viking's own. The
			/// outposts are left out otherwise, so no one sleeps out there
			/// </summary>
			/// <returns>The bed, or null</returns>
			private Bed FindBed()
			{
				life.Home(out VillagePaths.House home);
				ZDO self = life.nview.GetZDO();
				Bed best = null;
				float bestScore = float.MaxValue;
				foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
				{
					ZNetView view = instance.Value;
					if (view == null || (!life.village.InCoreHouse(view.transform.position, 0.5f) && !(instance.Key.GetBool(VillageBeds.BroughtKey) && life.village.Contains(view.transform.position))))
					{
						continue;
					}
					Bed candidate = view.GetComponent<Bed>();
					if (candidate == null || candidate.m_spawnPoint == null || candidate.GetOwner() != 0L || !life.IsFree(view))
					{
						continue;
					}
					ZDO holder = VillageBeds.Holder(instance.Key, life.village);
					if (holder != null && holder != self)
					{
						continue;
					}
					float score = Vector3.Distance(view.transform.position, life.transform.position) + (Village.InHouse(home, view.transform.position, 0.5f) ? 0f : 1000f) - ((holder == self) ? 10000f : 0f);
					if (score < bestScore)
					{
						best = candidate;
						bestScore = score;
					}
				}
				if (best == null || !life.ClaimObject(best.GetComponent<ZNetView>()))
				{
					return(null);
				}
				VillageBeds.Hold(self, best.GetComponent<ZNetView>());
				return(best);
			}

			/// <summary>
			/// Finds a chair in the viking's home that no other viking holds
			/// </summary>
			/// <param name="house">Its home</param>
			/// <returns>The chair, or null</returns>
			private Chair FindChair(VillagePaths.House house)
			{
				foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
				{
					ZNetView view = instance.Value;
					if (view == null || !Village.InHouse(house, view.transform.position, 0.5f))
					{
						continue;
					}
					Chair candidate = view.GetComponentInChildren<Chair>();
					if (candidate != null && candidate.m_attachPoint != null && !candidate.m_inShip && life.IsFree(view) && life.ClaimObject(view))
					{
						return(candidate);
					}
				}
				return(null);
			}
		}

		/// <summary>
		/// Keeping watch through the night - walking from doorstep to doorstep around the village and past the fire,
		/// stopping at each to look around
		/// </summary>
		private class WatchJob : Job
		{
			private readonly List<Vector3> stops = new List<Vector3>();
			private int next;
			private float pauseUntil = -1f;
			private float nextLook;

			/// <summary>
			/// When the watch has woken its relief at midnight and waits for it to get up, and whether it has handed the
			/// watch over
			/// </summary>
			private float wakeAt = -1f;
			private bool handedOver;

			/// <summary>
			/// The player whose noise the watch is looking into, where it heard them, until when it looks before giving
			/// up, until when it stands looking around once it has found them or nothing, and when it next listens
			/// </summary>
			private Player heard;
			private Vector3 noiseAt;
			private float investigateUntil = -1f;
			private float lookUntil = -1f;
			private float nextListen;

			/// <summary>
			/// When the watch next mutters about the cold in a biome whose weather freezes, or -1 before it's picked
			/// </summary>
			private float nextMutter = -1f;

			/// <summary>
			/// When the watch next checks its torches will last its shift
			/// </summary>
			private float nextTorchCheck;

			/// <summary>
			/// In freezing weather, where the watch keeps watch from instead of patrolling - in a burning fire's warmth, or
			/// else inside its house - the fire, or null inside, whether it has picked the spot and whether it's there
			/// </summary>
			private Vector3 post;
			private Fireplace postFire;
			private bool hasPost;
			private bool atPost;

			/// <summary>
			/// Describes which stop of its patrol the watch is at or heading for, or where it keeps warm in freezing weather
			/// </summary>
			/// <returns>The description</returns>
			public override string Describe()
			{
				if (hasPost)
				{
					return("Watch " + ((postFire != null) ? "by fire" : "inside") + (atPost ? ", looking around" : ", walking"));
				}
				return("Watch, stop " + (next + 1) + " of " + stops.Count + ((pauseUntil >= 0f) ? ", looking around" : ", walking"));
			}

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			public WatchJob(VillageLife owner) : base(owner)
			{
			}

			/// <summary>
			/// Lays out the patrol - the doorsteps of each house of the village's core and the fire, leaving out the
			/// outposts, too far out to watch alone at night - starting from the stop nearest the viking
			/// </summary>
			public override void Begin()
			{
				foreach (VillagePaths.House house in life.village.CoreHouses)
				{
					List<Vector3> steps = Village.Doorsteps(house);
					stops.AddRange((steps.Count > 0) ? steps : new List<Vector3> { house.Center + (house.Center - life.village.CoreMiddle).normalized * (house.HalfSize + DoorstepDistance) });
				}
				Fireplace fire = life.village.Fire();
				if (fire != null)
				{
					stops.Add(fire.transform.position + Vector3.forward * FireRing(fire, 1f));
				}
				float nearest = float.MaxValue;
				for (int i = 0; i < stops.Count; i++)
				{
					float distance = Vector3.Distance(stops[i], life.transform.position);
					if (distance < nearest)
					{
						nearest = distance;
						next = i;
					}
				}
			}

			/// <summary>
			/// Walks to the next stop and looks around there for a few seconds - or in freezing weather, keeps watch from a
			/// warm spot instead (see KeepWarm)
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once the night is over, it's no longer the watch, or its torches won't last its shift, so it
			/// can try to craft another (see TorchShort)</returns>
			public override bool Tick(float dt)
			{
				if (!IsNight() || !life.IsWatch() || stops.Count == 0)
				{
					return(true);
				}
				if (HandOver(dt) || Investigate(dt))
				{
					atPost = false;
					return(false);
				}
				// Its torches running short, it stops to craft another (see FindNightTorchWork) and keeps watch again after
				if (Time.time >= nextTorchCheck)
				{
					nextTorchCheck = Time.time + WatchTorchCheck;
					if (Time.time >= life.nightTorchAgainAt && life.TorchShort())
					{
						return(true);
					}
				}
				MutterCold();
				if (KeepWarm(dt))
				{
					return(false);
				}
				if (pauseUntil >= 0f)
				{
					life.ai.StopMoving();
					if (Time.time >= nextLook)
					{
						nextLook = Time.time + Random.Range(1.5f, 3f);
						Vector2 look = Random.insideUnitCircle.normalized;
						life.ai.LookTowards(new Vector3(look.x, 0f, look.y));
					}
					if (Time.time >= pauseUntil)
					{
						pauseUntil = -1f;
						next = (next + 1) % stops.Count;
					}
					return(false);
				}
				if (life.Go(dt, stops[next], 1f, false))
				{
					life.moveFailed = false;
					pauseUntil = Time.time + Random.Range(PatrolPauseMin, PatrolPauseMax);
				}
				return(false);
			}

			/// <summary>
			/// Has the watch mutter to itself about the cold now and then in a biome whose weather freezes (see
			/// VikingGear.FreezesAt), every ColdWatchIntervalMin to ColdWatchIntervalMax seconds, whether or not its gear
			/// keeps the frost off, and not over something else it's saying
			/// </summary>
			private void MutterCold()
			{
				if (!VikingGear.FreezesAt(life.transform.position))
				{
					nextMutter = -1f;
					return;
				}
				if (nextMutter < 0f)
				{
					nextMutter = Time.time + Random.Range(VikingWeatherSpeech.ColdWatchIntervalMin, VikingWeatherSpeech.ColdWatchIntervalMax);
				}
				if (Time.time < nextMutter)
				{
					return;
				}
				nextMutter = Time.time + Random.Range(VikingWeatherSpeech.ColdWatchIntervalMin, VikingWeatherSpeech.ColdWatchIntervalMax);
				if (!VikingSpeech.Chattering(life.humanoid, out string _))
				{
					VikingSpeech.Chatter(life.humanoid, VikingWeatherSpeech.ColdWatchRemark(life.body.HasBeard()), VikingSpeech.TalkTime);
				}
			}

			/// <summary>
			/// Keeps watch from a warm spot while the weather would freeze the watch (see VikingWeather.FreezingFor) - it
			/// walks to its post (see PickPost), picking another when its fire goes out, and stands there looking around,
			/// still listening for noise and handing over at midnight, and goes back to its patrol once the weather clears
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True while it keeps warm, so it doesn't patrol</returns>
			private bool KeepWarm(float dt)
			{
				if (!VikingWeather.FreezingFor(life.humanoid))
				{
					hasPost = false;
					atPost = false;
					return(false);
				}
				if (!hasPost || (postFire != null && !postFire.IsBurning()))
				{
					PickPost();
				}
				if (!hasPost)
				{
					return(false);
				}
				if (atPost && Utils.DistanceXZ(life.transform.position, post) > PushedDistance)
				{
					atPost = false;
				}
				if (!atPost)
				{
					life.mover = "keeping watch somewhere warm";
					if (!life.Go(dt, post, 0.75f, false))
					{
						return(true);
					}
					life.moveFailed = false;
					atPost = true;
				}
				life.ai.StopMoving();
				if (Time.time >= nextLook)
				{
					nextLook = Time.time + Random.Range(1.5f, 3f);
					Vector2 look = Random.insideUnitCircle.normalized;
					life.ai.LookTowards(new Vector3(look.x, 0f, look.y));
				}
				return(true);
			}

			/// <summary>
			/// Picks the watch's warm post - a spot in the warmth of the burning fire it would gather at (see
			/// ChooseGatherFire), clear of the flames, or else a spot on its home's floor clear of the doors (see FloorSpot)
			/// </summary>
			private void PickPost()
			{
				atPost = false;
				hasPost = false;
				Fireplace fire = life.ChooseGatherFire();
				if (fire != null && fire.IsBurning())
				{
					float min = FireRing(fire, 0f);
					float max = FireRing(fire, 1f);
					GatherJob.WarmRing(fire, ref min, ref max);
					Vector3 point = fire.transform.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(min, max);
					post = ((Pathfinding.instance != null && NavmeshPoints.FindValidPoint(out Vector3 valid, point, GatherSnap, Pathfinding.AgentType.Humanoid)) ? valid : point);
					postFire = fire;
					hasPost = true;
					return;
				}
				postFire = null;
				if (life.Home(out VillagePaths.House house))
				{
					post = FloorSpot(house);
					hasPost = true;
				}
			}

			/// <summary>
			/// Looks into a player's noise the watch hears (see HeardPlayer) - calling out, walking over to where it heard
			/// them for up to InvestigateTime seconds, and once it sees them, or gets there and finds no one, saying so and
			/// looking around a moment before going back to its patrol
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True while it's looking into a noise, so it doesn't patrol</returns>
			private bool Investigate(float dt)
			{
				if (investigateUntil < 0f)
				{
					if (Time.time < nextListen)
					{
						return(false);
					}
					nextListen = Time.time + ListenInterval;
					heard = life.HeardPlayer();
					if (heard == null)
					{
						return(false);
					}
					noiseAt = heard.transform.position;
					investigateUntil = Time.time + InvestigateTime;
					lookUntil = -1f;
					life.ai.LookAt(noiseAt + Vector3.up);
					VikingSpeech.Say(life.humanoid, VikingSpeech.HeardNoise(), VikingSpeech.QuickTime, VikingSpeech.TalkCull);
					return(true);
				}
				if (lookUntil >= 0f)
				{
					life.ai.StopMoving();
					if (heard != null && !heard.IsDead() && Utils.DistanceXZ(heard.transform.position, life.transform.position) <= RecognizeRange)
					{
						life.Face(heard.GetEyePoint());
					}
					else if (Time.time >= nextLook)
					{
						nextLook = Time.time + Random.Range(1f, 2f);
						Vector2 look = Random.insideUnitCircle.normalized;
						life.ai.LookTowards(new Vector3(look.x, 0f, look.y));
					}
					if (Time.time >= lookUntil)
					{
						investigateUntil = -1f;
						lookUntil = -1f;
						heard = null;
					}
					return(true);
				}
				life.mover = "looking into a noise";
				// Once it sees who made the noise close by, it knows them or takes them for a traveler
				if (heard != null && !heard.IsDead() && Utils.DistanceXZ(heard.transform.position, life.transform.position) <= RecognizeRange && life.ai.CanSeeTarget(heard))
				{
					life.Face(heard.GetEyePoint());
					VikingSpeech.Say(life.humanoid, VikingSpeech.FoundNoise(heard, VikingSpeech.Knows(life.humanoid, heard.GetPlayerID())), VikingSpeech.TalkTime, VikingSpeech.TalkCull);
					lookUntil = Time.time + InvestigateLook;
					return(true);
				}
				if (Time.time >= investigateUntil || life.Go(dt, noiseAt, NoiseReach, false))
				{
					life.moveFailed = false;
					VikingSpeech.Say(life.humanoid, VikingSpeech.NothingThere(), VikingSpeech.QuickTime, VikingSpeech.TalkCull);
					lookUntil = Time.time + InvestigateLook;
				}
				return(true);
			}

			/// <summary>
			/// Hands the watch over at midnight to the viking it's given to wake (see VillageWatch.ReliefFor) - walking
			/// over to it, waking it with a word, and standing by facing it until it has taken over. When it can't get to
			/// its relief it hands over where it stands
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True while it's handing over, so it doesn't patrol</returns>
			private bool HandOver(float dt)
			{
				ZDOID reliefId = VillageWatch.ReliefFor(life.village, life.nview.GetZDO().m_uid);
				if (reliefId == ZDOID.None)
				{
					return(false);
				}
				GameObject found = ZNetScene.instance.FindInstance(reliefId);
				Character relief = ((found != null) ? found.GetComponent<Character>() : null);
				if (handedOver)
				{
					life.mover = "handing over the watch";
					if (relief != null)
					{
						life.Face(relief.transform.position);
					}
					else
					{
						life.ai.StopMoving();
					}
					return(true);
				}
				// A relief this client doesn't have loaded is left to the watch's change, which comes in time anyway
				if (relief == null)
				{
					return(false);
				}
				if (wakeAt < 0f)
				{
					if (Utils.DistanceXZ(relief.transform.position, life.transform.position) > WakeReach)
					{
						life.mover = "going to wake " + relief.GetHoverName();
						life.Go(dt, relief.transform.position, WakeReach * 0.8f, false);
						if (life.moveFailed)
						{
							life.moveFailed = false;
							handedOver = true;
							VillageWatch.HandedOver(life.humanoid);
						}
						return(true);
					}
					life.Face(relief.transform.position);
					VikingSpeech.Chatter(life.humanoid, VikingSpeech.Address(VikingSpeech.WakeRelief(), VikingNames.Get(relief), "", "", "", ""), VikingSpeech.TalkTime);
					wakeAt = Time.time + WakeTime;
					return(true);
				}
				life.Face(relief.transform.position);
				if (Time.time >= wakeAt)
				{
					handedOver = true;
					VillageWatch.HandedOver(life.humanoid);
				}
				return(true);
			}
		}

		/// <summary>
		/// Finds arrows for the viking to craft - when it has a bow and wants a batch of the best arrows the village can
		/// make (see MissingArrows and PlanArrows), no other viking is working at the station that makes them, and there's
		/// wood it can get. While it still has arrows to shoot, it first gets what the best arrows its station allows are
		/// short of, like flint or feathers, when there's a way to (see GatherFor)
		/// </summary>
		/// <returns>The job, or null when there's nothing to craft</returns>
		private Job FindArrowWork()
		{
			ArrowPlan plan = PlanArrows();
			if (plan == null || !MissingArrows(plan.Batch, plan.Wood))
			{
				return(null);
			}
			// A viking with arrows still to shoot gets what the best arrows it could make take first, rather than
			// making worse ones
			string ammoType = BowAmmo();
			if (ammoType != null && OwnArrows(ammoType, out float _) > 0)
			{
				Job gather = GatherFor(BestArrowsMissing(ammoType));
				if (gather != null)
				{
					return(gather);
				}
			}
			bool woodLeft = (Time.time >= noWoodUntil || CarriedWood() >= plan.Wood || FindWoodChest() != null);
			if (!woodLeft || !ClaimObject(plan.Station.GetComponent<ZNetView>()))
			{
				return(null);
			}
			return(new ArrowJob(this, plan));
		}

		/// <summary>
		/// Checks whether the viking has a bow and wants a batch of arrows crafted - it's missing at least a batch from
		/// its quiver, and it has fewer than QuiverImportant or the village has SpareWood more than the batch takes
		/// </summary>
		/// <param name="batch">How many arrows a craft makes</param>
		/// <param name="wood">The wood a craft takes</param>
		/// <returns>True when it wants a batch</returns>
		private bool MissingArrows(int batch, int wood)
		{
			string ammoType = BowAmmo();
			if (ammoType == null)
			{
				return(false);
			}
			int own = OwnArrows(ammoType, out float _);
			if (own > QuiverFull - batch)
			{
				return(false);
			}
			return(own < QuiverImportant || VillageWood() >= wood + SpareWood);
		}

		/// <summary>
		/// Checks whether the viking has a bow and fewer arrows than it can do without (see QuiverCritical)
		/// </summary>
		/// <returns>True when it has</returns>
		private bool CriticalArrows()
		{
			string ammoType = BowAmmo();
			return(ammoType != null && OwnArrows(ammoType, out float _) < QuiverCritical);
		}

		/// <summary>
		/// Finds a torch for the viking to craft - when its own is half burnt or it has none, and it has no spare (see
		/// NeedsTorch) - with what the torch takes in the village's chests or carried, and wood it can get, or else the
		/// job that gets what the village is short of, like a hunt for greylings for their resin (see GatherFor)
		/// </summary>
		/// <returns>The job, or null when it needs no torch or can't get what one takes</returns>
		private Job FindTorchWork()
		{
			if (!NeedsTorch())
			{
				return(null);
			}
			ArrowPlan plan = PlanCraft(TorchPrefab, out string missing);
			if (plan == null)
			{
				return(GatherFor(missing));
			}
			bool woodLeft = (Time.time >= noWoodUntil || CarriedWood() >= plan.Wood || FindWoodChest() != null);
			if (!woodLeft || (plan.Station != null && !ClaimObject(plan.Station.GetComponent<ZNetView>())))
			{
				return(null);
			}
			return(new TorchJob(this, plan));
		}

		/// <summary>
		/// Finds the job that gets something the viking is short of to craft with - foraging for it when it's picked,
		/// like flint or stone, or else hunting only the prey or birds that drop it, like greylings for resin, boars for
		/// leather or birds for feathers - from the Plains on, as a party for prey that isn't only for archers (see
		/// HuntParty) - or with nothing wild about that drops it, slaughtering one of the village's livestock that does -
		/// one viking at a time for each, and none while it's badly hurt
		/// </summary>
		/// <remarks>
		/// What drops what comes from the game's own drop tables (see Drops), so a recipe's ingredients are gathered
		/// wherever the game has them come from
		/// </remarks>
		/// <param name="item">The ingredient's prefab name, or null</param>
		/// <returns>The job, or null when there's nothing to gather it from</returns>
		private Job GatherFor(string item)
		{
			if (string.IsNullOrEmpty(item) || Wounded())
			{
				return(null);
			}
			if (!OtherDoing(ForagingKey) && FindForage(item) != null)
			{
				return(new ForageJob(this, item));
			}
			bool archer = HasBow(humanoid);
			if (OtherDoing(HuntingKey))
			{
				return(null);
			}
			if (FindPrey(archer, item, false) != null || (archer && FindBird(item) != null))
			{
				return(new HuntJob(this, archer, item, false));
			}
			// From the Plains on, prey that isn't only for archers is hunted by a party (see HuntParty)
			if (CanLeadParty(item))
			{
				Job led = LeadParty(item);
				if (led != null)
				{
					return(led);
				}
			}
			// With nothing wild about that drops it, it slaughters one of the village's livestock, which its pen brings
			// back (see VikingFarmRespawnPatch)
			if (FindPrey(archer, item, true) != null)
			{
				return(new HuntJob(this, archer, item, true));
			}
			return(null);
		}

		/// <summary>
		/// Checks whether a creature or bird drops something when it's killed, from the game's drop tables for its prefab,
		/// remembering what each prefab drops
		/// </summary>
		/// <param name="creature">The creature or bird</param>
		/// <param name="item">The item's prefab name</param>
		/// <returns>True when it drops it</returns>
		private static bool Drops(GameObject creature, string item)
		{
			string name = Utils.GetPrefabName(creature);
			if (!dropsByPrefab.TryGetValue(name, out HashSet<string> drops))
			{
				drops = new HashSet<string>();
				GameObject prefab = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(name) : null);
				CharacterDrop characterDrop = ((prefab != null) ? prefab.GetComponent<CharacterDrop>() : null);
				if (characterDrop != null)
				{
					foreach (CharacterDrop.Drop drop in characterDrop.m_drops)
					{
						if (drop != null && drop.m_prefab != null)
						{
							drops.Add(drop.m_prefab.name);
						}
					}
				}
				DropOnDestroyed destroyed = ((prefab != null) ? prefab.GetComponent<DropOnDestroyed>() : null);
				if (destroyed != null && destroyed.m_dropWhenDestroyed != null)
				{
					foreach (DropTable.DropData drop in destroyed.m_dropWhenDestroyed.m_drops)
					{
						if (drop.m_item != null)
						{
							drops.Add(drop.m_item.name);
						}
					}
				}
				dropsByPrefab[name] = drops;
			}
			return(drops.Contains(item));
		}

		/// <summary>
		/// Checks whether the viking needs a new torch - it has none of its own, or its only one has burnt down to
		/// TorchReplaceShare of its strength - which it never does with a dverger circlet of its own to light its way
		/// </summary>
		/// <returns>True when it does</returns>
		private bool NeedsTorch()
		{
			if (viking != null && viking.HasCirclet())
			{
				return(false);
			}
			List<ItemDrop.ItemData> torches = humanoid.GetInventory().GetAllItems().FindAll((ItemDrop.ItemData item) => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch && !VikingItems.IsCarried(item));
			if (torches.Count != 1)
			{
				return(torches.Count == 0);
			}
			ItemDrop.ItemData torch = torches[0];
			return(torch.m_durability <= torch.GetMaxDurability() * TorchReplaceShare);
		}

		/// <summary>
		/// Checks whether the viking's torches won't see it through the dark - for the night watch, when they don't hold
		/// enough burn between them to last the rest of its shift (see WatchLeft), and for any other viking, when it has
		/// none of its own at all. A viking with a dverger circlet of its own needs no torches
		/// </summary>
		/// <returns>True when they won't</returns>
		private bool TorchShort()
		{
			if (viking != null && viking.HasCirclet())
			{
				return(false);
			}
			List<ItemDrop.ItemData> torches = humanoid.GetInventory().GetAllItems().FindAll((ItemDrop.ItemData item) => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch && !VikingItems.IsCarried(item));
			if (torches.Count == 0)
			{
				return(true);
			}
			if (!IsWatch())
			{
				return(false);
			}
			float burn = 0f;
			foreach (ItemDrop.ItemData torch in torches)
			{
				float drain = torch.m_shared.m_durabilityDrain * Game.m_durabilityRate;
				if (!torch.m_shared.m_useDurability || drain <= 0f)
				{
					return(false);
				}
				burn += torch.m_durability / drain;
			}
			return(burn < WatchLeft());
		}

		/// <summary>
		/// Works out how long the night watch's shift has left - the first until midnight, and the second until dawn
		/// (see VillageWatch)
		/// </summary>
		/// <returns>The seconds left</returns>
		private static float WatchLeft()
		{
			if (EnvMan.instance == null)
			{
				return(0f);
			}
			return(ShiftLeft(EnvMan.instance.GetDayFraction(), EnvMan.instance.m_dayLengthSec));
		}

		/// <summary>
		/// Works out how long the night watch's shift has left at a time of day - from noon, the first shift's time
		/// until midnight, and from midnight, the second's until dawn, with none left between dawn and noon
		/// </summary>
		/// <param name="fraction">How far through the day it is, from 0 at midnight to 1 at the next</param>
		/// <param name="dayLength">How long a day is, in seconds</param>
		/// <returns>The seconds left</returns>
		internal static float ShiftLeft(float fraction, float dayLength)
		{
			float end = ((fraction < 0.5f) ? 0.25f : 1f);
			return(Mathf.Max(0f, end - fraction) * dayLength);
		}

		/// <summary>
		/// Finds a torch for the viking to craft at dusk or night, when its torches won't see it through the dark (see
		/// TorchShort) - only from wood and what else a torch takes that it carries or the village's chests hold, since
		/// it doesn't go out gathering in the dark - leaving it for NightTorchRetry seconds when they can't give it that
		/// </summary>
		/// <returns>The job, or null when its torches will do or it can't craft one</returns>
		private Job FindNightTorchWork()
		{
			if (Time.time < nightTorchAgainAt || !TorchShort())
			{
				return(null);
			}
			ArrowPlan plan = PlanCraft(TorchPrefab, out string _);
			if (plan == null || VillageWood() < plan.Wood || (plan.Station != null && !ClaimObject(plan.Station.GetComponent<ZNetView>())))
			{
				nightTorchAgainAt = Time.time + NightTorchRetry;
				return(null);
			}
			return(new TorchJob(this, plan, true));
		}

		/// <summary>
		/// Works out what an item the viking crafts for itself takes, like its torch or a hoe, from the game's recipe for
		/// it - the wood, everything else, which has to be carried for crafting or in the village's chests, and the
		/// station it's crafted at, if any, which the village has to have at the recipe's level
		/// </summary>
		/// <param name="prefab">The item's prefab name</param>
		/// <param name="missing">Set to the first ingredient the village is short of, or null</param>
		/// <returns>The item, or null when the game has no recipe for it, the village has no station for it, or it's
		/// short of an ingredient</returns>
		private ArrowPlan PlanCraft(string prefab, out string missing)
		{
			missing = null;
			Recipe recipe = ((ObjectDB.instance != null) ? ObjectDB.instance.m_recipes.Find((Recipe candidate) => Crafts(candidate) && candidate.m_item.gameObject.name == prefab) : null);
			if (recipe == null)
			{
				return(null);
			}
			CraftingStation station = null;
			if (recipe.m_craftingStation != null)
			{
				station = FindStation(recipe.m_craftingStation.gameObject.name.GetStableHashCode());
				if (station == null || station.GetLevel() < recipe.m_minStationLevel)
				{
					return(null);
				}
			}
			ArrowPlan plan = new ArrowPlan { Prefab = prefab, Batch = Mathf.Max(1, recipe.m_amount), Station = station };
			List<Container> chests = UsableChests();
			foreach (Piece.Requirement requirement in recipe.m_resources)
			{
				int amount = CraftAmount(requirement, 1);
				if (amount <= 0)
				{
					continue;
				}
				string name = requirement.m_resItem.gameObject.name;
				if (WoodItems.Contains(name))
				{
					plan.Wood += amount;
				}
				else if (CraftStock(name, chests) >= amount)
				{
					plan.Others.Add(new KeyValuePair<string, int>(name, amount));
				}
				else
				{
					missing = name;
					return(null);
				}
			}
			return(plan);
		}

		/// <summary>
		/// Checks whether a recipe crafts its item, rather than only upgrading one a player already has
		/// </summary>
		/// <param name="recipe">The recipe, or null</param>
		/// <returns>True when it does</returns>
		private static bool Crafts(Recipe recipe)
		{
			return(recipe != null && recipe.m_enabled && recipe.m_item != null && !recipe.m_noCraftOnlyUpgrade);
		}

		/// <summary>
		/// Gets how much of an ingredient crafting or upgrading an item at a workbench or forge takes - none of one only
		/// an upgrader station takes, which the game leaves out at any other station, the way the vikings craft
		/// </summary>
		/// <param name="requirement">The ingredient, or null</param>
		/// <param name="quality">The quality crafted, 1 for a new item</param>
		/// <returns>How much it takes, or 0</returns>
		private static int CraftAmount(Piece.Requirement requirement, int quality)
		{
			if (requirement == null || requirement.m_resItem == null || requirement.m_upgraderResource)
			{
				return(0);
			}
			return(requirement.GetAmount(quality));
		}

		/// <summary>
		/// Arrows, a torch or a hoe a viking can craft, or a piece of its gear it can upgrade - what it crafts, how many a
		/// craft makes, the wood it takes, what else it takes and how much, the station it's crafted at, or null for one
		/// crafted anywhere, how much an arrow deals, whether they're silver arrows (see FindSilverArrowWork), and for an
		/// upgrade, the piece and the quality it's raised to, or for a weapon lined with silver (see FindLiningWork), the
		/// weapon at its own quality
		/// </summary>
		private class ArrowPlan
		{
			public string Prefab;
			public int Batch;
			public int Wood;
			public readonly List<KeyValuePair<string, int>> Others = new List<KeyValuePair<string, int>>();
			public CraftingStation Station;
			public float Damage;
			public bool Silver;
			public ItemDrop.ItemData Upgrade;
			public int Quality;
			public bool Lines;
		}

		/// <summary>
		/// Works out the best arrows the viking can craft for its bow - the ones that deal the most damage of those the
		/// game has a recipe for that the village has a station of the recipe's level for, and whose ingredients other
		/// than wood the viking carries for crafting or the village's chests hold, since wood can always be gathered.
		/// Wood arrows need only wood, so there are always some. Silver arrows are left out, being crafted apart (see
		/// PlanSilverArrows)
		/// </summary>
		/// <returns>The arrows, or null when it has no bow, or the village has no station for any</returns>
		private ArrowPlan PlanArrows()
		{
			string ammoType = BowAmmo();
			if (ammoType == null || ObjectDB.instance == null)
			{
				return(null);
			}
			List<Container> chests = UsableChests();
			Dictionary<int, CraftingStation> stations = new Dictionary<int, CraftingStation>();
			ArrowPlan best = null;
			foreach (Recipe recipe in ObjectDB.instance.m_recipes)
			{
				if (!Crafts(recipe) || recipe.m_craftingStation == null)
				{
					continue;
				}
				ItemDrop.ItemData arrow = recipe.m_item.m_itemData;
				if (arrow.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo || arrow.m_shared.m_ammoType != ammoType || VikingSilver.IsSilverArrows(arrow))
				{
					continue;
				}
				int stationHash = recipe.m_craftingStation.gameObject.name.GetStableHashCode();
				if (!stations.TryGetValue(stationHash, out CraftingStation station))
				{
					station = FindStation(stationHash);
					stations[stationHash] = station;
				}
				if (station == null || station.GetLevel() < recipe.m_minStationLevel)
				{
					continue;
				}
				ArrowPlan plan = new ArrowPlan { Prefab = recipe.m_item.gameObject.name, Batch = Mathf.Max(1, recipe.m_amount), Station = station, Damage = AmmoDamage(arrow) };
				bool makeable = true;
				foreach (Piece.Requirement requirement in recipe.m_resources)
				{
					int amount = CraftAmount(requirement, 1);
					if (amount <= 0)
					{
						continue;
					}
					string name = requirement.m_resItem.gameObject.name;
					if (WoodItems.Contains(name))
					{
						plan.Wood += amount;
					}
					else if (CraftStock(name, chests) >= amount)
					{
						plan.Others.Add(new KeyValuePair<string, int>(name, amount));
					}
					else
					{
						makeable = false;
						break;
					}
				}
				if (makeable && (best == null || plan.Damage > best.Damage))
				{
					best = plan;
				}
			}
			return(best);
		}

		/// <summary>
		/// Finds what the best arrows the viking could make for its bow are short of - the ones that deal the most damage
		/// of those the village has a station of the recipe's level for, whatever they take - from what it carries for
		/// crafting and the village's chests
		/// </summary>
		/// <param name="ammoType">The ammo its bow takes</param>
		/// <returns>The first ingredient other than wood they're short of, or null when they're short of nothing</returns>
		private string BestArrowsMissing(string ammoType)
		{
			if (ObjectDB.instance == null)
			{
				return(null);
			}
			Recipe best = null;
			float bestDamage = float.MinValue;
			foreach (Recipe recipe in ObjectDB.instance.m_recipes)
			{
				if (!Crafts(recipe) || recipe.m_craftingStation == null)
				{
					continue;
				}
				ItemDrop.ItemData arrow = recipe.m_item.m_itemData;
				if (arrow.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo || arrow.m_shared.m_ammoType != ammoType || VikingSilver.IsSilverArrows(arrow))
				{
					continue;
				}
				CraftingStation station = FindStation(recipe.m_craftingStation.gameObject.name.GetStableHashCode());
				float damage = AmmoDamage(arrow);
				if (station != null && station.GetLevel() >= recipe.m_minStationLevel && damage > bestDamage)
				{
					best = recipe;
					bestDamage = damage;
				}
			}
			if (best == null)
			{
				return(null);
			}
			List<Container> chests = UsableChests();
			foreach (Piece.Requirement requirement in best.m_resources)
			{
				int amount = CraftAmount(requirement, 1);
				if (amount <= 0)
				{
					continue;
				}
				string name = requirement.m_resItem.gameObject.name;
				if (!WoodItems.Contains(name) && CraftStock(name, chests) < amount)
				{
					return(name);
				}
			}
			return(null);
		}

		/// <summary>
		/// Picks a material the vikings build or craft with that's in the village's chests, at random, for a talk (see
		/// VikingSpeech.IsMaterial)
		/// </summary>
		/// <returns>Its name, in lowercase, or "" when the chests hold none</returns>
		private string VillageMaterial()
		{
			List<string> names = new List<string>();
			if (village != null)
			{
				foreach (Container chest in UsableChests())
				{
					foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
					{
						if (VikingSpeech.IsMaterial(item))
						{
							string name = VikingSpeech.MaterialName(item);
							if (!names.Contains(name))
							{
								names.Add(name);
							}
						}
					}
				}
			}
			return((names.Count > 0) ? names[Random.Range(0, names.Count)] : "");
		}

		/// <summary>
		/// Counts how many of an ingredient the viking carries for crafting and the village's chests hold
		/// </summary>
		/// <param name="name">The ingredient's prefab name</param>
		/// <param name="chests">The village's chests the viking may use</param>
		/// <returns>How many</returns>
		private int CraftStock(string name, List<Container> chests)
		{
			int count = CarriedCraft(name);
			foreach (Container chest in chests)
			{
				foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
				{
					if (item.m_dropPrefab != null && item.m_dropPrefab.name == name && !VikingItems.IsCarried(item))
					{
						count += item.m_stack;
					}
				}
			}
			return(count);
		}

		/// <summary>
		/// Counts how many of an ingredient the viking carries for crafting
		/// </summary>
		/// <param name="name">The ingredient's prefab name</param>
		/// <returns>How many</returns>
		private int CarriedCraft(string name)
		{
			int count = 0;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (IsCarriedFor(item, CarryCraft) && item.m_dropPrefab != null && item.m_dropPrefab.name == name)
				{
					count += item.m_stack;
				}
			}
			return(count);
		}

		/// <summary>
		/// Finds an ingredient other than wood the viking still needs for a batch of arrows
		/// </summary>
		/// <param name="plan">The arrows</param>
		/// <param name="name">The ingredient's prefab name</param>
		/// <param name="needed">How many more it needs</param>
		/// <returns>False when it carries everything</returns>
		private bool MissingCraft(ArrowPlan plan, out string name, out int needed)
		{
			foreach (KeyValuePair<string, int> other in plan.Others)
			{
				int shortBy = other.Value - CarriedCraft(other.Key);
				if (shortBy > 0)
				{
					name = other.Key;
					needed = shortBy;
					return(true);
				}
			}
			name = null;
			needed = 0;
			return(false);
		}

		/// <summary>
		/// Finds the nearest of the village's chests holding an ingredient
		/// </summary>
		/// <param name="name">The ingredient's prefab name</param>
		/// <returns>The chest, or null when none holds any</returns>
		private Container ChestWithCraft(string name)
		{
			return(Nearest(UsableChests().FindAll((Container chest) => !Avoided(chest.m_nview.GetZDO().m_uid) && chest.GetInventory().GetAllItems().Exists((ItemDrop.ItemData item) => item.m_dropPrefab != null && item.m_dropPrefab.name == name && !VikingItems.IsCarried(item)))));
		}

		/// <summary>
		/// Takes an ingredient out of a chest the viking has reached, as many as it needs and can carry, to carry for
		/// crafting
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <param name="name">The ingredient's prefab name</param>
		/// <param name="wanted">How many it needs</param>
		/// <returns>How many it took</returns>
		private int TakeCraft(Container chest, string name, int wanted)
		{
			if (wanted <= 0 || !OpenChest(chest))
			{
				return(0);
			}
			Inventory store = chest.GetInventory();
			int taken = 0;
			foreach (ItemDrop.ItemData item in store.m_inventory.FindAll((ItemDrop.ItemData stored) => stored.m_dropPrefab != null && stored.m_dropPrefab.name == name && !VikingItems.IsCarried(stored)))
			{
				if (taken >= wanted)
				{
					break;
				}
				int took = Carry(item, Mathf.Min(wanted - taken, item.m_stack), CarryCraft);
				if (took <= 0)
				{
					break;
				}
				store.RemoveItem(item, took);
				taken += took;
			}
			return(taken);
		}

		/// <summary>
		/// Uses up the ingredients other than wood a batch of arrows takes from what the viking carries for crafting
		/// </summary>
		/// <param name="plan">The arrows</param>
		/// <returns>False when it didn't carry them all, and nothing was used</returns>
		private bool UseCraft(ArrowPlan plan)
		{
			if (MissingCraft(plan, out string _, out int _))
			{
				return(false);
			}
			Inventory inventory = humanoid.GetInventory();
			foreach (KeyValuePair<string, int> other in plan.Others)
			{
				int left = other.Value;
				foreach (ItemDrop.ItemData item in inventory.m_inventory.FindAll((ItemDrop.ItemData carried) => IsCarriedFor(carried, CarryCraft) && carried.m_dropPrefab != null && carried.m_dropPrefab.name == other.Key))
				{
					if (left <= 0)
					{
						break;
					}
					int used = Mathf.Min(left, item.m_stack);
					inventory.RemoveItem(item, used);
					left -= used;
				}
			}
			return(true);
		}

		/// <summary>
		/// Has the viking carry what it carries for crafting back to a chest as loot, once it's done crafting
		/// </summary>
		private void ReturnCraft()
		{
			Inventory inventory = humanoid.GetInventory();
			bool changed = false;
			foreach (ItemDrop.ItemData item in inventory.m_inventory.FindAll((ItemDrop.ItemData carried) => IsCarriedFor(carried, CarryCraft)))
			{
				item.m_customData[VikingItems.CarryKey] = CarryLoot;
				LogCraftLeftover(item);
				changed = true;
			}
			if (changed)
			{
				inventory.Changed();
			}
		}

		/// <summary>
		/// Finds the ammo type of the bow or crossbow the viking has of its own
		/// </summary>
		/// <returns>The ammo type, or null when it has none</returns>
		private string BowAmmo()
		{
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (!VikingItems.IsCarried(item) && !VikingItems.IsProp(item) && item.IsWeapon() && !string.IsNullOrEmpty(item.m_shared.m_ammoType) && item.m_shared.m_attack != null && item.m_shared.m_attack.m_attackType == Attack.AttackType.Projectile)
				{
					return(item.m_shared.m_ammoType);
				}
			}
			return(null);
		}

		/// <summary>
		/// Checks whether an item is ammo of a type for the viking's quiver, of its own or in a chest - silver arrows
		/// being kept apart from it, for the dead (see FindSilverArrowWork)
		/// </summary>
		/// <param name="item">The item</param>
		/// <param name="ammoType">The ammo type</param>
		/// <returns>True when it is</returns>
		private static bool IsAmmoOf(ItemDrop.ItemData item, string ammoType)
		{
			return(item != null && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo && item.m_shared.m_ammoType == ammoType && !VikingItems.IsCarried(item) && !VikingItems.IsProp(item) && !VikingSilver.IsSilverArrows(item));
		}

		/// <summary>
		/// Adds up the damage one of a kind of ammo deals
		/// </summary>
		/// <param name="ammo">The ammo</param>
		/// <returns>Its damage</returns>
		private static float AmmoDamage(ItemDrop.ItemData ammo)
		{
			return(ammo.GetDamage().GetTotalDamage());
		}

		/// <summary>
		/// Counts the viking's own arrows of its bow's ammo type and finds how much its best deal
		/// </summary>
		/// <param name="ammoType">The ammo type</param>
		/// <param name="best">How much one of its best arrows deals, or -1 when it has none</param>
		/// <returns>How many it has</returns>
		private int OwnArrows(string ammoType, out float best)
		{
			int count = 0;
			best = -1f;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (IsAmmoOf(item, ammoType))
				{
					count += item.m_stack;
					best = Mathf.Max(best, AmmoDamage(item));
				}
			}
			return(count);
		}

		/// <summary>
		/// Finds the nearest of the village's chests with arrows for an archer - any of its bow's ammo type while it's
		/// missing at least a batch from its quiver, or ones better than its best otherwise
		/// </summary>
		/// <returns>The chest, or null when there's none, or the viking isn't an archer</returns>
		private Container FindArrowChest()
		{
			string ammoType = BowAmmo();
			if (ammoType == null)
			{
				return(null);
			}
			ArrowRecipe(out int batch, out int _);
			int count = OwnArrows(ammoType, out float best);
			bool missing = (count <= QuiverFull - batch);
			return(Nearest(UsableChests().FindAll((Container chest) => !Avoided(chest.m_nview.GetZDO().m_uid) && chest.GetInventory().GetAllItems().Exists((ItemDrop.ItemData item) => IsAmmoOf(item, ammoType) && (missing || AmmoDamage(item) > best)))));
		}

		/// <summary>
		/// Fills an archer's quiver from a chest it has reached with the best arrows among its own and the chest's, up
		/// to QuiverFull, putting back in the chest its own that don't make the cut while the chest has room, then
		/// readies its best
		/// </summary>
		/// <param name="chest">The chest</param>
		/// <returns>False when it took and put back nothing, or the chest couldn't be used</returns>
		private bool TakeArrows(Container chest)
		{
			string ammoType = BowAmmo();
			if (ammoType == null || !OpenChest(chest))
			{
				return(false);
			}
			Inventory own = humanoid.GetInventory();
			Inventory store = chest.GetInventory();
			List<ArrowStack> pool = new List<ArrowStack>();
			foreach (ItemDrop.ItemData item in own.m_inventory.FindAll((ItemDrop.ItemData stored) => IsAmmoOf(stored, ammoType)))
			{
				pool.Add(new ArrowStack(item, AmmoDamage(item), true, item.m_stack));
			}
			foreach (ItemDrop.ItemData item in store.m_inventory.FindAll((ItemDrop.ItemData stored) => IsAmmoOf(stored, ammoType)))
			{
				pool.Add(new ArrowStack(item, AmmoDamage(item), false, item.m_stack));
			}
			FillQuiver(pool, QuiverFull);
			bool changed = false;
			foreach (ArrowStack arrows in pool)
			{
				ItemDrop.ItemData item = arrows.Item;
				int keep = arrows.Keep;
				if (arrows.Own)
				{
					int extra = item.m_stack - keep;
					if (extra > 0)
					{
						ItemDrop.ItemData back = item.Clone();
						back.m_stack = extra;
						back.m_equipped = false;
						if (store.CanAddItem(back) && store.AddItem(back))
						{
							own.RemoveItem(item, extra);
							changed = true;
						}
					}
					continue;
				}
				if (keep <= 0)
				{
					continue;
				}
				ItemDrop.ItemData taken = item.Clone();
				taken.m_stack = keep;
				taken.m_equipped = false;
				if (own.CanAddItem(taken) && own.AddItem(taken))
				{
					store.RemoveItem(item, keep);
					changed = true;
				}
			}
			if (changed)
			{
				own.Changed();
				ReadyBestArrows();
				Plugin.Log.LogInfo(LogName() + " filled its quiver from a chest, " + OwnArrows(ammoType, out float _) + " arrows now");
			}
			return(changed);
		}

		/// <summary>
		/// A stack of arrows an archer weighs filling its quiver from, its own or a chest's
		/// </summary>
		internal class ArrowStack
		{
			/// <summary>
			/// The arrows, or null when only the numbers are weighed
			/// </summary>
			public readonly ItemDrop.ItemData Item;

			/// <summary>
			/// How much damage each arrow does
			/// </summary>
			public readonly float Damage;

			/// <summary>
			/// Whether the archer carries them, rather than the chest holding them
			/// </summary>
			public readonly bool Own;

			/// <summary>
			/// How many arrows the stack holds
			/// </summary>
			public readonly int Stack;

			/// <summary>
			/// How many of them go in the quiver (see FillQuiver)
			/// </summary>
			public int Keep;

			/// <summary>
			/// Makes a stack of arrows to weigh
			/// </summary>
			/// <param name="item">The arrows, or null</param>
			/// <param name="damage">How much damage each does</param>
			/// <param name="own">Whether the archer carries them</param>
			/// <param name="stack">How many the stack holds</param>
			public ArrowStack(ItemDrop.ItemData item, float damage, bool own, int stack)
			{
				Item = item;
				Damage = damage;
				Own = own;
				Stack = stack;
			}
		}

		/// <summary>
		/// Sorts an archer's arrows and a chest's best first, and of two as good its own before the chest's so it
		/// doesn't swap like for like, and works out how many of each go in its quiver, filling it from the best down -
		/// any of its own left over go back in the chest
		/// </summary>
		/// <param name="stacks">The stacks, sorted in place, each given how many it keeps</param>
		/// <param name="quiver">How many arrows the quiver holds</param>
		internal static void FillQuiver(List<ArrowStack> stacks, int quiver)
		{
			stacks.Sort((ArrowStack a, ArrowStack b) =>
			{
				int better = b.Damage.CompareTo(a.Damage);
				return((better != 0) ? better : b.Own.CompareTo(a.Own));
			});
			int room = quiver;
			foreach (ArrowStack arrows in stacks)
			{
				arrows.Keep = Mathf.Min(room, arrows.Stack);
				room -= arrows.Keep;
			}
		}

		/// <summary>
		/// Readies an archer's best arrows, so its bow shoots them before the rest - in place of silver arrows it readied
		/// against the dead, which it readies again when it next fights them (see VikingCore.BestAmmo)
		/// </summary>
		private void ReadyBestArrows()
		{
			string ammoType = (GearHeld() ? null : BowAmmo());
			if (ammoType == null)
			{
				return;
			}
			ItemDrop.ItemData best = null;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (IsAmmoOf(item, ammoType) && (best == null || AmmoDamage(item) > AmmoDamage(best)))
				{
					best = item;
				}
			}
			ItemDrop.ItemData readied = humanoid.GetAmmoItem();
			if (best != null && readied != best && (readied == null || !humanoid.GetInventory().ContainsItem(readied) || VikingSilver.IsSilverArrows(readied) || AmmoDamage(best) > AmmoDamage(readied)))
			{
				humanoid.EquipItem(best, false);
			}
		}

		/// <summary>
		/// Reads how many arrows the game's recipe makes and how much wood it takes
		/// </summary>
		/// <param name="batch">How many arrows a craft makes</param>
		/// <param name="wood">The wood it takes</param>
		private static void ArrowRecipe(out int batch, out int wood)
		{
			batch = ArrowBatchDefault;
			wood = ArrowWoodDefault;
			ItemDrop.ItemData arrow = ItemOf(ArrowPrefab);
			Recipe recipe = ((arrow != null && ObjectDB.instance != null) ? ObjectDB.instance.GetRecipe(arrow) : null);
			if (recipe == null)
			{
				return;
			}
			batch = Mathf.Max(1, recipe.m_amount);
			foreach (Piece.Requirement requirement in recipe.m_resources)
			{
				if (requirement.m_resItem != null && requirement.m_resItem.gameObject.name == RecipeWood)
				{
					wood = Mathf.Max(1, requirement.m_amount);
				}
			}
		}

		/// <summary>
		/// Finds the nearest crafting station of a kind in the village that a viking can use (see StationUsable)
		/// </summary>
		/// <param name="prefab">The station's prefab hash</param>
		/// <returns>The station, or null when the village has none loaded it can use</returns>
		private CraftingStation FindStation(int prefab)
		{
			CraftingStation nearest = null;
			float nearestDistance = float.MaxValue;
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				if (instance.Key.GetPrefab() != prefab || instance.Value == null || !village.OwnsPiece(instance.Value.transform.position))
				{
					continue;
				}
				CraftingStation bench = instance.Value.GetComponent<CraftingStation>();
				float distance = Vector3.Distance(instance.Value.transform.position, transform.position);
				if (bench != null && distance < nearestDistance && StationUsable(bench, instance.Key.m_uid))
				{
					nearest = bench;
					nearestDistance = distance;
				}
			}
			return(nearest);
		}

		/// <summary>
		/// Checks whether a viking can use a crafting station, the same as a player - under a roof and covered enough
		/// when the station needs one, like a workbench or forge, and with a fire going when it needs one - remembering
		/// the answer for StationCheckTime, since the roof check casts many rays
		/// </summary>
		/// <param name="station">The station</param>
		/// <param name="id">Its ZDO</param>
		/// <returns>True when it can</returns>
		private static bool StationUsable(CraftingStation station, ZDOID id)
		{
			if (stationChecks.TryGetValue(id, out KeyValuePair<float, bool> known) && Time.time < known.Key)
			{
				return(known.Value);
			}
			if (stationChecks.Count > StationChecksMax)
			{
				stationChecks.Clear();
			}
			bool usable = true;
			if (station.m_craftRequireRoof && station.m_roofCheckPoint != null)
			{
				Cover.GetCoverForPoint(station.m_roofCheckPoint.position, out float cover, out bool underRoof);
				usable = (underRoof && cover >= StationMinCover);
			}
			if (station.m_craftRequireFire && !station.m_haveFire)
			{
				usable = false;
			}
			stationChecks[id] = new KeyValuePair<float, bool>(Time.time + StationCheckTime, usable);
			return(usable);
		}

		/// <summary>
		/// Finds gear for the viking to repair at the village's workbench or forge - when a piece of it the station can
		/// repair has worn down to GearWornShare and no other viking is working at the station
		/// </summary>
		/// <returns>The job, or null when there's nothing to repair</returns>
		private Job FindGearWork()
		{
			foreach (int prefab in new[] { WorkbenchHash, ForgeHash })
			{
				CraftingStation bench = FindStation(prefab);
				if (bench != null && WornGear(bench, GearWornShare) != null && ClaimObject(bench.GetComponent<ZNetView>()))
				{
					return(new GearJob(this, bench));
				}
			}
			return(null);
		}

		/// <summary>
		/// Finds a piece of the viking's own gear that a crafting station can repair and that has worn below a share of
		/// its durability
		/// </summary>
		/// <param name="bench">The station</param>
		/// <param name="below">The share of its durability it has to be below - 1 for any wear</param>
		/// <returns>The most worn piece, or null when there's none</returns>
		private ItemDrop.ItemData WornGear(CraftingStation bench, float below)
		{
			ItemDrop.ItemData worst = null;
			float worstShare = below;
			foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
			{
				if (VikingItems.IsCarried(item) || VikingItems.IsProp(item) || !item.m_shared.m_useDurability || item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch)
				{
					continue;
				}
				float max = item.GetMaxDurability();
				float share = ((max > 0f) ? item.m_durability / max : 1f);
				if (share < worstShare && CanRepairAt(item, bench))
				{
					worst = item;
					worstShare = share;
				}
			}
			return(worst);
		}

		/// <summary>
		/// Checks whether a crafting station can repair an item, the way the game decides for a player - the station
		/// its recipe is crafted or repaired at, upgraded far enough
		/// </summary>
		/// <param name="item">The item</param>
		/// <param name="bench">The station</param>
		/// <returns>True when it can</returns>
		private static bool CanRepairAt(ItemDrop.ItemData item, CraftingStation bench)
		{
			if (!item.m_shared.m_canBeReparied || ObjectDB.instance == null)
			{
				return(false);
			}
			Recipe recipe = ObjectDB.instance.GetRecipe(item);
			if (recipe == null || (recipe.m_craftingStation == null && recipe.m_repairStation == null))
			{
				return(false);
			}
			bool station = ((recipe.m_repairStation != null && recipe.m_repairStation.m_name == bench.m_name) || (recipe.m_craftingStation != null && recipe.m_craftingStation.m_name == bench.m_name));
			return((station || item.m_worldLevel < Game.m_worldLevel) && Mathf.Min(bench.GetLevel(), 4) >= recipe.m_minStationLevel);
		}

		/// <summary>
		/// The kinds of gear a viking crafts for itself, in the order it crafts them - a melee weapon, a bow or crossbow, a
		/// shield, then armor for its chest, legs, head and shoulders
		/// </summary>
		private static readonly string[] CraftedGearKinds = { "melee", "ranged", "Shield", "Chest", "Legs", "Helmet", "Shoulder" };

		/// <summary>
		/// Finds a weapon, armor or a shield for the viking to craft - any the game has a recipe for that it would take up
		/// over its own (see VikingCore.WouldTakeUp), when the village has a station of the recipe's level and its chests
		/// hold everything it takes, wood included, so it never goes out gathering for it - the best of each kind it can
		/// make, in the order of CraftedGearKinds, one viking of the village arming itself at a time. It takes the piece
		/// up once it's crafted
		/// </summary>
		/// <remarks>
		/// A village seldom has what better gear takes unless players put it in its chests
		/// </remarks>
		/// <returns>The job, or null when there's nothing better it can craft</returns>
		private Job FindGearCraftWork()
		{
			if (viking == null || ObjectDB.instance == null || Time.time < nextGearCraftCheck || OtherDoing(ArmingKey))
			{
				return(null);
			}
			nextGearCraftCheck = Time.time + GearCraftCheckInterval;
			Dictionary<string, KeyValuePair<float, ArrowPlan>> picks = new Dictionary<string, KeyValuePair<float, ArrowPlan>>();
			int wood = VillageWood();
			foreach (Recipe recipe in ObjectDB.instance.m_recipes)
			{
				if (!Crafts(recipe))
				{
					continue;
				}
				ItemDrop.ItemData item = ItemOf(recipe.m_item);
				string kind = ((item != null) ? CraftedGearKind(item) : null);
				if (kind == null || !viking.WouldTakeUp(item))
				{
					continue;
				}
				float value = viking.GearValue(item);
				if (picks.TryGetValue(kind, out KeyValuePair<float, ArrowPlan> known) && known.Key >= value)
				{
					continue;
				}
				ArrowPlan plan = PlanCraft(item.m_dropPrefab.name, out string _);
				if (plan != null && plan.Wood <= wood)
				{
					picks[kind] = new KeyValuePair<float, ArrowPlan>(value, plan);
				}
			}
			foreach (string kind in CraftedGearKinds)
			{
				if (!picks.TryGetValue(kind, out KeyValuePair<float, ArrowPlan> pick))
				{
					continue;
				}
				ArrowPlan plan = pick.Value;
				if (plan.Station != null && !ClaimObject(plan.Station.GetComponent<ZNetView>()))
				{
					continue;
				}
				string name = Localization.instance.Localize(ItemOf(plan.Prefab).m_shared.m_name);
				return(new WeaponJob(this, plan, plan.Prefab, "Crafting " + name, false, true));
			}
			return(null);
		}

		/// <summary>
		/// Finds which kind of gear a viking crafts for itself an item is (see CraftedGearKinds)
		/// </summary>
		/// <param name="item">The item</param>
		/// <returns>Its kind, or null when it isn't a weapon other than a torch or a pickaxe, armor or a shield</returns>
		private static string CraftedGearKind(ItemDrop.ItemData item)
		{
			ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
			if (type == ItemDrop.ItemData.ItemType.Shield || type == ItemDrop.ItemData.ItemType.Chest || type == ItemDrop.ItemData.ItemType.Legs || type == ItemDrop.ItemData.ItemType.Helmet || type == ItemDrop.ItemData.ItemType.Shoulder)
			{
				return(type.ToString());
			}
			if (!item.IsWeapon() || type == ItemDrop.ItemData.ItemType.Torch || item.m_shared.m_damages.m_pickaxe > 0f)
			{
				return(null);
			}
			return(VikingCore.IsRanged(item) ? "ranged" : "melee");
		}

		/// <summary>
		/// Finds a piece of the viking's own gear to upgrade (see PlanUpgrade) when the village has everything it takes -
		/// its lowest quality pieces first, and of those, what it has equipped - one viking of the village at a time
		/// </summary>
		/// <returns>The job, or null when there's nothing it can upgrade</returns>
		private Job FindUpgradeWork()
		{
			if (OtherDoing(UpgradingKey) || ObjectDB.instance == null)
			{
				return(null);
			}
			List<ItemDrop.ItemData> gear = humanoid.GetInventory().GetAllItems().FindAll((ItemDrop.ItemData item) => !VikingItems.IsCarried(item) && !VikingItems.IsProp(item) && item.IsEquipable() && item.m_quality < item.m_shared.m_maxQuality);
			gear.Sort((ItemDrop.ItemData a, ItemDrop.ItemData b) => ((a.m_quality != b.m_quality) ? a.m_quality.CompareTo(b.m_quality) : b.m_equipped.CompareTo(a.m_equipped)));
			foreach (ItemDrop.ItemData item in gear)
			{
				ArrowPlan plan = PlanUpgrade(item);
				if (plan != null && ClaimObject(plan.Station.GetComponent<ZNetView>()))
				{
					return(new GearUpgradeJob(this, plan));
				}
			}
			return(null);
		}

		/// <summary>
		/// Works out what upgrading a piece of the viking's gear to its next quality takes, the way it does for a player -
		/// the station its recipe is crafted at, upgraded far enough for that quality, and the recipe's ingredients for
		/// it, everything but wood in the village's chests or carried for crafting, and the wood in the village too
		/// </summary>
		/// <param name="item">The piece</param>
		/// <returns>The upgrade, or null when the piece can't be upgraded, the village has no station for it, or it's
		/// short of anything it takes</returns>
		private ArrowPlan PlanUpgrade(ItemDrop.ItemData item)
		{
			Recipe recipe = ObjectDB.instance.GetRecipe(item);
			if (recipe == null || recipe.m_craftingStation == null || item.m_dropPrefab == null || item.m_quality >= item.m_shared.m_maxQuality)
			{
				return(null);
			}
			int quality = item.m_quality + 1;
			CraftingStation station = FindStation(recipe.m_craftingStation.gameObject.name.GetStableHashCode());
			if (station == null || station.GetLevel() < recipe.GetRequiredStationLevel(quality))
			{
				return(null);
			}
			ArrowPlan plan = new ArrowPlan { Prefab = item.m_dropPrefab.name, Batch = 1, Station = station, Upgrade = item, Quality = quality };
			List<Container> chests = UsableChests();
			foreach (Piece.Requirement requirement in recipe.m_resources)
			{
				int amount = CraftAmount(requirement, quality);
				if (amount <= 0)
				{
					continue;
				}
				string name = requirement.m_resItem.gameObject.name;
				if (WoodItems.Contains(name))
				{
					plan.Wood += amount;
				}
				else if (CraftStock(name, chests) >= amount)
				{
					plan.Others.Add(new KeyValuePair<string, int>(name, amount));
				}
				else
				{
					return(null);
				}
			}
			return((plan.Wood <= VillageWood()) ? plan : null);
		}

		/// <summary>
		/// Checks whether the piece an upgrade is for is still the viking's, at the quality it was to be raised from - or
		/// for a lining, still the viking's and not yet lined
		/// </summary>
		/// <param name="plan">The upgrade</param>
		/// <returns>True when it is</returns>
		private bool CanStillUpgrade(ArrowPlan plan)
		{
			if (!humanoid.GetInventory().ContainsItem(plan.Upgrade))
			{
				return(false);
			}
			return(plan.Lines ? ZombieLink.LiningRecipe(plan.Upgrade) != null : plan.Upgrade.m_quality == plan.Quality - 1);
		}

		/// <summary>
		/// Raises a piece of the viking's gear to the quality its upgrade is for, at full durability, as an upgrade a
		/// player makes leaves it - or lines a weapon with silver, leaving it as it is otherwise, as lining it at the
		/// forge does a player's
		/// </summary>
		/// <param name="plan">The upgrade</param>
		/// <returns>The piece</returns>
		private ItemDrop.ItemData UpgradeGear(ArrowPlan plan)
		{
			ItemDrop.ItemData item = plan.Upgrade;
			if (plan.Lines)
			{
				ZombieLink.Line(item);
				humanoid.GetInventory().Changed();
				TestLog.Count("Gear silver-lined", TestLog.ItemName(item));
				Plugin.Log.LogInfo(LogName() + " lined its " + Localization.instance.Localize(item.m_shared.m_name) + " with silver");
				return(item);
			}
			item.m_quality = plan.Quality;
			item.m_durability = item.GetMaxDurability();
			item.m_worldLevel = (byte)Game.m_worldLevel;
			humanoid.GetInventory().Changed();
			TestLog.Count("Gear upgraded", TestLog.ItemName(item));
			Plugin.Log.LogInfo(LogName() + " upgraded its " + Localization.instance.Localize(item.m_shared.m_name) + " to quality " + item.m_quality);
			return(item);
		}

		/// <summary>
		/// Adds arrows the viking crafted to its quiver, and readies the best it has
		/// </summary>
		/// <param name="prefab">The arrows' prefab name</param>
		/// <param name="count">How many</param>
		private void AddArrows(string prefab, int count)
		{
			humanoid.GetInventory().AddItem(prefab, count, 1, 0, 0L, "", false);
			ReadyBestArrows();
		}

		/// <summary>
		/// Sets the viking's crafting animation - the station's, or 0 to stop
		/// </summary>
		/// <param name="animation">The animation</param>
		private void SetCrafting(int animation)
		{
			if (humanoid != null && humanoid.m_zanim != null)
			{
				humanoid.m_zanim.SetInt(VikingPoses.CraftingInt, animation);
			}
		}

		/// <summary>
		/// Repairing its gear at the village's workbench or forge - walking to the station and working at it with the
		/// player's crafting animation, then repairing a piece the station can repair, the most worn first, with the
		/// station's repair sound, until every piece it can repair there is as good as new
		/// </summary>
		private class GearJob : Job
		{
			private readonly CraftingStation bench;
			private float workUntil = -1f;
			private float nextRenew;

			/// <summary>
			/// Makes the job, with the workbench already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="workbench">The workbench</param>
			public GearJob(VillageLife owner, CraftingStation workbench) : base(owner)
			{
				bench = workbench;
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Walks to the workbench and repairs its gear there a piece at a time
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once there's nothing left to repair, or it can't get to the workbench</returns>
			public override bool Tick(float dt)
			{
				if (bench == null)
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.ClaimObject(bench.GetComponent<ZNetView>());
				}
				ItemDrop.ItemData worn = life.WornGear(bench, 1f);
				if (workUntil >= 0f)
				{
					life.Face(bench.transform.position);
					if (Time.time < workUntil)
					{
						return(false);
					}
					workUntil = -1f;
					if (worn != null)
					{
						// The more worn it was, the more repairing it raises the skill, the same as a player's
						float max = worn.GetMaxDurability();
						life.RaiseSkill(Skills.SkillType.Crafting, ((max > 0f) ? 1f - worn.m_durability / max : 0f));
						worn.m_durability = max;
						VillageSignals.SendEffect(VillageSignals.GearRepairEffect, bench.GetComponent<ZNetView>().GetZDO().m_uid);
					}
					return(false);
				}
				if (worn == null)
				{
					return(true);
				}
				if (Utils.DistanceXZ(bench.transform.position, life.transform.position) > CraftReach)
				{
					life.Go(dt, bench.transform.position, CraftReach, false);
					return(life.moveFailed);
				}
				life.Face(bench.transform.position);
				life.SetCrafting(Mathf.Max(1, bench.m_useAnimation));
				workUntil = Time.time + GearRepairTime;
				return(false);
			}

			/// <summary>
			/// Stops working at the workbench and lets go of it
			/// </summary>
			public override void End()
			{
				life.SetCrafting(0);
				if (bench != null)
				{
					life.ReleaseObject(bench.GetComponent<ZNetView>());
				}
			}
		}

		/// <summary>
		/// Upgrading a piece of its own gear by day, one viking of the village at a time, when the village has everything
		/// it takes (see FindUpgradeWork) - getting what it takes besides wood from the village's chests and its wood,
		/// then working at the station its recipe is crafted at, the way it crafts (see ItemRun), and raising the piece a
		/// quality - or lining a weapon with silver at the forge (see FindLiningWork). Not to be mixed up with UpgradeJob,
		/// which takes up better gear from a chest
		/// </summary>
		private class GearUpgradeJob : Job
		{
			private readonly ItemRun upgradeRun;
			private readonly string label;
			private float nextRenew;

			/// <summary>
			/// Makes the job, with the station the piece is upgraded at already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="upgrade">What upgrading the piece takes</param>
			public GearUpgradeJob(VillageLife owner, ArrowPlan upgrade) : base(owner)
			{
				upgradeRun = new ItemRun(owner, null, (ItemDrop.ItemData item) => false, upgrade.Prefab, upgrade);
				string name = Localization.instance.Localize(upgrade.Upgrade.m_shared.m_name);
				label = (upgrade.Lines ? "Lining " + name + " with silver" : "Upgrading " + name + " to quality " + upgrade.Quality);
			}

			/// <summary>
			/// Notes that the viking is upgrading its gear, so no other viking of the village starts to
			/// </summary>
			public override void Begin()
			{
				life.SetDoing(UpgradingKey, true);
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>What it's upgrading</returns>
			public override string Describe()
			{
				return(label);
			}

			/// <summary>
			/// Checks whether the wood the viking is after lies outside the village
			/// </summary>
			/// <returns>True when it does</returns>
			public override bool Outing()
			{
				return(upgradeRun.Outing());
			}

			/// <summary>
			/// Gets what the upgrade takes and works at the station
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once the piece is upgraded or it can't get what it takes, or it's dusk</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk())
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.SetDoing(UpgradingKey, true);
					upgradeRun.Renew();
				}
				return(upgradeRun.Tick(dt, out ItemDrop.ItemData _));
			}

			/// <summary>
			/// Lets go of the station and the wood it was after, carries what it carries for crafting back to a chest, and
			/// notes it's no longer upgrading
			/// </summary>
			public override void End()
			{
				upgradeRun.Release();
				life.SetDoing(UpgradingKey, false);
			}
		}

		/// <summary>
		/// How many of a meal the village keeps in its chests before its vikings stop making more of it at the cauldron
		/// </summary>
		private const int MealKeep = 10;

		/// <summary>
		/// How often a viking looks for a meal to make, in seconds, since it goes through every recipe and chest
		/// </summary>
		private const float MealCheckInterval = 15f;

		/// <summary>
		/// When the viking next looks for a meal to make, by Time.time
		/// </summary>
		private float nextMealCheck;

		/// <summary>
		/// Finds a meal for the viking to make at the village's cauldron by day (see VillageCooking.TryPlaceCauldron) -
		/// the one that feeds best, by its health and stamina, among the cauldron's foods whose recipe its level allows,
		/// the chests hold what it takes, and the chests hold fewer than MealKeep of - claiming the cauldron, so one
		/// viking cooks at it at a time
		/// </summary>
		/// <returns>The job, or null when there's no cauldron, nothing it can make, or someone else is at it</returns>
		private Job FindMealWork()
		{
			if (Time.time < nextMealCheck || IsNight() || IsDusk() || ObjectDB.instance == null)
			{
				return(null);
			}
			nextMealCheck = Time.time + MealCheckInterval;
			CraftingStation cauldron = FindStation(VillageCooking.CauldronHash);
			ZNetView view = ((cauldron != null) ? cauldron.GetComponent<ZNetView>() : null);
			if (view == null || !IsFree(view))
			{
				return(null);
			}
			List<Container> chests = UsableChests();
			ArrowPlan best = null;
			float bestFood = 0f;
			foreach (Recipe recipe in ObjectDB.instance.m_recipes)
			{
				if (!IsMeal(recipe))
				{
					continue;
				}
				ItemDrop.ItemData.SharedData food = recipe.m_item.m_itemData.m_shared;
				float feeds = food.m_food + food.m_foodStamina;
				string prefab = recipe.m_item.gameObject.name;
				if (feeds <= bestFood || Stored(prefab, chests) >= MealKeep)
				{
					continue;
				}
				ArrowPlan plan = PlanCraft(prefab, out string _);
				if (plan != null && plan.Station == cauldron && plan.Wood <= 0)
				{
					best = plan;
					bestFood = feeds;
				}
			}
			if (best == null || !ClaimObject(view))
			{
				return(null);
			}
			return(new MealJob(this, best));
		}

		/// <summary>
		/// Checks whether a recipe makes a meal at the cauldron that the vikings eat - not what's cooked further
		/// elsewhere, like bread dough
		/// </summary>
		/// <param name="recipe">The recipe</param>
		/// <returns>True when it does</returns>
		private static bool IsMeal(Recipe recipe)
		{
			if (!Crafts(recipe) || recipe.m_craftingStation == null || recipe.m_craftingStation.gameObject.name != VillageCooking.CauldronPrefab)
			{
				return(false);
			}
			ItemDrop.ItemData.SharedData food = recipe.m_item.m_itemData.m_shared;
			return(food.m_itemType == ItemDrop.ItemData.ItemType.Consumable && food.m_food > 0f);
		}

		/// <summary>
		/// Checks whether a village's cauldron needs something for its meals - one of the meals its level allows takes
		/// it, and the chests hold fewer than MealKeep of that meal and less of it than the meal takes - like thistle for
		/// sausages, which foraging for food brings home then (see QuestLoot.PlantsFor)
		/// </summary>
		/// <param name="village">The village</param>
		/// <param name="item">The item's prefab name</param>
		/// <returns>True when it does, false with no cauldron loaded</returns>
		internal static bool CauldronWants(Village village, string item)
		{
			if (ObjectDB.instance == null || ZNetScene.instance == null || !VillageCooking.IsCauldronIngredient(item))
			{
				return(false);
			}
			int level = -1;
			foreach (KeyValuePair<ZDO, ZNetView> instance in ZNetScene.instance.m_instances)
			{
				CraftingStation station = ((instance.Key.GetPrefab() == VillageCooking.CauldronHash && instance.Value != null && village.OwnsPiece(instance.Value.transform.position)) ? instance.Value.GetComponent<CraftingStation>() : null);
				if (station != null)
				{
					level = Mathf.Max(level, station.GetLevel());
				}
			}
			if (level < 0)
			{
				return(false);
			}
			List<Container> chests = VillageChests(village);
			int stored = Stored(item, chests);
			foreach (Recipe recipe in ObjectDB.instance.m_recipes)
			{
				if (!IsMeal(recipe) || recipe.m_minStationLevel > level || Stored(recipe.m_item.gameObject.name, chests) >= MealKeep)
				{
					continue;
				}
				foreach (Piece.Requirement requirement in recipe.m_resources)
				{
					if (requirement.m_resItem != null && requirement.m_resItem.gameObject.name == item && stored < CraftAmount(requirement, 1))
					{
						return(true);
					}
				}
			}
			return(false);
		}

		/// <summary>
		/// How often a viking works out again how much raw food the village's chests hold back for meals, in seconds
		/// </summary>
		private const float MealReserveInterval = 10f;

		/// <summary>
		/// The raw food the village's chests hold back for meals at its cauldron, and how much of each they held, by
		/// prefab name, as last worked out (see UpdateMealReserve), and when to work it out again
		/// </summary>
		private readonly Dictionary<string, int> mealReserve = new Dictionary<string, int>();
		private readonly Dictionary<string, int> mealReserveStored = new Dictionary<string, int>();
		private float nextMealReserve;

		/// <summary>
		/// Works out how much raw food the village's chests hold back from its cooking stations for meals at its
		/// cauldron, every MealReserveInterval seconds - for each meal the cauldron's level allows and the chests hold
		/// fewer than MealKeep of, as much of each raw food its recipe takes, like the raw meat sausages take, so cooking
		/// leaves enough in the chests for one of each
		/// </summary>
		private void UpdateMealReserve()
		{
			if (Time.time < nextMealReserve)
			{
				return;
			}
			nextMealReserve = Time.time + MealReserveInterval;
			mealReserve.Clear();
			mealReserveStored.Clear();
			CraftingStation cauldron = ((ObjectDB.instance != null) ? FindStation(VillageCooking.CauldronHash) : null);
			if (cauldron == null)
			{
				return;
			}
			int level = cauldron.GetLevel();
			List<Container> chests = UsableChests();
			foreach (Recipe recipe in ObjectDB.instance.m_recipes)
			{
				if (!IsMeal(recipe) || recipe.m_minStationLevel > level || Stored(recipe.m_item.gameObject.name, chests) >= MealKeep)
				{
					continue;
				}
				foreach (Piece.Requirement requirement in recipe.m_resources)
				{
					int amount = CraftAmount(requirement, 1);
					if (amount <= 0 || !VillageCooking.IsRaw(requirement.m_resItem.gameObject.name))
					{
						continue;
					}
					string name = requirement.m_resItem.gameObject.name;
					mealReserve.TryGetValue(name, out int held);
					mealReserve[name] = Mathf.Max(held, amount);
				}
			}
			foreach (string name in mealReserve.Keys)
			{
				mealReserveStored[name] = Stored(name, chests);
			}
		}

		/// <summary>
		/// Gets how much of a raw food the village's chests hold back for meals (see UpdateMealReserve)
		/// </summary>
		/// <param name="name">The food's prefab name</param>
		/// <returns>How much, or 0</returns>
		private int MealHeld(string name)
		{
			UpdateMealReserve();
			return(mealReserve.TryGetValue(name, out int held) ? held : 0);
		}

		/// <summary>
		/// Checks whether the village's chests are short of a raw food they hold back for meals, as last worked out (see
		/// UpdateMealReserve), so what the viking carries of it goes into a chest rather than onto a cooking station
		/// </summary>
		/// <param name="name">The food's prefab name</param>
		/// <returns>True when they are</returns>
		private bool MealShort(string name)
		{
			int held = MealHeld(name);
			return(held > 0 && (!mealReserveStored.TryGetValue(name, out int stored) || stored < held));
		}

		/// <summary>
		/// Counts how many of an item the village's chests hold
		/// </summary>
		/// <param name="prefab">The item's prefab name</param>
		/// <param name="chests">The chests</param>
		/// <returns>How many</returns>
		private static int Stored(string prefab, List<Container> chests)
		{
			int count = 0;
			foreach (Container chest in chests)
			{
				foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
				{
					if (item.m_dropPrefab != null && item.m_dropPrefab.name == prefab)
					{
						count += item.m_stack;
					}
				}
			}
			return(count);
		}

		/// <summary>
		/// Making a meal at the village's cauldron (see FindMealWork) - getting what it takes from the village's chests,
		/// walking to the cauldron and working at it with the player's crafting animation and the cauldron's sounds,
		/// then trading what it carries for the meal, carried as loot to put away in a chest - raising the viking's
		/// Cooking skill. The game's crafting isn't run. What it carries for cooking goes back to a chest when it's done
		/// </summary>
		private class MealJob : Job
		{
			private readonly ArrowPlan plan;
			private readonly CraftingStation cauldron;
			private Container chest;
			private float actAt = -1f;
			private float craftUntil = -1f;
			private float nextRenew;

			/// <summary>
			/// Makes the job, with the cauldron already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="meal">The meal it makes</param>
			public MealJob(VillageLife owner, ArrowPlan meal) : base(owner)
			{
				plan = meal;
				cauldron = meal.Station;
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name and the meal it makes</returns>
			public override string Describe()
			{
				return("Cook a meal (" + plan.Prefab + ")");
			}

			/// <summary>
			/// Gets what the meal takes, walks to the cauldron, works at it and makes the meal
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once the meal's made, what it takes is gone, or it can't get to the cauldron</returns>
			public override bool Tick(float dt)
			{
				if (cauldron == null)
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.ClaimObject(cauldron.GetComponent<ZNetView>());
				}
				if (craftUntil >= 0f)
				{
					life.Face(cauldron.transform.position);
					if (Time.time < craftUntil)
					{
						return(false);
					}
					craftUntil = -1f;
					life.SetCrafting(0);
					if (life.UseCraft(plan))
					{
						life.CarryMeal(plan.Prefab, plan.Batch);
						life.RaiseSkill(Skills.SkillType.Cooking, CookAddSkill);
						VillageSignals.SendEffect(VillageSignals.CraftDoneEffect, cauldron.GetComponent<ZNetView>().GetZDO().m_uid);
					}
					return(true);
				}
				if (life.MissingCraft(plan, out string ingredient, out int needed))
				{
					return(Fetch(dt, ingredient, needed));
				}
				// A cauldron over a fire is worked from outside the fire's flames, the way a cooking station is
				FireSafety.CookSpot(cauldron.transform.position, life.transform.position, CraftReach, out Vector3 spot, out float within);
				if (Utils.DistanceXZ(spot, life.transform.position) > Mathf.Max(within, ArriveReach))
				{
					life.Go(dt, spot, within, false);
					return(life.moveFailed);
				}
				life.Face(cauldron.transform.position);
				life.SetCrafting(Mathf.Max(1, cauldron.m_useAnimation));
				VillageSignals.SendEffect(VillageSignals.CraftEffect, cauldron.GetComponent<ZNetView>().GetZDO().m_uid);
				craftUntil = Time.time + CraftTime;
				return(false);
			}

			/// <summary>
			/// Walks to the nearest chest holding something the meal takes and takes it - giving up when no chest holds it
			/// any more, or it can't get to the chest
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="ingredient">The ingredient's prefab name</param>
			/// <param name="needed">How many more it needs</param>
			/// <returns>True when it gives up</returns>
			private bool Fetch(float dt, string ingredient, int needed)
			{
				if (chest == null || !life.Usable(chest))
				{
					chest = life.ChestWithCraft(ingredient);
					actAt = -1f;
					if (chest == null)
					{
						return(true);
					}
				}
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						chest = null;
					}
					return(false);
				}
				if (actAt < 0f)
				{
					life.Face(chest.transform.position);
					VikingPoses.Interact(life.humanoid);
					life.ShowChestOpen(chest);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (Time.time < actAt)
				{
					return(false);
				}
				if (life.TakeCraft(chest, ingredient, needed) <= 0)
				{
					life.Avoid(chest.m_nview);
				}
				chest = null;
				actAt = -1f;
				return(false);
			}

			/// <summary>
			/// Stops working at the cauldron and lets go of it, carrying what it carries for cooking back to a chest
			/// </summary>
			public override void End()
			{
				life.SetCrafting(0);
				if (cauldron != null)
				{
					life.ReleaseObject(cauldron.GetComponent<ZNetView>());
				}
				life.ReturnCraft();
			}
		}

		/// <summary>
		/// Carries a meal the viking made, as loot to put away in a chest - dropping what it has no room to carry at its
		/// feet, where it's picked up like any loot
		/// </summary>
		/// <param name="prefab">The meal's prefab name</param>
		/// <param name="amount">How many it made</param>
		private void CarryMeal(string prefab, int amount)
		{
			GameObject item = ObjectDB.instance.GetItemPrefab(prefab);
			ItemDrop drop = ((item != null) ? item.GetComponent<ItemDrop>() : null);
			if (drop == null)
			{
				return;
			}
			ItemDrop.ItemData meal = drop.m_itemData.Clone();
			meal.m_dropPrefab = item;
			int carried = Carry(meal, amount, CarryLoot);
			if (carried < amount)
			{
				ItemDrop.DropItem(meal, amount - carried, transform.position + transform.forward * 0.6f + Vector3.up * 0.5f, Quaternion.identity);
			}
		}

		/// <summary>
		/// Crafting arrows - the best the village can make (see PlanArrows) - at the station their recipe needs: getting
		/// what they take besides wood from the village's chests, getting a batch's wood when it carries too little,
		/// walking to the station and working at it with the player's crafting animation and the station's sounds, then
		/// trading what it carries for the arrows, a batch at a time until its quiver is nearly full - the game's
		/// crafting isn't run. Once it's done, what it carries for crafting goes back to a chest
		/// </summary>
		private class ArrowJob : Job
		{
			private ArrowPlan plan;
			private readonly CraftingStation bench;
			private WoodRun run;
			private Container chest;
			private float actAt = -1f;
			private float craftUntil = -1f;
			private float nextRenew;

			/// <summary>
			/// Makes the job, with the station already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="arrows">The arrows it crafts</param>
			public ArrowJob(VillageLife owner, ArrowPlan arrows) : base(owner)
			{
				plan = arrows;
				bench = arrows.Station;
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name and the arrows it crafts</returns>
			public override string Describe()
			{
				return("Arrow (" + plan.Prefab + ")");
			}

			/// <summary>
			/// Gets the ingredients and wood, walks to the station, works at it and crafts a batch of arrows
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it isn't missing a batch, what it takes is gone, there's no wood left to get, or it can't
			/// get to the station</returns>
			public override bool Tick(float dt)
			{
				if (bench == null)
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.ClaimObject(bench.GetComponent<ZNetView>());
				}
				if (craftUntil >= 0f)
				{
					life.Face(bench.transform.position);
					if (Time.time < craftUntil)
					{
						return(false);
					}
					craftUntil = -1f;
					life.SetCrafting(0);
					if (life.CarriedWood() >= plan.Wood && life.UseCraft(plan))
					{
						life.UseWood(plan.Wood);
						int made = plan.Batch + life.CraftBonus(bench);
						life.AddArrows(plan.Prefab, made);
						TestLog.Count("Crafted", TestLog.ItemName(ItemOf(plan.Prefab)), made);
						VillageSignals.SendEffect(VillageSignals.CraftDoneEffect, bench.GetComponent<ZNetView>().GetZDO().m_uid);
					}
					// The next batch is the best the village can make now, at this station
					ArrowPlan next = (plan.Silver ? life.PlanSilverArrows() : life.PlanArrows());
					if (next == null || next.Station != bench)
					{
						return(true);
					}
					plan = next;
					return(false);
				}
				if (!(plan.Silver ? life.MissingSilverArrows(plan.Batch) : life.MissingArrows(plan.Batch, plan.Wood)))
				{
					return(true);
				}
				if (life.MissingCraft(plan, out string ingredient, out int needed))
				{
					return(Fetch(dt, ingredient, needed));
				}
				if (!life.HaveWood(plan.Wood, ref run, dt, out bool done))
				{
					return(done);
				}
				if (Utils.DistanceXZ(bench.transform.position, life.transform.position) > CraftReach)
				{
					life.Go(dt, bench.transform.position, CraftReach, false);
					return(life.moveFailed);
				}
				life.Face(bench.transform.position);
				life.SetCrafting(Mathf.Max(1, bench.m_useAnimation));
				VillageSignals.SendEffect(VillageSignals.CraftEffect, bench.GetComponent<ZNetView>().GetZDO().m_uid);
				craftUntil = Time.time + CraftTime;
				return(false);
			}

			/// <summary>
			/// Walks to the nearest chest holding an ingredient it needs and takes it - giving up when no chest holds it
			/// any more, or it can't get to the chest
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="ingredient">The ingredient's prefab name</param>
			/// <param name="needed">How many more it needs</param>
			/// <returns>True when it gives up</returns>
			private bool Fetch(float dt, string ingredient, int needed)
			{
				if (chest == null || !life.Usable(chest))
				{
					chest = life.ChestWithCraft(ingredient);
					actAt = -1f;
					if (chest == null)
					{
						return(true);
					}
				}
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						chest = null;
					}
					return(false);
				}
				if (actAt < 0f)
				{
					life.Face(chest.transform.position);
					VikingPoses.Interact(life.humanoid);
					life.ShowChestOpen(chest);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (Time.time < actAt)
				{
					return(false);
				}
				// A chest it took nothing from - no room to carry it, say - is left alone for a while
				if (life.TakeCraft(chest, ingredient, needed) <= 0)
				{
					life.Avoid(chest.m_nview);
				}
				chest = null;
				actAt = -1f;
				return(false);
			}

			/// <summary>
			/// Checks whether the wood the viking is after lies outside the village
			/// </summary>
			/// <returns>True when it does</returns>
			public override bool Outing()
			{
				return(run != null && run.Outing());
			}

			/// <summary>
			/// Stops working at the station and lets go of it and the wood it was after, carrying what it carries for
			/// crafting back to a chest
			/// </summary>
			public override void End()
			{
				life.SetCrafting(0);
				if (run != null)
				{
					run.Release();
				}
				if (bench != null)
				{
					life.ReleaseObject(bench.GetComponent<ZNetView>());
				}
				life.ReturnCraft();
			}
		}

		/// <summary>
		/// Crafting a new torch for itself (see FindTorchWork) - getting what it takes from the village's chests and
		/// wood, then crafting it at the station its recipe needs, or where it stands when it needs none - raising the
		/// viking's crafting skill the way a player's rises. At dusk and night (see FindNightTorchWork) it takes only
		/// wood it carries or the chests hold, rather than going out to gather it in the dark
		/// </summary>
		private class TorchJob : Job
		{
			private readonly ArrowPlan plan;
			private readonly CraftingStation bench;
			private readonly bool night;
			private WoodRun run;
			private Container chest;
			private float actAt = -1f;
			private float craftUntil = -1f;
			private float nextRenew;

			/// <summary>
			/// Makes the job, with the station already claimed when the torch needs one
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="torch">What the torch takes</param>
			/// <param name="dark">True for one crafted at dusk or night, from what it carries and the chests hold</param>
			public TorchJob(VillageLife owner, ArrowPlan torch, bool dark = false) : base(owner)
			{
				plan = torch;
				bench = torch.Station;
				night = dark;
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Gets the ingredients and wood, goes to the station when there is one, works and crafts the torch
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it's crafted, it no longer needs one, what it takes is gone, there's no wood left to get -
			/// carried or in the chests, in the dark - or it can't get to the station</returns>
			public override bool Tick(float dt)
			{
				if (bench != null && Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.ClaimObject(bench.GetComponent<ZNetView>());
				}
				if (craftUntil >= 0f)
				{
					if (bench != null)
					{
						life.Face(bench.transform.position);
					}
					if (Time.time < craftUntil)
					{
						return(false);
					}
					craftUntil = -1f;
					life.SetCrafting(0);
					if (life.CarriedWood() >= plan.Wood && life.UseCraft(plan))
					{
						life.UseWood(plan.Wood);
						life.humanoid.GetInventory().AddItem(plan.Prefab, plan.Batch, 1, 0, 0L, "", false);
						Skills.SkillType skill = ((bench != null && bench.m_craftingSkill != Skills.SkillType.None) ? bench.m_craftingSkill : Skills.SkillType.Crafting);
						life.RaiseSkill(skill, 1f);
						if (bench != null)
						{
							VillageSignals.SendEffect(VillageSignals.CraftDoneEffect, bench.GetComponent<ZNetView>().GetZDO().m_uid);
						}
					}
					return(true);
				}
				if (night ? !life.TorchShort() : !life.NeedsTorch())
				{
					return(true);
				}
				if (life.MissingCraft(plan, out string ingredient, out int needed))
				{
					return(Fetch(dt, ingredient, needed));
				}
				// In the dark it only takes wood it carries or the chests hold
				if (night && life.CarriedWood() < plan.Wood && life.FindWoodChest() == null)
				{
					life.nightTorchAgainAt = Time.time + NightTorchRetry;
					return(true);
				}
				if (!life.HaveWood(plan.Wood, ref run, dt, out bool done))
				{
					return(done);
				}
				if (bench != null)
				{
					if (Utils.DistanceXZ(bench.transform.position, life.transform.position) > CraftReach)
					{
						life.Go(dt, bench.transform.position, CraftReach, false);
						return(life.moveFailed);
					}
					life.Face(bench.transform.position);
					life.SetCrafting(Mathf.Max(1, bench.m_useAnimation));
					VillageSignals.SendEffect(VillageSignals.CraftEffect, bench.GetComponent<ZNetView>().GetZDO().m_uid);
				}
				else
				{
					life.ai.StopMoving();
					VikingPoses.Interact(life.humanoid);
				}
				craftUntil = Time.time + CraftTime;
				return(false);
			}

			/// <summary>
			/// Walks to the nearest chest holding an ingredient it needs and takes it - giving up when no chest holds it
			/// any more, or it can't get to the chest
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="ingredient">The ingredient's prefab name</param>
			/// <param name="needed">How many more it needs</param>
			/// <returns>True when it gives up</returns>
			private bool Fetch(float dt, string ingredient, int needed)
			{
				if (chest == null || !life.Usable(chest))
				{
					chest = life.ChestWithCraft(ingredient);
					actAt = -1f;
					if (chest == null)
					{
						return(true);
					}
				}
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						chest = null;
					}
					return(false);
				}
				if (actAt < 0f)
				{
					life.Face(chest.transform.position);
					VikingPoses.Interact(life.humanoid);
					life.ShowChestOpen(chest);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (Time.time < actAt)
				{
					return(false);
				}
				// A chest it took nothing from - no room to carry it, say - is left alone for a while
				if (life.TakeCraft(chest, ingredient, needed) <= 0)
				{
					life.Avoid(chest.m_nview);
				}
				chest = null;
				actAt = -1f;
				return(false);
			}

			/// <summary>
			/// Checks whether the wood the viking is after lies outside the village
			/// </summary>
			/// <returns>True when it does</returns>
			public override bool Outing()
			{
				return(run != null && run.Outing());
			}

			/// <summary>
			/// Stops working and lets go of the station and the wood it was after, carrying what it carries for crafting
			/// back to a chest
			/// </summary>
			public override void End()
			{
				life.SetCrafting(0);
				if (run != null)
				{
					run.Release();
				}
				if (bench != null)
				{
					life.ReleaseObject(bench.GetComponent<ZNetView>());
				}
				life.ReturnCraft();
			}
		}

		/// <summary>
		/// Making paths by day - from a house added to the village for a player's bed to the rest of the village (see
		/// VillagePaths.PlanHousePath). The viking gets a hoe - one of its own, one from a chest, or one it crafts at the
		/// workbench from what the village has, gathering the wood - then walks the path, swinging the hoe at each spot
		/// the way a player does, painting the hoe's path there and clearing the clutter lying on it, the hoe wearing down
		/// as a player's does. Where the path crosses water, it gathers the wood for a bridge when it reaches the bank and
		/// hammers it up (see VillagePaths.HouseBridge). A hoe that breaks is repaired at the workbench before it carries
		/// on. Once the path is made, it repairs the hoe at the workbench and puts it away in a chest
		/// </summary>
		private class PathJob : Job
		{
			private readonly PathProgress progress;
			private readonly VillagePaths.HousePath plan;
			private readonly ItemRun hoeRun;
			private CraftingStation repairBench;

			/// <summary>
			/// The wood it's getting for the path's next bridge, and its hammering at it
			/// </summary>
			private WoodRun bridgeRun;
			private HammerWork bridgeWork;
			private bool finishing;
			private float repairUntil = -1f;
			private float hitAt = -1f;
			private float nextSwing;
			private float nextRenew;

			/// <summary>
			/// Makes the job, with the station the hoe is crafted at already claimed when it's crafted
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="path">The path it makes, carried on from where it was left</param>
			/// <param name="fromChest">The chest it takes a hoe from, or null</param>
			/// <param name="hoe">What the hoe takes to craft when it crafts one, or null</param>
			public PathJob(VillageLife owner, PathProgress path, Container fromChest, ArrowPlan hoe) : base(owner)
			{
				progress = path;
				plan = path.Plan;
				hoeRun = new ItemRun(owner, fromChest, (ItemDrop.ItemData item) => item.m_dropPrefab != null && item.m_dropPrefab.name == HoePrefab, HoePrefab, hoe);
			}

			/// <summary>
			/// Notes that the viking is making paths, so no other viking of the village starts to
			/// </summary>
			public override void Begin()
			{
				life.SetDoing(PathingKey, true);
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name, and how far along the path it is</returns>
			public override string Describe()
			{
				return("Making paths (" + progress.Next + "/" + plan.Spots.Count + ")");
			}

			/// <summary>
			/// Checks whether the path or the wood the viking is after has it out of the village
			/// </summary>
			/// <returns>True when it has</returns>
			public override bool Outing()
			{
				bool spotOutside = (progress.Next < plan.Spots.Count && !life.village.Contains(plan.Spots[progress.Next]));
				return(spotOutside || hoeRun.Outing() || (bridgeRun != null && bridgeRun.Outing()));
			}

			/// <summary>
			/// Gets a hoe, makes the path a spot at a time, repairing the hoe when it breaks, then repairs it at the end
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once the path is made and the hoe repaired, there's no hoe to be had, it's dusk, or a broken
			/// hoe can't be repaired</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk())
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.SetDoing(PathingKey, true);
					hoeRun.Renew();
					if (repairBench != null)
					{
						life.ClaimObject(repairBench.GetComponent<ZNetView>());
					}
				}
				ItemDrop.ItemData hoe = life.OwnTool(HoePrefab);
				if (hoe == null)
				{
					return(hoeRun.Tick(dt, out ItemDrop.ItemData _) && life.OwnTool(HoePrefab) == null);
				}
				bool broken = (hoe.m_shared.m_useDurability && hoe.m_durability <= 0f);
				if (finishing || broken)
				{
					bool worn = (hoe.m_shared.m_useDurability && hoe.m_durability < hoe.GetMaxDurability());
					if (worn && !Repair(dt, hoe, out bool gaveUp))
					{
						return(gaveUp);
					}
					return(finishing);
				}
				return(MakePath(dt, hoe));
			}

			/// <summary>
			/// Walks to the next spot of the path, swings the hoe at it and paints the path there, clearing its clutter
			/// and wearing the hoe down - leaving out a spot it can't get to
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="hoe">The hoe</param>
			/// <returns>False, so the job goes on</returns>
			private bool MakePath(float dt, ItemDrop.ItemData hoe)
			{
				if (progress.BridgeNext < plan.Bridges.Count && progress.Next >= plan.Bridges[progress.BridgeNext].Before)
				{
					BuildBridge(dt);
					return(false);
				}
				Vector3 spot = plan.Spots[progress.Next];
				if (hitAt >= 0f)
				{
					life.Face(spot);
					if (Time.time < hitAt)
					{
						return(false);
					}
					hitAt = -1f;
					VillagePaths.PaintSpot(spot, plan.Clutter[progress.Next]);
					if (hoe.m_shared.m_useDurability)
					{
						hoe.m_durability = Mathf.Max(0f, hoe.m_durability - hoe.m_shared.m_useDurabilityDrain * Game.m_durabilityRate);
					}
					Advance();
					return(false);
				}
				if (Utils.DistanceXZ(spot, life.transform.position) > WorkReach)
				{
					life.Go(dt, spot, WorkReach, false);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						Advance();
					}
					return(false);
				}
				life.Face(spot);
				if (!life.humanoid.IsItemEquiped(hoe))
				{
					life.humanoid.EquipItem(hoe, false);
				}
				if (Time.time < nextSwing)
				{
					return(false);
				}
				VikingPoses.Swing(life.humanoid, hoe);
				hitAt = Time.time + SwingHitTime;
				nextSwing = Time.time + SwingInterval;
				return(false);
			}

			/// <summary>
			/// Builds the path's next bridge once it reaches the bank it starts from - gathering the wood it takes, then
			/// walking to the bank and hammering it up - leaving it out when there's no wood left to get or it can't get
			/// to the bank
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			private void BuildBridge(float dt)
			{
				VillagePaths.HouseBridge bridge = plan.Bridges[progress.BridgeNext];
				int wood = Mathf.Min(bridge.Wood, CarryMax);
				if (!life.HaveWood(wood, ref bridgeRun, dt, out bool done))
				{
					if (done)
					{
						NextBridge();
					}
					return;
				}
				if (bridgeWork == null)
				{
					bridgeWork = new HammerWork(life, RebuildSwings);
				}
				if (!bridgeWork.Tick(dt, bridge.Start))
				{
					if (life.moveFailed)
					{
						life.moveFailed = false;
						NextBridge();
					}
					return;
				}
				if (life.CarriedWood() >= wood)
				{
					life.UseWood(wood);
					VillagePaths.BuildHouseBridge(life.village, bridge);
				}
				NextBridge();
			}

			/// <summary>
			/// Moves on to the path's next bridge
			/// </summary>
			private void NextBridge()
			{
				progress.BridgeNext++;
				bridgeWork = null;
				if (bridgeRun != null)
				{
					bridgeRun.Release();
					bridgeRun = null;
				}
			}

			/// <summary>
			/// Moves on to the path's next spot, noting the house as joined to the village once it's past the last, and
			/// the path as no longer partway made
			/// </summary>
			private void Advance()
			{
				progress.Next++;
				if (progress.Next >= plan.Spots.Count)
				{
					VillagePaths.Pathed(life.village, plan.House);
					pathProgress.Remove(life.village.Zdo.m_uid);
					finishing = true;
					ItemDrop.ItemData hoe = life.OwnTool(HoePrefab);
					if (hoe != null && life.humanoid.IsItemEquiped(hoe))
					{
						life.humanoid.UnequipItem(hoe, false);
					}
				}
			}

			/// <summary>
			/// Repairs the hoe at the village's workbench, working at it with the player's crafting animation
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="hoe">The hoe</param>
			/// <param name="gaveUp">Set to true when there's no workbench that can repair it, or it can't get there</param>
			/// <returns>True once it's repaired</returns>
			private bool Repair(float dt, ItemDrop.ItemData hoe, out bool gaveUp)
			{
				gaveUp = false;
				if (repairBench == null)
				{
					repairBench = life.FindStation(WorkbenchHash);
					if (repairBench == null || !CanRepairAt(hoe, repairBench) || !life.ClaimObject(repairBench.GetComponent<ZNetView>()))
					{
						repairBench = null;
						gaveUp = true;
						return(false);
					}
				}
				if (repairUntil >= 0f)
				{
					life.Face(repairBench.transform.position);
					if (Time.time < repairUntil)
					{
						return(false);
					}
					repairUntil = -1f;
					life.SetCrafting(0);
					float max = hoe.GetMaxDurability();
					life.RaiseSkill(Skills.SkillType.Crafting, ((max > 0f) ? 1f - hoe.m_durability / max : 0f));
					hoe.m_durability = max;
					VillageSignals.SendEffect(VillageSignals.GearRepairEffect, repairBench.GetComponent<ZNetView>().GetZDO().m_uid);
					life.ReleaseObject(repairBench.GetComponent<ZNetView>());
					repairBench = null;
					return(true);
				}
				if (Utils.DistanceXZ(repairBench.transform.position, life.transform.position) > CraftReach)
				{
					life.Go(dt, repairBench.transform.position, CraftReach, false);
					gaveUp = life.moveFailed;
					return(false);
				}
				life.Face(repairBench.transform.position);
				life.SetCrafting(Mathf.Max(1, repairBench.m_useAnimation));
				repairUntil = Time.time + GearRepairTime;
				return(false);
			}

			/// <summary>
			/// Stops working, lets go of the stations and the wood it was after, carries what it carries for crafting
			/// back to a chest, puts the hoe away in a chest and notes it's no longer making paths
			/// </summary>
			public override void End()
			{
				hoeRun.Release();
				if (bridgeRun != null)
				{
					bridgeRun.Release();
				}
				if (repairBench != null)
				{
					life.ReleaseObject(repairBench.GetComponent<ZNetView>());
				}
				life.PutToolAway(HoePrefab);
				life.SetDoing(PathingKey, false);
			}
		}

		/// <summary>
		/// Getting an item a viking works or fights with - taking it from a chest as its own, or else crafting it from
		/// the game's recipe: getting what it takes besides wood from the village's chests, gathering the wood, then
		/// working at the station with the player's crafting animation and the station's sounds, raising the viking's
		/// skill the way a player's rises. A plan for an upgrade (see PlanUpgrade) raises a piece it has a quality
		/// instead of crafting a new one
		/// </summary>
		private class ItemRun
		{
			private readonly VillageLife life;
			private readonly System.Predicate<ItemDrop.ItemData> takes;
			private readonly string prefab;
			private Container source;
			private ArrowPlan craft;
			private WoodRun run;
			private Container chest;
			private float actAt = -1f;
			private float craftUntil = -1f;

			/// <summary>
			/// Starts getting the item, with the station it's crafted at already claimed when it's crafted
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="fromChest">The chest it takes the item from, or null to craft it</param>
			/// <param name="wanted">Which items in the chest it takes</param>
			/// <param name="craftPrefab">The prefab it crafts when the chest has none</param>
			/// <param name="recipe">What it takes to craft, or null when it's taken from the chest</param>
			public ItemRun(VillageLife owner, Container fromChest, System.Predicate<ItemDrop.ItemData> wanted, string craftPrefab, ArrowPlan recipe)
			{
				life = owner;
				source = fromChest;
				takes = wanted;
				prefab = craftPrefab;
				craft = recipe;
			}

			/// <summary>
			/// Takes the item from the chest, or else crafts it
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="got">Set to the item once it has it, or null</param>
			/// <returns>True once it has it, or can't get it</returns>
			public bool Tick(float dt, out ItemDrop.ItemData got)
			{
				got = null;
				if (source != null)
				{
					if (!life.Usable(source))
					{
						FromChestFailed(false);
						return(craft == null);
					}
					if (!Reach(dt, source))
					{
						return(false);
					}
					got = life.TakeTool(source, takes);
					if (got == null)
					{
						FromChestFailed(true);
						return(craft == null);
					}
					source = null;
					return(true);
				}
				if (craft == null)
				{
					return(true);
				}
				return(Craft(dt, out got));
			}

			/// <summary>
			/// Gives up on the chest - leaving it alone for a while when it couldn't be used - and crafts the item instead
			/// when the village has what it takes
			/// </summary>
			/// <param name="avoid">True to leave the chest alone for a while</param>
			private void FromChestFailed(bool avoid)
			{
				if (avoid && source != null)
				{
					life.Avoid(source.m_nview);
				}
				source = null;
				craft = life.PlanCraft(prefab, out string _);
				if (craft != null && craft.Station != null && !life.ClaimObject(craft.Station.GetComponent<ZNetView>()))
				{
					craft = null;
				}
			}

			/// <summary>
			/// Crafts the item - getting what it takes besides wood from the village's chests and its wood, then working
			/// at the station
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="got">Set to the item once it's crafted, or null</param>
			/// <returns>True once it's crafted, or what it takes is gone, there's no wood left to get, or it can't get to
			/// the station</returns>
			private bool Craft(float dt, out ItemDrop.ItemData got)
			{
				got = null;
				CraftingStation bench = craft.Station;
				if (craftUntil >= 0f)
				{
					if (bench != null)
					{
						life.Face(bench.transform.position);
					}
					if (Time.time < craftUntil)
					{
						return(false);
					}
					craftUntil = -1f;
					life.SetCrafting(0);
					// A piece to upgrade that's gone, or was upgraded meanwhile, takes nothing
					if (craft.Upgrade != null && !life.CanStillUpgrade(craft))
					{
						return(true);
					}
					if (life.CarriedWood() < craft.Wood || !life.UseCraft(craft))
					{
						return(true);
					}
					life.UseWood(craft.Wood);
					got = ((craft.Upgrade != null) ? life.UpgradeGear(craft) : life.humanoid.GetInventory().AddItem(craft.Prefab, 1, 1, 0, 0L, "", false));
					if (got != null && craft.Upgrade == null)
					{
						TestLog.Count("Crafted", TestLog.ItemName(got));
					}
					life.RaiseSkill(((bench != null && bench.m_craftingSkill != Skills.SkillType.None) ? bench.m_craftingSkill : Skills.SkillType.Crafting), 1f);
					if (bench != null)
					{
						VillageSignals.SendEffect(VillageSignals.CraftDoneEffect, bench.GetComponent<ZNetView>().GetZDO().m_uid);
					}
					return(true);
				}
				if (life.MissingCraft(craft, out string ingredient, out int needed))
				{
					return(Fetch(dt, ingredient, needed));
				}
				if (!life.HaveWood(craft.Wood, ref run, dt, out bool done))
				{
					return(done);
				}
				if (bench != null)
				{
					if (Utils.DistanceXZ(bench.transform.position, life.transform.position) > CraftReach)
					{
						life.Go(dt, bench.transform.position, CraftReach, false);
						return(life.moveFailed);
					}
					life.Face(bench.transform.position);
					life.SetCrafting(Mathf.Max(1, bench.m_useAnimation));
					VillageSignals.SendEffect(VillageSignals.CraftEffect, bench.GetComponent<ZNetView>().GetZDO().m_uid);
				}
				else
				{
					life.ai.StopMoving();
					VikingPoses.Interact(life.humanoid);
				}
				craftUntil = Time.time + CraftTime;
				return(false);
			}

			/// <summary>
			/// Walks to the nearest chest holding an ingredient the item takes and takes it - giving up when no chest
			/// holds it any more
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="ingredient">The ingredient's prefab name</param>
			/// <param name="needed">How many more it needs</param>
			/// <returns>True when it gives up</returns>
			private bool Fetch(float dt, string ingredient, int needed)
			{
				if (chest == null || !life.Usable(chest))
				{
					chest = life.ChestWithCraft(ingredient);
					actAt = -1f;
					if (chest == null)
					{
						return(true);
					}
				}
				if (!Reach(dt, chest))
				{
					return(false);
				}
				// A chest it took nothing from - no room to carry it, say - is left alone for a while
				if (life.TakeCraft(chest, ingredient, needed) <= 0)
				{
					life.Avoid(chest.m_nview);
				}
				chest = null;
				return(false);
			}

			/// <summary>
			/// Walks to a chest and reaches into it, the way a player opens one - leaving a chest it can't get to alone
			/// for a while
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <param name="target">The chest</param>
			/// <returns>True once it has reached into it</returns>
			private bool Reach(float dt, Container target)
			{
				if (Utils.DistanceXZ(target.transform.position, life.transform.position) > ChestReach)
				{
					life.Go(dt, target.transform.position, ChestReach, false);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						if (target == source)
						{
							FromChestFailed(true);
						}
						else
						{
							life.Avoid(target.m_nview);
							chest = null;
						}
					}
					actAt = -1f;
					return(false);
				}
				if (actAt < 0f)
				{
					life.Face(target.transform.position);
					VikingPoses.Interact(life.humanoid);
					life.ShowChestOpen(target);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (Time.time < actAt)
				{
					return(false);
				}
				actAt = -1f;
				return(true);
			}

			/// <summary>
			/// Checks whether the wood the viking is after lies outside the village
			/// </summary>
			/// <returns>True when it does</returns>
			public bool Outing()
			{
				return(run != null && run.Outing());
			}

			/// <summary>
			/// Renews the viking's claim on the station it crafts at
			/// </summary>
			public void Renew()
			{
				if (craft != null && craft.Station != null)
				{
					life.ClaimObject(craft.Station.GetComponent<ZNetView>());
				}
			}

			/// <summary>
			/// Stops working at the station and lets go of it and the wood it was after, carrying what it carries for
			/// crafting back to a chest
			/// </summary>
			public void Release()
			{
				life.SetCrafting(0);
				if (run != null)
				{
					run.Release();
				}
				if (craft != null && craft.Station != null)
				{
					life.ReleaseObject(craft.Station.GetComponent<ZNetView>());
				}
				life.ReturnCraft();
			}
		}

		/// <summary>
		/// Arming itself or the village by day, one viking of the village at a time - crafting the weapon it's missing of
		/// the two every viking carries, a crude bow or a melee weapon (see FindSidearmWork), at the station its recipe
		/// needs, then keeping it with the other to pick between as each fight calls for (see VikingCore.ChooseWeapon);
		/// crafting a tool for the village's chests (see FindToolStockWork) or what its goal takes (see FindGoalWork), which
		/// it then puts away; or crafting a weapon,
		/// armor or a shield better than its own from what the village has (see FindGearCraftWork), which it then takes
		/// up the way it takes up gear it finds
		/// </summary>
		private class WeaponJob : Job
		{
			private readonly ItemRun weaponRun;
			private readonly string label;
			private readonly bool store;
			private readonly bool takeUp;
			private float nextRenew;

			/// <summary>
			/// Makes the job, with the station the weapon is crafted at already claimed
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="weapon">What the weapon takes to craft</param>
			/// <param name="prefab">The weapon it crafts</param>
			/// <param name="name">What the job shows as in the debug hover text</param>
			/// <param name="forChest">True to put the weapon away in a chest for the village, rather than keep it</param>
			/// <param name="wear">True to take it up as gear better than its own, carrying what it replaces to a chest</param>
			public WeaponJob(VillageLife owner, ArrowPlan weapon, string prefab, string name, bool forChest, bool wear = false) : base(owner)
			{
				weaponRun = new ItemRun(owner, null, (ItemDrop.ItemData item) => false, prefab, weapon);
				label = name;
				store = forChest;
				takeUp = wear;
			}

			/// <summary>
			/// Notes that the viking is arming itself, so no other viking of the village starts to
			/// </summary>
			public override void Begin()
			{
				life.SetDoing(ArmingKey, true);
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name</returns>
			public override string Describe()
			{
				return(label);
			}

			/// <summary>
			/// Checks whether the wood the viking is after lies outside the village
			/// </summary>
			/// <returns>True when it does</returns>
			public override bool Outing()
			{
				return(weaponRun.Outing());
			}

			/// <summary>
			/// Crafts the weapon, and carries one for the village's chests to put away
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has the weapon or can't get one, or it's dusk</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk())
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					nextRenew = Time.time + ClaimRenew;
					life.SetDoing(ArmingKey, true);
					weaponRun.Renew();
				}
				if (!weaponRun.Tick(dt, out ItemDrop.ItemData weapon))
				{
					return(false);
				}
				if ((store || takeUp) && weapon != null)
				{
					weapon.m_customData[VikingItems.CarryKey] = CarryLoot;
					life.humanoid.GetInventory().Changed();
				}
				if (takeUp && weapon != null)
				{
					life.TakeUpGear();
				}
				return(true);
			}

			/// <summary>
			/// Lets go of the station and the wood it was after, carries what it carries for crafting back to a chest, and
			/// notes it's no longer arming itself
			/// </summary>
			public override void End()
			{
				weaponRun.Release();
				life.SetDoing(ArmingKey, false);
			}
		}

		/// <summary>
		/// Lumberjane - cutting brush trees by day when the village is out of wood and badly needs some (see
		/// FindLumberWork): with no axe of its own, first taking one from a chest, or else crafting a stone axe - and
		/// while it can't, for want of wood, punching the trees instead, trying again with the wood each one drops. Then
		/// walking to the nearest tree, holding its axe, and chopping at it the way a player does until it falls and
		/// drops its wood, up to LumberTrees of them. The wood lies where it fell for the work that needs it to gather (see
		/// WoodRun), and an axe it took from a chest or crafted goes back in one, so the village keeps one in storage.
		/// Heading home as soon as an enemy comes near outside the village
		/// </summary>
		private class LumberJob : Job
		{
			private TreeBase tree;
			private ZDOID treeId;
			private ItemRun axeRun;
			private ItemDrop.ItemData borrowed;
			private int felled;
			private int swings;
			private float hitAt = -1f;
			private float nextSwing;
			private float nextRenew;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			/// <param name="first">The first tree it cuts</param>
			/// <param name="axeChest">The chest it takes an axe from, or null</param>
			/// <param name="axeCraft">What a stone axe takes to craft when it crafts one, or null</param>
			public LumberJob(VillageLife owner, TreeBase first, Container axeChest, ArrowPlan axeCraft) : base(owner)
			{
				Take(first);
				if (axeChest != null || axeCraft != null)
				{
					axeRun = new ItemRun(owner, axeChest, IsAxe, StoneAxePrefab, axeCraft);
				}
			}

			/// <summary>
			/// Notes that the viking is cutting trees, so no other viking of the village starts to
			/// </summary>
			public override void Begin()
			{
				life.SetDoing(LumberKey, true);
				nextRenew = Time.time + ClaimRenew;
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name, and how many trees it has cut</returns>
			public override string Describe()
			{
				return("Lumberjane (" + felled + "/" + LumberTrees + ")");
			}

			/// <summary>
			/// Checks whether the tree the viking is cutting, or the wood it's after for an axe, is outside the village
			/// </summary>
			/// <returns>True when it is</returns>
			public override bool Outing()
			{
				return((tree != null && !life.village.Contains(tree.transform.position)) || (axeRun != null && axeRun.Outing()));
			}

			/// <summary>
			/// Gets an axe when it has none, then walks to the tree and chops at it until it falls - or punches it, with no
			/// axe to be had yet - then goes on to the next
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it has cut LumberTrees, there's no tree left to cut, or it's dusk</returns>
			public override bool Tick(float dt)
			{
				if (IsNight() || IsDusk())
				{
					return(true);
				}
				if (Time.time >= nextRenew)
				{
					life.SetDoing(LumberKey, true);
					nextRenew = Time.time + ClaimRenew;
					if (axeRun != null)
					{
						axeRun.Renew();
					}
				}
				if (!life.village.Contains(life.transform.position) && life.DangerNear(life.transform.position))
				{
					life.fleeing = true;
					return(true);
				}
				if (axeRun != null && life.OwnAxe() == null)
				{
					if (!axeRun.Tick(dt, out ItemDrop.ItemData got))
					{
						return(false);
					}
					// With no axe to be had, it punches the trees, and tries crafting one again with the wood they drop
					axeRun.Release();
					axeRun = null;
					borrowed = (got ?? borrowed);
				}
				if (tree == null || tree.m_nview == null || !tree.m_nview.IsValid())
				{
					if (swings > 0)
					{
						felled++;
						TryCraftingAxe();
					}
					Release();
					if (felled >= LumberTrees)
					{
						return(true);
					}
					Take(life.FindBrushTree());
					if (tree == null)
					{
						return(true);
					}
					life.UpdateOuting(Outing());
				}
				Vector3 spot = tree.transform.position;
				if (hitAt >= 0f)
				{
					life.Face(spot);
					if (Time.time < hitAt)
					{
						return(false);
					}
					hitAt = -1f;
					bool punching = (life.OwnAxe() == null);
					life.ChopTree(tree);
					swings++;
					// A tree that won't fall - one it can't hurt, say - is left alone for a while
					if (swings >= (punching ? LumberPunchesMax : LumberSwingsMax))
					{
						life.Avoid(tree.m_nview);
						Release();
						swings = 0;
					}
					return(false);
				}
				if (Utils.DistanceXZ(spot, life.transform.position) > WorkReach)
				{
					life.Go(dt, spot, WorkReach, false);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(tree.m_nview);
						Release();
						swings = 0;
					}
					return(false);
				}
				life.Face(spot);
				ItemDrop.ItemData axe = life.OwnAxe();
				ItemDrop.ItemData held = null;
				if (axe != null && axe.m_dropPrefab != null)
				{
					life.HoldProp(axe.m_dropPrefab.name);
					held = life.humanoid.GetInventory().GetAllItems().Find((ItemDrop.ItemData item) => VikingItems.IsProp(item));
				}
				else
				{
					// A punch, with nothing in its hands
					life.HoldHammer(false);
					held = ((life.humanoid.m_unarmedWeapon != null) ? life.humanoid.m_unarmedWeapon.m_itemData : null);
				}
				if (Time.time < nextSwing)
				{
					return(false);
				}
				VikingPoses.Swing(life.humanoid, held);
				hitAt = Time.time + SwingHitTime;
				nextSwing = Time.time + SwingInterval;
				return(false);
			}

			/// <summary>
			/// Tries crafting a stone axe again, when the viking still has none and the village has what one takes
			/// besides the wood it's cutting
			/// </summary>
			private void TryCraftingAxe()
			{
				if (axeRun != null || life.OwnAxe() != null)
				{
					return;
				}
				ArrowPlan plan = life.PlanCraft(StoneAxePrefab, out string _);
				if (plan != null && (plan.Station == null || life.ClaimObject(plan.Station.GetComponent<ZNetView>())))
				{
					axeRun = new ItemRun(life, null, IsAxe, StoneAxePrefab, plan);
				}
			}

			/// <summary>
			/// Starts on a tree, claiming it so no other viking goes for it
			/// </summary>
			/// <param name="next">The tree, or null</param>
			private void Take(TreeBase next)
			{
				tree = next;
				swings = 0;
				hitAt = -1f;
				if (tree != null && tree.m_nview != null && tree.m_nview.IsValid())
				{
					treeId = tree.m_nview.GetZDO().m_uid;
					life.ClaimWork(treeId);
				}
			}

			/// <summary>
			/// Lets go of the tree it was cutting
			/// </summary>
			private void Release()
			{
				if (treeId != ZDOID.None)
				{
					life.ReleaseWork(treeId);
					treeId = ZDOID.None;
				}
				tree = null;
			}

			/// <summary>
			/// Lets go of the tree, puts the axe away - carrying one it took from a chest or crafted back to a chest - and
			/// notes it's no longer cutting trees, and that there may be wood lying about again, so the work that needs it
			/// looks for it
			/// </summary>
			public override void End()
			{
				Release();
				if (axeRun != null)
				{
					axeRun.Release();
					axeRun = null;
				}
				life.HoldHammer(false);
				if (borrowed != null && life.humanoid.GetInventory().ContainsItem(borrowed))
				{
					borrowed.m_customData[VikingItems.CarryKey] = CarryLoot;
					life.humanoid.GetInventory().Changed();
				}
				life.SetDoing(LumberKey, false);
				if (felled > 0)
				{
					life.noWoodUntil = 0f;
				}
			}
		}

		/// <summary>
		/// Lightening its load when it carries more than it can - the heaviest thing it can do without first (see
		/// HeaviestSpare), put away in the nearest chest with room within UnburdenReach, or else dropped where it stands,
		/// as many as it has to shed, until it's no longer held back
		/// </summary>
		private class UnburdenJob : Job
		{
			private ItemDrop.ItemData item;
			private Container chest;
			private float actAt = -1f;

			/// <summary>
			/// Makes the job
			/// </summary>
			/// <param name="owner">The viking's life</param>
			public UnburdenJob(VillageLife owner) : base(owner)
			{
			}

			/// <summary>
			/// Describes the job for the debug hover text
			/// </summary>
			/// <returns>Its name</returns>
			public override string Describe()
			{
				return("Lightening the load");
			}

			/// <summary>
			/// Puts away or drops what it can do without, a thing at a time
			/// </summary>
			/// <param name="dt">Seconds since the AI's last update</param>
			/// <returns>True once it carries no more than it can, or has nothing left it can do without</returns>
			public override bool Tick(float dt)
			{
				Inventory inventory = life.humanoid.GetInventory();
				if (inventory.GetTotalWeight() <= life.body.GetMaxCarryWeight())
				{
					return(true);
				}
				if (item == null || !inventory.ContainsItem(item))
				{
					item = life.HeaviestSpare();
					if (item == null)
					{
						return(true);
					}
					chest = life.UnburdenChest(item);
					actAt = -1f;
				}
				if (chest == null || !life.Usable(chest))
				{
					life.DropSpare(item);
					item = null;
					return(false);
				}
				if (!life.AtChest(chest))
				{
					life.GoToChest(dt, chest);
					if (life.moveFailed)
					{
						life.moveFailed = false;
						life.Avoid(chest.m_nview);
						chest = null;
					}
					return(false);
				}
				if (actAt < 0f)
				{
					life.Face(chest.transform.position);
					VikingPoses.Interact(life.humanoid);
					life.ShowChestOpen(chest);
					actAt = Time.time + ReachTime;
					return(false);
				}
				if (Time.time < actAt)
				{
					return(false);
				}
				// A chest that couldn't take it after all leaves it to be dropped
				if (!life.StoreSpare(chest, item))
				{
					chest = null;
					return(false);
				}
				item = null;
				return(false);
			}
		}
	}
}
