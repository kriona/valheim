using System.Collections.Generic;
using Kriona.Vikings;
using UnityEngine;

namespace VikingVillages
{
	/// <summary>
	/// A PC viking's life in its village - what it does with nothing to fight, chosen by the time of day, on the
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
		/// On a PC viking, that it came to its village from outside the fence and has yet to walk in
		/// </summary>
		public static readonly int ArrivingKey = "kriona.VikingVillages.vikingArriving".GetStableHashCode();

		/// <summary>
		/// On a PC viking, when it was last found away from its village, in ZNet time ticks, or 0 while it's there
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
		/// On a PC viking, until when in ZNet time ticks it's stocking its village's chests with wood, until when
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
		/// On a PC viking, until when in ZNet time ticks it's out of its village or heading out, renewed every
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
		/// How near a PC viking a monster outside the village has to come to be a threat it goes after, when the
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
		/// The farthest the spot a viking lay down in its bed from can be from where it lies, for it to get up onto
		/// that spot again, in meters - the length of a bed and a stride past its foot, more than any spot it lies
		/// down from, so a spot farther off means the bed was moved, like on a ship
		/// </summary>
		private const float BedLeaveReach = 3f;

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
		/// How far a PC viking sees, in meters
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
		/// The most the ground can fall away from the viking's feet, behind it or as it backs up, for it to back up for a
		/// run-up to a jump, in meters - on a slope falling away from a fence it would slide down it, out of reach of the
		/// fence's top
		/// </summary>
		private const float RunUpDrop = 0.3f;

		/// <summary>
		/// How a viking looks for a side of a pen's fence it can jump into the pen from (see PenWayIn) - how many level
		/// rays it casts out from what it's after in the pen, at what height and how far, how far past the fence's face
		/// the spot outside it stands at, how much lower than the viking can jump the fence's top has to stand above that
		/// spot, how near the spot it has to get before it jumps, and how long an answer is kept, in meters and seconds
		/// </summary>
		private const int PenWayRays = 12;
		private const float PenWayHeight = 0.5f;
		private const float PenWayReach = 8f;
		private const float PenWayOut = 1f;
		private const float PenWayMargin = 0.1f;
		private const float PenWayArrive = 0.6f;
		private const float PenWayRemember = 10f;

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
		/// enemies outnumber it, the least share of its health below which it's badly hurt, the share a healthy viking
		/// has, how long after it was hurt it counts as attacked, and seconds between looks
		/// </summary>
		private const float AllyRange = 15f;
		private const float EnemyRange = 15f;
		private const int FightOutnumbered = 4;
		private const float BadlyHurtShare = 0.25f;
		private const float HealthyShare = 0.5f;

		/// <summary>
		/// How the next hit raises the share of its health below which a viking is badly hurt (see BadlyHurtLine) - how
		/// many of the hardest hit of an enemy fighting near it, through its armor, it wants to have left, the highest
		/// share that puts the line at, so a monster that kills in one hit anyway is left to the tough-monster weigh-up
		/// (see VikingCore.IsTough) rather than having vikings hang back from near full health, and how far above the
		/// line it has to climb to count as recovered, so it doesn't flicker in and out of hanging back
		/// </summary>
		private const float NextHitMargin = 1.5f;
		private const float BadlyHurtCap = 0.6f;
		private const float RecoveredMargin = 0.15f;

		/// <summary>
		/// The share of its health below which the viking is badly hurt as it last weighed up its fight (see
		/// WeighFight), BadlyHurtShare outside a fight
		/// </summary>
		private float badlyHurtLine = BadlyHurtShare;
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
		/// Each enemy's hardest hit before armor (see MonsterThreat.Hit), by its ZDO, with when it's weighed again, by
		/// Time.time, since its gear can change - all forgotten once there are more than RangedCacheMax - and how long
		/// one is kept, in seconds
		/// </summary>
		private static readonly Dictionary<ZDOID, KeyValuePair<float, float>> hitCache = new Dictionary<ZDOID, KeyValuePair<float, float>>();
		private const float HitCacheTime = 10f;

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
		/// The PC vikings this client has loaded
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
		private PlayerLikeVikingBody body;

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
		/// When a viking fighting a stone golem it scolds next scolds it, in Time.time seconds, and the line it said last,
		/// or -1 (see Scold)
		/// </summary>
		private float nextScold;
		private int lastScold = -1;

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
		/// How high the viking's feet stood when it started its jump over a fence, which it stops backing up once it has
		/// slid more than RunUpDrop below
		/// </summary>
		private float hopFrom;

		/// <summary>
		/// The way into a pen the viking is walking to (see PenWayIn) - what in the pen it's after, the spot outside the
		/// fence it jumps from, whether it found one, until when that answer is kept, and the spots it has already tried
		/// </summary>
		private Vector3 penWayTarget = Vector3.positiveInfinity;
		private Vector3 penWaySpot;
		private bool penWayFound;
		private float penWayUntil;
		private readonly List<Vector3> penWayTried = new List<Vector3>();

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
		/// For the log of why the viking's last try at a jump wasn't made (see LogHopRefused) - why, or null when it was
		/// made, where it stood and the way it would have jumped, what was in front of its knees and where, and whether
		/// it's been written for the time it's been stuck
		/// </summary>
		private string hopRefused;
		private Vector3 hopRefusedFeet;
		private Vector3 hopRefusedDirection;
		private Collider hopRefusedThing;
		private Vector3 hopRefusedPoint;
		private bool hopRefusedLogged;

		/// <summary>
		/// How a jump over a fence ended, for the log (see HopResult) - with the viking past the fence's face by at least
		/// HopOverPast, in meters, with its feet rising to within HopOnTopDrop of the fence's top on the way, or short of
		/// the face with its feet within HopOnTopDrop of the top
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
		/// Where a jump over a fence left the viking - past the fence, on top of it, back on the side it jumped from, or
		/// past the fence's face without its feet ever getting near its top, having gone around the fence's end or slid
		/// past it
		/// </summary>
		internal enum HopOutcome
		{
			Over,
			OnTop,
			FellBack,
			Around
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
			body = GetComponent<PlayerLikeVikingBody>();
			if (ai != null)
			{
				// A PC viking keeps a sharp lookout, so it spots monsters before they're on it
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
		/// time (see VikingIdentity) - and follows it after a hit for the test log, for being flung (see CheckFling)
		/// </summary>
		private void FixedUpdate()
		{
			CheckFling();
			bool owner = (nview != null && nview.IsValid() && nview.IsOwner());
			if (owner && !hasId)
			{
				hasId = (VikingIdentity.Ensure(nview).Length > 0);
			}
			if (attachPoint != null && owner)
			{
				HoldAttached();
			}
			if (owner && body != null && VikingCore.IsPlayerLikeViking(body))
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
			// An NPC viking runs inside from a fight, only fighting back with its knife when it's caught
			if (Peaceful())
			{
				ShelterMove shelter = UpdateShelter(dt, target);
				if (shelter == ShelterMove.Handled)
				{
					return(true);
				}
				if (shelter == ShelterMove.Fight)
				{
					lastFightAt = Time.time;
					return(false);
				}
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
				Scold(target);
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
		/// A hunter doesn't call out its prey, and one that scolds the enemy scolds it in place of calling it out (see
		/// Scold)
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
			string line = (ScoldsAt(enemy) ? ScoldLine() : VikingSpeech.Spotted(enemy));
			VikingSpeech.SayFirst(humanoid, line, VikingSpeech.CallTime, VikingSpeech.CallCull);
		}

		/// <summary>
		/// Has a viking named Elsa fighting a stone golem scold it as Marshmallow, the snow giant she made in the film
		/// Frozen - every ScoldMin to ScoldMax seconds while she fights it, a different line each time (see
		/// VikingFrozenSpeech)
		/// </summary>
		/// <param name="enemy">The enemy it fights</param>
		private void Scold(Character enemy)
		{
			if (Time.time < nextScold || !ScoldsAt(enemy))
			{
				return;
			}
			VikingSpeech.Say(humanoid, ScoldLine(), VikingSpeech.CallTime, VikingSpeech.CallCull);
		}

		/// <summary>
		/// Checks whether the viking scolds an enemy as Marshmallow
		/// </summary>
		/// <param name="enemy">The enemy</param>
		/// <returns>True when it does</returns>
		private bool ScoldsAt(Character enemy)
		{
			return(enemy != null && VikingFrozenSpeech.Scolds(VikingNames.Get(humanoid), Utils.GetPrefabName(enemy.gameObject)));
		}

		/// <summary>
		/// Picks the next line the viking scolds a stone golem with, and when it scolds it again
		/// </summary>
		/// <returns>The line</returns>
		private string ScoldLine()
		{
			nextScold = Time.time + Random.Range(VikingFrozenSpeech.ScoldMin, VikingFrozenSpeech.ScoldMax);
			lastScold = VikingFrozenSpeech.PickScold(lastScold, (int count) => Random.Range(0, count));
			return(VikingFrozenSpeech.MarshmallowLines[lastScold]);
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
			UpdateFree();
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
	}
}
